using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;
using AuswertungPro.Next.Infrastructure.Import.Common;
using AuswertungPro.Next.Infrastructure.Media;

namespace AuswertungPro.Next.Infrastructure.Import.Xtf;

/// <summary>
/// Rein statische, zustandslose Hilfsklasse fuer M150/WinCan-Import-Logik.
/// Enthaelt alle Normalisierungs- und Erkennungsmethoden aus M150MdbImportHelper,
/// die kein IO, keine Dokumentobjekte und keine externen Abhaengigkeiten benoetigen.
/// </summary>
internal static class M150ValueExtractor
{
    private static readonly Regex HoldingRx = new(
        @"(?<!\d)((?:\d{3,}|\d{1,3}(?:\.\d+)+)\s*[-/]\s*(?:\d{3,}|\d{1,3}(?:\.\d+)+))(?!\d)",
        RegexOptions.Compiled);

    private static readonly Regex PointRx = new(
        @"^[A-Za-z0-9][A-Za-z0-9._-]*$",
        RegexOptions.Compiled);

    private static readonly Regex DateRx = new(
        @"(\d{2}[./-]\d{2}[./-]\d{2,4}|\d{4}-\d{2}-\d{2})",
        RegexOptions.Compiled);

    // -----------------------------------------------------------------------
    // Richtung
    // -----------------------------------------------------------------------

    /// <summary>
    /// Normalisiert einen rohen Richtungstext auf "oben -> unten", "unten -> oben"
    /// oder den Originaltext, falls kein bekanntes Muster erkannt wird.
    /// </summary>
    public static string NormalizeDirection(string? value)
    {
        var v = (value ?? "").Trim();
        if (string.IsNullOrWhiteSpace(v))
            return string.Empty;

        var lower = v.ToLowerInvariant();

        // Spezifische Muster zuerst pruefen (z.B. "von unten nach oben")
        if (lower.Contains("unten") && lower.Contains("oben") && lower.IndexOf("unten") < lower.IndexOf("oben"))
            return "unten -> oben";
        if (lower.Contains("oben") && lower.Contains("unten") && lower.IndexOf("oben") < lower.IndexOf("unten"))
            return "oben -> unten";

        // DWA-M 150 Codes
        if (lower is "d" or "down" or "1")
            return "oben -> unten";
        if (lower is "u" or "up" or "2")
            return "unten -> oben";

        // Einfache Schluesselwoerter
        if (lower.Contains("oben") || lower.StartsWith("von"))
            return "oben -> unten";
        if (lower.Contains("unten") || lower.StartsWith("nach"))
            return "unten -> oben";

        return v;
    }

