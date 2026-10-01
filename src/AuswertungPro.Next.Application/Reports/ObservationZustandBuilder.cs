using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text.RegularExpressions;

using AuswertungPro.Next.Application.Protocol;
using AuswertungPro.Next.Domain.Protocol;

namespace AuswertungPro.Next.Application.Reports;

/// <summary>
/// Baut den Zustandstext einer Beobachtung als Klartext: menschliche Beschreibung plus
/// über den Code-Katalog benannte, mit Einheit versehene Quantifizierer
/// (z.B. "Bogen nach rechts, Winkel = 45°" statt rohem "Q1=45").
///
/// Ohne Katalog oder für unbekannte Codes ist das Ergebnis verhaltensneutral identisch zu
/// <see cref="ProtocolZustandText.BuildObservationZustandTextLong"/> (Rohverhalten bleibt).
/// </summary>
public static class ObservationZustandBuilder
{
    public static string Build(ProtocolEntry entry, ICodeCatalogProvider? catalog)
    {
        var parts = new List<string>();
        var definition = ResolveDefinition(entry, catalog);

        var human = ProtocolZustandText.NormalizeZustandDescription(entry.Beschreibung, entry.Code);
        if (definition is not null && string.IsNullOrWhiteSpace(human))
        {
            var parameters = entry.CodeMeta?.Parameters;
            human = parameters is null
                ? entry.CodeMeta?.Notes?.Trim()
                : ProtocolPdfObservationText.GetParam(parameters, "vsa.anmerkung")
                  ?? entry.CodeMeta?.Notes?.Trim();
        }
        AddTitleAndDescription(parts, definition?.Title, human);

        parts.AddRange(BuildCatalogQuantifiers(entry, definition, human));

        if (parts.Count == 0)
            return ProtocolZustandText.BuildObservationZustandTextLong(entry);

        return string.Join(", ", parts);
    }

    private static CodeDefinition? ResolveDefinition(ProtocolEntry entry, ICodeCatalogProvider? catalog)
    {
        if (catalog is null || string.IsNullOrWhiteSpace(entry.Code))
            return null;

        return catalog.TryGet(entry.Code, out var definition)
            ? definition
            : null;
    }

    private static void AddTitleAndDescription(List<string> parts, string? title, string? description)
    {
        var cleanTitle = title?.Trim() ?? string.Empty;
        var cleanDescription = description?.Trim() ?? string.Empty;

        if (cleanTitle.Length == 0)
        {
            if (cleanDescription.Length > 0)
                parts.Add(cleanDescription);
            return;
        }

        if (cleanDescription.Length == 0)
        {
            parts.Add(cleanTitle);
            return;
        }

        var comparableTitle = NormalizeComparableText(cleanTitle);
        var comparableDescription = NormalizeComparableText(cleanDescription);

        if (string.Equals(comparableDescription, comparableTitle, StringComparison.OrdinalIgnoreCase))
        {
            // Bei gleicher Aussage bleibt die Schreibweise des Katalogs massgebend.
            parts.Add(cleanTitle);
            return;
        }

        if (IsPhrasePrefix(comparableDescription, comparableTitle))
        {
            // Die Beschreibung erweitert den Katalogtitel bereits.
            parts.Add(cleanDescription);
            return;
        }

        if (IsPhrasePrefix(comparableTitle, comparableDescription))
        {
            // Eine verkuerzte Beschreibung wiederholt nur den Anfang des Katalogtitels.
            parts.Add(cleanTitle);
            return;
        }

        parts.Add(cleanTitle);
        parts.Add(cleanDescription);
    }

    private static string NormalizeComparableText(string value)
        => string.Join(' ', value
            .Trim(' ', ',', ';', ':', '.', '-', '\u2013', '\u2014')
            .Split([' ', '\t', '\r', '\n'], StringSplitOptions.RemoveEmptyEntries));

