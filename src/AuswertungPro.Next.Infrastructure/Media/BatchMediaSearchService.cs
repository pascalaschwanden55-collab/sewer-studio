using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using AuswertungPro.Next.Application.Common;
using AuswertungPro.Next.Domain.Models;

namespace AuswertungPro.Next.Infrastructure.Media;

public enum MediaMatchStatus
{
    Found,
    Ambiguous,
    NotFound,
    AlreadyLinked
}

public sealed record MediaMatch(
    HaltungRecord Record,
    string Haltungsname,
    MediaMatchStatus VideoStatus,
    string? VideoPath,
    List<string>? VideoCandidates,
    MediaMatchStatus PdfStatus,
    string? PdfPath,
    List<string>? PdfCandidates,
    MediaMatchStatus FotoStatus,
    List<string> FotoPaths,
    bool Apply);

public sealed class BatchMediaSearchOptions
{
    public string SearchFolder { get; set; } = "";
    public bool OverwriteExisting { get; set; }
    public bool SearchPdfs { get; set; } = true;
    public bool SearchPhotos { get; set; } = true;
    public bool Recursive { get; set; } = true;
}

public sealed class BatchMediaSearchService
{
    private static readonly string[] PdfExt = { ".pdf" };
    private static readonly string[] PhotoSubfolders = { "Fotos", "Photos", "Bilder", "Images", "fotos", "photos", "bilder", "images" };
    private static readonly Regex DatePrefixRegex = new(@"^(\d{8})_", RegexOptions.Compiled);
    private static readonly Regex DigitsOnlyRegex = new(@"[^\d]", RegexOptions.Compiled);

    public List<MediaMatch> Search(
        IReadOnlyList<HaltungRecord> records,
        BatchMediaSearchOptions options,
        IProgress<(int current, int total, string status)>? progress = null,
        CancellationToken ct = default)
    {
        // Phase 1: Robust recursive file index (total=0 signals indeterminate mode)
        progress?.Report((0, 0, "Dateien werden indexiert..."));
        ct.ThrowIfCancellationRequested();

        var allFiles = EnumerateFilesSafe(options.SearchFolder, options.Recursive, options.SearchPdfs, options.SearchPhotos, ct);

        var videoFiles = allFiles
            .Where(MediaFileTypes.HasVideoExtension)
            .ToList();
        var pdfFiles = options.SearchPdfs
            ? allFiles.Where(f => PdfExt.Contains(Path.GetExtension(f), StringComparer.OrdinalIgnoreCase)).ToList()
            : new List<string>();
        var photoFiles = options.SearchPhotos
            ? allFiles.Where(MediaFileTypes.HasImageExtension).ToList()
            : new List<string>();

        var photoSummary = options.SearchPhotos ? $", {photoFiles.Count} Fotos" : "";
        progress?.Report((0, records.Count, $"{videoFiles.Count} Videos, {pdfFiles.Count} PDFs{photoSummary} indexiert. Starte Zuordnung..."));

        // Phase 2: Match each record
        var results = new List<MediaMatch>(records.Count);
        for (int i = 0; i < records.Count; i++)
        {
            ct.ThrowIfCancellationRequested();
            var record = records[i];
            var haltungsname = (record.GetFieldValue("Haltungsname") ?? "").Trim();
            progress?.Report((i + 1, records.Count, $"Suche: {haltungsname}"));

            if (string.IsNullOrWhiteSpace(haltungsname))
            {
                results.Add(new MediaMatch(record, haltungsname,
                    MediaMatchStatus.NotFound, null, null,
                    MediaMatchStatus.NotFound, null, null,
                    MediaMatchStatus.NotFound, new List<string>(),
                    false));
                continue;
            }

            // Check existing link
            var existingLink = record.GetFieldValue("Link")?.Trim();
            var hasExistingVideo = !string.IsNullOrWhiteSpace(existingLink) && File.Exists(existingLink);
            if (hasExistingVideo && !options.OverwriteExisting)
            {
                results.Add(new MediaMatch(record, haltungsname,
                    MediaMatchStatus.AlreadyLinked, existingLink, null,
                    MediaMatchStatus.AlreadyLinked, null, null,
                    MediaMatchStatus.NotFound, new List<string>(),
                    false));
                continue;
            }

            // Build comprehensive token list from all relevant fields
            var tokens = BuildTokens(record, haltungsname);
            var datumStr = (record.GetFieldValue("Datum_Jahr") ?? "").Trim();

            // Video matching
            var (videoStatus, videoPath, videoCandidates) = MatchFiles(videoFiles, tokens, datumStr);

            // PDF matching
            var (pdfStatus, pdfPath, pdfCandidates) = options.SearchPdfs
                ? MatchFiles(pdfFiles, tokens, datumStr)
                : (MediaMatchStatus.NotFound, (string?)null, (List<string>?)null);

            // Photo matching
            var foundPhotos = options.SearchPhotos
                ? FindPhotosForRecord(photoFiles, tokens, videoPath)
                : new List<string>();
            var fotoStatus = foundPhotos.Count > 0 ? MediaMatchStatus.Found : MediaMatchStatus.NotFound;

            var shouldApply = videoStatus == MediaMatchStatus.Found || foundPhotos.Count > 0;
            results.Add(new MediaMatch(record, haltungsname,
                videoStatus, videoPath, videoCandidates,
                pdfStatus, pdfPath, pdfCandidates,
                fotoStatus, foundPhotos,
                shouldApply));
        }

        return results;
    }

