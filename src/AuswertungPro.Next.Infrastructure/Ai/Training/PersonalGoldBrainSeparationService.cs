using System.Text.Json;
using AuswertungPro.Next.Application.Ai.Training;
using AuswertungPro.Next.Application.Backup;
using AuswertungPro.Next.Infrastructure.Backup;

namespace AuswertungPro.Next.Infrastructure.Ai.Training;

/// <summary>
/// Baut einen geprueften Gold-only-Stand neben dem aktiven Wissensordner auf
/// und schaltet erst nach vollstaendiger Pruefung atomar auf ihn um.
/// </summary>
public sealed class PersonalGoldBrainSeparationService : IPersonalGoldBrainSeparationService
{
    public const string LegacyArchiveMarkerFileName = ".sewerstudio-legacy-brain-archive";
    internal const string CommitOwnerMarkerFileName = ".sewerstudio-gold-brain-commit-owner";

    private readonly PersonalGoldBrainWorkspace _workspace;
    private readonly PersonalGoldBrainCommitExecutor _commit;

    public PersonalGoldBrainSeparationService(ISqliteSnapshotCopier? sqliteSnapshots = null)
        : this(sqliteSnapshots, commitStepObserver: null)
    {
    }

    internal PersonalGoldBrainSeparationService(
        ISqliteSnapshotCopier? sqliteSnapshots,
        Action<PersonalGoldBrainCommitStep>? commitStepObserver)
    {
        var database = new PersonalGoldBrainDatabaseBuilder(
            sqliteSnapshots ?? new SqliteSnapshotCopyService());
        _workspace = new PersonalGoldBrainWorkspace(database);
        _commit = new PersonalGoldBrainCommitExecutor(commitStepObserver);
    }

    public async Task<PersonalGoldBrainSeparationResult> SeparateAsync(
        PersonalGoldBrainSeparationRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        PersonalGoldBrainResolvedPaths? paths = null;
        try
        {
            var declaredPaths = PersonalGoldBrainSeparationInput.ResolveDeclaredPaths(
                request,
                Guid.NewGuid().ToString("N"));
            if (request.DryRun
                && (File.Exists(declaredPaths.CommitJournalPath)
                    || Directory.Exists(declaredPaths.CommitJournalPath)))
            {
                throw new InvalidDataException(
                    "Ein offenes Commit-Journal wurde gefunden. Der Prueflauf " +
                    "veraendert nichts; die Wiederherstellung muss ausdruecklich " +
                    "im Ausfuehrungsmodus gestartet werden.");
            }
            await PersonalGoldBrainCommitRecovery
                .RecoverPendingAsync(request, declaredPaths, cancellationToken)
                .ConfigureAwait(false);
            paths = PersonalGoldBrainSeparationInput.ValidateAndResolvePaths(declaredPaths);
            PersonalGoldBrainSeparationInput.EnsureSourceIsIdle(paths.KnowledgeRoot);

            var sourceBytes = await PersonalGoldMigrationFileService
                .ReadStableBytesAsync(paths.SamplesPath, cancellationToken)
                .ConfigureAwait(false);
            var sourceSamples = JsonSerializer.Deserialize<List<TrainingSample>>(sourceBytes)
                                ?? throw new InvalidDataException(
                                    "training_samples.json enthaelt keine Liste.");
            var selected = PersonalGoldBrainSeparationInput.SelectPersonalGold(
                sourceSamples,
                request.ConfirmedByUser);
            PersonalGoldBrainSeparationInput.ValidateSelectedFrames(
                paths.KnowledgeRoot,
                selected);

            var sourceDatabase = PersonalGoldBrainSeparationInput.InspectSourceDatabase(
                paths.DatabasePath,
                selected);
            await PersonalGoldBrainSeparationInput
                .ValidateExternalMirrorAsync(paths, selected, cancellationToken)
                .ConfigureAwait(false);

            var fullGold = selected.Count(PersonalGoldInventoryWriter.IsFullGold);
            if (request.DryRun)
            {
                return Success(
                    request,
                    sourceSamples.Count,
                    selected.Length,
                    fullGold,
                    sourceDatabase.SourceSamples,
                    selected.Length,
                    paths,
                    receiptPath: null);
            }

            await _workspace
                .BuildAsync(
                    request,
                    paths,
                    selected,
                    sourceSamples.Count,
                    sourceDatabase.SourceSamples,
                    cancellationToken)
                .ConfigureAwait(false);
            _workspace.Validate(paths, selected);

            await _commit.CommitAsync(request, paths, cancellationToken).ConfigureAwait(false);

            var receiptPath = Path.Combine(
                paths.KnowledgeRoot,
                "training",
                "gold_standard",
                PersonalGoldBrainWorkspace.SeparationReceiptFileName);
            return Success(
                request,
                sourceSamples.Count,
                selected.Length,
                fullGold,
                sourceDatabase.SourceSamples,
                selected.Length,
                paths,
                receiptPath);
        }
        catch (OperationCanceledException)
        {
            PersonalGoldBrainCommitRecovery.TryDeleteUncommittedOwnedStaging(paths);
            throw;
        }
        catch (Exception ex)
        {
            PersonalGoldBrainCommitRecovery.TryDeleteUncommittedOwnedStaging(paths);

            return new PersonalGoldBrainSeparationResult(
                false,
                request.DryRun,
                0,
                0,
                0,
                0,
                0,
                null,
                null,
                null,
                null,
                ex.Message);
        }
    }

    internal static string ResolveCommitJournalPath(string knowledgeRoot)
        => PersonalGoldBrainSeparationInput.ResolveCommitJournalPath(knowledgeRoot);

    private static PersonalGoldBrainSeparationResult Success(
        PersonalGoldBrainSeparationRequest request,
        int sourceSamples,
        int personalGoldSamples,
        int fullGoldSamples,
        int sourceKnowledgeSamples,
        int activeKnowledgeSamples,
        PersonalGoldBrainResolvedPaths paths,
        string? receiptPath)
        => new(
            true,
            request.DryRun,
            sourceSamples,
            personalGoldSamples,
            fullGoldSamples,
            sourceKnowledgeSamples,
            activeKnowledgeSamples,
            paths.KnowledgeRoot,
            paths.LocalArchiveRoot,
            paths.ExternalArchiveRoot,
            receiptPath,
            null);
}
