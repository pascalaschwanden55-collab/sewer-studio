using System;
using System.Linq;
using System.Threading;
using System.Windows;
using System.Windows.Controls;
using AuswertungPro.Next.UI;
using AuswertungPro.Next.UI.Views.Pages;
using Xunit;

namespace AuswertungPro.Next.UI.Tests;

/// <summary>
/// P0 Show/Hide-Erweiterung des DataGridColumnLayoutController.
/// Prueft, dass Sichtbarkeit persistiert wird, ein All-Hidden-Zustand
/// NIE gespeichert wird und bestehende Layouts (ohne IsVisible) sichtbar bleiben.
/// </summary>
public sealed class DataGridColumnVisibilityTests
{
    [Fact]
    public void Restore_hides_column_marked_not_visible()
    {
        RunOnSta(() =>
        {
            var grid = new DataGrid();
            var a = AddTextColumn(grid, "A");
            var b = AddTextColumn(grid, "B");

            var controller = new DataGridColumnLayoutController();
            var layout = new DataPageLayoutSettings
            {
                Columns =
                [
                    new DataPageColumnLayout { FieldName = "A", DisplayIndex = 0, IsVisible = true },
                    new DataPageColumnLayout { FieldName = "B", DisplayIndex = 1, IsVisible = false }
                ]
            };

            controller.Restore(grid.Columns, layout);

            Assert.Equal(Visibility.Visible, a.Visibility);
            Assert.Equal(Visibility.Collapsed, b.Visibility);
        });
    }

    [Fact]
    public void Capture_reads_current_visibility()
    {
        RunOnSta(() =>
        {
            var grid = new DataGrid();
            AddTextColumn(grid, "A");
            var b = AddTextColumn(grid, "B");
            b.Visibility = Visibility.Collapsed;

            var layout = new DataGridColumnLayoutController().Capture(grid.Columns);

            Assert.True(layout.Columns.Single(c => c.FieldName == "A").IsVisible);
            Assert.False(layout.Columns.Single(c => c.FieldName == "B").IsVisible);
        });
    }

    [Fact]
    public void Capture_never_persists_all_hidden_state()
    {
        RunOnSta(() =>
        {
            var grid = new DataGrid();
            var a = AddTextColumn(grid, "A");
            var b = AddTextColumn(grid, "B");
            a.Visibility = Visibility.Collapsed;
            b.Visibility = Visibility.Collapsed;

            var layout = new DataGridColumnLayoutController().Capture(grid.Columns);

            Assert.All(layout.Columns, c => Assert.True(c.IsVisible));
        });
    }

    [Fact]
    public void Restore_legacy_layout_without_visibility_keeps_all_visible()
    {
        // Verhaltensneutralitaet: altes Layout ohne IsVisible -> Default true -> sichtbar.
        RunOnSta(() =>
        {
            var grid = new DataGrid();
            var a = AddTextColumn(grid, "A");

            var layout = new DataPageLayoutSettings
            {
                Columns = [new DataPageColumnLayout { FieldName = "A", DisplayIndex = 0 }]
            };

            new DataGridColumnLayoutController().Restore(grid.Columns, layout);

            Assert.Equal(Visibility.Visible, a.Visibility);
        });
    }

    private static DataGridTextColumn AddTextColumn(DataGrid grid, string fieldName)
    {
        var column = new DataGridTextColumn { Header = fieldName, Width = DataGridLength.SizeToHeader };
        column.SetValue(FrameworkElement.TagProperty, fieldName);
        grid.Columns.Add(column);
        return column;
    }

    private static void RunOnSta(Action action)
    {
        Exception? exception = null;
        var thread = new Thread(() =>
        {
            try { action(); }
            catch (Exception ex) { exception = ex; }
        });
        thread.SetApartmentState(ApartmentState.STA);
        thread.Start();
        thread.Join();
        if (exception is not null)
            throw exception;
    }
}
