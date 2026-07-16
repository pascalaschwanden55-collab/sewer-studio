using AuswertungPro.Next.Application.Import;
using AuswertungPro.Next.Application.Common;

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
        ValidateImportKind(importKind);

        if (string.IsNullOrWhiteSpace(projectPath))
            return new StoredImportFilesResult(true, Array.Empty<string>());

        var projectDirectory = Path.GetDirectoryName(projectPath) ?? "";
        if (string.IsNullOrWhiteSpace(projectDirectory))
            return new StoredImportFilesResult(false, Array.Empty<string>());

        var targetDirectory = Path.Combine(projectDirectory, "Imports", importKind);
        var storedPaths = new List<string>();
        var errors = new List<StoredImportFileError>();
        foreach (var sourcePath in paths)
        {
            if (!File.Exists(sourcePath))
            {
                var missingPath = sourcePath ?? string.Empty;
                var error = new StoredImportFileError(missingPath, "Quelldatei wurde nicht gefunden.");
                errors.Add(error);
                BestEffort.ReportWarning(
                    $"Importdatei '{missingPath}' konnte nicht im Projekt abgelegt werden: Quelldatei fehlt.");
                continue;
            }

            try
            {
                Directory.CreateDirectory(targetDirectory);
                var fileName = Path.GetFileName(sourcePath);
                var targetPath = Path.Combine(targetDirectory, fileName);
                if (File.Exists(targetPath))
                {
                    if (FileContentsEqual(sourcePath, targetPath))
                    {
                        storedPaths.Add(Path.GetRelativePath(projectDirectory, targetPath));
                        continue;
                    }

                    targetPath = ResolveCollisionPath(targetDirectory, fileName, now());
                }

                File.Copy(sourcePath, targetPath, overwrite: false);
                storedPaths.Add(Path.GetRelativePath(projectDirectory, targetPath));
            }
            catch (Exception ex)
            {
                var error = new StoredImportFileError(sourcePath, ex.Message);
                errors.Add(error);
                BestEffort.ReportWarning(
                    $"Importdatei '{sourcePath}' konnte nicht im Projekt abgelegt werden: "
                    + $"{ex.GetType().Name}: {ex.Message}");
            }
        }

        if (storedPaths.Count == 0)
        {
            return new StoredImportFilesResult(false, Array.Empty<string>())
            {
                Errors = errors
            };
        }

        StoredImportFileRegistry.Save(
            metadata,
            $"{importKind}_StoredFiles",
            storedPaths);
        return new StoredImportFilesResult(false, storedPaths)
        {
            Errors = errors
        };
    }

    private static string ResolveCollisionPath(
        string targetDirectory,
        string fileName,
        DateTime now)
    {
        var name = Path.GetFileNameWithoutExtension(fileName);
        var extension = Path.GetExtension(fileName);
        var stem = $"{name}_{now:yyyyMMdd_HHmmss}";
        var candidate = Path.Combine(targetDirectory, stem + extension);
        var suffix = 2;
        while (File.Exists(candidate))
        {
            candidate = Path.Combine(targetDirectory, $"{stem}_{suffix}{extension}");
            suffix++;
        }

        return candidate;
    }

    private static void ValidateImportKind(string importKind)
    {
        if (string.IsNullOrWhiteSpace(importKind)
            || importKind is "." or ".."
            || importKind.IndexOfAny(Path.GetInvalidFileNameChars()) >= 0
            || importKind.Contains(Path.DirectorySeparatorChar)
            || importKind.Contains(Path.AltDirectorySeparatorChar))
        {
            throw new ArgumentException(
                "Die Importart muss ein einzelner gueltiger Ordnername sein.",
                nameof(importKind));
        }
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
