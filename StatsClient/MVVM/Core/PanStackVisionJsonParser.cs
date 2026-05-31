using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.RegularExpressions;
using StatsClient.MVVM.Model;

namespace StatsClient.MVVM.Core;

/// <summary>
/// Parses pan-stack vision JSON, including truncated model output.
/// </summary>
internal static class PanStackVisionJsonParser
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        AllowTrailingCommas = true,
        ReadCommentHandling = JsonCommentHandling.Skip
    };

    private static readonly Regex NumberFieldRegex = new(
        @"""number""\s*:\s*""([^""]+)""",
        RegexOptions.Compiled | RegexOptions.CultureInvariant | RegexOptions.IgnoreCase);

    private static readonly Regex ConfidenceFieldRegex = new(
        @"""confidence""\s*:\s*([0-9.]+)",
        RegexOptions.Compiled | RegexOptions.CultureInvariant | RegexOptions.IgnoreCase);

    private static readonly Regex CenterXFieldRegex = new(
        @"""centerX""\s*:\s*([0-9.]+)",
        RegexOptions.Compiled | RegexOptions.CultureInvariant | RegexOptions.IgnoreCase);

    private static readonly Regex CenterYFieldRegex = new(
        @"""centerY""\s*:\s*([0-9.]+)",
        RegexOptions.Compiled | RegexOptions.CultureInvariant | RegexOptions.IgnoreCase);

    private static readonly Regex ColumnIndexRegex = new(
        @"""columnIndex""\s*:\s*(\d+)",
        RegexOptions.Compiled | RegexOptions.CultureInvariant);

    private static readonly Regex LabelObjectRegex = new(
        @"\{\s*""number""\s*:\s*""([^""]+)""\s*,\s*""confidence""\s*:\s*([0-9.]+)(?:\s*,\s*""centerX""\s*:\s*([0-9.]+))?(?:\s*,\s*""centerY""\s*:\s*([0-9.]+))?\s*\}",
        RegexOptions.Compiled | RegexOptions.CultureInvariant);

    public static bool TryParseMatrix(
        string content,
        double minimumConfidence,
        out List<PanStackVisionColumnData> columns,
        out int skippedLowConfidence,
        out string error)
    {
        columns = [];
        skippedLowConfidence = 0;
        error = string.Empty;

        if (string.IsNullOrWhiteSpace(content))
        {
            error = "Vision model returned empty content.";
            return false;
        }

        var json = RepairMalformedLabelJson(ExtractJsonPayload(content));
        if (string.IsNullOrWhiteSpace(json))
        {
            error = "Could not find JSON in vision response.";
            return false;
        }

        try
        {
            if (TryParseRepairedJson(json, minimumConfidence, out columns, out skippedLowConfidence))
            {
                return true;
            }
        }
        catch (Exception ex)
        {
            if (TrySalvageLabelsFlexible(json, minimumConfidence, out columns, out skippedLowConfidence))
            {
                return FinalizeParse(columns, ref skippedLowConfidence);
            }

            error = $"Vision JSON parse error: {ex.Message}";
            return false;
        }

        if (TrySalvageLabelsFlexible(json, minimumConfidence, out columns, out skippedLowConfidence)
            || TrySalvagePartialMatrix(json, minimumConfidence, out columns, out skippedLowConfidence))
        {
            return FinalizeParse(columns, ref skippedLowConfidence);
        }

        error = "Vision JSON was invalid or cut off (partial labels may be missing).";
        return false;
    }

    private static bool TryParseRepairedJson(
        string json,
        double minimumConfidence,
        out List<PanStackVisionColumnData> columns,
        out int skippedLowConfidence)
    {
        columns = [];
        skippedLowConfidence = 0;

        if (TryDeserializeMatrix(json, minimumConfidence, out columns, out skippedLowConfidence))
        {
            return FinalizeParse(columns, ref skippedLowConfidence);
        }

        var closed = RepairMalformedLabelJson(TryCloseTruncatedJson(json));
        if (!string.Equals(closed, json, StringComparison.Ordinal)
            && TryDeserializeMatrix(closed, minimumConfidence, out columns, out skippedLowConfidence))
        {
            return FinalizeParse(columns, ref skippedLowConfidence);
        }

        if (TrySalvageLabelsFlexible(json, minimumConfidence, out columns, out skippedLowConfidence)
            || TrySalvageLabelsFlexible(closed, minimumConfidence, out columns, out skippedLowConfidence))
        {
            return FinalizeParse(columns, ref skippedLowConfidence);
        }

        var trimmed = RepairMalformedLabelJson(TrimToLastLabelObject(json));
        if (!string.Equals(trimmed, json, StringComparison.Ordinal)
            && TryDeserializeMatrix(RepairMalformedLabelJson(TryCloseTruncatedJson(trimmed)), minimumConfidence, out columns, out skippedLowConfidence))
        {
            return FinalizeParse(columns, ref skippedLowConfidence);
        }

        return TrySalvagePartialMatrix(json, minimumConfidence, out columns, out skippedLowConfidence)
               && FinalizeParse(columns, ref skippedLowConfidence);
    }

    private static bool FinalizeParse(List<PanStackVisionColumnData> columns, ref int skippedLowConfidence)
    {
        return columns.Sum(column => column.Labels.Count) > 0;
    }

    private static string RepairMalformedLabelJson(string json)
    {
        if (string.IsNullOrEmpty(json))
        {
            return json;
        }

        json = Regex.Replace(json, @"\}""\s*\]", "}]", RegexOptions.CultureInvariant);
        json = Regex.Replace(
            json,
            @"(""center[XY]""\s*:\s*[0-9.]+)""(\s*[\}\]])",
            "$1$2",
            RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);
        json = Regex.Replace(
            json,
            @"([0-9]+\.?[0-9]*)\s*""\s*(\})",
            "$1$2",
            RegexOptions.CultureInvariant);
        return json;
    }

    private static bool TryDeserializeMatrix(
        string json,
        double minimumConfidence,
        out List<PanStackVisionColumnData> columns,
        out int skippedLowConfidence)
    {
        columns = [];
        skippedLowConfidence = 0;

        try
        {
            var trimmed = json.TrimStart();
            if (trimmed.StartsWith('['))
            {
                return TryDeserializeColumnArray(trimmed, minimumConfidence, columns, ref skippedLowConfidence);
            }

            var payload = JsonSerializer.Deserialize<PanStackVisionPayload>(json, JsonOptions);

            if (payload is null)
            {
                return false;
            }

            if (payload.Labels is { Count: > 0 })
            {
                BuildColumnsFromFlatLabels(payload.Labels, minimumConfidence, columns, ref skippedLowConfidence);
                return columns.Count > 0;
            }

            if (payload.Columns is not { Count: > 0 } rawColumns)
            {
                return false;
            }

            BuildColumnsFromDtos(rawColumns, minimumConfidence, columns, ref skippedLowConfidence);
            return columns.Count > 0;
        }
        catch (JsonException)
        {
            return false;
        }
        catch (Exception)
        {
            return false;
        }
    }

    /// <summary>Model sometimes returns a bare JSON array of column objects instead of {"columns":[...]}.</summary>
    private static bool TryDeserializeColumnArray(
        string json,
        double minimumConfidence,
        List<PanStackVisionColumnData> columns,
        ref int skippedLowConfidence)
    {
        var rawColumns = JsonSerializer.Deserialize<List<PanStackVisionColumnDto>>(json, JsonOptions);
        if (rawColumns is not { Count: > 0 })
        {
            return false;
        }

        // Flatten — model often sends horizontal rows as repeated columnIndex groups; grid snap uses centerX/Y.
        var flat = new PanStackVisionColumnData { ColumnIndex = 1 };
        foreach (var rawColumn in rawColumns)
        {
            foreach (var label in GetColumnLabels(rawColumn))
            {
                if (!TryNormalizeNumber(label.Number, out var digits))
                {
                    continue;
                }

                var confidence = label.Confidence ?? 0;
                if (confidence < minimumConfidence)
                {
                    skippedLowConfidence++;
                    continue;
                }

                flat.Labels.Add(new PanStackVisionCell
                {
                    Number = digits,
                    Confidence = confidence,
                    CenterX = Clamp01(label.CenterX),
                    CenterY = Clamp01(label.CenterY)
                });
            }
        }

        if (flat.Labels.Count == 0)
        {
            return false;
        }

        columns.Add(flat);
        return true;
    }

    /// <summary>Salvage labels when JSON has bad quotes, field reordering, or truncation mid-stream.</summary>
    private static bool TrySalvageLabelsFlexible(
        string json,
        double minimumConfidence,
        out List<PanStackVisionColumnData> columns,
        out int skippedLowConfidence)
    {
        columns = [];
        skippedLowConfidence = 0;

        var numberMatches = NumberFieldRegex.Matches(json);
        if (numberMatches.Count == 0)
        {
            return false;
        }

        var column = new PanStackVisionColumnData { ColumnIndex = 1 };
        foreach (Match numberMatch in numberMatches)
        {
            if (!TryNormalizeNumber(numberMatch.Groups[1].Value, out var digits))
            {
                continue;
            }

            var windowStart = numberMatch.Index;
            var windowEnd = Math.Min(json.Length, windowStart + 220);
            var window = json[windowStart..windowEnd];

            var confidence = 0.0;
            var confidenceMatch = ConfidenceFieldRegex.Match(window);
            if (confidenceMatch.Success)
            {
                double.TryParse(confidenceMatch.Groups[1].Value, System.Globalization.NumberStyles.Float,
                    System.Globalization.CultureInfo.InvariantCulture, out confidence);
            }

            if (confidence < minimumConfidence)
            {
                skippedLowConfidence++;
                continue;
            }

            double? centerX = null;
            double? centerY = null;
            var xMatch = CenterXFieldRegex.Match(window);
            if (xMatch.Success
                && double.TryParse(xMatch.Groups[1].Value, System.Globalization.NumberStyles.Float,
                    System.Globalization.CultureInfo.InvariantCulture, out var parsedX))
            {
                centerX = Clamp01(parsedX);
            }

            var yMatch = CenterYFieldRegex.Match(window);
            if (yMatch.Success
                && double.TryParse(yMatch.Groups[1].Value, System.Globalization.NumberStyles.Float,
                    System.Globalization.CultureInfo.InvariantCulture, out var parsedY))
            {
                centerY = Clamp01(parsedY);
            }

            column.Labels.Add(new PanStackVisionCell
            {
                Number = digits,
                Confidence = confidence,
                CenterX = centerX,
                CenterY = centerY
            });
        }

        if (column.Labels.Count == 0)
        {
            return false;
        }

        columns.Add(column);
        return columns.Count > 0;
    }

    private static string TrimToLastLabelObject(string json)
    {
        var lastNumber = json.LastIndexOf("\"number\"", StringComparison.OrdinalIgnoreCase);
        if (lastNumber < 0)
        {
            return json;
        }

        var slice = json[lastNumber..];
        var endBrace = slice.IndexOf('}');
        if (endBrace < 0)
        {
            return json[..lastNumber] + slice;
        }

        return json[..(lastNumber + endBrace + 1)];
    }

    private static bool TrySalvagePartialMatrix(
        string json,
        double minimumConfidence,
        out List<PanStackVisionColumnData> columns,
        out int skippedLowConfidence)
    {
        columns = [];
        skippedLowConfidence = 0;

        var columnMarkers = ColumnIndexRegex.Matches(json)
            .Select(match => (Index: int.Parse(match.Groups[1].Value, System.Globalization.CultureInfo.InvariantCulture), Position: match.Index))
            .OrderBy(pair => pair.Position)
            .ToList();

        var labelMatches = LabelObjectRegex.Matches(json);
        if (labelMatches.Count == 0)
        {
            return false;
        }

        var byColumn = new Dictionary<int, PanStackVisionColumnData>();

        foreach (Match labelMatch in labelMatches)
        {
            if (!TryNormalizeNumber(labelMatch.Groups[1].Value, out var digits))
            {
                continue;
            }

            if (!double.TryParse(labelMatch.Groups[2].Value, System.Globalization.NumberStyles.Float,
                    System.Globalization.CultureInfo.InvariantCulture, out var confidence))
            {
                confidence = 0;
            }

            if (confidence < minimumConfidence)
            {
                skippedLowConfidence++;
                continue;
            }

            double? centerX = null;
            double? centerY = null;
            if (labelMatch.Groups[3].Success
                && double.TryParse(labelMatch.Groups[3].Value, System.Globalization.NumberStyles.Float,
                    System.Globalization.CultureInfo.InvariantCulture, out var parsedX))
            {
                centerX = Clamp01(parsedX);
            }

            if (labelMatch.Groups[4].Success
                && double.TryParse(labelMatch.Groups[4].Value, System.Globalization.NumberStyles.Float,
                    System.Globalization.CultureInfo.InvariantCulture, out var parsedY))
            {
                centerY = Clamp01(parsedY);
            }

            var columnIndex = 1;
            foreach (var marker in columnMarkers)
            {
                if (marker.Position < labelMatch.Index)
                {
                    columnIndex = marker.Index;
                }
                else
                {
                    break;
                }
            }

            if (!byColumn.TryGetValue(columnIndex, out var column))
            {
                column = new PanStackVisionColumnData { ColumnIndex = columnIndex };
                byColumn[columnIndex] = column;
            }

            column.Labels.Add(new PanStackVisionCell
            {
                Number = digits,
                Confidence = confidence,
                CenterX = centerX,
                CenterY = centerY
            });
        }

        columns = byColumn.Values.OrderBy(column => column.ColumnIndex).ToList();
        return columns.Count > 0;
    }

    private static void BuildColumnsFromFlatLabels(
        List<PanStackVisionLabelDto> flatLabels,
        double minimumConfidence,
        List<PanStackVisionColumnData> columns,
        ref int skippedLowConfidence)
    {
        var column = new PanStackVisionColumnData { ColumnIndex = 1 };
        foreach (var label in flatLabels)
        {
            if (!TryNormalizeNumber(label.Number, out var digits))
            {
                continue;
            }

            var confidence = label.Confidence ?? 0;
            if (confidence < minimumConfidence)
            {
                skippedLowConfidence++;
                continue;
            }

            column.Labels.Add(new PanStackVisionCell
            {
                Number = digits,
                Confidence = confidence,
                CenterX = Clamp01(label.CenterX),
                CenterY = Clamp01(label.CenterY)
            });
        }

        if (column.Labels.Count > 0)
        {
            columns.Add(column);
        }
    }

    private static void BuildColumnsFromDtos(
        List<PanStackVisionColumnDto> rawColumns,
        double minimumConfidence,
        List<PanStackVisionColumnData> columns,
        ref int skippedLowConfidence)
    {
        var orderedColumns = rawColumns
            .Select((column, index) => (column, index))
            .OrderBy(pair => pair.column.ColumnIndex ?? (pair.index + 1))
            .ToList();

        var columnIndex = 1;
        foreach (var (rawColumn, _) in orderedColumns)
        {
            var column = new PanStackVisionColumnData
            {
                ColumnIndex = rawColumn.ColumnIndex ?? columnIndex
            };

            foreach (var label in GetColumnLabels(rawColumn))
            {
                if (!TryNormalizeNumber(label.Number, out var digits))
                {
                    continue;
                }

                var confidence = label.Confidence ?? 0;
                if (confidence < minimumConfidence)
                {
                    skippedLowConfidence++;
                    continue;
                }

                column.Labels.Add(new PanStackVisionCell
                {
                    Number = digits,
                    Confidence = confidence,
                    CenterX = Clamp01(label.CenterX),
                    CenterY = Clamp01(label.CenterY)
                });
            }

            if (column.Labels.Count > 0)
            {
                columns.Add(column);
            }

            columnIndex++;
        }
    }

    private static string ExtractJsonPayload(string content)
    {
        content = content.Trim();
        if (content.StartsWith("```", StringComparison.Ordinal))
        {
            var firstLine = content.IndexOf('\n');
            if (firstLine >= 0)
            {
                content = content[(firstLine + 1)..];
            }

            var fence = content.LastIndexOf("```", StringComparison.Ordinal);
            if (fence > 0)
            {
                content = content[..fence];
            }

            content = content.Trim();
        }

        var objectStart = content.IndexOf('{');
        var arrayStart = content.IndexOf('[');

        if (objectStart < 0 && arrayStart < 0)
        {
            return string.Empty;
        }

        // Prefer whichever JSON root token appears first (array vs object wrapper).
        if (arrayStart >= 0 && (objectStart < 0 || arrayStart < objectStart))
        {
            return content[arrayStart..].Trim();
        }

        return content[objectStart..].Trim();
    }

    /// <summary>Appends missing ] and } for responses cut off mid-stream.</summary>
    private static string TryCloseTruncatedJson(string json)
    {
        var stack = new Stack<char>();
        var inString = false;
        var escaped = false;

        foreach (var ch in json)
        {
            if (inString)
            {
                if (escaped)
                {
                    escaped = false;
                    continue;
                }

                if (ch == '\\')
                {
                    escaped = true;
                    continue;
                }

                if (ch == '"')
                {
                    inString = false;
                }

                continue;
            }

            switch (ch)
            {
                case '"':
                    inString = true;
                    break;
                case '{':
                    stack.Push('}');
                    break;
                case '[':
                    stack.Push(']');
                    break;
                case '}':
                case ']':
                    if (stack.Count > 0 && stack.Peek() == ch)
                    {
                        stack.Pop();
                    }

                    break;
            }
        }

        if (!inString && stack.Count == 0)
        {
            return json;
        }

        var sb = new StringBuilder(json);
        if (inString)
        {
            sb.Append('"');
        }

        while (stack.Count > 0)
        {
            sb.Append(stack.Pop());
        }

        return sb.ToString();
    }

    private static IEnumerable<PanStackVisionLabelDto> GetColumnLabels(PanStackVisionColumnDto column)
    {
        if (column.Labels is { Count: > 0 })
        {
            return column.Labels;
        }

        if (column.Entries is { Count: > 0 })
        {
            return column.Entries;
        }

        if (column.Numbers is not { Count: > 0 })
        {
            return [];
        }

        return column.Numbers.Select(number => new PanStackVisionLabelDto
        {
            Number = number,
            Confidence = 0
        });
    }

    private static bool TryNormalizeNumber(string? raw, out string digits)
    {
        digits = string.Empty;
        if (string.IsNullOrWhiteSpace(raw))
        {
            return false;
        }

        digits = Regex.Replace(raw.Trim(), @"[^\d]", string.Empty);
        return digits.Length is > 0 and <= 5;
    }

    private static double? Clamp01(double? value)
    {
        if (!value.HasValue || double.IsNaN(value.Value))
        {
            return null;
        }

        var normalized = value.Value;
        if (normalized < 0)
        {
            normalized = 0;
        }

        if (normalized > 1.01)
        {
            // Some vision models return 0–1000 or 0–100 instead of 0–1.
            normalized = normalized > 100 ? normalized / 1000.0 : normalized / 100.0;
        }

        return Math.Clamp(normalized, 0, 1);
    }

    private sealed class PanStackVisionPayload
    {
        [JsonPropertyName("columns")]
        public List<PanStackVisionColumnDto>? Columns { get; set; }

        [JsonPropertyName("labels")]
        public List<PanStackVisionLabelDto>? Labels { get; set; }
    }

    private sealed class PanStackVisionColumnDto
    {
        [JsonPropertyName("columnIndex")]
        public int? ColumnIndex { get; set; }

        [JsonPropertyName("labels")]
        public List<PanStackVisionLabelDto>? Labels { get; set; }

        [JsonPropertyName("entries")]
        public List<PanStackVisionLabelDto>? Entries { get; set; }

        [JsonPropertyName("numbers")]
        public List<string>? Numbers { get; set; }
    }

    private sealed class PanStackVisionLabelDto
    {
        [JsonPropertyName("number")]
        public string? Number { get; set; }

        [JsonPropertyName("confidence")]
        public double? Confidence { get; set; }

        [JsonPropertyName("centerX")]
        public double? CenterX { get; set; }

        [JsonPropertyName("centerY")]
        public double? CenterY { get; set; }
    }
}
