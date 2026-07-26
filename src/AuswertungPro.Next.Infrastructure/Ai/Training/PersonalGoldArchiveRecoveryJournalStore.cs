using System.Text.Json;
using AuswertungPro.Next.Application.Common;

namespace AuswertungPro.Next.Infrastructure.Ai.Training;

/// <summary>Schreibt, liest und entfernt ausschliesslich das atomare Recovery-Journal.</summary>
internal sealed class PersonalGoldArchiveRecoveryJournalStore(
    PersonalGoldArchiveRecoveryValidator validator)
{
    public bool Exists(ArchiveRecoveryPaths paths)
    {
        var journalPath = PersonalGoldArchiveRecoveryTransaction.ResolveJournalPath(
            paths.ActiveRoot);
        PersonalGoldBrainFileService.EnsureMutationPathIsSafe(
            GetJournalSafetyRoot(paths),
            journalPath);
        if (Directory.Exists(journalPath))
            throw new InvalidDataException(
                $"Recovery-Journal ist unerwartet ein Ordner: {journalPath}");
        return File.Exists(journalPath);
    }

    public async Task WriteAsync(
        ArchiveRecoveryPaths paths,
        ArchiveRecoveryTransactionJournal transaction,
        bool requireMissing,
        CancellationToken cancellationToken)
    {
        var journalPath = PersonalGoldArchiveRecoveryTransaction.ResolveJournalPath(
            paths.ActiveRoot);
        var journalSafetyRoot = GetJournalSafetyRoot(paths);
        PersonalGoldBrainFileService.EnsureMutationPathIsSafe(
            journalSafetyRoot,
            journalPath);
        if (Directory.Exists(journalPath))
            throw new InvalidDataException(
                $"Recovery-Journal ist unerwartet ein Ordner: {journalPath}");
        if (requireMissing && File.Exists(journalPath))
            throw new IOException($"Recovery-Journal existiert bereits: {journalPath}");

        await PersonalGoldBrainFileService
            .WriteJsonAsync(
                journalPath,
                transaction,
                journalSafetyRoot,
                cancellationToken)
            .ConfigureAwait(false);
        var saved = await ReadAsync(paths, cancellationToken).ConfigureAwait(false);
        if (!string.Equals(
                saved.TransactionId,
                transaction.TransactionId,
                StringComparison.Ordinal)
            || saved.Stage != transaction.Stage)
        {
            throw new InvalidDataException(
                "Das atomar geschriebene Recovery-Journal konnte nicht verifiziert werden.");
        }
    }

    public async Task<ArchiveRecoveryTransactionJournal> ReadAsync(
        ArchiveRecoveryPaths paths,
        CancellationToken cancellationToken)
    {
        var journalPath = PersonalGoldArchiveRecoveryTransaction.ResolveJournalPath(
            paths.ActiveRoot);
        PersonalGoldBrainFileService.EnsureMutationPathIsSafe(
            GetJournalSafetyRoot(paths),
            journalPath);
        byte[] bytes;
        try
        {
            bytes = await PersonalGoldMigrationFileService
                .ReadStableBytesAsync(journalPath, cancellationToken)
                .ConfigureAwait(false);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            throw new InvalidDataException(
                $"Recovery-Journal konnte nicht sicher gelesen werden: {journalPath}",
                ex);
        }

        ArchiveRecoveryTransactionJournal transaction;
        try
        {
            transaction = JsonSerializer.Deserialize<ArchiveRecoveryTransactionJournal>(
                              bytes,
                              JsonDefaults.CaseInsensitive)
                          ?? throw new InvalidDataException("Recovery-Journal ist leer.");
        }
        catch (JsonException ex)
        {
            throw new InvalidDataException("Recovery-Journal ist unlesbar.", ex);
        }
        validator.ValidateJournal(paths, transaction);
        return transaction;
    }

    public async Task DeleteOwnedAsync(
        ArchiveRecoveryPaths paths,
        ArchiveRecoveryTransactionJournal transaction)
    {
        var current = await ReadAsync(paths, CancellationToken.None)
            .ConfigureAwait(false);
        if (!string.Equals(
                current.TransactionId,
                transaction.TransactionId,
                StringComparison.Ordinal))
        {
            throw new InvalidDataException(
                "Recovery-Journal gehoert nicht mehr zur laufenden Transaktion.");
        }
        PersonalGoldBrainFileService.DeleteFileSafe(
            GetJournalSafetyRoot(paths),
            PersonalGoldArchiveRecoveryTransaction.ResolveJournalPath(
                paths.ActiveRoot));
    }

    private static string GetJournalSafetyRoot(ArchiveRecoveryPaths paths)
        => Path.GetDirectoryName(paths.ActiveRoot)
           ?? throw new InvalidDataException(
               "Aktiver Wissensordner besitzt keinen Elternordner.");
}
