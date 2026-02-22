using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using AuswertungPro.Next.Domain.Models;

namespace AuswertungPro.Next.Infrastructure.Media;

public sealed record VideoResolveResult(
    bool Success,
    string Message,
    string? Holding,
    DateTime? Date,
    string? PdfPath,
    string? VideoPath);

public sealed class VideoSearchTool
{
    private static readonly string[] VideoExt = { ".mpg", ".mpeg", ".mp4", ".avi", ".mov", ".mkv", ".wmv" };

    private readonly string _root;
    private readonly string _unmatchedFolderName;

    public VideoSearchTool(string gemeindeRoot, string unmatchedFolderName = "__UNMATCHED")
    {
        if (string.IsNullOrWhiteSpace(gemeindeRoot))
            throw new ArgumentNullException(nameof(gemeindeRoot));
        _root = gemeindeRoot;
        _unmatchedFolderName = unmatchedFolderName;
    }

    public VideoResolveResult ResolveForRecord(HaltungRecord rec)
    {
        var holdingRaw = (rec.GetFieldValue("Haltungsname") ?? "").Trim();
        if (string.IsNullOrWhiteSpace(holdingRaw))
            return new VideoResolveResult(false, "Haltungsname ist leer in der Zeile.", null, null, null, null);

        var tokens = new List<string>
        {
            SanitizePathSegment(holdingRaw),
            holdingRaw
        }.Where(t => !string.IsNullOrWhiteSpace(t))
         .Distinct(StringComparer.OrdinalIgnoreCase)
         .ToList();

        var holdingDir = FindHoldingDirectory(_root, tokens);
        if (holdingDir is null)
        {
            return new VideoResolveResult(false,
                $"Haltungsordner nicht gefunden (root: {_root}).",
                holdingRaw, null, null, null);
        }

        var pdf = FindPdf(holdingDir, tokens);
        var date = pdf is null ? null : TryParseDateFromFileName(pdf);

        var video = FindVideo(holdingDir, tokens, date);
        if (video is not null)
            return new VideoResolveResult(true, "OK", holdingRaw, date, pdf, video);

        // Try __UNMATCHED in gemeinde folder (parent of holdingDir)
        var gemeindeDir = Directory.GetParent(holdingDir)?.FullName;
        if (!string.IsNullOrWhiteSpace(gemeindeDir))
        {
            foreach (var t in tokens)
            {
                var unmatchedDir = Path.Combine(gemeindeDir, _unmatchedFolderName, t);
                if (!Directory.Exists(unmatchedDir))
                    continue;
                var unmatchedVideo = FindAnyVideo(unmatchedDir);
                if (unmatchedVideo is not null)
                    return new VideoResolveResult(true, "OK (UNMATCHED)", holdingRaw, date, pdf, unmatchedVideo);
            }
        }

        return new VideoResolveResult(false, "Kein Video gefunden.", holdingRaw, date, pdf, null);
    }

    private static string? FindHoldingDirectory(string root, IReadOnlyList<string> tokens)
    {
        foreach (var t in tokens)
        {
            var direct = Path.Combine(root, t);
            if (Directory.Exists(direct))
                return direct;
        }

        if (!Directory.Exists(root))
            return null;

        foreach (var sub in Directory.EnumerateDirectories(root))
        {
            var subName = Path.GetFileName(sub);
            if (string.Equals(subName, "__UNMATCHED", StringComparison.OrdinalIgnoreCase))
                continue;

            foreach (var t in tokens)
            {
                var candidate = Path.Combine(sub, t);
                if (Directory.Exists(candidate))
                    return candidate;
            }
        }

        return null;
    }

    private static string? FindPdf(string holdingDir, IReadOnlyList<string> tokens)
    {
        var direct = FindPdfInDir(holdingDir, tokens, SearchOption.TopDirectoryOnly);
        if (direct is not null)
            return direct;

        return FindPdfInDir(holdingDir, tokens, SearchOption.AllDirectories);
    }

    private static string? FindPdfInDir(string holdingDir, IReadOnlyList<string> tokens, SearchOption searchOption)
    {
        foreach (var t in tokens)
        {
            var pdf = Directory.EnumerateFiles(holdingDir, $"*_{t}.pdf", searchOption)
                .OrderByDescending(f => f)
                .FirstOrDefault();
            if (pdf is not null)
                return pdf;
        }

        return Directory.EnumerateFiles(holdingDir, "*.pdf", searchOption)
            .OrderByDescending(f => f)
            .FirstOrDefault();
    }

