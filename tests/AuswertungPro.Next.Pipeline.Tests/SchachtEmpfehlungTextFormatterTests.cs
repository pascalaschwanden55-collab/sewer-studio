using AuswertungPro.Next.Application.Schacht;
using AuswertungPro.Next.Domain.Models;

namespace AuswertungPro.Next.Pipeline.Tests;

/// <summary>
/// Reine Formatier-Logik fuer einfache Schacht-Empfehlungen: Massnahmen-Text +
/// Nettosumme aus einer HoldingCost (kein NPK, kein IO).
/// </summary>
public sealed class SchachtEmpfehlungTextFormatterTests
{
    private static HoldingCost Cost(params (string Text, decimal Qty, decimal Price, bool Selected)[] lines)
    {
        var measure = new MeasureCost { MeasureId = "SCHACHT_EMPFEHLUNG", MeasureName = "Empfohlene Massnahmen" };
        foreach (var l in lines)
            measure.Lines.Add(new CostLine { Text = l.Text, Qty = l.Qty, UnitPrice = l.Price, Selected = l.Selected });
        return new HoldingCost { Holding = "KS 1", Measures = { measure } };
    }

    [Fact]
    public void BuildMassnahmenText_verbindet_selektierte_mit_Semikolon()
    {
        var cost = Cost(("Rahmen/Deckel ersetzen", 1m, 350m, true), ("Fugen sanieren", 1m, 480m, true));

        Assert.Equal("Rahmen/Deckel ersetzen; Fugen sanieren", SchachtEmpfehlungTextFormatter.BuildMassnahmenText(cost));
    }

    [Fact]
    public void BuildMassnahmenText_ignoriert_nicht_selektierte_und_leere()
    {
        var cost = Cost(
            ("Rahmen/Deckel ersetzen", 1m, 350m, true),
            ("", 1m, 0m, true),
            ("Steigeisen ersetzen", 1m, 120m, false));

        Assert.Equal("Rahmen/Deckel ersetzen", SchachtEmpfehlungTextFormatter.BuildMassnahmenText(cost));
    }

    [Fact]
    public void ResolveTotal_summiert_menge_mal_preis_der_selektierten()
    {
        var cost = Cost(("A", 2m, 100m, true), ("B", 1m, 480m, true), ("C", 5m, 999m, false));

        Assert.Equal(680m, SchachtEmpfehlungTextFormatter.ResolveTotal(cost));
    }

    [Fact]
    public void FormatTotal_zwei_nachkommastellen_invariant()
    {
        Assert.Equal("830.00", SchachtEmpfehlungTextFormatter.FormatTotal(830m));
    }
}
