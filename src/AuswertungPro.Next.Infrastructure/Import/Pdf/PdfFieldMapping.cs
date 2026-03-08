using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text.RegularExpressions;

namespace AuswertungPro.Next.Infrastructure.Import.Pdf;

public sealed class PdfFieldRule
{
    public IReadOnlyList<string> Regexes { get; }
    public bool Multiline { get; }
    public int MaxLines { get; }

    public PdfFieldRule(IReadOnlyList<string> regexes, bool multiline, int maxLines)
    {
        Regexes = regexes;
        Multiline = multiline;
        MaxLines = maxLines <= 0 ? 1 : maxLines;
    }
}

public static class PdfFieldMapping
{
    public static readonly IReadOnlyDictionary<string, PdfFieldRule> Rules =
        new Dictionary<string, PdfFieldRule>(StringComparer.Ordinal)
        {
        ["Haltungsname"] = new PdfFieldRule(new[]
        {
            @"(?im)^\s*Leitung\s+(?<value>\d[\d\.]*\s*[-/]\s*\d[\d\.]*)\b",
            @"(?im)^\s*(Haltungsname|Haltungsnahme|Haltungs?nummer|Haltung\s*Nr\.?|Haltung[\s\-]?ID|Haltungs[-\s]?ID|Leitung[\s\-]?ID|Leitung\s*Nr\.?|Leitungsnummer)\s*[:\-]?\s*(?<value>\d[\d\.]*\s*[-/]\s*\d[\d\.]*)\b"
        }, multiline: false, maxLines: 1),
        ["Strasse"] = new PdfFieldRule(new[]
        {
            @"(?im)^\s*Stra(?:ß|ss)e\s*/\s*Standort\s+(?<value>.+?)(?:\s{2,}|$)",
            @"(?im)^\s*Strasse\s*[:\-]?\s*(?<value>.+?)(?:\s{2,}|$)"
        }, multiline: false, maxLines: 1),
        ["Rohrmaterial"] = new PdfFieldRule(new[]
        {
            @"(?im)^\s*Material\s+(?<value>.+?)(?:\s{2,}|$)"
        }, multiline: false, maxLines: 1),
        ["DN_mm"] = new PdfFieldRule(new[]
        {
            @"(?im)^\s*Dimension(?:\s*\[mm\])?\s+(?<value>\d{2,4}\s*/\s*\d{2,4}|\d{2,4})"
        }, multiline: false, maxLines: 1),
        ["Nutzungsart"] = new PdfFieldRule(new[]
        {
            @"(?im)^\s*Nutzungsart\s+(?<value>.+?)(?:\s{2,}|$)",
            @"(?im)^\s*Kanalart\s+(?<value>.+?)(?:\s{2,}|$)",
            @"(?i)\b(?<value>Schmutzabwasser|Schmutzwasser|Regenabwasser|Regenwasser|Mischabwasser)\b"
        }, multiline: false, maxLines: 1),
        ["Haltungslaenge_m"] = new PdfFieldRule(new[]
        {
            @"(?im)^\s*Leitungsl(?:a|ä)nge\s+(?<value>\d+(?:[.,]\d+)?)\s*m\b",
            @"(?im)^\s*Inspektionsl(?:a|ä)nge\s+(?<value>\d+(?:[.,]\d+)?)\s*m\b"
        }, multiline: false, maxLines: 1),
        ["Inspektionsrichtung"] = new PdfFieldRule(new[] {
            @"(?i)Inspektionsrichtung\s*[:\-]?\s*(?<value>gegen\s*flie(?:ss|ß)richtung|in\s*flie(?:ss|ß)richtung)",
            @"(?i)Aufnahmerichtung\s*[:\-]?\s*(?<value>gegen\s*flie(?:ss|ß)richtung|in\s*flie(?:ss|ß)richtung)",
            @"(?i)Richtung\s*[:\-]?\s*(?<value>gegen\s*flie(?:ss|ß)richtung|in\s*flie(?:ss|ß)richtung)",
            @"(?i)\b(gegen\s*flie(?:ss|ß)richtung|in\s*flie(?:ss|ß)richtung)\b"
        }, multiline: false, maxLines: 1),
        ["Primaere_Schaeden"] = new PdfFieldRule(Array.Empty<string>(), multiline: true, maxLines: 200),
        ["Zustandsklasse"] = new PdfFieldRule(Array.Empty<string>(), multiline: false, maxLines: 1),
        ["Pruefungsresultat"] = new PdfFieldRule(Array.Empty<string>(), multiline: true, maxLines: 3),
        ["Sanieren_JaNein"] = new PdfFieldRule(Array.Empty<string>(), multiline: false, maxLines: 1),
        ["Empfohlene_Sanierungsmassnahmen"] = new PdfFieldRule(Array.Empty<string>(), multiline: true, maxLines: 5),
        ["Kosten"] = new PdfFieldRule(Array.Empty<string>(), multiline: false, maxLines: 1),
        ["Eigentuemer"] = new PdfFieldRule(Array.Empty<string>(), multiline: false, maxLines: 1),
        ["Bemerkungen"] = new PdfFieldRule(Array.Empty<string>(), multiline: true, maxLines: 5),
        ["Link"] = new PdfFieldRule(Array.Empty<string>(), multiline: false, maxLines: 1),
        ["Renovierung_Inliner_Stk"] = new PdfFieldRule(Array.Empty<string>(), multiline: false, maxLines: 1),
        ["Renovierung_Inliner_m"] = new PdfFieldRule(Array.Empty<string>(), multiline: false, maxLines: 1),
        ["Anschluesse_verpressen"] = new PdfFieldRule(new[]
        {
            @"(?im)^\s*Anschl(?:u|ue|\u00FC)sse(?:\s*(?:verpressen|einbinden|auffr(?:a|\u00E4)sen))?\s*(?:\((?:Stk|St)\))?\s*[:\-]?\s*(?<value>\d+(?:[.,]\d+)?)\b",
            @"(?im)^\s*Anschluss(?:zahl)?\s*(?:\((?:Stk|St)\))?\s*[:\-]?\s*(?<value>\d+(?:[.,]\d+)?)\b",
            @"(?im)^\s*Anzahl\s+Anschl(?:u|ue|\u00FC)sse\s*[:\-]?\s*(?<value>\d+(?:[.,]\d+)?)\b"
        }, multiline: false, maxLines: 1),
        ["Reparatur_Manschette"] = new PdfFieldRule(Array.Empty<string>(), multiline: false, maxLines: 1),
        ["Reparatur_Kurzliner"] = new PdfFieldRule(Array.Empty<string>(), multiline: false, maxLines: 1),
        ["Erneuerung_Neubau_m"] = new PdfFieldRule(Array.Empty<string>(), multiline: false, maxLines: 1),
        ["Offen_abgeschlossen"] = new PdfFieldRule(Array.Empty<string>(), multiline: false, maxLines: 1),
        ["Datum_Jahr"] = new PdfFieldRule(new[]
        {
            @"(?im)^\s*Insp\.?-?\s*datum\s+(?<value>\d{2}\.\d{2}\.\d{4})",
            @"(?im)^\s*Insp\.-?\s*Datum\s+(?<value>\d{2}\.\d{2}\.\d{4})"
        }, multiline: false, maxLines: 1)
        };
}