    private static bool IsPhrasePrefix(string value, string prefix)
    {
        if (prefix.Length == 0 || !value.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
            return false;

        return value.Length == prefix.Length
               || char.IsWhiteSpace(value[prefix.Length])
               || char.IsPunctuation(value[prefix.Length]);
    }

    private static IEnumerable<string> BuildCatalogQuantifiers(
        ProtocolEntry entry,
        CodeDefinition? definition,
        string? humanDescription)
    {
        var result = new List<string>();
        var parameters = entry.CodeMeta?.Parameters;
        if (definition is null || parameters is null || parameters.Count == 0)
            return result;

        var consumed = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var emittedValues = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        // Code-spezifische Parameter mit Namen + Einheit ("Winkel = 45°", "Breite = 10mm").
        foreach (var p in definition.Parameters)
        {
            // Uhrlagen werden unten einmal einheitlich als "Lage ... Uhr" formatiert.
            // SchadenlageAnfang/-Ende duerfen deshalb nicht zusaetzlich als normale
            // Katalogparameter erscheinen.
            if (IsClockParameter(p))
                continue;

            var key = string.IsNullOrWhiteSpace(p.DataKey) ? p.Name : p.DataKey!;
            var value = ProtocolDescriptionBuilder.GetFirstParameter(parameters, key, p.Name);
            if (string.IsNullOrWhiteSpace(value))
                continue;

            consumed.Add(key);
            consumed.Add(p.Name);
            emittedValues.Add(value.Trim());

            var label = string.IsNullOrWhiteSpace(p.Name) ? key : p.Name;
            var unit = p.Unit ?? string.Empty;
            result.Add($"{label} = {value}{unit}");
        }

        // Uhrlage.
        var uhrVon = GetFirstValidClock(
            parameters,
            "vsa.uhr.von",
            "ClockPos1",
            "Uhr_von",
            "SchadenlageAnfang",
            "Uhrlage Anfang");
        var uhrBis = GetFirstValidClock(
            parameters,
            "vsa.uhr.bis",
            "ClockPos2",
            "Uhr_bis",
            "SchadenlageEnde",
            "Uhrlage Ende");
        if (SupportsClockPosition(entry, definition)
            && !DescriptionContainsClock(humanDescription, uhrVon, uhrBis))
        {
            if (!string.IsNullOrWhiteSpace(uhrVon) && !string.IsNullOrWhiteSpace(uhrBis)
                && !string.Equals(uhrVon, uhrBis, StringComparison.OrdinalIgnoreCase))
                result.Add($"Lage {uhrVon}–{uhrBis} Uhr");
            else if (!string.IsNullOrWhiteSpace(uhrVon))
                result.Add($"Lage {uhrVon} Uhr");
        }

        // Rohe Quantifizierung nur, wenn kein benannter Parameter sie bereits abgedeckt hat
        // (weder über den Schlüssel noch über denselben Wert -> keine Doppelzählung).
        if (!ConsumedAny(consumed, "Quantifizierung1", "vsa.q1", "Q1"))
        {
            var q1 = ProtocolDescriptionBuilder.GetFirstParameter(parameters, "Quantifizierung1", "vsa.q1", "Q1");
            if (!string.IsNullOrWhiteSpace(q1) && !emittedValues.Contains(q1.Trim()))
                result.Add($"Q1 = {q1}");
        }
        if (!ConsumedAny(consumed, "Quantifizierung2", "vsa.q2", "Q2"))
        {
            var q2 = ProtocolDescriptionBuilder.GetFirstParameter(parameters, "Quantifizierung2", "vsa.q2", "Q2");
            if (!string.IsNullOrWhiteSpace(q2) && !emittedValues.Contains(q2.Trim()))
                result.Add($"Q2 = {q2}");
        }

        return result;
    }

    private static bool IsClockParameter(CodeParameter parameter)
        => string.Equals(parameter.Type, "clock", StringComparison.OrdinalIgnoreCase)
           || IsAny(parameter.DataKey,
               "vsa.uhr.von", "vsa.uhr.bis", "ClockPos1", "ClockPos2",
               "Uhr_von", "Uhr_bis", "SchadenlageAnfang", "SchadenlageEnde")
           || IsAny(parameter.Name, "Uhrlage Anfang", "Uhrlage Ende");

    private static bool SupportsClockPosition(ProtocolEntry entry, CodeDefinition definition)
        => ProtocolTextHelpers.IsLateralConnection(entry)
           || definition.Parameters.Any(IsClockParameter);

    private static string? GetFirstValidClock(
        IReadOnlyDictionary<string, string> parameters,
        params string[] aliases)
    {
        foreach (var alias in aliases)
        {
            var raw = ProtocolDescriptionBuilder.GetFirstParameter(parameters, alias);
            if (ProtocolTextHelpers.TryParseClockHourValue(raw, out var hour))
                return hour.ToString(CultureInfo.InvariantCulture);
        }

        return null;
    }

    private static bool IsAny(string? value, params string[] candidates)
        => !string.IsNullOrWhiteSpace(value)
           && candidates.Any(candidate => string.Equals(value, candidate, StringComparison.OrdinalIgnoreCase));

    private static bool DescriptionContainsClock(
        string? description,
        string? clockFrom,
        string? clockTo)
    {
        if (string.IsNullOrWhiteSpace(description)
            || !int.TryParse(clockFrom, NumberStyles.Integer, CultureInfo.InvariantCulture, out var from))
        {
            return false;
        }

        if (int.TryParse(clockTo, NumberStyles.Integer, CultureInfo.InvariantCulture, out var to)
            && to != from)
        {
            var rangePattern = $@"\bvon\s+0?{from}\s*(?:Uhr\s*)?(?:bis|[-–])\s*0?{to}\s*Uhr\b";
            return Regex.IsMatch(description, rangePattern, RegexOptions.IgnoreCase);
        }

        var singlePattern = $@"\b0?{from}\s*(?::00)?\s*Uhr\b";
        return Regex.IsMatch(description, singlePattern, RegexOptions.IgnoreCase);
    }

    private static bool ConsumedAny(HashSet<string> consumed, params string[] keys)
        => keys.Any(consumed.Contains);
}
