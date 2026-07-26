using System.Text;
using AuswertungPro.Next.Application.Ai.Training;

namespace AuswertungPro.Next.Infrastructure.Ai.Training;

/// <summary>
/// Fuehrt die drei atomaren Verzeichniswechsel der Gold-Umschaltung aus.
/// </summary>
internal sealed class PersonalGoldBrainCommitExecutor(
    Action<PersonalGoldBrainCommitStep>? commitStepObserver)
{
    internal async Task CommitAsync(
        PersonalGoldBrainSeparationRequest request,
        PersonalGoldBrainResolvedPaths paths,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var externalArchiveParent = Path.GetDirectoryName(paths.ExternalArchiveRoot)
                                    ?? throw new InvalidDataException(
                                        "Elements-Altarchiv besitzt keinen Elternordner.");
        PersonalGoldBrainFileService.CreateDirectorySafe(
            paths.ExternalSafetyRoot,
            externalArchiveParent);
        var journal = await PersonalGoldBrainCommitJournalStore
            .CreateAsync(request, paths, cancellationToken)
            .ConfigureAwait(false);
        try
        {
            PersonalGoldBrainFileService.MoveDirectorySafe(
                paths.ExternalSafetyRoot,
                paths.ExternalMirrorRoot,
                paths.ExternalSafetyRoot,
                paths.ExternalArchiveRoot);
            NotifyCommitStep(PersonalGoldBrainCommitStep.ExternalMirrorMoved);
            PersonalGoldBrainFileService.MoveDirectorySafe(
                paths.LocalSafetyRoot,
                paths.KnowledgeRoot,
                paths.LocalSafetyRoot,
                paths.LocalArchiveRoot);
            NotifyCommitStep(PersonalGoldBrainCommitStep.LocalKnowledgeMoved);
            PersonalGoldBrainFileService.MoveDirectorySafe(
                paths.LocalSafetyRoot,
                paths.StagingRoot,
                paths.LocalSafetyRoot,
                paths.KnowledgeRoot);
            NotifyCommitStep(PersonalGoldBrainCommitStep.ActiveKnowledgePublished);

            await WriteArchiveMarkerAsync(
                    paths.LocalArchiveRoot,
                    request,
                    paths,
                    paths.LocalSafetyRoot,
                    cancellationToken)
                .ConfigureAwait(false);
            await WriteArchiveMarkerAsync(
                    paths.ExternalArchiveRoot,
                    request,
                    paths,
                    paths.ExternalSafetyRoot,
                    cancellationToken)
                .ConfigureAwait(false);
            await ArchiveLegacyProtocolTrainingAsync(paths, cancellationToken)
                .ConfigureAwait(false);

            TryMarkDirectoryReadOnly(paths.LocalArchiveRoot);
            TryMarkDirectoryReadOnly(paths.ExternalArchiveRoot);
            PersonalGoldBrainCommitJournalStore.Delete(paths);
        }
        catch (CommitProcessInterruptedException)
        {
            throw;
        }
        catch (Exception commitError)
        {
            try
            {
                await PersonalGoldBrainCommitRecovery
                    .RecoverAsync(journal, paths)
                    .ConfigureAwait(false);
            }
            catch (Exception recoveryError)
            {
                throw new IOException(
                    "Gold-Gehirn-Umschaltung ist fehlgeschlagen und konnte nicht sicher " +
                    $"zurueckgerollt werden. Commit-Fehler: {commitError.Message}; " +
                    $"Rollback-Fehler: {recoveryError.Message}",
                    new AggregateException(commitError, recoveryError));
            }

            throw;
        }
    }

    private void NotifyCommitStep(PersonalGoldBrainCommitStep step)
    {
        if (commitStepObserver is null)
            return;

        try
        {
            commitStepObserver(step);
        }
        catch (Exception ex)
        {
            throw new CommitProcessInterruptedException(
                $"Simulierter Prozessabbruch nach Commit-Schritt '{step}'.",
                ex);
        }
    }

    private static async Task ArchiveLegacyProtocolTrainingAsync(
        PersonalGoldBrainResolvedPaths paths,
        CancellationToken cancellationToken)
    {
        if (!File.Exists(paths.LegacyProtocolTrainingPath))
            return;

        var localTarget = Path.Combine(
            paths.LocalArchiveRoot,
            "external_context",
            "protocol_training.json");
        var externalTarget = Path.Combine(
            paths.ExternalArchiveRoot,
            "external_context",
            "protocol_training.json");
        await PersonalGoldBrainFileService
            .CopyVerifiedAsync(
                paths.LegacyProtocolTrainingPath,
                localTarget,
                paths.LocalSafetyRoot,
                cancellationToken)
            .ConfigureAwait(false);
        await PersonalGoldBrainFileService
            .CopyVerifiedAsync(
                paths.LegacyProtocolTrainingPath,
                externalTarget,
                paths.ExternalSafetyRoot,
                cancellationToken)
            .ConfigureAwait(false);

        var localHash = await PersonalGoldBrainFileService.HashAsync(localTarget, cancellationToken)
            .ConfigureAwait(false);
        var externalHash = await PersonalGoldBrainFileService.HashAsync(externalTarget, cancellationToken)
            .ConfigureAwait(false);
        if (!localHash.Equals(externalHash, StringComparison.OrdinalIgnoreCase))
            throw new IOException("Protokoll-Lernarchiv stimmt lokal und extern nicht ueberein.");

        PersonalGoldBrainFileService.MoveFileSafe(
            paths.LegacySafetyRoot,
            paths.LegacyProtocolTrainingPath,
            paths.LegacySafetyRoot,
            paths.DisconnectedLegacyProtocolTrainingPath);
    }

    private static async Task WriteArchiveMarkerAsync(
        string archiveRoot,
        PersonalGoldBrainSeparationRequest request,
        PersonalGoldBrainResolvedPaths paths,
        string safetyRoot,
        CancellationToken cancellationToken)
    {
        var content = new StringBuilder()
            .AppendLine("SewerStudio altes KI-Gehirn - nur Archiv")
            .AppendLine($"ArchivedUtc={request.StartedUtc:O}")
            .AppendLine($"CommitId={paths.TransactionId}")
            .AppendLine($"PreviousActiveRoot={paths.KnowledgeRoot}")
            .AppendLine("DoNotUseAsActiveKnowledgeRoot=true")
            .ToString();
        await PersonalGoldBrainFileService
            .WriteTextAsync(
                Path.Combine(
                    archiveRoot,
                    PersonalGoldBrainSeparationService.LegacyArchiveMarkerFileName),
                content,
                safetyRoot,
                cancellationToken)
            .ConfigureAwait(false);
    }

    private static void TryMarkDirectoryReadOnly(string path)
    {
        try
        {
            File.SetAttributes(path, File.GetAttributes(path) | FileAttributes.ReadOnly);
        }
        catch
        {
            // Der Archivmarker und der abgekoppelte Pfad sind der eigentliche Schutz.
        }
    }

    private sealed class CommitProcessInterruptedException(
        string message,
        Exception innerException) : IOException(message, innerException);
}
