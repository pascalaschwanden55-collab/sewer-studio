using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;

namespace AuswertungPro.Next.UI.ViewModels.Pages;

/// <summary>
/// Matcht einen angeklickten Kataster-Haltungsnamen gegen einen Projekt-Haltungsnamen.
/// Toleriert umgekehrte Schacht-Reihenfolge ("A-B" &lt;-&gt; "B-A") und Teilstrecken-Suffixe
/// (".1" .. ".99"); lange Kataster-Nummern mit Punkt (z.B. "7.32154", viele Stellen)
/// bleiben unberuehrt. Gleiche Regel wie in der QGIS-Bruecke, damit eine Auswahl
/// dieselbe Haltung findet.
///
/// Namensvermerk: Der Name stammt aus der am 2026-08-30 entfernten Kartenansicht.
/// Heute dient die Klasse ausschliesslich der QGIS-Auswahl
/// (DataPage/DataPageProjectBindingController). Sie darf spaeter umbenannt oder
/// ganz entfernt werden, wenn der QGIS-Weg sie nicht mehr braucht.
/// </summary>
public static class KarteHaltungNameMatcher
{
    private static readonly Regex SubsectionSuffix = new(@"^(.+)\.\d{1,2}$", RegexOptions.Compiled);

    /// <summary>True, wenn beide Namen dieselbe Haltung meinen (exakt / umgekehrt / ohne Suffix).</summary>
    public static bool Matches(string? clickedName, string? recordName)
    {
        if (string.IsNullOrWhiteSpace(clickedName) || string.IsNullOrWhiteSpace(recordName))
            return false;

        var candidates = Candidates(clickedName);
        foreach (var c in Candidates(recordName))
            if (candidates.Contains(c))
                return true;
        return false;
    }

    private static HashSet<string> Candidates(string name)
    {
        var set = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        void Add(string? s)
        {
            if (!string.IsNullOrWhiteSpace(s))
                set.Add(s!.Trim());
        }

        var trimmed = name.Trim();
        Add(trimmed);
        Add(Reverse(trimmed));

        var baseName = StripSuffix(trimmed);
        if (baseName is not null)
        {
            Add(baseName);
            Add(Reverse(baseName));
        }
        return set;
    }

    /// <summary>Kehrt "A-B" zu "B-A" um; null, wenn nicht genau zwei Teile.</summary>
    private static string? Reverse(string name)
    {
        var parts = name.Split('-', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        return parts.Length == 2 ? $"{parts[1]}-{parts[0]}" : null;
    }

    /// <summary>Schneidet einen Teilstrecken-Suffix (".1" .. ".99") ab; sonst null.</summary>
    private static string? StripSuffix(string name)
    {
        var match = SubsectionSuffix.Match(name);
        return match.Success ? match.Groups[1].Value : null;
    }
}
