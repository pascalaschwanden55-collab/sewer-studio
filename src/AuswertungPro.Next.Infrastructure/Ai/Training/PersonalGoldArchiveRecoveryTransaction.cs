using AuswertungPro.Next.Application.Backup;

namespace AuswertungPro.Next.Infrastructure.Ai.Training;

/// <summary>
/// Orchestriert nur die persistente Recovery-Transaktion. Journal-I/O,
/// Validierung und Artefaktpflege liegen in getrennten internen Klassen.
/// </summary>
internal sealed class PersonalGoldArchiveRecoveryTransaction
{
    private const string TransactionJournalSuffix =
        ".gold-archive-recovery.transaction.json";

    private readonly PersonalGoldArchiveRecoveryValidator _validator;
    private readonly PersonalGoldArchiveRecoveryArtifacts _artifacts;
    private readonly PersonalGoldArchiveRecoveryJournalStore _journal;

    public PersonalGoldArchiveRecoveryTransaction(
        ISqliteSnapshotCopier sqliteSnapshots)
    {
        _validator = new PersonalGoldArchiveRecoveryValidator();
        _artifacts = new PersonalGoldArchiveRecoveryArtifacts(
            sqliteSnapshots,
            _validator);
        _journal = new PersonalGoldArchiveRecoveryJournalStore(_validator);
    }

    public static string ResolveJournalPath(string activeKnowledgeRoot)
        => PersonalGoldBrainFileService.NormalizeRoot(
               activeKnowledgeRoot,
               "Aktiver Wissensordner")
           + TransactionJournalSuffix;

    public static string GetInventoryPath(string activeRoot)
        => PersonalGoldArchiveRecoveryArtifacts.GetInventoryPath(activeRoot);

    public static string GetReceiptPath(string activeRoot)
        => PersonalGoldArchiveRecoveryArtifacts.GetReceiptPath(activeRoot);

    public static string GetManifestPath(string activeRoot)
        => PersonalGoldArchiveRecoveryArtifacts.GetManifestPath(activeRoot);

    public static string HashBytes(ReadOnlySpan<byte> bytes)
        => Convert.ToHexStringLower(
            System.Security.Cryptography.SHA256.HashData(bytes));

    public async Task<ArchiveRecoveryTransactionJournal> PrepareBackupsAsync(
        ArchiveRecoveryPaths paths,
        ArchiveRecoveryTransactionJournal transaction,
        ArchiveRecoveryPreimages preimages,
        CancellationToken cancellationToken)
    {
        var databaseHash = await _artifacts
            .PrepareBackupsAsync(
                paths,
                transaction,
                preimages,
                cancellationToken)
            .ConfigureAwait(false);
        var prepared = transaction with
        {
            Stage = ArchiveRecoveryTransactionStage.Prepared,
            DatabaseSnapshotSha256 = databaseHash
        };
        await _validator
            .ValidatePreparedArtifactsAsync(paths, prepared, cancellationToken)
            .ConfigureAwait(false);
        await _journal
            .WriteAsync(
                paths,
                prepared,
                requireMissing: false,
                cancellationToken)
            .ConfigureAwait(false);
        return prepared;
    }

    public async Task WriteJournalAsync(
        ArchiveRecoveryPaths paths,
        ArchiveRecoveryTransactionJournal transaction,
        bool requireMissing,
        CancellationToken cancellationToken)
        => await _journal
            .WriteAsync(
                paths,
                transaction,
                requireMissing,
                cancellationToken)
            .ConfigureAwait(false);

    public async Task RecoverPendingAsync(
        ArchiveRecoveryPaths paths,
        bool dryRun,
        CancellationToken cancellationToken)
    {
        if (!_journal.Exists(paths))
            return;
        if (dryRun)
        {
            throw new InvalidDataException(
                "Eine offene Archiv-Recovery-Transaktion muss zuerst im echten Lauf " +
                "wiederhergestellt werden; der Prueflauf veraendert nichts.");
        }

        var transaction = await _journal
            .ReadAsync(paths, cancellationToken)
            .ConfigureAwait(false);
        await RollBackAsync(paths, transaction).ConfigureAwait(false);
    }

