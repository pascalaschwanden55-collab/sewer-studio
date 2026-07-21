namespace AuswertungPro.Next.Infrastructure.Ai.Training.Inventory;

internal static class TrainingInventoryPaths
{
    public static string NormalizeRequired(string path)
    {
        if (string.IsNullOrWhiteSpace(path))
            throw new ArgumentException("KnowledgeRoot darf nicht leer sein.", nameof(path));
        return Path.GetFullPath(path.Trim());
    }

    public static string? NormalizeOptional(string? path)
        => string.IsNullOrWhiteSpace(path) ? null : Path.GetFullPath(path.Trim());

    public static IReadOnlyList<string> NormalizeDistinct(IEnumerable<string> paths)
        => paths
            .Where(path => !string.IsNullOrWhiteSpace(path))
            .Select(path => Path.GetFullPath(path.Trim()))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderBy(path => path, StringComparer.OrdinalIgnoreCase)
            .ToArray();

    public static string ResolveAgainstRoot(string path, string root)
    {
        var trimmed = path.Trim();
        return Path.IsPathFullyQualified(trimmed)
            ? Path.GetFullPath(trimmed)
            : Path.GetFullPath(trimmed, root);
    }

    public static bool IsWithinAny(string path, IReadOnlyList<string> roots)
        => roots.Any(root => IsWithin(path, root));

    public static bool IsWithin(string path, string root)
    {
        var fullPath = Path.GetFullPath(path);
        var fullRoot = Path.GetFullPath(root)
            .TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
        return fullPath.Equals(fullRoot, StringComparison.OrdinalIgnoreCase)
               || fullPath.StartsWith(fullRoot + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase);
    }

    public static string? FindReparsePoint(string path)
    {
        var fullPath = Path.GetFullPath(path);
        var pathRoot = Path.GetPathRoot(fullPath);
        if (string.IsNullOrWhiteSpace(pathRoot))
            return null;

        var current = pathRoot;
        var relative = Path.GetRelativePath(pathRoot, fullPath);
        foreach (var part in relative.Split(
                     [Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar],
                     StringSplitOptions.RemoveEmptyEntries))
        {
            current = Path.Combine(current, part);
            FileAttributes attributes;
            try
            {
                attributes = File.GetAttributes(current);
            }
            catch (Exception ex) when (ex is FileNotFoundException or DirectoryNotFoundException)
            {
                break;
            }

            if ((attributes & FileAttributes.ReparsePoint) != 0)
                return current;
        }

        return null;
    }
}
