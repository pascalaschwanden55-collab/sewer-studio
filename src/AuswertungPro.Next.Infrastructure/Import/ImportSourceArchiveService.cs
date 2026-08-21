using AuswertungPro.Next.Infrastructure.Common;
using AuswertungPro.Next.Application.Import;

namespace AuswertungPro.Next.Infrastructure.Import;

/// <summary>
/// Instanzbasierter Archivierungsdienst fuer Import-Rohdaten. Gleichzeitige
/// Aufrufe derselben Instanz werden serialisiert, damit Zielnamen eindeutig bleiben.
/// </summary>
public sealed class ImportSourceArchiveService : IImportSourceArchiver
{
    private static readonly Dictionary<string, string> ExtensionMapping = new(
        StringComparer.OrdinalIgnoreCase)
    {
        { ".fdb",  ProjectStructure.Datenbanken },
        { ".db3",  ProjectStructure.Datenbanken },
        { ".mdb",  ProjectStructure.Datenbanken },
        { ".xtf",  ProjectStructure.XtfDir },
        { ".m150", ProjectStructure.XtfDir },
        { ".xml",  ProjectStructure.XtfDir },
        { ".pdf",  ProjectStructure.PdfDir },
        { ".txt",  ProjectStructure.TxtDir },
    };

    private readonly object _sync = new();

    public ArchiveResult Archive(string sourceFolder, string projectFolder)
        => Archive(sourceFolder, projectFolder, fileStaging: null);

    public ArchiveResult Archive(
        string sourceFolder,
        string projectFolder,
        IImportFileStagingSession? fileStaging)
    {
        lock (_sync)
        {
            var copied = 0;
            var reused = 0;
            var messages = new List<string>();
            var writePaths = fileStaging is null
                ? new ProjectWritePathGuard(projectFolder)
                : null;

            foreach (var sourcePath in SafeFileEnumeration.EnumerateFilesSafe(
                         sourceFolder,
                         "*",
                         recursive: true))
            {
                try
                {
                    ArchiveOne(
                        sourcePath,
                        projectFolder,
                        fileStaging,
                        writePaths,
                        ref copied,
                        ref reused,
                        messages);
                }
                catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or PathTooLongException)
                {
                    messages.Add(
                        $"Archivkopie fehlgeschlagen, weitere Dateien werden verarbeitet: {sourcePath} ({ex.Message})");
                }
            }

            return new ArchiveResult(copied, reused, messages);
        }
    }

    private static void ArchiveOne(
        string sourcePath,
        string projectFolder,
        IImportFileStagingSession? fileStaging,
        ProjectWritePathGuard? writePaths,
        ref int copied,
        ref int reused,
        List<string> messages)
    {
        var extension = Path.GetExtension(sourcePath);
        if (!ExtensionMapping.TryGetValue(extension, out var subKind))
            return;

        var targetDirectory = ProjectStructure.ImportdateienDir(projectFolder, subKind);
        if (fileStaging is null)
        {
            targetDirectory = writePaths!.EnsureSafeDirectoryTarget(targetDirectory);
            writePaths.EnsureSafeDirectoryTarget(targetDirectory);
            Directory.CreateDirectory(targetDirectory);
        }

        if (fileStaging is not null)
        {
            var before = fileStaging.PreparedFiles.Count;
            var stagedTarget = fileStaging.StageCopy(sourcePath, targetDirectory);
            if (fileStaging.PreparedFiles.Count == before)
            {
                reused++;
                return;
            }

            copied++;
            var stagedSourceName = Path.GetFileName(sourcePath);
            var targetName = Path.GetFileName(stagedTarget);
            if (!stagedSourceName.Equals(targetName, StringComparison.OrdinalIgnoreCase))
            {
                messages.Add(
                    $"Namenskollision: '{stagedSourceName}' im Ziel hat abweichenden Inhalt. " +
                    $"Vorbereitet als '{targetName}'.");
            }

            return;
        }

        var directWritePaths = writePaths
            ?? throw new InvalidOperationException("Direkte Archivziele brauchen eine Projektpfadpruefung.");
        var fileName = Path.GetFileName(sourcePath);
        var targetPath = directWritePaths.EnsureSafeFileTarget(
            Path.Combine(targetDirectory, fileName));
        if (!File.Exists(targetPath))
        {
            CopyAtomically(sourcePath, targetPath, directWritePaths);
            copied++;
            return;
        }

        if (VerifiedImportFileCopy.ContentsEqual(sourcePath, targetPath))
        {
            reused++;
            return;
        }

        var safePath = BuildCollisionSafePath(targetDirectory, fileName, directWritePaths);
        CopyAtomically(sourcePath, safePath, directWritePaths);
        copied++;
        messages.Add(
            $"Namenskollision: '{fileName}' im Ziel hat abweichenden Inhalt. " +
            $"Kopiert als '{Path.GetFileName(safePath)}'.");
    }

    private static void CopyAtomically(
        string sourcePath,
        string targetPath,
        ProjectWritePathGuard writePaths)
    {
        var directory = writePaths.EnsureSafeDirectoryTarget(Path.GetDirectoryName(targetPath)!);
        targetPath = writePaths.EnsureSafeFileTarget(targetPath);
        var tempPath = writePaths.EnsureSafeFileTarget(Path.Combine(
            directory,
            $".{Path.GetFileName(targetPath)}.{Guid.NewGuid():N}.tmp"));
        try
        {
            writePaths.EnsureSafeDirectoryTarget(directory);
            writePaths.EnsureSafeFileTarget(tempPath);
            writePaths.EnsureSafeFileTarget(targetPath);
            File.Copy(sourcePath, tempPath, overwrite: false);

            writePaths.EnsureSafeDirectoryTarget(directory);
            writePaths.EnsureSafeFileTarget(tempPath);
            writePaths.EnsureSafeFileTarget(targetPath);
            File.Move(tempPath, targetPath, overwrite: false);
        }
        finally
        {
            try
            {
                writePaths.EnsureSafeFileTarget(tempPath);
                if (File.Exists(tempPath))
                    File.Delete(tempPath);
            }
            catch
            {
                // Temp-Aufraeumen darf den eigentlichen Kopierfehler nicht verdecken.
            }
        }
    }

    private static string BuildCollisionSafePath(
        string targetDirectory,
        string originalName,
        ProjectWritePathGuard writePaths)
    {
        var baseName = Path.GetFileNameWithoutExtension(originalName);
        var extension = Path.GetExtension(originalName);
        var counter = 1;

        string candidatePath;
        do
        {
            candidatePath = writePaths.EnsureSafeFileTarget(Path.Combine(
                targetDirectory,
                $"{baseName}_{counter}{extension}"));
            counter++;
        }
        while (File.Exists(candidatePath));

        return candidatePath;
    }
}
