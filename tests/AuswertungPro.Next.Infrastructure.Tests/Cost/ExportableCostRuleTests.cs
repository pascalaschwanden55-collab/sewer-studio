using AuswertungPro.Next.Application.Cost;
using AuswertungPro.Next.Domain.Models;

namespace AuswertungPro.Next.Infrastructure.Tests.Cost;

/// <summary>
/// Zentrale Regel "hat exportierbare Kostenzeile" (Gesamtaudit 2026-08-18, F-01).
/// </summary>
public sealed class ExportableCostRuleTests
{
    private static CostLine Zeile(bool selected, decimal qty, decimal price = 100m)
        => new() { Group = "G", ItemKey = "K", Qty = qty, UnitPrice = price, Selected = selected };

    private static HoldingCost Kosten(params CostLine[] zeilen)
        => new()
        {
            Holding = "S1",
            Measures = { new MeasureCost { MeasureId = "m", Lines = zeilen.ToList() } }
        };

    [Fact]
    public void AusgewaehlteZeileMitMenge_IstExportierbar()
        => Assert.True(ExportableCostRule.HasExportableLine(Kosten(Zeile(true, 3m))));

    [Fact]
    public void AbgewaehlteZeile_IstNichtExportierbar()
        => Assert.False(ExportableCostRule.HasExportableLine(Kosten(Zeile(false, 3m))));

    [Fact]
    public void ZeileOhneMenge_IstNichtExportierbar()
        => Assert.False(ExportableCostRule.HasExportableLine(Kosten(Zeile(true, 0m))));

    [Fact]
    public void NegativeMenge_IstNichtExportierbar()
        => Assert.False(ExportableCostRule.HasExportableLine(Kosten(Zeile(true, -5m))));

    [Fact]
    public void PreisNull_BleibtExportierbar()
    {
        // Im NPK-LV bleibt die EP-Spalte leer, wo der Preis variabel ist.
        // Eine Position ohne Preis ist trotzdem eine Position.
        Assert.True(ExportableCostRule.HasExportableLine(Kosten(Zeile(true, 2m, price: 0m))));
    }

    [Fact]
    public void MassnahmeGanzOhneZeilen_IstNichtExportierbar()
    {
        var kosten = new HoldingCost
        {
            Holding = "S1",
            Measures = { new MeasureCost { MeasureId = "leer" } }
        };

        // Genau dieser Fall galt frueher als "hat Massnahmen" (Measures.Count > 0).
        Assert.False(ExportableCostRule.HasExportableLine(kosten));
    }

    [Fact]
    public void KostenOhneMassnahmen_SindNichtExportierbar()
        => Assert.False(ExportableCostRule.HasExportableLine(new HoldingCost { Holding = "S1" }));

    [Fact]
    public void Null_IstNichtExportierbar()
    {
        Assert.False(ExportableCostRule.HasExportableLine((HoldingCost?)null));
        Assert.False(ExportableCostRule.HasExportableLine((MeasureCost?)null));
        Assert.False(ExportableCostRule.IsExportable(null));
    }
}
