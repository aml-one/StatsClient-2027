using System.Text.Json;
using System.Text.RegularExpressions;
using HtmlAgilityPack;
using Microsoft.Web.WebView2.Wpf;
using Newtonsoft.Json;
using StatsClient.MVVM.Model;

namespace StatsClient.MVVM.Core;

public sealed class LabnextCaseLookupResult
{
    public string CaseId { get; init; } = "";
    public string PatientLine { get; init; } = "";
    public bool IsInvoiced { get; init; }
}

public static class LabnextCaseBrowserLookup
{
    public static async Task<LabnextCaseLookupResult?> TryLookupOpenCaseByPanAsync(
        WebView2 webView,
        string labnextUrl,
        string panNumber,
        string statsFirstName,
        string statsLastName)
    {
        if (!webView.IsInitialized || webView.CoreWebView2 is null || string.IsNullOrWhiteSpace(panNumber))
            return null;

        string baseUrl = labnextUrl.TrimEnd('/');
        string panEndpoint = $"{baseUrl}/cases/pan/id/{Uri.EscapeDataString(panNumber)}";
        string panJson = await FetchTextAsync(webView, panEndpoint).ConfigureAwait(true);
        if (string.IsNullOrWhiteSpace(panJson)
            || panJson.Contains("<html", StringComparison.OrdinalIgnoreCase)
            || panJson.Contains("Login - Labnext", StringComparison.OrdinalIgnoreCase))
            return null;

        if (!panJson.TrimStart().StartsWith('{'))
            return null;

        LabNextObjectResponse? panResponse = JsonConvert.DeserializeObject<LabNextObjectResponse>(panJson);
        if (string.IsNullOrWhiteSpace(panResponse?.caseId))
            return null;

        string caseId = panResponse.caseId.Trim();
        string caseEndpoint = $"{baseUrl}/cases/case/id/{caseId}";
        string html = await FetchTextAsync(webView, caseEndpoint).ConfigureAwait(true);
        if (string.IsNullOrWhiteSpace(html))
            return null;

        if (IsLabnextCaseClosed(html))
            return null;

        string patientLine = ExtractPatientLine(html);
        if (string.IsNullOrWhiteSpace(patientLine))
            return null;

        if (!PatientNameMatcher.IsLikelySamePatient(statsFirstName, statsLastName, patientLine))
            return null;

        return new LabnextCaseLookupResult
        {
            CaseId = caseId,
            PatientLine = patientLine,
            IsInvoiced = false,
        };
    }

    private static bool IsLabnextCaseClosed(string html)
    {
        if (html.Contains("Closed - Invoiced", StringComparison.OrdinalIgnoreCase))
            return true;

        string status = ExtractCaseStatus(html);
        if (string.IsNullOrWhiteSpace(status))
            return false;

        return status.Contains("Closed", StringComparison.OrdinalIgnoreCase)
               || status.Contains("Cancelled", StringComparison.OrdinalIgnoreCase)
               || status.Contains("Invoiced", StringComparison.OrdinalIgnoreCase);
    }

    private static string ExtractCaseStatus(string html)
    {
        try
        {
            var doc = new HtmlDocument();
            doc.LoadHtml(html);
            string flattened = Regex.Replace(doc.Text, @"\s+", " ");
            string raw = CopyStringFromAfter(flattened, "Status:", 75)
                .Replace("</dt>", "", StringComparison.Ordinal)
                .Replace("<dd>", "", StringComparison.Ordinal)
                .Trim();
            return CopyStringTill(raw, "<").Trim();
        }
        catch
        {
            return "";
        }
    }

    private static async Task<string> FetchTextAsync(WebView2 webView, string url)
    {
        string urlLiteral = System.Text.Json.JsonSerializer.Serialize(url);
        string script = $@"
(async () => {{
  try {{
    const response = await fetch({urlLiteral}, {{ credentials: 'include' }});
    return await response.text();
  }} catch (_) {{
    return '';
  }}
}})()";

        try
        {
            string result = await webView.ExecuteScriptAsync(script).ConfigureAwait(true);
            return System.Text.Json.JsonSerializer.Deserialize<string>(result) ?? "";
        }
        catch
        {
            return "";
        }
    }

    private static string ExtractPatientLine(string html)
    {
        try
        {
            var doc = new HtmlDocument();
            doc.LoadHtml(html);
            HtmlNode? patientNode = doc.GetElementbyId("patient_add_content");
            if (patientNode is not null)
                return patientNode.InnerText.Trim();

            string flattened = Regex.Replace(doc.Text, @"\s+", " ");
            string afterLabel = CopyStringFromAfter(flattened, "Patient:", 120);
            return CopyStringTill(afterLabel, "Account:").Trim();
        }
        catch
        {
            return "";
        }
    }

    private static string CopyStringFromAfter(string source, string marker, int maxLength)
    {
        int index = source.IndexOf(marker, StringComparison.OrdinalIgnoreCase);
        if (index < 0)
            return "";

        return source[(index + marker.Length)..].Trim()[..Math.Min(maxLength, source.Length - index - marker.Length)];
    }

    private static string CopyStringTill(string source, string marker)
    {
        int index = source.IndexOf(marker, StringComparison.OrdinalIgnoreCase);
        return index < 0 ? source.Trim() : source[..index].Trim();
    }
}
