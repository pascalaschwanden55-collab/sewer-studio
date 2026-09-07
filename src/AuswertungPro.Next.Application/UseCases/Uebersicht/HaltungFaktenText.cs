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

    /// <summary>
    /// Zaehlt <paramref name="wert"/> als leer? Leer sind: nichts, nur Leerzeichen — und der
    /// Text einer nicht gesetzten Bindung.
    ///
    /// Task 6: Ohne gewaehlte Zeile liefert WPF fuer eine Feldbindung
    /// <c>DependencyProperty.UnsetValue</c>. Wird das stur in Text verwandelt, steht in der
    /// Uebersicht "{DependencyProperty.UnsetValue}" (Pascals Bild vom 07.09., DN / Profil).
    /// Solche Platzhalter beginnen immer mit einer geschweiften Klammer; ein Fachwert tut das
    /// nie. Die Uebersicht bekommt deshalb hier dieselbe Antwort wie fuer ein leeres Feld.
    /// Diese Klasse bleibt dabei WPF-frei — sie kennt nur den Text.
    /// </summary>
    public static bool IstLeer(string? wert)
    {
        var text = (wert ?? string.Empty).Trim();
        return text.Length == 0 || text.StartsWith('{');
    }

    /// <summary>Ein einzelner Wert, wahlweise mit Einheit ("30" + " m" = "30 m"; leer = "–").</summary>
    public static string Wert(string? wert, string? einheit = null)
    {
        if (IstLeer(wert))
            return Leer;

        var text = (wert ?? string.Empty).Trim();
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
            .Where(t => !IstLeer(t))
            .Select(t => (t ?? string.Empty).Trim())
            .ToList();
        return gefuellt.Count == 0 ? Leer : string.Join(trenner, gefuellt);
    }
}
