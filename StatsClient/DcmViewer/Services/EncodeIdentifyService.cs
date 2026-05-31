using System.IO;
using System.Diagnostics;
using System.Text.Json;
using System.Text.Json.Serialization;
using StatsClient.MVVM.Core;
using static StatsClient.MVVM.ViewModel.MainViewModel;

namespace DCMViewer.Services;

public sealed class EncodeCapIdentifyResult
{
    public bool Success { get; init; }
    public string ErrorMessage { get; init; } = string.Empty;
    public string Profile { get; init; } = string.Empty;
    public string Family { get; init; } = string.Empty;
    public int? CenterGrooves { get; init; }
    public double MeasuredDiameterMm { get; init; }
    public double PlatformMm { get; init; }
    public string ThreeShapeSuggestion { get; init; } = string.Empty;
    public double Confidence { get; init; }
    public string Notes { get; init; } = string.Empty;

    /// <summary>Triple section-cut diameter summary (0° / 120° / 240°).</summary>
    public string MeasurementSummary { get; set; } = string.Empty;

    /// <summary>PNG screenshots of each diameter section cut (0°, 120°, 240°).</summary>
    public IReadOnlyList<EncodeMeasureCutSnapshot> CutSnapshots { get; init; } = [];

    /// <summary>Vision API debug log text from the last call.</summary>
    public string VisionDebugLog { get; init; } = string.Empty;

    /// <summary>Full path to the debug log file written beside StatsClient.exe.</summary>
    public string VisionDebugLogFilePath { get; init; } = string.Empty;
}

public sealed class EncodeMeasureCutSnapshot
{
    public double AngleDegrees { get; init; }
    public double DiameterMm { get; init; }
    public bool Succeeded { get; init; }
    public byte[] Png { get; init; } = [];
}

internal static class EncodeIdentifyService
{
    private static readonly JsonSerializerOptions JsonOptions = new() { PropertyNameCaseInsensitive = true };
    private static readonly NvidiaVisionService Vision = new();

    public static async Task<EncodeCapIdentifyResult> IdentifyFromScreenshotAsync(
        byte[] viewportPng,
        double measuredDiameterMm,
        CancellationToken cancellationToken = default)
    {
        string apiKey = DatabaseConnection.ReadStatsSetting("Nvidia_API_KEY");
        if (string.IsNullOrWhiteSpace(apiKey))
        {
            return Fail("Nvidia_API_KEY is not set in Stats database Settings.");
        }

        string endpoint = DatabaseConnection.ReadStatsSetting(SettingEncodeIdentifierVisionEndpoint);
        if (string.IsNullOrWhiteSpace(endpoint))
        {
            endpoint = DefaultEncodeIdentifierVisionEndpoint;
        }
        else
        {
            // Migrate old VLM endpoint to the new chat-completions endpoint automatically.
            if (endpoint.Contains("ai.api.nvidia.com/v1/vlm", StringComparison.OrdinalIgnoreCase))
            {
                endpoint = DefaultEncodeIdentifierVisionEndpoint;
            }
        }

        _ = int.TryParse(DatabaseConnection.ReadStatsSetting(SettingEncodeIdentifierMaxTokens), out int maxTokens);
        if (maxTokens <= 0)
        {
            maxTokens = 512;
        }

        _ = double.TryParse(DatabaseConnection.ReadStatsSetting(SettingEncodeIdentifierTemperature), out double temperature);
        if (temperature <= 0)
        {
            temperature = 0.2;
        }

        _ = double.TryParse(DatabaseConnection.ReadStatsSetting(SettingEncodeIdentifierTopP), out double topP);
        if (topP <= 0)
        {
            topP = 0.7;
        }

        var vision = await Vision.AnalyzeImageAsync(
            apiKey,
            endpoint,
            viewportPng,
            NvidiaVisionService.BuildEncodeCapVisionPrompt(),
            maxTokens,
            temperature,
            topP,
            cancellationToken).ConfigureAwait(false);

        string debugLog = vision.ToDebugLog();
        string? logFilePath = WriteVisionDebugFile(debugLog);
        if (logFilePath is null)
        {
            debugLog = AppendLogWriteFailureNote(debugLog);
        }

        if (!vision.IsSuccess)
        {
            return Fail(
                string.IsNullOrWhiteSpace(vision.ErrorSummary)
                    ? "Vision model returned no response. Check endpoint and API key."
                    : $"Vision API: {vision.ErrorSummary}",
                debugLog,
                logFilePath);
        }

        var features = ParseVisionJson(vision.Content);
        if (features is null)
        {
            return Fail("Could not parse vision model JSON response.", debugLog, logFilePath);
        }

        var resolved = Resolve(features, measuredDiameterMm, vision.Content);
        resolved = new EncodeCapIdentifyResult
        {
            Success = resolved.Success,
            ErrorMessage = resolved.ErrorMessage,
            Profile = resolved.Profile,
            Family = resolved.Family,
            CenterGrooves = resolved.CenterGrooves,
            MeasuredDiameterMm = resolved.MeasuredDiameterMm,
            PlatformMm = resolved.PlatformMm,
            ThreeShapeSuggestion = resolved.ThreeShapeSuggestion,
            Confidence = resolved.Confidence,
            Notes = resolved.Notes,
            MeasurementSummary = resolved.MeasurementSummary,
            CutSnapshots = resolved.CutSnapshots,
            VisionDebugLog = debugLog,
            VisionDebugLogFilePath = logFilePath ?? string.Empty
        };
        return resolved;
    }

