using System.Threading;
using System.Windows;
using System.Windows.Controls;
using AuswertungPro.Next.UI;
using AuswertungPro.Next.UI.Views.Pages;
using Xunit;

namespace AuswertungPro.Next.UI.Tests;

public sealed class DataGridColumnLayoutControllerTests
{
    [Fact]
    public void Restore_applies_saved_width_alignment_and_order_by_field_tag()
    {
        RunOnSta(() =>
        {
            var grid = new DataGrid();
            var first = AddTextColumn(grid, "A");
            var second = AddTextColumn(grid, "B");
            var unsaved = AddTextColumn(grid, "C");

            var controller = new DataGridColumnLayoutController();
            var layout = new DataPageLayoutSettings
            {
                Columns =
                [
                    new DataPageColumnLayout
                    {
                        FieldName = "B",
                        DisplayIndex = 0,
                        WidthValue = 144,
                        WidthUnitType = nameof(DataGridLengthUnitType.Pixel),
                        HorizontalAlignment = nameof(HorizontalAlignment.Right),
                        VerticalAlignment = nameof(VerticalAlignment.Bottom)
                    },
                    new DataPageColumnLayout
                    {
                        FieldName = "A",
                        DisplayIndex = 1,
                        WidthValue = 2,
                        WidthUnitType = nameof(DataGridLengthUnitType.Star),
                        HorizontalAlignment = nameof(HorizontalAlignment.Center),
                        VerticalAlignment = nameof(VerticalAlignment.Top)
                    }
                ]
            };

            controller.Restore(grid.Columns, layout);

            Assert.Equal(1, first.DisplayIndex);
            Assert.Equal(0, second.DisplayIndex);
            Assert.Equal(2, unsaved.DisplayIndex);
            Assert.Equal(2, first.Width.Value);
            Assert.Equal(DataGridLengthUnitType.Star, first.Width.UnitType);
            Assert.Equal(144, second.Width.Value);
            Assert.Equal(DataGridLengthUnitType.Pixel, second.Width.UnitType);
            Assert.Equal(HorizontalAlignment.Center, controller.GetHorizontalAlignment(first));
            Assert.Equal(VerticalAlignment.Top, controller.GetVerticalAlignment(first));
            Assert.Equal(HorizontalAlignment.Right, controller.GetHorizontalAlignment(second));
            Assert.Equal(VerticalAlignment.Bottom, controller.GetVerticalAlignment(second));
        });
    }

    [Fact]
    public void Capture_serializes_tagged_columns_with_current_width_order_and_alignment()
    {
        RunOnSta(() =>
        {
            var grid = new DataGrid();
            var cost = AddTextColumn(grid, "Kosten");
            var nr = AddTextColumn(grid, "NR");
            var untagged = new DataGridTextColumn
            {
                Header = "Internal",
                Width = new DataGridLength(50, DataGridLengthUnitType.Pixel)
            };
            grid.Columns.Add(untagged);

            cost.Width = new DataGridLength(3, DataGridLengthUnitType.Star);
            nr.Width = new DataGridLength(90, DataGridLengthUnitType.Pixel);

            var controller = new DataGridColumnLayoutController();
            controller.SetAlignment(cost, HorizontalAlignment.Right, VerticalAlignment.Center);
            controller.SetAlignment(nr, HorizontalAlignment.Left, VerticalAlignment.Top);

            var layout = controller.Capture(grid.Columns);

            Assert.Equal(2, layout.Columns.Count);
            var savedCost = Assert.Single(layout.Columns, x => x.FieldName == "Kosten");
            Assert.Equal(cost.DisplayIndex, savedCost.DisplayIndex);
            Assert.Equal(3, savedCost.WidthValue);
            Assert.Equal(nameof(DataGridLengthUnitType.Star), savedCost.WidthUnitType);
            Assert.Equal(nameof(HorizontalAlignment.Right), savedCost.HorizontalAlignment);
            Assert.Equal(nameof(VerticalAlignment.Center), savedCost.VerticalAlignment);

            var savedNr = Assert.Single(layout.Columns, x => x.FieldName == "NR");
            Assert.Equal(nr.DisplayIndex, savedNr.DisplayIndex);
            Assert.Equal(90, savedNr.WidthValue);
            Assert.Equal(nameof(DataGridLengthUnitType.Pixel), savedNr.WidthUnitType);
            Assert.Equal(nameof(HorizontalAlignment.Left), savedNr.HorizontalAlignment);
            Assert.Equal(nameof(VerticalAlignment.Top), savedNr.VerticalAlignment);
        });
    }

    [Fact]
    public void SetAlignment_updates_cell_and_text_column_styles()
    {
        RunOnSta(() =>
        {
            var column = AddTextColumn(new DataGrid(), "Bemerkung");

            var controller = new DataGridColumnLayoutController();
            controller.SetAlignment(column, HorizontalAlignment.Right, VerticalAlignment.Bottom);

            Assert.Equal(HorizontalAlignment.Right, controller.GetHorizontalAlignment(column));
            Assert.Equal(VerticalAlignment.Bottom, controller.GetVerticalAlignment(column));
            Assert.Contains(
                column.CellStyle.Setters.OfType<Setter>(),
                x => x.Property == Control.HorizontalContentAlignmentProperty
                     && Equals(x.Value, HorizontalAlignment.Right));
            Assert.Contains(
                column.CellStyle.Setters.OfType<Setter>(),
                x => x.Property == Control.VerticalContentAlignmentProperty
                     && Equals(x.Value, VerticalAlignment.Bottom));
            Assert.Contains(
                column.ElementStyle.Setters.OfType<Setter>(),
                x => x.Property == TextBlock.TextAlignmentProperty
                     && Equals(x.Value, TextAlignment.Right));
            Assert.Contains(
                column.EditingElementStyle.Setters.OfType<Setter>(),
                x => x.Property == TextBox.TextAlignmentProperty
                     && Equals(x.Value, TextAlignment.Right));
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
