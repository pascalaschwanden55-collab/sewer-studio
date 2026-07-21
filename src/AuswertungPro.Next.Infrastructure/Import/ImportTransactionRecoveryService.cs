using System.IO;
using AuswertungPro.Next.Application.Import;

namespace AuswertungPro.Next.Infrastructure.Import;

/// <summary>
/// Stellt beim Projekt-Laden den Alles-oder-nichts-Zustand einer unterbrochenen
/// Import-Transaktion her. Existiert der Marker (<see cref="FileImportTransactionJournal"/>)
/// noch, starb der Prozess mitten drin: bei passender Commit-TxId nur aufraeumen, sonst die
/// veroeffentlichten Dateien SHA-verifiziert zurueckrollen. Idempotent (der Marker wird am
/// Ende geloescht).
/// </summary>
public sealed class ImportTransactionRecoveryService : IImportTransactionRecoveryService
{
    private readonly IImportTransactionJournal _journal;

    public ImportTransactionRecoveryService(IImportTransactionJournal journal)
        => _journal = journal ?? throw new ArgumentNullException(nameof(journal));

    public ImportRecoveryResult RecoverIfNeeded(string projectRoot, string? committedImportTxId)
    {
        if (string.IsNullOrWhiteSpace(projectRoot))
            return new ImportRecoveryResult(ImportRecoveryOutcome.None, null);

        var marker = _journal.TryRead(projectRoot);
        if (marker is null)
            return new ImportRecoveryResult(ImportRecoveryOutcome.None, null);

        if (string.Equals(marker.TxId, committedImportTxId, StringComparison.Ordinal))
        {
            // Der atomare projekt.json-Save ist durchgelaufen (Absturz erst danach) —
            // der neue Zustand ist konsistent, nur Arbeitsordner + Marker aufraeumen.
            CleanupStaging(marker.StagingRoot);
            _journal.Clear(projectRoot);
            return new ImportRecoveryResult(
                ImportRecoveryOutcome.CompletedCleanup,
                $"Ein abgeschlossener Import vom {marker.StartedUtc.ToLocalTime():g} wurde aufgeraeumt.");
        }

        // Commit ist NICHT durchgelaufen: die veroeffentlichten Dateien zuruecknehmen,
        // aber nur, wenn ihr Inhalt seit der Veroeffentlichung unveraendert ist.
        var rolledBack = 0;
        foreach (var target in marker.PublishedTargets)
        {
            var path = Path.Combine(projectRoot, target.RelativePath);
            if (TryRollbackFile(path, target.Sha256))
                rolledBack++;
        }

        CleanupStaging(marker.StagingRoot);
        _journal.Clear(projectRoot);
        return new ImportRecoveryResult(
            ImportRecoveryOutcome.RolledBack,
            $"Ein unvollstaendiger Import vom {marker.StartedUtc.ToLocalTime():g} wurde zurueckgenommen " +
            $"({rolledBack} Datei(en)).");
    }

    private static bool TryRollbackFile(string path, string expectedSha)
    {
        try
        {
            if (!File.Exists(path))
                return false;

            var currentSha = VerifiedImportFileCopy.ComputeSha256(path);
            if (!currentSha.Equals(expectedSha, StringComparison.OrdinalIgnoreCase))
                return false;   // inzwischen anderweitig veraendert -> nicht loeschen

            File.Delete(path);
            return true;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            return false;
        }
    }

    private static void CleanupStaging(string stagingRoot)
    {
        try
        {
            if (!string.IsNullOrWhiteSpace(stagingRoot) && Directory.Exists(stagingRoot))
                Directory.Delete(stagingRoot, recursive: true);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            // Rest-Arbeitsordner ist unkritisch; naechster Lauf raeumt erneut auf.
        }
    }
}
