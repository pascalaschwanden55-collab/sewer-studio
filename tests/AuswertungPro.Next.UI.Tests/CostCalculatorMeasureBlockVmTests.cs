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
        var line = Assert.Single(block.Lines);
        line.Qty = 4m;
        line.TransferMarked = true;
        var lineStates = new List<string>();
        var blockStates = new List<string>();
        line.LineChanged += () => lineStates.Add(
            $"{line.Qty}|{line.IsQtyOverridden}|{line.Selected}|{line.TransferMarked}");
        block.BlockChanged += () => blockStates.Add(
            $"{line.Qty}|{line.IsQtyOverridden}|{line.Selected}|{line.TransferMarked}");

        block.SetConnectionsFromImport("0");

        Assert.NotEmpty(lineStates);
        Assert.Equal("0|True|True|True", lineStates[0]);
        Assert.Contains("0|False|False|True", lineStates);
        Assert.Equal("0|False|False|False", blockStates[^1]);
        Assert.Equal(0m, line.Qty);
        Assert.False(line.Selected);
        Assert.False(line.TransferMarked);
        Assert.False(line.IsQtyOverridden);
    }

    [Fact]
    public void PositiveAnschluesse_ReaktivierenDurchNullDeaktivierteArbeit()
    {
        var block = CreateBlock("ANSCHLUSS_ROBOTER", "Anschluss fraesen");
        var line = Assert.Single(block.Lines);
        block.ConnectionsText = "0";
        var lineStates = new List<string>();
        var blockStates = new List<string>();
        line.LineChanged += () => lineStates.Add($"{line.Qty}|{line.Selected}");
        block.BlockChanged += () => blockStates.Add($"{line.Qty}|{line.Selected}|{block.Total}");

        block.ConnectionsText = "3";

        Assert.NotEmpty(lineStates);
        Assert.Equal("0|True", lineStates[0]);
        Assert.Contains("3|True", lineStates);
        Assert.Equal("3|True|300", blockStates[^1]);
        Assert.Equal(3m, line.Qty);
        Assert.True(line.Selected);
        Assert.False(line.IsQtyOverridden);
    }

    [Fact]
    public void PositiveAnschluesse_BewahrenManuellUeberschriebeneMenge()
    {
        var block = CreateBlock("ANSCHLUSS_ROBOTER", "Anschluss fraesen");
        var line = Assert.Single(block.Lines);
        line.Qty = 5m;

        block.SetConnectionsFromImport("2");

        Assert.Equal(5m, line.Qty);
        Assert.True(line.Selected);
        Assert.True(line.IsQtyOverridden);
    }

    [Fact]
    public void ReaktivierungsEreignis_KannMengenOverrideNochVorVorschlagSetzen()
    {
        var block = CreateBlock("ANSCHLUSS_ROBOTER", "Anschluss fraesen");
        var line = Assert.Single(block.Lines);
        block.ConnectionsText = "0";
        line.LineChanged += () =>
        {
            if (line.Selected && line.Qty == 0m)
                line.IsQtyOverridden = true;
        };

        block.ConnectionsText = "3";

        Assert.True(line.Selected);
        Assert.Equal(0m, line.Qty);
        Assert.True(line.IsQtyOverridden);
    }

    [Fact]
    public void Anschlussregel_ErkenntPositionAuchUeberText()
    {
        var block = CreateBlock("POSITION", "Hausanschluss reparieren");
        var line = Assert.Single(block.Lines);

        block.SetConnectionsFromImport("2");

        Assert.Equal(2m, line.Qty);
        Assert.True(line.Selected);
        Assert.False(line.IsQtyOverridden);
    }

    [Fact]
    public void Anschlussregel_VeraendertAnderePositionNicht()
    {
        var block = CreateBlock("POSITION", "Normale Position");
        var line = Assert.Single(block.Lines);
        line.TransferMarked = true;

        block.SetConnectionsFromImport("0");

        Assert.Equal(1m, line.Qty);
        Assert.True(line.Selected);
        Assert.True(line.TransferMarked);
        Assert.False(line.IsQtyOverridden);
    }

    [Theory]
    [InlineData("0", 0, false)]
    [InlineData("-2", 0, false)]
    [InlineData("2", 2, true)]
    public void NeueAnschlusszeile_BehaeltBisherigenOverrideSonderweg(
        string connections,
        int expectedQuantity,
        bool expectedSelected)
    {
        var item = new CostCatalogItem
        {
            Key = "ANSCHLUSS_NEU",
            Name = "Anschluss neu",
            Unit = "Stk",
            Type = "Fixed",
            Price = 100m,
            Active = true
        };
        var block = new MeasureBlockVm(
            template: null,
            catalog: new Dictionary<string, CostCatalogItem>(StringComparer.OrdinalIgnoreCase)
            {
                [item.Key] = item
            })
        {
            ConnectionsText = connections
        };

        var added = block.AddLineFromCatalogKey(item.Key);

        Assert.True(added);
        var line = Assert.Single(block.Lines);
        Assert.Equal(expectedQuantity, line.Qty);
        Assert.Equal(expectedSelected, line.Selected);
        Assert.False(line.TransferMarked);
        Assert.True(line.IsQtyOverridden);
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

    private static MeasureBlockVm CreateBlock(string itemKey, string name)
    {
        var catalogItem = new CostCatalogItem
        {
            Key = itemKey,
            Name = name,
            Unit = "Stk",
            Type = "Fixed",
            Price = 100m,
            Active = true
        };
        var block = new MeasureBlockVm(
            new MeasureTemplate
            {
                Id = "TEST",
                Name = "Test",
                Lines =
                [
                    new MeasureLineTemplate
                    {
                        ItemKey = itemKey,
                        Enabled = true,
                        DefaultQty = 1m
                    }
                ]
            },
            new Dictionary<string, CostCatalogItem>(StringComparer.OrdinalIgnoreCase)
            {
                [itemKey] = catalogItem
            });
        block.ApplyCatalogPrices();
        return block;
    }
}
