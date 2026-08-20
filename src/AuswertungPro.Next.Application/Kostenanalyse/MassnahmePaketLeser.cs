using System;
using System.Collections.Generic;
using System.Linq;
using AuswertungPro.Next.Domain.Models;

namespace AuswertungPro.Next.Application.Kostenanalyse;

/// <summary>
/// Liest das Massnahmenpaket einer Haltung aus ihren Kostenzeilen — nur Mengen, nie Preise.
/// Dadurch bleibt ein Fall von heute auch nach einer Preisrunde gueltig.
/// Es zaehlen nur ausgewaehlte Zeilen, dieselbe Regel wie in der Kostenzusammenstellung.
/// </summary>
public static class MassnahmePaketLeser
{
    public static IReadOnlyList<MassnahmePosition> Lies(HoldingCost? cost)
    {
        if (cost is null)
            return [];

        var eimer = new Dictionary<string, (decimal Menge, string Einheit, int Reihenfolge)>(
            StringComparer.OrdinalIgnoreCase);
        var lauf = 0;

        foreach (var zeile in cost.Measures.SelectMany(m => m.Lines).Where(l => l.Selected))
        {
            if (zeile.Qty <= 0m)
                continue;

            var key = (zeile.ItemKey ?? "").Trim();
            if (key.Length == 0)
                key = (zeile.Text ?? "").Trim();
            if (key.Length == 0)
                continue;

            var einheit = (zeile.Unit ?? "").Trim();

            if (eimer.TryGetValue(key, out var vorher))
                eimer[key] = (vorher.Menge + zeile.Qty, vorher.Einheit, vorher.Reihenfolge);
            else
                eimer[key] = (zeile.Qty, einheit, lauf++);
        }

        return eimer
            .OrderBy(kv => kv.Value.Reihenfolge)
            .Select(kv => new MassnahmePosition(kv.Key, kv.Value.Menge, kv.Value.Einheit))
            .ToList();
    }
}
