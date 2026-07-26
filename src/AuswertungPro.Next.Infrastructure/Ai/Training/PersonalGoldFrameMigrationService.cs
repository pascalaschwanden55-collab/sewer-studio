using System.Text.Json;
using AuswertungPro.Next.Application.Ai.Training;
using AuswertungPro.Next.Infrastructure.Ai;

namespace AuswertungPro.Next.Infrastructure.Ai.Training;

/// <summary>
/// Einmalige, wiederholbare Migration bestehender persoenlicher Goldsamples.
/// Bilder werden zuerst inhaltsadressiert kopiert. Danach werden SQLite und JSON
/// gemeinsam umgestellt; bei einem Fehler werden beide Pfadstaende zurueckgesetzt.
/// </summary>
public sealed class PersonalGoldFrameMigrationService(
    ITrainingFrameStore? frameStore = null,
    Func<string, string?>? codeLabelLookup = null) : IPersonalGoldFrameMigrationService
{
    private readonly ITrainingFrameStore _frameStore = frameStore ?? new TrainingFrameFileStore();
    private readonly Func<string, string?> _codeLabelLookup =
        codeLabelLookup ?? VsaCodeResolver.LookupLabel;

    public async Task<PersonalGoldFrameMigrationResult> MigrateAsync(
        PersonalGoldFrameMigrationRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        ValidateRequest(request);

        var knowledgeRoot = Path.GetFullPath(request.KnowledgeRoot)
            .TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
        var samplesPath = Path.Combine(knowledgeRoot, "training_samples.json");
        var databasePath = Path.Combine(knowledgeRoot, "KnowledgeBase.db");
        var goldFramesDir = Path.Combine(knowledgeRoot, "gold_frames");
        if (!File.Exists(samplesPath))
            return Failure(request, $"Trainingsdaten fehlen: {samplesPath}");
        if (!File.Exists(databasePath))
            return Failure(request, $"Wissensdatenbank fehlt: {databasePath}");

        byte[] originalSamplesBytes;
        List<TrainingSample> samples;
        try
        {
            originalSamplesBytes = await PersonalGoldMigrationFileService
                .ReadStableBytesAsync(samplesPath, cancellationToken)
                .ConfigureAwait(false);
            samples = JsonSerializer.Deserialize<List<TrainingSample>>(originalSamplesBytes)
                      ?? throw new InvalidDataException("training_samples.json enthaelt keine Liste.");
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            return Failure(request, $"Trainingsdaten konnten nicht sicher gelesen werden: {ex.Message}");
        }

        var selected = samples
            .Where(sample => ManualGoldTrainingPolicy.IsManuallyConfirmed(
                sample,
                request.ConfirmedByUser))
            .OrderBy(sample => sample.SampleId, StringComparer.OrdinalIgnoreCase)
            .ToArray();
        if (selected.Length == 0)
            return Failure(request, "Keine persoenlich bestaetigten Hand-Goldsamples gefunden.");
        EnsureUniqueSampleIds(selected);

        Dictionary<string, string> oldDatabasePaths;
        try
        {
            oldDatabasePaths = PersonalGoldMigrationDatabaseStore.ReadPaths(databasePath, selected);
            foreach (var sample in selected.Where(sample => sample.KbIndexState == KbIndexState.Indexed))
            {
                if (!oldDatabasePaths.ContainsKey(sample.SampleId))
                {
                    throw new InvalidDataException(
                        $"Indexiertes Sample '{sample.SampleId}' fehlt in KnowledgeBase.db.");
                }
            }
        }
        catch (Exception ex)
        {
            return Failure(request, $"Wissensdatenbank konnte nicht vorbereitet werden: {ex.Message}");
        }

        var targetPaths = await PrepareTargetPathsAsync(
                request,
                selected,
                goldFramesDir,
                cancellationToken)
            .ConfigureAwait(false);
        if (!targetPaths.Success)
        {
            return Failure(
                request,
                targetPaths.Error!,
                selected.Length,
                targetPaths.Paths.Count);
        }

        var statuses = PersonalGoldInventoryWriter.BuildStatus(
            request,
            selected,
            targetPaths.Paths);
        var uniqueGoldFrames = targetPaths.Paths.Values
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .Count();
        var fullGoldSamples = selected.Count(PersonalGoldInventoryWriter.IsFullGold);
        if (request.DryRun)
        {
            return new PersonalGoldFrameMigrationResult(
                true,
                true,
                selected.Length,
                0,
                uniqueGoldFrames,
                fullGoldSamples,
                null,
                null,
                null,
                statuses);
        }

        return await PersonalGoldMigrationCommitter.CommitAsync(
                new PersonalGoldMigrationCommitContext(
                    request,
                    samplesPath,
                    databasePath,
                    knowledgeRoot,
                    originalSamplesBytes,
                    samples,
                    selected,
                    oldDatabasePaths,
                    targetPaths.Paths,
                    statuses,
                    uniqueGoldFrames,
                    fullGoldSamples),
                cancellationToken)
            .ConfigureAwait(false);
    }

    private async Task<TargetPathPreparation> PrepareTargetPathsAsync(
        PersonalGoldFrameMigrationRequest request,
        IReadOnlyList<TrainingSample> selected,
        string goldFramesDir,
        CancellationToken cancellationToken)
    {
        var paths = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        try
        {
            foreach (var sample in selected)
            {
                cancellationToken.ThrowIfCancellationRequested();
                var codeFolder = PersonalGoldMainCodeCatalog.FormatFolderName(
                    sample.Code,
                    _codeLabelLookup);
                var codeFramesDir = Path.Combine(goldFramesDir, codeFolder);
                var targetPath = request.DryRun
                    ? await PersonalGoldMigrationFileService
                        .ResolveTargetPathAsync(sample.FramePath, codeFramesDir, cancellationToken)
                        .ConfigureAwait(false)
                    : await _frameStore
                        .StoreExistingAsync(sample.FramePath, codeFramesDir, cancellationToken)
                        .ConfigureAwait(false);
                if (string.IsNullOrWhiteSpace(targetPath))
                    throw new IOException($"Goldbild fuer Sample '{sample.SampleId}' konnte nicht kopiert werden.");
                paths.Add(sample.SampleId, Path.GetFullPath(targetPath));
            }
            return new TargetPathPreparation(true, paths, null);
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex)
        {
            return new TargetPathPreparation(
                false,
                paths,
                $"Goldbilder konnten nicht vollstaendig vorbereitet werden: {ex.Message}");
        }
    }

    private static void ValidateRequest(PersonalGoldFrameMigrationRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.KnowledgeRoot))
            throw new ArgumentException("KnowledgeRoot darf nicht leer sein.", nameof(request));
        if (string.IsNullOrWhiteSpace(request.ConfirmedByUser))
            throw new ArgumentException("Bestaetiger darf nicht leer sein.", nameof(request));
        if (request.TargetMinimumPerMainCode <= 0
            || request.TargetMaximumPerMainCode < request.TargetMinimumPerMainCode)
        {
            throw new ArgumentException("Ungueltiger Goldstandard-Zielbereich.", nameof(request));
        }
    }

    private static void EnsureUniqueSampleIds(IEnumerable<TrainingSample> samples)
    {
        var ids = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var sample in samples)
        {
            if (string.IsNullOrWhiteSpace(sample.SampleId) || !ids.Add(sample.SampleId.Trim()))
                throw new InvalidDataException($"Leere oder doppelte Sample-ID '{sample.SampleId}'.");
        }
    }

    private static PersonalGoldFrameMigrationResult Failure(
        PersonalGoldFrameMigrationRequest request,
        string error,
        int selectedSamples = 0,
        int migratedSamples = 0,
        string? auditDirectory = null,
        IReadOnlyList<PersonalGoldMainCodeStatus>? statuses = null)
        => new(
            false,
            request.DryRun,
            selectedSamples,
            migratedSamples,
            0,
            0,
            null,
            auditDirectory,
            error,
            statuses ?? []);

    private sealed record TargetPathPreparation(
        bool Success,
        IReadOnlyDictionary<string, string> Paths,
        string? Error);
}
