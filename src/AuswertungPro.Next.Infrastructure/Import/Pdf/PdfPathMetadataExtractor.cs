using System.Globalization;
using System.Text.RegularExpressions;

namespace AuswertungPro.Next.Infrastructure.Import.Pdf;

/// <summary>
/// Extrahiert Haltungs-ID und Datum aus PDF-Dateipfaden.
/// Kein IO (kein Lesen von Dateiinhalten), nur Pfad-String-Analyse.
/// </summary>
internal static class PdfPathMetadataExtractor
{
    /// <summary>
    /// Versucht eine Haltungs-ID aus dem Dateinamen (ohne Erweiterung) zu extrahieren.
    /// </summary>
    internal static string? TryExtractHoldingIdFromFileName(string pdfPath)
    {
        var name = Path.GetFileNameWithoutExtension(pdfPath) ?? "";
        return TryExtractHoldingIdFromName(name);
    }

    /// <summary>
    /// Versucht eine Haltungs-ID aus dem Dateinamen oder einem uebergeordneten Verzeichnisnamen zu extrahieren.
    /// </summary>
    internal static string? TryExtractHoldingIdFromPath(string pdfPath)
    {
        var fromFileName = TryExtractHoldingIdFromFileName(pdfPath);
        if (HoldingIdPlausibility.IsLikelyHoldingId(fromFileName))
            return HoldingIdPlausibility.Normalize(fromFileName!);

        var dir = Path.GetDirectoryName(pdfPath);
        while (!string.IsNullOrWhiteSpace(dir))
        {
            var segment = Path.GetFileName(dir.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar));
            var fromSegment = TryExtractHoldingIdFromName(segment);
            if (HoldingIdPlausibility.IsLikelyHoldingId(fromSegment))
                return HoldingIdPlausibility.Normalize(fromSegment!);

            var parent = Directory.GetParent(dir);
            if (parent is null || string.Equals(parent.FullName, dir, StringComparison.OrdinalIgnoreCase))
                break;
            dir = parent.FullName;
        }

        return null;
    }

    /// <summary>
    /// Versucht ein Datum aus dem Dateinamen (ohne Erweiterung) zu extrahieren.
    /// </summary>
    internal static DateTime? TryExtractDateFromFileName(string pdfPath)
    {
        var name = Path.GetFileNameWithoutExtension(pdfPath) ?? "";
        return TryExtractDateFromName(name);
    }

    /// <summary>
    /// Versucht ein Datum aus dem Dateinamen oder einem uebergeordneten Verzeichnisnamen zu extrahieren.
    /// </summary>
    internal static DateTime? TryExtractDateFromPath(string pdfPath)
    {
        var fromFileName = TryExtractDateFromFileName(pdfPath);
        if (fromFileName is not null)
            return fromFileName;

        var dir = Path.GetDirectoryName(pdfPath);
        while (!string.IsNullOrWhiteSpace(dir))
        {
            var segment = Path.GetFileName(dir.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar));
            var fromSegment = TryExtractDateFromName(segment);
            if (fromSegment is not null)
                return fromSegment;

            var parent = Directory.GetParent(dir);
            if (parent is null || string.Equals(parent.FullName, dir, StringComparison.OrdinalIgnoreCase))
                break;
            dir = parent.FullName;
        }

        return null;
    }

    /// <summary>
    /// Kern-Logik fuer ApplyPathDateFallback: setzt Datum_Jahr aus Pfad, wenn kein Datum vorhanden.
    /// Nur aufrufen wenn der Schluessels bereits plausibel ist.
    /// </summary>
    internal static void ApplyPathDateFallbackCore(Dictionary<string, string> fields, string key, string pdfPath)
    {
        if (!HoldingIdPlausibility.IsLikelyHoldingId(key)
            || !string.IsNullOrWhiteSpace(fields.GetValueOrDefault("Datum_Jahr")))
            return;

        var pathDate = TryExtractDateFromPath(pdfPath);
        if (pathDate is null)
            return;

        var pathHolding = TryExtractHoldingIdFromPath(pdfPath);
        if (HoldingIdPlausibility.IsLikelyHoldingId(pathHolding)
            && !string.Equals(
                HoldingIdPlausibility.Normalize(pathHolding!),
                HoldingIdPlausibility.Normalize(key),
                StringComparison.OrdinalIgnoreCase))
        {
            return;
        }

        fields["Datum_Jahr"] = pathDate.Value.ToString("dd.MM.yyyy", CultureInfo.InvariantCulture);
    }

    // --- Interne Hilfsmethoden ---

    /// <summary>
    /// Versucht eine Haltungs-ID aus einem Namensstring (ohne Pfad/Erweiterung) zu extrahieren.
    /// </summary>
    internal static string? TryExtractHoldingIdFromName(string name)
    {
        if (string.IsNullOrWhiteSpace(name))
            return null;

        var dashPair = Regex.Match(name, @"(?<!\d)(\d[\d\.]*-\d[\d\.]*)(?!\d)");
        if (dashPair.Success)
            return HoldingIdPlausibility.Normalize(dashPair.Groups[1].Value);

        // z.B. 32953_1225 -> 32953-1225; muss nach dash-pair laufen
        // damit datierte Namen wie 20250630_29120-03.27666 die echte Haltungs-ID behalten.
        var underscorePair = Regex.Match(name, @"(?<!\d)(\d{3,})_(\d{3,})(?!\d)");
        if (underscorePair.Success)
            return $"{underscorePair.Groups[1].Value}-{underscorePair.Groups[2].Value}";

        return null;
    }

    /// <summary>
    /// Versucht ein Datum aus einem Namensstring zu extrahieren (yyyyMMdd oder dd.MM.yyyy Varianten).
    /// </summary>
    internal static DateTime? TryExtractDateFromName(string name)
    {
        if (string.IsNullOrWhiteSpace(name))
            return null;

        var ymd = Regex.Match(name, @"(?<!\d)(\d{4})(\d{2})(\d{2})(?!\d)");
        if (ymd.Success && DateTime.TryParseExact(ymd.Value, "yyyyMMdd", CultureInfo.InvariantCulture, DateTimeStyles.None, out var dateYmd))
            return dateYmd;

        var dmy = Regex.Match(name, @"(?<!\d)(\d{2})[._-](\d{2})[._-](\d{2,4})(?!\d)");
        if (dmy.Success)
        {
            var candidate = $"{dmy.Groups[1].Value}.{dmy.Groups[2].Value}.{dmy.Groups[3].Value}";
            var formats = new[] { "dd.MM.yyyy", "dd.MM.yy" };
            if (DateTime.TryParseExact(candidate, formats, CultureInfo.InvariantCulture, DateTimeStyles.None, out var dateDmy))
                return dateDmy;
        }

        return null;
    }
}
