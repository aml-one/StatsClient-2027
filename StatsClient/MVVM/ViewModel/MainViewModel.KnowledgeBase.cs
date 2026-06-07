using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Windows;
using StatsClient.KnowledgeBase.Core;
using StatsClient.MVVM.Core;
using StatsClient.MVVM.Core.KnowledgeBase;
using StatsClient.MVVM.View;
using static StatsClient.MVVM.Core.DatabaseConnection;
using static StatsClient.MVVM.Core.LocalSettingsDB;

namespace StatsClient.MVVM.ViewModel;

public sealed class KnowledgeBaseLinkEditorItem : ObservableObject
{
    private string label = string.Empty;
    private string url = string.Empty;

    public string Label
    {
        get => label;
        set
        {
            label = value;
            RaisePropertyChanged(nameof(Label));
        }
    }

    public string Url
    {
        get => url;
        set
        {
            url = value;
            RaisePropertyChanged(nameof(Url));
        }
    }
}

public sealed class KnowledgeBaseImageEditorItem : ObservableObject
{
    public int ImageId { get; set; }
    public byte[] ImageData { get; set; } = [];
    public byte[]? ThumbnailData { get; set; }
    public string FileName { get; set; } = "image.png";
    public string ContentType { get; set; } = "image/png";
    public int SortOrder { get; set; }
}

public sealed class KnowledgeBaseCardListItem : ObservableObject
{
    public int CardId { get; init; }
    public string Title { get; init; } = string.Empty;
    public string BodyPreview { get; init; } = string.Empty;
    public string? CategoryName { get; init; }
    public byte[]? ThumbnailData { get; init; }
    public string TagsDisplay { get; init; } = string.Empty;
    public DateTime ModifiedUtc { get; init; }
}

public sealed class KnowledgeBaseTagFilterItem : ObservableObject
{
    public string TagName { get; init; } = string.Empty;
    public int UsageCount { get; init; }

    private bool isSelected;
    public bool IsSelected
    {
        get => isSelected;
        set
        {
            isSelected = value;
            RaisePropertyChanged(nameof(IsSelected));
        }
    }
}

public enum KnowledgeBaseDetailPaneMode
{
    None,
    View,
    Edit
}

public partial class MainViewModel
{
    private readonly KnowledgeBaseRepository _knowledgeBaseRepository = new();
    private readonly KnowledgeBaseBackupRepository _knowledgeBaseBackupRepository = new();
    private readonly KnowledgeBaseAutoSaveCoordinator _knowledgeBaseAutoSave = new();
    private bool _knowledgeBaseInitialized;
    private bool _knowledgeBaseLoadingEditor;
    private int _knowledgeBaseEditingCardId;
    private KnowledgeBaseDetailPaneMode _knowledgeBaseDetailPaneMode = KnowledgeBaseDetailPaneMode.None;
    private bool _knowledgeBaseIsNewCard;
    private int _knowledgeBaseCreatingCard;
    private bool _knowledgeBaseSuppressTagFilterRefresh;

    private bool cbSettingModuleKnowledgeBase = true;
    public bool CbSettingModuleKnowledgeBase
    {
        get => cbSettingModuleKnowledgeBase;
        set
        {
            cbSettingModuleKnowledgeBase = value;
            RaisePropertyChanged(nameof(CbSettingModuleKnowledgeBase));
        }
    }

    public RelayCommand SwitchToKnowledgeBaseTabCommand { get; set; } = null!;
    public RelayCommand KnowledgeBaseRefreshCommand { get; set; } = null!;
    public RelayCommand KnowledgeBaseNewCardCommand { get; set; } = null!;
    public RelayCommand KnowledgeBaseDeleteCardCommand { get; set; } = null!;
    public RelayCommand KnowledgeBaseSelectCardCommand { get; set; } = null!;
    public RelayCommand KnowledgeBaseEditCardCommand { get; set; } = null!;
    public RelayCommand KnowledgeBaseCloseDetailCommand { get; set; } = null!;
    public RelayCommand KnowledgeBaseAddLinkCommand { get; set; } = null!;
    public RelayCommand KnowledgeBaseRemoveLinkCommand { get; set; } = null!;
    public RelayCommand KnowledgeBaseAddTagCommand { get; set; } = null!;
    public RelayCommand KnowledgeBaseRemoveTagCommand { get; set; } = null!;
    public RelayCommand KnowledgeBaseClearFiltersCommand { get; set; } = null!;
    public RelayCommand KnowledgeBasePreviewBackupCommand { get; set; } = null!;
    public RelayCommand KnowledgeBaseRestoreBackupCommand { get; set; } = null!;
    public RelayCommand KnowledgeBaseSearchByImageCommand { get; set; } = null!;

    public ObservableCollection<KnowledgeBaseCardListItem> KnowledgeBaseCards { get; } = [];
    public ObservableCollection<KnowledgeBaseCategory> KnowledgeBaseCategories { get; } = [];
    public ObservableCollection<KnowledgeBaseCategory> KnowledgeBaseEditorCategories { get; } = [];
    public ObservableCollection<KnowledgeBaseTagFilterItem> KnowledgeBaseTagFilters { get; } = [];
    public ObservableCollection<KnowledgeBaseLinkEditorItem> KnowledgeBaseEditorLinks { get; } = [];
    public ObservableCollection<KnowledgeBaseImageEditorItem> KnowledgeBaseEditorImages { get; } = [];
    public ObservableCollection<string> KnowledgeBaseEditorTags { get; } = [];
    public ObservableCollection<KnowledgeBaseVisionMatch> KnowledgeBaseVisionResults { get; } = [];

