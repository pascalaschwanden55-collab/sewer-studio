using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;

namespace AuswertungPro.Next.Infrastructure;

internal static class HoldingVideoMatching
{
    public static HoldingFolderDistributor.VideoFindResult FindVideo(
        string videoFileNameFromPdf,
        string haltung,
        string dateStamp,
        IReadOnlyList<string> files)
    {
        var normalizedVideoFileName = NormalizeVideoFileName(videoFileNameFromPdf);
        if (string.IsNullOrWhiteSpace(normalizedVideoFileName))
        {
            return new HoldingFolderDistributor.VideoFindResult(
                HoldingFolderDistributor.VideoMatchStatus.NotFound,
                null,
                Array.Empty<string>(),
                "No usable video filename from PDF");
        }

        var exact = files.Where(f => string.Equals(Path.GetFileName(f), normalizedVideoFileName, StringComparison.OrdinalIgnoreCase)).ToList();
        if (exact.Count == 1)
            return new HoldingFolderDistributor.VideoFindResult(HoldingFolderDistributor.VideoMatchStatus.Matched, exact[0], Array.Empty<string>(), null);
        if (exact.Count > 1)
            return new HoldingFolderDistributor.VideoFindResult(HoldingFolderDistributor.VideoMatchStatus.Ambiguous, null, exact, "Multiple exact matches");

        // Some M150/MDB exports store link references without extension.
        // In that case resolve by basename across known video extensions.
        if (string.IsNullOrWhiteSpace(Path.GetExtension(normalizedVideoFileName)))
        {
            var baseNameMatches = files.Where(f =>
                    string.Equals(Path.GetFileNameWithoutExtension(f), normalizedVideoFileName, StringComparison.OrdinalIgnoreCase))
                .ToList();
            if (baseNameMatches.Count == 1)
                return new HoldingFolderDistributor.VideoFindResult(HoldingFolderDistributor.VideoMatchStatus.Matched, baseNameMatches[0], Array.Empty<string>(), "Matched by basename (no ext)");
            if (baseNameMatches.Count > 1)
                return new HoldingFolderDistributor.VideoFindResult(HoldingFolderDistributor.VideoMatchStatus.Ambiguous, null, baseNameMatches, "Multiple basename matches (no ext)");
        }

        var suffix = GetSuffixFromFirstUnderscore(normalizedVideoFileName);
        if (!string.IsNullOrWhiteSpace(suffix))
        {
            var suffixMatches = files.Where(f => Path.GetFileName(f).EndsWith(suffix, StringComparison.OrdinalIgnoreCase)).ToList();
            if (suffixMatches.Count == 1)
                return new HoldingFolderDistributor.VideoFindResult(HoldingFolderDistributor.VideoMatchStatus.Matched, suffixMatches[0], Array.Empty<string>(), null);
            if (suffixMatches.Count > 1)
                return new HoldingFolderDistributor.VideoFindResult(HoldingFolderDistributor.VideoMatchStatus.Ambiguous, null, suffixMatches, "Multiple suffix matches");

            var suffixNoExt = Path.GetFileNameWithoutExtension(suffix);
            if (!string.IsNullOrWhiteSpace(suffixNoExt))
            {
                var suffixNoExtMatches = files.Where(f =>
                        Path.GetFileNameWithoutExtension(f).EndsWith(suffixNoExt, StringComparison.OrdinalIgnoreCase))
                    .ToList();
                if (suffixNoExtMatches.Count == 1)
                    return new HoldingFolderDistributor.VideoFindResult(HoldingFolderDistributor.VideoMatchStatus.Matched, suffixNoExtMatches[0], Array.Empty<string>(), null);
                if (suffixNoExtMatches.Count > 1)
                    return new HoldingFolderDistributor.VideoFindResult(HoldingFolderDistributor.VideoMatchStatus.Ambiguous, null, suffixNoExtMatches, "Multiple suffix matches (no ext)");
            }
        }

        var ext = Path.GetExtension(normalizedVideoFileName);
        if (!string.IsNullOrWhiteSpace(ext))
        {
            var expectedName = $"{dateStamp}_{haltung}{ext}";
            var renamed = files.Where(f => string.Equals(Path.GetFileName(f), expectedName, StringComparison.OrdinalIgnoreCase)).ToList();
            if (renamed.Count == 1)
                return new HoldingFolderDistributor.VideoFindResult(HoldingFolderDistributor.VideoMatchStatus.Matched, renamed[0], Array.Empty<string>(), null);
            if (renamed.Count > 1)
                return new HoldingFolderDistributor.VideoFindResult(HoldingFolderDistributor.VideoMatchStatus.Ambiguous, null, renamed, "Multiple renamed matches");
        }

        return new HoldingFolderDistributor.VideoFindResult(HoldingFolderDistributor.VideoMatchStatus.NotFound, null, Array.Empty<string>(), "No video found");
    }

