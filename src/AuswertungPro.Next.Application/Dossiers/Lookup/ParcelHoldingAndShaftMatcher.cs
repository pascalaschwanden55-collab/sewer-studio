using System;
using System.Collections.Generic;
using System.Linq;

namespace AuswertungPro.Next.Application.Dossiers.Lookup;

/// <summary>
/// Ordnet die Leitungen und Schaechte eines Projekts einer Parzelle zu.
///
/// Zwei Wege, und beide werden gebraucht:
///   - Die LAGE: der Kanton weiss, welche Leitungen ueber die Parzelle laufen.
///     Die privaten Hausanschluesse fuehrt er aber grosstenteils nicht.
///   - Der NAME: genau diese privaten Leitungen heissen nach ihrer Parzelle,
///     "439.01-36051" laeuft auf Parzelle 439. Das kostet keine Abfrage.
///
/// Die Schaechte ergeben sich auf zwei Wegen, und wieder werden beide
/// gebraucht:
///   - Aus den KNOTEN der Leitung: "439.01-36051" verbindet die Schaechte
///     "439.01" und "36051".
///   - Aus dem EIGENEN NAMEN: ein Schacht "439.02" steht auf Parzelle 439,
///     auch wenn keine dazu passende Leitung im Projekt liegt. Genau das
///     passiert, wenn nur der Schacht aufgenommen wurde oder seine Leitung
///     anders heisst.
///
/// Aufgenommen wird nur, was das Projekt wirklich kennt — ein erfundener
/// Schacht im Dossier waere schlimmer als eine kurze Liste.
///
/// Reine Logik: kein Netz, kein Dateizugriff.
/// </summary>
public static class ParcelHoldingAndShaftMatcher
{
    /// <summary>
    /// Die Leitungen des Projekts, deren Name auf diese Parzelle zeigt.
    /// </summary>
    public static IReadOnlyList<string> HoldingsByName(
        IReadOnlyList<string>? projectHoldingNames, string? parcelNumber)
    {
        var nummer = (parcelNumber ?? string.Empty).Trim();
        if (nummer.Length == 0 || projectHoldingNames is null)
            return Array.Empty<string>();

        var treffer = new List<string>();

        foreach (var name in projectHoldingNames)
        {
            if (string.IsNullOrWhiteSpace(name))
                continue;

            var kandidaten = ParcelNumberFromHoldingName.Extract(name);
            if (!kandidaten.Contains(nummer, StringComparer.Ordinal))
                continue;

            if (!treffer.Contains(name, StringComparer.OrdinalIgnoreCase))
                treffer.Add(name);
        }

        return treffer;
    }

    /// <summary>
    /// Die Schaechte an den Enden der uebergebenen Leitungen — aber nur die,
    /// die es im Projekt gibt. Die Reihenfolge folgt den Leitungen, damit die
    /// Liste im Dossier nachvollziehbar bleibt.
    /// </summary>
    public static IReadOnlyList<string> ShaftsOfHoldings(
        IReadOnlyList<string>? holdingDesignations,
        IReadOnlyList<string>? projectShaftNumbers)
    {
        if (holdingDesignations is null || projectShaftNumbers is null)
            return Array.Empty<string>();

        // Die Schreibweise im Projekt gewinnt: sie steht so im Protokoll.
        var bekannt = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        foreach (var nummer in projectShaftNumbers)
        {
            var sauber = (nummer ?? string.Empty).Trim();
            if (sauber.Length > 0 && !bekannt.ContainsKey(sauber))
                bekannt[sauber] = sauber;
        }

        var treffer = new List<string>();

        foreach (var leitung in holdingDesignations)
        {
            foreach (var knoten in Knoten(leitung))
            {
                if (!bekannt.TryGetValue(knoten, out var imProjekt))
                    continue;

                if (!treffer.Contains(imProjekt, StringComparer.OrdinalIgnoreCase))
                    treffer.Add(imProjekt);
            }
        }

        return treffer;
    }

    /// <summary>
    /// Die Schaechte, deren eigener Name auf diese Parzelle zeigt.
    ///
    /// Dieselbe Namensregel wie bei den Leitungen: ein Schachtname "439.02"
    /// nennt Parzelle 439. Die Regel verlangt mindestens zwei Ziffern vor dem
    /// Punkt, damit ein Bestandsname wie "7.34854" keine Parzelle 7 erfindet.
    /// </summary>
    public static IReadOnlyList<string> ShaftsOnParcel(
        IReadOnlyList<string>? projectShaftNumbers, string? parcelNumber)
    {
        var nummer = (parcelNumber ?? string.Empty).Trim();
        if (nummer.Length == 0 || projectShaftNumbers is null)
            return Array.Empty<string>();

        var treffer = new List<string>();

        foreach (var schacht in projectShaftNumbers)
        {
            var sauber = (schacht ?? string.Empty).Trim();
            if (sauber.Length == 0)
                continue;

            if (!ParcelNumberFromHoldingName.Extract(sauber).Contains(nummer, StringComparer.Ordinal))
                continue;

            if (!treffer.Contains(sauber, StringComparer.OrdinalIgnoreCase))
                treffer.Add(sauber);
        }

        return treffer;
    }

    /// <summary>
    /// Alle Schaechte einer Parzelle: zuerst die an den Enden der gewaehlten
    /// Leitungen, danach die, die nur ihr eigener Name der Parzelle zuordnet.
    ///
    /// Die Reihenfolge ist Absicht. Die Schaechte der Leitungen sind die, die
    /// der Empfaenger im Protokoll wiederfindet; die uebrigen kommen danach.
    /// </summary>
    public static IReadOnlyList<string> ShaftsForParcel(
        IReadOnlyList<string>? holdingDesignations,
        IReadOnlyList<string>? projectShaftNumbers,
        string? parcelNumber)
    {
        var ergebnis = ShaftsOfHoldings(holdingDesignations, projectShaftNumbers).ToList();

        foreach (var schacht in ShaftsOnParcel(projectShaftNumbers, parcelNumber))
        {
            if (!ergebnis.Contains(schacht, StringComparer.OrdinalIgnoreCase))
                ergebnis.Add(schacht);
        }

        return ergebnis;
    }

    /// <summary>
    /// Die Knotennamen einer Leitungsbezeichnung. Getrennt wird nur am
    /// Bindestrich; "439.01" bleibt ein Knoten, kein Rechenausdruck.
    /// </summary>
    internal static IEnumerable<string> Knoten(string? designation)
        => (designation ?? string.Empty)
            .Split('-', StringSplitOptions.RemoveEmptyEntries)
            .Select(t => t.Trim())
            .Where(t => t.Length > 0);
}
