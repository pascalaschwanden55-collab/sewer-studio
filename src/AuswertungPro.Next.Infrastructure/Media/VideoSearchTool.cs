using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using AuswertungPro.Next.Application.Common;
using AuswertungPro.Next.Domain.Models;

namespace AuswertungPro.Next.Infrastructure.Media;

public sealed record VideoResolveResult(
    bool Success,
    string Message,
    string? Holding,
    DateTime? Date,
    string? PdfPath,
    string? VideoPath,
    IReadOnlyList<string>? Candidates = null);

public sealed class VideoSearchTool
{
    private readonly string _root;
    private readonly string _unmatchedFolderName;
    private readonly SearchIndex _index;

    public VideoSearchTool(string gemeindeRoot, string unmatchedFolderName = "__UNMATCHED")
    {
        if (string.IsNullOrWhiteSpace(gemeindeRoot))
            throw new ArgumentNullException(nameof(gemeindeRoot));

        _root = gemeindeRoot;
        _unmatchedFolderName = unmatchedFolderName;
        _index = SearchIndex.Build(gemeindeRoot, unmatchedFolderName);
    }

    public VideoResolveResult ResolveForRecord(HaltungRecord rec)
    {
        var holdingRaw = (rec.GetFieldValue("Haltungsname") ?? "").Trim();
        if (string.IsNullOrWhiteSpace(holdingRaw))
            return new VideoResolveResult(false, "Haltungsname ist leer in der Zeile.", null, null, null, null);

        var tokens = BuildHoldingTokens(holdingRaw);
        var holdingDirs = _index.FindHoldingDirectories(tokens);
        if (holdingDirs.Count == 0)
        {
            return new VideoResolveResult(
                false,
                $"Haltungsordner nicht gefunden (root: {_root}).",
                holdingRaw,
                null,
                null,
                null);
        }

        if (holdingDirs.Count > 1)
        {
            return new VideoResolveResult(
                false,
                $"Mehrere Haltungsordner gefunden ({holdingDirs.Count}). Auto-Relink uebersprungen.",
                holdingRaw,
                null,
                null,
                null,
                holdingDirs);
        }

        var holdingDir = holdingDirs[0];
        var pdf = FindPdf(holdingDir, tokens);
        var date = pdf is null ? null : TryParseDateFromFileName(pdf);

        var directCandidates = FindVideoCandidates(_index.GetVideoFilesUnder(holdingDir), tokens, date);
        if (directCandidates.Count == 1)
        {
            return new VideoResolveResult(
                true,
                "OK",
                holdingRaw,
                date,
                pdf,
                directCandidates[0]);
        }

        if (directCandidates.Count > 1)
        {
            return new VideoResolveResult(
                false,
                $"Mehrdeutige Video-Treffer im Haltungsordner ({directCandidates.Count}). Auto-Relink uebersprungen.",
                holdingRaw,
                date,
                pdf,
                null,
                directCandidates);
        }

        var unmatchedCandidates = _index.GetUnmatchedVideos(tokens);
        if (unmatchedCandidates.Count == 1)
        {
            return new VideoResolveResult(
                true,
                "OK (UNMATCHED)",
                holdingRaw,
                date,
                pdf,
                unmatchedCandidates[0]);
        }

        if (unmatchedCandidates.Count > 1)
        {
            return new VideoResolveResult(
                false,
                $"Mehrdeutige Treffer in {_unmatchedFolderName} ({unmatchedCandidates.Count}). Auto-Relink uebersprungen.",
                holdingRaw,
                date,
                pdf,
                null,
                unmatchedCandidates);
        }

        return new VideoResolveResult(false, "Kein eindeutiges Video gefunden.", holdingRaw, date, pdf, null);
    }

    private static IReadOnlyList<string> BuildHoldingTokens(string holdingRaw)
    {
        return new List<string>
        {
            SanitizePathSegment(holdingRaw),
            holdingRaw
        }
        .Where(t => !string.IsNullOrWhiteSpace(t))
        .Distinct(StringComparer.OrdinalIgnoreCase)
        .ToList();
    }

    private string? FindPdf(string holdingDir, IReadOnlyList<string> tokens)
    {
        var files = _index.GetPdfFilesUnder(holdingDir);
        if (files.Count == 0)
            return null;

        foreach (var token in tokens)
        {
            var match = files
                .Where(pdf => Path.GetFileName(pdf).EndsWith($"_{token}.pdf", StringComparison.OrdinalIgnoreCase))
                .OrderByDescending(pdf => pdf, StringComparer.OrdinalIgnoreCase)
                .FirstOrDefault();
            if (!string.IsNullOrWhiteSpace(match))
                return match;
        }

        return files
            .OrderByDescending(pdf => pdf, StringComparer.OrdinalIgnoreCase)
            .FirstOrDefault();
    }

