using System.Windows.Controls;
using System.Windows.Input;

namespace StatsClient.MVVM.Core;

public class TabControlX : TabControl
{
    protected override void OnKeyDown(KeyEventArgs e)
    {
        if (e.Key == Key.Home || e.Key == Key.End)
            return;  // don't process keys

        base.OnKeyDown(e);
    }
}