    public async Task RollBackAsync(
        ArchiveRecoveryPaths paths,
        ArchiveRecoveryTransactionJournal expectedTransaction)
    {
        var transaction = await _journal
            .ReadAsync(paths, CancellationToken.None)
            .ConfigureAwait(false);
        if (!string.Equals(
                transaction.TransactionId,
                expectedTransaction.TransactionId,
                StringComparison.Ordinal))
        {
            throw new InvalidDataException(
                "Recovery-Journal wurde durch eine andere Transaktion ersetzt.");
        }

        _validator.ValidateOwnedAuditDirectory(
            transaction,
            requirePreparedBackups:
            transaction.Stage == ArchiveRecoveryTransactionStage.Prepared);
        if (transaction.Stage == ArchiveRecoveryTransactionStage.Preparing)
        {
            _artifacts.DeleteOwnedAuditDirectory(paths, transaction);
            await _journal.DeleteOwnedAsync(paths, transaction).ConfigureAwait(false);
            return;
        }

        await _validator
            .ValidatePreparedArtifactsAsync(
                paths,
                transaction,
                CancellationToken.None)
            .ConfigureAwait(false);
        await _validator
            .ValidateFrameRollbackStateAsync(transaction)
            .ConfigureAwait(false);
        await _artifacts
            .RestorePreimagesAsync(paths, transaction)
            .ConfigureAwait(false);
        _artifacts.DeleteOwnedAuditDirectory(paths, transaction);
        await _journal.DeleteOwnedAsync(paths, transaction).ConfigureAwait(false);
    }

    public async Task DeleteJournalAsync(
        ArchiveRecoveryPaths paths,
        ArchiveRecoveryTransactionJournal transaction)
        => await _journal
            .DeleteOwnedAsync(paths, transaction)
            .ConfigureAwait(false);
}

internal sealed record ArchiveRecoveryPaths(
    string ActiveRoot,
    string LegacyRoot,
    string PreviousActiveRoot,
    string ActiveSamplesPath,
    string ActiveDatabasePath,
    string LegacyDatabasePath);

internal sealed record ArchiveRecoveryPreimages(
    byte[] Samples,
    ArchiveRecoveryOptionalBytes Inventory,
    ArchiveRecoveryOptionalBytes Receipt,
    ArchiveRecoveryOptionalBytes Manifest);

internal sealed record ArchiveRecoveryOptionalBytes(
    bool WasPresent,
    byte[]? Bytes,
    string? Sha256);

internal sealed record ArchiveRecoveryFramePlan(
    string SampleId,
    string SourcePath,
    string TargetPath,
    string Sha256,
    bool TargetWasPresent);

internal sealed record ArchiveRecoveryTransactionJournal(
    int SchemaVersion,
    string TransactionId,
    ArchiveRecoveryTransactionStage Stage,
    DateTimeOffset StartedUtc,
    string ActiveRoot,
    string LegacyRoot,
    string ConfirmedByUser,
    string AuditDirectory,
    string ActiveDatabasePath,
    string ActiveSamplesPath,
    string InventoryPath,
    string ReceiptPath,
    string ManifestPath,
    bool InventoryWasPresent,
    bool ReceiptWasPresent,
    bool ManifestWasPresent,
    string OriginalSamplesSha256,
    string? OriginalInventorySha256,
    string? OriginalReceiptSha256,
    string? OriginalManifestSha256,
    string? DatabaseSnapshotSha256,
    IReadOnlyList<ArchiveRecoveryFramePlan> Frames);

internal enum ArchiveRecoveryTransactionStage
{
    Unknown,
    Preparing,
    Prepared
}
