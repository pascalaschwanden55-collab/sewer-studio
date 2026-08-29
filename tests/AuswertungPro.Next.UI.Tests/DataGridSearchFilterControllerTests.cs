using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Threading;
using System.Windows.Data;
using AuswertungPro.Next.UI.Views.Pages;
using Xunit;

namespace AuswertungPro.Next.UI.Tests;

public sealed class DataGridSearchFilterControllerTests
{
    [Fact]
    public void Apply_clears_filter_and_reports_total_count_for_blank_search()
    {
        RunOnSta(() =>
        {
            var rows = new ObservableCollection<Row>
            {
                new("Alpha"),
                new("Beta")
            };
            var view = CollectionViewSource.GetDefaultView(rows);
            view.Filter = _ => false;
            var reportedCount = -1;

            DataGridSearchFilterController.Apply(
                view,
                rows,
                searchText: " ",
                matches: row => row.Name.Contains("Alpha", StringComparison.OrdinalIgnoreCase),
                updateSearchResultInfo: count => reportedCount = count,
                deferRefresh: _ => throw new InvalidOperationException("No deferral expected."));

            Assert.Null(view.Filter);
            Assert.Equal(2, reportedCount);
        });
    }

    [Fact]
    public void Apply_sets_filter_and_reports_visible_count_for_search_text()
    {
        RunOnSta(() =>
        {
            var rows = new ObservableCollection<Row>
            {
                new("Alpha"),
                new("Beta"),
                new("Alpine")
            };
            var view = CollectionViewSource.GetDefaultView(rows);
            var reportedCount = -1;

            DataGridSearchFilterController.Apply(
                view,
                rows,
                searchText: "al",
                matches: row => row.Name.Contains("al", StringComparison.OrdinalIgnoreCase),
                updateSearchResultInfo: count => reportedCount = count,
                deferRefresh: _ => throw new InvalidOperationException("No deferral expected."));

            Assert.Equal(2, reportedCount);
            Assert.Equal(new[] { "Alpha", "Alpine" }, view.Cast<Row>().Select(x => x.Name).ToArray());
        });
    }

    [Fact]
    public void Apply_defers_filtering_while_collection_view_is_adding_new()
    {
        RunOnSta(() =>
        {
            var rows = new ObservableCollection<Row>
            {
                new("Alpha"),
                new("Beta")
            };
            var view = CollectionViewSource.GetDefaultView(rows);
            var editableView = Assert.IsAssignableFrom<IEditableCollectionView>(view);
            var reportedCount = -1;
            Action? deferred = null;

            editableView.AddNew();

            DataGridSearchFilterController.Apply(
                view,
                rows,
                searchText: "al",
                matches: row => row.Name.Contains("al", StringComparison.OrdinalIgnoreCase),
                updateSearchResultInfo: count => reportedCount = count,
                deferRefresh: action => deferred = action);

            Assert.NotNull(deferred);
            Assert.Equal(-1, reportedCount);

            editableView.CancelNew();
            deferred();

            Assert.Equal(1, reportedCount);
        });
    }

    [Fact]
    public void Apply_deferred_filtering_uses_latest_search_text()
    {
        RunOnSta(() =>
        {
            var rows = new ObservableCollection<Row>
            {
                new("Alpha"),
                new("Beta")
            };
            var view = CollectionViewSource.GetDefaultView(rows);
            var editableView = Assert.IsAssignableFrom<IEditableCollectionView>(view);
            var reportedCount = -1;
            var searchText = " ";
            Action? deferred = null;

            editableView.AddNew();

            DataGridSearchFilterController.Apply(
                view,
                rows,
                getSearchText: () => searchText,
                matches: row => row.Name.Contains(searchText, StringComparison.OrdinalIgnoreCase),
                updateSearchResultInfo: count => reportedCount = count,
                deferRefresh: action => deferred = action);

            Assert.NotNull(deferred);
            Assert.Equal(-1, reportedCount);

            searchText = "al";
            editableView.CancelNew();
            deferred();

            Assert.Equal(1, reportedCount);
            Assert.Equal(new[] { "Alpha" }, view.Cast<Row>().Select(x => x.Name).ToArray());
        });
    }

    [Fact]
    public void ApplyFilter_applies_one_combined_predicate_and_reports_visible_count()
    {
        RunOnSta(() =>
        {
            var rows = new ObservableCollection<Row>
            {
                new("Alpha"),
                new("Alpine"),
                new("Beta")
            };
            var view = CollectionViewSource.GetDefaultView(rows);
            var reportedCount = -1;

            DataGridSearchFilterController.ApplyFilter(
                view,
                rows,
                getFilter: () => row => row.Name.StartsWith("Al", StringComparison.OrdinalIgnoreCase)
                                       && row.Name.EndsWith("a", StringComparison.OrdinalIgnoreCase),
                updateSearchResultInfo: count => reportedCount = count,
                deferRefresh: _ => throw new InvalidOperationException("No deferral expected."));

            Assert.Equal(1, reportedCount);
            Assert.Equal(new[] { "Alpha" }, view.Cast<Row>().Select(x => x.Name).ToArray());
        });
    }

    [Fact]
    public void ApplyFilter_deferred_callback_uses_latest_combined_filter()
    {
        RunOnSta(() =>
        {
            var rows = new ObservableCollection<Row>
            {
                new("Alpha"),
                new("Beta")
            };
            var view = CollectionViewSource.GetDefaultView(rows);
            var editableView = Assert.IsAssignableFrom<IEditableCollectionView>(view);
            Predicate<Row>? currentFilter = row => row.Name == "Alpha";
            Action? deferred = null;
            var reportedCount = -1;
            editableView.AddNew();

            DataGridSearchFilterController.ApplyFilter(
                view,
                rows,
                getFilter: () => currentFilter,
                updateSearchResultInfo: count => reportedCount = count,
                deferRefresh: action => deferred = action);

            currentFilter = row => row.Name == "Beta";
            editableView.CancelNew();
            Assert.NotNull(deferred);
            deferred();

            Assert.Equal(1, reportedCount);
            Assert.Equal(new[] { "Beta" }, view.Cast<Row>().Select(x => x.Name).ToArray());
        });
    }

    private sealed class Row
    {
        public Row()
            : this("")
        {
        }

        public Row(string name)
        {
            Name = name;
        }

        public string Name { get; }
    }

    private static void RunOnSta(Action action)
    {
        Exception? exception = null;
        var thread = new Thread(() =>
        {
            try
            {
                action();
            }
            catch (Exception ex)
            {
                exception = ex;
            }
        });

        thread.SetApartmentState(ApartmentState.STA);
        thread.Start();
        thread.Join();

        if (exception is not null)
            throw exception;
    }
}