    private string knowledgeBaseStatusText = "Ready";
    public string KnowledgeBaseStatusText
    {
        get => knowledgeBaseStatusText;
        set
        {
            knowledgeBaseStatusText = value;
            RaisePropertyChanged(nameof(KnowledgeBaseStatusText));
        }
    }

    private string knowledgeBaseSchemaMessage = string.Empty;
    public string KnowledgeBaseSchemaMessage
    {
        get => knowledgeBaseSchemaMessage;
        set
        {
            knowledgeBaseSchemaMessage = value;
            RaisePropertyChanged(nameof(KnowledgeBaseSchemaMessage));
        }
    }

    private bool knowledgeBaseSchemaAvailable;
    public bool KnowledgeBaseSchemaAvailable
    {
        get => knowledgeBaseSchemaAvailable;
        set
        {
            knowledgeBaseSchemaAvailable = value;
            RaisePropertyChanged(nameof(KnowledgeBaseSchemaAvailable));
        }
    }

    private string knowledgeBaseSearchText = string.Empty;
    public string KnowledgeBaseSearchText
    {
        get => knowledgeBaseSearchText;
        set
        {
            knowledgeBaseSearchText = value;
            RaisePropertyChanged(nameof(KnowledgeBaseSearchText));
            _ = RefreshKnowledgeBaseCardsAsync();
        }
    }

    private KnowledgeBaseCategory? knowledgeBaseFilterCategory;
    public KnowledgeBaseCategory? KnowledgeBaseFilterCategory
    {
        get => knowledgeBaseFilterCategory;
        set
        {
            knowledgeBaseFilterCategory = value;
            RaisePropertyChanged(nameof(KnowledgeBaseFilterCategory));
            _ = RefreshKnowledgeBaseCardsAsync();
        }
    }

    private string knowledgeBaseEditorCategoryName = string.Empty;
    public string KnowledgeBaseEditorCategoryName
    {
        get => knowledgeBaseEditorCategoryName;
        set
        {
            knowledgeBaseEditorCategoryName = value;
            RaisePropertyChanged(nameof(KnowledgeBaseEditorCategoryName));
            QueueKnowledgeBaseSave();
        }
    }

    private string knowledgeBaseEditorTitle = string.Empty;
    public string KnowledgeBaseEditorTitle
    {
        get => knowledgeBaseEditorTitle;
        set
        {
            knowledgeBaseEditorTitle = value;
            RaisePropertyChanged(nameof(KnowledgeBaseEditorTitle));
            RaisePropertyChanged(nameof(KnowledgeBaseDetailPaneTitle));
            QueueKnowledgeBaseSave();
        }
    }

    private string knowledgeBaseEditorBody = string.Empty;
    public string KnowledgeBaseEditorBody
    {
        get => knowledgeBaseEditorBody;
        set
        {
            knowledgeBaseEditorBody = value;
            RaisePropertyChanged(nameof(KnowledgeBaseEditorBody));
            QueueKnowledgeBaseSave();
        }
    }

    private string knowledgeBaseNewTagText = string.Empty;
    public string KnowledgeBaseNewTagText
    {
        get => knowledgeBaseNewTagText;
        set
        {
            knowledgeBaseNewTagText = value;
            RaisePropertyChanged(nameof(KnowledgeBaseNewTagText));
        }
    }

    private bool knowledgeBaseHasBackup;
    public bool KnowledgeBaseHasBackup
    {
        get => knowledgeBaseHasBackup;
        set
        {
            knowledgeBaseHasBackup = value;
            RaisePropertyChanged(nameof(KnowledgeBaseHasBackup));
        }
    }

    private byte[]? knowledgeBaseSearchImageBytes;
    public byte[]? KnowledgeBaseSearchImageBytes
    {
        get => knowledgeBaseSearchImageBytes;
        set
        {
            knowledgeBaseSearchImageBytes = value;
            RaisePropertyChanged(nameof(KnowledgeBaseSearchImageBytes));
        }
    }

    private bool knowledgeBaseVisionSearchRunning;
    public bool KnowledgeBaseVisionSearchRunning
    {
        get => knowledgeBaseVisionSearchRunning;
        set
        {
            knowledgeBaseVisionSearchRunning = value;
            RaisePropertyChanged(nameof(KnowledgeBaseVisionSearchRunning));
        }
    }

    public bool KnowledgeBaseDetailPaneVisible => _knowledgeBaseDetailPaneMode != KnowledgeBaseDetailPaneMode.None;

    public bool KnowledgeBaseIsViewMode => _knowledgeBaseDetailPaneMode == KnowledgeBaseDetailPaneMode.View;

    public bool KnowledgeBaseIsEditMode => _knowledgeBaseDetailPaneMode == KnowledgeBaseDetailPaneMode.Edit;

