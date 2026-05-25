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
        }
    }
    
    private MainMenuItemModel? selectedMenuItem;
    public MainMenuItemModel? SelectedMenuItem
    {
        get => selectedMenuItem;
        set
        {
            selectedMenuItem = value;
            RaisePropertyChanged(nameof(SelectedMenuItem));
            if (value != null)
            { 
                RunCommand();
            }
        }
    }

    public RelayCommand CloseMainMenuCommand { get; set; }
    public RelayCommand RunCommandCommand { get; set; }

    

    public MainMenuViewModel()
    {
        StaticInstance = this;
        Instance = this;

        CloseMainMenuCommand = new RelayCommand(o => CloseMainMenu());
        RunCommandCommand = new RelayCommand(o => RunCommand());

        _ = bool.TryParse(ReadLocalSetting("ModuleSmartOrderNames"), out bool moduleSmartOrderNames);
        
        if (moduleSmartOrderNames)
            ShowSmartRenameMenuItem();
        else
            HideSmartRenameMenuItem();
    }

    private void BuildMenu(string hideMenuItems = "", bool hideItems = true)
    {
        MenuItems.Clear();
        MenuItems =
        [
            new MainMenuItemModel { Icon = "/Images/ToolBar/update.png", Header = "Look for update", Command = "lookForUpdate", Visible = GetVisibility(hideMenuItems, hideItems, "lookForUpdate") },
            new MainMenuItemModel { Icon = "/Images/ToolBar/folder.png", Header = "Open Manufacturing folder", Command = "openManufFolder", Visible = GetVisibility(hideMenuItems, hideItems, "openManufFolder")},
            new MainMenuItemModel { Icon = "/Images/ToolBar/folder.png", Header = "Open Trios Inbox folder", Command = "openTriosInbox", Visible = GetVisibility(hideMenuItems, hideItems, "openTriosInbox")},
            new MainMenuItemModel { Icon = "/Images/ToolBar/rename.png", Header = "Smart Order Names", Command = "openSmartRenameWindw", Visible = GetVisibility(hideMenuItems, hideItems, "openSmartRenameWindw")},
        ];
    }

    private Visibility GetVisibility(string hideMenuItems, bool hidingItems, string menuCommand)
    {
        if (hidingItems)
        {
            if (hideMenuItems.Contains(menuCommand))
                return Visibility.Collapsed;
            else 
                return Visibility.Visible;
        }
        else
        {
            if (hideMenuItems.Contains(menuCommand))
                return Visibility.Visible;
            else
                return Visibility.Collapsed;
        }
    }

    public void ShowSmartRenameMenuItem()
    {
        BuildMenu("lookForUpdate|openManufFolder|openTriosInbox|openSmartRenameWindw", false);
    }
    
    public void HideSmartRenameMenuItem()
    {
        BuildMenu("openSmartRenameWindw", true);
    }

    private void CloseMainMenu()
    {
        if (SelectedMenuItem is null)
            MainViewModel.Instance.MainMenuOpen = Visibility.Hidden;
        
    }

    private void RunCommand()
    {
        if (SelectedMenuItem is not null)
        {
            MainViewModel.Instance.RunMainMenuCommand(SelectedMenuItem.Command!);
            MainViewModel.Instance.MainMenuOpen = Visibility.Hidden;
            MainMenu.StaticInstance.mainMenuListView.UnselectAll();
        }

        SelectedMenuItem = null;
    }

    
}
