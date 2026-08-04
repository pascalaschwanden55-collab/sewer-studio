namespace DetectReleaseHoldoutPdfExtractor;

internal static class PathSafety
{
    public static string RequireExistingFile(string? value, string label)
    {
        if (string.IsNullOrWhiteSpace(value))
            throw new InvalidDataException($"{label} fehlt.");
        var fullPath = Path.GetFullPath(value);
        if (!File.Exists(fullPath))
            throw new FileNotFoundException($"{label} wurde nicht gefunden.");
        RequireNoReparsePoints(fullPath, label);
        return fullPath;
    }

    public static string RequireExistingDirectory(string? value, string label)
    {
        if (string.IsNullOrWhiteSpace(value))
            throw new InvalidDataException($"{label} fehlt.");
        var fullPath = Path.GetFullPath(value);
        if (!Directory.Exists(fullPath))
            throw new DirectoryNotFoundException($"{label} wurde nicht gefunden.");
        RequireNoReparsePoints(fullPath, label);
        return fullPath;
    }

    public static void RequireInside(string root, string path, string label)
    {
        var fullRoot = Path.GetFullPath(root)
            .TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
        var fullPath = Path.GetFullPath(path);
        var relative = Path.GetRelativePath(fullRoot, fullPath);
        if (Path.IsPathRooted(relative)
            || string.Equals(relative, "..", StringComparison.Ordinal)
            || relative.StartsWith($"..{Path.DirectorySeparatorChar}", StringComparison.Ordinal)
            || relative.StartsWith($"..{Path.AltDirectorySeparatorChar}", StringComparison.Ordinal))
        {
            throw new IOException($"{label} liegt außerhalb des erlaubten Ordners.");
        }
    }

    public static void RequireNoReparsePoints(string path, string label)
    {
        var fullPath = Path.GetFullPath(path);
        var root = Path.GetPathRoot(fullPath)
                   ?? throw new IOException($"{label} besitzt keinen sicheren Laufwerkspfad.");
        var relative = Path.GetRelativePath(root, fullPath);
        var current = root;
        foreach (var segment in relative.Split(
                     [Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar],
                     StringSplitOptions.RemoveEmptyEntries))
        {
            current = Path.Combine(current, segment);
            if (!File.Exists(current) && !Directory.Exists(current))
                continue;
            if ((File.GetAttributes(current) & FileAttributes.ReparsePoint) != 0)
                throw new IOException($"{label} enthält eine Verknüpfung oder Junction.");
        }
    }
}
