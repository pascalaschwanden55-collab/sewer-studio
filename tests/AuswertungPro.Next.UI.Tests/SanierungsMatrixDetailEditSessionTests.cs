using System;
using System.Linq;
using AuswertungPro.Next.Domain.Models;
using AuswertungPro.Next.UI.ViewModels.Pages;
using Xunit;

namespace AuswertungPro.Next.UI.Tests;

public sealed class SanierungsMatrixDetailEditSessionTests
{
    [Fact]
    public void Mehrfach_massnahmen_sperren_matrix_schnellbearbeitung()
    {
        var row = new SanierungMatrixRowVm(new HaltungRecord(), "H1", "300", "12.0", 0, _ => { });

        row.MarkMultipleStoredMeasures();

        Assert.False(row.IsMatrixEditable);
        Assert.Contains("Mehrfach-Massnahme", row.Hinweis);
    }

    [Fact]
    public void FromCost_erstellt_arbeitskopie_und_mutiert_original_nicht()
    {
        var original = Holding(
            Measure("GFK", "GFK", Line("Hauptarbeit", "GFK", "GFK-Liner", "m", 1m, 100m, true)));

        var session = SanierungsMatrixDetailEditSession.FromCost(original, 0.081m);
        session.Measures[0].Lines[0].Qty = 2m;

        Assert.Equal(1m, original.Measures[0].Lines[0].Qty);
        Assert.True(session.IsDirty);
        Assert.Equal(200m, session.Total);
    }

    [Fact]
    public void FromCost_zeigt_auch_nicht_ausgewaehlte_positionen_zum_wieder_aktivieren()
    {
        var original = Holding(
            Measure("GFK", "GFK",
                Line("Hauptarbeit", "GFK", "GFK-Liner", "m", 1m, 100m, true),
                Line("Option", "VD", "Verkehrsdienst", "Stk", 1m, 50m, false)));

        var session = SanierungsMatrixDetailEditSession.FromCost(original, 0.081m);

        Assert.Equal(2, session.Measures[0].Lines.Count);
        var option = session.Measures[0].Lines.Single(l => l.ItemKey == "VD");
        Assert.False(option.Selected);

        option.Selected = true;

        Assert.True(session.IsDirty);
        Assert.Equal(150m, session.Total);
    }

    [Fact]
    public void ToHoldingCost_erhaelt_alle_massnahmen_und_berechnet_totals_neu()
    {
        var original = Holding(
            Measure("GFK", "GFK", Line("Hauptarbeit", "GFK", "GFK-Liner", "m", 1m, 100m, true)),
            Measure("LEM", "LEM", Line("Hauptarbeit", "LEM", "Linerendmanschette", "Stk", 1m, 40m, true)),
            Measure("BCA", "Anschluss abdichten", Line("Hauptarbeit", "BCA", "Anschluss", "Stk", 1m, 20m, true)));
        var session = SanierungsMatrixDetailEditSession.FromCost(original, 0.081m);

        session.Measures[2].Lines[0].Qty = 2m;
        var updated = session.ToHoldingCost("H1", new DateTime(2026, 6, 11), 0.081m);

        Assert.Equal("H1", updated.Holding);
        Assert.Equal(new DateTime(2026, 6, 11), updated.Date);
        Assert.Equal(new[] { "GFK", "LEM", "BCA" }, updated.Measures.Select(m => m.MeasureId));
        Assert.Equal(180m, updated.Total);
        Assert.Equal(14.58m, updated.MwstAmount);
        Assert.Equal(194.58m, updated.TotalInclMwst);
    }

    [Fact]
    public void MarkClean_setzt_dirty_nach_uebernehmen_zurueck()
    {
        var session = SanierungsMatrixDetailEditSession.FromCost(
            Holding(Measure("GFK", "GFK", Line("Hauptarbeit", "GFK", "GFK-Liner", "m", 1m, 100m, true))),
            0.081m);

        session.Measures[0].Lines[0].Qty = 3m;
        Assert.True(session.IsDirty);

        session.MarkClean();

        Assert.False(session.IsDirty);
    }

    [Fact]
    public void ApplyManualOverrides_erhaelt_handpreis_bei_neuberechneter_matrixzeile()
    {
        var oldCost = Holding(
            Measure("GFK", "GFK", Line("Hauptarbeit", "GFK", "GFK-Liner", "m", 10m, 123m, true) with
            {
                IsPriceOverridden = true,
                PriceHint = ""
            }));
        var recomputed = Holding(
            Measure("GFK", "GFK", Line("Hauptarbeit", "GFK", "GFK-Liner", "m", 10m, 90m, true)));

        SanierungsMatrixDetailOverrideMerger.ApplyManualOverrides(recomputed, oldCost);

        var line = recomputed.Measures[0].Lines[0];
        Assert.Equal(123m, line.UnitPrice);
        Assert.True(line.IsPriceOverridden);
        Assert.Equal(1230m, recomputed.Total);
        Assert.Equal(99.63m, recomputed.MwstAmount);
    }

    private static HoldingCost Holding(params MeasureCost[] measures)
    {
        return new HoldingCost
        {
            Holding = "ALT",
            Date = new DateTime(2025, 1, 2),
            Measures = measures.ToList(),
            Total = measures.Sum(m => m.Total),
            MwstRate = 0.081m,
        };
    }

    private static MeasureCost Measure(string id, string name, params CostLine[] lines)
    {
        return new MeasureCost
        {
            MeasureId = id,
            MeasureName = name,
            Total = lines.Where(l => l.Selected).Sum(l => l.Qty * l.UnitPrice),
            Lines = lines.ToList(),
        };
    }

    private static CostLine Line(string group, string itemKey, string text, string unit, decimal qty, decimal price, bool selected)
    {
        return new CostLine
        {
            Group = group,
            ItemKey = itemKey,
            Text = text,
            Unit = unit,
            Qty = qty,
            UnitPrice = price,
            Selected = selected,
        };
    }
}
