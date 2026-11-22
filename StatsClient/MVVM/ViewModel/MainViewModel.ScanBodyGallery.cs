using System.Collections.ObjectModel;
using System.Windows;
using StatsClient.KnowledgeBase.Core;
using StatsClient.MVVM.Core;
using StatsClient.MVVM.Core.ScanBodyGallery;
using StatsClient.MVVM.Model;
using StatsClient.MVVM.View;
using static StatsClient.MVVM.Core.DatabaseConnection;
using static StatsClient.MVVM.Core.Enums;
using static StatsClient.MVVM.Core.LocalSettingsDB;

namespace StatsClient.MVVM.ViewModel;

public partial class MainViewModel
{
    private List<ScanBodyGalleryEntryModel> _scanBodyGalleryMasterList = [];
    private bool _scanBodyGalleryInitialized;

    public RelayCommand AddScanBodyGalleryEntryCommand { get; private set; } = null!;
    public RelayCommand EditScanBodyGalleryEntryCommand { get; private set; } = null!;
    public RelayCommand DeleteScanBodyGalleryEntryCommand { get; private set; } = null!;
    public RelayCommand ToggleScanBodyGalleryTagFilterCommand { get; private set; } = null!;
    public RelayCommand ClearScanBodyGalleryTagFiltersCommand { get; private set; } = null!;
    public RelayCommand ClearScanBodyGallerySearchCommand { get; private set; } = null!;
    public RelayCommand ScanBodyGallerySearchByImageCommand { get; private set; } = null!;
    public RelayCommand ScanBodyGalleryOpenVisionMatchCommand { get; private set; } = null!;
    public RelayCommand ScanBodyGalleryMoreInfoCommand { get; private set; } = null!;

    private List<ScanBodyGalleryEntryModel> scanBodyGalleryList = [];
    public List<ScanBodyGalleryEntryModel> ScanBodyGalleryList
    {
        get => scanBodyGalleryList;
        set
        {
            scanBodyGalleryList = value;
            RaisePropertyChanged(nameof(ScanBodyGalleryList));
            RaisePropertyChanged(nameof(ScanBodyGalleryCountText));
        }
    }

    public string ScanBodyGalleryCountText =>
        ScanBodyGalleryList.Count == 1 ? "1 scan body" : $"{ScanBodyGalleryList.Count} scan bodies";

    private string searchScanBodyGallery = string.Empty;
    public string SearchScanBodyGallery
    {
        get => searchScanBodyGallery;
        set
        {
            searchScanBodyGallery = value;
            RaisePropertyChanged(nameof(SearchScanBodyGallery));
            ApplyScanBodyGalleryFilters();
        }
    }

    private ObservableCollection<ScanBodyGalleryTagChipItem> scanBodyGalleryTagFilters = [];
    public ObservableCollection<ScanBodyGalleryTagChipItem> ScanBodyGalleryTagFilters
    {
        get => scanBodyGalleryTagFilters;
        set
        {
            scanBodyGalleryTagFilters = value;
            RaisePropertyChanged(nameof(ScanBodyGalleryTagFilters));
        }
    }

    private bool scanBodyGallerySchemaMissing;
    public bool ScanBodyGallerySchemaMissing
    {
        get => scanBodyGallerySchemaMissing;
        set
        {
            scanBodyGallerySchemaMissing = value;
            RaisePropertyChanged(nameof(ScanBodyGallerySchemaMissing));
        }
    }

    private string scanBodyGalleryStatusText = string.Empty;
    public string ScanBodyGalleryStatusText
    {
        get => scanBodyGalleryStatusText;
        set
        {
            scanBodyGalleryStatusText = value;
            RaisePropertyChanged(nameof(ScanBodyGalleryStatusText));
        }
    }

    private byte[]? scanBodyGallerySearchImageBytes;
    public byte[]? ScanBodyGallerySearchImageBytes
    {
        get => scanBodyGallerySearchImageBytes;
        set
        {
            scanBodyGallerySearchImageBytes = value;
            RaisePropertyChanged(nameof(ScanBodyGallerySearchImageBytes));
        }
    }

    private bool scanBodyGalleryVisionSearchRunning;
    public bool ScanBodyGalleryVisionSearchRunning
    {
        get => scanBodyGalleryVisionSearchRunning;
        set
        {
            scanBodyGalleryVisionSearchRunning = value;
            RaisePropertyChanged(nameof(ScanBodyGalleryVisionSearchRunning));
        }
    }

