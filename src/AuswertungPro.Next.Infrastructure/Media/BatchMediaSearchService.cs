using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
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
    bool Apply);

public sealed class BatchMediaSearchOptions
{
    public string SearchFolder { get; set; } = "";
    public bool OverwriteExisting { get; set; }
    public bool SearchPdfs { get; set; } = true;
    public bool Recursive { get; set; } = true;
}

public sealed class BatchMediaSearchService
{
    private static readonly string[] VideoExt = { ".mpg", ".mpeg", ".mp4", ".avi", ".mov", ".mkv", ".wmv" };
    private static readonly string[] PdfExt = { ".pdf" };

    public List<MediaMatch> Search(
        IReadOnlyList<HaltungRecord> records,
        BatchMediaSearchOptions options,
        IProgress<(int current, int total, string status)>? progress = null,
        CancellationToken ct = default)
    {
        var searchOption = options.Recursive ? SearchOption.AllDirectories : SearchOption.TopDirectoryOnly;

        // Phase 1: Index all files
        progress?.Report((0, records.Count, "Dateien werden indexiert..."));
        ct.ThrowIfCancellationRequested();

        var allFiles = new List<string>();
        try
        {
            allFiles = Directory.EnumerateFiles(options.SearchFolder, "*.*", searchOption)
                .Where(f =>
                {
                    var ext = Path.GetExtension(f);
                    return VideoExt.Contains(ext, StringComparer.OrdinalIgnoreCase)
                           || (options.SearchPdfs && PdfExt.Contains(ext, StringComparer.OrdinalIgnoreCase));
                })
                .ToList();
        }
        catch (UnauthorizedAccessException)
        {
            // Partial index — skip inaccessible folders
        }

        var videoFiles = allFiles
            .Where(f => VideoExt.Contains(Path.GetExtension(f), StringComparer.OrdinalIgnoreCase))
            .ToList();
        var pdfFiles = options.SearchPdfs
            ? allFiles.Where(f => PdfExt.Contains(Path.GetExtension(f), StringComparer.OrdinalIgnoreCase)).ToList()
            : new List<string>();

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
                    false));
                continue;
            }

            var tokens = BuildTokens(haltungsname);

            // Video matching
            var (videoStatus, videoPath, videoCandidates) = MatchFiles(videoFiles, tokens);

            // PDF matching
            var (pdfStatus, pdfPath, pdfCandidates) = options.SearchPdfs
                ? MatchFiles(pdfFiles, tokens)
                : (MediaMatchStatus.NotFound, (string?)null, (List<string>?)null);

            var shouldApply = videoStatus == MediaMatchStatus.Found;
            results.Add(new MediaMatch(record, haltungsname,
                videoStatus, videoPath, videoCandidates,
                pdfStatus, pdfPath, pdfCandidates,
                shouldApply));
        }

        return results;
    }

    private static List<string> BuildTokens(string haltungsname)
    {
        var sanitized = SanitizePathSegment(haltungsname);
        return new List<string> { sanitized, haltungsname }
            .Where(t => !string.IsNullOrWhiteSpace(t))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    private static (MediaMatchStatus status, string? path, List<string>? candidates) MatchFiles(
        List<string> files, List<string> tokens)
    {
        // Hierarchy: Exact filename == token → Suffix _TOKEN → Prefix TOKEN_ → Contains
        foreach (var token in tokens)
        {
            var exact = files.Where(f =>
                    string.Equals(Path.GetFileNameWithoutExtension(f), token, StringComparison.OrdinalIgnoreCase))
                .ToList();
            if (exact.Count == 1) return (MediaMatchStatus.Found, exact[0], null);
            if (exact.Count > 1) return (MediaMatchStatus.Ambiguous, exact[0], exact);
        }

        foreach (var token in tokens)
        {
            var suffix = "_" + token;
            var matches = files.Where(f =>
                    Path.GetFileNameWithoutExtension(f).EndsWith(suffix, StringComparison.OrdinalIgnoreCase))
                .ToList();
            if (matches.Count == 1) return (MediaMatchStatus.Found, matches[0], null);
            if (matches.Count > 1) return (MediaMatchStatus.Ambiguous, matches[0], matches);
        }

        foreach (var token in tokens)
        {
            var prefix = token + "_";
            var matches = files.Where(f =>
                    Path.GetFileNameWithoutExtension(f).StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
                .ToList();
            if (matches.Count == 1) return (MediaMatchStatus.Found, matches[0], null);
            if (matches.Count > 1) return (MediaMatchStatus.Ambiguous, matches[0], matches);
        }

        foreach (var token in tokens)
        {
            var matches = files.Where(f =>
                    Path.GetFileNameWithoutExtension(f).Contains(token, StringComparison.OrdinalIgnoreCase))
                .ToList();
            if (matches.Count == 1) return (MediaMatchStatus.Found, matches[0], null);
            if (matches.Count > 1) return (MediaMatchStatus.Ambiguous, matches[0], matches);
        }

        return (MediaMatchStatus.NotFound, null, null);
    }

    private static string SanitizePathSegment(string value)
    {
        var invalid = Path.GetInvalidFileNameChars();
        var sb = new StringBuilder(value.Length);
        foreach (var ch in value)
        {
            if (invalid.Contains(ch))
                sb.Append('_');
            else
                sb.Append(ch);
        }
        var cleaned = sb.ToString().Trim();
        return string.IsNullOrWhiteSpace(cleaned) ? "UNKNOWN" : cleaned;
    }
}
