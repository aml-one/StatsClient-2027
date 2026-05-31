using System.Net.Http;
using System.Net.Http.Headers;
using System.IO;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace DCMViewer.Services;

/// <summary>
/// NVIDIA VLM client — uses Stats DB Settings, runs inside StatsClient.
/// </summary>
internal sealed class NvidiaVisionService
{
    public const string DefaultVisionEndpoint = "https://integrate.api.nvidia.com/v1/chat/completions";
    private const string DefaultVisionModel = "google/gemma-3n-e4b-it";

    private static readonly HttpClient SharedHttpClient = new() { Timeout = TimeSpan.FromSeconds(300) };

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
    };

    public async Task<NvidiaVisionCallResult> AnalyzeImageAsync(
        string apiKey,
        string visionEndpoint,
        byte[] imagePng,
        string prompt,
        int maxTokens,
        double temperature,
        double topP,
        CancellationToken cancellationToken = default,
        string? imageMimeType = null,
        bool enableThinking = false)
    {
        if (string.IsNullOrWhiteSpace(visionEndpoint))
        {
            visionEndpoint = DefaultVisionEndpoint;
        }

        var endpoint = visionEndpoint.Trim();
        if (string.IsNullOrWhiteSpace(apiKey))
        {
            return new NvidiaVisionCallResult
            {
                Endpoint = endpoint,
                ErrorSummary = "API key is empty.",
                ImageBytes = imagePng.Length,
                PromptChars = prompt.Length
            };
        }

        if (imagePng.Length == 0)
        {
            return new NvidiaVisionCallResult
            {
                Endpoint = endpoint,
                ErrorSummary = "Screenshot image is empty.",
                PromptChars = prompt.Length
            };
        }

        var mime = string.IsNullOrWhiteSpace(imageMimeType) ? "image/png" : imageMimeType.Trim();
        string base64 = Convert.ToBase64String(imagePng);
        var imageDataUrl = $"data:{mime};base64,{base64}";

        var request = new NvidiaChatCompletionsRequest
        {
            Model = DefaultVisionModel,
            Messages =
            [
                new NvidiaChatMessage
                {
                    Role = "user",
                    ContentParts =
                    [
                        new NvidiaContentPart { Type = "text", Text = prompt },
                        new NvidiaContentPart
                        {
                            Type = "image_url",
                            ImageUrl = new NvidiaImageUrl { Url = imageDataUrl }
                        }
                    ]
                }
            ],
            MaxTokens = Math.Clamp(maxTokens, 16, 8192),
            Temperature = Math.Clamp(temperature, 0, 2),
            TopP = Math.Clamp(topP, 0.01, 1),
            Stream = true,
            ChatTemplateKwargs = enableThinking
                ? new NvidiaChatTemplateKwargs { EnableThinking = true }
                : null
        };

        byte[] requestBytes = JsonSerializer.SerializeToUtf8Bytes(request, JsonOptions);
        using var httpRequest = new HttpRequestMessage(HttpMethod.Post, endpoint)
        {
            Content = new ByteArrayContent(requestBytes)
        };
        httpRequest.Content.Headers.ContentType = new MediaTypeHeaderValue("application/json");
        httpRequest.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("text/event-stream"));
        httpRequest.Headers.Authorization = new AuthenticationHeaderValue("Bearer", apiKey.Trim());

        try
        {
            using var response = await SharedHttpClient.SendAsync(httpRequest, HttpCompletionOption.ResponseHeadersRead, cancellationToken)
                .ConfigureAwait(false);
            int status = (int)response.StatusCode;
            var contentType = response.Content.Headers.ContentType?.MediaType ?? string.Empty;
            var isSse = contentType.Contains("text/event-stream", StringComparison.OrdinalIgnoreCase);
            string content;
            string bodyPreview;

            if (isSse)
            {
                (content, bodyPreview) = await ReadSseContentAsync(response, cancellationToken).ConfigureAwait(false);
            }
            else
            {
                byte[] responseBytes = await response.Content.ReadAsByteArrayAsync(cancellationToken).ConfigureAwait(false);
                bodyPreview = TruncateForLog(Encoding.UTF8.GetString(responseBytes), 4000);
                var parsed = JsonSerializer.Deserialize<NvidiaChatCompletionsResponse>(responseBytes, JsonOptions);
                content = ExtractTextContent(parsed?.Choices?.FirstOrDefault()?.Message?.Content) ?? string.Empty;
            }

            if (!response.IsSuccessStatusCode)
            {
                return new NvidiaVisionCallResult
                {
                    Endpoint = endpoint,
                    HttpStatusCode = status,
                    ImageBytes = imagePng.Length,
                    PromptChars = prompt.Length,
                    ResponseBodyPreview = bodyPreview,
                    ErrorSummary = FormatHttpError(status, response.ReasonPhrase?.ToString(), bodyPreview)
                };
            }

            if (string.IsNullOrWhiteSpace(content))
            {
                return new NvidiaVisionCallResult
                {
                    Endpoint = endpoint,
                    HttpStatusCode = status,
                    ImageBytes = imagePng.Length,
                    PromptChars = prompt.Length,
                    ResponseBodyPreview = bodyPreview,
                    ErrorSummary = "HTTP 200 but no message content in choices[0]."
                };
            }

            return new NvidiaVisionCallResult
            {
                Endpoint = endpoint,
                HttpStatusCode = status,
                ImageBytes = imagePng.Length,
                PromptChars = prompt.Length,
                ResponseBodyPreview = bodyPreview,
                Content = content
            };
        }
        catch (Exception ex)
        {
            return new NvidiaVisionCallResult
            {
                Endpoint = endpoint,
                ImageBytes = imagePng.Length,
                PromptChars = prompt.Length,
                ErrorSummary = ex.Message
            };
        }
    }

    public static string FormatHttpError(int status, string? reasonPhrase, string? bodyPreview)
    {
        var detail = ExtractApiErrorDetail(bodyPreview);
        var summary = $"HTTP {status}";
        if (!string.IsNullOrWhiteSpace(reasonPhrase))
        {
            summary += $" {reasonPhrase}";
        }

        return string.IsNullOrWhiteSpace(detail) ? summary : $"{summary} — {detail}";
    }

    public static string? ExtractApiErrorDetail(string? bodyPreview)
    {
        if (string.IsNullOrWhiteSpace(bodyPreview))
        {
            return null;
        }

        try
        {
            var start = bodyPreview.IndexOf('{');
            var end = bodyPreview.LastIndexOf('}');
            if (start < 0 || end <= start)
            {
                return TruncateForLog(bodyPreview.Trim(), 280);
            }

            using var doc = JsonDocument.Parse(bodyPreview[start..(end + 1)]);
            if (doc.RootElement.TryGetProperty("detail", out var detail))
            {
                return detail.ValueKind switch
                {
                    JsonValueKind.String => detail.GetString(),
                    JsonValueKind.Array => string.Join("; ",
                        detail.EnumerateArray().Select(item => item.ToString())),
                    _ => detail.ToString()
                };
            }

            if (doc.RootElement.TryGetProperty("message", out var message) && message.ValueKind == JsonValueKind.String)
            {
                return message.GetString();
            }
        }
        catch
        {
            // fall through
        }

        return TruncateForLog(bodyPreview.Trim(), 280);
    }

    private static string? ExtractTextContent(JsonElement? contentElement)
    {
        if (contentElement is not { } content)
        {
            return null;
        }

        if (content.ValueKind == JsonValueKind.String)
        {
            return content.GetString()?.Trim();
        }

        if (content.ValueKind != JsonValueKind.Array)
        {
            return null;
        }

        var sb = new StringBuilder();
        foreach (var part in content.EnumerateArray())
        {
            if (part.TryGetProperty("text", out var text) && text.ValueKind == JsonValueKind.String)
            {
                sb.Append(text.GetString());
            }
        }

        return sb.Length == 0 ? null : sb.ToString().Trim();
    }

    private static async Task<(string Content, string Preview)> ReadSseContentAsync(HttpResponseMessage response, CancellationToken ct)
    {
        var sb = new StringBuilder();
        var previewSb = new StringBuilder();
        using var stream = await response.Content.ReadAsStreamAsync(ct).ConfigureAwait(false);
        using var reader = new StreamReader(stream, Encoding.UTF8);

        while (true)
        {
            ct.ThrowIfCancellationRequested();
            var line = await reader.ReadLineAsync(ct).ConfigureAwait(false);
            if (line is null)
            {
                break;
            }

            if (line.StartsWith("data:", StringComparison.OrdinalIgnoreCase))
            {
                var data = line["data:".Length..].Trim();
                if (data == "[DONE]")
                {
                    break;
                }

                if (previewSb.Length < 4000)
                {
                    previewSb.AppendLine(data);
                }

                try
                {
                    var bytes = Encoding.UTF8.GetBytes(data);
                    var chunk = JsonSerializer.Deserialize<NvidiaChatCompletionsChunk>(bytes, JsonOptions);
                    var delta = chunk?.Choices?.FirstOrDefault()?.Delta?.Content;
                    if (!string.IsNullOrEmpty(delta))
                    {
                        sb.Append(delta);
                    }
                }
                catch
                {
                    // ignore malformed chunks
                }
            }
        }

        return (sb.ToString(), TruncateForLog(previewSb.ToString(), 4000));
    }

    private static string TruncateForLog(string text, int maxChars)
    {
        if (string.IsNullOrEmpty(text) || text.Length <= maxChars)
        {
            return text;
        }

        return text[..maxChars] + "\n… (truncated)";
    }

    public static string BuildEncodeCapVisionPrompt() =>
        """
        You analyze greyscale dental scan renders of ZimVie BellaTek Encode healing-cap tops.
        Do NOT use color. Classify geometry only.

        Profile:
        - "Emergence": one horizontal shelf line on the occlusal surface, often with small circular dimples (sometimes shelf with no dimples).
        - "Legacy": two distinct flat mesa/table regions on the occlusal surface (always two flat parts).

        Then inspect the center screw area:
        - Count grooves in the center: 2 = CERTAIN (Biomet), 3 = TSV (Zimmer).

        If Emergence (shelf horizontal):
        - dimplesBelowLine: count dimples strictly below the shelf (0-3).
        - dimplesRight: count dimples on the right side of the dome above the shelf (0-2).

        If Legacy:
        - peripheralNotchCount: count peripheral notches on the outer edge (1-4).
        - legacyHeightMm: best match 3,4,6,8 for CERTAIN or 3,5,7 for TSV from notch layout.

        Return ONLY valid JSON:
        {"profile":"Emergence|Legacy","centerGrooves":2|3,"family":"CERTAIN|TSV","dimplesBelowLine":null|0|1|2|3,"dimplesRight":null|0|1|2,"peripheralNotchCount":null|1|2|3|4,"legacyHeightMm":null|3|4|5|6|7|8,"confidence":0.0-1.0,"notes":""}
        """;

    private sealed class NvidiaChatCompletionsRequest
    {
        [JsonPropertyName("model")]
        public string Model { get; set; } = string.Empty;

        [JsonPropertyName("messages")]
        public List<NvidiaChatMessage> Messages { get; set; } = [];

        [JsonPropertyName("max_tokens")]
        public int MaxTokens { get; set; }

        [JsonPropertyName("temperature")]
        public double Temperature { get; set; }

        [JsonPropertyName("top_p")]
        public double TopP { get; set; }

        [JsonPropertyName("stream")]
        public bool Stream { get; set; }

        [JsonPropertyName("chat_template_kwargs")]
        public NvidiaChatTemplateKwargs? ChatTemplateKwargs { get; set; }
    }

    private sealed class NvidiaChatTemplateKwargs
    {
        [JsonPropertyName("enable_thinking")]
        public bool EnableThinking { get; set; }
    }

    private sealed class NvidiaChatMessage
    {
        [JsonPropertyName("role")]
        public string Role { get; set; } = string.Empty;

        [JsonPropertyName("content")]
        public List<NvidiaContentPart>? ContentParts { get; set; }

        [JsonIgnore]
        public string Content { get; set; } = string.Empty;
    }

    private sealed class NvidiaContentPart
    {
        [JsonPropertyName("type")]
        public string Type { get; set; } = string.Empty;

        [JsonPropertyName("text")]
        public string? Text { get; set; }

        [JsonPropertyName("image_url")]
        public NvidiaImageUrl? ImageUrl { get; set; }
    }

    private sealed class NvidiaImageUrl
    {
        [JsonPropertyName("url")]
        public string Url { get; set; } = string.Empty;
    }

    private sealed class NvidiaChatCompletionsResponse
    {
        [JsonPropertyName("choices")]
        public List<NvidiaChatChoice>? Choices { get; set; }
    }

    private sealed class NvidiaChatChoice
    {
        [JsonPropertyName("message")]
        public NvidiaChatResponseMessage? Message { get; set; }
    }

    private sealed class NvidiaChatResponseMessage
    {
        [JsonPropertyName("role")]
        public string? Role { get; set; }

        [JsonPropertyName("content")]
        public JsonElement? Content { get; set; }
    }

    private sealed class NvidiaChatCompletionsChunk
    {
        [JsonPropertyName("choices")]
        public List<NvidiaChatChunkChoice>? Choices { get; set; }
    }

    private sealed class NvidiaChatChunkChoice
    {
        [JsonPropertyName("delta")]
        public NvidiaChatChunkDelta? Delta { get; set; }
    }

    private sealed class NvidiaChatChunkDelta
    {
        [JsonPropertyName("content")]
        public string? Content { get; set; }
    }
}
