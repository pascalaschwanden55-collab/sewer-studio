using System;
using System.Collections.Generic;
using System.Linq;
using AuswertungPro.Next.Domain.Models;
using AuswertungPro.Next.UI.ViewModels.Windows;

namespace AuswertungPro.Next.UI.Tests;

public sealed class HoldingMeasureFactoryTests
{
    private static HaltungRecord Record(string holding, string dn, string laenge, string schaeden = "")
    {
        var r = new HaltungRecord();
        r.SetFieldValue("Haltungsname", holding, FieldSource.Manual, userEdited: true);
        r.SetFieldValue("DN_mm", dn, FieldSource.Manual, userEdited: true);
        r.SetFieldValue("Haltungslaenge_m", laenge, FieldSource.Manual, userEdited: true);
        if (!string.IsNullOrEmpty(schaeden))
            r.SetFieldValue("Primaere_Schaeden", schaeden, FieldSource.Manual, userEdited: true);
        return r;
    }

    private static (Dictionary<string, MeasureTemplate> templates, Dictionary<string, CostCatalogItem> catalog) Setup()
    {
        var tpl = new MeasureTemplate
        {
            Id = "SCHLAUCHLINER_NADELFILZ",
            Name = "Nadelfilz",
            Lines = new List<MeasureLineTemplate>
            {
                new() { Group = "Vorarbeiten", ItemKey = "VORARBEIT_REINIGUNG", Enabled = true, DefaultQty = 1 },
                new() { Group = "Vorarbeiten", ItemKey = "VORARBEIT_FRAESEN", Enabled = false, DefaultQty = 1 },
                new() { Group = "Vorarbeiten", ItemKey = "VORARBEIT_VD", Enabled = false, DefaultQty = 1 },
                new() { Group = "Hauptarbeit", ItemKey = "SCHLAUCHLINER_NADELFILZ", Enabled = true, DefaultQty = 1 },
                new() { Group = "Hauptarbeit", ItemKey = "LINERENDMANSCHETTE_LEM", Enabled = true, DefaultQty = 2 },
                new() { Group = "Hauptarbeit", ItemKey = "ANSCHLUSS_AUFFRAESEN", Enabled = true, DefaultQty = 1 },
            }
        };
        var templates = new Dictionary<string, MeasureTemplate>(StringComparer.OrdinalIgnoreCase) { [tpl.Id] = tpl };

        var catalog = new Dictionary<string, CostCatalogItem>(StringComparer.OrdinalIgnoreCase)
        {
            ["VORARBEIT_REINIGUNG"] = new() { Key = "VORARBEIT_REINIGUNG", Unit = "m", Type = "Fixed", Price = 5m },
            ["SCHLAUCHLINER_NADELFILZ"] = new()
            {
                Key = "SCHLAUCHLINER_NADELFILZ", Unit = "m", Type = "ByDN",
                DnPrices = new List<DnPrice>
                {
                    new() { DnFrom = 150, DnTo = 150, Price = 250m },
                    new() { DnFrom = 250, DnTo = 250, Price = 300m }
                }
            },
            ["LINERENDMANSCHETTE_LEM"] = new()
            {
                Key = "LINERENDMANSCHETTE_LEM", Unit = "Stk", Type = "ByDN",
                DnPrices = new List<DnPrice> { new() { DnFrom = 250, DnTo = 250, Price = 490m } }
            },
            ["ANSCHLUSS_AUFFRAESEN"] = new() { Key = "ANSCHLUSS_AUFFRAESEN", Unit = "Stk", Type = "Fixed", Price = 100m },
            ["VORARBEIT_FRAESEN"] = new() { Key = "VORARBEIT_FRAESEN", Unit = "m", Type = "Fixed", Price = 29m },
            ["VORARBEIT_VD"] = new() { Key = "VORARBEIT_VD", Unit = "pro Tag", Type = "Fixed", Price = 1000m },
        };
        return (templates, catalog);
    }

