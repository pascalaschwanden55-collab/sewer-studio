using System;
using System.Collections.Generic;
using System.Linq;

namespace AuswertungPro.Next.UI.Settings;

/// <summary>
/// Reiner Textabgleich fuer die Einstellungssuche. Umlaute und ihre ae/oe/ue-Schreibweise
/// gelten als gleich. Mehrere Suchwoerter muessen alle vorkommen.
/// </summary>
public static class SettingsSearchMatcher
{
    public static string Normalisiere(string text)
        => (text ?? string.Empty)
            .ToLowerInvariant()
            .Replace("ä", "ae")
            .Replace("ö", "oe")
            .Replace("ü", "ue")
            .Replace("ß", "ss");

    public static bool Passt(string suche, IEnumerable<string> texte)
    {
        var woerter = Normalisiere(suche).Split(
            ' ',
            StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        if (woerter.Length == 0)
            return true;

        var inhalt = Normalisiere(string.Join(
            " ",
            texte.Where(text => !string.IsNullOrWhiteSpace(text))));
        return woerter.All(wort => inhalt.Contains(wort, StringComparison.Ordinal));
    }
}
