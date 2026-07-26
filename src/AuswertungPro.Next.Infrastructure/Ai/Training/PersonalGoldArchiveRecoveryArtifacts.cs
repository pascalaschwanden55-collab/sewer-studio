using AuswertungPro.Next.Application.Backup;

namespace AuswertungPro.Next.Infrastructure.Ai.Training;

/// <summary>
/// Erzeugt und restauriert ausschliesslich die eigenen Recovery-Vorherkopien
/// und entfernt nur durch Besitzmarker belegte Transaktionsartefakte.
/// </summary>
internal sealed class PersonalGoldArchiveRecoveryArtifacts(
    ISqliteSnapshotCopier sqliteSnapshots,
    PersonalGoldArchiveRecoveryValidator validator)
{
    private const string TransactionOwnerFileName =
        ".sewerstudio-gold-archive-recovery-owner";
    private const string DatabaseBackupFileName = "KnowledgeBase.before.db";
    private const string SamplesBackupFileName = "training_samples.before.json";
    private const string InventoryBackupFileName = "main_code_inventory.before.json";
    private const string ReceiptBackupFileName = "archive_recovery_receipt.before.json";
    private const string ManifestBackupFileName = "gold_brain_files.before.json";
    private const string RecoveryReceiptFileName = "gold_brain_archive_recovery_v1.json";

    public static string GetInventoryPath(string activeRoot)
        => Path.Combine(
            activeRoot,
            "training",
            "gold_standard",
            "main_code_inventory_v1.json");

    public static string GetReceiptPath(string activeRoot)
        => Path.Combine(
            activeRoot,
            "training",
            "gold_standard",
            RecoveryReceiptFileName);

    public static string GetManifestPath(string activeRoot)
        => Path.GetFullPath(Path.Combine(
            activeRoot,
            PersonalGoldBrainManifestWriter.RelativePath.Replace(
                '/',
                Path.DirectorySeparatorChar)));

    public static string GetOwnerMarkerPath(
        ArchiveRecoveryTransactionJournal transaction)
        => Path.Combine(transaction.AuditDirectory, TransactionOwnerFileName);

    public static string GetDatabaseBackupPath(
        ArchiveRecoveryTransactionJournal transaction)
        => Path.Combine(transaction.AuditDirectory, DatabaseBackupFileName);

    public static string GetSamplesBackupPath(
        ArchiveRecoveryTransactionJournal transaction)
        => Path.Combine(transaction.AuditDirectory, SamplesBackupFileName);

    public static string GetInventoryBackupPath(
        ArchiveRecoveryTransactionJournal transaction)
        => Path.Combine(transaction.AuditDirectory, InventoryBackupFileName);

    public static string GetReceiptBackupPath(
        ArchiveRecoveryTransactionJournal transaction)
        => Path.Combine(transaction.AuditDirectory, ReceiptBackupFileName);

    public static string GetManifestBackupPath(
        ArchiveRecoveryTransactionJournal transaction)
        => Path.Combine(transaction.AuditDirectory, ManifestBackupFileName);

    public static HashSet<string> GetAllowedAuditPaths(
        ArchiveRecoveryTransactionJournal transaction)
        => new(
            new[]
            {
                GetOwnerMarkerPath(transaction),
                GetDatabaseBackupPath(transaction),
                GetSamplesBackupPath(transaction),
                GetInventoryBackupPath(transaction),
                GetReceiptBackupPath(transaction),
                GetManifestBackupPath(transaction)
            }.Select(Path.GetFullPath),
            StringComparer.OrdinalIgnoreCase);

    public async Task<string> PrepareBackupsAsync(
        ArchiveRecoveryPaths paths,
        ArchiveRecoveryTransactionJournal transaction,
        ArchiveRecoveryPreimages preimages,
        CancellationToken cancellationToken)
    {
        PersonalGoldBrainFileService.EnsureMutationPathIsSafe(
            paths.ActiveRoot,
            transaction.AuditDirectory);
        if (Directory.Exists(transaction.AuditDirectory)
            || File.Exists(transaction.AuditDirectory))
        {
            throw new IOException(
                $"Nachhol-Pruefpfad existiert bereits: {transaction.AuditDirectory}");
        }

        PersonalGoldBrainFileService.CreateDirectorySafe(
            paths.ActiveRoot,
            transaction.AuditDirectory);
        await PersonalGoldBrainFileService
            .WriteTextAsync(
                GetOwnerMarkerPath(transaction),
                $"TransactionId={transaction.TransactionId}{Environment.NewLine}",
                paths.ActiveRoot,
                cancellationToken)
            .ConfigureAwait(false);
        await WriteNewDurableBytesAsync(
                paths.ActiveRoot,
                GetSamplesBackupPath(transaction),
                preimages.Samples,
                cancellationToken)
            .ConfigureAwait(false);
        await WriteOptionalBackupAsync(
                paths.ActiveRoot,
                GetInventoryBackupPath(transaction),
                preimages.Inventory,
                cancellationToken)
            .ConfigureAwait(false);
        await WriteOptionalBackupAsync(
                paths.ActiveRoot,
                GetReceiptBackupPath(transaction),
                preimages.Receipt,
                cancellationToken)
            .ConfigureAwait(false);
        await WriteOptionalBackupAsync(
                paths.ActiveRoot,
                GetManifestBackupPath(transaction),
                preimages.Manifest,
                cancellationToken)
            .ConfigureAwait(false);

        var databaseBackupPath = GetDatabaseBackupPath(transaction);
        PersonalGoldBrainFileService.EnsureMutationPathIsSafe(
            paths.ActiveRoot,
            databaseBackupPath);
        await sqliteSnapshots
            .CreateVerifiedSnapshotAsync(
                paths.ActiveDatabasePath,
                databaseBackupPath,
                null,
                cancellationToken)
            .ConfigureAwait(false);
        PersonalGoldBrainFileService.EnsureMutationPathIsSafe(
            paths.ActiveRoot,
            databaseBackupPath);
        return await PersonalGoldBrainFileService
            .HashAsync(databaseBackupPath, cancellationToken)
            .ConfigureAwait(false);
    }

    public async Task RestorePreimagesAsync(
        ArchiveRecoveryPaths paths,
        ArchiveRecoveryTransactionJournal transaction)
    {
        PersonalGoldBrainFileService.EnsureMutationPathIsSafe(
            paths.ActiveRoot,
            transaction.ActiveDatabasePath);
        await sqliteSnapshots
            .CreateVerifiedSnapshotAsync(
                GetDatabaseBackupPath(transaction),
                transaction.ActiveDatabasePath,
                null,
                CancellationToken.None)
            .ConfigureAwait(false);
        await RestoreFileAsync(
                paths.ActiveRoot,
                transaction.ActiveSamplesPath,
                GetSamplesBackupPath(transaction),
                wasPresent: true,
                transaction.OriginalSamplesSha256)
            .ConfigureAwait(false);
        await RestoreFileAsync(
                paths.ActiveRoot,
                transaction.InventoryPath,
                GetInventoryBackupPath(transaction),
                transaction.InventoryWasPresent,
                transaction.OriginalInventorySha256)
            .ConfigureAwait(false);
        await RestoreFileAsync(
                paths.ActiveRoot,
                transaction.ReceiptPath,
                GetReceiptBackupPath(transaction),
                transaction.ReceiptWasPresent,
                transaction.OriginalReceiptSha256)
            .ConfigureAwait(false);
        await RestoreFileAsync(
                paths.ActiveRoot,
                transaction.ManifestPath,
                GetManifestBackupPath(transaction),
                transaction.ManifestWasPresent,
                transaction.OriginalManifestSha256)
            .ConfigureAwait(false);

        foreach (var frame in transaction.Frames.Where(frame => !frame.TargetWasPresent))
        {
            if (!File.Exists(frame.TargetPath))
                continue;
            PersonalGoldBrainFileService.DeleteFileSafe(
                paths.ActiveRoot,
                frame.TargetPath);
            PersonalGoldBrainFileService.DeleteEmptyDirectorySafe(
                paths.ActiveRoot,
                Path.GetDirectoryName(frame.TargetPath)!);
        }
    }

    public void DeleteOwnedAuditDirectory(
        ArchiveRecoveryPaths paths,
        ArchiveRecoveryTransactionJournal transaction)
    {
        if (!Directory.Exists(transaction.AuditDirectory))
            return;
        validator.ValidateOwnedAuditDirectory(
            transaction,
            requirePreparedBackups: false);
        foreach (var path in GetAllowedAuditPaths(transaction)
                     .OrderByDescending(path => path.Length))
        {
            PersonalGoldBrainFileService.DeleteFileSafe(paths.ActiveRoot, path);
        }
        PersonalGoldBrainFileService.DeleteEmptyDirectorySafe(
            paths.ActiveRoot,
            transaction.AuditDirectory);
        if (Directory.Exists(transaction.AuditDirectory))
        {
            throw new InvalidDataException(
                "Recovery-Pruefpfad blieb nach dem sicheren Aufraeumen bestehen.");
        }
    }

    private static async Task WriteOptionalBackupAsync(
        string safetyRoot,
        string backupPath,
        ArchiveRecoveryOptionalBytes original,
        CancellationToken cancellationToken)
    {
        if (!original.WasPresent)
            return;
        if (original.Bytes is null)
            throw new InvalidDataException("Vorher-Datei ist ohne Inhalt markiert.");
        await WriteNewDurableBytesAsync(
                safetyRoot,
                backupPath,
                original.Bytes,
                cancellationToken)
            .ConfigureAwait(false);
    }

    private static async Task WriteNewDurableBytesAsync(
        string safetyRoot,
        string path,
        ReadOnlyMemory<byte> bytes,
        CancellationToken cancellationToken)
    {
        PersonalGoldBrainFileService.EnsureMutationPathIsSafe(safetyRoot, path);
        if (File.Exists(path) || Directory.Exists(path))
            throw new IOException($"Transaktionsdatei existiert bereits: {path}");
        await using var stream = new FileStream(
            path,
            FileMode.CreateNew,
            FileAccess.Write,
            FileShare.None,
            64 * 1024,
            FileOptions.Asynchronous | FileOptions.WriteThrough);
        await stream.WriteAsync(bytes, cancellationToken).ConfigureAwait(false);
        await stream.FlushAsync(cancellationToken).ConfigureAwait(false);
        stream.Flush(flushToDisk: true);
        PersonalGoldBrainFileService.EnsureMutationPathIsSafe(safetyRoot, path);
    }

    private static async Task RestoreFileAsync(
        string safetyRoot,
        string targetPath,
        string backupPath,
        bool wasPresent,
        string? expectedHash)
    {
        PersonalGoldBrainFileService.EnsureMutationPathIsSafe(safetyRoot, targetPath);
        if (!wasPresent)
        {
            if (Directory.Exists(targetPath))
            {
                throw new InvalidDataException(
                    $"Ruecksetz-Ziel ist unerwartet ein Ordner: {targetPath}");
            }
            PersonalGoldBrainFileService.DeleteFileSafe(safetyRoot, targetPath);
            return;
        }

        var bytes = await PersonalGoldMigrationFileService
            .ReadStableBytesAsync(backupPath, CancellationToken.None)
            .ConfigureAwait(false);
        if (!PersonalGoldArchiveRecoveryTransaction
                .HashBytes(bytes)
                .Equals(expectedHash, StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidDataException(
                "Vorher-Datei besitzt eine falsche Pruefsumme.");
        }
        await WriteReplacementBytesAsync(
                safetyRoot,
                targetPath,
                bytes,
                CancellationToken.None)
            .ConfigureAwait(false);
    }

    private static async Task WriteReplacementBytesAsync(
        string safetyRoot,
        string targetPath,
        ReadOnlyMemory<byte> bytes,
        CancellationToken cancellationToken)
    {
        var directory = Path.GetDirectoryName(targetPath)
                        ?? throw new InvalidDataException(
                            $"Ruecksetz-Ziel besitzt keinen Ordner: {targetPath}");
        PersonalGoldBrainFileService.CreateDirectorySafe(safetyRoot, directory);
        PersonalGoldBrainFileService.EnsureMutationPathIsSafe(safetyRoot, targetPath);
        var temporaryPath = targetPath + ".archive-recovery.tmp";
        PersonalGoldBrainFileService.EnsureMutationPathIsSafe(safetyRoot, temporaryPath);
        if (File.Exists(temporaryPath) || Directory.Exists(temporaryPath))
            throw new InvalidDataException(
                $"Ruecksetz-Temporaerpfad ist bereits belegt: {temporaryPath}");
        try
        {
            await using (var stream = new FileStream(
                             temporaryPath,
                             FileMode.CreateNew,
                             FileAccess.Write,
                             FileShare.None,
                             64 * 1024,
                             FileOptions.Asynchronous | FileOptions.WriteThrough))
            {
                await stream.WriteAsync(bytes, cancellationToken).ConfigureAwait(false);
                await stream.FlushAsync(cancellationToken).ConfigureAwait(false);
                stream.Flush(flushToDisk: true);
            }
            PersonalGoldBrainFileService.EnsureMutationPathIsSafe(
                safetyRoot,
                temporaryPath);
            PersonalGoldBrainFileService.EnsureMutationPathIsSafe(
                safetyRoot,
                targetPath);
            File.Move(temporaryPath, targetPath, overwrite: true);
        }
        finally
        {
            if (File.Exists(temporaryPath))
                PersonalGoldBrainFileService.DeleteFileSafe(safetyRoot, temporaryPath);
        }
    }
}
