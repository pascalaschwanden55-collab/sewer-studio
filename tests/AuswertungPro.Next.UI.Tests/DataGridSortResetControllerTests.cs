using System.Collections;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Threading;
using System.Windows.Controls;
using System.Windows.Data;
using AuswertungPro.Next.UI.Views.Pages;

namespace AuswertungPro.Next.UI.Tests;

public sealed class DataGridSortResetControllerTests
{
    [Fact]
    public void Reset_clears_sort_descriptions_and_column_sort_directions()
    {
        RunOnSta(() =>
        {
            var rows = new ObservableCollection<Row>
            {
                new("Beta"),
                new("Alpha")
            };
            var view = CollectionViewSource.GetDefaultView(rows);
            view.SortDescriptions.Add(new SortDescription(nameof(Row.Name), ListSortDirection.Ascending));
            var columns = new DataGridColumn[]
            {
                new DataGridTextColumn { SortDirection = ListSortDirection.Ascending },
                new DataGridTextColumn { SortDirection = ListSortDirection.Descending }
            };

            DataGridSortResetController.Reset(view, columns);

            Assert.Empty(view.SortDescriptions);
            Assert.All(columns, column => Assert.Null(column.SortDirection));
        });
    }

    [Fact]
    public void Reset_clears_list_collection_view_custom_sort()
    {
        RunOnSta(() =>
        {
            var rows = new ObservableCollection<Row>
            {
                new("Beta"),
                new("Alpha")
            };
            var view = Assert.IsType<ListCollectionView>(CollectionViewSource.GetDefaultView(rows));
            view.CustomSort = new RowNameDescendingComparer();

            DataGridSortResetController.Reset(view, Array.Empty<DataGridColumn>());

            Assert.Null(view.CustomSort);
        });
    }

    [Fact]
    public void Reset_does_not_touch_columns_when_view_is_null()
    {
        RunOnSta(() =>
        {
            var column = new DataGridTextColumn { SortDirection = ListSortDirection.Ascending };

            DataGridSortResetController.Reset(null, new[] { column });

            Assert.Equal(ListSortDirection.Ascending, column.SortDirection);
        });
    }

    private sealed record Row(string Name);

    private sealed class RowNameDescendingComparer : IComparer
    {
        public int Compare(object? x, object? y)
        {
            var left = (Row)x!;
            var right = (Row)y!;
            return string.Compare(right.Name, left.Name, StringComparison.Ordinal);
        }
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
