using AuswertungPro.Next.Application.Common;
using AuswertungPro.Next.Infrastructure.Import;
using AuswertungPro.Next.Infrastructure.Media;

internal static class ModernizerPathResolver
{
    public static bool HasAnyFile(string root, Func<string, bool> predicate)
        => Directory.Exists(root) && ModernizerFileSystem.EnumerateFilesSafe(root).Any(predicate);

    public static bool TryResolveOrCopyModernPath(
        string raw,
        string modernRoot,
        string projectFolder,
        Func<string, bool> predicate,
        IReadOnlyDictionary<string, List<string>> externalFiles,
        bool dryRun,
        ModernizeReport report,
        FileCopyKind copyKind,
        out string relative)
    {
        if (TryResolveModernPath(raw, modernRoot, projectFolder, predicate, out relative))
            return true;

        var source = ModernizerFileLookup.ResolveSourceFile(raw, projectFolder, predicate, externalFiles);
        if (source is null)
            return false;

        var target = BuildModernTarget(raw, source, modernRoot);
        var copied = ModernizerFileSystem.CopyFileExact(source, target, dryRun, report, copyKind);
        if (string.IsNullOrWhiteSpace(copied))
            return false;

        relative = ProjectPathResolver.MakeRelative(copied, projectFolder);
        return true;
    }

    private static string BuildModernTarget(string raw, string source, string modernRoot)
    {
        var normalized = raw.Replace('/', Path.DirectorySeparatorChar);
        if (!Path.IsPathRooted(normalized))
        {
            var parts = normalized.Split(Path.DirectorySeparatorChar, StringSplitOptions.RemoveEmptyEntries);
            for (var i = 0; i < parts.Length - 2; i++)
            {
                if (ModernizerLegacyFolders.IsDataTreeRoot(parts[i]))
                {
                    var rest = parts.Skip(i + 2).ToArray();
                    if (rest.Length > 0)
                        return Path.Combine(new[] { modernRoot }.Concat(rest).ToArray());
                }
            }
        }

        return Path.Combine(
            modernRoot,
            MediaDistributionService.GetSubfolder(Path.GetExtension(source)),
            Path.GetFileName(source));
    }

    private static bool TryResolveModernPath(
        string raw,
        string modernRoot,
        string projectFolder,
        Func<string, bool> predicate,
        out string relative)
    {
        relative = "";
        var trimmed = raw.Trim();
        var fileName = Path.GetFileName(trimmed);

        if (ProjectPathResolver.IsRelative(trimmed))
        {
            var resolved = ProjectPathResolver.ResolveFilePathFromProjectFolder(trimmed, projectFolder);
            if (resolved is not null && ModernizerFileSystem.IsUnder(resolved, modernRoot) && predicate(resolved))
            {
                relative = ProjectPathResolver.MakeRelative(resolved, projectFolder);
                return true;
            }
        }
        else if (File.Exists(trimmed) && ModernizerFileSystem.IsUnder(trimmed, modernRoot) && predicate(trimmed))
        {
            relative = ProjectPathResolver.MakeRelative(trimmed, projectFolder);
            return true;
        }

        var (searchRoot, mapsToModern) = ResolveSearchRoot(modernRoot, projectFolder);

        var exact = ModernizerFileLookup.FindExactFile(searchRoot, fileName, predicate);
        if (exact is not null)
        {
            var modernPath = mapsToModern
                ? Path.Combine(modernRoot, Path.GetRelativePath(searchRoot, exact))
                : exact;
            relative = ProjectPathResolver.MakeRelative(modernPath, projectFolder);
            return true;
        }

        var typed = ModernizerFileLookup.FindTypedFiles(searchRoot, predicate, max: 2);
        if (typed.Count == 1)
        {
            var modernPath = mapsToModern
                ? Path.Combine(modernRoot, Path.GetRelativePath(searchRoot, typed[0]))
                : typed[0];
            relative = ProjectPathResolver.MakeRelative(modernPath, projectFolder);
            return true;
        }

        return false;
    }

    private static (string Root, bool MapsToModern) ResolveSearchRoot(string modernRoot, string projectFolder)
    {
        if (Directory.Exists(modernRoot))
            return (modernRoot, false);

        var haltungenModern = Path.Combine(projectFolder, ProjectStructure.HaltungenVerteilt);
        if (ModernizerFileSystem.IsUnder(modernRoot, haltungenModern))
        {
            var legacy = Path.Combine(
                projectFolder,
                ModernizerLegacyFolders.Haltungen,
                Path.GetRelativePath(haltungenModern, modernRoot));
            if (Directory.Exists(legacy))
                return (legacy, true);
        }

        var schaechteModern = Path.Combine(projectFolder, ProjectStructure.SchaechteVerteilt);
        if (ModernizerFileSystem.IsUnder(modernRoot, schaechteModern))
        {
            foreach (var legacyFolderName in ModernizerLegacyFolders.SchachtFolders)
            {
                var legacy = Path.Combine(
                    projectFolder,
                    legacyFolderName,
                    Path.GetRelativePath(schaechteModern, modernRoot));
                if (Directory.Exists(legacy))
                    return (legacy, true);
            }
        }

        return (modernRoot, false);
    }
}
