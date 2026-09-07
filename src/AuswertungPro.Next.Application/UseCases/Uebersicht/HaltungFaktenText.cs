using System.Collections.Generic;
using System.Linq;

namespace AuswertungPro.Next.Application.UseCases.Uebersicht;

/// <summary>
/// Die Eckdaten der Haltungsuebersicht (Inventar 5.1) als Text. Ein leerer Wert wird zum
/// Gedankenstrich, nie zu einer nackten Einheit: "&#160;m" oder "&#160;·&#160;" ohne Zahl sieht
/// nach einem Fehler aus. Reine Werte-Logik ohne WPF.
/// </summary>
public static class HaltungFaktenText
{
    /// <summary>Der Platzhalter fuer ein leeres Feld.</summary>
    public const string Leer = "–";

    /// <summary>Ein einzelner Wert, wahlweise mit Einheit ("30" + " m" = "30 m"; leer = "–").</summary>
    public static string Wert(string? wert, string? einheit = null)
    {
        var text = (wert ?? string.Empty).Trim();
        if (text.Length == 0)
            return Leer;
        var suffix = (einheit ?? string.Empty).Trim();
        return suffix.Length == 0 ? text : $"{text} {suffix}";
    }

    /// <summary>
    /// Mehrere Werte in einer Zelle ("300 · Kreisprofil"). Leere Teile fallen weg; sind alle
    /// leer, steht der Gedankenstrich.
    /// </summary>
    public static string Zusammen(IEnumerable<string?> teile, string trenner = " · ")
    {
        var gefuellt = (teile ?? Enumerable.Empty<string?>())
            .Select(t => (t ?? string.Empty).Trim())
            .Where(t => t.Length > 0)
            .ToList();
        return gefuellt.Count == 0 ? Leer : string.Join(trenner, gefuellt);
    }
}
