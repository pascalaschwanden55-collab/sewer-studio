using AuswertungPro.Next.Application.Costs;
using AuswertungPro.Next.Domain.Models;
using AuswertungPro.Next.Infrastructure.Costs;

namespace AuswertungPro.Next.Infrastructure.Tests.Costs;

public sealed class SanierungsMatrixStoredRowProjectionTests
{
    private static readonly MeasureOption EmptyOption = new(null, "keine", "", false, "");
    private static readonly MeasureOption MainOption = new(
        "KURZLINER",
        "Kurzliner",
        "Reparatur",
        true,
        "HAUPT_KURZLINER");

    [Fact]
    public void Project_waehlt_keine_Option_wenn_keine_Kosten_gespeichert_sind()
    {
        var state = SanierungsMatrixStoredRowProjection.Project(
            new ProjectCostStore(),
            "H-01",
            [EmptyOption, MainOption]);

        Assert.Same(EmptyOption, state.SelectedMeasure);
        Assert.Null(state.StoredCost);
        Assert.Equal(0m, state.Total);
        Assert.Null(state.AdditionalOption);
        Assert.False(state.HasMultipleMeasures);
    }

    [Fact]
    public void Project_liest_Hauptmenge_und_Zusatzoptionen_aus_erster_Massnahme()
    {
        var cost = Cost(
            Measure(
                "KURZLINER",
                Line("HAUPT_KURZLINER", 3m),
                Line(SanierungsMatrixOptionKeys.Verkehrsdienst, 1m),
                Line(SanierungsMatrixOptionKeys.Dokumentation, 1m, selected: false)));
        var store = Store("H-01", cost);

        var state = SanierungsMatrixStoredRowProjection.Project(
            store,
            "H-01",
            [EmptyOption, MainOption]);

        Assert.Same(cost, state.StoredCost);
        Assert.Same(MainOption, state.SelectedMeasure);
        Assert.Equal(3m, state.Menge);
        Assert.True(state.Verkehrsdienst);
        Assert.False(state.Dokumentation);
        Assert.Null(state.AdditionalOption);
    }

    [Fact]
    public void Project_erhaelt_unbekannte_und_mehrfache_Massnahmen_fuer_den_Detail_Editor()
    {
        var cost = Cost(
            Measure("FREMD", Line("FREMD", 1m)),
            Measure("ZWEIT", Line("ZWEIT", 1m)));
        var store = Store("H-01", cost);

        var state = SanierungsMatrixStoredRowProjection.Project(
            store,
            "H-01",
            [EmptyOption, MainOption]);

        Assert.True(state.HasMultipleMeasures);
        Assert.Equal("FREMD", state.SelectedMeasure.Id);
        Assert.Equal("FREMD (gespeichert)", state.SelectedMeasure.Name);
        Assert.Same(state.SelectedMeasure, state.AdditionalOption);
        Assert.Equal(cost.Total, state.Total);
    }

    private static ProjectCostStore Store(string holding, HoldingCost cost)
        => new() { ByHolding = new Dictionary<string, HoldingCost> { [holding] = cost } };

    private static HoldingCost Cost(params MeasureCost[] measures)
        => new()
        {
            Total = measures.Sum(measure => measure.Total),
            Measures = measures.ToList()
        };

    private static MeasureCost Measure(string id, params CostLine[] lines)
        => new()
        {
            MeasureId = id,
            MeasureName = id,
            Total = lines.Sum(line => line.Qty * line.UnitPrice),
            Lines = lines.ToList()
        };

    private static CostLine Line(string itemKey, decimal quantity, bool selected = true)
        => new()
        {
            ItemKey = itemKey,
            Qty = quantity,
            UnitPrice = 10m,
            Selected = selected
        };
}
