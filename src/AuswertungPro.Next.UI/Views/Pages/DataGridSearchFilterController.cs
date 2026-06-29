using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Windows.Data;

namespace AuswertungPro.Next.UI.Views.Pages;

public static class DataGridSearchFilterController
{
    public static void Apply<TRecord>(
        ICollectionView? view,
        IEnumerable<TRecord> records,
        string? searchText,
        Predicate<TRecord> matches,
        Action<int> updateSearchResultInfo,
        Action<Action> deferRefresh)
    {
        if (view is null)
            return;

        if (view is IEditableCollectionView editableView &&
            (editableView.IsAddingNew || editableView.IsEditingItem))
        {
            deferRefresh(() => Apply(view, records, searchText, matches, updateSearchResultInfo, deferRefresh));
            return;
        }

        if (string.IsNullOrWhiteSpace(searchText))
        {
            using (view.DeferRefresh())
                view.Filter = null;
            updateSearchResultInfo(records.Count());
            return;
        }

        using (view.DeferRefresh())
            view.Filter = obj => obj is TRecord record && matches(record);

        updateSearchResultInfo(view.Cast<object>().Count());
    }
}
