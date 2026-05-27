using System.Windows;

namespace DCMViewer;

/// <summary>
/// Holds the viewer <see cref="ViewModels.MainViewModel"/> for bindings inside data templates
/// when <c>ElementName</c> / ancestor lookups fail after embed reparenting.
/// </summary>
public sealed class BindingProxy : Freezable
{
    public static readonly DependencyProperty DataProperty =
        DependencyProperty.Register(
            nameof(Data),
            typeof(object),
            typeof(BindingProxy),
            new PropertyMetadata(null));

    public object? Data
    {
        get => GetValue(DataProperty);
        set => SetValue(DataProperty, value);
    }

    protected override Freezable CreateInstanceCore() => new BindingProxy();
}