    public string KnowledgeBaseDetailPaneTitle =>
        _knowledgeBaseDetailPaneMode switch
        {
            KnowledgeBaseDetailPaneMode.Edit when _knowledgeBaseIsNewCard => "New card",
            KnowledgeBaseDetailPaneMode.Edit => "Edit card",
            KnowledgeBaseDetailPaneMode.View when !string.IsNullOrWhiteSpace(KnowledgeBaseEditorTitle) => KnowledgeBaseEditorTitle,
            KnowledgeBaseDetailPaneMode.View => "Card details",
            _ => string.Empty
        };

    private void InitializeKnowledgeBaseCommands()
    {
        SwitchToKnowledgeBaseTabCommand = new RelayCommand(_ => SwitchToKnowledgeBaseTab());
        CbSettingModuleKnowledgeBaseCommand = new RelayCommand(_ => CbSettingModuleKnowledgeBaseMethod());
        KnowledgeBaseRefreshCommand = new RelayCommand(_ => _ = RefreshKnowledgeBaseAsync());
        KnowledgeBaseNewCardCommand = new RelayCommand(_ => _ = CreateKnowledgeBaseCardAsync());
        KnowledgeBaseDeleteCardCommand = new RelayCommand(_ => _ = DeleteKnowledgeBaseCardAsync());
        KnowledgeBaseSelectCardCommand = new RelayCommand(o => _ = LoadKnowledgeBaseCardAsync(o));
        KnowledgeBaseEditCardCommand = new RelayCommand(_ => EnterKnowledgeBaseEditMode());
        KnowledgeBaseCloseDetailCommand = new RelayCommand(_ => _ = CloseKnowledgeBaseDetailAsync());
        KnowledgeBaseAddLinkCommand = new RelayCommand(_ => KnowledgeBaseEditorLinks.Add(new KnowledgeBaseLinkEditorItem()));
        KnowledgeBaseRemoveLinkCommand = new RelayCommand(o => RemoveKnowledgeBaseLink(o));
        KnowledgeBaseAddTagCommand = new RelayCommand(_ => AddKnowledgeBaseEditorTag());
        KnowledgeBaseRemoveTagCommand = new RelayCommand(o => RemoveKnowledgeBaseEditorTag(o));
        KnowledgeBaseClearFiltersCommand = new RelayCommand(_ => ClearKnowledgeBaseFilters());
        KnowledgeBasePreviewBackupCommand = new RelayCommand(_ => PreviewKnowledgeBaseBackup());
        KnowledgeBaseRestoreBackupCommand = new RelayCommand(_ => _ = RestoreKnowledgeBaseBackupAsync());
        KnowledgeBaseSearchByImageCommand = new RelayCommand(_ => _ = RunKnowledgeBaseVisionSearchAsync());

        _knowledgeBaseAutoSave.Configure(
            status => Application.Current.Dispatcher.Invoke(() => KnowledgeBaseStatusText = status),
            onSaved: RefreshKnowledgeBaseAfterSaveAsync,
            onError: ex => Application.Current.Dispatcher.Invoke(() =>
                KnowledgeBaseStatusText = $"Error: {ex.Message}"));
    }

    private void CbSettingModuleKnowledgeBaseMethod()
    {
        WriteLocalSetting("ModuleKnowledgeBase", CbSettingModuleKnowledgeBase.ToString());
    }

    private void SwitchToKnowledgeBaseTab()
    {
        Application.Current.Dispatcher.Invoke(() =>
        {
            LabnextCanReload = true;
            ClearAllSearchCriteria();
            _MainWindow.mainTabControl.SelectedItem = _MainWindow.knowledgeBaseTab;
            UpdateTabChromeForSelection();
            _ = EnsureKnowledgeBaseInitializedAsync();
        });
    }

    private async Task EnsureKnowledgeBaseInitializedAsync()
    {
        if (_knowledgeBaseInitialized)
        {
            await RefreshKnowledgeBaseAsync().ConfigureAwait(false);
            return;
        }

        KnowledgeBaseDatabase.ConnectionStringFactory = ConnectionStrToStatsDatabase;
        _knowledgeBaseInitialized = true;
        KnowledgeBaseSchemaAvailable = await KnowledgeBaseSchemaProbe.IsAvailableAsync().ConfigureAwait(false);
        KnowledgeBaseSchemaMessage = KnowledgeBaseSchemaAvailable
            ? string.Empty
            : "Knowledge Base tables are missing. Run Docs/SQL/KnowledgeBase_Schema.sql on StatsDB.";

        if (!KnowledgeBaseSchemaAvailable)
        {
            return;
        }

        await RefreshKnowledgeBaseAsync().ConfigureAwait(false);
    }