    public static HoldingFolderDistributor.VideoFindResult FindVideoByHaltungDate(
        string haltung,
        string dateStamp,
        IReadOnlyList<string> files)
        => FindVideoByHaltungDate(haltung, dateStamp, files, fileTimestamp: null);

    public static HoldingFolderDistributor.VideoFindResult FindVideoByHaltungDate(
        string haltung,
        string dateStamp,
        IReadOnlyList<string> files,
        Func<string, DateTime?>? fileTimestamp)
    {
        // Strategy 1: Exact match with expected format: YYYYMMDD_HALTUNG.ext
        var expectedBase = $"{dateStamp}_{haltung}";
        var exact = files.Where(f => string.Equals(Path.GetFileNameWithoutExtension(f), expectedBase, StringComparison.OrdinalIgnoreCase)).ToList();
        if (exact.Count == 1)
            return new HoldingFolderDistributor.VideoFindResult(HoldingFolderDistributor.VideoMatchStatus.Matched, exact[0], Array.Empty<string>(), null);
        if (exact.Count > 1)
            return new HoldingFolderDistributor.VideoFindResult(HoldingFolderDistributor.VideoMatchStatus.Ambiguous, null, exact, "Multiple matches for date_haltung");

        // Strategy 2: Contains both Haltung and Date in filename (normalized)
        var hKey = NormalizeKey(haltung);
        var dateKey = NormalizeKey(dateStamp);
        var containing = files.Where(f =>
        {
            var nameKey = NormalizeKey(Path.GetFileNameWithoutExtension(f));
            return nameKey.Contains(hKey, StringComparison.OrdinalIgnoreCase)
                   && nameKey.Contains(dateKey, StringComparison.OrdinalIgnoreCase);
        }).ToList();
        if (containing.Count == 1)
            return new HoldingFolderDistributor.VideoFindResult(HoldingFolderDistributor.VideoMatchStatus.Matched, containing[0], Array.Empty<string>(), null);
        if (containing.Count > 1)
            return new HoldingFolderDistributor.VideoFindResult(HoldingFolderDistributor.VideoMatchStatus.Ambiguous, null, containing, "Multiple date+haltung matches");

        // Strategy 3: Enthaelt die Haltung. Politik (User 2026-06-30): Die Haltung ist das
        // Indiz - lieber zuordnen UND klar kennzeichnen als stillschweigend "missing". Das
        // fruehere harte Datums-Veto wird zum Tiebreaker/Hinweis; unsichere Treffer werden im
        // Import-Report etikettiert (Sicherheitsnetz statt zusaetzlicher UI).
        var haltungOnly = files.Where(f =>
        {
            var nameKey = NormalizeKey(Path.GetFileNameWithoutExtension(f));
            return nameKey.Contains(hKey, StringComparison.OrdinalIgnoreCase);
        }).ToList();

        if (haltungOnly.Count == 0)
            return new HoldingFolderDistributor.VideoFindResult(
                HoldingFolderDistributor.VideoMatchStatus.NotFound, null, Array.Empty<string>(), "No video found (fallback)");

        var haveProtocolDate = TryParseStamp(dateStamp, out var protocolDate);

        // Genau EIN Haltung-Video -> zuordnen. Traegt der Name ein abweichendes Datum, wird der
        // Treffer als "Datum weicht ab" gekennzeichnet (frueher: NotFound = Verwechslungsschutz).
        if (haltungOnly.Count == 1)
        {
            var only = haltungOnly[0];
            string message;
            if (TryExtractFileNameDate(only, haltung, out var ownDate))
                message = haveProtocolDate && ownDate.Date == protocolDate.Date
                    ? "Haltung-Video mit passendem Datum zugeordnet"
                    : "Einziges Haltung-Video zugeordnet (Datum im Namen weicht ab)";
            else
                message = "Eindeutiges Haltung-Video ohne Datum im Namen";

            return new HoldingFolderDistributor.VideoFindResult(
                HoldingFolderDistributor.VideoMatchStatus.Matched, only, Array.Empty<string>(), message);
        }

        // Mehrere Haltung-Videos -> das dem Protokoll-Datum naechstgelegene ueber das effektive
        // Datum (Dateiname-Datum bevorzugt, sonst Datei-Zeitstempel) waehlen. Nur bei klarem
        // Abstand (mind. 1 Tag zum naechsten Kandidaten); sonst bleibt es Ambiguous.
        if (haveProtocolDate)
        {
            var scored = haltungOnly
                .Select(f => new { File = f, Date = EffectiveDate(f, haltung, fileTimestamp) })
                .Where(x => x.Date.HasValue)
                .OrderBy(x => Math.Abs((x.Date!.Value - protocolDate).TotalDays))
                .ToList();

            if (scored.Count >= 1)
            {
                var bestDist = Math.Abs((scored[0].Date!.Value - protocolDate).TotalDays);
                var secondDist = scored.Count > 1
                    ? Math.Abs((scored[1].Date!.Value - protocolDate).TotalDays)
                    : double.MaxValue;
                if (secondDist - bestDist >= 1.0)
                {
                    var message = bestDist < 1.0
                        ? "Naechstes Haltung-Video ueber Datum/Zeitstempel zugeordnet"
                        : "Naechstes Haltung-Video zugeordnet (Datum weicht ab)";
                    return new HoldingFolderDistributor.VideoFindResult(
                        HoldingFolderDistributor.VideoMatchStatus.Matched, scored[0].File, Array.Empty<string>(), message);
                }
            }
        }

        // Nicht eindeutig aufloesbar -> Kandidaten als Ambiguous (statt "missing") zurueckgeben,
        // damit sie im Report sichtbar bleiben.
        return new HoldingFolderDistributor.VideoFindResult(
            HoldingFolderDistributor.VideoMatchStatus.Ambiguous, null, haltungOnly,
            "Mehrere Haltung-Videos, nicht eindeutig per Datum/Zeitstempel aufloesbar");
    }

