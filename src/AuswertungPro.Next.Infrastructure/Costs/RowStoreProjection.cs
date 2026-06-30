using System.Collections.Generic;
using System.Linq;
using AuswertungPro.Next.Domain.Models;

namespace AuswertungPro.Next.Infrastructure.Costs;

/// <summary>
/// Reine Projektions-Logik fuer Sanierungs-Matrix-Zeilen (keine WPF-Abhaengigkeiten).
/// </summary>
public static class RowStoreProjection
{
    /// <summary>
    /// Hinweis-Text der Zeile: Anschluss-Zahl plus Warnung bei fehlenden Katalogpreisen
    /// (Audit W9: 0-CHF-Totals waren vorher unsichtbar).
    /// </summary>
    /// <param name="anschluesse">Anzahl der Anschluesse aus dem HaltungRecord.</param>
    /// <param name="cost">Berechneter Kostenblock fuer die Haltung.</param>
    public static string BuildRowHinweis(int anschluesse, HoldingCost cost)
    {
        var hints = new List<string>();
        if (anschluesse > 0)
            hints.Add($"{anschluesse} Anschluss(e)");
        if (cost.Measures.SelectMany(m => m.Lines).Any(l => l.Selected && l.Qty > 0m && l.UnitPrice <= 0m))
            hints.Add("Preis fehlt im Katalog");
        return string.Join(" | ", hints);
    }
}