    public ObservableCollection<ScanBodyGalleryVisionMatch> ScanBodyGalleryVisionResults { get; } = [];

    private int scanBodyGalleryVisionMinConfidence = 85;
    public int ScanBodyGalleryVisionMinConfidence
    {
        get => scanBodyGalleryVisionMinConfidence;
        set
        {
            int clamped = Math.Clamp(value, 50, 99);
            if (scanBodyGalleryVisionMinConfidence == clamped)
                return;

            scanBodyGalleryVisionMinConfidence = clamped;
            RaisePropertyChanged(nameof(ScanBodyGalleryVisionMinConfidence));
            RaisePropertyChanged(nameof(ScanBodyGalleryVisionMinConfidenceLabel));
            WriteLocalSetting("ScanBodyGalleryVisionMinConfidence", clamped.ToString());
        }
    }

    public string ScanBodyGalleryVisionMinConfidenceLabel =>
        $"Min match confidence: {ScanBodyGalleryVisionMinConfidence}%";

    private void InitializeScanBodyGalleryCommands()
    {
        AddScanBodyGalleryEntryCommand = new RelayCommand(_ => AddScanBodyGalleryEntry());
        EditScanBodyGalleryEntryCommand = new RelayCommand(EditScanBodyGalleryEntry);
        DeleteScanBodyGalleryEntryCommand = new RelayCommand(DeleteScanBodyGalleryEntry);
        ToggleScanBodyGalleryTagFilterCommand = new RelayCommand(ToggleScanBodyGalleryTagFilter);
        ClearScanBodyGalleryTagFiltersCommand = new RelayCommand(_ => ClearScanBodyGalleryTagFilters());
        ClearScanBodyGallerySearchCommand = new RelayCommand(_ => SearchScanBodyGallery = string.Empty);
        ScanBodyGallerySearchByImageCommand = new RelayCommand(_ => _ = RunScanBodyGalleryVisionSearchAsync());
        ScanBodyGalleryOpenVisionMatchCommand = new RelayCommand(OpenScanBodyGalleryVisionMatch);
        ScanBodyGalleryMoreInfoCommand = new RelayCommand(OpenKnowledgeBaseFromScanBody);

        if (int.TryParse(ReadLocalSetting("ScanBodyGalleryVisionMinConfidence"), out int savedConfidence))
        {
            scanBodyGalleryVisionMinConfidence = Math.Clamp(savedConfidence, 50, 99);
            RaisePropertyChanged(nameof(ScanBodyGalleryVisionMinConfidence));
            RaisePropertyChanged(nameof(ScanBodyGalleryVisionMinConfidenceLabel));
        }
    }

    private void SwitchToScanBodyGalleryTab()
    {
        Application.Current.Dispatcher.Invoke(() =>
        {
            LabnextCanReload = false;
            ClearAllSearchCriteria();
            _MainWindow.mainTabControl.SelectedItem = _MainWindow.scanBodyGalleryTab;
            UpdateTabChromeForSelection();
        });

        _ = EnsureScanBodyGalleryLoadedAsync();
    }

    private async Task EnsureScanBodyGalleryLoadedAsync()
    {
        if (_scanBodyGalleryInitialized)
        {
            ApplyScanBodyGalleryFilters();
            return;
        }

        await RefreshScanBodyGalleryAsync().ConfigureAwait(false);
        _scanBodyGalleryInitialized = true;
    }

