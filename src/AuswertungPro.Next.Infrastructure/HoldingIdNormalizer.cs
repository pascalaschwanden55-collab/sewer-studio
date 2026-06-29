using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using AuswertungPro.Next.Infrastructure.Import.Pdf;

namespace AuswertungPro.Next.Infrastructure;

/// <summary>
/// Statische Helfer zur Normalisierung und Validierung von Haltungs-IDs.
/// Extrahiert aus HoldingFolderDistributor.TextUtils und VideoMatching – verhaltensneutral.
/// </summary>
internal static class HoldingIdNormalizer
{
    /// <summary>
    /// Entfernt XX. Praefixe (1-2 Ziffern + Punkt) von beiden Seiten eines Haltungsnamens.
    /// Z.B. "07.7695-07.7078" → "7695-7078"
    /// </summary>
    private static readonly Regex NodePrefixRegex = new(@"^\d{1,2}\.", RegexOptions.Compiled);

    /// <summary>
    /// Normalisiert eine Haltungs-ID auf kanonische Form (z.B. "23022-21598").
    /// Gibt "UNKNOWN" zurueck wenn der Wert leer oder nicht parsbar ist.
    /// </summary>
    internal static string NormalizeHaltungId(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
            return "UNKNOWN";

        var text = HoldingTextNormalizer.NormalizeText(value).Trim();
        // Paar-Muster extrahieren: XXXXX-XXXXX oder XX.XXXX-XX.XXXX
        var pairRx = new Regex(@"((?:\d{2,}\.\d{2,}|\d{4,})\s*[-]\s*(?:\d{2,}\.\d{2,}|\d{4,}))");
        var m = pairRx.Match(text);
        if (m.Success)
        {
            var normalized = m.Groups[1].Value.Replace(" ", "").Replace("/", "-");
            // Exakt einen Bindestrich sicherstellen
            normalized = Regex.Replace(normalized, @"\s*-+\s*", "-");
            return normalized;
        }

        return text;
    }

    /// <summary>
    /// Prueft ob der Wert eine plausible Haltungs-ID ist.
    /// Delegiert an HoldingIdPlausibility fuer die Plausibilitaetspruefung.
    /// </summary>
    internal static bool IsValidHaltungId(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
            return false;

        var normalized = value.Trim();
        var rx = new Regex(@"^(?:\d{2,}\.\d{2,}|\d{4,})\s*-\s*(?:\d{2,}\.\d{2,}|\d{4,})$");
        if (!rx.IsMatch(normalized))
            return false;

        var parts = normalized.Split('-', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries);
        if (parts.Length != 2)
            return false;

        // Ablehnen bekannter OCR-Kleberartefakte wie "04.201423022-215987" (Datumsfragment + ID).
        foreach (var part in parts)
        {
            if (Regex.IsMatch(part, @"^\d{2}\.20\d{2}\d+$"))
                return false;
        }

        return HoldingIdPlausibility.IsLikelyHoldingId(normalized);
    }

    /// <summary>
    /// Kehrt eine Haltungs-ID um (Anfangs- und Endknoten tauschen).
    /// Z.B. "23022-21598" → "21598-23022"
    /// </summary>
    internal static string ReverseHoldingId(string? haltung)
    {
        if (string.IsNullOrWhiteSpace(haltung))
            return string.Empty;

        var parts = haltung.Split('-', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries);
        if (parts.Length != 2)
            return string.Empty;

        return $"{parts[1]}-{parts[0]}";
    }

    /// <summary>
    /// Entfernt XX. Praefixe (1-2 Ziffern + Punkt) von beiden Seiten eines Haltungsnamens.
    /// Z.B. "07.7695-07.7078" → "7695-7078"
    /// </summary>
    internal static string StripNodePrefixes(string holdingKey)
    {
        var dashIdx = holdingKey.IndexOf('-');
        if (dashIdx < 0)
            return NodePrefixRegex.Replace(holdingKey, "");

        var left = holdingKey[..dashIdx];
        var right = holdingKey[(dashIdx + 1)..];
        left = NodePrefixRegex.Replace(left, "");
        right = NodePrefixRegex.Replace(right, "");
        return $"{left}-{right}";
    }

    /// <summary>
    /// Liefert alle Lookup-Schluessel fuer eine Haltung (normalisiert + umgekehrt).
    /// </summary>
    internal static IEnumerable<string> EnumerateHoldingLookupKeys(string haltung)
    {
        var normalized = NormalizeHaltungId(haltung);
        if (!string.IsNullOrWhiteSpace(normalized))
            yield return normalized;

        var reversed = ReverseHoldingId(normalized);
        if (!string.IsNullOrWhiteSpace(reversed)
            && !string.Equals(reversed, normalized, StringComparison.OrdinalIgnoreCase))
            yield return reversed;
    }
}