    /// <summary>
    /// Recursively enumerates files, skipping inaccessible directories instead of aborting.
    /// </summary>
    private static List<string> EnumerateFilesSafe(string root, bool recursive, bool includePdfs, bool includePhotos, CancellationToken ct)
    {
        var result = new List<string>();
        var stack = new Stack<string>();
        stack.Push(root);

        while (stack.Count > 0)
        {
            ct.ThrowIfCancellationRequested();
            var dir = stack.Pop();

            try
            {
                foreach (var file in Directory.EnumerateFiles(dir))
                {
                    var ext = Path.GetExtension(file);
                    if (MediaFileTypes.HasVideoExtension(ext)
                        || (includePdfs && PdfExt.Contains(ext, StringComparer.OrdinalIgnoreCase))
                        || (includePhotos && MediaFileTypes.HasImageExtension(ext)))
                    {
                        result.Add(file);
                    }
                }
            }
            catch (UnauthorizedAccessException) { }
            catch (DirectoryNotFoundException) { }
            catch (IOException) { }

            if (recursive)
            {
                try
                {
                    foreach (var sub in Directory.EnumerateDirectories(dir))
                        stack.Push(sub);
                }
                catch (UnauthorizedAccessException) { }
                catch (DirectoryNotFoundException) { }
                catch (IOException) { }
            }
        }

        return result;
    }

