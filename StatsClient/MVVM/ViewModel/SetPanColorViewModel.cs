using StatsClient.MVVM.Core;
using StatsClient.MVVM.Model;
using StatsClient.MVVM.View;
using System.Windows;
using static StatsClient.MVVM.Core.DatabaseOperations;

namespace StatsClient.MVVM.ViewModel;

public class SetPanColorViewModel : ObservableObject
{
    private static SetPanColorViewModel? staticInstance;
    public static SetPanColorViewModel? StaticInstance
    {
        get => staticInstance;
        set
        {
            staticInstance = value;
            RaisePropertyChangedStatic(nameof(StaticInstance));
        }
    }
    
    private List<PanColorModel> availablePanColors = [];
    public List<PanColorModel> AvailablePanColors
    {
        get => availablePanColors;
        set
        {
            availablePanColors = value;
            RaisePropertyChanged(nameof(AvailablePanColors));
        }
    }

    private bool isItDarkColor = true;
    public bool IsItDarkColor
    {
        get => isItDarkColor;
        set
        {
            isItDarkColor = value;
            RaisePropertyChanged(nameof(IsItDarkColor));
        }
    }

    private string windowTitle = "Pick the new color:";
    public string WindowTitle
    {
        get => windowTitle;
        set
        {
            windowTitle = value;
            RaisePropertyChanged(nameof(WindowTitle));
        }
    }
    
    private string panNumber = "";
    public string PanNumber
    {
        get => panNumber;
        set
        {
            panNumber = value;
            RaisePropertyChanged(nameof(PanNumber));
        }
    }
    
    private string originalColor = "";
    public string OriginalColor
    {
        get => originalColor;
        set
        {
            originalColor = value;
            RaisePropertyChanged(nameof(OriginalColor));
        }
    }

    public RelayCommand CloseWindowCommand { get; set; }
    public RelayCommand ItemClickedCommand { get; set; }

    public SetPanColorViewModel()
    {
        StaticInstance = this;
        CloseWindowCommand = new RelayCommand(o => CloseWindow());
        ItemClickedCommand = new RelayCommand(ItemClicked);
        GetAvailablePanColors();
    }

    private async void ItemClicked(object obj)
    {
        PanColorModel model = (PanColorModel)obj;

        await AddOrUpdatePanNumber(model.RgbColor!, PanNumber, model.FriendlyName!);
        CloseWindow();
    }

    private async void GetAvailablePanColors()
    {
        AvailablePanColors = await GetAvailablePanColorsAsync();
    }

    private void CloseWindow()
    {
        SetPanColorWindow.StaticInstance.Close();
    }
}
