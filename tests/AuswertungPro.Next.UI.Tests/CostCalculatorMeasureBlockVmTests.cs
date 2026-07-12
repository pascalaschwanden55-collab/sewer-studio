using AuswertungPro.Next.Domain.Models;
using AuswertungPro.Next.UI.ViewModels.Windows;

namespace AuswertungPro.Next.UI.Tests;

public sealed class CostCalculatorMeasureBlockVmTests
{
    [Fact]
    public void LoadFromUndToModel_BewahrenKostenfelderUndSummierenNurAktiveZeilen()
    {
        var block = new MeasureBlockVm(
            new MeasureTemplate { Id = "LINER", Name = "Schlauchliner" },
            new Dictionary<string, CostCatalogItem>());
        block.LoadFrom(new MeasureCost
        {
            MeasureId = "LINER",
            MeasureName = "Schlauchliner",
            Dn = 300,
            LengthMeters = 12.5m,
            Lines =
            [
                new CostLine
                {
                    Group = "A",
                    ItemKey = "AKTIV",
                    Text = "Aktiv",
                    Unit = "m",
                    Qty = 2m,
                    UnitPrice = 10m,
                    Selected = true,
                    TransferMarked = true,
                    IsPriceOverridden = true,
                    IsQtyOverridden = true,
                    PriceHint = "Manuell"
                },
                new CostLine
                {
                    Group = "B",
                    ItemKey = "INAKTIV",
                    Text = "Inaktiv",
                    Unit = "Stk",
                    Qty = 5m,
                    UnitPrice = 99m,
                    Selected = false
                }
            ]
        });

        var model = block.ToModel();

        Assert.Equal("LINER", model.MeasureId);
        Assert.Equal("Schlauchliner", model.MeasureName);
        Assert.Equal(300, model.Dn);
        Assert.Equal(12.5m, model.LengthMeters);
        Assert.Equal(20m, model.Total);
        Assert.Equal(2, model.Lines.Count);
        var active = model.Lines[0];
        Assert.True(active.TransferMarked);
        Assert.True(active.IsPriceOverridden);
        Assert.True(active.IsQtyOverridden);
        Assert.Equal("Manuell", active.PriceHint);
    }

    [Fact]
    public void LoadFrom_VerbindetZeilenaenderungenWiederMitBlocksumme()
    {
        var block = new MeasureBlockVm(null, new Dictionary<string, CostCatalogItem>());
        block.LoadFrom(new MeasureCost
        {
            Lines =
            [
                new CostLine
                {
                    ItemKey = "POSITION",
                    Text = "Position",
                    Qty = 2m,
                    UnitPrice = 10m,
                    Selected = true
                }
            ]
        });
        var changeCount = 0;
        block.BlockChanged += () => changeCount++;

        block.Lines[0].Qty = 3m;

        Assert.Equal(30m, block.Total);
        Assert.True(changeCount > 0);
    }

    [Fact]
    public void NullAnschluesse_DeaktivierenAnschlussarbeitOhneManuellenOverride()
    {
        var catalogItem = new CostCatalogItem
        {
            Key = "ANSCHLUSS_ROBOTER",
            Name = "Anschluss fraesen",
            Unit = "Stk",
            Type = "Fixed",
            Price = 100m,
            Active = true
        };
        var block = new MeasureBlockVm(
            new MeasureTemplate
            {
                Id = "ANSCHLUSS",
                Name = "Anschluss",
                Lines =
                [
                    new MeasureLineTemplate
                    {
                        ItemKey = catalogItem.Key,
                        Enabled = true,
                        DefaultQty = 1m
                    }
                ]
            },
            new Dictionary<string, CostCatalogItem>(StringComparer.OrdinalIgnoreCase)
            {
                [catalogItem.Key] = catalogItem
            });

        block.SetConnectionsFromImport("0");

        var line = Assert.Single(block.Lines);
        Assert.Equal(0m, line.Qty);
        Assert.False(line.Selected);
        Assert.False(line.TransferMarked);
        Assert.False(line.IsQtyOverridden);
    }

    [Fact]
    public void SortLines_StelltVorlagenreihenfolgeWiederHer()
    {
        var template = new MeasureTemplate
        {
            Id = "SORT",
            Name = "Sortierung",
            Lines =
            [
                new MeasureLineTemplate { ItemKey = "A", Group = "G", Enabled = true },
                new MeasureLineTemplate { ItemKey = "B", Group = "G", Enabled = true }
            ]
        };
        var block = new MeasureBlockVm(template, new Dictionary<string, CostCatalogItem>());
        block.Lines.Move(0, 1);

        block.SortLines();

        Assert.Equal(["A", "B"], block.Lines.Select(line => line.ItemKey));
    }
}
