using AuswertungPro.Next.Application.Ai.Training;

namespace AuswertungPro.Next.Infrastructure.Ai.Training;

/// <summary>
/// Erkennt unterbrochene Gold-Commits und stellt den letzten eindeutigen Stand wieder her.
/// </summary>
internal static class PersonalGoldBrainCommitRecovery
{
    internal static async Task RecoverPendingAsync(
        PersonalGoldBrainSeparationRequest request,
        PersonalGoldBrainResolvedPaths declaredPaths,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        PersonalGoldBrainFileService.EnsureMutationPathIsSafe(
            declaredPaths.LocalSafetyRoot,
            declaredPaths.CommitJournalPath);
        if (Directory.Exists(declaredPaths.CommitJournalPath))
        {
            throw new InvalidDataException(
                $"Commit-Journal ist unerwartet ein Ordner: {declaredPaths.CommitJournalPath}");
        }
        if (!File.Exists(declaredPaths.CommitJournalPath))
            return;

        var journal = await PersonalGoldBrainCommitJournalStore
            .ReadAsync(declaredPaths)
            .ConfigureAwait(false);
        var recoveryPaths = PersonalGoldBrainCommitJournalStore.ResolveJournalPaths(
            request,
            declaredPaths,
            journal);
        await RecoverAsync(journal, recoveryPaths).ConfigureAwait(false);
    }

    internal static async Task RecoverAsync(
        PersonalGoldBrainCommitJournal journal,
        PersonalGoldBrainResolvedPaths paths)
    {
        EnsureRecoveryPathsAreSafe(paths);
        var state = DetermineRecoveryState(paths);
        var ownerRoot = state switch
        {
            PersonalGoldBrainCommitRecoveryState.Prepared or
                PersonalGoldBrainCommitRecoveryState.ExternalMirrorMoved or
                PersonalGoldBrainCommitRecoveryState.LocalKnowledgeMoved => paths.StagingRoot,
            PersonalGoldBrainCommitRecoveryState.ActiveKnowledgePublished => paths.KnowledgeRoot,
            PersonalGoldBrainCommitRecoveryState.Restored => null,
            _ => throw new InvalidDataException(
                "Unklarer Commit-Journal-Zustand; es wird nichts automatisch veraendert.")
        };
        if (ownerRoot is not null)
        {
            PersonalGoldBrainCommitJournalStore.ValidateCommitOwnership(
                ownerRoot,
                journal.TransactionId);
        }

        ClearDirectoryReadOnly(paths.LocalSafetyRoot, paths.LocalArchiveRoot);
        ClearDirectoryReadOnly(paths.ExternalSafetyRoot, paths.ExternalArchiveRoot);
        await ValidateArchiveArtifactsAsync(
                paths.LocalArchiveRoot,
                paths.LocalSafetyRoot,
                journal.LocalExternalContextDirectoryWasPresent,
                journal)
            .ConfigureAwait(false);
        await ValidateArchiveArtifactsAsync(
                paths.ExternalArchiveRoot,
                paths.ExternalSafetyRoot,
                journal.ExternalContextDirectoryWasPresent,
                journal)
            .ConfigureAwait(false);
        await ValidateLegacyProtocolStateAsync(journal, paths).ConfigureAwait(false);

        RemoveRollbackArtifacts(
            paths.LocalArchiveRoot,
            paths.LocalSafetyRoot,
            journal.LocalExternalContextDirectoryWasPresent);
        RemoveRollbackArtifacts(
            paths.ExternalArchiveRoot,
            paths.ExternalSafetyRoot,
            journal.ExternalContextDirectoryWasPresent);
        RestoreLegacyProtocolTraining(journal, paths);

        if (state == PersonalGoldBrainCommitRecoveryState.ActiveKnowledgePublished)
        {
            PersonalGoldBrainFileService.MoveDirectorySafe(
                paths.LocalSafetyRoot,
                paths.KnowledgeRoot,
                paths.LocalSafetyRoot,
                paths.StagingRoot);
            state = PersonalGoldBrainCommitRecoveryState.LocalKnowledgeMoved;
        }
        if (state == PersonalGoldBrainCommitRecoveryState.LocalKnowledgeMoved)
        {
            PersonalGoldBrainFileService.MoveDirectorySafe(
                paths.LocalSafetyRoot,
                paths.LocalArchiveRoot,
                paths.LocalSafetyRoot,
                paths.KnowledgeRoot);
            state = PersonalGoldBrainCommitRecoveryState.ExternalMirrorMoved;
        }
        if (state == PersonalGoldBrainCommitRecoveryState.ExternalMirrorMoved)
        {
            PersonalGoldBrainFileService.MoveDirectorySafe(
                paths.ExternalSafetyRoot,
                paths.ExternalArchiveRoot,
                paths.ExternalSafetyRoot,
                paths.ExternalMirrorRoot);
            state = PersonalGoldBrainCommitRecoveryState.Prepared;
        }

        if (state == PersonalGoldBrainCommitRecoveryState.Prepared)
            DeleteOwnedStaging(paths);
        if (DetermineRecoveryState(paths) != PersonalGoldBrainCommitRecoveryState.Restored)
        {
            throw new InvalidDataException(
                "Rollback endete nicht im eindeutig wiederhergestellten Ausgangszustand.");
        }

        PersonalGoldBrainCommitJournalStore.Delete(paths);
    }