public static class PdfPostProcessors
{
    public static string Apply(string fieldName, string value)
    {
        value ??= "";
        value = value.Trim();

        return fieldName switch
        {
            "Kosten" => NormalizeKosten(value),
            "Sanieren_JaNein" => NormalizeJaNein(value),
            "Offen_abgeschlossen" => NormalizeOffenAbgeschlossen(value),
            "Rohrmaterial" => NormalizeMaterial(value),
            "Nutzungsart" => NormalizeNutzungsart(value),
            "Inspektionsrichtung" => NormalizeInspektionsrichtung(value),
            "DN_mm" => NormalizeDn(value),
            "Anschluesse_verpressen" => NormalizeNonNegativeInt(value),
            "Strasse" => TrimAtDoubleSpace(value),
            _ => value
        };
    }

    private static string NormalizeKosten(string v)
    {
        // Entferne CHF / Währung, Tausendertrenner, Komma -> Punkt
        v = Regex.Replace(v, @"(?i)CHF|Fr\.|Sfr\.", "");
        v = v.Replace("'", "");
        v = v.Replace(",", ".");
        return v.Trim();
    }

    private static string NormalizeInt(string v)
    {
        var match = Regex.Match(v ?? "", @"-?\d+(?:[.,]\d+)?");
        if (!match.Success)
            return "";

        var normalized = match.Value.Replace(',', '.');
        if (!decimal.TryParse(normalized, NumberStyles.Float, CultureInfo.InvariantCulture, out var parsed))
            return "";

        if (parsed <= 0)
            return "";

        var rounded = (int)Math.Round(parsed, 0, MidpointRounding.AwayFromZero);
        return rounded.ToString(CultureInfo.InvariantCulture);
    }

