using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Windows;
using StatsClient.MVVM.Core;
using StatsClient.MVVM.Core.UsefulWebsites;
using StatsClient.MVVM.Model;
using StatsClient.MVVM.View;
using static StatsClient.MVVM.Core.Enums;

namespace StatsClient.MVVM.ViewModel;

public partial class MainViewModel
{
    private List<UsefulWebsiteModel> _usefulWebsitesMasterList = [];
    private bool _usefulWebsitesInitialized;

    public RelayCommand AddUsefulWebsiteCommand { get; private set; } = null!;
    public RelayCommand EditUsefulWebsiteCommand { get; private set; } = null!;
    public RelayCommand DeleteUsefulWebsiteCommand { get; private set; } = null!;
    public RelayCommand OpenUsefulWebsiteCommand { get; private set; } = null!;
    public RelayCommand ToggleUsefulWebsiteTagFilterCommand { get; private set; } = null!;
    public RelayCommand ClearUsefulWebsiteTagFiltersCommand { get; private set; } = null!;
    public RelayCommand ClearUsefulWebsiteSearchCommand { get; private set; } = null!;

    private List<UsefulWebsiteModel> usefulWebsiteList = [];
    public List<UsefulWebsiteModel> UsefulWebsiteList
    {
        get => usefulWebsiteList;
        set
        {
            usefulWebsiteList = value;
            RaisePropertyChanged(nameof(UsefulWebsiteList));
            RaisePropertyChanged(nameof(UsefulWebsiteCountText));
        }
    }

    public string UsefulWebsiteCountText =>
        UsefulWebsiteList.Count == 1 ? "1 link" : $"{UsefulWebsiteList.Count} links";

    private UsefulWebsiteModel? selectedUsefulWebsite;
    public UsefulWebsiteModel? SelectedUsefulWebsite
    {
        get => selectedUsefulWebsite;
        set
        {
            selectedUsefulWebsite = value;
            RaisePropertyChanged(nameof(SelectedUsefulWebsite));
        }
    }

    private string searchUsefulWebsites = string.Empty;
    public string SearchUsefulWebsites
    {
        get => searchUsefulWebsites;
        set
        {
            searchUsefulWebsites = value;
            RaisePropertyChanged(nameof(SearchUsefulWebsites));
            ApplyUsefulWebsiteFilters();
        }
    }

    private ObservableCollection<UsefulWebsiteTagChipItem> usefulWebsiteTagFilters = [];
    public ObservableCollection<UsefulWebsiteTagChipItem> UsefulWebsiteTagFilters
    {
        get => usefulWebsiteTagFilters;
        set
        {
            usefulWebsiteTagFilters = value;
            RaisePropertyChanged(nameof(UsefulWebsiteTagFilters));
        }
    }

    private bool usefulWebsitesSchemaMissing;
    public bool UsefulWebsitesSchemaMissing
    {
        get => usefulWebsitesSchemaMissing;
        set
        {
            usefulWebsitesSchemaMissing = value;
            RaisePropertyChanged(nameof(UsefulWebsitesSchemaMissing));
        }
    }

    private void InitializeUsefulWebsitesCommands()
    {
        AddUsefulWebsiteCommand = new RelayCommand(_ => AddUsefulWebsite());
        EditUsefulWebsiteCommand = new RelayCommand(EditUsefulWebsite);
        DeleteUsefulWebsiteCommand = new RelayCommand(DeleteUsefulWebsite);
        OpenUsefulWebsiteCommand = new RelayCommand(OpenUsefulWebsite);
        ToggleUsefulWebsiteTagFilterCommand = new RelayCommand(ToggleUsefulWebsiteTagFilter);
        ClearUsefulWebsiteTagFiltersCommand = new RelayCommand(_ => ClearUsefulWebsiteTagFilters());
        ClearUsefulWebsiteSearchCommand = new RelayCommand(_ => SearchUsefulWebsites = string.Empty);
    }

