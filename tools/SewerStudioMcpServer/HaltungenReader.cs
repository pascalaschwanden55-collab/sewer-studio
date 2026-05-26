using System.Text.RegularExpressions;

namespace AuswertungPro.Tools.SewerStudioMcpServer;

public static partial class HaltungenReader
{
    private static readonly string[] VideoExtensions =
    [
        ".mp4", ".mpg", ".mpeg", ".avi", ".mov", ".mkv", ".wmv"
    ];

    private static readonly string[] FrameExtensions =
    [
        ".png", ".jpg", ".jpeg", ".bmp", ".webp"
    ];

    public static IReadOnlyList<HaltungSummary> List(string haltungenRoot)
    {
        if (string.IsNullOrWhiteSpace(haltungenRoot) || !Directory.Exists(haltungenRoot))
            return [];

        var rootFullPath = Path.GetFullPath(haltungenRoot);
        var summaries = new List<HaltungSummary>();

        foreach (var dir in EnumerateDirectoriesSafe(rootFullPath))
        {
            var name = Path.GetFileName(dir);
            if (!CaseIdRegex().IsMatch(name))
                continue;

            if (HasPathSegment(dir, "__UNMATCHED"))
                continue;

            summaries.Add(new HaltungSummary(
                CaseId: name,
                FolderPath: dir,
                RelativePath: Path.GetRelativePath(rootFullPath, dir),
                HasPdf: HasTopLevelFile(dir, ".pdf"),
                HasVideo: HasTopLevelFile(dir, VideoExtensions),
                FrameCount: CountFrames(dir)));
        }

        return summaries
            .OrderBy(s => s.CaseId, StringComparer.OrdinalIgnoreCase)
            .ToArray();
    }

    public static string? FindCaseFolder(string haltungenRoot, string caseId)
    {
        if (string.IsNullOrWhiteSpace(caseId))
            return null;

        return List(haltungenRoot)
            .FirstOrDefault(h => string.Equals(h.CaseId, caseId, StringComparison.OrdinalIgnoreCase))
            ?.FolderPath;
    }

    public static string? FindProtocolPdf(string holdingFolder)
    {
        if (!Directory.Exists(holdingFolder))
            return null;

        return Directory.EnumerateFiles(holdingFolder, "*.pdf", SearchOption.TopDirectoryOnly)
            .Where(IsLikelyProtocolPdf)
            .OrderByDescending(f => Path.GetFileName(f).Length)
            .FirstOrDefault();
    }

    private static IEnumerable<string> EnumerateDirectoriesSafe(string root)
    {
        var stack = new Stack<string>();
        stack.Push(root);

        while (stack.Count > 0)
        {
            var current = stack.Pop();
            string[] children;
            try
            {
                children = Directory.GetDirectories(current);
            }
            catch
            {
                continue;
            }

            foreach (var child in children)
            {
                var name = Path.GetFileName(child);
                if (name is "__pycache__" or ".git" or ".venv" or "bin" or "obj")
                    continue;

                yield return child;
                if (!string.Equals(name, "__UNMATCHED", StringComparison.OrdinalIgnoreCase))
                    stack.Push(child);
            }
        }
    }

    private static bool HasTopLevelFile(string dir, string extension)
        => Directory.EnumerateFiles(dir, "*" + extension, SearchOption.TopDirectoryOnly).Any();

    private static bool HasTopLevelFile(string dir, IReadOnlyCollection<string> extensions)
    {
        try
        {
            return Directory.EnumerateFiles(dir, "*.*", SearchOption.TopDirectoryOnly)
                .Any(f => extensions.Contains(Path.GetExtension(f), StringComparer.OrdinalIgnoreCase));
        }
        catch
        {
            return false;
        }
    }

    private static int CountFrames(string holdingFolder)
    {
        var count = 0;
        foreach (var framesDir in EnumerateDirectoriesSafe(holdingFolder)
                     .Where(d => string.Equals(Path.GetFileName(d), "frames", StringComparison.OrdinalIgnoreCase)))
        {
            try
            {
                count += Directory.EnumerateFiles(framesDir, "*.*", SearchOption.AllDirectories)
                    .Count(f => FrameExtensions.Contains(Path.GetExtension(f), StringComparer.OrdinalIgnoreCase));
            }
            catch
            {
                // Keep list_haltungen resilient; one locked folder must not break the tool.
            }
        }

        return count;
    }

    private static bool HasPathSegment(string path, string segment)
    {
        var parts = Path.GetFullPath(path)
            .Split(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
        return parts.Any(p => string.Equals(p, segment, StringComparison.OrdinalIgnoreCase));
    }

    private static bool IsLikelyProtocolPdf(string path)
    {
        var name = Path.GetFileNameWithoutExtension(path).ToLowerInvariant();
        return !name.Contains("plan", StringComparison.Ordinal)
               && !name.Contains("dichtheits", StringComparison.Ordinal)
               && !name.Contains("dp_", StringComparison.Ordinal);
    }

    [GeneratedRegex(@"^\d+(?:[._]\d+)*-\d+(?:[._]\d+)*$", RegexOptions.Compiled)]
    private static partial Regex CaseIdRegex();
}