    // Extrahiert das Datum aus dem Dateinamen (nach Tilgung der Haltungsnummer), falls vorhanden
    // (8-stellig YYYYMMDD, YYYY-MM-DD/YYYY.MM.DD oder DD.MM.YYYY/DD.MM.YY).
    // WICHTIG: zuerst die Haltungsnummer (und umgedrehte Variante) tilgen - sie kann selbst wie
    // ein Datum aussehen (z.B. 58875-10.1089399 -> "75-10.1089"), sonst falscher Datums-Treffer.
    private static bool TryExtractFileNameDate(string path, string haltung, out DateTime date)
    {
        date = default;
        var name = Path.GetFileNameWithoutExtension(path);
        if (string.IsNullOrWhiteSpace(name))
            return false;

        foreach (var h in new[] { haltung, ReverseHaltung(haltung) })
        {
            if (string.IsNullOrWhiteSpace(h)) continue;
            name = name.Replace(h, " ", StringComparison.OrdinalIgnoreCase);
        }

        var inv = System.Globalization.CultureInfo.InvariantCulture;
        var none = System.Globalization.DateTimeStyles.None;

        foreach (Match m in Regex.Matches(name, @"\d{8}"))
            if (DateTime.TryParseExact(m.Value, "yyyyMMdd", inv, none, out date))
                return true;
        foreach (Match m in Regex.Matches(name, @"\d{4}[.\-]\d{2}[.\-]\d{2}"))
            if (DateTime.TryParseExact(m.Value, new[] { "yyyy-MM-dd", "yyyy.MM.dd" }, inv, none, out date))
                return true;
        foreach (Match m in Regex.Matches(name, @"\d{2}[.\-]\d{2}[.\-]\d{2,4}"))
            if (DateTime.TryParseExact(m.Value, new[] { "dd.MM.yyyy", "dd-MM-yyyy", "dd.MM.yy", "dd-MM-yy" }, inv, none, out date))
                return true;

        return false;
    }

    // Effektives Datum eines Kandidaten: Dateiname-Datum bevorzugt, sonst Datei-Zeitstempel.
    private static DateTime? EffectiveDate(string path, string haltung, Func<string, DateTime?>? fileTimestamp)
    {
        if (TryExtractFileNameDate(path, haltung, out var d))
            return d;
        return fileTimestamp?.Invoke(path);
    }

    private static string ReverseHaltung(string haltung)
    {
        var parts = haltung.Split('-');
        return parts.Length == 2 ? $"{parts[1]}-{parts[0]}" : haltung;
    }

    // dateStamp kommt als YYYYMMDD aus dem Protokoll-Datum.
    private static bool TryParseStamp(string dateStamp, out DateTime date)
    {
        return DateTime.TryParseExact(dateStamp, "yyyyMMdd",
            System.Globalization.CultureInfo.InvariantCulture,
            System.Globalization.DateTimeStyles.None, out date);
    }

    private static string? GetSuffixFromFirstUnderscore(string fileName)
    {
        var idx = fileName.IndexOf('_');
        if (idx < 0 || idx + 1 >= fileName.Length)
            return null;
        return fileName.Substring(idx);
    }

    private static string? NormalizeVideoFileName(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
            return null;

        var normalized = Path.GetFileName(value.Trim());
        if (string.IsNullOrWhiteSpace(normalized))
            return null;

        normalized = normalized.Trim().Trim('\"', '\'', ')', ']', '}', ',', ';');
        normalized = normalized.Replace('\\', Path.DirectorySeparatorChar)
            .Replace('/', Path.DirectorySeparatorChar);
        normalized = Path.GetFileName(normalized);

        return string.IsNullOrWhiteSpace(normalized) ? null : normalized;
    }

    // Delegiert an HoldingTextNormalizer – identische Logik, jetzt mit Null-Guard.
    private static string NormalizeKey(string value)
        => HoldingTextNormalizer.NormalizeKey(value);
}
