using System.Collections.Generic;
using System.ComponentModel;
using System.Windows.Controls;
using System.Windows.Data;

namespace AuswertungPro.Next.UI.Views.Pages;

public static class DataGridSortResetController
{
    public static void Reset(ICollectionView? view, IEnumerable<DataGridColumn> columns)
    {
        if (view is null)
            return;

        view.SortDescriptions.Clear();
        if (view is ListCollectionView listView)
            listView.CustomSort = null;

        foreach (var column in columns)
            column.SortDirection = null;
    }
}