    private static IReadOnlyList<string> FindVideoCandidates(
        IReadOnlyList<string> files,
        IReadOnlyList<string> tokens,
        DateTime? date)
    {
        if (files.Count == 0)
            return Array.Empty<string>();

        if (date is not null)
        {
            var dateMatches = MatchLevel(
                files,
                tokens,
                (baseName, token) => baseName.Equals($"{date:yyyyMMdd}_{token}", StringComparison.OrdinalIgnoreCase));
            if (dateMatches.Count > 0)
                return dateMatches;
        }

        var exactMatches = MatchLevel(
            files,
            tokens,
            (baseName, token) => baseName.Equals(token, StringComparison.OrdinalIgnoreCase));
        if (exactMatches.Count > 0)
            return exactMatches;

        var suffixMatches = MatchLevel(
            files,
            tokens,
            (baseName, token) => baseName.EndsWith("_" + token, StringComparison.OrdinalIgnoreCase));
        if (suffixMatches.Count > 0)
            return suffixMatches;

        var prefixMatches = MatchLevel(
            files,
            tokens,
            (baseName, token) => baseName.StartsWith(token + "_", StringComparison.OrdinalIgnoreCase));
        if (prefixMatches.Count > 0)
            return prefixMatches;

        return MatchLevel(
            files,
            tokens.Where(token => token.Length >= 3).ToList(),
            (baseName, token) => baseName.Contains(token, StringComparison.OrdinalIgnoreCase));
    }

    private static IReadOnlyList<string> MatchLevel(
        IReadOnlyList<string> files,
        IReadOnlyList<string> tokens,
        Func<string, string, bool> isMatch)
    {
        if (tokens.Count == 0)
            return Array.Empty<string>();

        var matches = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var token in tokens)
        {
            foreach (var file in files)
            {
                if (isMatch(Path.GetFileNameWithoutExtension(file), token))
                    matches.Add(file);
            }
        }

