using AuswertungPro.Next.Application.Import;
using AuswertungPro.Next.Infrastructure.Backup;
using AuswertungPro.Next.Infrastructure.Common;

namespace AuswertungPro.Next.Infrastructure.Import;

/// <summary>
/// Erfasst den Projektordner vor einem Ein-Knopf-Import und nimmt die dabei neu
/// erzeugten Dateien wieder zurueck, wenn das Importergebnis verworfen wird
/// (Gesamtaudit 2026-08-14, P1-5).
///
/// Die Ruecknahme ist nur sicher, weil alle Verteilerwege des Imports Dateien
/// KOPIEREN und keine verschieben: Es gibt also kein Original, das durch das
/// Loeschen einer Zieldatei verloren gehen koennte.
/// </summary>
public sealed class ImportedFileLedgerService : IImportedFileLedger
{
    /// <summary>
    /// Ordner, die nie erfasst und nie zurueckgenommen werden.
    /// <c>__IMPORT_REPORTS</c> ist die Diagnosespur des Laufs — sie soll gerade bei
    /// einem verworfenen Import erhalten bleiben. <c>.import-staging</c> gehoert dem
    /// manuellen Importlauf und raeumt sich selbst auf.
    /// </summary>
    private static readonly string[] IgnoredDirectoryNames =
    {
        "__IMPORT_REPORTS", ".import-staging", ".git", ".tmp"
    };

    public ImportFolderSnapshot Capture(string projectFolder, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(projectFolder);
        return CaptureFolder(projectFolder, cancellationToken);
    }

    private static ImportFolderSnapshot CaptureFolder(string projectFolder, CancellationToken cancellationToken)
    {
        var root = Path.GetFullPath(projectFolder);

        var files = new Dictionary<string, long>(StringComparer.OrdinalIgnoreCase);
        var directories = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        if (!Directory.Exists(root))
            return new ImportFolderSnapshot(root, files, directories);

        Walk(root, root, cancellationToken, (relative, size) => files[relative] = size, relative => directories.Add(relative));
        return new ImportFolderSnapshot(root, files, directories);
    }

    public ImportRollbackResult RollbackNewFiles(
        ImportFolderSnapshot before,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(before);
        var messages = new List<string>();
        var root = Path.GetFullPath(before.ProjectFolder);

        if (!Directory.Exists(root))
        {
            messages.Add($"Projektordner nicht gefunden, keine Ruecknahme: {root}");
            return new ImportRollbackResult(false, 0, 0, messages);
        }

        var jetzt = CaptureFolder(root, cancellationToken);

        // Fail-closed: Ist eine zuvor vorhandene Datei verschwunden, war mehr als ein
        // reines Hinzufuegen im Spiel. Dann wird nichts geloescht.
        var verschwunden = before.FileSizesByRelativePath.Keys
            .Where(pfad => !jetzt.FileSizesByRelativePath.ContainsKey(pfad))
            .Take(5)
            .ToList();
        if (verschwunden.Count > 0)
        {
            messages.Add(
                "Ruecknahme abgebrochen: Dateien, die vor dem Import vorhanden waren, fehlen jetzt "
                + $"(z.B. {string.Join(", ", verschwunden)}). Es wurde nichts geloescht.");
            return new ImportRollbackResult(false, 0, 0, messages);
        }

        var neu = jetzt.FileSizesByRelativePath.Keys
            .Where(pfad => !before.FileSizesByRelativePath.ContainsKey(pfad))
            .OrderByDescending(pfad => pfad.Length)
            .ToList();

        var geloescht = 0;
        var behalten = 0;
        foreach (var relative in neu)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var vollPfad = Path.Combine(root, relative);
            try
            {
                if (!IsInside(root, vollPfad))
                {
                    behalten++;
                    messages.Add($"Nicht zurueckgenommen (ausserhalb des Projekts): {relative}");
                    continue;
                }

                if (ReparsePointGuard.IsReparsePoint(vollPfad))
                {
                    behalten++;
                    messages.Add($"Nicht zurueckgenommen (Verknuepfung): {relative}");
                    continue;
                }

                File.Delete(vollPfad);
                geloescht++;
            }
            catch (Exception ex)
            {
                behalten++;
                messages.Add($"Nicht zurueckgenommen ({ex.GetType().Name}): {relative}");
            }
        }

        RemoveNewEmptyDirectories(root, before, messages);

        messages.Insert(
            0,
            $"Importdateien zurueckgenommen: {geloescht} entfernt, {behalten} belassen.");
        return new ImportRollbackResult(true, geloescht, behalten, messages);
    }

    private static void Walk(
        string root,
        string current,
        CancellationToken cancellationToken,
        Action<string, long> onFile,
        Action<string> onDirectory)
    {
        foreach (var datei in SafeFileEnumeration.EnumerateFilesSafe(current, "*", recursive: false))
        {
            cancellationToken.ThrowIfCancellationRequested();
            try
            {
                onFile(Path.GetRelativePath(root, datei), new FileInfo(datei).Length);
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
            {
                // Eine nicht lesbare Datei zaehlt als vorhanden — dann wird sie spaeter
                // nicht als "neu" missverstanden und bleibt unangetastet.
                onFile(Path.GetRelativePath(root, datei), -1);
            }
        }

        IEnumerable<string> unterordner;
        try
        {
            unterordner = Directory.EnumerateDirectories(current).ToList();
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            return;
        }

        foreach (var ordner in unterordner)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var name = Path.GetFileName(ordner);
            if (IgnoredDirectoryNames.Contains(name, StringComparer.OrdinalIgnoreCase))
                continue;
            if (ReparsePointGuard.IsReparsePoint(ordner))
                continue;

            onDirectory(Path.GetRelativePath(root, ordner));
            Walk(root, ordner, cancellationToken, onFile, onDirectory);
        }
    }

    /// <summary>Entfernt nur Ordner, die der Lauf angelegt hat und die jetzt leer sind.</summary>
    private static void RemoveNewEmptyDirectories(
        string root,
        ImportFolderSnapshot before,
        List<string> messages)
    {
        var jetzt = CaptureFolder(root, CancellationToken.None);
        var neueOrdner = jetzt.RelativeDirectories
            .Where(ordner => !before.RelativeDirectories.Contains(ordner))
            .OrderByDescending(ordner => ordner.Length)
            .ToList();

        foreach (var relative in neueOrdner)
        {
            var vollPfad = Path.Combine(root, relative);
            try
            {
                if (!IsInside(root, vollPfad) || ReparsePointGuard.IsReparsePoint(vollPfad))
                    continue;
                if (!Directory.Exists(vollPfad))
                    continue;
                if (Directory.EnumerateFileSystemEntries(vollPfad).Any())
                    continue;

                Directory.Delete(vollPfad, recursive: false);
            }
            catch (Exception ex)
            {
                messages.Add($"Leerer neuer Ordner blieb stehen ({ex.GetType().Name}): {relative}");
            }
        }
    }

    private static bool IsInside(string root, string candidate)
    {
        var normalizedRoot = Path.TrimEndingDirectorySeparator(Path.GetFullPath(root));
        var relative = Path.GetRelativePath(normalizedRoot, Path.GetFullPath(candidate));
        return !relative.StartsWith("..", StringComparison.Ordinal) && !Path.IsPathRooted(relative);
    }
}
