using StatsClient.MVVM.Core;
using StatsClient.MVVM.Model;
using StatsClient.MVVM.View;
using System.Windows;
using static StatsClient.MVVM.Core.LocalSettingsDB;

namespace StatsClient.MVVM.ViewModel;

public class MainMenuViewModel : ObservableObject
{
    private static MainMenuViewModel? staticInstance;
    public static MainMenuViewModel StaticInstance
    {
        get => staticInstance!;
        set
        {
            staticInstance = value;
            RaisePropertyChangedStatic(nameof(StaticInstance));
        }
    }

    private MainMenuViewModel? instance;
    public MainMenuViewModel Instance
    {
        get => instance!;
        set
        {
            instance = value;
            RaisePropertyChanged(nameof(Instance));
        }
    }

    private List<MainMenuItemModel> menuItems = [];
    public List<MainMenuItemModel> MenuItems
    {
        get => menuItems;
        set
        {
            menuItems = value;
            RaisePropertyChanged(nameof(MenuItems));
            RaisePropertyChanged(nameof(MenuTileItems));
        }
    }

    public IEnumerable<MainMenuItemModel> MenuTileItems =>
        MenuItems.Where(item => item.Visible == Visibility.Visible);

    public MainMenuItemModel SettingsMenuItem { get; } = new()
    {
        Icon = "/Images/HomeButtons/settings_dark.png",
        Header = "Settings",
        Command = "openSettings"
    };

    public RelayCommand CloseMainMenuCommand { get; set; }
    public RelayCommand RunMenuItemCommand { get; set; }

    public MainMenuViewModel()
    {
        StaticInstance = this;
        Instance = this;

        CloseMainMenuCommand = new RelayCommand(_ => CloseMainMenu());
        RunMenuItemCommand = new RelayCommand(RunMenuItem);

        RefreshMenuItems();
    }

    public void RefreshMenuItems()
    {
        _ = bool.TryParse(ReadLocalSetting("ModuleSmartOrderNames"), out bool moduleSmartOrderNames);
        _ = bool.TryParse(ReadLocalSetting("ModuleAccountInfos"), out bool moduleAccountInfos);

        MenuItems =
        [
            new MainMenuItemModel { Icon = "/Images/ToolBar/update.png", Header = "Look for update", Command = "lookForUpdate" },
            new MainMenuItemModel { Icon = "/Images/ToolBar/folder.png", Header = "Manufacturing folder", Command = "openManufFolder" },
            new MainMenuItemModel { Icon = "/Images/ToolBar/folder.png", Header = "Trios Inbox", Command = "openTriosInbox" },
            new MainMenuItemModel
            {
                Icon = "/Images/ToolBar/rename.png",
                Header = "Smart Order Names",
                Command = "openSmartRenameWindw",
                Visible = moduleSmartOrderNames ? Visibility.Visible : Visibility.Collapsed
            },
            new MainMenuItemModel { IconGlyph = "\uE7B3", Header = "Watch list", Command = "openWatchList" },
            new MainMenuItemModel
            {
                Icon = "/Images/AccountInfoIcons/key.png",
                Header = "Passwords",
                Command = "openAccountInfos",
                Visible = moduleAccountInfos ? Visibility.Visible : Visibility.Collapsed
            },
            new MainMenuItemModel
            {
                Icon = "/Images/AccountInfoIcons/web.png",
                Header = "Useful websites",
                Command = "openUsefulWebsites"
            },
            new MainMenuItemModel
            {
                Icon = "/Images/Other/scanbody.png",
                Header = "Scan body gallery",
                Command = "openScanBodyGallery"
            }
        ];
    }

    public void ShowSmartRenameMenuItem() => RefreshMenuItems();

    public void HideSmartRenameMenuItem() => RefreshMenuItems();

    public void RefreshAccountInfosMenuItem() => RefreshMenuItems();

    private void CloseMainMenu()
    {
        MainViewModel.Instance.MainMenuOpen = Visibility.Collapsed;
    }

    private void RunMenuItem(object? parameter)
    {
        if (parameter is not MainMenuItemModel item || string.IsNullOrWhiteSpace(item.Command))
        {
            return;
        }

        MainViewModel.Instance.RunMainMenuCommand(item.Command);
        MainViewModel.Instance.MainMenuOpen = Visibility.Collapsed;
    }
}
