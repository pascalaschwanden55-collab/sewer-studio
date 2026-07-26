namespace AuswertungPro.Next.Infrastructure.Ai.Training;

/// <summary>
/// Prueft Journalbindung, Pfadgrenzen, Besitzmarker, Vorherkopien und Frame-Ziele,
/// ohne selbst Dateien zu veraendern.
/// </summary>
internal sealed class PersonalGoldArchiveRecoveryValidator
{
    public void ValidateJournal(
        ArchiveRecoveryPaths paths,
        ArchiveRecoveryTransactionJournal transaction)
    {
        if (transaction.SchemaVersion != 1)
            throw new InvalidDataException("Recovery-Journal besitzt eine unbekannte Version.");
        if (!Guid.TryParseExact(transaction.TransactionId, "N", out _))
            throw new InvalidDataException(
                "Recovery-Journal besitzt keine gueltige Transaktions-ID.");
        if (transaction.StartedUtc == default)
            throw new InvalidDataException("Recovery-Journal besitzt keinen Startzeitpunkt.");
        if (transaction.Stage is not ArchiveRecoveryTransactionStage.Preparing
            and not ArchiveRecoveryTransactionStage.Prepared)
        {
            throw new InvalidDataException(
                "Recovery-Journal besitzt einen unbekannten Zustand.");
        }
        if (string.IsNullOrWhiteSpace(transaction.ConfirmedByUser))
            throw new InvalidDataException("Recovery-Journal besitzt keinen Bestaetiger.");

        EnsureCanonicalPath(transaction.ActiveRoot, paths.ActiveRoot, "ActiveRoot");
        EnsureCanonicalPath(transaction.LegacyRoot, paths.LegacyRoot, "LegacyRoot");
        EnsureCanonicalPath(
            transaction.ActiveDatabasePath,
            paths.ActiveDatabasePath,
            "ActiveDatabasePath");
        EnsureCanonicalPath(
            transaction.ActiveSamplesPath,
            paths.ActiveSamplesPath,
            "ActiveSamplesPath");
        EnsureCanonicalPath(
            transaction.InventoryPath,
            PersonalGoldArchiveRecoveryArtifacts.GetInventoryPath(paths.ActiveRoot),
            "InventoryPath");
        EnsureCanonicalPath(
            transaction.ReceiptPath,
            PersonalGoldArchiveRecoveryArtifacts.GetReceiptPath(paths.ActiveRoot),
            "ReceiptPath");
        EnsureCanonicalPath(
            transaction.ManifestPath,
            PersonalGoldArchiveRecoveryArtifacts.GetManifestPath(paths.ActiveRoot),
            "ManifestPath");
        EnsureCanonicalPath(
            transaction.AuditDirectory,
            Path.Combine(
                paths.ActiveRoot,
                "training",
                "gold_migrations",
                $"archive_recovery_{transaction.StartedUtc:yyyyMMdd_HHmmss}_" +
                transaction.TransactionId),
            "AuditDirectory");

        ValidateSha256(transaction.OriginalSamplesSha256, "Samples");
        ValidateOptionalPreimage(
            transaction.InventoryWasPresent,
            transaction.OriginalInventorySha256,
            "Inventar");
        ValidateOptionalPreimage(
            transaction.ReceiptWasPresent,
            transaction.OriginalReceiptSha256,
            "Beleg");
        ValidateOptionalPreimage(
            transaction.ManifestWasPresent,
            transaction.OriginalManifestSha256,
            "Manifest");
        if (transaction.Stage == ArchiveRecoveryTransactionStage.Prepared)
            ValidateSha256(transaction.DatabaseSnapshotSha256, "Datenbank-Snapshot");
        else if (transaction.DatabaseSnapshotSha256 is not null)
            throw new InvalidDataException(
                "Vorbereitendes Recovery-Journal enthaelt unerwartet einen Datenbank-Hash.");

        var goldFramesRoot = Path.Combine(paths.ActiveRoot, "gold_frames");
        var sampleIds = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var targetPaths = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var frame in transaction.Frames
                 ?? throw new InvalidDataException(
                     "Recovery-Journal besitzt keine Frame-Liste."))
        {
            if (string.IsNullOrWhiteSpace(frame.SampleId)
                || !sampleIds.Add(frame.SampleId))
            {
                throw new InvalidDataException(
                    "Recovery-Journal enthaelt leere oder doppelte Frame-Sample-IDs.");
            }
            EnsureCanonicalSelfPath(frame.SourcePath, "FrameSourcePath");
            EnsureCanonicalSelfPath(frame.TargetPath, "FrameTargetPath");
            if (!PersonalGoldBrainFileService.IsInside(goldFramesRoot, frame.TargetPath)
                || !targetPaths.Add(frame.TargetPath))
            {
                throw new InvalidDataException(
                    "Recovery-Journal enthaelt einen unsicheren oder doppelten Frame-Zielpfad.");
            }
            ValidateSha256(frame.Sha256, $"Frame {frame.SampleId}");
        }
        EnsureMutationPathsAreSafe(paths, transaction);
    }

    public async Task ValidatePreparedArtifactsAsync(
        ArchiveRecoveryPaths paths,
        ArchiveRecoveryTransactionJournal transaction,
        CancellationToken cancellationToken)
    {
        if (transaction.Stage != ArchiveRecoveryTransactionStage.Prepared)
            throw new InvalidDataException(
                "Recovery-Transaktion ist noch nicht vollstaendig vorbereitet.");
        ValidateOwnedAuditDirectory(transaction, requirePreparedBackups: true);
        await ValidateFileHashAsync(
                PersonalGoldArchiveRecoveryArtifacts.GetDatabaseBackupPath(transaction),
                transaction.DatabaseSnapshotSha256!,
                "Datenbank-Snapshot",
                cancellationToken)
            .ConfigureAwait(false);
        await ValidateFileHashAsync(
                PersonalGoldArchiveRecoveryArtifacts.GetSamplesBackupPath(transaction),
                transaction.OriginalSamplesSha256,
                "Training-Samples-Sicherung",
                cancellationToken)
            .ConfigureAwait(false);
        await ValidateOptionalBackupAsync(
                PersonalGoldArchiveRecoveryArtifacts.GetInventoryBackupPath(transaction),
                transaction.InventoryWasPresent,
                transaction.OriginalInventorySha256,
                "Inventar-Sicherung",
                cancellationToken)
            .ConfigureAwait(false);
        await ValidateOptionalBackupAsync(
                PersonalGoldArchiveRecoveryArtifacts.GetReceiptBackupPath(transaction),
                transaction.ReceiptWasPresent,
                transaction.OriginalReceiptSha256,
                "Beleg-Sicherung",
                cancellationToken)
            .ConfigureAwait(false);
        await ValidateOptionalBackupAsync(
                PersonalGoldArchiveRecoveryArtifacts.GetManifestBackupPath(transaction),
                transaction.ManifestWasPresent,
                transaction.OriginalManifestSha256,
                "Manifest-Sicherung",
                cancellationToken)
            .ConfigureAwait(false);
        EnsureMutationPathsAreSafe(paths, transaction);
    }

    public void ValidateOwnedAuditDirectory(
        ArchiveRecoveryTransactionJournal transaction,
        bool requirePreparedBackups)
    {
        if (File.Exists(transaction.AuditDirectory))
            throw new InvalidDataException(
                "Recovery-Pruefpfad ist unerwartet eine Datei.");
        if (!Directory.Exists(transaction.AuditDirectory))
        {
            if (requirePreparedBackups)
                throw new InvalidDataException("Recovery-Pruefpfad fehlt.");
            return;
        }
        PersonalGoldBrainFileService.EnsureMutationTreeIsSafe(
            transaction.AuditDirectory);

        var ownerPath =
            PersonalGoldArchiveRecoveryArtifacts.GetOwnerMarkerPath(transaction);
        if (!File.Exists(ownerPath) || Directory.Exists(ownerPath))
        {
            throw new InvalidDataException(
                "Recovery-Pruefpfad besitzt keinen gueltigen Besitzmarker.");
        }
        if (!string.Equals(
                File.ReadAllText(ownerPath),
                $"TransactionId={transaction.TransactionId}{Environment.NewLine}",
                StringComparison.Ordinal))
        {
            throw new InvalidDataException(
                "Recovery-Pruefpfad besitzt einen fremden Besitzmarker.");
        }

        var allowed =
            PersonalGoldArchiveRecoveryArtifacts.GetAllowedAuditPaths(transaction);
        var unknown = Directory
            .EnumerateFileSystemEntries(transaction.AuditDirectory)
            .Select(Path.GetFullPath)
            .FirstOrDefault(entry => !allowed.Contains(entry));
        if (unknown is not null)
        {
            throw new InvalidDataException(
                $"Recovery-Pruefpfad enthaelt ein fremdes Artefakt: " +
                Path.GetFileName(unknown));
        }
    }

    public async Task ValidateFrameRollbackStateAsync(
        ArchiveRecoveryTransactionJournal transaction)
    {
        foreach (var frame in transaction.Frames)
        {
            if (Directory.Exists(frame.TargetPath))
            {
                throw new InvalidDataException(
                    $"Goldbild-Ziel ist unerwartet ein Ordner: {frame.TargetPath}");
            }
            if (!File.Exists(frame.TargetPath))
            {
                if (frame.TargetWasPresent)
                {
                    throw new InvalidDataException(
                        $"Vorhandenes Goldbild fehlt waehrend der Recovery: {frame.SampleId}");
                }
                continue;
            }

            var currentHash = await PersonalGoldBrainFileService
                .HashAsync(frame.TargetPath, CancellationToken.None)
                .ConfigureAwait(false);
            if (!currentHash.Equals(frame.Sha256, StringComparison.OrdinalIgnoreCase))
            {
                throw new InvalidDataException(
                    $"Goldbild wurde ausserhalb der Recovery veraendert: {frame.SampleId}");
            }
        }
    }

    private static async Task ValidateOptionalBackupAsync(
        string path,
        bool wasPresent,
        string? expectedHash,
        string name,
        CancellationToken cancellationToken)
    {
        if (!wasPresent)
        {
            if (File.Exists(path) || Directory.Exists(path))
                throw new InvalidDataException($"Unerwartete {name} vorhanden.");
            return;
        }
        await ValidateFileHashAsync(path, expectedHash!, name, cancellationToken)
            .ConfigureAwait(false);
    }

    private static async Task ValidateFileHashAsync(
        string path,
        string expectedHash,
        string name,
        CancellationToken cancellationToken)
    {
        if (Directory.Exists(path) || !File.Exists(path))
            throw new InvalidDataException($"{name} fehlt oder ist kein regulaeres File.");
        var actual = await PersonalGoldBrainFileService
            .HashAsync(path, cancellationToken)
            .ConfigureAwait(false);
        if (!actual.Equals(expectedHash, StringComparison.OrdinalIgnoreCase))
            throw new InvalidDataException($"{name} besitzt eine falsche Pruefsumme.");
    }

    private static void EnsureMutationPathsAreSafe(
        ArchiveRecoveryPaths paths,
        ArchiveRecoveryTransactionJournal transaction)
    {
        foreach (var path in new[]
                 {
                     transaction.ActiveDatabasePath,
                     transaction.ActiveSamplesPath,
                     transaction.InventoryPath,
                     transaction.ReceiptPath,
                     transaction.ManifestPath,
                     transaction.AuditDirectory
                 })
        {
            PersonalGoldBrainFileService.EnsureMutationPathIsSafe(
                paths.ActiveRoot,
                path);
        }
        foreach (var frame in transaction.Frames)
        {
            PersonalGoldBrainFileService.EnsureMutationPathIsSafe(
                paths.ActiveRoot,
                frame.TargetPath);
        }
    }

    private static void EnsureCanonicalPath(
        string actual,
        string expected,
        string name)
    {
        if (string.IsNullOrWhiteSpace(actual)
            || !string.Equals(
                Path.GetFullPath(actual),
                Path.GetFullPath(expected),
                StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidDataException(
                $"Recovery-Journal enthaelt einen nicht kanonischen Pfad: {name}.");
        }
    }

    private static void EnsureCanonicalSelfPath(string path, string name)
    {
        if (string.IsNullOrWhiteSpace(path)
            || !string.Equals(
                path,
                Path.GetFullPath(path),
                StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidDataException(
                $"Recovery-Journal enthaelt einen nicht kanonischen Pfad: {name}.");
        }
    }

    private static void ValidateOptionalPreimage(
        bool wasPresent,
        string? sha256,
        string name)
    {
        if (wasPresent)
            ValidateSha256(sha256, name);
        else if (sha256 is not null)
            throw new InvalidDataException(
                $"Recovery-Journal enthaelt einen unerwarteten {name}-Hash.");
    }

    private static void ValidateSha256(string? value, string name)
    {
        if (value is not { Length: 64 }
            || value.Any(character => !Uri.IsHexDigit(character)))
        {
            throw new InvalidDataException(
                $"Recovery-Journal besitzt keinen gueltigen {name}-Hash.");
        }
    }
}
