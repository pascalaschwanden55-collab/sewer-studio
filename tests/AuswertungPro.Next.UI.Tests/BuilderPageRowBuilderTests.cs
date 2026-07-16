using AuswertungPro.Next.Domain.Models;
using AuswertungPro.Next.UI.ViewModels.Pages;

namespace AuswertungPro.Next.UI.Tests;

public sealed class BuilderPageRowBuilderTests
{
    [Fact]
    public void Build_liest_Felder_Metadaten_und_bereinigt_Massnahmenvorschau()
    {
        var record = Record("H-1");
        record.Fields[FieldKeys.Street] = " Gotthardstrasse ";
        record.Fields[FieldKeys.RehabilitationExecutor] = "";
        record.Fields[FieldKeys.PipeMaterial] = "";
        record.Fields[FieldKeys.InspectionYear] = "2026-07-16";
        record.Fields[FieldKeys.RecommendedRehabilitationMeasures] =
            "- Liner; liner | * Manschette, Kurzliner";
        record.Fields[FieldKeys.Cost] = "125.50";

        var rows = BuilderPageRowBuilder.Build(
            [record],
            new Dictionary<string, string> { [FieldKeys.Owner] = " AWU " },
            new ProjectCostStore());

        var row = Assert.Single(rows);
        Assert.Equal("H-1", row.Holding);
        Assert.Equal("Gotthardstrasse", row.Street);
        Assert.Equal("AWU", row.Owner);
        Assert.Equal("(unbekannt)", row.ExecutedBy);
        Assert.Equal("(unbekannt)", row.Material);
        Assert.Equal("2026", row.Year);
        Assert.Equal("Liner; Manschette (+1 weitere)", row.MeasuresPreview);
        Assert.Equal(125.50m, row.NetCost);
        Assert.Equal("Tabellenwert", row.CostSource);
        Assert.True(row.HasMeasures);
    }

    [Fact]
    public void Build_findet_Positionskosten_ohne_Beachtung_der_Grossschreibung()
    {
        var record = Record("h-1");
        var storedCost = new HoldingCost
        {
            Holding = "H-1",
            Measures =
            [
                new MeasureCost
                {
                    Lines =
                    [
                        new CostLine
                        {
                            Selected = true,
                            Qty = 2m,
                            UnitPrice = 30m
                        }
                    ]
                }
            ]
        };
        var store = new ProjectCostStore
        {
            ByHolding = new Dictionary<string, HoldingCost> { ["H-1"] = storedCost }
        };

        var row = Assert.Single(BuilderPageRowBuilder.Build(
            [record],
            new Dictionary<string, string>(),
            store));

        Assert.Same(storedCost, row.StoredCost);
        Assert.True(row.HasDetailedCost);
        Assert.Equal(60m, row.NetCost);
        Assert.Equal("Positionsdetails", row.CostSource);
        Assert.True(row.HasMeasures);
    }

    [Fact]
    public void Build_behaelt_Tabellenkosten_wenn_der_Kostenstore_leer_ist()
    {
        var record = Record("H-1");
        record.Fields[FieldKeys.Cost] = "99.90";
        var store = new ProjectCostStore
        {
            ByHolding = new Dictionary<string, HoldingCost>
            {
                ["H-1"] = new HoldingCost { Holding = "H-1" }
            }
        };

        var row = Assert.Single(BuilderPageRowBuilder.Build(
            [record],
            new Dictionary<string, string>(),
            store));

        Assert.False(row.HasDetailedCost);
        Assert.Equal(99.90m, row.NetCost);
        Assert.Equal("Kostenstore", row.CostSource);
    }

    [Fact]
    public void Filterzusammenfassung_verwendet_den_eigenen_Baustein()
    {
        var criteria = new BuilderPageFilterCriteria(
            Owner: "AWU",
            ExecutedBy: BuilderPageRowFilter.AllFilterLabel,
            Sanieren: "Ja",
            Material: BuilderPageRowFilter.AllFilterLabel,
            Status: BuilderPageRowFilter.AllFilterLabel,
            Year: "2026",
            Search: "  liner  ",
            OnlyWithCost: true,
            OnlyWithMeasures: false);

        var text = BuilderPageFilterSummaryBuilder.Build(criteria, 3, 10);

        Assert.Equal(
            "Eigentuemer=AWU | Sanieren=Ja | Jahr=2026 | nur mit Kosten | Suche='liner' | Treffer=3/10",
            text);
    }

    private static HaltungRecord Record(string holding)
    {
        var record = new HaltungRecord();
        record.Fields[FieldKeys.HoldingName] = holding;
        return record;
    }
}
