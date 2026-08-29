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
        => Apply(
            view,
            records,
            () => searchText,
            matches,
            updateSearchResultInfo,
            deferRefresh);

    public static void Apply<TRecord>(
        ICollectionView? view,
        IEnumerable<TRecord> records,
        Func<string?> getSearchText,
        Predicate<TRecord> matches,
        Action<int> updateSearchResultInfo,
        Action<Action> deferRefresh)
    {
        ArgumentNullException.ThrowIfNull(getSearchText);

        ApplyFilter(
            view,
            records,
            getFilter: () => string.IsNullOrWhiteSpace(getSearchText()) ? null : matches,
            updateSearchResultInfo,
            deferRefresh);
    }

    public static void ApplyFilter<TRecord>(
        ICollectionView? view,
        IEnumerable<TRecord> records,
        Func<Predicate<TRecord>?> getFilter,
        Action<int> updateSearchResultInfo,
        Action<Action> deferRefresh)
    {
        ArgumentNullException.ThrowIfNull(getFilter);

        if (view is null)
            return;

        if (view is IEditableCollectionView editableView &&
            (editableView.IsAddingNew || editableView.IsEditingItem))
        {
            deferRefresh(() => ApplyFilter(view, records, getFilter, updateSearchResultInfo, deferRefresh));
            return;
        }

        var filter = getFilter();
        if (filter is null)
        {
            using (view.DeferRefresh())
                view.Filter = null;
            updateSearchResultInfo(records.Count());
            return;
        }

        using (view.DeferRefresh())
            view.Filter = obj => obj is TRecord record && filter(record);

        updateSearchResultInfo(view.Cast<object>().Count());
    }
}
