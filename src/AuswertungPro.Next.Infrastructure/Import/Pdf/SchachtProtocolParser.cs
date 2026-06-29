using System.Globalization;
using System.Text.RegularExpressions;

namespace AuswertungPro.Next.Infrastructure.Import.Pdf;

/// <summary>
/// Reine Parse-Logik fuer Schachtprotokoll-PDFs.
/// Kein IO, kein State, kein Dokumentobjekt -- nur Text-in, Ergebnis-out.
/// </summary>
internal static class SchachtProtocolParser
{
    /// <summary>
    /// Reihenfolge der Bauteile fuer die Schadens-Sortierung (Anzeigereihenfolge).
    /// </summary>
    internal static readonly string[] SchachtComponentOrder =
    {
        "Schachtdeckel",
        "Deckelrahmen",
        "Schachthals",
        "Konus",
        "Schachtrohr",
        "Bankett",
        "Durchlaufrinne",
        "Anschluss",
        "Leiter/Steigeisen",
        "Tauchbogen"
    };

    /// <summary>
    /// Parst alle relevanten Felder aus dem Volltext eines Schachtprotokolls.
    /// </summary>
    internal static LegacyPdfImportService.ParsedSchachtFields ParseSchachtFields(string text)
    {
        if (string.IsNullOrWhiteSpace(text))
            return new LegacyPdfImportService.ParsedSchachtFields(null, null, null, null, null, null, null);

        var normalized = text.Replace("\r\n", "\n");

        string? GetFirst(string pattern)
        {
            var m = Regex.Match(normalized, pattern, RegexOptions.IgnoreCase | RegexOptions.Multiline);
            return m.Success ? m.Groups["v"].Value.Trim() : null;
        }

        var schachtNummer = GetFirst(@"\bNr\.?\s*[:\-]?\s*(?<v>\d{3,})\b")
                            ?? GetFirst(@"\bSchachtnummer\s*[:\-]?\s*(?<v>\d{3,})\b");

        var dateRaw = GetFirst(@"\bDatum\s*[:\-]?\s*(?<v>" + SewerTextPatterns.GermanDateCore + @")\b");
        var datum = NormalizeDate(dateRaw);

        var funktion = GetFirst(@"\bSchachttyp\s+(?<v>[^\n\r]+)")?.Trim();

        var primaryDamages = ParsePrimaryDamagesFromConditionSection(normalized);
        var maengelfrei = Regex.IsMatch(normalized, @"\bM\S*ngelfrei\b", RegexOptions.IgnoreCase)
            ? "Maengelfrei"
            : null;
        var effectivePrimaryDamages = !string.IsNullOrWhiteSpace(primaryDamages) ? primaryDamages : maengelfrei;
        var status = DeriveSchachtStatus(effectivePrimaryDamages, normalized);

        var bemerkung = GetFirst(@"\bBemerkung(?:en)?\s*[:\-]?\s*(?<v>[^\n\r]+)");

        return new LegacyPdfImportService.ParsedSchachtFields(
            SchachtNummer: schachtNummer,
            Datum: datum,
            Funktion: funktion,
            PrimaereSchaeden: effectivePrimaryDamages,
            Bemerkungen: bemerkung,
            Status: status,
            Link: null);
    }

    /// <summary>
    /// Leitet den Schacht-Status (offen/abgeschlossen) aus Schaeden und explizitem Status-Text ab.
    /// </summary>
    internal static string? DeriveSchachtStatus(string? primaryDamages, string fullText)
    {
        // Expliziter Status-Text im PDF hat Vorrang.
        var explicitStatus = TryParseExplicitStatus(fullText);
        if (!string.IsNullOrWhiteSpace(explicitStatus))
            return explicitStatus;

        // Andernfalls aus Schadensbewertung ableiten.
        if (string.IsNullOrWhiteSpace(primaryDamages))
            return null;

        return string.Equals(primaryDamages.Trim(), "Maengelfrei", StringComparison.OrdinalIgnoreCase)
            ? "abgeschlossen"
            : "offen";
    }