    private void SwitchToUsefulWebsitesTab()
    {
        Application.Current.Dispatcher.Invoke(() =>
        {
            LabnextCanReload = false;
            ClearAllSearchCriteria();
            _MainWindow.mainTabControl.SelectedItem = _MainWindow.usefulWebsitesTab;
            UpdateTabChromeForSelection();
        });

        _ = EnsureUsefulWebsitesLoadedAsync();
    }

    private async Task EnsureUsefulWebsitesLoadedAsync()
    {
        if (_usefulWebsitesInitialized)
        {
            ApplyUsefulWebsiteFilters();
            return;
        }

        await RefreshUsefulWebsitesAsync().ConfigureAwait(false);
        _usefulWebsitesInitialized = true;
    }

    private async Task RefreshUsefulWebsitesAsync()
    {
        bool schemaExists = await Task.Run(UsefulWebsiteRepository.SchemaExists).ConfigureAwait(false);
        if (!schemaExists)
        {
            await Application.Current.Dispatcher.InvokeAsync(() =>
            {
                UsefulWebsitesSchemaMissing = true;
                _usefulWebsitesMasterList = [];
                UsefulWebsiteTagFilters = [];
                UsefulWebsiteList = [];
            });
            return;
        }

        var websites = await UsefulWebsiteRepository.GetAllAsync().ConfigureAwait(false);
        var tagCounts = await UsefulWebsiteRepository.GetTagCountsAsync().ConfigureAwait(false);

        await Application.Current.Dispatcher.InvokeAsync(() =>
        {
            UsefulWebsitesSchemaMissing = false;
            _usefulWebsitesMasterList = websites;

            var selectedTags = UsefulWebsiteTagFilters
                .Where(t => t.IsSelected)
                .Select(t => t.TagName)
                .ToHashSet(StringComparer.OrdinalIgnoreCase);

            UsefulWebsiteTagFilters = new ObservableCollection<UsefulWebsiteTagChipItem>(
                tagCounts.Select(t => new UsefulWebsiteTagChipItem
                {
                    TagName = t.TagName,
                    UsageCount = t.UsageCount,
                    IsSelected = selectedTags.Contains(t.TagName),
                }));

            ApplyUsefulWebsiteFilters();
        });
    }

    private void ApplyUsefulWebsiteFilters()
    {
        IEnumerable<UsefulWebsiteModel> query = _usefulWebsitesMasterList;

        var activeTags = UsefulWebsiteTagFilters
            .Where(t => t.IsSelected)
            .Select(t => t.TagName)
            .ToList();

        if (activeTags.Count > 0)
        {
            query = query.Where(w =>
                activeTags.All(tag =>
                    w.Tags.Any(t => string.Equals(t, tag, StringComparison.OrdinalIgnoreCase))));
        }

        if (!string.IsNullOrWhiteSpace(SearchUsefulWebsites))
        {
            string keyword = SearchUsefulWebsites.Trim();
            query = query.Where(w =>
                w.Title.Contains(keyword, StringComparison.OrdinalIgnoreCase) ||
                w.Url.Contains(keyword, StringComparison.OrdinalIgnoreCase) ||
                w.TagsDisplay.Contains(keyword, StringComparison.OrdinalIgnoreCase));
        }

        UsefulWebsiteList = query.ToList();

        if (SelectedUsefulWebsite is not null &&
            UsefulWebsiteList.All(w => w.WebsiteId != SelectedUsefulWebsite.WebsiteId))
        {
            SelectedUsefulWebsite = null;
        }
    }

    private void ToggleUsefulWebsiteTagFilter(object? parameter)
    {
        if (parameter is not string tagName || string.IsNullOrWhiteSpace(tagName))
            return;

        UsefulWebsiteTagChipItem? chip = UsefulWebsiteTagFilters
            .FirstOrDefault(t => string.Equals(t.TagName, tagName, StringComparison.OrdinalIgnoreCase));

        if (chip is null)
            return;

        chip.IsSelected = !chip.IsSelected;
        RaisePropertyChanged(nameof(UsefulWebsiteTagFilters));
        ApplyUsefulWebsiteFilters();
    }

