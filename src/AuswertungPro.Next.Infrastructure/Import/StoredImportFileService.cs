using AuswertungPro.Next.Application.Common;
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
        => StoreCore(
            projectPath,
            metadata,
            importKind,
            paths,
            now,
            fileStaging: null,
            cancellationToken: default);

    public StoredImportFilesResult StoreStaged(
        string? projectPath,
        IDictionary<string, string> metadata,
        string importKind,
        IReadOnlyCollection<string> paths,
        IImportFileStagingSession fileStaging,
        CancellationToken cancellationToken = default,
        Func<DateTime>? now = null)
    {
        ArgumentNullException.ThrowIfNull(fileStaging);
        return StoreCore(
            projectPath,
            metadata,
            importKind,
            paths,
            now,
            fileStaging,
            cancellationToken);
    }

    private StoredImportFilesResult StoreCore(
        string? projectPath,
        IDictionary<string, string> metadata,
        string importKind,
        IReadOnlyCollection<string> paths,
        Func<DateTime>? now,
        IImportFileStagingSession? fileStaging,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(metadata);
        ArgumentNullException.ThrowIfNull(importKind);
        ArgumentNullException.ThrowIfNull(paths);

        lock (_sync)
        {
            return StoreFromProjectFileCore(
                projectPath,
                metadata,
                importKind,
                paths,
                now ?? (() => DateTime.Now),
                fileStaging,
                cancellationToken);
        }
    }

    internal StoredImportFilesResult StoreInProjectDirectory(
        string projectDirectory,
        IDictionary<string, string> metadata,
        string importKind,
        string metadataKey,
        IReadOnlyCollection<string> paths,
        Func<DateTime>? now = null)
    {
        ArgumentNullException.ThrowIfNull(projectDirectory);
        ArgumentNullException.ThrowIfNull(metadata);
        ArgumentNullException.ThrowIfNull(importKind);
        ArgumentNullException.ThrowIfNull(metadataKey);
        ArgumentNullException.ThrowIfNull(paths);

        lock (_sync)
        {
            ValidateImportKind(importKind);
            if (string.IsNullOrWhiteSpace(projectDirectory))
                return new StoredImportFilesResult(false, Array.Empty<string>());

            return StoreInProjectDirectoryCore(
                projectDirectory,
                metadata,
                importKind,
                metadataKey,
                paths,
                now ?? (() => DateTime.Now),
                fileStaging: null,
                cancellationToken: default);
        }
    }

    private static StoredImportFilesResult StoreFromProjectFileCore(
        string? projectPath,
        IDictionary<string, string> metadata,
        string importKind,
        IReadOnlyCollection<string> paths,
        Func<DateTime> now,
        IImportFileStagingSession? fileStaging,
        CancellationToken cancellationToken)
    {
        ValidateImportKind(importKind);

        if (string.IsNullOrWhiteSpace(projectPath))
            return new StoredImportFilesResult(true, Array.Empty<string>());

        var projectDirectory = ProjectFileLocator.ProjectRootFromFile(projectPath)
                               ?? Path.GetDirectoryName(projectPath)
                               ?? "";
        if (string.IsNullOrWhiteSpace(projectDirectory))
            return new StoredImportFilesResult(false, Array.Empty<string>());

        return StoreInProjectDirectoryCore(
            projectDirectory,
            metadata,
            importKind,
            $"{importKind}_StoredFiles",
            paths,
            now,
            fileStaging,
            cancellationToken);
    }

    private static StoredImportFilesResult StoreInProjectDirectoryCore(
        string projectDirectory,
        IDictionary<string, string> metadata,
        string importKind,
        string metadataKey,
        IReadOnlyCollection<string> paths,
        Func<DateTime> now,
        IImportFileStagingSession? fileStaging,
        CancellationToken cancellationToken)
    {
        var targetDirectory = Path.Combine(projectDirectory, "Imports", importKind);
        if (fileStaging is not null
            && !SamePath(projectDirectory, fileStaging.ProjectRoot))
        {
            throw new InvalidOperationException(
                "Datei-Staging und Importziel gehoeren nicht zum selben Projekt.");
        }

        var storedPaths = new List<string>();
        var errors = new List<StoredImportFileError>();
        var writePathGuard = fileStaging is null
            ? new ProjectWritePathGuard(projectDirectory)
            : null;
        foreach (var sourcePath in paths)
        {
            cancellationToken.ThrowIfCancellationRequested();
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
                string targetPath;
                if (fileStaging is not null)
                {
                    targetPath = fileStaging.StageCopy(
                        sourcePath,
                        targetDirectory,
                        now,
                        cancellationToken);
                }
                else
                {
                    var safeTargetDirectory = writePathGuard!.EnsureSafeDirectoryTarget(targetDirectory);
                    Directory.CreateDirectory(safeTargetDirectory);
                    var fileName = Path.GetFileName(sourcePath);
                    targetPath = writePathGuard.EnsureSafeFileTarget(
                        Path.Combine(safeTargetDirectory, fileName));
                    if (File.Exists(targetPath))
                    {
                        if (FileContentComparer.FilesEqual(sourcePath, targetPath))
                        {
                            storedPaths.Add(Path.GetRelativePath(projectDirectory, targetPath));
                            continue;
                        }

                        targetPath = ResolveCollisionPath(safeTargetDirectory, fileName, now());
                    }

                    targetPath = writePathGuard.EnsureSafeFileTarget(targetPath);
                    File.Copy(sourcePath, targetPath, overwrite: false);
                }

                storedPaths.Add(Path.GetRelativePath(projectDirectory, targetPath));
            }
            catch (OperationCanceledException)
            {
                throw;
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
            metadataKey,
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

    private static bool SamePath(string firstPath, string secondPath)
        => string.Equals(
            Path.GetFullPath(firstPath)
                .TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar),
            Path.GetFullPath(secondPath)
                .TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar),
            StringComparison.OrdinalIgnoreCase);
}