    /// <summary>
    /// Sucht eine explizite Status-Zeile ("Status offen/abgeschlossen") im Text.
    /// </summary>
    internal static string? TryParseExplicitStatus(string text)
    {
        if (string.IsNullOrWhiteSpace(text))
            return null;

        var normalized = text.Replace("\r\n", "\n");
        foreach (var lineRaw in normalized.Split('\n'))
        {
            var line = (lineRaw ?? "").Trim();
            if (line.Length == 0)
                continue;

            if (!line.Contains("Status", StringComparison.OrdinalIgnoreCase))
                continue;

            if (Regex.IsMatch(line, @"\babgeschlossen\b", RegexOptions.IgnoreCase))
                return "abgeschlossen";
            if (Regex.IsMatch(line, @"\boffen\b", RegexOptions.IgnoreCase))
                return "offen";
        }

        return null;
    }

    /// <summary>
    /// Parst strukturierte Bauteil-Schaeden aus dem Zustandsabschnitt des Schachtprotokolls.
    /// </summary>
    internal static IReadOnlyList<(string Component, string Damage)> ParseSchachtDamageEntries(string text)
    {
        if (string.IsNullOrWhiteSpace(text))
            return Array.Empty<(string, string)>();

        var normalized = NormalizeCheckboxGlyphs(text);
        var lines = normalized.Split('\n');
        var entries = new List<(string Component, string Damage, int EncounterIndex)>();
        var encounterIndex = 0;

        foreach (var rawLine in lines)
        {
            var line = rawLine?.Trim();
            if (string.IsNullOrWhiteSpace(line))
                continue;

            if (!TryExtractComponentTail(line!, out var component, out var tail))
                continue;

            foreach (var damage in GetDamageCandidatesForComponent(component))
            {
                if (!IsMarkedDamage(tail, damage))
                    continue;

                entries.Add((component, damage, encounterIndex++));
            }
        }

        if (entries.Count == 0)
            return Array.Empty<(string, string)>();

        return entries
            .GroupBy(x => $"{x.Component}|{x.Damage}", StringComparer.OrdinalIgnoreCase)
            .Select(g => g.First())
            .OrderBy(x => GetComponentOrderIndex(x.Component))
            .ThenBy(x => GetDamageOrderIndex(x.Component, x.Damage))
            .ThenBy(x => x.EncounterIndex)
            .Select(x => (x.Component, x.Damage))
            .ToList();
    }

    /// <summary>
    /// Normalisiert Checkbox-Glyphen aus verschiedenen Zeichensatz-Encodierungen.
    /// </summary>
    internal static string NormalizeCheckboxGlyphs(string text)
    {
        return text
            .Replace("â—", "●")
            .Replace("â€¢", "●")
            .Replace("âœ“", "✓")
            .Replace("âœ”", "✓")
            .Replace("âœ—", "✗")
            .Replace("âœ˜", "✗")
            .Replace("☒", "☒")
            .Replace("☑", "☑")
            .Replace("☐", "☐")
            .Replace("■", "■")
            .Replace("□", "□")
            .Replace("•", "●")
            .Replace("✔", "✓")
            .Replace("✘", "✗");
    }

    /// <summary>
    /// Versucht einen Bauteil-Namen aus dem Zeilenanfang zu extrahieren und gibt den Rest (tail) zurueck.
    /// </summary>
    internal static bool TryExtractComponentTail(string line, out string component, out string tail)
    {
        foreach (var candidate in SchachtComponentOrder)
        {
            var m = Regex.Match(line, @"^\s*" + Regex.Escape(candidate) + @"\b(?<tail>.*)$", RegexOptions.IgnoreCase);
            if (!m.Success)
                continue;

            component = candidate;
            tail = m.Groups["tail"].Value ?? "";
            return true;
        }

        component = "";
        tail = "";
        return false;
    }

    /// <summary>
    /// Gibt die bekannten Schadens-Kandidaten fuer ein Bauteil zurueck.
    /// </summary>
    internal static IReadOnlyList<string> GetDamageCandidatesForComponent(string component)
    {
        if (component.Equals("Schachtdeckel", StringComparison.OrdinalIgnoreCase))
            return new[] { "gerissen", "ausgebrochen", "korrodiert", "klemmt" };

        if (component.Equals("Deckelrahmen", StringComparison.OrdinalIgnoreCase))
            return new[] { "gerissen", "ausgebrochen", "lose" };

        if (component.Equals("Schachthals", StringComparison.OrdinalIgnoreCase)
            || component.Equals("Konus", StringComparison.OrdinalIgnoreCase)
            || component.Equals("Schachtrohr", StringComparison.OrdinalIgnoreCase))
            return new[] { "gerissen", "ausgebrochen", "korrodiert", "fugen mangelhaft verputzt" };

        if (component.Equals("Bankett", StringComparison.OrdinalIgnoreCase)
            || component.Equals("Durchlaufrinne", StringComparison.OrdinalIgnoreCase))
            return new[] { "gerissen", "ausgebrochen", "korrodiert", "ablagerungen" };

        if (component.Equals("Anschluss", StringComparison.OrdinalIgnoreCase))
            return new[] { "gerissen", "ausgebrochen", "mangelhaft eingebunden" };

        if (component.Equals("Leiter/Steigeisen", StringComparison.OrdinalIgnoreCase))
            return new[] { "fehlt", "zu kurz", "verrostet", "defekt" };

        if (component.Equals("Tauchbogen", StringComparison.OrdinalIgnoreCase))
            return new[] { "fehlt", "defekt" };

        return Array.Empty<string>();
    }

