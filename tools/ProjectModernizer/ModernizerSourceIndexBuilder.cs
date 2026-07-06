using AuswertungPro.Next.Infrastructure.Media;

internal static class ModernizerSourceIndexBuilder
{
    public static Dictionary<string, List<string>> BuildSourceVideoIndex(string? sourceFolder)
    {
        var result = new Dictionary<string, List<string>>(StringComparer.OrdinalIgnoreCase);
        if (string.IsNullOrWhiteSpace(sourceFolder) || !Directory.Exists(sourceFolder))
            return result;

        foreach (var file in ModernizerFileSystem.EnumerateFilesSafe(sourceFolder).Where(MediaFileTypes.HasVideoExtension))
        {
            var name = Path.GetFileName(file);
            var key = name.Contains('_') ? name[..name.IndexOf('_')] : Path.GetFileNameWithoutExtension(name);
            if (!result.TryGetValue(key, out var list))
                result[key] = list = new List<string>();
            list.Add(file);
        }

        return result;
    }

    public static Dictionary<string, List<string>> BuildExternalFileIndex(string projectFolder, string? sourceFolder)
    {
        var result = new Dictionary<string, List<string>>(StringComparer.OrdinalIgnoreCase);
        foreach (var root in new[] { sourceFolder, Path.Combine(projectFolder, ModernizerLegacyFolders.Imports) })
        {
            if (string.IsNullOrWhiteSpace(root) || !Directory.Exists(root))
                continue;

            foreach (var file in ModernizerFileSystem.EnumerateFilesSafe(root).Where(IsKnownMediaOrPdf))
            {
                var name = Path.GetFileName(file);
                if (!result.TryGetValue(name, out var list))
                    result[name] = list = new List<string>();
                list.Add(file);
            }
        }

        return result;
    }

    private static bool IsKnownMediaOrPdf(string path)
        => string.Equals(Path.GetExtension(path), ".pdf", StringComparison.OrdinalIgnoreCase)
           || MediaFileTypes.HasVideoExtension(path)
           || MediaFileTypes.HasImageExtension(path);
}
