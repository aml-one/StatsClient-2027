using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Input;

namespace StatsClient.MVVM.View;

public partial class OrderScanPickerWindow : Window
{
    private readonly Func<OrderScanPickerItem, Task<bool>> _toggleItemAsync;

    public ObservableCollection<OrderScanPickerItem> Items { get; }

    public OrderScanPickerWindow(IEnumerable<OrderScanPickerItem> items, Func<OrderScanPickerItem, Task<bool>> toggleItemAsync)
    {
        InitializeComponent();

        Items = new ObservableCollection<OrderScanPickerItem>(items.OrderBy(i => i.Group).ThenBy(i => i.DisplayName, StringComparer.OrdinalIgnoreCase));
        _toggleItemAsync = toggleItemAsync;

        DataContext = this;

        var view = CollectionViewSource.GetDefaultView(Items);
        if (view is ListCollectionView listCollectionView)
        {
            listCollectionView.GroupDescriptions.Clear();
            listCollectionView.GroupDescriptions.Add(new PropertyGroupDescription(nameof(OrderScanPickerItem.Group)));
        }
    }

    private async void ActionButton_Click(object sender, RoutedEventArgs e)
    {
        if (sender is not Button { DataContext: OrderScanPickerItem item })
        {
            return;
        }

        try
        {
            Mouse.OverrideCursor = Cursors.Wait;
            bool changed = await _toggleItemAsync(item);
            if (changed)
            {
                item.IsLoaded = !item.IsLoaded;
            }
        }
        finally
        {
            Mouse.OverrideCursor = null;
        }
    }

    private void CloseButton_Click(object sender, RoutedEventArgs e)
    {
        Close();
    }
}

public sealed class OrderScanPickerItem : INotifyPropertyChanged
{
    private bool _isLoaded;

    public string Group { get; init; } = string.Empty;

    public string DisplayName { get; init; } = string.Empty;

    public string FullPath { get; init; } = string.Empty;

    public bool IsLoaded
    {
        get => _isLoaded;
        set
        {
            if (_isLoaded == value)
            {
                return;
            }

            _isLoaded = value;
            OnPropertyChanged();
        }
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    private void OnPropertyChanged([CallerMemberName] string? propertyName = null)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }
}
