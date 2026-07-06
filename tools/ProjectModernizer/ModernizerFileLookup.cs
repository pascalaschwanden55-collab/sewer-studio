using AuswertungPro.Next.Application.Common;

internal static class ModernizerFileLookup
{
    public static string? ResolveSourceFile(
        string raw,
        string projectFolder,
        Func<string, bool> predicate,
        IReadOnlyDictionary<string, List<string>> externalFiles)
    {
        var trimmed = raw.Trim();

        string? resolved = null;
        if (ProjectPathResolver.IsRelative(trimmed))
            resolved = ProjectPathResolver.ResolveFilePathFromProjectFolder(trimmed, projectFolder);
        else if (File.Exists(trimmed))
            resolved = trimmed;

        if (!string.IsNullOrWhiteSpace(resolved) && predicate(resolved))
            return resolved;

        var parentToken = TryGetParentToken(trimmed);
        if (!string.IsNullOrWhiteSpace(parentToken))
        {
            var legacyHolding = Path.Combine(projectFolder, "Haltungen", parentToken);
            var legacyMatches = FindTypedFiles(legacyHolding, predicate, max: 2);
            if (legacyMatches.Count == 1)
                return legacyMatches[0];
        }

        var fileName = Path.GetFileName(trimmed);
        if (string.IsNullOrWhiteSpace(fileName) || !externalFiles.TryGetValue(fileName, out var candidates))
            return null;

        var typed = candidates.Where(predicate).ToList();
        if (typed.Count == 0)
            return null;

        var byLength = typed
            .GroupBy(ModernizerFileSystem.SafeLength)
            .OrderByDescending(g => g.Count())
            .ThenByDescending(g => g.Key)
            .First();

        return byLength
            .OrderBy(p => p.Length)
            .ThenBy(p => p, StringComparer.OrdinalIgnoreCase)
            .FirstOrDefault();
    }

    public static string? FindExactFile(string root, string fileName, Func<string, bool> predicate)
    {
        if (string.IsNullOrWhiteSpace(fileName) || !Directory.Exists(root))
            return null;

        var matches = ModernizerFileSystem.EnumerateFilesSafe(root)
            .Where(p => predicate(p) && string.Equals(Path.GetFileName(p), fileName, StringComparison.OrdinalIgnoreCase))
            .OrderBy(p => p, StringComparer.OrdinalIgnoreCase)
            .Take(2)
            .ToList();

        return matches.Count == 1 ? matches[0] : null;
    }

    public static List<string> FindTypedFiles(string root, Func<string, bool> predicate, int max)
    {
        if (!Directory.Exists(root))
            return new List<string>();

        return ModernizerFileSystem.EnumerateFilesSafe(root)
            .Where(predicate)
            .Where(p => !Path.GetFileNameWithoutExtension(p).Contains("ambiguous", StringComparison.OrdinalIgnoreCase))
            .OrderBy(p => Path.GetFileName(p).Length)
            .ThenBy(p => p, StringComparer.OrdinalIgnoreCase)
            .Take(max)
            .ToList();
    }

    private static string? TryGetParentToken(string raw)
    {
        try
        {
            var normalized = raw.Replace('/', Path.DirectorySeparatorChar).TrimEnd(Path.DirectorySeparatorChar);
            var parent = Path.GetDirectoryName(normalized);
            return string.IsNullOrWhiteSpace(parent) ? null : Path.GetFileName(parent);
        }
        catch
        {
            return null;
        }
    }
}
