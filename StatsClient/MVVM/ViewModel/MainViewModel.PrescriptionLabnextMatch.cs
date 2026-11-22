using System.Diagnostics;
using System.Windows;
using StatsClient.MVVM.Core;
using StatsClient.MVVM.Model;

namespace StatsClient.MVVM.ViewModel;

public partial class MainViewModel
{
    private static readonly TimeSpan LabnextPrescriptionEnrichInterval = TimeSpan.FromSeconds(60);
    private static readonly TimeSpan LabnextPrescriptionNegativeCacheTtl = TimeSpan.FromMinutes(10);

    private bool _prescriptionLabnextMatchInProgress;
    private DateTime _lastLabnextPrescriptionEnrichUtc = DateTime.MinValue;
    private readonly Dictionary<string, LabnextPrescriptionMatchCacheEntry> _labnextPrescriptionMatchCache = new(StringComparer.OrdinalIgnoreCase);

    private sealed class LabnextPrescriptionMatchCacheEntry
    {
        public bool HasMatch { get; init; }
        public string? CaseId { get; init; }
        public DateTime CachedUtc { get; init; }
    }

    public void OpenLabnextCaseInExternalBrowser(object? obj)
    {
        string? caseId = obj as string;
        if (string.IsNullOrWhiteSpace(caseId))
            return;

        try
        {
            Process.Start(new ProcessStartInfo($"{LabnextUrl}cases/case/id/{caseId.Trim()}") { UseShellExecute = true });
        }
        catch (Exception ex)
        {
            AddDebugLine(ex, ex.Message);
        }
    }

    internal static bool ShouldRunPrescriptionLabnextEnrichment(int second) => second % 30 == 0;

    internal void ApplyCachedLabnextPrescriptionMatches(IEnumerable<InconsistencyModel> inconsistencies)
    {
        DateTime now = DateTime.UtcNow;
        foreach (InconsistencyModel item in inconsistencies)
        {
            if (string.IsNullOrWhiteSpace(item.OrderID))
                continue;

            if (!_labnextPrescriptionMatchCache.TryGetValue(item.OrderID, out LabnextPrescriptionMatchCacheEntry? cached))
                continue;

            if (!cached.HasMatch && now - cached.CachedUtc > LabnextPrescriptionNegativeCacheTtl)
                continue;

            item.HasLabnextMatch = cached.HasMatch;
            item.LabnextMatchCaseId = cached.CaseId ?? "";
        }
    }

    private async Task EnrichMissingPrescriptionLabnextMatchesOnUiThreadAsync(List<InconsistencyModel> inconsistencies)
    {
        if (!Application.Current.Dispatcher.CheckAccess())
        {
            await Application.Current.Dispatcher.InvokeAsync(
                () => EnrichMissingPrescriptionLabnextMatchesOnUiThreadAsync(inconsistencies)).Task.Unwrap();
            return;
        }

        await EnrichMissingPrescriptionLabnextMatchesAsync(inconsistencies);
    }

    private async Task EnrichMissingPrescriptionLabnextMatchesAsync(List<InconsistencyModel> inconsistencies)
    {
        ApplyCachedLabnextPrescriptionMatches(inconsistencies);

        if (_prescriptionLabnextMatchInProgress)
            return;

        DateTime now = DateTime.UtcNow;
        if (now - _lastLabnextPrescriptionEnrichUtc < LabnextPrescriptionEnrichInterval)
            return;

        if (!CbSettingModuleLabnext || !IsMainWindowReady || !_MainWindow.webviewLabnext.IsInitialized)
            return;

        if (!string.IsNullOrEmpty(LabNextWebViewStatusText)
            && LabNextWebViewStatusText.Contains("/login", StringComparison.OrdinalIgnoreCase))
            return;

        var candidates = inconsistencies
            .Where(x => IsMissingPrescriptionRow(x)
                        && HasPatientNameForLabnextMatch(x)
                        && !string.IsNullOrWhiteSpace(ResolveMissingPrescriptionLookupPan(x))
                        && !IsRecentLabnextPrescriptionCacheHit(x.OrderID!))
            .ToList();

        if (candidates.Count == 0)
            return;

        _prescriptionLabnextMatchInProgress = true;
        _lastLabnextPrescriptionEnrichUtc = now;
        var enrichSw = Stopwatch.StartNew();
        try
        {
            foreach (InconsistencyModel item in candidates)
            {
                string lookupPan = ResolveMissingPrescriptionLookupPan(item)!;
                LabnextCaseLookupResult? match = await LabnextCaseBrowserLookup.TryLookupOpenCaseByPanAsync(
                    _MainWindow.webviewLabnext,
                    LabnextUrl,
                    lookupPan,
                    item.Patient_FirstName ?? "",
                    item.Patient_LastName ?? "").ConfigureAwait(true);

                if (match is null)
                {
                    RememberLabnextPrescriptionCache(item.OrderID!, false, null);
                    continue;
                }

                item.HasLabnextMatch = true;
                item.LabnextMatchCaseId = match.CaseId;
                RememberLabnextPrescriptionCache(item.OrderID!, true, match.CaseId);
            }
        }
        finally
        {
            enrichSw.Stop();
            _prescriptionLabnextMatchInProgress = false;
            if (enrichSw.ElapsedMilliseconds >= PerfDiagLog.SlowOpThresholdMs)
            {
                PerfDiagLog.WriteSlow(
                    "PrescriptionMaker",
                    "Labnext prescription match enrich",
                    enrichSw.ElapsedMilliseconds,
                    $"candidates={candidates.Count}");
            }
        }
    }

    private static bool IsMissingPrescriptionRow(InconsistencyModel item) =>
        !item.Ignored
        && !string.IsNullOrWhiteSpace(item.OrderID)
        && string.IsNullOrWhiteSpace(item.PanNumber);

    private static bool HasPatientNameForLabnextMatch(InconsistencyModel item) =>
        !string.IsNullOrWhiteSpace(item.Patient_FirstName)
        || !string.IsNullOrWhiteSpace(item.Patient_LastName);

    private static string? ResolveMissingPrescriptionLookupPan(InconsistencyModel item)
    {
        if (!string.IsNullOrWhiteSpace(item.DigiPanNumber))
            return item.DigiPanNumber.Trim();

        if (string.IsNullOrWhiteSpace(item.OrderID))
            return null;

        string firstSegment = item.OrderID.Split('-', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries).FirstOrDefault() ?? "";
        return int.TryParse(firstSegment, out _) ? firstSegment : null;
    }

    private bool IsRecentLabnextPrescriptionCacheHit(string orderId)
    {
        if (!_labnextPrescriptionMatchCache.TryGetValue(orderId, out LabnextPrescriptionMatchCacheEntry? cached))
            return false;

        if (cached.HasMatch)
            return true;

        return DateTime.UtcNow - cached.CachedUtc < LabnextPrescriptionNegativeCacheTtl;
    }

    private void RememberLabnextPrescriptionCache(string orderId, bool hasMatch, string? caseId)
    {
        _labnextPrescriptionMatchCache[orderId] = new LabnextPrescriptionMatchCacheEntry
        {
            HasMatch = hasMatch,
            CaseId = caseId,
            CachedUtc = DateTime.UtcNow,
        };
    }
}