    private async Task RefreshScanBodyGalleryAsync()
    {
        bool schemaExists = await Task.Run(ScanBodyGalleryRepository.SchemaExists).ConfigureAwait(false);
        if (!schemaExists)
        {
            await Application.Current.Dispatcher.InvokeAsync(() =>
            {
                ScanBodyGallerySchemaMissing = true;
                _scanBodyGalleryMasterList = [];
                ScanBodyGalleryTagFilters = [];
                ScanBodyGalleryList = [];
                ScanBodyGalleryStatusText = string.Empty;
            });
            return;
        }

        var entries = await ScanBodyGalleryRepository.GetAllAsync().ConfigureAwait(false);
        await ApplyScanBodyKnowledgeBaseCrossReferencesAsync(entries).ConfigureAwait(false);
        var tagCounts = await ScanBodyGalleryRepository.GetTagCountsAsync().ConfigureAwait(false);

        await Application.Current.Dispatcher.InvokeAsync(() =>
        {
            ScanBodyGallerySchemaMissing = false;
            _scanBodyGalleryMasterList = entries;

            var selectedTags = ScanBodyGalleryTagFilters
                .Where(t => t.IsSelected)
                .Select(t => t.TagName)
                .ToHashSet(StringComparer.OrdinalIgnoreCase);

            ScanBodyGalleryTagFilters = new ObservableCollection<ScanBodyGalleryTagChipItem>(
                tagCounts.Select(t => new ScanBodyGalleryTagChipItem
                {
                    TagName = t.TagName,
                    UsageCount = t.UsageCount,
                    IsSelected = selectedTags.Contains(t.TagName),
                }));

            ApplyScanBodyGalleryFilters();
        });
    }

    private void ApplyScanBodyGalleryFilters()
    {
        IEnumerable<ScanBodyGalleryEntryModel> query = _scanBodyGalleryMasterList;

        var activeTags = ScanBodyGalleryTagFilters
            .Where(t => t.IsSelected)
            .Select(t => t.TagName)
            .ToList();

        if (activeTags.Count > 0)
        {
            query = query.Where(e =>
                activeTags.All(tag =>
                    e.Tags.Any(t => string.Equals(t, tag, StringComparison.OrdinalIgnoreCase))));
        }

        if (!string.IsNullOrWhiteSpace(SearchScanBodyGallery))
        {
            string keyword = SearchScanBodyGallery.Trim();
            query = query.Where(e =>
                e.Brand.Contains(keyword, StringComparison.OrdinalIgnoreCase) ||
                e.SystemName.Contains(keyword, StringComparison.OrdinalIgnoreCase) ||
                e.SizeLabel.Contains(keyword, StringComparison.OrdinalIgnoreCase) ||
                e.OtherInfo.Contains(keyword, StringComparison.OrdinalIgnoreCase) ||
                e.TagsDisplay.Contains(keyword, StringComparison.OrdinalIgnoreCase));
        }

        ScanBodyGalleryList = query.ToList();
    }

    private void ToggleScanBodyGalleryTagFilter(object? parameter)
    {
        if (parameter is not string tagName || string.IsNullOrWhiteSpace(tagName))
            return;

        ScanBodyGalleryTagChipItem? chip = ScanBodyGalleryTagFilters
            .FirstOrDefault(t => string.Equals(t.TagName, tagName, StringComparison.OrdinalIgnoreCase));

        if (chip is null)
            return;

        chip.IsSelected = !chip.IsSelected;
        RaisePropertyChanged(nameof(ScanBodyGalleryTagFilters));
        ApplyScanBodyGalleryFilters();
    }

    private void ClearScanBodyGalleryTagFilters()
    {
        foreach (ScanBodyGalleryTagChipItem chip in ScanBodyGalleryTagFilters)
            chip.IsSelected = false;

        RaisePropertyChanged(nameof(ScanBodyGalleryTagFilters));
        ApplyScanBodyGalleryFilters();
    }

    private async void AddScanBodyGalleryEntry()
    {
        var window = new ScanBodyGalleryEntryWindow
        {
            Owner = _MainWindow,
        };

        if (window.ShowDialog() != true || window.Entry is null)
            return;

        window.Entry.SortOrder = _scanBodyGalleryMasterList.Count;
        bool saved = await ScanBodyGalleryRepository.InsertAsync(window.Entry).ConfigureAwait(false);
        if (!saved)
        {
            ShowScanBodyGallerySaveError();
            return;
        }

        await RefreshScanBodyGalleryAsync().ConfigureAwait(false);
    }

    private async void EditScanBodyGalleryEntry(object? parameter)
    {
        if (ResolveScanBodyGalleryEntry(parameter) is not ScanBodyGalleryEntryModel summary)
            return;

        var detail = await ScanBodyGalleryRepository.GetDetailAsync(summary.EntryId).ConfigureAwait(false);
        if (detail is null)
        {
            ShowScanBodyGallerySaveError();
            return;
        }

        var window = new ScanBodyGalleryEntryWindow(detail)
        {
            Owner = _MainWindow,
        };

        if (window.ShowDialog() != true || window.Entry is null)
            return;

        bool saved = await ScanBodyGalleryRepository.UpdateAsync(window.Entry).ConfigureAwait(false);
        if (!saved)
        {
            ShowScanBodyGallerySaveError();
            return;
        }

        await RefreshScanBodyGalleryAsync().ConfigureAwait(false);
    }