    private static string NormalizeNonNegativeInt(string v)
    {
        var match = Regex.Match(v ?? "", @"-?\d+(?:[.,]\d+)?");
        if (!match.Success)
            return "";

        var normalized = match.Value.Replace(',', '.');
        if (!decimal.TryParse(normalized, NumberStyles.Float, CultureInfo.InvariantCulture, out var parsed))
            return "";

        if (parsed < 0)
            return "";

        var rounded = (int)Math.Round(parsed, 0, MidpointRounding.AwayFromZero);
        return rounded.ToString(CultureInfo.InvariantCulture);
    }

    private static string NormalizeJaNein(string v)
    {
        if (Regex.IsMatch(v, "(?i)ja|yes")) return "Ja";
        if (Regex.IsMatch(v, "(?i)nein|no")) return "Nein";
        return v.Trim();
    }

    private static string NormalizeOffenAbgeschlossen(string v)
    {
        if (Regex.IsMatch(v, "(?i)offen")) return "offen";
        if (Regex.IsMatch(v, "(?i)abgeschlossen")) return "abgeschlossen";
        return v.Trim();
    }

    private static string NormalizeMaterial(string v)
    {
        // Take only the first line – WinCan DB sometimes appends cleaning info
        // like "Zement\nGereinigt    Ja" into the material field.
        var firstLine = v.Split('\n')[0].Trim();
        // Strip trailing non-material tokens (e.g. "Gereinigt", "Ja", "Nein")
        firstLine = Regex.Replace(firstLine, @"(?i)\s*(gereinigt|nicht\s*gereinigt|verschmutzt)\s*(ja|nein)?\s*$", "").Trim();

        return firstLine;
    }

    private static string NormalizeNutzungsart(string v)
    {
        // "-" oder "n/a" als leer behandeln
        var trimmed = v.Trim();
        if (trimmed is "-" or "--" or "n/a" or "N/A" or "k.A." or "")
            return "";
        if (Regex.IsMatch(v, "(?i)Schmutzabwasser|Schmutzwasser")) return "Schmutzwasser";
        if (Regex.IsMatch(v, "(?i)Regenabwasser|Regenwasser")) return "Regenwasser";
        if (Regex.IsMatch(v, "(?i)Mischabwasser|Mischwasser")) return "Mischabwasser";
        return trimmed;
    }

    private static string NormalizeDn(string v)
    {
        if (string.IsNullOrWhiteSpace(v))
            return v;

        var m = Regex.Match(v, @"(?<dn>\d{2,4})");
        return m.Success ? m.Groups["dn"].Value : v.Trim();
    }

    private static string NormalizeInspektionsrichtung(string v)
    {
        if (Regex.IsMatch(v, "(?i)gegen\\s*flie(?:ss|ß)richtung")) return "Gegen Fliessrichtung";
        if (Regex.IsMatch(v, "(?i)in\\s*flie(?:ss|ß)richtung")) return "In Fliessrichtung";
        return v.Trim();
    }

    /// <summary>
    /// Schneidet den Wert am ersten Doppel-Leerzeichen ab.
    /// PDF-Tabellen trennen Spalten durch 2+ Leerzeichen.
    /// </summary>
    private static string TrimAtDoubleSpace(string v)
    {
        if (string.IsNullOrWhiteSpace(v))
            return v;
        var m = Regex.Match(v, @"^(?<t>.+?)(?:\s{2,}|$)");
        return m.Success ? m.Groups["t"].Value.Trim() : v.Trim();
    }
}
