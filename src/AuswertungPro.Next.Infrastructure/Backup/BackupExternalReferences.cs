using System.Text.Json;
using AuswertungPro.Next.Application.Backup;
using AuswertungPro.Next.Application.Common;

namespace AuswertungPro.Next.Infrastructure.Backup;

/// <summary>Liest nur Projektdateien. Fremde Verweise werden einzeln gesichert, nie ganze fremde Laufwerke.</summary>
internal static class BackupExternalReferences
{
    public static FullBackupSources Resolve(FullBackupSources sources, CancellationToken ct)
    {
        var roots = (sources.ProjectRoots ?? []).Concat(sources.OptionalProjectRoots ?? [])
            .Where(Directory.Exists).Select(Path.GetFullPath).Distinct(StringComparer.OrdinalIgnoreCase).ToArray();
        var covered = roots.Concat(sources.AdditionalRoots ?? []).Select(Path.GetFullPath).ToArray();
        var files = new Dictionary<string, BackupSingleFile>(StringComparer.OrdinalIgnoreCase);
        foreach (var file in sources.ReferencedFiles ?? []) files[file.SourcePath] = file;
        foreach (var root in roots)
        {
            BackupSourcePathGuard.EnsureDirectoryRootIsSafe(root);
            var options = new EnumerationOptions { RecurseSubdirectories = true,
                AttributesToSkip = FileAttributes.ReparsePoint, IgnoreInaccessible = false };
            foreach (var project in Directory.EnumerateFiles(root, "projekt.json", options))
            {
                ct.ThrowIfCancellationRequested();
                BackupSourcePathGuard.EnsureFileIsSafe(project);
                using var stream = File.OpenRead(project);
                using var document = JsonDocument.Parse(stream);
                Visit(document.RootElement, ProjectFileLocator.ProjectRootFromFile(project) ?? Path.GetDirectoryName(project)!,
                    covered, sources.IncludeProjectVideos, files, ct);
            }
        }
        return sources with { ReferencedFiles = files.Values.OrderBy(f => f.SourcePath, StringComparer.OrdinalIgnoreCase).ToArray() };
    }

    private static void Visit(JsonElement value, string projectFolder, string[] covered, bool videos,
        Dictionary<string, BackupSingleFile> files, CancellationToken ct, string? property = null)
    {
        ct.ThrowIfCancellationRequested();
        if (value.ValueKind == JsonValueKind.Object)
            foreach (var item in value.EnumerateObject()) Visit(item.Value, projectFolder, covered, videos, files, ct, item.Name);
        else if (value.ValueKind == JsonValueKind.Array)
            foreach (var item in value.EnumerateArray()) Visit(item, projectFolder, covered, videos, files, ct, property);
        else if (value.ValueKind == JsonValueKind.String)
        {
            var text = value.GetString();
            if (string.IsNullOrWhiteSpace(text) || !Path.HasExtension(text)) return;
            if (Uri.TryCreate(text, UriKind.Absolute, out var uri) && !uri.IsFile) return;
            if (uri?.IsFile == true) text = uri.LocalPath;
            var absolute = Path.IsPathFullyQualified(text);
            if (!absolute && !(property?.Contains("Path", StringComparison.OrdinalIgnoreCase) ?? false)) return;
            var path = Path.GetFullPath(text, projectFolder);
            if (Directory.Exists(path) || covered.Any(root => IsWithin(root, path))) return;
            if (!videos && BackupExclusionRules.IsProjectVideoFileExcluded(path)) return;
            files[path] = new BackupSingleFile(path,
                Path.Combine("Externe_Dateien", BackupExternalPathKey.ForPath(path), Path.GetFileName(path)), WarnIfMissing: true);
        }
    }

    private static bool IsWithin(string root, string path)
        => path.StartsWith(root.TrimEnd(Path.DirectorySeparatorChar) + Path.DirectorySeparatorChar,
            StringComparison.OrdinalIgnoreCase);
}
