using AuswertungPro.Next.Domain.Models;
using AuswertungPro.Next.UI.ViewModels.Windows;

namespace AuswertungPro.Next.UI.Tests;

public sealed class CostCalculatorMeasureInputStateControllerTests
{
    [Fact]
    public void ApplyDnText_unterdrueckt_nur_dn_change_waehrend_des_updates()
    {
        var controller = new CostCalculatorMeasureInputStateController();
        var text = "";
        var dnTriggered = false;
        var lengthTriggered = false;
        var connectionsTriggered = false;

        controller.ApplyDnText(
            "300",
            value =>
            {
                text = value;
                dnTriggered = controller.ShouldHandleDnTextChange();
                lengthTriggered = controller.ShouldHandleLengthTextChange();
                connectionsTriggered = controller.ShouldHandleConnectionsTextChange();
            });

        Assert.Equal("300", text);
        Assert.False(dnTriggered);
        Assert.True(lengthTriggered);
        Assert.True(connectionsTriggered);
        Assert.True(controller.ShouldHandleDnTextChange());
    }

    [Fact]
    public void ApplyLengthText_unterdrueckt_nur_length_change_waehrend_des_updates()
    {
        var controller = new CostCalculatorMeasureInputStateController();
        var lengthTriggered = false;
        var dnTriggered = false;

        controller.ApplyLengthText(
            "12.50",
            _ =>
            {
                lengthTriggered = controller.ShouldHandleLengthTextChange();
                dnTriggered = controller.ShouldHandleDnTextChange();
            });

        Assert.False(lengthTriggered);
        Assert.True(dnTriggered);
        Assert.True(controller.ShouldHandleLengthTextChange());
    }

    [Fact]
    public void ApplyConnectionsText_unterdrueckt_nur_connections_change_waehrend_des_updates()
    {
        var controller = new CostCalculatorMeasureInputStateController();
        var connectionsTriggered = false;
        var dnTriggered = false;

        controller.ApplyConnectionsText(
            "2",
            _ =>
            {
                connectionsTriggered = controller.ShouldHandleConnectionsTextChange();
                dnTriggered = controller.ShouldHandleDnTextChange();
            });

        Assert.False(connectionsTriggered);
        Assert.True(dnTriggered);
        Assert.True(controller.ShouldHandleConnectionsTextChange());
    }

    [Fact]
    public void Suppression_wird_auch_bei_exception_zurueckgesetzt()
    {
        var controller = new CostCalculatorMeasureInputStateController();

        Assert.Throws<InvalidOperationException>(() =>
            controller.ApplyLengthText("1", _ => throw new InvalidOperationException()));

        Assert.True(controller.ShouldHandleLengthTextChange());
    }

    [Fact]
    public void MeasureBlockVm_SetDnFromImport_wendet_katalogpreis_ohne_manuellen_override_an()
    {
        var block = new MeasureBlockVm(
            Template("DN_PRICE"),
            Catalog(ByDnItem("DN_PRICE", "DN-Preis", "m", price: 99m)));
        var line = Assert.Single(block.Lines);

        block.SetDnFromImport("300");

        Assert.Equal("300", block.DnText);
        Assert.Equal(99m, line.UnitPrice);
        Assert.False(line.IsPriceOverridden);
    }

    [Fact]
    public void MeasureBlockVm_SetLengthFromImport_wendet_meter_menge_ohne_manuellen_override_an()
    {
        var block = new MeasureBlockVm(
            Template("LINER"),
            Catalog(FixedItem("LINER", "Liner", "m")));
        var line = Assert.Single(block.Lines);

        block.SetLengthFromImport("12.50");

        Assert.Equal("12.50", block.LengthText);
        Assert.Equal(12.50m, line.Qty);
        Assert.False(line.IsQtyOverridden);
    }

    [Fact]
    public void MeasureBlockVm_SetConnectionsFromImport_wendet_anschluss_menge_ohne_manuellen_override_an()
    {
        var block = new MeasureBlockVm(
            Template("ANSCHLUSS_ROBOTER"),
            Catalog(FixedItem("ANSCHLUSS_ROBOTER", "Anschluss fraesen", "Stk")));
        var line = Assert.Single(block.Lines);

        block.SetConnectionsFromImport("2");

        Assert.Equal("2", block.ConnectionsText);
        Assert.Equal(2m, line.Qty);
        Assert.False(line.IsQtyOverridden);
        Assert.True(line.Selected);
    }

    private static MeasureTemplate Template(string itemKey)
        => new()
        {
            Id = "template",
            Name = "Template",
            Lines =
            [
                new MeasureLineTemplate
                {
                    ItemKey = itemKey,
                    Enabled = true,
                    DefaultQty = 1m
                }
            ]
        };

    private static Dictionary<string, CostCatalogItem> Catalog(params CostCatalogItem[] items)
        => items.ToDictionary(item => item.Key, StringComparer.OrdinalIgnoreCase);

    private static CostCatalogItem FixedItem(string key, string name, string unit)
        => new()
        {
            Key = key,
            Name = name,
            Unit = unit,
            Type = "Fixed"
        };

    private static CostCatalogItem ByDnItem(string key, string name, string unit, decimal price)
        => new()
        {
            Key = key,
            Name = name,
            Unit = unit,
            Type = "ByDN",
            DnPrices =
            [
                new DnPrice
                {
                    DnFrom = 200,
                    DnTo = 400,
                    Price = price
                }
            ]
        };
}
