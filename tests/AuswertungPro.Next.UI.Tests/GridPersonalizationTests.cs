using System;
using System.Linq;
using System.Threading;
using System.Windows;
using System.Windows.Controls;
using AuswertungPro.Next.UI;
using AuswertungPro.Next.UI.Behaviors;
using AuswertungPro.Next.UI.Controls;
using AuswertungPro.Next.UI.Services;
using AuswertungPro.Next.UI.Views.Pages;
using Xunit;

namespace AuswertungPro.Next.UI.Tests;

/// <summary>
/// P1 Spalten-Personalisierung: Restore/Capture ueber den ViewCustomizationStore
/// (Breite, Reihenfolge, Sichtbarkeit) und der Min-1-sichtbar-Guard des ColumnChooser.
/// </summary>
public sealed class GridPersonalizationTests
{
    [Fact]
    public void CaptureAndSave_persists_visibility_and_width_into_store_slot()
    {
        RunOnSta(() =>
        {
            var settings = new AppSettings();
            ViewCustomizationStore.Configure(settings);

            var grid = new DataGrid();
            var holding = AddColumn(grid, "Holding");
            var netto = AddColumn(grid, "NetCost");

            var controller = new DataGridColumnLayoutController();
            GridPersonalizationCore.Restore(grid, "BuilderPage", "Grid", controller); // erster Lauf: leer

            netto.Visibility = Visibility.Collapsed;
            holding.Width = new DataGridLength(210, DataGridLengthUnitType.Pixel);

            GridPersonalizationCore.CaptureAndSave(grid, "BuilderPage", "Grid", controller);

            var slot = settings.ViewCustomizations["BuilderPage"].Grids["Grid"];
            var savedNetto = slot.Columns.Single(c => c.FieldName == "NetCost");
            var savedHolding = slot.Columns.Single(c => c.FieldName == "Holding");
            Assert.False(savedNetto.IsVisible);
            Assert.True(savedHolding.IsVisible);
            Assert.Equal(210, savedHolding.WidthValue);
        });
    }

    [Fact]
    public void Restore_reapplies_persisted_visibility_and_width_on_a_fresh_grid()
    {
        RunOnSta(() =>
        {
            var settings = new AppSettings();
            ViewCustomizationStore.Configure(settings);

            // Erst auf Grid A speichern ...
            var gridA = new DataGrid();
            AddColumn(gridA, "Holding");
            var nettoA = AddColumn(gridA, "NetCost");
            nettoA.Visibility = Visibility.Collapsed;
            GridPersonalizationCore.CaptureAndSave(gridA, "BuilderPage", "Grid", new DataGridColumnLayoutController());

            // ... dann auf einem frischen Grid B wiederherstellen.
            var gridB = new DataGrid();
            AddColumn(gridB, "Holding");
            var nettoB = AddColumn(gridB, "NetCost");
            GridPersonalizationCore.Restore(gridB, "BuilderPage", "Grid", new DataGridColumnLayoutController());

            Assert.Equal(Visibility.Collapsed, nettoB.Visibility);
        });
    }

    [Fact]
    public void ColumnChooser_CanHide_blocks_hiding_the_last_visible_column()
    {
        RunOnSta(() =>
        {
            var grid = new DataGrid();
            var a = AddColumn(grid, "A");
            var b = AddColumn(grid, "B");

            Assert.True(ColumnChooser.CanHide(grid, a)); // zwei sichtbar -> darf verstecken

            b.Visibility = Visibility.Collapsed;
            Assert.False(ColumnChooser.CanHide(grid, a)); // a ist die letzte sichtbare -> gesperrt
            Assert.True(ColumnChooser.CanHide(grid, b));  // b bereits versteckt -> einblenden erlaubt
        });
    }

    private static DataGridTextColumn AddColumn(DataGrid grid, string fieldName)
    {
        var column = new DataGridTextColumn { Header = fieldName, Width = new DataGridLength(120, DataGridLengthUnitType.Pixel) };
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
