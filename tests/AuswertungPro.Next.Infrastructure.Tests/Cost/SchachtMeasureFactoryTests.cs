using System.Collections.Generic;
using System.Linq;
using AuswertungPro.Next.Domain.Models;
using AuswertungPro.Next.Infrastructure.Costs;
using Xunit;

namespace AuswertungPro.Next.Infrastructure.Tests.Cost;

public sealed class SchachtMeasureFactoryTests
{
    private static Dictionary<string, CostCatalogItem> Catalog() => new()
    {
        ["SCHACHT_SANIERUNG_PAUSCHAL"] = new() { Key = "SCHACHT_SANIERUNG_PAUSCHAL", Name = "Schachtsanierung pauschal", Unit = "St", Type = "Fixed", Price = 1500m, NpkCode = "700.001", Chapter = "700" },
        ["SCHACHT_REINIGUNG"] = new() { Key = "SCHACHT_REINIGUNG", Name = "Schachtreinigung", Unit = "St", Type = "Fixed", Price = 200m, NpkCode = "700.004", Chapter = "700" },
        ["QK_DOKUMENTATION"] = new() { Key = "QK_DOKUMENTATION", Name = "Dokumentation", Unit = "Stk", Type = "Fixed", Price = 100m, NpkCode = "234.101", Chapter = "200" },
        ["SCHACHT_STEIGEISEN_ERSETZEN"] = new() { Key = "SCHACHT_STEIGEISEN_ERSETZEN", Name = "Steigeisen ersetzen", Unit = "St", Type = "Fixed", Price = 90m, NpkCode = "731.101", Chapter = "700" },
    };

    private static Dictionary<string, MeasureTemplate> Templates() => new()
    {
        ["SCHACHT_PAUSCHAL"] = new()
        {
            Id = "SCHACHT_PAUSCHAL", Name = "Schachtsanierung pauschal", Disabled = false,
            Lines = new List<MeasureLineTemplate>
            {
                new() { Group = "Vorarbeiten", ItemKey = "SCHACHT_REINIGUNG", Enabled = false, DefaultQty = 1 },
                new() { Group = "Hauptarbeit", ItemKey = "SCHACHT_SANIERUNG_PAUSCHAL", Enabled = true, DefaultQty = 1 },
                new() { Group = "Qualitaetskontrolle", ItemKey = "QK_DOKUMENTATION", Enabled = false, DefaultQty = 1 },
            }
        },
        ["SCHACHT_STEIGEISEN"] = new()
        {
            Id = "SCHACHT_STEIGEISEN", Name = "Steigeisen ersetzen", Disabled = false,
            Lines = new List<MeasureLineTemplate>
            {
                new() { Group = "Hauptarbeit", ItemKey = "SCHACHT_STEIGEISEN_ERSETZEN", Enabled = true, DefaultQty = 1 },
            }
        },
    };

    [Fact]
    public void Pauschal_liefert_HoldingCost_mit_Menge_1_und_ohne_Installationszeile()
    {
        var cost = SchachtMeasureFactory.Build(
            "KS 60191", "SCHACHT_PAUSCHAL", Templates(), Catalog(), 0.081m,
            hauptarbeitMenge: 1m, hauptarbeitItemKey: "SCHACHT_SANIERUNG_PAUSCHAL");

        Assert.NotNull(cost);
        var lines = cost!.Measures.SelectMany(m => m.Lines).ToList();
        var haupt = lines.Single(l => l.ItemKey == "SCHACHT_SANIERUNG_PAUSCHAL");
        Assert.True(haupt.Selected);
        Assert.Equal(1m, haupt.Qty);
        Assert.Equal(1500m, cost.Total);
        // GFK/NADELFILZ-Regel darf KEINE Installationszeile injizieren (Schacht-Namen ohne diese Woerter).
        Assert.DoesNotContain(lines, l => (l.ItemKey ?? "").StartsWith("INSTALL", System.StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void Zusatz_Haekchen_aktiviert_deaktivierte_Zeile()
    {
        var cost = SchachtMeasureFactory.Build(
            "KS 1", "SCHACHT_PAUSCHAL", Templates(), Catalog(), 0.081m,
            extraOptionKeys: new[] { "SCHACHT_REINIGUNG" },
            hauptarbeitMenge: 1m, hauptarbeitItemKey: "SCHACHT_SANIERUNG_PAUSCHAL");

        var reinigung = cost!.Measures.SelectMany(m => m.Lines).Single(l => l.ItemKey == "SCHACHT_REINIGUNG");
        Assert.True(reinigung.Selected);
        Assert.Equal(1700m, cost.Total); // 1500 + 200
    }

    [Fact]
    public void Manuelle_Menge_setzt_Hauptarbeit_Stueckzahl()
    {
        var cost = SchachtMeasureFactory.Build(
            "KS 2", "SCHACHT_STEIGEISEN", Templates(), Catalog(), 0.081m,
            hauptarbeitMenge: 4m, hauptarbeitItemKey: "SCHACHT_STEIGEISEN_ERSETZEN");

        var haupt = cost!.Measures.SelectMany(m => m.Lines).Single(l => l.ItemKey == "SCHACHT_STEIGEISEN_ERSETZEN");
        Assert.Equal(4m, haupt.Qty);
        Assert.Equal(360m, cost.Total); // 4 * 90
    }
}