    private async Task RefreshKnowledgeBaseAsync()
    {
        if (!KnowledgeBaseSchemaAvailable)
        {
            return;
        }

        var categories = await _knowledgeBaseRepository.GetCategoriesAsync().ConfigureAwait(false);
        var tags = await _knowledgeBaseRepository.GetTagsAsync().ConfigureAwait(false);

        Application.Current.Dispatcher.Invoke(() =>
        {
            KnowledgeBaseCategories.Clear();
            KnowledgeBaseCategories.Add(new KnowledgeBaseCategory { CategoryId = 0, Name = "(All categories)" });
            KnowledgeBaseEditorCategories.Clear();
            KnowledgeBaseEditorCategories.Add(new KnowledgeBaseCategory { CategoryId = 0, Name = "(None)" });
            foreach (var category in categories)
            {
                KnowledgeBaseCategories.Add(category);
                KnowledgeBaseEditorCategories.Add(category);
            }

            _knowledgeBaseSuppressTagFilterRefresh = true;
            var selected = KnowledgeBaseTagFilters.Where(t => t.IsSelected).Select(t => t.TagName).ToHashSet(StringComparer.OrdinalIgnoreCase);
            foreach (var existing in KnowledgeBaseTagFilters)
            {
                existing.PropertyChanged -= KnowledgeBaseTagFilterItem_PropertyChanged;
            }

            KnowledgeBaseTagFilters.Clear();
            foreach (var tag in tags)
            {
                var filterItem = new KnowledgeBaseTagFilterItem
                {
                    TagName = tag.TagName,
                    UsageCount = tag.UsageCount,
                    IsSelected = selected.Contains(tag.TagName)
                };
                filterItem.PropertyChanged += KnowledgeBaseTagFilterItem_PropertyChanged;
                KnowledgeBaseTagFilters.Add(filterItem);
            }

            _knowledgeBaseSuppressTagFilterRefresh = false;
        });

        await RefreshKnowledgeBaseCardsAsync().ConfigureAwait(false);
    }

    private async Task RefreshKnowledgeBaseCardsAsync()
    {
        if (!KnowledgeBaseSchemaAvailable)
        {
            return;
        }

        int? categoryId = KnowledgeBaseFilterCategory?.CategoryId;
        if (categoryId == 0)
        {
            categoryId = null;
        }

        var filter = new KnowledgeBaseCardFilter
        {
            SearchText = KnowledgeBaseSearchText,
            CategoryId = categoryId,
            TagNames = KnowledgeBaseTagFilters.Where(t => t.IsSelected).Select(t => t.TagName).ToList()
        };

        var cards = await _knowledgeBaseRepository.ListCardsAsync(filter).ConfigureAwait(false);
        Application.Current.Dispatcher.Invoke(() =>
        {
            KnowledgeBaseCards.Clear();
            foreach (var card in cards)
            {
                KnowledgeBaseCards.Add(new KnowledgeBaseCardListItem
                {
                    CardId = card.CardId,
                    Title = string.IsNullOrWhiteSpace(card.Title) ? "(Untitled)" : card.Title,
                    BodyPreview = card.BodyPreview,
                    CategoryName = card.CategoryName,
                    ThumbnailData = card.ThumbnailData,
                    TagsDisplay = string.Join(", ", card.Tags),
                    ModifiedUtc = card.ModifiedUtc
                });
            }
        });
    }

    private async Task CreateKnowledgeBaseCardAsync()
    {
        if (!KnowledgeBaseSchemaAvailable)
        {
            return;
        }

        if (KnowledgeBaseIsEditMode && IsKnowledgeBaseEditorEmpty() && _knowledgeBaseEditingCardId <= 0)
        {
            Application.Current.Dispatcher.Invoke(() =>
            {
                ClearKnowledgeBaseEditorFields();
                _knowledgeBaseIsNewCard = true;
                SetKnowledgeBaseDetailPaneMode(KnowledgeBaseDetailPaneMode.Edit);
            });
            return;
        }

        await PrepareToSwitchKnowledgeBaseCardAsync().ConfigureAwait(false);
        _knowledgeBaseAutoSave.ResetSession();

        Application.Current.Dispatcher.Invoke(() =>
        {
            _knowledgeBaseEditingCardId = 0;
            _knowledgeBaseIsNewCard = true;
            _knowledgeBaseLoadingEditor = true;
            ClearKnowledgeBaseEditorFields();
            _knowledgeBaseLoadingEditor = false;
            SetKnowledgeBaseDetailPaneMode(KnowledgeBaseDetailPaneMode.Edit);
            KnowledgeBaseStatusText = "Ready";
        });
    }

    private async Task LoadKnowledgeBaseCardAsync(object? parameter)
    {
        if (parameter is not KnowledgeBaseCardListItem item)
        {
            return;
        }

        await PrepareToSwitchKnowledgeBaseCardAsync().ConfigureAwait(false);
        _knowledgeBaseAutoSave.ResetSession();
        _knowledgeBaseIsNewCard = false;
        await LoadKnowledgeBaseCardInternalAsync(item.CardId, KnowledgeBaseDetailPaneMode.View).ConfigureAwait(false);
    }

