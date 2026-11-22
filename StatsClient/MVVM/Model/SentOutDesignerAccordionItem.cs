using StatsClient.MVVM.Core;
using StatsClient.MVVM.View;

namespace StatsClient.MVVM.Model;

public class SentOutDesignerAccordionItem : ObservableObject
{
    private bool isExpanded;
    private double totalUnits;
    private double totalOrders;
    private string designerName = "";
    private UserPanel? panel;

    public string DesignerID { get; init; } = "";

    public string DesignerName
    {
        get => designerName;
        set
        {
            if (designerName == value)
                return;
            designerName = value;
            RaisePropertyChanged(nameof(DesignerName));
        }
    }

    public double TotalUnits
    {
        get => totalUnits;
        set
        {
            if (Math.Abs(totalUnits - value) < 0.01)
                return;
            totalUnits = value;
            RaisePropertyChanged(nameof(TotalUnits));
            RaisePropertyChanged(nameof(HasWork));
            RaisePropertyChanged(nameof(IsVisibleInAccordion));
        }
    }

    public double TotalOrders
    {
        get => totalOrders;
        set
        {
            if (Math.Abs(totalOrders - value) < 0.01)
                return;
            totalOrders = value;
            RaisePropertyChanged(nameof(TotalOrders));
        }
    }

    public bool IsExpanded
    {
        get => isExpanded;
        set
        {
            if (isExpanded == value)
                return;
            isExpanded = value;
            RaisePropertyChanged(nameof(IsExpanded));
        }
    }

    public bool HasWork => TotalUnits >= 1;

    public bool IsVisibleInAccordion => HasWork;

    public UserPanel? Panel
    {
        get => panel;
        set
        {
            panel = value;
            RaisePropertyChanged(nameof(Panel));
        }
    }
}
