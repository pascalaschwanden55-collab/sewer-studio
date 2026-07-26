using System.Text.Json;
using AuswertungPro.Next.Application.Ai.Training;
using AuswertungPro.Next.Application.Backup;
using AuswertungPro.Next.Application.Common;
using AuswertungPro.Next.Infrastructure.Backup;

namespace AuswertungPro.Next.Infrastructure.Ai.Training;

/// <summary>
/// Orchestriert das sichere Nachholen persoenlicher ManualCoding-Zeilen aus einer
/// archivierten SQLite-KB. Datei-, Input- und Transaktionsdetails liegen in Helfern.
/// </summary>
public sealed class PersonalGoldArchiveRecoveryService : IPersonalGoldArchiveRecoveryService
{
    private readonly PersonalGoldArchiveRecoveryOutput _output;
    private readonly PersonalGoldArchiveRecoveryTransaction _transaction;
    private readonly Action<PersonalGoldArchiveRecoveryStep>? _transactionStepObserver;

    public PersonalGoldArchiveRecoveryService(
        ITrainingFrameStore? frameStore = null,
        ISqliteSnapshotCopier? sqliteSnapshots = null)
        : this(frameStore, sqliteSnapshots, transactionStepObserver: null)
    {
    }

    internal PersonalGoldArchiveRecoveryService(
        ITrainingFrameStore? frameStore,
        ISqliteSnapshotCopier? sqliteSnapshots,
        Action<PersonalGoldArchiveRecoveryStep>? transactionStepObserver)
    {
        var snapshots = sqliteSnapshots ?? new SqliteSnapshotCopyService();
        _output = new PersonalGoldArchiveRecoveryOutput(
            frameStore ?? new TrainingFrameFileStore(),
            new PersonalGoldBrainDatabaseBuilder(snapshots));
        _transaction = new PersonalGoldArchiveRecoveryTransaction(snapshots);
        _transactionStepObserver = transactionStepObserver;
    }

    internal static string ResolveTransactionJournalPath(string activeKnowledgeRoot)
        => PersonalGoldArchiveRecoveryTransaction.ResolveJournalPath(
            activeKnowledgeRoot);

