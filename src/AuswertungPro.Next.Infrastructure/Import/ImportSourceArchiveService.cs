using AuswertungPro.Next.Infrastructure.Common;

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
    {
        lock (_sync)
        {
            var copied = 0;
            var reused = 0;
            var messages = new List<string>();

            foreach (var sourcePath in SafeFileEnumeration.EnumerateFilesSafe(
                         sourceFolder,
                         "*",
                         recursive: true))
            {
                try
                {
                    ArchiveOne(sourcePath, projectFolder, ref copied, ref reused, messages);
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
        ref int copied,
        ref int reused,
        List<string> messages)
    {
        var extension = Path.GetExtension(sourcePath);
        if (!ExtensionMapping.TryGetValue(extension, out var subKind))
            return;

        var targetDirectory = ProjectStructure.ImportdateienDir(projectFolder, subKind);
        Directory.CreateDirectory(targetDirectory);

        var fileName = Path.GetFileName(sourcePath);
        var targetPath = Path.Combine(targetDirectory, fileName);
        if (!File.Exists(targetPath))
        {
            CopyAtomically(sourcePath, targetPath);
            copied++;
            return;
        }

        var sourceSize = new FileInfo(sourcePath).Length;
        var targetSize = new FileInfo(targetPath).Length;
        if (sourceSize == targetSize)
        {
            reused++;
            return;
        }

        var safeName = BuildCollisionSafeName(targetDirectory, fileName);
        CopyAtomically(sourcePath, Path.Combine(targetDirectory, safeName));
        copied++;
        messages.Add(
            $"Namenskollision: '{fileName}' im Ziel hat abweichende Groesse " +
            $"({targetSize} vs. {sourceSize} Bytes). Kopiert als '{safeName}'.");
    }

    private static void CopyAtomically(string sourcePath, string targetPath)
    {
        var directory = Path.GetDirectoryName(targetPath)!;
        var tempPath = Path.Combine(
            directory,
            $".{Path.GetFileName(targetPath)}.{Guid.NewGuid():N}.tmp");
        try
        {
            File.Copy(sourcePath, tempPath, overwrite: false);
            File.Move(tempPath, targetPath, overwrite: false);
        }
        finally
        {
            try
            {
                if (File.Exists(tempPath))
                    File.Delete(tempPath);
            }
            catch
            {
                // Temp-Aufraeumen darf den eigentlichen Kopierfehler nicht verdecken.
            }
        }
    }

    private static string BuildCollisionSafeName(string targetDirectory, string originalName)
    {
        var baseName = Path.GetFileNameWithoutExtension(originalName);
        var extension = Path.GetExtension(originalName);
        var counter = 1;

        string candidate;
        do
        {
            candidate = $"{baseName}_{counter}{extension}";
            counter++;
        }
        while (File.Exists(Path.Combine(targetDirectory, candidate)));

        return candidate;
    }
}
