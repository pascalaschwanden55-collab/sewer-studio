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

        // Strategy 3: Contains Haltung only (no date filter) – Fallback mit Warnung,
        // da ohne Datumsabgleich eine Falschzuordnung moeglich ist.
        var haltungOnly = files.Where(f =>
        {
            var nameKey = NormalizeKey(Path.GetFileNameWithoutExtension(f));
            return nameKey.Contains(hKey, StringComparison.OrdinalIgnoreCase);
        }).ToList();

        if (haltungOnly.Count > 0)
        {
            // Eindeutiger Fall (User Buerglen_Gosmergasse): Genau EIN Haltung-Video, das
            // KEIN eigenes Datum im Namen traegt (z.B. L_58875-10.1089399.mpg). Dann ist die
            // Zuordnung ueber die Haltung eindeutig und ungefaehrlich -> Matched. Traegt der
            // einzige Kandidat dagegen ein ABWEICHENDES Datum (z.B. 20230101_06-001.mp4 bei
            // gesuchtem 20240630), bleibt es bei NotFound (Verwechslungsschutz). Bei mehreren
            // Kandidaten ebenfalls NotFound.
            // Nur datumslose Kandidaten (Name traegt KEIN eigenes Datum) duerfen ueber die
            // Haltung allein zugeordnet werden. Namen mit abweichendem Datum bleiben geschuetzt.
            var dateless = haltungOnly.Where(f => !FileNameHasOwnDate(f, haltung)).ToList();

            if (dateless.Count == 1)
                return new HoldingFolderDistributor.VideoFindResult(
                    HoldingFolderDistributor.VideoMatchStatus.Matched,
                    dateless[0],
                    Array.Empty<string>(),
                    "Eindeutiges Haltung-Video ohne Datum im Namen");

            // Mehrere datumslose Videos derselben Haltung (z.B. Nachinspektion): da im Namen
            // kein Datum steht, ueber den Datei-Zeitstempel das dem Protokoll-Datum
            // naechstgelegene Video waehlen (User-Regel: Haltung ist das Indiz, Datum der
            // Tiebreaker). Ist kein Zeitstempel verfuegbar oder nicht eindeutig -> Ambiguous.
            if (dateless.Count > 1 && fileTimestamp is not null && TryParseStamp(dateStamp, out var target))
            {
                var byClosest = dateless
                    .Select(f => new { File = f, Ts = fileTimestamp(f) })
                    .Where(x => x.Ts.HasValue)
                    .OrderBy(x => Math.Abs((x.Ts!.Value - target).TotalDays))
                    .ToList();

                if (byClosest.Count >= 1)
                {
                    var best = byClosest[0];
                    var secondDist = byClosest.Count > 1
                        ? Math.Abs((byClosest[1].Ts!.Value - target).TotalDays)
                        : double.MaxValue;
                    var bestDist = Math.Abs((best.Ts!.Value - target).TotalDays);
                    // Eindeutig naeher (mind. 1 Tag Abstand zum naechsten) -> zuordnen.
                    if (secondDist - bestDist >= 1.0)
                        return new HoldingFolderDistributor.VideoFindResult(
                            HoldingFolderDistributor.VideoMatchStatus.Matched,
                            best.File,
                            Array.Empty<string>(),
                            "Haltung-Video ueber Datei-Zeitstempel dem Protokoll-Datum zugeordnet");
                }
            }

            return new HoldingFolderDistributor.VideoFindResult(
                HoldingFolderDistributor.VideoMatchStatus.NotFound,
                null,
                haltungOnly,
                "Haltung-only candidates found, but not auto-matched without date validation");
        }

        return new HoldingFolderDistributor.VideoFindResult(HoldingFolderDistributor.VideoMatchStatus.NotFound, null, Array.Empty<string>(), "No video found (fallback)");
    }

    // True, wenn der Dateiname ein eigenes Datum traegt (8-stellig YYYYMMDD oder DD.MM.YYYY /
    // YYYY-MM-DD). Solche Namen NICHT ohne Datumsabgleich zuordnen (Verwechslungsschutz);
    // datumslose Namen (nur Haltung) duerfen bei Eindeutigkeit zugeordnet werden.
    // WICHTIG: zuerst die Haltungsnummer aus dem Namen entfernen - sie kann selbst wie ein
    // Datum aussehen (z.B. 58875-10.1089399 -> "75-10.1089"), sonst falscher Datums-Treffer.
    private static bool FileNameHasOwnDate(string path, string haltung)
    {
        var name = Path.GetFileNameWithoutExtension(path);
        if (string.IsNullOrWhiteSpace(name))
            return false;

        // Haltungsnummer (und ihre umgedrehte Variante) aus dem Namen tilgen.
        foreach (var h in new[] { haltung, ReverseHaltung(haltung) })
        {
            if (string.IsNullOrWhiteSpace(h)) continue;
            name = name.Replace(h, " ", StringComparison.OrdinalIgnoreCase);
        }

        return Regex.IsMatch(name, @"\d{8}")
            || Regex.IsMatch(name, @"\d{2}[.\-]\d{2}[.\-]\d{2,4}")
            || Regex.IsMatch(name, @"\d{4}[.\-]\d{2}[.\-]\d{2}");
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
