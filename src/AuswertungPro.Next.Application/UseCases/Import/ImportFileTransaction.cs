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
                // NUR den eigenen Marker aufraeumen. Frueher loeschte jeder abgebrochene
                // Import den Marker, der gerade dalag - auch den einer frueheren
                // Transaktion, deren Speichern fehlgeschlagen war. Damit verschwand der
                // Beweis, welche Dateien noch zurueckzunehmen sind. Das braucht keinen
                // Absturz: ein Speicherfehler und ein danach abgebrochener zweiter Import
                // genuegen (gemessen in ImportFileTransactionMarkerOwnershipTests).
                //
                // Fail-closed: geloescht wird nur ein Marker, der sich positiv als der
                // eigene ausweist. Ein fehlender Marker ist nichts zu tun, ein nicht
                // lesbarer bleibt liegen und die Wiederherstellung prueft ihn beim
                // naechsten Projektoeffnen.
                var vorhanden = _journal.TryRead(FileStaging.ProjectRoot);
                if (vorhanden is not null
                    && string.Equals(vorhanden.TxId, TxId, StringComparison.Ordinal))
                {
                    _journal.Clear(FileStaging.ProjectRoot);
                }
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

        EnsureNoForeignMarker();

        _journal.Begin(FileStaging.ProjectRoot, new ImportTransactionMarker(
            TxId,
            DateTime.UtcNow,
            _label,
            FileStaging.StagingRoot,
            files,
            RestorePointPath: null));
    }

    /// <summary>
    /// Es gibt genau einen Marker je Projekt. Liegt dort noch der einer fremden,
    /// unabgeschlossenen Transaktion, darf dieser Lauf ihn nicht ueberschreiben -
    /// sonst geht die Rollback-Information des frueheren Imports verloren.
    ///
    /// Der Weg heraus ist das Projekt neu zu laden: dort prueft die Wiederherstellung
    /// den offenen Marker und meldet, was noch im Weg ist.
    /// </summary>
    private void EnsureNoForeignMarker()
    {
        if (FileStaging is null || _journal is null)
            return;

        ImportTransactionMarker? vorhanden;
        try
        {
            vorhanden = _journal.TryRead(FileStaging.ProjectRoot);
        }
        catch
        {
            // Nicht lesbar heisst nicht "nicht vorhanden": fail-closed abbrechen.
            throw new InvalidOperationException(
                "Im Projekt liegt ein Import-Wiederherstellungsmarker, der nicht gelesen "
                + "werden kann. Bitte das Projekt neu laden, damit die Wiederherstellung "
                + "ihn pruefen kann.");
        }

        if (vorhanden is null
            || string.Equals(vorhanden.TxId, TxId, StringComparison.Ordinal))
        {
            return;
        }

        throw new InvalidOperationException(
            $"Im Projekt liegt noch eine unabgeschlossene Import-Transaktion vom "
            + $"{vorhanden.StartedUtc.ToLocalTime():g}. Bitte das Projekt zuerst neu laden - "
            + "die Wiederherstellung prueft den offenen Import und meldet, was zu tun ist. "
            + "Erst danach ist ein neuer Import moeglich.");
    }
}
