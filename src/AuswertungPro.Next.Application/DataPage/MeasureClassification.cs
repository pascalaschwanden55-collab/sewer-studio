using System;
using AuswertungPro.Next.Domain.Models;

namespace AuswertungPro.Next.Application.DataPage;

/// <summary>
/// Reine Klassifikations-Helfer fuer Massnahmen-Zeilen (Liner, Hauptarbeit, Identifier-Match).
/// Aus <c>UI.DataPage.DataPageSanierungCostMapper</c> extrahiert, damit unit-testbar
/// (verhaltensneutral; die UI-Klasse delegiert ihre puren Methoden hierher).
/// </summary>
public static class MeasureClassification
{
    /// <summary>
    /// Prueft, ob eine Kostenzeile zur Gruppe "Hauptarbeit" gehoert (ueber Group-Feld ODER
    /// den Item-Key bzw. Text). Null-sicher.
    /// </summary>
    public static bool IsHauptarbeitLine(CostLine? line)
    {
        if (line is null)
            return false;

        if (!string.IsNullOrWhiteSpace(line.Group) &&
            line.Group.Contains("hauptarbeit", StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        return IsHauptarbeitIdentifier(line.ItemKey)
            || IsHauptarbeitIdentifier(line.Text);
    }

    /// <summary>
    /// Prueft, ob ein Bezeichner (ItemKey oder Text) auf eine Hauptarbeit-Position hinweist.
    /// </summary>
    public static bool IsHauptarbeitIdentifier(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
            return false;

        return MatchesIdentifier(value, "SCHLAUCHLINER")
            || MatchesIdentifier(value, "LINERENDMANSCHETTE")
            || MatchesIdentifier(value, "KURZLINER")
            || MatchesIdentifier(value, "MANSCHETTE")
            || MatchesIdentifier(value, "ANSCHLUSS_AUFFRAESEN")
            || MatchesIdentifier(value, "ANSCHLUSS_EINBINDEN")
            || MatchesIdentifier(value, "HAUPTARBEIT");
    }

    /// <summary>
    /// Prueft, ob eine Kostenzeile eine Liner-Position ist (Schlauchliner, Nadelfilz, GFK).
    /// </summary>
    public static bool IsLinerLine(CostLine? line)
    {
        if (line is null)
            return false;

        if (IsLinerIdentifier(line.ItemKey))
            return true;

        var text = line.Text ?? "";
        return text.Contains("schlauchliner", StringComparison.OrdinalIgnoreCase)
            || text.Contains("nadelfilz", StringComparison.OrdinalIgnoreCase)
            || text.Contains("gfk", StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>
    /// Prueft, ob ein Bezeichner auf eine Liner-Massnahme hinweist.
    /// </summary>
    public static bool IsLinerIdentifier(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
            return false;

        return MatchesIdentifier(value, "SCHLAUCHLINER_NADELFILZ")
            || MatchesIdentifier(value, "SCHLAUCHLINER_NADELFILZ_OPENEND")
            || MatchesIdentifier(value, "SCHLAUCHLINER_GFK")
            || MatchesIdentifier(value, "NADELFILZ_LINER_BIS_5M")
            || MatchesIdentifier(value, "SCHLAUCHLINER_NADELFILZ_BIS_5M")
            || MatchesIdentifier(value, "NADELFILZ")
            || MatchesIdentifier(value, "GFK");
    }

    /// <summary>
    /// Prueft ob <paramref name="value"/> dem <paramref name="pattern"/> entspricht:
    /// exakter Vergleich (case-insensitive) oder — bei einfachen Tokens ohne '_'/'-' —
    /// Substring-Match (fuer Legacy-Patterns wie "NADELFILZ" oder "GFK").
    /// </summary>
    public static bool MatchesIdentifier(string? value, string pattern)
    {
        if (string.IsNullOrWhiteSpace(value) || string.IsNullOrWhiteSpace(pattern))
            return false;

        var candidate = value.Trim();
        var token = pattern.Trim();
        if (string.Equals(candidate, token, StringComparison.OrdinalIgnoreCase))
            return true;

        // Legacy-Patterns wie "NADELFILZ" oder "GFK" sollen neuere IDs matchen.
        if (token.IndexOf('_') >= 0 || token.IndexOf('-') >= 0)
            return false;

        return candidate.Contains(token, StringComparison.OrdinalIgnoreCase);
    }
}