    private static string? FindVideo(string holdingDir, IReadOnlyList<string> tokens, DateTime? date)
    {
        var (direct, ambiguous) = TryFindVideoInDir(holdingDir, tokens, date, SearchOption.TopDirectoryOnly);
        if (direct is not null || ambiguous)
            return direct;

        var (recursive, _) = TryFindVideoInDir(holdingDir, tokens, date, SearchOption.AllDirectories);
        return recursive;
    }

    private static (string? VideoPath, bool Ambiguous) TryFindVideoInDir(
        string holdingDir,
        IReadOnlyList<string> tokens,
        DateTime? date,
        SearchOption searchOption)
    {
        if (date is not null)
        {
            foreach (var t in tokens)
            {
                var baseName = $"{date:yyyyMMdd}_{t}";
                if (searchOption == SearchOption.TopDirectoryOnly)
                {
                    var exact = Directory.EnumerateFiles(holdingDir, baseName + ".*", searchOption)
                        .FirstOrDefault(f => VideoExt.Contains(Path.GetExtension(f), StringComparer.OrdinalIgnoreCase));
                    if (exact is not null)
                        return (exact, false);
                }
                else
                {
                    var exactMatches = Directory.EnumerateFiles(holdingDir, baseName + ".*", searchOption)
                        .Where(f => VideoExt.Contains(Path.GetExtension(f), StringComparer.OrdinalIgnoreCase))
                        .ToList();
                    if (exactMatches.Count == 1)
                        return (exactMatches[0], false);
                }
            }
        }

        var candidates = FindVideoCandidates(holdingDir, tokens, searchOption);
        if (candidates.Count == 1)
            return (candidates[0], false);
        if (candidates.Count > 1)
            return (null, true);

        return (null, false);
    }

    private static List<string> FindVideoCandidates(string holdingDir, IReadOnlyList<string> tokens, SearchOption searchOption)
    {
        var files = Directory.EnumerateFiles(holdingDir, "*.*", searchOption)
            .Where(f => VideoExt.Contains(Path.GetExtension(f), StringComparer.OrdinalIgnoreCase))
            .ToList();

        List<string>? best = null;
        foreach (var token in tokens)
        {
            var matches = FindCandidatesForToken(files, token);
            if (matches.Count == 1)
                return matches;
            if (matches.Count > 1 && (best is null || matches.Count < best.Count))
                best = matches;
        }

        return best ?? new List<string>();
    }

    private static List<string> FindCandidatesForToken(IEnumerable<string> files, string token)
    {
        var baseSuffix = "_" + token;
        var matches = files.Where(f =>
                Path.GetFileNameWithoutExtension(f).EndsWith(baseSuffix, StringComparison.OrdinalIgnoreCase))
            .ToList();

        if (matches.Count == 0)
        {
            var prefix = token + "_";
            matches = files.Where(f =>
                    Path.GetFileNameWithoutExtension(f).StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
                .ToList();
        }

        if (matches.Count == 0)
        {
            matches = files.Where(f =>
                    Path.GetFileNameWithoutExtension(f).Contains(token, StringComparison.OrdinalIgnoreCase))
                .ToList();
        }

        return matches;
    }

    private static string? FindAnyVideo(string dir)
    {
        var direct = Directory.EnumerateFiles(dir, "*.*", SearchOption.TopDirectoryOnly)
            .FirstOrDefault(f => VideoExt.Contains(Path.GetExtension(f), StringComparer.OrdinalIgnoreCase));
        if (direct is not null)
            return direct;

        return Directory.EnumerateFiles(dir, "*.*", SearchOption.AllDirectories)
            .FirstOrDefault(f => VideoExt.Contains(Path.GetExtension(f), StringComparer.OrdinalIgnoreCase));
    }

    private static DateTime? TryParseDateFromFileName(string path)
    {
        var name = Path.GetFileNameWithoutExtension(path);
        var m = Regex.Match(name, @"^(?<d>\d{8})_", RegexOptions.CultureInvariant);
        if (!m.Success) return null;

        return DateTime.TryParseExact(m.Groups["d"].Value, "yyyyMMdd", CultureInfo.InvariantCulture,
            DateTimeStyles.None, out var dt) ? dt : null;
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
