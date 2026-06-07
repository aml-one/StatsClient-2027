using System.ComponentModel;
using System.Windows;
using System.Windows.Controls;
using DCMViewer.ViewModels;

namespace StatsClient.MVVM.View;

public partial class OrderInfoDesignEditPanel : UserControl
{
    private MainViewModel? _viewerViewModel;

    public OrderInfoDesignEditPanel()
    {
        InitializeComponent();
        DataContext = null;
    }

    public void BindViewer(MainViewModel? viewModel)
    {
        if (_viewerViewModel is not null)
        {
            _viewerViewModel.PropertyChanged -= ViewerViewModelOnPropertyChanged;
        }

        _viewerViewModel = viewModel;
        DataContext = viewModel;

        if (_viewerViewModel is not null)
        {
            _viewerViewModel.PropertyChanged += ViewerViewModelOnPropertyChanged;
        }

        UpdateSculptSectionState();
    }

    public void UnbindViewer()
    {
        if (_viewerViewModel is not null)
        {
            _viewerViewModel.PropertyChanged -= ViewerViewModelOnPropertyChanged;
        }

        _viewerViewModel = null;
        DataContext = null;
    }

    private void ViewerViewModelOnPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName is nameof(MainViewModel.IsCutPlaneMode))
        {
            Dispatcher.BeginInvoke(UpdateSculptSectionState);
        }
    }

    private void UpdateSculptSectionState()
    {
        if (SculptSection is null)
        {
            return;
        }

        var dimCut = _viewerViewModel?.IsCutPlaneMode == true;
        SculptSection.IsEnabled = !dimCut;
        SculptSection.Opacity = dimCut ? 0.5 : 1.0;
    }

}