    private async Task LoadKnowledgeBaseCardInternalAsync(int cardId, KnowledgeBaseDetailPaneMode paneMode)
    {
        var detail = await _knowledgeBaseRepository.GetCardDetailAsync(cardId).ConfigureAwait(false);
        if (detail is null)
        {
            return;
        }

        var backup = await _knowledgeBaseBackupRepository.GetBackupAsync(cardId, Environment.MachineName).ConfigureAwait(false);

        Application.Current.Dispatcher.Invoke(() =>
        {
            _knowledgeBaseLoadingEditor = true;
            _knowledgeBaseEditingCardId = cardId;
            KnowledgeBaseEditorTitle = detail.Title;
            KnowledgeBaseEditorBody = detail.BodyText;
            KnowledgeBaseEditorLinks.Clear();
            foreach (var link in detail.Links)
            {
                KnowledgeBaseEditorLinks.Add(new KnowledgeBaseLinkEditorItem { Label = link.Label, Url = link.Url });
            }

            KnowledgeBaseEditorImages.Clear();
            foreach (var image in detail.Images)
            {
                KnowledgeBaseEditorImages.Add(new KnowledgeBaseImageEditorItem
                {
                    ImageId = image.ImageId,
                    ImageData = image.ImageData,
                    ThumbnailData = image.ThumbnailData,
                    FileName = image.FileName,
                    ContentType = image.ContentType,
                    SortOrder = image.SortOrder
                });
            }

            KnowledgeBaseEditorTags.Clear();
            foreach (var tag in detail.Tags)
            {
                KnowledgeBaseEditorTags.Add(tag);
            }

            KnowledgeBaseEditorCategoryName = detail.CategoryName ?? string.Empty;

            if (KnowledgeBaseFilterCategory is null)
            {
                KnowledgeBaseFilterCategory = KnowledgeBaseCategories.FirstOrDefault(c => c.CategoryId == 0);
            }

            KnowledgeBaseHasBackup = backup is not null;
            _knowledgeBaseLoadingEditor = false;
            KnowledgeBaseStatusText = "Ready";
            SetKnowledgeBaseDetailPaneMode(paneMode);
        });
    }

    private void EnterKnowledgeBaseEditMode()
    {
        if (_knowledgeBaseEditingCardId <= 0)
        {
            return;
        }

        _knowledgeBaseIsNewCard = false;
        SetKnowledgeBaseDetailPaneMode(KnowledgeBaseDetailPaneMode.Edit);
    }

    private async Task CloseKnowledgeBaseDetailAsync()
    {
        if (_knowledgeBaseDetailPaneMode == KnowledgeBaseDetailPaneMode.None)
        {
            return;
        }

        if (_knowledgeBaseDetailPaneMode == KnowledgeBaseDetailPaneMode.Edit)
        {
            if (IsKnowledgeBaseEditorEmpty())
            {
                await DiscardKnowledgeBaseDraftAsync().ConfigureAwait(false);
                return;
            }

            await PersistKnowledgeBaseDraftAsync().ConfigureAwait(false);
            await _knowledgeBaseAutoSave.FlushAsync().ConfigureAwait(false);

            if (_knowledgeBaseEditingCardId > 0)
            {
                Application.Current.Dispatcher.Invoke(() =>
                {
                    _knowledgeBaseIsNewCard = false;
                    SetKnowledgeBaseDetailPaneMode(KnowledgeBaseDetailPaneMode.View);
                });
                await RefreshKnowledgeBaseCardsAsync().ConfigureAwait(false);
            }

            return;
        }

        await DiscardKnowledgeBaseDraftAsync().ConfigureAwait(false);
    }

    private async Task PrepareToSwitchKnowledgeBaseCardAsync()
    {
        if (_knowledgeBaseEditingCardId <= 0)
        {
            if (!IsKnowledgeBaseEditorEmpty())
            {
                await PersistKnowledgeBaseDraftAsync().ConfigureAwait(false);
                await _knowledgeBaseAutoSave.FlushAsync().ConfigureAwait(false);
            }

            return;
        }

        if (IsKnowledgeBaseEditorEmpty())
        {
            await AbandonEmptyKnowledgeBaseCardAsync(_knowledgeBaseEditingCardId).ConfigureAwait(false);
            return;
        }

        await _knowledgeBaseAutoSave.FlushAsync().ConfigureAwait(false);
    }

    private async Task DiscardKnowledgeBaseDraftAsync()
    {
        if (_knowledgeBaseEditingCardId > 0 && IsKnowledgeBaseEditorEmpty())
        {
            await AbandonEmptyKnowledgeBaseCardAsync(_knowledgeBaseEditingCardId).ConfigureAwait(false);
        }

        Application.Current.Dispatcher.Invoke(() =>
        {
            SetKnowledgeBaseDetailPaneMode(KnowledgeBaseDetailPaneMode.None);
            _knowledgeBaseEditingCardId = 0;
            _knowledgeBaseIsNewCard = false;
            _knowledgeBaseAutoSave.ResetSession();
            ClearKnowledgeBaseEditorFields();
        });

        await RefreshKnowledgeBaseCardsAsync().ConfigureAwait(false);
    }

    private async Task AbandonEmptyKnowledgeBaseCardAsync(int cardId)
    {
        if (cardId <= 0)
        {
            return;
        }

        await _knowledgeBaseRepository.SoftDeleteCardAsync(cardId, Environment.MachineName).ConfigureAwait(false);

        Application.Current.Dispatcher.Invoke(() =>
        {
            if (_knowledgeBaseEditingCardId == cardId)
            {
                _knowledgeBaseEditingCardId = 0;
                ClearKnowledgeBaseEditorFields();
            }
        });
    }