    private async void DeleteScanBodyGalleryEntry(object? parameter)
    {
        if (ResolveScanBodyGalleryEntry(parameter) is not ScanBodyGalleryEntryModel entry)
            return;

        SMessageBoxResult confirm = ShowMessageBox(
            "Scan body gallery",
            $"Delete \"{entry.DisplayTitle}\"?",
            SMessageBoxButtons.YesNo,
            NotificationIcon.Question,
            15,
            _MainWindow);

        if (confirm != SMessageBoxResult.Yes)
            return;

        bool deleted = await ScanBodyGalleryRepository.DeleteAsync(entry.EntryId).ConfigureAwait(false);
        if (!deleted)
        {
            ShowMessageBox(
                "Scan body gallery",
                "Could not delete the scan body entry.",
                SMessageBoxButtons.Ok,
                NotificationIcon.Error,
                10,
                _MainWindow);
            return;
        }

        await RefreshScanBodyGalleryAsync().ConfigureAwait(false);
    }

    private ScanBodyGalleryEntryModel? ResolveScanBodyGalleryEntry(object? parameter) =>
        parameter as ScanBodyGalleryEntryModel;

    public void SetScanBodyGallerySearchImageBytes(byte[] bytes) =>
        ScanBodyGallerySearchImageBytes = bytes;

    private async Task RunScanBodyGalleryVisionSearchAsync()
    {
        if (ScanBodyGallerySearchImageBytes is null || ScanBodyGallerySearchImageBytes.Length == 0 || ScanBodyGallerySchemaMissing)
            return;

        ScanBodyGalleryVisionSearchRunning = true;
        ScanBodyGalleryStatusText = "Analyzing scan body photo…";
        try
        {
            var entryIds = ScanBodyGalleryList.Select(e => e.EntryId).ToList();
            if (entryIds.Count == 0)
            {
                Application.Current.Dispatcher.Invoke(() =>
                {
                    ScanBodyGalleryVisionResults.Clear();
                    ScanBodyGalleryStatusText = "No entries with images match the current filters.";
                });
                return;
            }

            var service = new ScanBodyGalleryVisionSearchService();
            var matches = await service.SearchAsync(
                ScanBodyGallerySearchImageBytes,
                entryIds,
                ScanBodyGalleryVisionMinConfidence,
                () => ReadStatsSetting("Nvidia_API_KEY"),
                CancellationToken.None).ConfigureAwait(false);

            Application.Current.Dispatcher.Invoke(() =>
            {
                ScanBodyGalleryVisionResults.Clear();
                foreach (var match in matches)
                {
                    var entry = ScanBodyGalleryList.FirstOrDefault(e => e.EntryId == match.EntryId);
                    if (entry is not null)
                        match.Title = entry.DisplayTitle;

                    ScanBodyGalleryVisionResults.Add(match);
                }

                ScanBodyGalleryStatusText = matches.Count == 0
                    ? $"No matches at ≥{ScanBodyGalleryVisionMinConfidence}% confidence."
                    : $"{matches.Count} match(es) at ≥{ScanBodyGalleryVisionMinConfidence}% confidence.";
            });
        }
        catch (Exception ex)
        {
            ScanBodyGalleryStatusText = $"Vision search error: {ex.Message}";
        }
        finally
        {
            ScanBodyGalleryVisionSearchRunning = false;
        }
    }