    /// <summary>
    /// Prueft ob ein Schaden-Text im tail durch ein Marker-Glyph als markiert gilt.
    /// </summary>
    internal static bool IsMarkedDamage(string tail, string damage)
    {
        if (string.IsNullOrWhiteSpace(tail) || string.IsNullOrWhiteSpace(damage))
            return false;

        var marker = @"(?:●|•|■|☒|☑|✓|✔|✗|✘|\[\s*[xX]\s*\]|\(\s*[xX]\s*\))";
        var d = Regex.Escape(damage);

        // Marker unmittelbar vor dem Schaden: "● ausgebrochen" / "[x] korrodiert"
        var before = marker + @"\s*" + d + @"\b";
        if (Regex.IsMatch(tail, before, RegexOptions.IgnoreCase))
            return true;

        // Marker unmittelbar nach dem Schaden: "ausgebrochen ●" / "korrodiert [x]"
        var after = d + @"\b\s*" + marker;
        if (Regex.IsMatch(tail, after, RegexOptions.IgnoreCase))
            return true;

        // Robustheitsfall: marker und Schaden in unmittelbarer Nachbarschaft (max 8 Zeichen)
        var nearBefore = marker + @"[^\n\r]{0,8}\b" + d + @"\b";
        if (Regex.IsMatch(tail, nearBefore, RegexOptions.IgnoreCase))
            return true;

        var nearAfter = @"\b" + d + @"\b[^\n\r]{0,8}" + marker;
        return Regex.IsMatch(tail, nearAfter, RegexOptions.IgnoreCase);
    }

    /// <summary>
    /// Gibt den Sortierindex eines Bauteils gemaess SchachtComponentOrder zurueck.
    /// </summary>
    internal static int GetComponentOrderIndex(string component)
    {
        for (var i = 0; i < SchachtComponentOrder.Length; i++)
        {
            if (string.Equals(SchachtComponentOrder[i], component, StringComparison.OrdinalIgnoreCase))
                return i;
        }

        return int.MaxValue;
    }

    /// <summary>
    /// Gibt den Sortierindex eines Schadens innerhalb eines Bauteils zurueck.
    /// </summary>
    internal static int GetDamageOrderIndex(string component, string damage)
    {
        var candidates = GetDamageCandidatesForComponent(component);
        for (var i = 0; i < candidates.Count; i++)
        {
            if (string.Equals(candidates[i], damage, StringComparison.OrdinalIgnoreCase))
                return i;
        }

        return int.MaxValue;
    }

    /// <summary>
    /// Normalisiert ein rohes Datumsstring in das einheitliche Format dd.MM.yyyy.
    /// </summary>
    internal static string? NormalizeDate(string? raw)
    {
        if (string.IsNullOrWhiteSpace(raw))
            return null;

        var candidate = raw.Trim();
        var formats = new[] { "dd.MM.yyyy", "dd.MM.yy", "dd/MM/yyyy", "dd/MM/yy", "dd-MM-yyyy", "dd-MM-yy", "yyyy-MM-dd" };
        if (DateTime.TryParseExact(candidate, formats, CultureInfo.InvariantCulture, DateTimeStyles.None, out var parsed))
            return parsed.ToString("dd.MM.yyyy", CultureInfo.InvariantCulture);

        return candidate;
    }

    // --- Hilfsmethoden (intern) ---

    private static string? ParsePrimaryDamagesFromConditionSection(string text)
    {
        var entries = ParseSchachtDamageEntries(text);
        if (entries.Count == 0)
            return null;

        return string.Join("\n", entries.Select(x => $"{x.Component}: {x.Damage}"));
    }
}