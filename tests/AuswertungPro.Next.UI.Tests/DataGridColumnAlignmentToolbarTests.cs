using System.Threading;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Media;
using AuswertungPro.Next.UI.Views.Pages;

namespace AuswertungPro.Next.UI.Tests;

public sealed class DataGridColumnAlignmentToolbarTests
{
    [Fact]
    public void ApplyHorizontalAlignment_uses_current_cell_and_updates_buttons()
    {
        RunOnSta(() =>
        {
            var grid = new DataGrid();
            grid.SelectionUnit = DataGridSelectionUnit.CellOrRowHeader;
            var row = new object();
            grid.Items.Add(row);
            var column = AddTextColumn(grid, "Kosten");
            var layoutController = new DataGridColumnLayoutController();
            var buttons = CreateButtons();
            var toolbar = new DataGridColumnAlignmentToolbar(grid, layoutController, buttons);
            grid.CurrentCell = new DataGridCellInfo(row, column);

            toolbar.ApplyHorizontalAlignment(HorizontalAlignment.Right);

            Assert.Equal(HorizontalAlignment.Right, layoutController.GetHorizontalAlignment(column));
            Assert.Equal(VerticalAlignment.Center, layoutController.GetVerticalAlignment(column));
            Assert.True(buttons.Right.IsChecked);
            Assert.True(buttons.Middle.IsChecked);
            Assert.False(buttons.Left.IsChecked);
            Assert.False(buttons.Center.IsChecked);
        });
    }

    [Fact]
    public void TrackCurrentCell_keeps_active_column_for_later_alignment()
    {
        RunOnSta(() =>
        {
            var grid = new DataGrid();
            var row = new object();
            grid.Items.Add(row);
            var first = AddTextColumn(grid, "Schachtnummer");
            var second = AddTextColumn(grid, "Funktion");
            var layoutController = new DataGridColumnLayoutController();
            var buttons = CreateButtons();
            var toolbar = new DataGridColumnAlignmentToolbar(grid, layoutController, buttons);

            grid.CurrentCell = new DataGridCellInfo(row, first);
            toolbar.TrackCurrentCell();
            grid.CurrentCell = new DataGridCellInfo(row, second);

            toolbar.ApplyVerticalAlignment(VerticalAlignment.Bottom);

            Assert.Equal(VerticalAlignment.Bottom, layoutController.GetVerticalAlignment(first));
            Assert.Equal(VerticalAlignment.Center, layoutController.GetVerticalAlignment(second));
            Assert.True(buttons.Bottom.IsChecked);
            Assert.True(buttons.Left.IsChecked);
        });
    }

    [Fact]
    public void TrackSelectedCells_keeps_selected_column_for_later_alignment()
    {
        RunOnSta(() =>
        {
            var grid = new DataGrid
            {
                SelectionUnit = DataGridSelectionUnit.CellOrRowHeader,
                Width = 500,
                Height = 240
            };
            var row = new object();
            grid.Items.Add(row);
            var first = AddTextColumn(grid, "NR");
            var second = AddTextColumn(grid, "Haltungsname");
            var layoutController = new DataGridColumnLayoutController();
            var buttons = CreateButtons();
            var toolbar = new DataGridColumnAlignmentToolbar(grid, layoutController, buttons);
            var host = new Window
            {
                Content = grid,
                Width = grid.Width,
                Height = grid.Height,
                ShowInTaskbar = false,
                WindowStyle = WindowStyle.None
            };

            try
            {
                host.Show();
                grid.UpdateLayout();
                grid.CurrentCell = new DataGridCellInfo(row, second);
                grid.SelectedCells.Clear();
                grid.SelectedCells.Add(new DataGridCellInfo(row, second));
                Assert.Single(grid.SelectedCells);
                Assert.Same(second, grid.SelectedCells[0].Column);
                toolbar.TrackSelectedCells();
                grid.SelectedCells.Clear();
                grid.CurrentCell = new DataGridCellInfo(row, first);

                toolbar.ApplyHorizontalAlignment(HorizontalAlignment.Center);

                Assert.Equal(HorizontalAlignment.Left, layoutController.GetHorizontalAlignment(first));
                Assert.Equal(HorizontalAlignment.Center, layoutController.GetHorizontalAlignment(second));
                Assert.True(buttons.Center.IsChecked);
                Assert.True(buttons.Middle.IsChecked);
            }
            finally
            {
                host.Close();
            }
        });
    }

