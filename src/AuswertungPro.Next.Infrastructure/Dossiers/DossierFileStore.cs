using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Security.Cryptography;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading;
using System.Threading.Tasks;

using AuswertungPro.Next.Application.Common;
using AuswertungPro.Next.Application.Dossiers;
using AuswertungPro.Next.Domain.Models;
using AuswertungPro.Next.Domain.Models.Dossiers;
using AuswertungPro.Next.Infrastructure.Import;

namespace AuswertungPro.Next.Infrastructure.Dossiers;

/// <summary>
/// Speichert die Eigentuemerdossiers eines Projekts in
/// "&lt;Projekt&gt;\Dossiers\dossiers.json".
///
/// Regeln wie beim Training-Center-Speicher: eine fehlende Datei ist ein
/// Erstlauf und ergibt ein leeres Dokument. Eine vorhandene, aber unlesbare
/// Datei ist ein Fehler — sie wird zur Beweissicherung kopiert, es wird das
/// Backup versucht, und erst wenn auch das scheitert, bricht der Vorgang ab.
/// Niemals wird eine kaputte Datei stillschweigend durch einen leeren Stand
/// ueberschrieben.
/// </summary>
public sealed class DossierFileStore : IDossierStore
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        PropertyNameCaseInsensitive = true,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
        Converters = { new JsonStringEnumConverter() }
    };

    // Alle Store-Instanzen eines laufenden SewerStudio teilen dieselbe Sperre.
    // Dadurch kann kein zweiter Speicher- oder Ladevorgang einen gerade neu
    // angelegten Ordner uebernehmen, waehrend der erste ihn noch zurueckrollen
    // koennte.
    private static readonly SemaphoreSlim MutationLock = new(1, 1);
    private readonly IDossierConditionClassPdfService? _conditionClassPdf;
    public DossierFileStore()
        : this(conditionClassPdf: null)
    {
    }

    public DossierFileStore(IDossierConditionClassPdfService? conditionClassPdf)
    {
        _conditionClassPdf = conditionClassPdf;
    }

    /// <summary>
    /// Kompatibler Konstruktor fuer Aufrufer des frueheren automatischen
    /// Haltungslistenwegs. Listen werden heute bewusst erst ueber die
    /// Schaltflaechen im Dossier-Cockpit erzeugt.
    /// </summary>
    public DossierFileStore(
        IDossierConditionClassPdfService? conditionClassPdf,
        IDossierHoldingListPdfService? holdingListPdf,
        Func<DateTime>? currentTime = null)
        : this(conditionClassPdf)
    {
        _ = holdingListPdf;
        _ = currentTime;
    }

    public Task<DossierDocument> LoadAsync(
        string projectRoot,
        CancellationToken ct = default)
        => LoadCoreAsync(projectRoot, project: null, ct);

    public Task<DossierDocument> LoadAsync(
        string projectRoot,
        Project project,
        CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(project);
        return LoadCoreAsync(projectRoot, project, ct);
    }

    private async Task<DossierDocument> LoadCoreAsync(
        string projectRoot,
        Project? project,
        CancellationToken ct)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(projectRoot);

        var path = DossierFolderPlanner.ResolveDocumentPath(projectRoot);
        if (!File.Exists(path))
            return new DossierDocument();

        try
        {
            var document = await ReadAsync(path, ct).ConfigureAwait(false);
            await EnsureDossierFoldersOnLoadAsync(projectRoot, document, project, ct)
                .ConfigureAwait(false);
            return document;
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (DossierFolderProvisionException)
        {
            // Eine lesbare JSON-Datei mit einem nicht anlegbaren Zielordner ist
            // kein JSON-Schaden und darf deshalb nicht quarantänisiert werden.
            throw;
        }
        catch (DossierSchemaVersionException)
        {
            // Der Versionsschutz darf nicht durch das Backup umgangen werden:
            // eine .bak gibt es in echten Projekten fast immer, und ein
            // stiller Rueckfall wuerde die neuere Datei beim naechsten
            // Speichern mit dem alten Stand ueberschreiben.
            throw;
        }
        catch (Exception ex)
        {
            BestEffort.ReportWarning($"[Dossiers] Ladefehler: {ex.Message}");

            var badPath = path + ".bad_" + DateTime.UtcNow.ToString("yyyyMMdd_HHmmss");
            BestEffort.Try(
                () => File.Copy(path, badPath, overwrite: false),
                $"Dossiers: unlesbare Datei nach {badPath} sichern");

            var backupPath = path + ".bak";
            if (File.Exists(backupPath))
            {
                try
                {
                    var backup = await ReadAsync(backupPath, ct).ConfigureAwait(false);
                    await EnsureDossierFoldersOnLoadAsync(projectRoot, backup, project, ct)
                        .ConfigureAwait(false);
                    Trace.WriteLine("[Dossiers] Backup .bak geladen");
                    return backup;
                }
                catch (OperationCanceledException)
                {
                    throw;
                }
                catch (DossierFolderProvisionException)
                {
                    throw;
                }
                catch (Exception backupError)
                {
                    BestEffort.ReportWarning(
                        $"[Dossiers] Backup ebenfalls unlesbar: {backupError.Message}");
                }
            }

            throw new InvalidOperationException(
                "Die Dossier-Datei ist nicht lesbar und es gibt kein brauchbares Backup. "
                + $"Eine Kopie liegt unter '{badPath}'. Es wurde nichts ueberschrieben.",
                ex);
        }
    }

    public Task SaveAsync(
        string projectRoot,
        DossierDocument document,
        CancellationToken ct = default)
        => SaveCoreAsync(projectRoot, document, project: null, ct);

    public Task SaveAsync(
        string projectRoot,
        DossierDocument document,
        Project project,
        CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(project);
        return SaveCoreAsync(projectRoot, document, project, ct);
    }

    private async Task SaveCoreAsync(
        string projectRoot,
        DossierDocument document,
        Project? project,
        CancellationToken ct)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(projectRoot);
        ArgumentNullException.ThrowIfNull(document);

        await MutationLock.WaitAsync(ct).ConfigureAwait(false);
        try
        {
            var guard = new ProjectWritePathGuard(projectRoot);
            var root = guard.EnsureSafeDirectoryTarget(
                DossierFolderPlanner.ResolveRoot(projectRoot));
            Directory.CreateDirectory(root);

            var path = guard.EnsureSafeFileTarget(
                Path.Combine(root, DossierFolderPlanner.DocumentFileName));

            var backupPath = guard.EnsureSafeFileTarget(path + ".bak");

            var newFolders = new List<DossierFolderProvision>();
            var documentWritten = false;
            try
            {
                await EnsureDossierFoldersAsync(
                        projectRoot,
                        root,
                        document,
                        project,
                        guard,
                        newFolders,
                        ct)
                    .ConfigureAwait(false);

                // Ist die vorhandene Datei kaputt, muss das bisherige .bak den
                // Schreibvorgang ueberleben.
                //
                // Das Speichern selbst legt naemlich immer eines an: der gemeinsame
                // atomare Schreiber ersetzt die Zieldatei ueber File.Replace und
                // schiebt die alte — hier also die kaputte — als .bak beiseite.
                // Ohne diese Rettung waere ausgerechnet die letzte gute Fassung weg,
                // und zwar genau in dem Moment, in dem man sie braucht.
                var zuRettendesBackup = File.Exists(path) && !IstLesbar(path) && File.Exists(backupPath)
                    ? await File.ReadAllBytesAsync(backupPath, ct).ConfigureAwait(false)
                    : null;

                // Der letzte gute Stand als .bak. Der atomare Schreiber tut das
                // unten ohnehin; dieser Weg deckt seinen Rueckfall ohne Replace ab.
                if (File.Exists(path) && IstLesbar(path))
                {
                    BestEffort.Try(
                        () => File.Copy(path, backupPath, overwrite: true),
                        "Dossiers: Backup schreiben");
                }

                document.ModifiedNow();
                var json = JsonSerializer.Serialize(document, JsonOptions);
                await AtomicTextFileWriter.WriteAllTextAsync(path, json, ct).ConfigureAwait(false);
                documentWritten = true;

                if (zuRettendesBackup is not null)
                {
                    await File.WriteAllBytesAsync(backupPath, zuRettendesBackup, ct)
                        .ConfigureAwait(false);
                }
            }
            catch
            {
                if (!documentWritten)
                    RollbackNewFolders(projectRoot, newFolders);
                throw;
            }
        }
        finally
        {
            MutationLock.Release();
        }
    }

    /// <summary>
    /// Zieht die Ordner eines bereits gespeicherten Dokuments beim Laden nach,
    /// ohne die JSON-Datei oder ihre Zeitstempel zu verändern.
    /// </summary>
    private async Task EnsureDossierFoldersOnLoadAsync(
        string projectRoot,
        DossierDocument document,
        Project? project,
        CancellationToken ct)
    {
        await MutationLock.WaitAsync(ct).ConfigureAwait(false);
        var newFolders = new List<DossierFolderProvision>();

        try
        {
            var guard = new ProjectWritePathGuard(projectRoot);
            var root = guard.EnsureSafeDirectoryTarget(
                DossierFolderPlanner.ResolveRoot(projectRoot));

            await EnsureDossierFoldersAsync(
                    projectRoot,
                    root,
                    document,
                    project,
                    guard,
                    newFolders,
                    ct)
                .ConfigureAwait(false);
        }
        catch (OperationCanceledException)
        {
            RollbackNewFolders(projectRoot, newFolders);
            throw;
        }
        catch (Exception ex)
        {
            RollbackNewFolders(projectRoot, newFolders);
            throw new DossierFolderProvisionException(
                "Die Dossier-Datei ist lesbar, aber mindestens ein "
                + "Liegenschaftsordner konnte nicht angelegt werden.",
                ex);
        }
        finally
        {
            MutationLock.Release();
        }
    }

    /// <summary>
    /// Legt die Ordner aller gespeicherten Liegenschaften an. So gilt dieselbe
    /// Regel fuer Einzel- und Stapelanlage, ohne Dateilogik im ViewModel.
    /// </summary>
    private async Task EnsureDossierFoldersAsync(
        string projectRoot,
        string dossierRoot,
        DossierDocument document,
        Project? project,
        ProjectWritePathGuard guard,
        List<DossierFolderProvision> newFolders,
        CancellationToken ct)
    {
        foreach (var dossier in document.Dossiers)
        {
            ct.ThrowIfCancellationRequested();

            if (string.IsNullOrWhiteSpace(dossier.FolderName))
                continue;

            var plannedFolder = DossierFolderPlanner.ResolveDossierFolder(
                projectRoot,
                dossier.FolderName);
            var folder = guard.EnsureSafeDirectoryTarget(plannedFolder);

            if (!string.Equals(
                    Path.GetDirectoryName(folder),
                    dossierRoot,
                    StringComparison.OrdinalIgnoreCase))
            {
                throw new ArgumentException(
                    "Der Dossierordner liegt nicht direkt unter dem Dossier-Sammelordner.",
                    nameof(document));
            }

            if (Directory.Exists(folder))
                continue;

            var provision = new DossierFolderProvision(folder);
            newFolders.Add(provision);
            Directory.CreateDirectory(folder);
            guard.EnsureSafeDirectoryTarget(folder);

            var standardFiles = BuildStandardFiles();
            foreach (var standardFile in standardFiles)
            {
                var target = guard.EnsureSafeFileTarget(Path.Combine(
                    folder,
                    standardFile.FileName));

                await WriteNewFileAtomicallyAsync(target, standardFile.Content, guard, ct)
                    .ConfigureAwait(false);
                provision.OwnedFiles.Add(new DossierOwnedFileProvision(
                    target,
                    SHA256.HashData(standardFile.Content)));
            }
        }
    }

    private IReadOnlyList<DossierStandardFile> BuildStandardFiles()
    {
        var files = new List<DossierStandardFile>(1);

        if (_conditionClassPdf is not null)
        {
            var conditionPdf = _conditionClassPdf.CreatePdf();
            if (conditionPdf.Length == 0)
                throw new InvalidDataException("Das feste Zustandsklassenblatt ist leer.");

            files.Add(new DossierStandardFile(
                DossierFolderPlanner.ConditionClassPdfFileName,
                conditionPdf));
        }

        return files;
    }

    /// <summary>
    /// Bei einem Speicherfehler wird nur die eigene, unveraenderte PDF entfernt.
    /// Danach werden weiterhin nur leere, gerade neu erzeugte Ordner entfernt.
    /// Vorhandene Ordner und Benutzerdateien bleiben unangetastet.
    /// </summary>
    private static void RollbackNewFolders(
        string projectRoot,
        IReadOnlyList<DossierFolderProvision> newFolders)
    {
        for (var index = newFolders.Count - 1; index >= 0; index--)
        {
            var provision = newFolders[index];
            var folder = provision.FolderPath;

            for (var fileIndex = provision.OwnedFiles.Count - 1; fileIndex >= 0; fileIndex--)
            {
                var ownedFile = provision.OwnedFiles[fileIndex];
                BestEffort.Try(
                    () => DeleteOwnUnchangedFile(
                        projectRoot,
                        ownedFile.Path,
                        ownedFile.Sha256),
                    $"Dossiers: eigene Standarddatei in '{folder}' zuruecknehmen");
            }

            BestEffort.Try(
                () =>
                {
                    var guard = new ProjectWritePathGuard(projectRoot);
                    var safeFolder = guard.EnsureSafeDirectoryTarget(folder);
                    if (!Directory.Exists(safeFolder))
                        return;

                    using var entries = Directory
                        .EnumerateFileSystemEntries(safeFolder)
                        .GetEnumerator();
                    if (!entries.MoveNext())
                        Directory.Delete(safeFolder, recursive: false);
                },
                $"Dossiers: leeren neuen Ordner '{folder}' zuruecknehmen");
        }
    }

    private static async Task WriteNewFileAtomicallyAsync(
        string target,
        byte[] content,
        ProjectWritePathGuard guard,
        CancellationToken ct)
    {
        var temporary = guard.EnsureSafeFileTarget(
            target + ".tmp_" + Guid.NewGuid().ToString("N"));

        try
        {
            await File.WriteAllBytesAsync(temporary, content, ct).ConfigureAwait(false);
            ct.ThrowIfCancellationRequested();
            guard.EnsureSafeFileTarget(temporary);
            guard.EnsureSafeFileTarget(target);
            File.Move(temporary, target, overwrite: false);
        }
        finally
        {
            BestEffort.Try(
                () =>
                {
                    var safeTemporary = guard.EnsureSafeFileTarget(temporary);
                    if (File.Exists(safeTemporary))
                        File.Delete(safeTemporary);
                },
                "Dossiers: temporaere Standarddatei entfernen");
        }
    }

    private static void DeleteOwnUnchangedFile(
        string projectRoot,
        string path,
        byte[] expectedSha256)
    {
        var guard = new ProjectWritePathGuard(projectRoot);
        var safePath = guard.EnsureSafeFileTarget(path);
        if (!File.Exists(safePath))
            return;

        DossierOwnedFileRollback.DeleteIfSha256Matches(safePath, expectedSha256);
    }

    /// <summary>
    /// Wahr, wenn die Datei sich als Dossier-Dokument lesen laesst.
    ///
    /// Bewusst nur eine Formpruefung ohne Umstellung: gefragt ist „taugt das
    /// als Sicherungsexemplar", nicht „ist der Inhalt aktuell".
    /// </summary>
    private static bool IstLesbar(string path)
    {
        try
        {
            using var stream = File.OpenRead(path);
            return JsonSerializer.Deserialize<DossierDocument>(stream, JsonOptions) is not null;
        }
        catch
        {
            return false;
        }
    }

    private static async Task<DossierDocument> ReadAsync(string path, CancellationToken ct)
    {
        await using var stream = File.OpenRead(path);
        var document = await JsonSerializer
            .DeserializeAsync<DossierDocument>(stream, JsonOptions, ct)
            .ConfigureAwait(false);

        if (document is null)
            throw new InvalidOperationException($"'{path}' enthaelt kein gueltiges Dossier-Dokument.");

        // Ein neueres Format als bekannt: nicht raten, sondern melden. Ein
        // stiller Weiterlauf wuerde beim naechsten Speichern Felder verlieren.
        if (document.SchemaVersion > DossierDocument.CurrentSchemaVersion)
        {
            throw new DossierSchemaVersionException(
                $"'{path}' hat Formatversion {document.SchemaVersion}. "
                + $"Diese Programmversion kennt nur Version {DossierDocument.CurrentSchemaVersion}.");
        }

        // Aeltere Staende werden beim Laden umgestellt; gespeichert wird erst,
        // wenn Pascal wirklich etwas aendert.
        return DossierDocumentMigration.MigrateToCurrent(document);
    }

    private sealed class DossierFolderProvisionException : InvalidOperationException
    {
        public DossierFolderProvisionException(string message, Exception innerException)
            : base(message, innerException)
        {
        }
    }

    private sealed class DossierFolderProvision(string folderPath)
    {
        public string FolderPath { get; } = folderPath;

        public List<DossierOwnedFileProvision> OwnedFiles { get; } = new();
    }

    private sealed record DossierOwnedFileProvision(string Path, byte[] Sha256);

    private sealed record DossierStandardFile(string FileName, byte[] Content);
}

internal static class DossierDocumentExtensions
{
    /// <summary>Setzt den Aenderungszeitpunkt aller Dossiers ohne Zeitstempel.</summary>
    public static void ModifiedNow(this DossierDocument document)
    {
        foreach (var dossier in document.Dossiers)
        {
            if (dossier.CreatedAtUtc == default)
                dossier.CreatedAtUtc = DateTime.UtcNow;
        }
    }
}
