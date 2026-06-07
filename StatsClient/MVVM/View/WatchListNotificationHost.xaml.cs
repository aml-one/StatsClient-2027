using StatsClient.MVVM.Services;
using System.Windows;

namespace StatsClient.MVVM.View;

public partial class WatchListNotificationHost : Window
{
    private const double RightMarginPx = 48;
    private readonly Dictionary<string, WatchListNotificationCard> _cardsByOrder = new(StringComparer.OrdinalIgnoreCase);

    public WatchListNotificationHost()
    {
        InitializeComponent();
        Loaded += (_, _) => Reposition();
    }

    public void ShowChange(WatchListStatusChange change)
    {
        if (string.IsNullOrWhiteSpace(change.IntOrderID))
            return;

        if (_cardsByOrder.TryGetValue(change.IntOrderID, out var existing))
        {
            existing.BeginDismiss();
            _cardsByOrder.Remove(change.IntOrderID);
        }

        var card = new WatchListNotificationCard();
        card.Apply(change);
        card.Dismissed += (_, _) => RemoveCard(change.IntOrderID, card);

        _cardsByOrder[change.IntOrderID] = card;
        NotificationStack.Children.Add(card);

        if (!IsVisible)
        {
            Show();
        }

        Reposition();
        card.PlaySlideIn();
    }

    private void RemoveCard(string orderId, WatchListNotificationCard card)
    {
        NotificationStack.Children.Remove(card);
        _cardsByOrder.Remove(orderId);

        if (NotificationStack.Children.Count == 0)
            Hide();
        else
            Reposition();
    }

    private void Reposition()
    {
        var area = SystemParameters.WorkArea;
        UpdateLayout();
        double width = Math.Max(360, ActualWidth > 0 ? ActualWidth : 360);
        double height = Math.Max(120, ActualHeight > 0 ? ActualHeight : 120);

        Left = area.Right - width - RightMarginPx;
        Top = area.Top;
        MaxHeight = area.Height - 24;
    }
}