    internal static void TryDeleteUncommittedOwnedStaging(
        PersonalGoldBrainResolvedPaths? paths)
    {
        if (paths is null)
            return;
        try
        {
            PersonalGoldBrainFileService.EnsureMutationPathIsSafe(
                paths.LocalSafetyRoot,
                paths.CommitJournalPath);
            if (File.Exists(paths.CommitJournalPath)
                || Directory.Exists(paths.CommitJournalPath)
                || !Directory.Exists(paths.StagingRoot))
            {
                return;
            }

            DeleteOwnedStaging(paths);
        }
        catch (Exception)
        {
            // Unklare Arbeitsstaende bleiben absichtlich fuer die manuelle Pruefung liegen.
        }
    }

    private static void EnsureRecoveryPathsAreSafe(PersonalGoldBrainResolvedPaths paths)
    {
        foreach (var path in new[]
                 {
                     paths.KnowledgeRoot,
                     paths.LocalArchiveRoot,
                     paths.StagingRoot,
                     paths.CommitJournalPath
                 })
        {
            PersonalGoldBrainFileService.EnsureMutationPathIsSafe(
                paths.LocalSafetyRoot,
                path);
        }

        foreach (var path in new[] { paths.ExternalMirrorRoot, paths.ExternalArchiveRoot })
        {
            PersonalGoldBrainFileService.EnsureMutationPathIsSafe(
                paths.ExternalSafetyRoot,
                path);
        }

        PersonalGoldBrainFileService.EnsureMutationPathIsSafe(
            paths.LegacySafetyRoot,
            paths.LegacyProtocolTrainingPath);
        PersonalGoldBrainFileService.EnsureMutationPathIsSafe(
            paths.LegacySafetyRoot,
            paths.DisconnectedLegacyProtocolTrainingPath);
    }

    private static PersonalGoldBrainCommitRecoveryState DetermineRecoveryState(
        PersonalGoldBrainResolvedPaths paths)
    {
        var knowledge = DirectoryExistsWithoutFileCollision(paths.KnowledgeRoot);
        var localArchive = DirectoryExistsWithoutFileCollision(paths.LocalArchiveRoot);
        var externalMirror = DirectoryExistsWithoutFileCollision(paths.ExternalMirrorRoot);
        var externalArchive = DirectoryExistsWithoutFileCollision(paths.ExternalArchiveRoot);
        var staging = DirectoryExistsWithoutFileCollision(paths.StagingRoot);

        if (knowledge && !localArchive && externalMirror && !externalArchive && staging)
            return PersonalGoldBrainCommitRecoveryState.Prepared;
        if (knowledge && !localArchive && !externalMirror && externalArchive && staging)
            return PersonalGoldBrainCommitRecoveryState.ExternalMirrorMoved;
        if (!knowledge && localArchive && !externalMirror && externalArchive && staging)
            return PersonalGoldBrainCommitRecoveryState.LocalKnowledgeMoved;
        if (knowledge && localArchive && !externalMirror && externalArchive && !staging)
            return PersonalGoldBrainCommitRecoveryState.ActiveKnowledgePublished;
        if (knowledge && !localArchive && externalMirror && !externalArchive && !staging)
            return PersonalGoldBrainCommitRecoveryState.Restored;
        return PersonalGoldBrainCommitRecoveryState.Unknown;
    }

