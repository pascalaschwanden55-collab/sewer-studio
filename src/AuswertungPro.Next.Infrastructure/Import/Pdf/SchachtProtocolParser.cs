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
        "Schacht",
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

        var normalized = NormalizePdfText(text.Replace("\r\n", "\n"));

        string? GetFirst(string pattern)
        {
            var m = Regex.Match(normalized, pattern, RegexOptions.IgnoreCase | RegexOptions.Multiline);
            return m.Success ? m.Groups["v"].Value.Trim() : null;
        }

        var schachtNummer = GetFirst(@"\bNr\.?\s*[:\-]?\s*(?<v>\d{3,})\b")
                            ?? GetFirst(@"\bSchachtnummer\s*[:\-]?\s*(?<v>\d{3,})\b");

        var dateRaw = GetFirst(@"\bDatum\s*[:\-]?\s*(?<v>" + SewerTextPatterns.GermanDateCore + @")\b");
        var datum = NormalizeDate(dateRaw);

        var funktion = GetFirst(@"\bSchachttyp\s+(?<v>[^\n\r]+)")?.Trim()
                       ?? GetFirst(@"\bSchachtfunktion\s+(?<v>[^\n\r]+)")?.Trim();

        var primaryDamages = ParsePrimaryDamagesFromConditionSection(normalized);
        var remarkDamages = ParseRemarkDamageLines(normalized);
        var combinedPrimaryDamages = CombineDamageLines(primaryDamages, remarkDamages);
        var maengelfrei = Regex.IsMatch(normalized, @"\bM\S*ngelfrei\b", RegexOptions.IgnoreCase)
            ? "Maengelfrei"
            : null;
        var effectivePrimaryDamages = !string.IsNullOrWhiteSpace(combinedPrimaryDamages) ? combinedPrimaryDamages : maengelfrei;
        var status = DeriveSchachtStatus(effectivePrimaryDamages, normalized);

        return new LegacyPdfImportService.ParsedSchachtFields(
            SchachtNummer: schachtNummer,
            Datum: datum,
            Funktion: funktion,
            PrimaereSchaeden: effectivePrimaryDamages,
            Bemerkungen: null,
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

        var normalized = NormalizeCheckboxGlyphs(NormalizePdfText(text));
        var lines = normalized.Split('\n');
        var entries = new List<(string Component, string Damage, int EncounterIndex)>();
        var encounterIndex = 0;
        var inConditionSection = false;

        foreach (var rawLine in lines)
        {
            var line = rawLine?.Trim();
            if (string.IsNullOrWhiteSpace(line))
                continue;

            if (IsConditionSectionStart(line!))
            {
                inConditionSection = true;
                continue;
            }

            if (inConditionSection && IsConditionSectionEnd(line!))
                inConditionSection = false;

            if (!TryExtractComponentTail(line!, out var component, out var tail))
                continue;

            if (!inConditionSection && !ContainsDamageMarker(tail))
                continue;

            var damages = inConditionSection
                ? ParseDamageTexts(component, tail, allowFreeText: true)
                : ParseMarkedDamageTexts(component, tail);

            foreach (var damage in damages)
            {
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
    /// Normalisiert typische PDF-Textartefakte, die sonst das Schadensmatching stoeren.
    /// </summary>
    internal static string NormalizePdfText(string text)
    {
        return (text ?? string.Empty)
            .Replace("\r\n", "\n")
            .Replace('\r', '\n')
            .Replace("\uFB01", "fi", StringComparison.Ordinal)
            .Replace("\uFB02", "fl", StringComparison.Ordinal);
    }

    /// <summary>
    /// Versucht einen Bauteil-Namen aus dem Zeilenanfang zu extrahieren und gibt den Rest (tail) zurueck.
    /// </summary>
    internal static bool TryExtractComponentTail(string line, out string component, out string tail)
    {
        foreach (var (alias, canonical) in GetComponentAliases())
        {
            var m = Regex.Match(line, @"^\s*" + Regex.Escape(alias) + @"\b(?<tail>.*)$", RegexOptions.IgnoreCase);
            if (!m.Success)
                continue;

            component = canonical;
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
        if (component.Equals("Schacht", StringComparison.OrdinalIgnoreCase))
            return new[] { "Überdeckt", "Ueberdeckt" };

        if (component.Equals("Schachtdeckel", StringComparison.OrdinalIgnoreCase))
            return new[] { "gerissen", "Riss", "ausgebrochen", "Korrodiert", "korrodiert", "klemmt" };

        if (component.Equals("Deckelrahmen", StringComparison.OrdinalIgnoreCase))
            return new[] { "gerissen", "Riss", "ausgebrochen", "Lose", "lose", "Korrodiert", "korrodiert" };

        if (component.Equals("Schachthals", StringComparison.OrdinalIgnoreCase)
            || component.Equals("Konus", StringComparison.OrdinalIgnoreCase)
            || component.Equals("Schachtrohr", StringComparison.OrdinalIgnoreCase))
            return new[]
            {
                "gerissen",
                "Riss",
                "ausgebrochen",
                "Ausgebrochen",
                "korrodiert",
                "Korrodiert",
                "Infiltration",
                "Fugen mangelhaft verputzt",
                "fugen mangelhaft verputzt",
                "Verkalkungen"
            };

        if (component.Equals("Bankett", StringComparison.OrdinalIgnoreCase)
            || component.Equals("Durchlaufrinne", StringComparison.OrdinalIgnoreCase))
            return new[]
            {
                "gerissen",
                "Riss",
                "ausgebrochen",
                "Ausgebrochen",
                "korrodiert",
                "Korrodiert",
                "Ablagerung",
                "Ablagerungen",
                "Mangelhaft ausgebildet",
                "mangelhaft ausgebildet"
            };

        if (component.Equals("Anschluss", StringComparison.OrdinalIgnoreCase))
            return new[] { "gerissen", "Riss", "ausgebrochen", "Ausgebrochen", "Mangelhaft eingebunden", "mangelhaft eingebunden" };

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

    private static IReadOnlyList<(string Alias, string Canonical)> GetComponentAliases()
        => new List<(string Alias, string Canonical)>
        {
            ("Schacht", "Schacht"),
            ("Schachtdeckel", "Schachtdeckel"),
            ("Deckel", "Schachtdeckel"),
            ("Deckelrahmen", "Deckelrahmen"),
            ("Rahmen", "Deckelrahmen"),
            ("Schachthals", "Schachthals"),
            ("Konus", "Konus"),
            ("Schachtrohr", "Schachtrohr"),
            ("Bankett", "Bankett"),
            ("Durchlaufrinne", "Durchlaufrinne"),
            ("Anschluss", "Anschluss"),
            ("Leiter/Steigeisen", "Leiter/Steigeisen"),
            ("Leiter", "Leiter/Steigeisen"),
            ("Tauchbogen", "Tauchbogen")
        };

    private static bool IsConditionSectionStart(string line)
        => line.Contains("ZUSTAND DER SCHACHTBAUTEILE", StringComparison.OrdinalIgnoreCase)
           || line.Contains("Zustand der Schachtbauteile", StringComparison.OrdinalIgnoreCase)
           || line.Contains("Zustand der Bauteile", StringComparison.OrdinalIgnoreCase);

    private static bool IsConditionSectionEnd(string line)
        => Regex.IsMatch(line, @"^\s*ANSCHL", RegexOptions.IgnoreCase)
           || Regex.IsMatch(line, @"^\s*FOTOS\b", RegexOptions.IgnoreCase)
           || Regex.IsMatch(line, @"^\s*LAGE\b", RegexOptions.IgnoreCase)
           || Regex.IsMatch(line, @"^\s*Seite\s+\d+\b", RegexOptions.IgnoreCase);

    private static bool ContainsDamageMarker(string text)
        => Regex.IsMatch(
            text ?? string.Empty,
            @"(?:●|•|■|☒|☑|✓|✔|✗|✘|\[\s*[xX]\s*\]|\(\s*[xX]\s*\))",
            RegexOptions.IgnoreCase);

    private static IReadOnlyList<string> ParseDamageTexts(string component, string tail, bool allowFreeText)
    {
        if (string.IsNullOrWhiteSpace(tail))
            return Array.Empty<string>();

        var result = new List<string>();
        var normalizedTail = NormalizeDamageSegment(tail);
        if (string.IsNullOrWhiteSpace(normalizedTail))
            return result;

        var segments = SplitDamageSegments(normalizedTail);
        foreach (var segmentRaw in segments)
        {
            var segment = NormalizeDamageSegment(segmentRaw);
            if (string.IsNullOrWhiteSpace(segment) || IsNonDamageSegment(segment))
                continue;

            var known = ExtractKnownDamageMatches(component, segment);
            if (known.Count > 0)
            {
                result.AddRange(known);
                continue;
            }

            if (allowFreeText)
                result.Add(segment);
        }

        return result
            .Where(x => !string.IsNullOrWhiteSpace(x))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    private static IReadOnlyList<string> ParseMarkedDamageTexts(string component, string tail)
    {
        var result = new List<string>();
        foreach (var damage in GetDamageCandidatesForComponent(component))
        {
            if (IsMarkedDamage(tail, damage))
                result.Add(damage);
        }

        return result.Distinct(StringComparer.OrdinalIgnoreCase).ToList();
    }

    private static IReadOnlyList<string> SplitDamageSegments(string text)
    {
        return Regex
            .Split(text, @"(?:●|•|■|☒|☑|✓|✔|✗|✘|\[\s*[xX]\s*\]|\(\s*[xX]\s*\)|;)")
            .Select(x => x.Trim())
            .Where(x => x.Length > 0)
            .ToList();
    }

    private static string NormalizeDamageSegment(string value)
    {
        var text = NormalizePdfText(value ?? string.Empty).Trim();
        text = Regex.Replace(text, @"^\s*Bemerkung(?:en)?\s*[:\-]?\s*", "", RegexOptions.IgnoreCase).Trim();
        text = text.Trim(' ', '\t', ':', '-', ',', '.', ';');
        return text;
    }

    private static bool IsNonDamageSegment(string value)
    {
        var normalized = NormalizeForComparison(value);
        return normalized.Length == 0
               || normalized == "-"
               || normalized == "in ordnung"
               || normalized == "nicht notwendig"
               || normalized == "vorhanden"
               || normalized == "keine"
               || normalized == "maengelfrei"
               || normalized == "ohne auffaelligkeiten";
    }

    private static IReadOnlyList<string> ExtractKnownDamageMatches(string component, string segment)
    {
        var matches = new List<(int Index, string Text)>();
        foreach (var candidate in GetDamageCandidatesForComponent(component)
                     .OrderByDescending(x => x.Length))
        {
            if (string.IsNullOrWhiteSpace(candidate))
                continue;

            var m = Regex.Match(
                segment,
                @"(?<![\p{L}\p{N}])" + Regex.Escape(candidate) + @"(?![\p{L}\p{N}])",
                RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);
            if (!m.Success)
                continue;

            matches.Add((m.Index, m.Value.Trim()));
        }

        return matches
            .OrderBy(x => x.Index)
            .Select(x => x.Text)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    private static string NormalizeForComparison(string value)
        => NormalizePdfText(value ?? string.Empty)
            .Trim()
            .ToLowerInvariant()
            .Replace("ä", "ae", StringComparison.Ordinal)
            .Replace("ö", "oe", StringComparison.Ordinal)
            .Replace("ü", "ue", StringComparison.Ordinal)
            .Replace("ß", "ss", StringComparison.Ordinal);

    private static IReadOnlyList<string> ParseRemarkDamageLines(string text)
    {
        if (string.IsNullOrWhiteSpace(text))
            return Array.Empty<string>();

        var result = new List<string>();
        foreach (var rawLine in NormalizePdfText(text).Split('\n'))
        {
            var line = rawLine.Trim();
            if (line.Length == 0)
                continue;

            var m = Regex.Match(line, @"^\s*Bemerkung(?:en)?\s*[:\-]?\s*(?<v>.+)$", RegexOptions.IgnoreCase);
            if (!m.Success)
                continue;

            var value = NormalizeDamageSegment(m.Groups["v"].Value);
            if (string.IsNullOrWhiteSpace(value) || IsNonDamageSegment(value))
                continue;

            result.Add($"Bemerkungen: {value}");
        }

        return result.Distinct(StringComparer.OrdinalIgnoreCase).ToList();
    }

    private static string? CombineDamageLines(string? primaryDamages, IReadOnlyList<string> remarkDamages)
    {
        var lines = new List<string>();
        if (!string.IsNullOrWhiteSpace(primaryDamages))
        {
            lines.AddRange(primaryDamages.Split(
                '\n',
                StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries));
        }

        lines.AddRange(remarkDamages);
        return lines.Count == 0
            ? null
            : string.Join("\n", lines.Distinct(StringComparer.OrdinalIgnoreCase));
    }

    private static string? ParsePrimaryDamagesFromConditionSection(string text)
    {
        var entries = ParseSchachtDamageEntries(text);
        if (entries.Count == 0)
            return null;

        return string.Join("\n", entries.Select(x => $"{x.Component}: {x.Damage}"));
    }
}
