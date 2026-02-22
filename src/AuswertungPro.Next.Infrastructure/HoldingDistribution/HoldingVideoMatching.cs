using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

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

        // Strategy 3: Contains Haltung only (no date filter)
        var haltungOnly = files.Where(f =>
        {
            var nameKey = NormalizeKey(Path.GetFileNameWithoutExtension(f));
            return nameKey.Contains(hKey, StringComparison.OrdinalIgnoreCase);
        }).ToList();
        if (haltungOnly.Count == 1)
            return new HoldingFolderDistributor.VideoFindResult(HoldingFolderDistributor.VideoMatchStatus.Matched, haltungOnly[0], Array.Empty<string>(), null);
        if (haltungOnly.Count > 1)
            return new HoldingFolderDistributor.VideoFindResult(HoldingFolderDistributor.VideoMatchStatus.Ambiguous, null, haltungOnly, "Multiple haltung-only matches");

        return new HoldingFolderDistributor.VideoFindResult(HoldingFolderDistributor.VideoMatchStatus.NotFound, null, Array.Empty<string>(), "No video found (fallback)");
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

    private static string NormalizeKey(string value)
    {
        var chars = value
            .Where(char.IsLetterOrDigit)
            .Select(char.ToLowerInvariant)
            .ToArray();
        return new string(chars);
    }
}