    private bool IsKnowledgeBaseEditorEmpty()
    {
        int? categoryId = null;
        if (!string.IsNullOrWhiteSpace(KnowledgeBaseEditorCategoryName))
        {
            var existing = KnowledgeBaseEditorCategories.FirstOrDefault(c =>
                c.CategoryId > 0 &&
                c.Name.Equals(KnowledgeBaseEditorCategoryName.Trim(), StringComparison.OrdinalIgnoreCase));
            categoryId = existing?.CategoryId;
            if (categoryId is null or 0 && !string.IsNullOrWhiteSpace(KnowledgeBaseEditorCategoryName))
            {
                return false;
            }
        }

        return KnowledgeBaseCardContentRules.IsEmpty(
            KnowledgeBaseEditorTitle,
            KnowledgeBaseEditorBody,
            categoryId,
            KnowledgeBaseEditorTags,
            KnowledgeBaseEditorLinks.Select(l => new KnowledgeBaseCardLink
            {
                Label = l.Label,
                Url = l.Url
            }),
            KnowledgeBaseEditorImages.Select(i => new KnowledgeBaseCardImage
            {
                ImageId = i.ImageId,
                FileName = i.FileName,
                ContentType = i.ContentType,
                ImageData = i.ImageData,
                ThumbnailData = i.ThumbnailData,
                SortOrder = i.SortOrder
            }));
    }

    private async Task PersistKnowledgeBaseDraftAsync()
    {
        if (IsKnowledgeBaseEditorEmpty())
        {
            return;
        }

        if (_knowledgeBaseEditingCardId > 0)
        {
            _ = QueueKnowledgeBaseSaveAsync();
            return;
        }

        if (Interlocked.CompareExchange(ref _knowledgeBaseCreatingCard, 1, 0) != 0)
        {
            return;
        }

        try
        {
            int? categoryId = await ResolveEditorCategoryIdAsync().ConfigureAwait(false);
            int cardId = await _knowledgeBaseRepository.CreateCardAsync(Environment.MachineName).ConfigureAwait(false);
            await Application.Current.Dispatcher.InvokeAsync(() =>
            {
                _knowledgeBaseEditingCardId = cardId;
                QueueKnowledgeBaseSaveInternal(categoryId);
            });
        }
        finally
        {
            Interlocked.Exchange(ref _knowledgeBaseCreatingCard, 0);
        }
    }

    private void ClearKnowledgeBaseEditorFields()
    {
        KnowledgeBaseEditorTitle = string.Empty;
        KnowledgeBaseEditorBody = string.Empty;
        KnowledgeBaseEditorLinks.Clear();
        KnowledgeBaseEditorImages.Clear();
        KnowledgeBaseEditorTags.Clear();
        KnowledgeBaseEditorCategoryName = string.Empty;
        KnowledgeBaseHasBackup = false;
        KnowledgeBaseNewTagText = string.Empty;
    }

    private void SetKnowledgeBaseDetailPaneMode(KnowledgeBaseDetailPaneMode mode)
    {
        _knowledgeBaseDetailPaneMode = mode;
        RaisePropertyChanged(nameof(KnowledgeBaseDetailPaneVisible));
        RaisePropertyChanged(nameof(KnowledgeBaseIsViewMode));
        RaisePropertyChanged(nameof(KnowledgeBaseIsEditMode));
        RaisePropertyChanged(nameof(KnowledgeBaseDetailPaneTitle));
    }

    private void QueueKnowledgeBaseSave()
    {
        if (_knowledgeBaseLoadingEditor || !KnowledgeBaseSchemaAvailable || !KnowledgeBaseIsEditMode)
        {
            return;
        }

        if (IsKnowledgeBaseEditorEmpty())
        {
            return;
        }

        if (_knowledgeBaseEditingCardId <= 0)
        {
            _ = PersistKnowledgeBaseDraftAsync();
            return;
        }

        _ = QueueKnowledgeBaseSaveAsync();
    }

    private async Task QueueKnowledgeBaseSaveAsync()
    {
        if (_knowledgeBaseLoadingEditor || _knowledgeBaseEditingCardId <= 0 || !KnowledgeBaseSchemaAvailable || !KnowledgeBaseIsEditMode)
        {
            return;
        }

        if (IsKnowledgeBaseEditorEmpty())
        {
            return;
        }

        int? categoryId = await ResolveEditorCategoryIdAsync().ConfigureAwait(false);
        Application.Current.Dispatcher.Invoke(() => QueueKnowledgeBaseSaveInternal(categoryId));
    }

    private void QueueKnowledgeBaseSaveInternal(int? categoryId = null)
    {
        if (_knowledgeBaseLoadingEditor || _knowledgeBaseEditingCardId <= 0 || !KnowledgeBaseSchemaAvailable || !KnowledgeBaseIsEditMode)
        {
            return;
        }

        if (IsKnowledgeBaseEditorEmpty())
        {
            return;
        }

        if (categoryId is 0)
        {
            categoryId = null;
        }

        var request = new KnowledgeBaseSaveRequest
        {
            CardId = _knowledgeBaseEditingCardId,
            Title = KnowledgeBaseEditorTitle,
            BodyText = KnowledgeBaseEditorBody,
            CategoryId = categoryId,
            MachineName = Environment.MachineName,
            Links = KnowledgeBaseEditorLinks.Select((l, i) => new KnowledgeBaseCardLink
            {
                CardId = _knowledgeBaseEditingCardId,
                Label = l.Label,
                Url = l.Url,
                SortOrder = i
            }).ToList(),
            Images = KnowledgeBaseEditorImages.Select((img, i) => new KnowledgeBaseCardImage
            {
                ImageId = img.ImageId,
                CardId = _knowledgeBaseEditingCardId,
                FileName = img.FileName,
                ContentType = img.ContentType,
                ImageData = img.ImageData,
                ThumbnailData = img.ThumbnailData,
                SortOrder = i
            }).ToList(),
            Tags = KnowledgeBaseEditorTags.ToList()
        };

        _knowledgeBaseAutoSave.QueueSave(request);
    }