    public async Task<PersonalGoldArchiveRecoveryResult> RecoverAsync(
        PersonalGoldArchiveRecoveryRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        ArchiveRecoveryPaths? paths = null;
        ArchiveRecoveryTransactionJournal? transaction = null;
        try
        {
            paths = PersonalGoldArchiveRecoveryInput.ValidateAndResolve(request);
            PersonalGoldArchiveRecoveryInput.EnsureActiveIsIdle(paths.ActiveRoot);
            PersonalGoldBrainFileService.EnsureMutationTreeIsSafe(paths.ActiveRoot);
            await _transaction
                .RecoverPendingAsync(paths, request.DryRun, cancellationToken)
                .ConfigureAwait(false);

            var originalSamplesBytes = await PersonalGoldMigrationFileService
                .ReadStableBytesAsync(paths.ActiveSamplesPath, cancellationToken)
                .ConfigureAwait(false);
            var activeSamples = JsonSerializer.Deserialize<List<TrainingSample>>(
                                    originalSamplesBytes)
                                ?? throw new InvalidDataException(
                                    "Aktive training_samples.json enthaelt keine Liste.");
            PersonalGoldArchiveRecoveryInput.EnsureUniqueIds(activeSamples);
            var existingPersonal = activeSamples.Count(sample =>
                ManualGoldTrainingPolicy.IsManuallyConfirmed(
                    sample,
                    request.ConfirmedByUser));
            if (!request.DryRun
                && activeSamples.Any(sample =>
                    !ManualGoldTrainingPolicy.IsManuallyConfirmed(
                        sample,
                        request.ConfirmedByUser)))
            {
                throw new InvalidDataException(
                    "Das aktive Gehirn enthaelt nicht ausschliesslich persoenliche Goldsamples.");
            }

            var knownIds = activeSamples
                .Select(sample => sample.SampleId)
                .ToHashSet(StringComparer.OrdinalIgnoreCase);
            var candidates = LegacyPersonalGoldDatabaseReader
                .ReadMissing(
                    paths.LegacyDatabasePath,
                    request.ConfirmedByUser,
                    knownIds)
                .ToArray();
            PersonalGoldArchiveRecoveryInput.ResolveAndValidateSourceFrames(
                paths.LegacyRoot,
                paths.PreviousActiveRoot,
                candidates);

            if (request.DryRun || candidates.Length == 0)
            {
                return Success(
                    request,
                    existingPersonal,
                    candidates.Length,
                    recovered: 0,
                    activePersonal: request.DryRun
                        ? existingPersonal + candidates.Length
                        : existingPersonal,
                    receiptPath: null);
            }

            var framePlans = await PersonalGoldArchiveRecoveryInput
                .BuildCandidateFramePlansAsync(paths, candidates, cancellationToken)
                .ConfigureAwait(false);
            var preimages = await PersonalGoldArchiveRecoveryInput
                .ReadPreimagesAsync(
                    paths,
                    originalSamplesBytes,
                    cancellationToken)
                .ConfigureAwait(false);
            transaction = PersonalGoldArchiveRecoveryInput.CreatePreparingTransaction(
                request,
                paths,
                framePlans,
                preimages);
            await _transaction
                .WriteJournalAsync(
                    paths,
                    transaction,
                    requireMissing: true,
                    cancellationToken)
                .ConfigureAwait(false);
            transaction = await _transaction
                .PrepareBackupsAsync(
                    paths,
                    transaction,
                    preimages,
                    cancellationToken)
                .ConfigureAwait(false);

            var targetPaths = await _output
                .CopyCandidateFramesAsync(
                    paths,
                    candidates,
                    framePlans,
                    cancellationToken)
                .ConfigureAwait(false);
            foreach (var candidate in candidates)
                candidate.Sample.FramePath = targetPaths[candidate.Sample.SampleId];

            PersonalGoldArchiveDatabaseImporter.Import(
                paths.ActiveDatabasePath,
                candidates,
                targetPaths);
            NotifyTransactionStep(PersonalGoldArchiveRecoveryStep.DatabaseImported);

            activeSamples.AddRange(candidates.Select(candidate => candidate.Sample));
            var updatedJson = JsonSerializer.Serialize(activeSamples, JsonDefaults.Indented);
            PersonalGoldMigrationFileService.ValidateUpdatedJson(
                updatedJson,
                activeSamples.Count,
                candidates.Select(candidate => candidate.Sample).ToArray());
            await AtomicTextFileWriter
                .WriteAllTextAsync(
                    paths.ActiveSamplesPath,
                    updatedJson,
                    cancellationToken)
                .ConfigureAwait(false);

            var allTargetPaths = activeSamples.ToDictionary(
                sample => sample.SampleId,
                sample => sample.FramePath,
                StringComparer.OrdinalIgnoreCase);
            var inventoryRequest = new PersonalGoldFrameMigrationRequest(
                paths.ActiveRoot,
                request.ConfirmedByUser,
                request.RequiredMainCodes,
                request.StartedUtc,
                TargetMinimumPerMainCode: 30,
                TargetMaximumPerMainCode: 50);
            var statuses = PersonalGoldInventoryWriter.BuildStatus(
                inventoryRequest,
                activeSamples,
                allTargetPaths);
            await PersonalGoldInventoryWriter
                .WriteInventoryAsync(
                    transaction.InventoryPath,
                    inventoryRequest,
                    activeSamples,
                    allTargetPaths,
                    statuses,
                    cancellationToken)
                .ConfigureAwait(false);

            _output.VerifyRecoveredState(paths, activeSamples, allTargetPaths);
            await PersonalGoldArchiveRecoveryOutput
                .WriteReceiptAsync(
                    request,
                    paths,
                    transaction.ReceiptPath,
                    existingPersonal,
                    activeSamples.Count,
                    candidates,
                    cancellationToken)
                .ConfigureAwait(false);
            await PersonalGoldBrainManifestWriter
                .WriteAsync(paths.ActiveRoot, cancellationToken)
                .ConfigureAwait(false);
            await _transaction.DeleteJournalAsync(paths, transaction)
                .ConfigureAwait(false);

            return Success(
                request,
                existingPersonal,
                candidates.Length,
                candidates.Length,
                activeSamples.Count,
                transaction.ReceiptPath);
        }
        catch (ArchiveRecoveryProcessInterruptedException ex)
        {
            return Failure(request, ex.Message);
        }
        catch (OperationCanceledException)
        {
            if (paths is not null
                && transaction is not null
                && File.Exists(ResolveTransactionJournalPath(paths.ActiveRoot)))
            {
                await _transaction.RollBackAsync(paths, transaction)
                    .ConfigureAwait(false);
            }
            throw;
        }
        catch (Exception ex)
        {
            string? rollbackError = null;
            if (paths is not null
                && transaction is not null
                && File.Exists(ResolveTransactionJournalPath(paths.ActiveRoot)))
            {
                try
                {
                    await _transaction.RollBackAsync(paths, transaction)
                        .ConfigureAwait(false);
                }
                catch (Exception recoveryError)
                {
                    rollbackError = recoveryError.Message;
                }
            }
            var error = rollbackError is null
                ? ex.Message
                : $"{ex.Message} Ruecksetzung unvollstaendig: {rollbackError}";
            return Failure(request, error);
        }
    }

    private void NotifyTransactionStep(PersonalGoldArchiveRecoveryStep step)
    {
        if (_transactionStepObserver is null)
            return;
        try
        {
            _transactionStepObserver(step);
        }
        catch (Exception ex)
        {
            throw new ArchiveRecoveryProcessInterruptedException(
                $"Simulierter Prozessabbruch nach Recovery-Schritt '{step}'.",
                ex);
        }
    }

    private static PersonalGoldArchiveRecoveryResult Success(
        PersonalGoldArchiveRecoveryRequest request,
        int existingPersonal,
        int candidates,
        int recovered,
        int activePersonal,
        string? receiptPath)
        => new(
            true,
            request.DryRun,
            existingPersonal,
            candidates,
            recovered,
            activePersonal,
            receiptPath,
            null);

    private static PersonalGoldArchiveRecoveryResult Failure(
        PersonalGoldArchiveRecoveryRequest request,
        string error)
        => new(
            false,
            request.DryRun,
            0,
            0,
            0,
            0,
            null,
            error);

    private sealed class ArchiveRecoveryProcessInterruptedException(
        string message,
        Exception innerException) : IOException(message, innerException);
}

internal enum PersonalGoldArchiveRecoveryStep
{
    DatabaseImported
}
