using System;
using System.Collections.Generic;
using System.Linq;

namespace AuswertungPro.Next.Application.Common;

/// <summary>
/// Gemeinsame Umlaut-Faltung fuer Textsuchen im ganzen Programm (Optikanalyse 28.09.2026,
/// Aufgabe 14: Befehle in der Strg+K-Suche). ae/oe/ue/ss statt Umlaute/scharfem s, klein
/// geschrieben. Reiner Text, keine WPF-Abhaengigkeit — damit sowohl die Einstellungssuche
/// (<c>SettingsSearchMatcher</c>, UI) als auch die globale Suche (<c>GlobaleSucheRegel</c>,
/// Application) dieselbe Regel verwenden. Nie eine zweite Faltung daneben schreiben.
/// </summary>
public static class SucheTextFaltung
{
    public static string Falte(string? text)
        => (text ?? string.Empty)
            .ToLowerInvariant()
            .Replace("ä", "ae")
            .Replace("ö", "oe")
            .Replace("ü", "ue")
            .Replace("ß", "ss");

    /// <summary>Alle durch Leerzeichen getrennten Suchwoerter muessen (gefaltet) im
    /// zusammengefuegten Text vorkommen (UND-Verknuepfung).</summary>
    public static bool PasstAlle(string suche, IEnumerable<string> texte)
    {
        var woerter = Falte(suche).Split(
            ' ',
            StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        if (woerter.Length == 0)
            return true;

        var inhalt = Falte(string.Join(
            " ",
            texte.Where(text => !string.IsNullOrWhiteSpace(text))));
        return woerter.All(wort => inhalt.Contains(wort, StringComparison.Ordinal));
    }
}