    private async Task ApplyScanBodyKnowledgeBaseCrossReferencesAsync(List<ScanBodyGalleryEntryModel> entries)
    {
        if (entries.Count == 0)
            return;

        await EnsureKnowledgeBaseInitializedAsync().ConfigureAwait(false);
        if (!KnowledgeBaseSchemaAvailable)
            return;

        var kbCards = await _knowledgeBaseRepository.ListCardsCrossReferenceAsync().ConfigureAwait(false);

        foreach (var entry in entries)
        {
            if (!ScanBodyKnowledgeBaseCrossReference.HasRequiredScanBodyFields(entry.Brand, entry.SystemName))
            {
                entry.HasKnowledgeBaseMatch = false;
                entry.KnowledgeBaseMatchCount = 0;
                continue;
            }

            int count = kbCards.Count(c =>
                ScanBodyKnowledgeBaseCrossReference.CardMatchesScanBody(c, entry.Brand, entry.SystemName));
            entry.KnowledgeBaseMatchCount = count;
            entry.HasKnowledgeBaseMatch = count > 0;
        }
    }

    private async void OpenKnowledgeBaseFromScanBody(object? parameter)
    {
        if (parameter is not ScanBodyGalleryEntryModel entry || !entry.HasKnowledgeBaseMatch)
            return;

        await EnsureKnowledgeBaseInitializedAsync().ConfigureAwait(false);
        if (!KnowledgeBaseSchemaAvailable)
            return;

        var kbCards = await _knowledgeBaseRepository.ListCardsCrossReferenceAsync().ConfigureAwait(false);
        var matches = kbCards
            .Where(c => ScanBodyKnowledgeBaseCrossReference.CardMatchesScanBody(c, entry.Brand, entry.SystemName))
            .ToList();

        if (matches.Count == 0)
            return;

        await Application.Current.Dispatcher.InvokeAsync(() =>
        {
            LabnextCanReload = false;
            ClearAllSearchCriteria();
            _MainWindow.mainTabControl.SelectedItem = _MainWindow.knowledgeBaseTab;
            UpdateTabChromeForSelection();

            _knowledgeBaseSuppressTagFilterRefresh = true;
            foreach (var tagFilter in KnowledgeBaseTagFilters)
                tagFilter.IsSelected = false;
            _knowledgeBaseSuppressTagFilterRefresh = false;

            KnowledgeBaseFilterCategory = KnowledgeBaseCategories.FirstOrDefault(c => c.CategoryId == 0);
            KnowledgeBaseSearchText = $"{entry.Brand} {entry.SystemName}".Trim();

            KnowledgeBaseCards.Clear();
            foreach (var card in matches)
            {
                string preview = card.BodyText.Length > 240 ? card.BodyText[..240] : card.BodyText;
                KnowledgeBaseCards.Add(new KnowledgeBaseCardListItem
                {
                    CardId = card.CardId,
                    Title = string.IsNullOrWhiteSpace(card.Title) ? "(Untitled)" : card.Title,
                    BodyPreview = preview,
                    TagsDisplay = string.Join(", ", card.Tags),
                });
            }

            UpdateKnowledgeBaseCardListSelection();
            NotifyKnowledgeBaseFiltersActiveChanged();
        });

        await LoadKnowledgeBaseCardInternalAsync(matches[0].CardId, KnowledgeBaseDetailPaneMode.View)
            .ConfigureAwait(false);
    }

    private void OpenScanBodyGalleryVisionMatch(object? parameter)
    {
        if (parameter is not ScanBodyGalleryVisionMatch match || match.EntryId <= 0)
            return;

        ScanBodyGalleryEntryModel? entry = ScanBodyGalleryList.FirstOrDefault(e => e.EntryId == match.EntryId)
            ?? _scanBodyGalleryMasterList.FirstOrDefault(e => e.EntryId == match.EntryId);

        if (entry is not null)
            EditScanBodyGalleryEntry(entry);
    }

    private void ShowScanBodyGallerySaveError()
    {
        Application.Current.Dispatcher.Invoke(() =>
            ShowMessageBox(
                "Scan body gallery",
                "Could not save the scan body entry. Check database connection and run Docs/SQL/ScanBodyGallery_Schema.sql on StatsDB.",
                SMessageBoxButtons.Ok,
                NotificationIcon.Error,
                12,
                _MainWindow));
    }
}

public sealed class ScanBodyGalleryTagChipItem : ObservableObject
{
    public string TagName { get; init; } = string.Empty;
    public int UsageCount { get; init; }

    private bool isSelected;
    public bool IsSelected
    {
        get => isSelected;
        set
        {
            if (isSelected == value)
                return;

            isSelected = value;
            RaisePropertyChanged(nameof(IsSelected));
        }
    }

    public string DisplayText => $"{TagName} ({UsageCount})";
}
