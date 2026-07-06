using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;

namespace AuswertungPro.Next.UI.DataPage;

/// <summary>
/// Erkennt Haltungen mit mehreren Inspektionen: Records, deren Haltungsname sich nur durch
/// einen abschliessenden Inspektions-Suffix (".01", ".02", …) unterscheidet, gehoeren zur
/// selben physischen Haltung (z.B. Erst- + Zweit-/Gegeninspektion, je eigenes Video + Codierung).
///
/// WICHTIG: Der Suffix ist eine kurze Laufnummer (1–2 Ziffern) am ENDE des Namens. Kataster-
/// Knoten mit Punkt (z.B. "10.24046-345402", "21730-10.23867") bleiben dadurch unberuehrt —
/// deren Nachkommateil hat mehr Stellen. Zusaetzlich zaehlt eine Gruppe erst ab ZWEI Records
/// als Mehrfachinspektion, sodass ein faelschlich abgeschnittener Suffix folgenlos bleibt.
/// </summary>
public static class HaltungInspektionsGruppen
{
    private static readonly Regex SuffixPattern =
        new(@"^(.*[^.])\.\d{1,2}$", RegexOptions.Compiled);

    /// <summary>Basis-Haltungsname ohne Inspektions-Suffix (aus "A-B.01" wird "A-B").</summary>
    public static string BasisName(string? haltungsname)
    {
        var name = haltungsname?.Trim() ?? string.Empty;
        var match = SuffixPattern.Match(name);
        return match.Success ? match.Groups[1].Value : name;
    }

    /// <summary>
    /// Liefert die Basis-Haltungsnamen, die in der uebergebenen Liste mehr als eine Inspektion
    /// (Record) tragen — also die Haltungen mit Doppel-/Mehrfachinspektion.
    /// </summary>
    public static IReadOnlySet<string> MehrfachInspektionsBasen(IEnumerable<string?> haltungsnamen)
    {
        var counts = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
        foreach (var name in haltungsnamen ?? Enumerable.Empty<string?>())
        {
            if (string.IsNullOrWhiteSpace(name))
                continue;
            var basis = BasisName(name);
            counts[basis] = counts.TryGetValue(basis, out var c) ? c + 1 : 1;
        }

        return counts.Where(kv => kv.Value > 1)
            .Select(kv => kv.Key)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);
    }

    /// <summary>
    /// Gehoert diese Haltung zu einer Doppel-/Mehrfachinspektion (gibt es also einen weiteren
    /// Record mit demselben Basis-Namen in der Liste)?
    /// </summary>
    public static bool IstMehrfachInspektion(string? haltungsname, IReadOnlySet<string> mehrfachBasen)
    {
        if (string.IsNullOrWhiteSpace(haltungsname) || mehrfachBasen is null || mehrfachBasen.Count == 0)
            return false;
        return mehrfachBasen.Contains(BasisName(haltungsname));
    }
}
