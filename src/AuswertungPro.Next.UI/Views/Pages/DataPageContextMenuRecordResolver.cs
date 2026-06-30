using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using AuswertungPro.Next.Domain.Models;

namespace AuswertungPro.Next.UI.Views.Pages;

public static class DataPageContextMenuRecordResolver
{
    public static HaltungRecord? Resolve(object sender, HaltungRecord? selected)
        => ResolveFromSender(sender) ?? selected;

    public static HaltungRecord? ResolveFromSender(object sender)
    {
        if (sender is not DependencyObject dependencyObject)
            return null;

        var current = dependencyObject;
        while (current is not null)
        {
            if (current is FrameworkElement { DataContext: HaltungRecord record })
                return record;

            if (current is ContextMenu menu)
            {
                if (menu.PlacementTarget is DataGridRow row)
                    return row.Item as HaltungRecord;
                if (menu.PlacementTarget is DataGrid grid)
                    return grid.SelectedItem as HaltungRecord;
            }

            current = LogicalTreeHelper.GetParent(current) ?? VisualTreeHelper.GetParent(current);
        }

        return null;
    }
}
