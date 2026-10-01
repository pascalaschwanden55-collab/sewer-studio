using System;
using System.Globalization;
using System.Linq;
using System.Text.RegularExpressions;

namespace AuswertungPro.Next.Infrastructure.HoldingDistribution;

/// <summary>Erkannte Pegel-Dichtheitspruefung an einem Behaelter. Objekt null = Bauwerk nicht lesbar.</summary>
internal sealed record BehaelterPruefung(string? Objekt, DateTime? Datum);

/// <summary>
/// Reine Texterkennung fuer Pegel-Dichtheitspruefungen an Behaeltern (Regenbecken,
/// Referenzgefaess). Diese Protokolle haben KEINE Haltung und keine zwei Schaechte -
/// ihr Pruefgegenstand ist ein Bauwerk.
///
/// Anlass 18.09.2026 (KIT-Pruefbericht Beckenmessung RB2 Ellbogenkapelle und
/// KIT-Pruefbericht Referenzmessung): Frueher liefen sie in die Haltungserkennung und
/// erzeugten dort erfundene Haltungen - "2005-2025" aus der Hersteller-Fusszeile und
/// "000-100" aus der Masstabelle der Anlage.
///
/// Erkannt wird nur, was BEIDE Merkmale traegt: die Kopfzeile "Pegel-Dichtheitspruefung"
/// UND den Pruefgegenstand "Behaelter". Die echten Haltungs-Pruefberichte derselben Firma
/// (Kanal-Ueberdruck Luft) tragen keines von beiden.
/// </summary>
internal static class BehaelterPruefungParser
{
    // Kopfzeile, teils ohne Leerzeichen und ohne Umlaute:
    // "Pegel-Dichtheitsprufung nach SIA190:2017..." / "Pegel-DichtheitsprufungnachSIAI 90..."
    private static readonly Regex PegelKopfRegex = new(
        @"Pegel\s*-?\s*Dichtheitspr[uüi]{1,2}f",
        RegexOptions.IgnoreCase | RegexOptions.Compiled);

    // "Erstprufung (Grundwasserschutzzone) Behalter"
    private static readonly Regex BehaelterRegex = new(
        @"\bBeh[aä]lter\b",
        RegexOptions.IgnoreCase | RegexOptions.Compiled);

    // Schweizer Sonderbauwerke mit Nummer: "RB 2", "RB2", "RUEB 1", "RKB-3".
    // Bewusst eine kurze, bekannte Liste: lieber kein Name als ein geratener.
    private static readonly Regex BauwerkRegex = new(
        @"\b(RB|R[UÜ]B|RUEB|RKB|SKB|PW)\s*[-_]?\s*(\d{1,3})\b",
        RegexOptions.IgnoreCase | RegexOptions.Compiled);

    // Pruefzeitpunkte des Protokolls: "Beginn Sattigung", "Beginn Prufung", "Prufungsende nach".
    private static readonly Regex PruefzeitZeileRegex = new(
        @"(Beginn|Pr[uü]fungsende|S[aä]ttigung|SSttigung)",
        RegexOptions.IgnoreCase | RegexOptions.Compiled);

    // Datum, ggf. direkt gefolgt von der Uhrzeit ohne Leerzeichen: "16.09.202612:03:18".
    private static readonly Regex DatumRegex = new(
        @"\b(\d{2})\.(\d{2})\.(\d{4})",
        RegexOptions.Compiled);

    internal static BehaelterPruefung? Erkenne(string dokumentText)
    {
        if (string.IsNullOrWhiteSpace(dokumentText))
            return null;

        if (!PegelKopfRegex.IsMatch(dokumentText) || !BehaelterRegex.IsMatch(dokumentText))
            return null;

        return new BehaelterPruefung(Bauwerksname(dokumentText), Pruefdatum(dokumentText));
    }

    /// <summary>Haeufigste Bauwerkskennung des Dokuments; bei Gleichstand die zuerst genannte.</summary>
    private static string? Bauwerksname(string text)
    {
        var treffer = BauwerkRegex.Matches(text)
            .Select(m => (m.Groups[1].Value.ToUpperInvariant().Replace("Ü", "UE") + m.Groups[2].Value))
            .ToList();

        if (treffer.Count == 0)
            return null;

        return treffer
            .Select((name, index) => (name, index))
            .GroupBy(x => x.name, StringComparer.OrdinalIgnoreCase)
            .OrderByDescending(g => g.Count())
            .ThenBy(g => g.Min(x => x.index))
            .First()
            .Key;
    }

    /// <summary>Datum der Pruefzeitzeilen; sonst das erste Datum des Dokuments.</summary>
    private static DateTime? Pruefdatum(string text)
    {
        var zeilen = text.Replace("\r\n", "\n").Split('\n');

        foreach (var zeile in zeilen)
        {
            if (!PruefzeitZeileRegex.IsMatch(zeile))
                continue;
            var ausZeile = ErstesDatum(zeile);
            if (ausZeile is not null)
                return ausZeile;
        }

        return ErstesDatum(text);
    }

    private static DateTime? ErstesDatum(string text)
    {
        foreach (Match m in DatumRegex.Matches(text))
        {
            var kandidat = $"{m.Groups[1].Value}.{m.Groups[2].Value}.{m.Groups[3].Value}";
            if (DateTime.TryParseExact(kandidat, "dd.MM.yyyy", CultureInfo.InvariantCulture,
                    DateTimeStyles.None, out var datum))
                return datum;
        }
        return null;
    }
}
