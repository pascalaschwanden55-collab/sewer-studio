using AuswertungPro.Next.Application.Common;

internal static class ModernizerStructureFileResolver
{
    public static string? ResolveExistingFile(string raw, string projectFolder, Func<string, bool> predicate)
    {
        var trimmed = raw.Trim();
        string? resolved = null;

        if (ProjectPathResolver.IsRelative(trimmed))
            resolved = ProjectPathResolver.ResolveFilePathFromProjectFolder(trimmed, projectFolder);
        else if (File.Exists(trimmed))
            resolved = trimmed;

        if (!string.IsNullOrWhiteSpace(resolved) && File.Exists(resolved) && predicate(resolved))
            return resolved;

        try
        {
            var combined = Path.Combine(projectFolder, trimmed.Replace('/', Path.DirectorySeparatorChar));
            if (File.Exists(combined) && predicate(combined))
                return combined;
        }
        catch
        {
            // ignored
        }

        return null;
    }
}
