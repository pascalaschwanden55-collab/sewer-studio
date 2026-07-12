namespace AuswertungPro.Next.Infrastructure.Import.Xtf;

/// <summary>
/// Loest Foto- und Videopfade aus KEK.Datei relativ zur XTF und ihren Export-Eltern auf.
/// </summary>
internal static class VsaMediaPathResolver
{
    private static readonly string[] PhotoFolders = ["Foto", "Fotos", "Picture", "Pictures"];
    private static readonly string[] VideoFolders = ["Film", "Video", "Videos"];

    public static string ResolvePhoto(string xtfPath, string? relativeFolder, string? fileName)
        => Resolve(xtfPath, relativeFolder, fileName, PhotoFolders);

    public static string ResolveVideo(string xtfPath, string? relativeFolder, string? fileName)
        => Resolve(xtfPath, relativeFolder, fileName, VideoFolders);

    internal static string Resolve(
        string xtfPath,
        string? relativeFolder,
        string? fileName,
        IReadOnlyList<string> subfolders)
    {
        var normalizedFileName = (fileName ?? string.Empty).Trim();
        if (string.IsNullOrWhiteSpace(normalizedFileName))
            return string.Empty;

        if (Path.IsPathRooted(normalizedFileName))
            return normalizedFileName;

        var relative = (relativeFolder ?? string.Empty).Trim()
            .Replace('/', Path.DirectorySeparatorChar)
            .Replace('\\', Path.DirectorySeparatorChar);

        var candidates = new List<string>();
        var directory = Path.GetDirectoryName(xtfPath) ?? string.Empty;
        for (var level = 0; level < 3 && !string.IsNullOrWhiteSpace(directory); level++)
        {
            if (!string.IsNullOrWhiteSpace(relative))
                candidates.Add(Path.GetFullPath(Path.Combine(directory, relative, normalizedFileName)));
            candidates.Add(Path.GetFullPath(Path.Combine(directory, normalizedFileName)));
            foreach (var subfolder in subfolders)
                candidates.Add(Path.GetFullPath(Path.Combine(directory, subfolder, normalizedFileName)));

            directory = Path.GetDirectoryName(directory) ?? string.Empty;
        }

        return candidates.FirstOrDefault(File.Exists)
               ?? (candidates.Count > 0 ? candidates[0] : normalizedFileName);
    }
}
