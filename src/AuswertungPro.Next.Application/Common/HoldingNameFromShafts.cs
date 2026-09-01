using System;

namespace AuswertungPro.Next.Application.Common;

/// <summary>
/// Leitet den Haltungsnamen aus den beiden Schachtnummern ab, wenn er wirklich
/// aus ihnen besteht.
///
/// Die Reihenfolge ist im Bestand nicht einheitlich: Beim Import entsteht
/// "{Schacht_oben}-{Schacht_unten}", in Kundendaten steht bei einer
/// Gegenbefahrung auch der untere Schacht vorn. Deshalb wird die vorhandene
/// Reihenfolge gelesen statt eine feste Regel erzwungen — eine Haltung soll sich
/// beim Korrigieren einer Schachtnummer nicht unerwartet umdrehen.
///
/// Ein selbst vergebener Name wie "Jagdmatt West" folgt keinem der beiden
/// Muster und bleibt unangetastet.
/// </summary>
public static class HoldingNameFromShafts
{
    /// <summary>
    /// Der neue Haltungsname — oder <c>null</c>, wenn der Name nicht aus den
    /// Schachtnummern gebildet ist, eine noetige Angabe fehlt oder sich nichts
    /// aendert.
    /// </summary>
    public static string? Ableiten(
        string? aktuellerName,
        string? altOben,
        string? altUnten,
        string? neuOben,
        string? neuUnten)
    {
        var name = Trim(aktuellerName);
        var vorherOben = Trim(altOben);
        var vorherUnten = Trim(altUnten);
        var nachherOben = Trim(neuOben);
        var nachherUnten = Trim(neuUnten);

        if (name.Length == 0
            || vorherOben.Length == 0
            || vorherUnten.Length == 0
            || nachherOben.Length == 0
            || nachherUnten.Length == 0)
        {
            return null;
        }

        var neuerName = Passt(name, vorherOben, vorherUnten)
            ? Verbinde(nachherOben, nachherUnten)
            : Passt(name, vorherUnten, vorherOben)
                ? Verbinde(nachherUnten, nachherOben)
                : null;

        return neuerName is null
            || string.Equals(neuerName, name, StringComparison.OrdinalIgnoreCase)
                ? null
                : neuerName;
    }

    private static bool Passt(string name, string erster, string zweiter)
        => string.Equals(name, Verbinde(erster, zweiter), StringComparison.OrdinalIgnoreCase);

    private static string Verbinde(string erster, string zweiter) => erster + "-" + zweiter;

    private static string Trim(string? value) => (value ?? string.Empty).Trim();
}