    /// <summary>
    /// Builds a comprehensive list of search tokens from all available record fields.
    /// </summary>
    private static List<string> BuildTokens(HaltungRecord record, string haltungsname)
    {
        var raw = new List<string>();

        // 1. Haltungsname (primary)
        raw.Add(haltungsname);
        raw.Add(SanitizePathSegment(haltungsname));

        // 2. Schacht_oben / Schacht_unten and combinations
        var oben = (record.GetFieldValue("Schacht_oben") ?? "").Trim();
        var unten = (record.GetFieldValue("Schacht_unten") ?? "").Trim();
        if (!string.IsNullOrWhiteSpace(oben)) raw.Add(oben);
        if (!string.IsNullOrWhiteSpace(unten)) raw.Add(unten);
        if (!string.IsNullOrWhiteSpace(oben) && !string.IsNullOrWhiteSpace(unten))
        {
            raw.Add($"{oben}-{unten}");
            raw.Add($"{unten}-{oben}");
            raw.Add($"{oben}_{unten}");
            raw.Add($"{unten}_{oben}");
        }

        // 3. Strasse as fallback
        var strasse = (record.GetFieldValue("Strasse") ?? "").Trim();
        if (!string.IsNullOrWhiteSpace(strasse))
            raw.Add(strasse);

        // 4. Normalized variants of all tokens so far
        var expanded = new List<string>(raw);
        foreach (var t in raw)
        {
            // Strip separators: "H-123.45" -> "H12345"
            var stripped = t.Replace("-", "").Replace(".", "").Replace(" ", "");
            if (!string.Equals(stripped, t, StringComparison.OrdinalIgnoreCase))
                expanded.Add(stripped);

            // Digits only: "H-123.45" -> "12345"
            var digitsOnly = DigitsOnlyRegex.Replace(t, "");
            if (digitsOnly.Length >= 3)
                expanded.Add(digitsOnly);

            // Remove leading zeros from numeric parts: "H-007" -> "H-7"
            var noLeadingZeros = Regex.Replace(t, @"(?<=^|[^0-9])0+(\d)", "$1");
            if (!string.Equals(noLeadingZeros, t, StringComparison.OrdinalIgnoreCase))
                expanded.Add(noLeadingZeros);
        }

        return expanded
            .Where(t => !string.IsNullOrWhiteSpace(t) && t.Length >= 2)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    private static (MediaMatchStatus status, string? path, List<string>? candidates) MatchFiles(
        List<string> files, List<string> tokens, string datumStr)
    {
        // Level 0: Date-prefixed exact match (yyyyMMdd_token)
        var date = TryParseDate(datumStr);
        if (date is not null)
        {
            foreach (var token in tokens)
            {
                var datePrefix = $"{date:yyyyMMdd}_{token}";
                var matches = files.Where(f =>
                        Path.GetFileNameWithoutExtension(f).Equals(datePrefix, StringComparison.OrdinalIgnoreCase))
                    .ToList();
                if (matches.Count == 1) return (MediaMatchStatus.Found, matches[0], null);
                if (matches.Count > 1) return (MediaMatchStatus.Ambiguous, matches[0], matches);
            }
        }

        // Level 1: Exact filename == token
        foreach (var token in tokens)
        {
            var exact = files.Where(f =>
                    string.Equals(Path.GetFileNameWithoutExtension(f), token, StringComparison.OrdinalIgnoreCase))
                .ToList();
            if (exact.Count == 1) return (MediaMatchStatus.Found, exact[0], null);
            if (exact.Count > 1) return (MediaMatchStatus.Ambiguous, exact[0], exact);
        }

        // Level 2: Suffix _TOKEN
        foreach (var token in tokens)
        {
            var suffix = "_" + token;
            var matches = files.Where(f =>
                    Path.GetFileNameWithoutExtension(f).EndsWith(suffix, StringComparison.OrdinalIgnoreCase))
                .ToList();
            if (matches.Count == 1) return (MediaMatchStatus.Found, matches[0], null);
            if (matches.Count > 1) return (MediaMatchStatus.Ambiguous, matches[0], matches);
        }

        // Level 3: Prefix TOKEN_
        foreach (var token in tokens)
        {
            var prefix = token + "_";
            var matches = files.Where(f =>
                    Path.GetFileNameWithoutExtension(f).StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
                .ToList();
            if (matches.Count == 1) return (MediaMatchStatus.Found, matches[0], null);
            if (matches.Count > 1) return (MediaMatchStatus.Ambiguous, matches[0], matches);
        }

        // Level 4: Token in filename (Contains)
        foreach (var token in tokens)
        {
            if (token.Length < 3) continue; // Skip very short tokens for contains
            var matches = files.Where(f =>
                    Path.GetFileNameWithoutExtension(f).Contains(token, StringComparison.OrdinalIgnoreCase))
                .ToList();
            if (matches.Count == 1) return (MediaMatchStatus.Found, matches[0], null);
            if (matches.Count > 1) return (MediaMatchStatus.Ambiguous, matches[0], matches);
        }

        // Level 5: Token matches parent folder name exactly
        foreach (var token in tokens)
        {
            var matches = files.Where(f =>
            {
                var parent = Path.GetDirectoryName(f);
                if (string.IsNullOrEmpty(parent)) return false;
                var dirName = Path.GetFileName(parent);
                return string.Equals(dirName, token, StringComparison.OrdinalIgnoreCase);
            }).ToList();
            if (matches.Count == 1) return (MediaMatchStatus.Found, matches[0], null);
            if (matches.Count > 1) return (MediaMatchStatus.Ambiguous, matches[0], matches);
        }

        // Level 6: Token appears anywhere in the full path (any folder segment)
        foreach (var token in tokens)
        {
            if (token.Length < 4) continue; // Avoid false positives from short tokens
            var matches = files.Where(f =>
            {
                var dir = Path.GetDirectoryName(f) ?? "";
                return dir.Split(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar)
                    .Any(seg => seg.Contains(token, StringComparison.OrdinalIgnoreCase));
            }).ToList();
            if (matches.Count == 1) return (MediaMatchStatus.Found, matches[0], null);
            if (matches.Count > 1) return (MediaMatchStatus.Ambiguous, matches[0], matches);
        }

        return (MediaMatchStatus.NotFound, null, null);
    }

    /// <summary>
    /// Finds photos related to a record using multiple strategies.
    /// </summary>
    private static List<string> FindPhotosForRecord(List<string> photoFiles, List<string> tokens, string? videoPath)
    {
        var found = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        // Strategy 1: If video was found, search same directory and photo subfolders
        if (!string.IsNullOrWhiteSpace(videoPath))
        {
            var videoDir = Path.GetDirectoryName(videoPath);
            if (!string.IsNullOrEmpty(videoDir))
            {
                // Photos in same folder as video
                foreach (var pf in photoFiles)
                {
                    if (string.Equals(Path.GetDirectoryName(pf), videoDir, StringComparison.OrdinalIgnoreCase))
                        found.Add(pf);
                }

                // Photos in known subfolders (Fotos/, Photos/, Bilder/, Images/)
                foreach (var sub in PhotoSubfolders)
                {
                    var subDir = Path.Combine(videoDir, sub);
                    foreach (var pf in photoFiles)
                    {
                        var pfDir = Path.GetDirectoryName(pf);
                        if (!string.IsNullOrEmpty(pfDir)
                            && pfDir.StartsWith(subDir, StringComparison.OrdinalIgnoreCase))
                            found.Add(pf);
                    }
                }
            }
        }

        if (found.Count > 0)
            return found.OrderBy(f => f, StringComparer.OrdinalIgnoreCase).ToList();

        // Strategy 2: Directory name matches a token and contains photos
        foreach (var token in tokens)
        {
            if (token.Length < 3) continue;
            foreach (var pf in photoFiles)
            {
                var dir = Path.GetDirectoryName(pf);
                if (string.IsNullOrEmpty(dir)) continue;
                var dirName = Path.GetFileName(dir);
                if (string.Equals(dirName, token, StringComparison.OrdinalIgnoreCase)
                    || dirName.Contains(token, StringComparison.OrdinalIgnoreCase))
                    found.Add(pf);
            }
            if (found.Count > 0) break;
        }

        if (found.Count > 0)
            return found.OrderBy(f => f, StringComparer.OrdinalIgnoreCase).ToList();

        // Strategy 3: Filename contains a token (fallback for individual photo files)
        foreach (var token in tokens)
        {
            if (token.Length < 3) continue;
            foreach (var pf in photoFiles)
            {
                var name = Path.GetFileNameWithoutExtension(pf);
                if (name.Contains(token, StringComparison.OrdinalIgnoreCase))
                    found.Add(pf);
            }
            if (found.Count > 0) break;
        }

        return found.OrderBy(f => f, StringComparer.OrdinalIgnoreCase).ToList();
    }

    private static DateTime? TryParseDate(string? raw)
    {
        if (string.IsNullOrWhiteSpace(raw)) return null;

        // Try yyyyMMdd
        if (DateTime.TryParseExact(raw.Trim(), "yyyyMMdd", CultureInfo.InvariantCulture, DateTimeStyles.None, out var dt1))
            return dt1;

        // Try dd.MM.yyyy
        if (DateTime.TryParseExact(raw.Trim(), "dd.MM.yyyy", CultureInfo.InvariantCulture, DateTimeStyles.None, out var dt2))
            return dt2;

        // Try yyyy
        if (raw.Trim().Length == 4 && int.TryParse(raw.Trim(), out var year) && year > 1990 && year < 2100)
            return new DateTime(year, 1, 1);

        return null;
    }

    private static string SanitizePathSegment(string value)
        => ProjectPathResolver.SanitizePathSegment(value);
}