        return matches.OrderBy(path => path, StringComparer.OrdinalIgnoreCase).ToList();
    }

    private static DateTime? TryParseDateFromFileName(string path)
    {
        var name = Path.GetFileNameWithoutExtension(path);
        var m = Regex.Match(name, @"^(?<d>\d{8})_", RegexOptions.CultureInvariant);
        if (!m.Success)
            return null;

        return DateTime.TryParseExact(
            m.Groups["d"].Value,
            "yyyyMMdd",
            CultureInfo.InvariantCulture,
            DateTimeStyles.None,
            out var dt)
            ? dt
            : null;
    }

    private static string SanitizePathSegment(string value)
        => ProjectPathResolver.SanitizePathSegment(value);

    private sealed class SearchIndex
    {
        private readonly Dictionary<string, List<string>> _holdingDirectoriesByName =
            new(StringComparer.OrdinalIgnoreCase);
        private readonly Dictionary<string, List<string>> _unmatchedVideosByHolding =
            new(StringComparer.OrdinalIgnoreCase);

        public SearchIndex(string root, IReadOnlyList<string> allVideoFiles, IReadOnlyList<string> allPdfFiles)
        {
            Root = root;
            AllVideoFiles = allVideoFiles;
            AllPdfFiles = allPdfFiles;
        }

        public string Root { get; }
        public IReadOnlyList<string> AllVideoFiles { get; }
        public IReadOnlyList<string> AllPdfFiles { get; }

        public static SearchIndex Build(string root, string unmatchedFolderName)
        {
            if (!Directory.Exists(root))
                return new SearchIndex(root, Array.Empty<string>(), Array.Empty<string>());

            var allFiles = EnumerateFiles(root);
            var videoFiles = allFiles
                .Where(MediaFileTypes.HasVideoExtension)
                .ToList();
            var pdfFiles = allFiles
                .Where(file => string.Equals(Path.GetExtension(file), ".pdf", StringComparison.OrdinalIgnoreCase))
                .ToList();

            var index = new SearchIndex(root, videoFiles, pdfFiles);

            foreach (var dir in EnumerateDirectories(root))
            {
                var name = Path.GetFileName(dir);
                if (string.IsNullOrWhiteSpace(name))
                    continue;

                if (IsUnderUnmatched(root, dir, unmatchedFolderName))
                    continue;

                if (!index._holdingDirectoriesByName.TryGetValue(name, out var dirs))
                {
                    dirs = new List<string>();
                    index._holdingDirectoriesByName[name] = dirs;
                }

                if (!dirs.Contains(dir, StringComparer.OrdinalIgnoreCase))
                    dirs.Add(dir);
            }

            foreach (var video in videoFiles)
            {
                var holdingName = TryGetUnmatchedHoldingName(root, Path.GetDirectoryName(video), unmatchedFolderName);
                if (string.IsNullOrWhiteSpace(holdingName))
                    continue;

                if (!index._unmatchedVideosByHolding.TryGetValue(holdingName, out var videos))
                {
                    videos = new List<string>();
                    index._unmatchedVideosByHolding[holdingName] = videos;
                }

                if (!videos.Contains(video, StringComparer.OrdinalIgnoreCase))
                    videos.Add(video);
            }

            return index;
        }

        public IReadOnlyList<string> FindHoldingDirectories(IReadOnlyList<string> tokens)
        {
            var results = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (var token in tokens)
            {
                if (!_holdingDirectoriesByName.TryGetValue(token, out var dirs))
                    continue;

                foreach (var dir in dirs)
                    results.Add(dir);
            }

            return results.OrderBy(path => path, StringComparer.OrdinalIgnoreCase).ToList();
        }

        public IReadOnlyList<string> GetVideoFilesUnder(string holdingDir)
        {
            var prefix = EnsureDirectorySuffix(holdingDir);
            return AllVideoFiles
                .Where(file => file.StartsWith(prefix, StringComparison.OrdinalIgnoreCase)
                               || string.Equals(Path.GetDirectoryName(file), holdingDir, StringComparison.OrdinalIgnoreCase))
                .OrderBy(path => path, StringComparer.OrdinalIgnoreCase)
                .ToList();
        }

        public IReadOnlyList<string> GetPdfFilesUnder(string holdingDir)
        {
            var prefix = EnsureDirectorySuffix(holdingDir);
            return AllPdfFiles
                .Where(file => file.StartsWith(prefix, StringComparison.OrdinalIgnoreCase)
                               || string.Equals(Path.GetDirectoryName(file), holdingDir, StringComparison.OrdinalIgnoreCase))
                .OrderBy(path => path, StringComparer.OrdinalIgnoreCase)
                .ToList();
        }

        public IReadOnlyList<string> GetUnmatchedVideos(IReadOnlyList<string> tokens)
        {
            var results = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (var token in tokens)
            {
                if (!_unmatchedVideosByHolding.TryGetValue(token, out var videos))
                    continue;

                foreach (var video in videos)
                    results.Add(video);
            }

            return results.OrderBy(path => path, StringComparer.OrdinalIgnoreCase).ToList();
        }

        private static IEnumerable<string> EnumerateDirectories(string root)
        {
            var stack = new Stack<string>();
            stack.Push(root);

            while (stack.Count > 0)
            {
                var current = stack.Pop();
                yield return current;

                IEnumerable<string> children;
                try
                {
                    children = Directory.EnumerateDirectories(current);
                }
                catch (UnauthorizedAccessException)
                {
                    continue;
                }
                catch (DirectoryNotFoundException)
                {
                    continue;
                }
                catch (IOException)
                {
                    continue;
                }

                foreach (var child in children)
                    stack.Push(child);
            }
        }

        private static List<string> EnumerateFiles(string root)
        {
            var files = new List<string>();
            foreach (var dir in EnumerateDirectories(root))
            {
                try
                {
                    files.AddRange(Directory.EnumerateFiles(dir));
                }
                catch (UnauthorizedAccessException)
                {
                    // Skip inaccessible directories.
                }
                catch (DirectoryNotFoundException)
                {
                    // Skip transient directories.
                }
                catch (IOException)
                {
                    // Skip broken directories.
                }
            }

            return files;
        }

        private static bool IsUnderUnmatched(string root, string dir, string unmatchedFolderName)
        {
            var relative = Path.GetRelativePath(root, dir);
            var segments = relative.Split(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
            return segments.Contains(unmatchedFolderName, StringComparer.OrdinalIgnoreCase);
        }

        private static string? TryGetUnmatchedHoldingName(string root, string? dir, string unmatchedFolderName)
        {
            if (string.IsNullOrWhiteSpace(dir))
                return null;

            var relative = Path.GetRelativePath(root, dir);
            var segments = relative.Split(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
            for (var i = 0; i < segments.Length; i++)
            {
                if (!string.Equals(segments[i], unmatchedFolderName, StringComparison.OrdinalIgnoreCase))
                    continue;

                return i + 1 < segments.Length ? segments[i + 1] : null;
            }

            return null;
        }

        private static string EnsureDirectorySuffix(string path)
        {
            if (path.EndsWith(Path.DirectorySeparatorChar.ToString(), StringComparison.Ordinal)
                || path.EndsWith(Path.AltDirectorySeparatorChar.ToString(), StringComparison.Ordinal))
                return path;

            return path + Path.DirectorySeparatorChar;
        }
    }
}
