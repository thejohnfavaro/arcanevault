using System.Windows;
using System.Windows.Controls;

namespace ArcaneVault.Next.Desktop;

/// <summary>
/// WPF MouseDoubleClick bubbles even from empty ListView space and column
/// headers. Only real hit-tested card row containers may open card details.
/// </summary>
internal static class NativeCardRowActivation
{
    public static object? SelectClickedRow(ListView list, object? hitSource)
    {
        ArgumentNullException.ThrowIfNull(list);
        if (hitSource is not DependencyObject visual) return null;
        if (ItemsControl.ContainerFromElement(list, visual) is not ListViewItem row)
            return null;
        object? item = list.ItemContainerGenerator.ItemFromContainer(row);
        if (item is null || ReferenceEquals(item, DependencyProperty.UnsetValue))
            return null;
        list.SelectedItem = item;
        return item;
    }
}
