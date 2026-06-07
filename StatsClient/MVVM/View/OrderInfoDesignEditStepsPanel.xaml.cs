using System.Windows.Controls;
using DCMViewer.ViewModels;

namespace StatsClient.MVVM.View;

public partial class OrderInfoDesignEditStepsPanel : UserControl
{
    public OrderInfoDesignEditStepsPanel()
    {
        InitializeComponent();
        DataContext = null;
    }

    public void BindViewer(MainViewModel? viewModel)
    {
        DataContext = viewModel;
    }

    public void UnbindViewer()
    {
        DataContext = null;
    }
}
