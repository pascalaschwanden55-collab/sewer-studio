using AuswertungPro.Next.Application.Import;

namespace AuswertungPro.Next.Infrastructure.Import;

public sealed class StoredImportFileService : IStoredImportFileService
{
    private readonly object _sync = new();

    public StoredImportFilesResult Store(
        string? projectPath,
        IDictionary<string, string> metadata,
        string importKind,
        IReadOnlyCollection<string> paths,
        Func<DateTime>? now = null)
    {
        ArgumentNullException.ThrowIfNull(metadata);
        ArgumentNullException.ThrowIfNull(importKind);
        ArgumentNullException.ThrowIfNull(paths);

        lock (_sync)
            return StoreCore(projectPath, metadata, importKind, paths, now ?? (() => DateTime.Now));
    }

    private static StoredImportFilesResult StoreCore(
        string? projectPath,
        IDictionary<string, string> metadata,
        string importKind,
        IReadOnlyCollection<string> paths,
        Func<DateTime> now)
    {
        if (string.IsNullOrWhiteSpace(projectPath))
            return new StoredImportFilesResult(true, Array.Empty<string>());

        var projectDirectory = Path.GetDirectoryName(projectPath) ?? "";
        if (string.IsNullOrWhiteSpace(projectDirectory))
            return new StoredImportFilesResult(false, Array.Empty<string>());

        var targetDirectory = Path.Combine(projectDirectory, "Imports", importKind);
        Directory.CreateDirectory(targetDirectory);

        var storedPaths = new List<string>();
        foreach (var sourcePath in paths)
        {
            if (!File.Exists(sourcePath))
                continue;

            var fileName = Path.GetFileName(sourcePath);
            var targetPath = Path.Combine(targetDirectory, fileName);
            if (File.Exists(targetPath))
            {
                if (FileContentsEqual(sourcePath, targetPath))
                {
                    storedPaths.Add(Path.GetRelativePath(projectDirectory, targetPath));
                    continue;
                }

                var name = Path.GetFileNameWithoutExtension(fileName);
                var extension = Path.GetExtension(fileName);
                targetPath = Path.Combine(
                    targetDirectory,
                    $"{name}_{now():yyyyMMdd_HHmmss}{extension}");
            }

            File.Copy(sourcePath, targetPath, overwrite: false);
            storedPaths.Add(Path.GetRelativePath(projectDirectory, targetPath));
        }

        if (storedPaths.Count == 0)
            return new StoredImportFilesResult(false, Array.Empty<string>());

        StoredImportFileRegistry.Save(
            metadata,
            $"{importKind}_StoredFiles",
            storedPaths);
        return new StoredImportFilesResult(false, storedPaths);
    }

    private static bool FileContentsEqual(string firstPath, string secondPath)
    {
        var firstInfo = new FileInfo(firstPath);
        var secondInfo = new FileInfo(secondPath);
        if (firstInfo.Length != secondInfo.Length)
            return false;

        using var first = File.OpenRead(firstPath);
        using var second = File.OpenRead(secondPath);
        var firstBuffer = new byte[8192];
        var secondBuffer = new byte[8192];
        while (true)
        {
            var firstRead = first.Read(firstBuffer, 0, firstBuffer.Length);
            var secondRead = second.Read(secondBuffer, 0, secondBuffer.Length);
            if (firstRead != secondRead)
                return false;
            if (firstRead == 0)
                return true;
            if (!firstBuffer.AsSpan(0, firstRead).SequenceEqual(secondBuffer.AsSpan(0, secondRead)))
                return false;
        }
    }
}
