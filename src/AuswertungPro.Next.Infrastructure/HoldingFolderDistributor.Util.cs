using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using AuswertungPro.Next.Application.Common;
using AuswertungPro.Next.Infrastructure.Media;

namespace AuswertungPro.Next.Infrastructure;

/// <summary>
/// String-/ID-Normalisierungs-Helfer fuer HoldingFolderDistributor.
///
/// Refactor 2026-05-07 (Etappe 2, Charge R4): Holding-ID-Normalisierung,
/// Video-Filename-Helpers und Pfad-Sanitizing in eigenen Util-Partial
/// ausgelagert. Mechanisch — keine Verhaltensaenderung.
/// </summary>
public static partial class HoldingFolderDistributor
{
    private static readonly Regex NodePrefixRegex = new(@"^\d{1,2}\.", RegexOptions.Compiled);

    /// <summary>
    /// Entfernt Node-Praefixe (z.B. "07." in "07.7695-07.7078" → "7695-7078").
    /// Unterscheidet links und rechts vom Bindestrich.
    /// </summary>
    private static string StripNodePrefixes(string holdingKey)
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
    /// Liefert die Holding-ID + ihre umgekehrte Variante (a-b und b-a) als
    /// Lookup-Schluessel — fuer Faelle wo die PDF-Richtung der Sidecar-/Record-
    /// Richtung entgegengesetzt ist.
    /// </summary>
    private static IEnumerable<string> EnumerateHoldingLookupKeys(string haltung)
    {
        var normalized = NormalizeHaltungId(haltung);
        if (!string.IsNullOrWhiteSpace(normalized))
            yield return normalized;

        var reversed = ReverseHoldingId(normalized);
        if (!string.IsNullOrWhiteSpace(reversed)
            && !string.Equals(reversed, normalized, StringComparison.OrdinalIgnoreCase))
            yield return reversed;
    }

    /// <summary>Vertauscht die zwei Teile einer Holding-ID (a-b → b-a).</summary>
    private static string ReverseHoldingId(string? haltung)
    {
        if (string.IsNullOrWhiteSpace(haltung))
            return string.Empty;

        var parts = haltung.Split('-', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries);
        if (parts.Length != 2)
            return string.Empty;

        return $"{parts[1]}-{parts[0]}";
    }

    /// <summary>
    /// Normalisiert einen IBAK-Photo-Token (z.B. "001_002_0000123_A")
    /// auf "1_2_123_A" — fuer Vergleich ueber Schreibweisen hinweg.
    /// </summary>
    private static string? NormalizePhotoToken(string? token)
    {
        if (string.IsNullOrWhiteSpace(token))
            return null;

        var m = Regex.Match(token, @"(?<a>\d{1,5})_(?<b>\d{1,5})_(?<c>\d{1,7})_(?<d>[A-Za-z])");
        if (!m.Success)
            return null;

        static string TrimLeadingZeros(string value)
        {
            var trimmed = value.TrimStart('0');
            return string.IsNullOrEmpty(trimmed) ? "0" : trimmed;
        }

        var a = TrimLeadingZeros(m.Groups["a"].Value);
        var b = TrimLeadingZeros(m.Groups["b"].Value);
        var c = TrimLeadingZeros(m.Groups["c"].Value);
        var d = char.ToUpperInvariant(m.Groups["d"].Value[0]);
        return $"{a}_{b}_{c}_{d}";
    }

    private static IReadOnlyList<string> Tokenize(string line)
        => line.Split(new[] { ' ', '\t', ';', ',', ':' }, StringSplitOptions.RemoveEmptyEntries)
            .Select(t => t.Trim()).ToList();

    private static bool HasVideoExtension(string token)
    {
        var normalized = NormalizeVideoFileName(token);
        return MediaFileTypes.HasVideoExtension(normalized);
    }

    private static bool HasImageExtension(string token)
    {
        var normalized = NormalizeVideoFileName(token);
        return MediaFileTypes.HasImageExtension(normalized);
    }

    /// <summary>
    /// Stellt sicher, dass nur der Dateiname (ohne Pfad) und ohne Trailing-
    /// Punctuation zurueckgegeben wird — damit Match-Vergleiche stabil sind.
    /// </summary>
    private static string? NormalizeVideoFileName(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
            return null;

        var candidate = value.Trim().Trim('"', '\'');
        candidate = candidate.TrimEnd('.', ',', ';', ':', ')', ']', '}', '>');
        if (string.IsNullOrWhiteSpace(candidate))
            return null;

        candidate = candidate.Replace('\\', '/');
        var fileName = Path.GetFileName(candidate).Trim();
        if (string.IsNullOrWhiteSpace(fileName))
            return null;

        return fileName.Trim('"', '\'');
    }

    /// <summary>Delegiert an die zentrale Pfad-Sanitizing-Logik.</summary>
    private static string SanitizePathSegment(string value)
        => ProjectPathResolver.SanitizePathSegment(value);

    /// <summary>
    /// Liest den Haltungsnamen aus dem KIAS/IBAK-PDF-Dateinamen.
    /// Delegiert an die zentrale KIAS-Pattern-Logik.
    /// </summary>
    private static string? HoldingFromKiasFilename(string? pdfPath)
        => Import.Ibak.KiasExportPattern.HoldingFromKiasFilename(pdfPath);

    /// <summary>
    /// Bringt eine Holding-ID auf das Standard-Format "X-Y" mit genau
    /// einem Bindestrich. Liefert "UNKNOWN" wenn der Wert leer ist.
    /// </summary>
    private static string NormalizeHaltungId(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
            return "UNKNOWN";

        var text = NormalizeText(value).Trim();
        // Extract pair pattern: XXXXX-XXXXX or XX.XXXX-XX.XXXX
        var pairRx = new Regex(@"((?:\d{2,}\.\d{2,}|\d{4,})\s*[-]\s*(?:\d{2,}\.\d{2,}|\d{4,}))");
        var m = pairRx.Match(text);
        if (m.Success)
        {
            var normalized = m.Groups[1].Value.Replace(" ", "").Replace("/", "-");
            // Ensure exactly one dash
            normalized = Regex.Replace(normalized, @"\s*-+\s*", "-");
            return normalized;
        }

        return text;
    }

    /// <summary>Lowercase-Alphanumerisch fuer Lookup-Keys.</summary>
    private static string NormalizeKey(string value)
    {
        if (string.IsNullOrWhiteSpace(value)) return "";
        var sb = new StringBuilder(value.Length);
        foreach (var ch in value)
        {
            if (char.IsLetterOrDigit(ch))
                sb.Append(char.ToLowerInvariant(ch));
        }
        return sb.ToString();
    }

    /// <summary>
    /// Pattern-Validierung fuer Holding-IDs: "XXXXX-YYYYY" oder
    /// "XX.XXXX-YY.YYYY". Lehnt OCR-Glue-Artefakte wie "04.201423022-215987"
    /// (Datumsfragment + Id) ab.
    /// </summary>
    private static bool IsValidHaltungId(string? value)
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

        // Reject common OCR glue artifacts such as "04.201423022-215987" (date fragment + id).
        foreach (var part in parts)
        {
            if (Regex.IsMatch(part, @"^\d{2}\.20\d{2}\d+$"))
                return false;
        }

        return true;
    }
}