    /// <summary>
    /// Prueft, ob die WinCan-Richtungsangabe eine Umkehrung der Schacht-Reihenfolge erfordert
    /// (d.h. Upstream: von unten nach oben).
    /// </summary>
    public static bool ShouldReverseWinCanDirection(string? raw)
    {
        var dir = (raw ?? "").Trim();
        if (string.IsNullOrWhiteSpace(dir))
            return false;

        return dir.Equals("U", StringComparison.OrdinalIgnoreCase)
               || dir.Equals("UP", StringComparison.OrdinalIgnoreCase)
               || dir.Equals("UPSTREAM", StringComparison.OrdinalIgnoreCase)
               || dir.Equals("2", StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>
    /// Normalisiert einen WinCan-Richtungscode auf lesbaren deutschen Text.
    /// </summary>
    public static string NormalizeWinCanDirection(string? raw)
    {
        var dir = (raw ?? "").Trim();
        if (string.IsNullOrWhiteSpace(dir))
            return string.Empty;

        if (ShouldReverseWinCanDirection(dir))
            return "unten -> oben";

        if (dir.Equals("D", StringComparison.OrdinalIgnoreCase)
            || dir.Equals("DOWN", StringComparison.OrdinalIgnoreCase)
            || dir.Equals("DOWNSTREAM", StringComparison.OrdinalIgnoreCase)
            || dir.Equals("1", StringComparison.OrdinalIgnoreCase))
            return "oben -> unten";

        return NormalizeDirection(dir);
    }

    // -----------------------------------------------------------------------
    // Schacht- und Haltungs-IDs
    // -----------------------------------------------------------------------

    /// <summary>
    /// Baut eine normalisierte Haltungs-ID aus WinCan-Startknoten, Endknoten und
    /// Inspektionsrichtung. Gibt leer zurueck, wenn kein gueltiges Ergebnis moeglich ist.
    /// </summary>
    public static string BuildHoldingFromWinCanSection(string startRaw, string endRaw, string dirRaw)
    {
        var start = ExtractPointId(startRaw);
        var end = ExtractPointId(endRaw);
        if (!IsPointId(start) || !IsPointId(end))
            return string.Empty;

        return ShouldReverseWinCanDirection(dirRaw)
            ? HoldingKeyNormalizer.Normalize($"{end}-{start}")
            : HoldingKeyNormalizer.Normalize($"{start}-{end}");
    }

    /// <summary>
    /// Extrahiert eine Punkt-ID aus einem rohen Knotennamen.
    /// Bei GUID-artigen oder zusammengesetzten Texten wird der numerische Teilausdruck extrahiert.
    /// </summary>
    public static string ExtractPointId(string? raw)
    {
        var value = (raw ?? "").Trim();
        if (string.IsNullOrWhiteSpace(value))
            return string.Empty;

        if (IsPointId(value))
            return value;

        var m = Regex.Match(value, @"(\d{2,}(?:\.\d+)+|\d{3,})");
        return m.Success ? m.Groups[1].Value : string.Empty;
    }

    /// <summary>
    /// Prueft, ob ein Wert als Haltungs-ID (Format "Knoten1-Knoten2") erkannt wird.
    /// </summary>
    public static bool IsHoldingId(string? value)
        => !string.IsNullOrWhiteSpace(value) && HoldingRx.IsMatch(value.Trim());

    /// <summary>
    /// Prueft, ob ein Wert als Punkt-ID (alphanumerisch mit Punkt/Bindestrich) erkannt wird.
    /// </summary>
    public static bool IsPointId(string? value)
        => !string.IsNullOrWhiteSpace(value) && PointRx.IsMatch(value.Trim());

    // -----------------------------------------------------------------------
    // Datum
    // -----------------------------------------------------------------------

    /// <summary>
    /// Versucht, einen Datums-String zu normalisieren. Gibt null zurueck, wenn kein
    /// gueltiges Datum erkannt wird.
    /// </summary>
    public static string? TryNormalizeDate(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
            return null;

        var text = value.Trim();
        var m = DateRx.Match(text);
        if (m.Success)
            text = m.Groups[1].Value;

        var formats = new[] { "dd.MM.yyyy", "dd.MM.yy", "dd/MM/yyyy", "dd/MM/yy", "dd-MM-yyyy", "dd-MM-yy", "yyyy-MM-dd", "yyyyMMdd" };
        if (DateTime.TryParseExact(text, formats, CultureInfo.InvariantCulture, DateTimeStyles.None, out var d))
            return d.ToString("dd.MM.yyyy", CultureInfo.InvariantCulture);

        return null;
    }

    // -----------------------------------------------------------------------
    // Textnormalisierung
    // -----------------------------------------------------------------------

    /// <summary>
    /// Normalisiert einen Zahlenwert-Text: Extrahiert den ersten numerischen Ausdruck
    /// und ersetzt Komma durch Punkt. Gibt leer zurueck, wenn kein Zahl erkannt wird.
    /// </summary>
    public static string NormalizeNumberText(string? value)
    {
        var v = (value ?? "").Trim();
        if (string.IsNullOrWhiteSpace(v))
            return string.Empty;

        var m = Regex.Match(v, @"-?\d+(?:[.,]\d+)?");
        return m.Success ? m.Value.Replace(",", ".") : v;
    }

    /// <summary>
    /// Prueft ob ein Wert wie ein Video-Link aussieht (anhand Dateiendung oder
    /// typischem Zeitstempel-Muster ohne Erweiterung).
    /// </summary>
    public static bool LooksLikeVideoLink(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
            return false;

        var v = value.Trim().Trim('"', '\'');
        var ext = Path.GetExtension(v).ToLowerInvariant();
        if (MediaFileTypes.HasVideoExtension(ext))
            return true;

        // Einige Exporte enthalten keinen Extension, aber das klassische Zeitstempel-Muster.
        return Regex.IsMatch(v, @"^\d+_\d+_\d+_\d{8}_\d{6}$", RegexOptions.CultureInvariant);
    }

    /// <summary>
    /// Sucht in einem Dictionary nach dem ersten Wert, dessen Schluessel einen der Hints enthaelt
    /// und der den Validator besteht.
    /// </summary>
    public static string PickValue(
        Dictionary<string, string> map,
        IEnumerable<string> keyHints,
        Func<string, bool> validator)
    {
        foreach (var hint in keyHints)
        {
            foreach (var kv in map)
            {
                if (!kv.Key.Contains(hint, StringComparison.OrdinalIgnoreCase))
                    continue;

                var raw = kv.Value?.Trim() ?? string.Empty;
                if (validator(raw))
                    return raw;
            }
        }

        return string.Empty;
    }

    /// <summary>
    /// Normalisiert einen XML-Element- oder Feldnamen: nur alphanumerische Zeichen,
    /// Kleinbuchstaben.
    /// </summary>
    public static string NormalizeKey(string? key)
    {
        if (string.IsNullOrWhiteSpace(key))
            return string.Empty;

        var sb = new StringBuilder(key.Length);
        foreach (var ch in key)
        {
            if (char.IsLetterOrDigit(ch))
                sb.Append(char.ToLowerInvariant(ch));
        }
        return sb.ToString();
    }
}