    [Fact]
    public void Build_FillsMeterLinesWithLength_EndManschetteAndConnections()
    {
        var (templates, catalog) = Setup();
        var record = Record("H1", "250", "45.00", "Anschluss gerissen 12.50m");

        var cost = HoldingMeasureFactory.Build("H1", record, "SCHLAUCHLINER_NADELFILZ", templates, catalog, 0.081m);

        Assert.NotNull(cost);
        var lines = cost!.Measures[0].Lines;

        var reinigung = lines.First(l => l.ItemKey == "VORARBEIT_REINIGUNG");
        Assert.Equal(45m, reinigung.Qty);   // m-Zeile = Haltungslänge

        var liner = lines.First(l => l.ItemKey == "SCHLAUCHLINER_NADELFILZ");
        Assert.Equal(45m, liner.Qty);
        Assert.Equal(300m, liner.UnitPrice); // Preis DN 250

        var lem = lines.First(l => l.ItemKey == "LINERENDMANSCHETTE_LEM");
        Assert.True(lem.Selected);
        Assert.Equal(2m, lem.Qty);           // DN 250 >= 200 -> 2 Stk

        var auffraesen = lines.First(l => l.ItemKey == "ANSCHLUSS_AUFFRAESEN");
        Assert.Equal(1m, auffraesen.Qty);    // 1 Anschluss aus Dedup
        Assert.True(cost.Total > 0m);
    }

    [Fact]
    public void Build_Dn150_DisablesEndManschette()
    {
        var (templates, catalog) = Setup();
        var record = Record("H2", "150", "30.00");

        var cost = HoldingMeasureFactory.Build("H2", record, "SCHLAUCHLINER_NADELFILZ", templates, catalog, 0.081m);

        Assert.NotNull(cost);
        var lem = cost!.Measures[0].Lines.First(l => l.ItemKey == "LINERENDMANSCHETTE_LEM");
        Assert.False(lem.Selected);          // DN 150 < 200 -> keine Endmanschette
    }

    [Fact]
    public void Build_NoConnections_DisablesConnectionLines()
    {
        var (templates, catalog) = Setup();
        var record = Record("H3", "250", "40.00"); // keine Schäden -> 0 Anschlüsse

        var cost = HoldingMeasureFactory.Build("H3", record, "SCHLAUCHLINER_NADELFILZ", templates, catalog, 0.081m);

        Assert.NotNull(cost);
        var auffraesen = cost!.Measures[0].Lines.First(l => l.ItemKey == "ANSCHLUSS_AUFFRAESEN");
        Assert.False(auffraesen.Selected);   // 0 Anschlüsse -> Zeile deaktiviert
    }

    [Fact]
    public void Build_UnknownMeasure_ReturnsNull()
    {
        var (templates, catalog) = Setup();
        var record = Record("H4", "250", "40.00");

        var cost = HoldingMeasureFactory.Build("H4", record, "GIBT_ES_NICHT", templates, catalog, 0.081m);

        Assert.Null(cost);
    }

    [Fact]
    public void Build_ExtraOption_ActivatesLine_WithLengthForMeterUnit()
    {
        var (templates, catalog) = Setup();
        var record = Record("H5", "250", "45.00");

        var cost = HoldingMeasureFactory.Build("H5", record, "SCHLAUCHLINER_NADELFILZ", templates, catalog, 0.081m,
            extraOptionKeys: new[] { "VORARBEIT_FRAESEN", "VORARBEIT_VD" });

        Assert.NotNull(cost);
        var lines = cost!.Measures[0].Lines;

        var fraesen = lines.First(l => l.ItemKey == "VORARBEIT_FRAESEN");
        Assert.True(fraesen.Selected);
        Assert.Equal(45m, fraesen.Qty);      // m-Option = Haltungslänge

        var vd = lines.First(l => l.ItemKey == "VORARBEIT_VD");
        Assert.True(vd.Selected);            // Verkehrsdienst aktiviert
    }

    [Fact]
    public void Build_HauptarbeitMenge_OverridesMainLineQty()
    {
        var (templates, catalog) = Setup();
        var record = Record("H6", "250", "45.00");

        var cost = HoldingMeasureFactory.Build("H6", record, "SCHLAUCHLINER_NADELFILZ", templates, catalog, 0.081m,
            hauptarbeitMenge: 5m);

        Assert.NotNull(cost);
        var liner = cost!.Measures[0].Lines.First(l => l.ItemKey == "SCHLAUCHLINER_NADELFILZ");
        Assert.Equal(5m, liner.Qty);         // manuelle Menge übersteuert Auto-Länge
    }
}
