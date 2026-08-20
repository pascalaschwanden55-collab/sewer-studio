using AuswertungPro.Next.Application.Costs;
using AuswertungPro.Next.Domain.Models;
using Xunit;

namespace AuswertungPro.Next.Infrastructure.Tests.Cost;

/// <summary>
/// Schacht-Kosten haben ZWEI gepflegte Quellen: die Schacht-Matrix
/// (schacht_costs.json) und den Massnahmen-Dialog (schacht_empfehlungen.json).
/// Das Druckcenter fuehrt beide zusammen, das Projekt-Cockpit las bis zum
/// 2026-08-20 nur die Matrix — bei einem Projekt, das nur den Dialog nutzt,
/// stand deshalb ueberall 0 CHF.
/// </summary>
public sealed class SchachtCostStoreMergerTests
{
    private static HoldingCost Cost(string nummer, decimal total) => new()
    {
        Holding = nummer,
        Total = total,
        Measures =
        [
            new MeasureCost
            {
                MeasureId = "M", MeasureName = "Massnahme", Total = total,
                Lines = [new CostLine { Text = "Massnahme", Qty = 1m, UnitPrice = total, Selected = true }]
            }
        ]
    };

    [Fact]
    public void Nimmt_Schaechte_aus_beiden_Quellen_auf()
    {
        var matrix = new ProjectCostStore { ByHolding = { ["S1"] = Cost("S1", 1200m) } };
        var empfehlungen = new ProjectCostStore { ByHolding = { ["S2"] = Cost("S2", 450m) } };

        var zusammen = SchachtCostStoreMerger.Merge(matrix, empfehlungen);

        Assert.Equal(2, zusammen.ByHolding.Count);
        Assert.Equal(1200m, zusammen.ByHolding["S1"].Total);
        Assert.Equal(450m, zusammen.ByHolding["S2"].Total);
    }

    [Fact]
    public void Die_Matrix_hat_Vorrang_und_wird_nicht_verdoppelt()
    {
        var matrix = new ProjectCostStore { ByHolding = { ["S1"] = Cost("S1", 1200m) } };
        var empfehlungen = new ProjectCostStore { ByHolding = { ["S1"] = Cost("S1", 450m) } };

        var zusammen = SchachtCostStoreMerger.Merge(matrix, empfehlungen);

        var eintrag = Assert.Single(zusammen.ByHolding);
        Assert.Equal(1200m, eintrag.Value.Total);
    }

    [Fact]
    public void Vergleicht_Schachtnummern_ohne_Gross_Kleinschreibung()
    {
        var matrix = new ProjectCostStore { ByHolding = { ["s1"] = Cost("s1", 1200m) } };
        var empfehlungen = new ProjectCostStore { ByHolding = { ["S1"] = Cost("S1", 450m) } };

        var zusammen = SchachtCostStoreMerger.Merge(matrix, empfehlungen);

        Assert.Single(zusammen.ByHolding);
    }

    [Fact]
    public void Fehlende_Quellen_sind_erlaubt()
    {
        var nurEmpfehlung = SchachtCostStoreMerger.Merge(
            null,
            new ProjectCostStore { ByHolding = { ["S2"] = Cost("S2", 450m) } });

        Assert.Equal(450m, Assert.Single(nurEmpfehlung.ByHolding).Value.Total);
        Assert.Empty(SchachtCostStoreMerger.Merge(null, null).ByHolding);
    }

    [Fact]
    public void Laesst_die_Quellspeicher_unveraendert()
    {
        var matrix = new ProjectCostStore { ByHolding = { ["S1"] = Cost("S1", 1200m) } };
        var empfehlungen = new ProjectCostStore { ByHolding = { ["S2"] = Cost("S2", 450m) } };

        SchachtCostStoreMerger.Merge(matrix, empfehlungen);

        Assert.Single(matrix.ByHolding);
        Assert.Single(empfehlungen.ByHolding);
    }
}
