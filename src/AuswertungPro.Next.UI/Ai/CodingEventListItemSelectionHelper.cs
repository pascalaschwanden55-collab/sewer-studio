using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace AuswertungPro.Next.UI.Ai;

public static class CodingEventListItemSelectionHelper
{
    public static bool SelectContainingListBoxItem(DependencyObject? source)
    {
        var item = FindContainingListBoxItem(source);
        if (item is null)
            return false;

        item.IsSelected = true;
        item.Focus();
        return true;
    }

    private static ListBoxItem? FindContainingListBoxItem(DependencyObject? source)
    {
        var current = source;
        while (current != null && current is not ListBoxItem)
        {
            // Run/Inline-Elemente sind kein Visual, daher LogicalTreeHelper als Fallback.
            current = current is Visual or System.Windows.Media.Media3D.Visual3D
                ? VisualTreeHelper.GetParent(current)
                : LogicalTreeHelper.GetParent(current);
        }

        return current as ListBoxItem;
    }
}