    private static EncodeCapIdentifyResult Resolve(EncodeVisionFeatures features, double measuredDiameterMm, string rawNotes)
    {
        if (!Enum.TryParse<EncodeCapProfile>(features.Profile, true, out var profile))
        {
            profile = EncodeCapProfile.Unknown;
        }

        EncodeCapFamily family = features.CenterGrooves switch
        {
            2 => EncodeCapFamily.Certain,
            3 => EncodeCapFamily.Tsv,
            _ => EncodeCapFamily.Unknown
        };

        if (Enum.TryParse<EncodeCapFamily>(features.Family, true, out var parsedFamily))
        {
            family = parsedFamily;
        }

        double platformMm = measuredDiameterMm;
        if (profile == EncodeCapProfile.Emergence && family != EncodeCapFamily.Unknown && features.DimplesBelowLine is int below)
        {
            if (EncodeCapRuleCatalog.EmergencePlatformFromDimplesBelow(family, below) is double fromDimples)
            {
                platformMm = fromDimples;
            }
        }

        int? emergenceHeight = features.DimplesRight is int right
            ? EncodeCapRuleCatalog.EmergenceHeightFromRightDimples(right)
            : null;

        string? suggestion = EncodeCapRuleCatalog.ResolveThreeShapeSuggestion(
            profile,
            family,
            platformMm,
            emergenceHeight,
            features.LegacyHeightMm);

        if (string.IsNullOrEmpty(suggestion))
        {
            return new EncodeCapIdentifyResult
            {
                Success = false,
                ErrorMessage = "No matching 3Shape library entry for detected features and measured diameter.",
                Profile = features.Profile ?? string.Empty,
                Family = family.ToString(),
                CenterGrooves = features.CenterGrooves,
                MeasuredDiameterMm = measuredDiameterMm,
                PlatformMm = platformMm,
                Confidence = features.Confidence,
                Notes = features.Notes ?? rawNotes
            };
        }

        return new EncodeCapIdentifyResult
        {
            Success = true,
            Profile = features.Profile ?? string.Empty,
            Family = family.ToString(),
            CenterGrooves = features.CenterGrooves,
            MeasuredDiameterMm = measuredDiameterMm,
            PlatformMm = platformMm,
            ThreeShapeSuggestion = suggestion,
            Confidence = features.Confidence,
            Notes = features.Notes ?? string.Empty
        };
    }

    private static EncodeCapIdentifyResult Fail(string message, string? visionDebugLog = null, string? logFilePath = null) =>
        new()
        {
            Success = false,
            ErrorMessage = message,
            VisionDebugLog = visionDebugLog ?? string.Empty,
            VisionDebugLogFilePath = logFilePath ?? string.Empty
        };

    private static string AppendLogWriteFailureNote(string debugLog)
    {
        var localDir = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "StatsClient",
            "EncodeIdentifyLogs");

        return debugLog +
               Environment.NewLine +
               Environment.NewLine +
               "Could not write vision debug log file. Tried:" +
               Environment.NewLine +
               AppContext.BaseDirectory +
               Environment.NewLine +
               localDir;
    }

    private static string? WriteVisionDebugFile(string debugLog)
    {
        var content = string.IsNullOrWhiteSpace(debugLog)
            ? "(Vision call produced no debug text.)"
            : debugLog;

        var fileName = $"EncodeIdentify_vision_{DateTime.Now:yyyyMMdd_HHmmss}.txt";
        var localAppData = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "StatsClient",
            "EncodeIdentifyLogs");

        // Prefer the *actual exe location* (AppContext.BaseDirectory can be misleading in some hosts).
        string? exeDir = null;
        try
        {
            var exePath = Process.GetCurrentProcess().MainModule?.FileName;
            if (!string.IsNullOrWhiteSpace(exePath))
            {
                exeDir = Path.GetDirectoryName(exePath);
            }
        }
        catch
        {
            exeDir = null;
        }

        var directories = new[]
        {
            exeDir,
            AppContext.BaseDirectory,
            localAppData,
            Path.GetTempPath()
        }
        .Where(d => !string.IsNullOrWhiteSpace(d))
        .Select(d => d!)
        .Distinct(StringComparer.OrdinalIgnoreCase)
        .ToArray();

        string? lastError = null;
        string? firstPath = null;
        foreach (var directory in directories)
        {
            try
            {
                Directory.CreateDirectory(directory);
                var path = Path.Combine(directory, fileName);
                File.WriteAllText(path, content);
                firstPath ??= path;
            }
            catch (Exception ex)
            {
                lastError = ex.Message;
            }
        }

        if (firstPath is not null)
        {
            return firstPath;
        }

        System.Diagnostics.Debug.WriteLine(
            $"EncodeIdentify: could not write vision debug log ({lastError ?? "unknown error"}).");

        return null;
    }

    private static EncodeVisionFeatures? ParseVisionJson(string raw)
    {
        int start = raw.IndexOf('{');
        int end = raw.LastIndexOf('}');
        if (start < 0 || end <= start)
        {
            return null;
        }

        try
        {
            return JsonSerializer.Deserialize<EncodeVisionFeatures>(raw[start..(end + 1)], JsonOptions);
        }
        catch
        {
            return null;
        }
    }

    private sealed class EncodeVisionFeatures
    {
        [JsonPropertyName("profile")]
        public string? Profile { get; set; }

        [JsonPropertyName("centerGrooves")]
        public int? CenterGrooves { get; set; }

        [JsonPropertyName("family")]
        public string? Family { get; set; }

        [JsonPropertyName("dimplesBelowLine")]
        public int? DimplesBelowLine { get; set; }

        [JsonPropertyName("dimplesRight")]
        public int? DimplesRight { get; set; }

        [JsonPropertyName("legacyHeightMm")]
        public int? LegacyHeightMm { get; set; }

        [JsonPropertyName("confidence")]
        public double Confidence { get; set; }

        [JsonPropertyName("notes")]
        public string? Notes { get; set; }
    }
}