    private void ClearUsefulWebsiteTagFilters()
    {
        foreach (UsefulWebsiteTagChipItem chip in UsefulWebsiteTagFilters)
            chip.IsSelected = false;

        RaisePropertyChanged(nameof(UsefulWebsiteTagFilters));
        ApplyUsefulWebsiteFilters();
    }

    private async void AddUsefulWebsite()
    {
        var window = new UsefulWebsiteEntryWindow
        {
            Owner = _MainWindow,
        };

        if (window.ShowDialog() != true || window.Entry is null)
            return;

        window.Entry.SortOrder = _usefulWebsitesMasterList.Count;
        bool saved = await UsefulWebsiteRepository.InsertAsync(window.Entry, window.Entry.Tags).ConfigureAwait(false);
        if (!saved)
        {
            ShowUsefulWebsiteSaveError();
            return;
        }

        await RefreshUsefulWebsitesAsync().ConfigureAwait(false);
    }

    private async void EditUsefulWebsite(object? parameter)
    {
        if (ResolveUsefulWebsite(parameter) is not UsefulWebsiteModel website)
            return;

        var window = new UsefulWebsiteEntryWindow(website)
        {
            Owner = _MainWindow,
        };

        if (window.ShowDialog() != true || window.Entry is null)
            return;

        bool saved = await UsefulWebsiteRepository.UpdateAsync(window.Entry, window.Entry.Tags).ConfigureAwait(false);
        if (!saved)
        {
            ShowUsefulWebsiteSaveError();
            return;
        }

        await RefreshUsefulWebsitesAsync().ConfigureAwait(false);
    }

    private async void DeleteUsefulWebsite(object? parameter)
    {
        if (ResolveUsefulWebsite(parameter) is not UsefulWebsiteModel website)
            return;

        SMessageBoxResult confirm = ShowMessageBox(
            "Useful websites",
            $"Delete \"{website.Title}\"?",
            SMessageBoxButtons.YesNo,
            NotificationIcon.Question,
            15,
            _MainWindow);

        if (confirm != SMessageBoxResult.Yes)
            return;

        int websiteId = website.WebsiteId;
        bool deleted = await UsefulWebsiteRepository.DeleteAsync(websiteId).ConfigureAwait(false);
        if (!deleted)
        {
            MessageBox.Show(
                _MainWindow,
                "Could not delete the website link.",
                "Useful websites",
                MessageBoxButton.OK,
                MessageBoxImage.Error);
            return;
        }

        if (SelectedUsefulWebsite?.WebsiteId == websiteId)
            SelectedUsefulWebsite = null;

        await RefreshUsefulWebsitesAsync().ConfigureAwait(false);
    }

    private UsefulWebsiteModel? ResolveUsefulWebsite(object? parameter) =>
        parameter as UsefulWebsiteModel ?? SelectedUsefulWebsite;

    private void OpenUsefulWebsite(object? parameter)
    {
        string? url = parameter as string ?? SelectedUsefulWebsite?.Url;
        if (string.IsNullOrWhiteSpace(url))
            return;

        url = UsefulWebsiteRepository.NormalizeUrl(url);
        try
        {
            Process.Start(new ProcessStartInfo { FileName = url, UseShellExecute = true });
        }
        catch (Exception ex)
        {
            AddDebugLine(ex);
        }
    }

    private void ShowUsefulWebsiteSaveError()
    {
        Application.Current.Dispatcher.Invoke(() =>
            MessageBox.Show(
                _MainWindow,
                "Could not save the website link. Check database connection and run Docs/SQL/UsefulWebsites_Schema.sql on StatsDB.",
                "Useful websites",
                MessageBoxButton.OK,
                MessageBoxImage.Error));
    }
}

public sealed class UsefulWebsiteTagChipItem : ObservableObject
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
