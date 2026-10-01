using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using AuswertungPro.Next.Domain.Models;

namespace AuswertungPro.Next.Application.Schacht;

/// <summary>
/// Reine Formatier-Logik fuer die einfachen Schacht-Empfehlungen: aus einer
/// <see cref="HoldingCost"/> (eine Massnahme, mehrere Zeilen) den Massnahmen-Text
/// und die Nettosumme bilden. Bewusst NPK-frei — kein Katalog, keine ItemKeys.
/// </summary>
public static class SchachtEmpfehlungTextFormatter
{
    /// <summary>Namen der selektierten, nicht-leeren Zeilen mit "; " verbunden.</summary>
    public static string BuildMassnahmenText(HoldingCost? cost)
    {
        var namen = SelectedLines(cost)
            .Where(l => !string.IsNullOrWhiteSpace(l.Text))
            .Select(l => l.Text.Trim());

        return string.Join("; ", namen);
    }

    /// <summary>Nettosumme = Summe (Menge * Preis) ueber alle selektierten Zeilen.</summary>
    public static decimal ResolveTotal(HoldingCost? cost)
        => SelectedLines(cost).Sum(l => l.Qty * l.UnitPrice);

    /// <summary>Alle selektierten Zeilen ueber alle Massnahmen, unabhaengig von ihrem Text.</summary>
    private static IEnumerable<CostLine> SelectedLines(HoldingCost? cost)
    {
        if (cost is null)
            return Enumerable.Empty<CostLine>();

        return cost.Measures
            .SelectMany(m => m.Lines)
            .Where(l => l.Selected);
    }

    /// <summary>Betrag mit zwei Nachkommastellen, kultur-invariant (wie Tabellenfeld "Kosten").</summary>
    public static string FormatTotal(decimal total)
        => total.ToString("0.00", CultureInfo.InvariantCulture);
}
