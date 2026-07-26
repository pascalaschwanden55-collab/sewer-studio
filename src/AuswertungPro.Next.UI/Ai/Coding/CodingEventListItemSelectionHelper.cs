using System.Windows;
using System.Windows.Controls;
using AuswertungPro.Next.UI.Behaviors;

namespace AuswertungPro.Next.UI.Ai.Coding;

public static class CodingEventListItemSelectionHelper
{
    public static bool SelectContainingListBoxItem(DependencyObject? source)
    {
        var item = VisualTreeSafe.FindAncestor<ListBoxItem>(source);
        if (item is null)
            return false;

        item.IsSelected = true;
        item.Focus();
        return true;
    }
}