    private static bool DirectoryExistsWithoutFileCollision(string path)
    {
        if (File.Exists(path))
        {
            throw new InvalidDataException(
                $"Unklarer Commit-Journal-Zustand: Verzeichnisziel ist eine Datei: {path}");
        }

        return Directory.Exists(path);
    }

    private static async Task ValidateArchiveArtifactsAsync(
        string archiveRoot,
        string safetyRoot,
        bool externalContextDirectoryWasPresent,
        PersonalGoldBrainCommitJournal journal)
    {
        if (!Directory.Exists(archiveRoot))
            return;

        var marker = Path.Combine(
            archiveRoot,
            PersonalGoldBrainSeparationService.LegacyArchiveMarkerFileName);
        var markerTemporary = marker + ".tmp";
        PersonalGoldBrainFileService.EnsureMutationPathIsSafe(safetyRoot, marker);
        PersonalGoldBrainFileService.EnsureMutationPathIsSafe(safetyRoot, markerTemporary);
        if (Directory.Exists(marker) || Directory.Exists(markerTemporary))
            throw new InvalidDataException("Archivmarker-Ziel ist unerwartet ein Ordner.");
        if (File.Exists(marker))
        {
            var markerLines = await File.ReadAllLinesAsync(marker, CancellationToken.None)
                .ConfigureAwait(false);
            if (!markerLines.Contains(
                    $"CommitId={journal.TransactionId}",
                    StringComparer.Ordinal))
            {
                throw new InvalidDataException(
                    "Archivmarker gehoert nicht zur offenen Commit-Transaktion.");
            }
        }

        var contextDirectory = Path.Combine(archiveRoot, "external_context");
        var protocolCopy = Path.Combine(contextDirectory, "protocol_training.json");
        var protocolTemporary = protocolCopy + ".tmp";
        PersonalGoldBrainFileService.EnsureMutationPathIsSafe(safetyRoot, contextDirectory);
        PersonalGoldBrainFileService.EnsureMutationPathIsSafe(safetyRoot, protocolCopy);
        PersonalGoldBrainFileService.EnsureMutationPathIsSafe(safetyRoot, protocolTemporary);
        if (File.Exists(contextDirectory)
            || Directory.Exists(protocolCopy)
            || Directory.Exists(protocolTemporary))
        {
            throw new InvalidDataException("Protokoll-Archivziel besitzt einen unklaren Typ.");
        }
        if (File.Exists(protocolCopy))
        {
            if (!journal.LegacyProtocolTrainingWasPresent)
                throw new InvalidDataException("Unerwartete Protokollkopie im Commit-Archiv.");
            var hash = await PersonalGoldBrainFileService
                .HashAsync(protocolCopy, CancellationToken.None)
                .ConfigureAwait(false);
            if (!hash.Equals(
                    journal.LegacyProtocolTrainingSha256,
                    StringComparison.OrdinalIgnoreCase))
            {
                throw new InvalidDataException(
                    "Protokollkopie im Commit-Archiv besitzt eine falsche Pruefsumme.");
            }
        }

        if (Directory.Exists(contextDirectory) && !externalContextDirectoryWasPresent)
        {
            var allowed = new HashSet<string>(
                new[] { protocolCopy, protocolTemporary },
                StringComparer.OrdinalIgnoreCase);
            if (Directory.EnumerateFileSystemEntries(contextDirectory)
                .Any(entry => !allowed.Contains(Path.GetFullPath(entry))))
            {
                throw new InvalidDataException(
                    "Commit-Archiv enthaelt unbekannte Daten im erzeugten Kontextordner.");
            }
        }
    }

