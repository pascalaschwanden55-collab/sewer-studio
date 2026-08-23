using System;
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
            return await ReadAsync(path, ct).ConfigureAwait(false);
        }
        catch (OperationCanceledException)
        {
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
                    Trace.WriteLine("[Dossiers] Backup .bak geladen");
                    return backup;
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

            // Vor dem Ersetzen den letzten guten Stand als .bak sichern.
            if (File.Exists(path))
            {
                var backupPath = guard.EnsureSafeFileTarget(path + ".bak");
                BestEffort.Try(
                    () => File.Copy(path, backupPath, overwrite: true),
                    "Dossiers: Backup schreiben");
            }

            document.ModifiedNow();
            var json = JsonSerializer.Serialize(document, JsonOptions);
            await AtomicTextFileWriter.WriteAllTextAsync(path, json, ct).ConfigureAwait(false);
        }
        finally
        {
            _saveLock.Release();
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
