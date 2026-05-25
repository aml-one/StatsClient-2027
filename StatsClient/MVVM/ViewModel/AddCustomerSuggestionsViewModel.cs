using StatsClient.MVVM.Core;
using StatsClient.MVVM.View;
using static StatsClient.MVVM.Core.DatabaseOperations;
using static StatsClient.MVVM.Core.Functions;

namespace StatsClient.MVVM.ViewModel;

public class AddCustomerSuggestionsViewModel : ObservableObject
{
    private static AddCustomerSuggestionsViewModel? staticInstance;
    public static AddCustomerSuggestionsViewModel? StaticInstance
    {
        get => staticInstance; 
        set
        {
            staticInstance = value;
            RaisePropertyChangedStatic(nameof(StaticInstance));
        }
    }
    
    private string? customerName;
    public string? CustomerName
    {
        get => customerName; 
        set
        {
            customerName = value;
            RaisePropertyChanged(nameof(CustomerName));
            GetCustomerSuggestions();
        }
    }
    
    private string? newName;
    public string? NewName
    {
        get => newName; 
        set
        {
            newName = value;
            RaisePropertyChanged(nameof(NewName));
        }
    }
    
    private List<string>? customerSuggestionsList;
    public List<string>? CustomerSuggestionsList
    {
        get => customerSuggestionsList; 
        set
        {
            customerSuggestionsList = value;
            RaisePropertyChanged(nameof(CustomerSuggestionsList));
        }
    }

    public RelayCommand AddCommand { get; set; }
    public RelayCommand CloseWindowCommand { get; set; }

    public AddCustomerSuggestionsViewModel()
    {
        StaticInstance = this;
        AddCommand = new RelayCommand(o => AddNewSuggestion());
        CloseWindowCommand = new RelayCommand(o => CloseWindow());
        GetCustomerSuggestions();
    }

    private async void GetCustomerSuggestions()
    {
        if (CustomerName is not null)
            CustomerSuggestionsList = await CustomerHasSuggestedName(CustomerName);
    }

    private async void AddNewSuggestion()
    {
        if (NewName is not null && CustomerName is not null)
        {
            if (await AddNewCustomerSuggestion(CustomerName, CleanUpCustomerName(NewName)))
            {
                SmartOrderNames2ViewModel.StaticInstance.SelectedCustomerName = NewName;
                CloseWindow();
            }
        }
    }

    private static void CloseWindow()
    {
        AddCustomerSuggestionsWindow.StaticInstance.Close();
    }
}