    public void KnowledgeBaseEditorLinksChanged()
    {
        QueueKnowledgeBaseSave();
    }

    public void KnowledgeBaseEditorTagsChanged()
    {
        QueueKnowledgeBaseSave();
    }

    private void KnowledgeBaseTagFilterItem_PropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (_knowledgeBaseSuppressTagFilterRefresh || e.PropertyName != nameof(KnowledgeBaseTagFilterItem.IsSelected))
        {
            return;
        }

        _ = RefreshKnowledgeBaseCardsAsync();
    }

    private void ClearKnowledgeBaseFilters()
    {
        _knowledgeBaseSuppressTagFilterRefresh = true;
        knowledgeBaseSearchText = string.Empty;
        RaisePropertyChanged(nameof(KnowledgeBaseSearchText));
        KnowledgeBaseFilterCategory = KnowledgeBaseCategories.FirstOrDefault(c => c.CategoryId == 0);
        foreach (var tag in KnowledgeBaseTagFilters)
        {
            tag.IsSelected = false;
        }

        KnowledgeBaseSearchImageBytes = null;
        KnowledgeBaseVisionResults.Clear();
        _knowledgeBaseSuppressTagFilterRefresh = false;
        _ = RefreshKnowledgeBaseCardsAsync();
    }

    private async Task<int?> ResolveEditorCategoryIdAsync()
    {
        string name = await Application.Current.Dispatcher.InvokeAsync(() => KnowledgeBaseEditorCategoryName.Trim());
        if (string.IsNullOrWhiteSpace(name))
        {
            return null;
        }

        int categoryId = await _knowledgeBaseRepository.EnsureCategoryAsync(name).ConfigureAwait(false);
        await Application.Current.Dispatcher.InvokeAsync(() => MergeCategoryIntoCollections(categoryId, name));
        return categoryId;
    }

    private void MergeCategoryIntoCollections(int categoryId, string name)
    {
        if (KnowledgeBaseEditorCategories.All(c => c.CategoryId != categoryId))
        {
            KnowledgeBaseEditorCategories.Add(new KnowledgeBaseCategory { CategoryId = categoryId, Name = name });
        }

        if (KnowledgeBaseCategories.All(c => c.CategoryId != categoryId))
        {
            KnowledgeBaseCategories.Add(new KnowledgeBaseCategory { CategoryId = categoryId, Name = name });
        }
    }

    public async Task KnowledgeBaseAddImageFromBytesAsync(byte[] bytes, string? fileName = null)
    {
        if (_knowledgeBaseEditingCardId <= 0 || bytes.Length == 0 || !KnowledgeBaseIsEditMode)
        {
            return;
        }

        var prepared = await Application.Current.Dispatcher.InvokeAsync(() => KnowledgeBaseImageHelper.PrepareImage(bytes, fileName));
        Application.Current.Dispatcher.Invoke(() =>
        {
            KnowledgeBaseEditorImages.Add(new KnowledgeBaseImageEditorItem
            {
                ImageData = prepared.ImageData,
                ThumbnailData = prepared.ThumbnailData,
                FileName = prepared.FileName,
                ContentType = prepared.ContentType
            });
            QueueKnowledgeBaseSave();
        });
    }

    private async Task RefreshKnowledgeBaseAfterSaveAsync()
    {
        if (_knowledgeBaseEditingCardId <= 0)
        {
            return;
        }

        var detail = await _knowledgeBaseRepository.GetCardDetailAsync(_knowledgeBaseEditingCardId).ConfigureAwait(false);
        if (detail is null)
        {
            return;
        }

        Application.Current.Dispatcher.Invoke(() =>
        {
            KnowledgeBaseEditorTags.Clear();
            foreach (var tag in detail.Tags)
            {
                KnowledgeBaseEditorTags.Add(tag);
            }
        });

        await RefreshKnowledgeBaseAsync().ConfigureAwait(false);
    }

    private void RemoveKnowledgeBaseLink(object? parameter)
    {
        if (parameter is KnowledgeBaseLinkEditorItem link)
        {
            KnowledgeBaseEditorLinks.Remove(link);
            QueueKnowledgeBaseSave();
        }
    }

    private void AddKnowledgeBaseEditorTag()
    {
        var tag = KnowledgeBaseTagNormalizer.Normalize(KnowledgeBaseNewTagText);
        if (string.IsNullOrWhiteSpace(tag) || KnowledgeBaseEditorTags.Any(t => t.Equals(tag, StringComparison.OrdinalIgnoreCase)))
        {
            return;
        }

        KnowledgeBaseEditorTags.Add(tag);
        KnowledgeBaseNewTagText = string.Empty;
        QueueKnowledgeBaseSave();
    }

    private void RemoveKnowledgeBaseEditorTag(object? parameter)
    {
        if (parameter is string tag)
        {
            KnowledgeBaseEditorTags.Remove(tag);
            QueueKnowledgeBaseSave();
        }
    }

    private async Task DeleteKnowledgeBaseCardAsync()
    {
        if (_knowledgeBaseEditingCardId <= 0)
        {
            await DiscardKnowledgeBaseDraftAsync().ConfigureAwait(false);
            return;
        }

        await _knowledgeBaseRepository.SoftDeleteCardAsync(_knowledgeBaseEditingCardId, Environment.MachineName).ConfigureAwait(false);
        _knowledgeBaseEditingCardId = 0;
        _knowledgeBaseIsNewCard = false;
        _knowledgeBaseAutoSave.ResetSession();

        Application.Current.Dispatcher.Invoke(() =>
        {
            SetKnowledgeBaseDetailPaneMode(KnowledgeBaseDetailPaneMode.None);
            ClearKnowledgeBaseEditorFields();
        });

        await RefreshKnowledgeBaseAsync().ConfigureAwait(false);
    }

    private void PreviewKnowledgeBaseBackup()
    {
        if (_knowledgeBaseEditingCardId <= 0)
        {
            return;
        }

        _ = PreviewKnowledgeBaseBackupAsync();
    }

    private async Task PreviewKnowledgeBaseBackupAsync()
    {
        var backup = await _knowledgeBaseBackupRepository.GetBackupAsync(_knowledgeBaseEditingCardId, Environment.MachineName).ConfigureAwait(false);
        if (backup is null)
        {
            return;
        }

        var snapshot = KnowledgeBaseSnapshotSerializer.Deserialize(backup.SnapshotJson);
        Application.Current.Dispatcher.Invoke(() =>
        {
            var window = new KnowledgeBaseBackupPreviewWindow(snapshot, backup.BackedUpUtc)
            {
                Owner = _MainWindow
            };
            window.ShowDialog();
        });
    }

    private async Task RestoreKnowledgeBaseBackupAsync()
    {
        if (_knowledgeBaseEditingCardId <= 0)
        {
            return;
        }

        var backup = await _knowledgeBaseBackupRepository.GetBackupAsync(_knowledgeBaseEditingCardId, Environment.MachineName).ConfigureAwait(false);
        if (backup is null)
        {
            return;
        }

        var snapshot = KnowledgeBaseSnapshotSerializer.Deserialize(backup.SnapshotJson);
        bool step1 = Application.Current.Dispatcher.Invoke(() =>
            MessageBox.Show(
                _MainWindow,
                $"Restore will replace the current card with the backup from {backup.BackedUpUtc:yyyy-MM-dd HH:mm} UTC.\n\nContinue?",
                "Restore backup — step 1 of 3",
                MessageBoxButton.YesNo,
                MessageBoxImage.Warning) == MessageBoxResult.Yes);

        if (!step1)
        {
            return;
        }

        bool step2 = Application.Current.Dispatcher.Invoke(() =>
            MessageBox.Show(
                _MainWindow,
                "This cannot be undone unless you edit again. The original backup will be kept.\n\nContinue?",
                "Restore backup — step 2 of 3",
                MessageBoxButton.YesNo,
                MessageBoxImage.Warning) == MessageBoxResult.Yes);

        if (!step2)
        {
            return;
        }

        bool step3 = Application.Current.Dispatcher.Invoke(() =>
        {
            var dialog = new KnowledgeBaseRestoreConfirmWindow { Owner = _MainWindow };
            return dialog.ShowDialog() == true;
        });

        if (!step3)
        {
            return;
        }

        await _knowledgeBaseRepository.RestoreSnapshotAsync(snapshot, Environment.MachineName).ConfigureAwait(false);
        await LoadKnowledgeBaseCardInternalAsync(_knowledgeBaseEditingCardId, KnowledgeBaseDetailPaneMode.Edit).ConfigureAwait(false);
        await RefreshKnowledgeBaseAsync().ConfigureAwait(false);
        KnowledgeBaseStatusText = "Restored from backup";
    }

    private async Task RunKnowledgeBaseVisionSearchAsync()
    {
        if (KnowledgeBaseSearchImageBytes is null || KnowledgeBaseSearchImageBytes.Length == 0 || !KnowledgeBaseSchemaAvailable)
        {
            return;
        }

        KnowledgeBaseVisionSearchRunning = true;
        try
        {
            var service = new KnowledgeBaseVisionSearchService();
            int? categoryId = KnowledgeBaseFilterCategory?.CategoryId;
            if (categoryId == 0)
            {
                categoryId = null;
            }

            var cardIds = KnowledgeBaseCards.Select(c => c.CardId).ToList();
            var matches = await service.SearchAsync(
                KnowledgeBaseSearchImageBytes,
                cardIds,
                () => ReadStatsSetting("Nvidia_API_KEY"),
                CancellationToken.None).ConfigureAwait(false);

            Application.Current.Dispatcher.Invoke(() =>
            {
                KnowledgeBaseVisionResults.Clear();
                foreach (var match in matches)
                {
                    var card = KnowledgeBaseCards.FirstOrDefault(c => c.CardId == match.CardId);
                    if (card is not null)
                    {
                        match.Title = card.Title;
                    }

                    KnowledgeBaseVisionResults.Add(match);
                }
            });
        }
        catch (Exception ex)
        {
            KnowledgeBaseStatusText = $"Vision search error: {ex.Message}";
        }
        finally
        {
            KnowledgeBaseVisionSearchRunning = false;
        }
    }
}
