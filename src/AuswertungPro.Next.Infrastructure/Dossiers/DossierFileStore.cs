using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading;
using System.Threading.Tasks;

using AuswertungPro.Next.Application.Common;
using AuswertungPro.Next.Application.Dossiers;
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

    private readonly SemaphoreSlim _saveLock = new(1, 1);

    public async Task<DossierDocument> LoadAsync(string projectRoot, CancellationToken ct = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(projectRoot);

        var path = DossierFolderPlanner.ResolveDocumentPath(projectRoot);
        if (!File.Exists(path))
            return new DossierDocument();

        try
        {
            var document = await ReadAsync(path, ct).ConfigureAwait(false);
            await EnsureDossierFoldersOnLoadAsync(projectRoot, document, ct)
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
                    await EnsureDossierFoldersOnLoadAsync(projectRoot, backup, ct)
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

    public async Task SaveAsync(
        string projectRoot,
        DossierDocument document,
        CancellationToken ct = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(projectRoot);
        ArgumentNullException.ThrowIfNull(document);

        await _saveLock.WaitAsync(ct).ConfigureAwait(false);
        try
        {
            var guard = new ProjectWritePathGuard(projectRoot);
            var root = guard.EnsureSafeDirectoryTarget(
                DossierFolderPlanner.ResolveRoot(projectRoot));
            Directory.CreateDirectory(root);

            var path = guard.EnsureSafeFileTarget(
                Path.Combine(root, DossierFolderPlanner.DocumentFileName));

            var backupPath = guard.EnsureSafeFileTarget(path + ".bak");

            var newFolders = new List<string>();
            var documentWritten = false;
            try
            {
                EnsureDossierFolders(projectRoot, root, document, guard, newFolders);

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
                    RollbackEmptyFolders(projectRoot, newFolders);
                throw;
            }
        }
        finally
        {
            _saveLock.Release();
        }
    }

    /// <summary>
    /// Zieht die Ordner eines bereits gespeicherten Dokuments beim Laden nach,
    /// ohne die JSON-Datei oder ihre Zeitstempel zu verändern.
    /// </summary>
    private async Task EnsureDossierFoldersOnLoadAsync(
        string projectRoot,
        DossierDocument document,
        CancellationToken ct)
    {
        await _saveLock.WaitAsync(ct).ConfigureAwait(false);
        var newFolders = new List<string>();

        try
        {
            var guard = new ProjectWritePathGuard(projectRoot);
            var root = guard.EnsureSafeDirectoryTarget(
                DossierFolderPlanner.ResolveRoot(projectRoot));

            EnsureDossierFolders(projectRoot, root, document, guard, newFolders);
        }
        catch (Exception ex)
        {
            RollbackEmptyFolders(projectRoot, newFolders);
            throw new DossierFolderProvisionException(
                "Die Dossier-Datei ist lesbar, aber mindestens ein "
                + "Liegenschaftsordner konnte nicht angelegt werden.",
                ex);
        }
        finally
        {
            _saveLock.Release();
        }
    }

    /// <summary>
    /// Legt die Ordner aller gespeicherten Liegenschaften an. So gilt dieselbe
    /// Regel fuer Einzel- und Stapelanlage, ohne Dateilogik im ViewModel.
    /// </summary>
    private static void EnsureDossierFolders(
        string projectRoot,
        string dossierRoot,
        DossierDocument document,
        ProjectWritePathGuard guard,
        List<string> newFolders)
    {
        foreach (var dossier in document.Dossiers)
        {
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

            newFolders.Add(folder);
            Directory.CreateDirectory(folder);
            guard.EnsureSafeDirectoryTarget(folder);
        }
    }

    /// <summary>
    /// Bei einem Speicherfehler werden nur gerade neu erzeugte und weiterhin
    /// leere Ordner entfernt. Vorhandene Ordner und Benutzerdateien bleiben.
    /// </summary>
    private static void RollbackEmptyFolders(
        string projectRoot,
        IReadOnlyList<string> newFolders)
    {
        for (var index = newFolders.Count - 1; index >= 0; index--)
        {
            var folder = newFolders[index];
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