    private static async Task ValidateLegacyProtocolStateAsync(
        PersonalGoldBrainCommitJournal journal,
        PersonalGoldBrainResolvedPaths paths)
    {
        if (Directory.Exists(paths.LegacyProtocolTrainingPath)
            || Directory.Exists(paths.DisconnectedLegacyProtocolTrainingPath))
        {
            throw new InvalidDataException(
                "Protokoll-Lernpfad besitzt im Commit-Journal-Zustand einen unklaren Typ.");
        }

        var originalExists = File.Exists(paths.LegacyProtocolTrainingPath);
        var disconnectedExists = File.Exists(paths.DisconnectedLegacyProtocolTrainingPath);
        if (!journal.LegacyProtocolTrainingWasPresent)
        {
            if (originalExists || disconnectedExists)
            {
                throw new InvalidDataException(
                    "Unerwartete Protokoll-Lerndatei im Commit-Journal-Zustand.");
            }

            return;
        }

        if (originalExists == disconnectedExists)
        {
            throw new InvalidDataException(
                "Unklarer Zustand der abgekoppelten Protokoll-Lerndatei.");
        }

        var current = originalExists
            ? paths.LegacyProtocolTrainingPath
            : paths.DisconnectedLegacyProtocolTrainingPath;
        var hash = await PersonalGoldBrainFileService.HashAsync(current, CancellationToken.None)
            .ConfigureAwait(false);
        if (!hash.Equals(
                journal.LegacyProtocolTrainingSha256,
                StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidDataException(
                "Protokoll-Lerndatei stimmt nicht mit dem Commit-Journal ueberein.");
        }
    }

    private static void RemoveRollbackArtifacts(
        string archiveRoot,
        string safetyRoot,
        bool externalContextDirectoryWasPresent)
    {
        if (!Directory.Exists(archiveRoot))
            return;

        var marker = Path.Combine(
            archiveRoot,
            PersonalGoldBrainSeparationService.LegacyArchiveMarkerFileName);
        PersonalGoldBrainFileService.DeleteFileSafe(safetyRoot, marker);
        PersonalGoldBrainFileService.DeleteFileSafe(safetyRoot, marker + ".tmp");
        var contextDirectory = Path.Combine(archiveRoot, "external_context");
        var protocolCopy = Path.Combine(contextDirectory, "protocol_training.json");
        PersonalGoldBrainFileService.DeleteFileSafe(safetyRoot, protocolCopy);
        PersonalGoldBrainFileService.DeleteFileSafe(safetyRoot, protocolCopy + ".tmp");
        if (!externalContextDirectoryWasPresent)
        {
            PersonalGoldBrainFileService.DeleteEmptyDirectorySafe(
                safetyRoot,
                contextDirectory);
        }
    }

    private static void RestoreLegacyProtocolTraining(
        PersonalGoldBrainCommitJournal journal,
        PersonalGoldBrainResolvedPaths paths)
    {
        if (!journal.LegacyProtocolTrainingWasPresent
            || File.Exists(paths.LegacyProtocolTrainingPath))
        {
            return;
        }

        PersonalGoldBrainFileService.MoveFileSafe(
            paths.LegacySafetyRoot,
            paths.DisconnectedLegacyProtocolTrainingPath,
            paths.LegacySafetyRoot,
            paths.LegacyProtocolTrainingPath);
    }

    private static void ClearDirectoryReadOnly(string safetyRoot, string path)
    {
        if (!Directory.Exists(path))
            return;
        PersonalGoldBrainFileService.EnsureMutationPathIsSafe(safetyRoot, path);
        var attributes = File.GetAttributes(path);
        if ((attributes & FileAttributes.ReadOnly) != 0)
            File.SetAttributes(path, attributes & ~FileAttributes.ReadOnly);
    }

    private static void DeleteOwnedStaging(PersonalGoldBrainResolvedPaths paths)
    {
        PersonalGoldBrainCommitJournalStore.ValidateCommitOwnership(
            paths.StagingRoot,
            paths.TransactionId);
        PersonalGoldBrainFileService.DeleteTreeSafe(
            paths.LocalSafetyRoot,
            paths.StagingRoot);
    }
}
