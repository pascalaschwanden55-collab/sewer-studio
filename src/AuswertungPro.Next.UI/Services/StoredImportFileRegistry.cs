using System.IO;
using System.Text.Json;

namespace AuswertungPro.Next.UI.Services;

public sealed record StoredImportFileRegistryResult(
    bool MissingProjectPath,
    IReadOnlyList<string> StoredRelativePaths);

public static class StoredImportFileRegistry
{
    public static StoredImportFileRegistryResult Store(
        string? projectPath,
        IDictionary<string, string> metadata,
        string importKind,
        IReadOnlyCollection<string> paths,
        Func<DateTime>? now = null)
    {
        ArgumentNullException.ThrowIfNull(metadata);
        ArgumentNullException.ThrowIfNull(importKind);
        ArgumentNullException.ThrowIfNull(paths);

        if (string.IsNullOrWhiteSpace(projectPath))
            return new StoredImportFileRegistryResult(true, Array.Empty<string>());

        var projectDir = Path.GetDirectoryName(projectPath) ?? "";
        if (string.IsNullOrWhiteSpace(projectDir))
            return new StoredImportFileRegistryResult(false, Array.Empty<string>());

        var targetDir = Path.Combine(projectDir, "Imports", importKind);
        Directory.CreateDirectory(targetDir);

        now ??= () => DateTime.Now;
        var stored = new List<string>();
        foreach (var src in paths)
        {
            if (!File.Exists(src))
                continue;

            var fileName = Path.GetFileName(src);
            var dest = Path.Combine(targetDir, fileName);

            if (File.Exists(dest))
            {
                var srcInfo = new FileInfo(src);
                var destInfo = new FileInfo(dest);
                if (srcInfo.Length != destInfo.Length)
                {
                    var name = Path.GetFileNameWithoutExtension(fileName);
                    var ext = Path.GetExtension(fileName);
                    dest = Path.Combine(targetDir, $"{name}_{now():yyyyMMdd_HHmmss}{ext}");
                }
                else
                {
                    stored.Add(Path.GetRelativePath(projectDir, dest));
                    continue;
                }
            }

            File.Copy(src, dest, overwrite: false);
            stored.Add(Path.GetRelativePath(projectDir, dest));
        }

        if (stored.Count == 0)
            return new StoredImportFileRegistryResult(false, Array.Empty<string>());

        var metadataKey = $"{importKind}_StoredFiles";
        var existing = LoadStoredFiles(metadata, metadataKey);
        foreach (var item in stored)
        {
            if (!existing.Contains(item, StringComparer.OrdinalIgnoreCase))
                existing.Add(item);
        }

        metadata[metadataKey] = JsonSerializer.Serialize(existing);
        return new StoredImportFileRegistryResult(false, stored);
    }

    private static List<string> LoadStoredFiles(IDictionary<string, string> metadata, string metadataKey)
    {
        if (!metadata.TryGetValue(metadataKey, out var raw) || string.IsNullOrWhiteSpace(raw))
            return new List<string>();

        try
        {
            var list = JsonSerializer.Deserialize<List<string>>(raw);
            return list?.Where(item => !string.IsNullOrWhiteSpace(item))
                .Select(item => item.Trim())
                .ToList()
                ?? new List<string>();
        }
        catch
        {
            var parts = raw.Split(';', StringSplitOptions.RemoveEmptyEntries);
            return parts.Select(part => part.Trim())
                .Where(part => part.Length > 0)
                .ToList();
        }
    }
}
