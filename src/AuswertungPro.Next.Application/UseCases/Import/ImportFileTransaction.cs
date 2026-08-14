using AuswertungPro.Next.Application.Import;
using AuswertungPro.Next.Domain.Models;

namespace AuswertungPro.Next.Application.UseCases.Import;

/// <summary>Ergebnis des sicheren Abschlusses einer Import-Dateitransaktion.</summary>
public sealed record ImportFileTransactionCleanupResult(
    bool StagingCleanupSucceeded,
    Exception? StagingCleanupError);

/// <summary>
/// Gemeinsamer Ablauf fuer manuelle und Ein-Knopf-Importe:
/// Marker schreiben, Dateien veroeffentlichen, Transaktions-ID im Projekt setzen
/// und den Marker erst nach dauerhaftem Speichern entfernen.
/// </summary>
public sealed class ImportFileTransaction
{
    private readonly string _label;
    private readonly IImportTransactionJournal? _journal;
    private bool _projectCommitted;
    private bool _projectSaved;
    private bool _cleanedUp;

    public ImportFileTransaction(
        string label,
        IImportFileStagingSession? fileStaging,
        IImportTransactionJournal? journal)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(label);
        _label = label;
        FileStaging = fileStaging;
        _journal = journal;
        TxId = Guid.NewGuid().ToString("N");
    }

    public string TxId { get; }

    public IImportFileStagingSession? FileStaging { get; }

    public void Publish()
    {
        if (FileStaging is null)
            return;

        WriteMarker(FileStaging.PreparedFiles);
        FileStaging.Publish();
        WriteMarker(FileStaging.PublishedFiles);
    }

    public void StampProject(Project project)
    {
        ArgumentNullException.ThrowIfNull(project);
        if (FileStaging is not null && _journal is not null)
            project.LastCommittedImportTxId = TxId;
    }

    /// <summary>
    /// Erst nach erfolgreichem Austausch des Live-Projekts aufrufen. Ab hier darf
    /// ein altes Datei-Ledger nichts mehr loeschen; bei Speicherfehlern bleibt der
    /// persistente Marker fuer die Wiederherstellung liegen.
    /// </summary>
    public void MarkProjectCommitted()
    {
        _projectCommitted = true;
        FileStaging?.Accept();
    }

    public void MarkProjectSaved() => _projectSaved = true;

    public ImportFileTransactionCleanupResult Cleanup()
    {
        if (_cleanedUp)
            return new ImportFileTransactionCleanupResult(true, null);
        _cleanedUp = true;

        Exception? cleanupError = null;
        try
        {
            FileStaging?.Dispose();
        }
        catch (Exception ex)
        {
            cleanupError = ex;
        }

        var transactionIsDurable = !_projectCommitted || _projectSaved;
        if (FileStaging is not null
            && _journal is not null
            && cleanupError is null
            && transactionIsDurable)
        {
            try
            {
                _journal.Clear(FileStaging.ProjectRoot);
            }
            catch
            {
                // Ein Marker-Rest ist sicher: die Wiederherstellung ist idempotent
                // und prueft die gespeicherte Transaktions-ID vor jedem Loeschen.
            }
        }

        return new ImportFileTransactionCleanupResult(cleanupError is null, cleanupError);
    }

    private void WriteMarker(IReadOnlyList<PublishedFileInfo> files)
    {
        if (FileStaging is null || _journal is null)
            return;

        _journal.Begin(FileStaging.ProjectRoot, new ImportTransactionMarker(
            TxId,
            DateTime.UtcNow,
            _label,
            FileStaging.StagingRoot,
            files,
            RestorePointPath: null));
    }
}