    [Fact]
    public void TrackHeaderClick_activates_clicked_column_and_current_cell()
    {
        RunOnSta(() =>
        {
            var grid = new DataGrid
            {
                AutoGenerateColumns = false,
                Width = 500,
                Height = 240
            };
            var row = new object();
            grid.Items.Add(row);
            var first = AddTextColumn(grid, "NR");
            var second = AddTextColumn(grid, "Haltungsname");
            var layoutController = new DataGridColumnLayoutController();
            var buttons = CreateButtons();
            var toolbar = new DataGridColumnAlignmentToolbar(grid, layoutController, buttons);
            var host = new Window
            {
                Content = grid,
                Width = grid.Width,
                Height = grid.Height,
                ShowInTaskbar = false,
                WindowStyle = WindowStyle.None
            };

            try
            {
                host.Show();
                grid.UpdateLayout();
                var secondHeader = FindVisualDescendants<DataGridColumnHeader>(grid)
                    .Single(header => ReferenceEquals(header.Column, second));

                toolbar.TrackHeaderClick(secondHeader);
                toolbar.ApplyHorizontalAlignment(HorizontalAlignment.Right);

                Assert.Same(second, grid.CurrentCell.Column);
                Assert.Equal(HorizontalAlignment.Left, layoutController.GetHorizontalAlignment(first));
                Assert.Equal(HorizontalAlignment.Right, layoutController.GetHorizontalAlignment(second));
                Assert.True(buttons.Right.IsChecked);
                Assert.True(buttons.Middle.IsChecked);
            }
            finally
            {
                host.Close();
            }
        });
    }

    [Fact]
    public void UpdateButtons_clears_buttons_when_no_active_column_exists()
    {
        RunOnSta(() =>
        {
            var grid = new DataGrid();
            var layoutController = new DataGridColumnLayoutController();
            var buttons = CreateButtons();
            buttons.Left.IsChecked = true;
            buttons.Bottom.IsChecked = true;
            var toolbar = new DataGridColumnAlignmentToolbar(grid, layoutController, buttons);

            toolbar.UpdateButtons();

            Assert.False(buttons.Left.IsChecked);
            Assert.False(buttons.Center.IsChecked);
            Assert.False(buttons.Right.IsChecked);
            Assert.False(buttons.Top.IsChecked);
            Assert.False(buttons.Middle.IsChecked);
            Assert.False(buttons.Bottom.IsChecked);
        });
    }

    private static DataGridTextColumn AddTextColumn(DataGrid grid, string fieldName)
    {
        var column = new DataGridTextColumn
        {
            Header = fieldName,
            Width = DataGridLength.SizeToHeader
        };
        column.SetValue(FrameworkElement.TagProperty, fieldName);
        grid.Columns.Add(column);
        return column;
    }

    private static DataGridColumnAlignmentButtons CreateButtons()
        => new(
            new ToggleButton(),
            new ToggleButton(),
            new ToggleButton(),
            new ToggleButton(),
            new ToggleButton(),
            new ToggleButton());

    private static IEnumerable<T> FindVisualDescendants<T>(DependencyObject root)
        where T : DependencyObject
    {
        var childCount = VisualTreeHelper.GetChildrenCount(root);
        for (var index = 0; index < childCount; index++)
        {
            var child = VisualTreeHelper.GetChild(root, index);
            if (child is T match)
                yield return match;

            foreach (var nested in FindVisualDescendants<T>(child))
                yield return nested;
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
