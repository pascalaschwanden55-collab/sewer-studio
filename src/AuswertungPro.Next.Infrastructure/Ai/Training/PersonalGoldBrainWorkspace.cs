using System.Text.Json;
using AuswertungPro.Next.Application.Ai.Training;
using AuswertungPro.Next.Infrastructure.Ai;

namespace AuswertungPro.Next.Infrastructure.Ai.Training;

/// <summary>
/// Baut und prueft den neuen Gold-Arbeitsstand vor der atomaren Umschaltung.
/// </summary>
internal sealed class PersonalGoldBrainWorkspace(PersonalGoldBrainDatabaseBuilder database)
{
    internal const string SeparationReceiptFileName = "gold_brain_separation_v1.json";

    internal async Task BuildAsync(
        PersonalGoldBrainSeparationRequest request,
        PersonalGoldBrainResolvedPaths paths,
        IReadOnlyList<TrainingSample> selected,
        int sourceSampleCount,
        int sourceKnowledgeCount,
        CancellationToken cancellationToken)
    {
        PersonalGoldBrainFileService.CreateDirectorySafe(
            paths.LocalSafetyRoot,
            paths.StagingRoot);
        await PersonalGoldBrainFileService
            .WriteTextAsync(
                Path.Combine(
                    paths.StagingRoot,
                    PersonalGoldBrainSeparationService.CommitOwnerMarkerFileName),
                paths.TransactionId,
                paths.LocalSafetyRoot,
                cancellationToken)
            .ConfigureAwait(false);
        var finalGoldFrames = Path.Combine(paths.KnowledgeRoot, "gold_frames");
        var stagingGoldFrames = Path.Combine(paths.StagingRoot, "gold_frames");
        PersonalGoldBrainFileService.CreateDirectorySafe(
            paths.LocalSafetyRoot,
            stagingGoldFrames);

        var targetPaths = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        foreach (var sample in selected)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var codeFolder = PersonalGoldMainCodeCatalog.FormatFolderName(
                sample.Code,
                VsaCodeResolver.LookupLabel);
            var fileName = Path.GetFileName(sample.FramePath);
            var finalPath = Path.Combine(finalGoldFrames, codeFolder, fileName);
            var stagingPath = Path.Combine(stagingGoldFrames, codeFolder, fileName);
            await PersonalGoldBrainFileService
                .CopyVerifiedAsync(
                    sample.FramePath,
                    stagingPath,
                    paths.LocalSafetyRoot,
                    cancellationToken)
                .ConfigureAwait(false);
            sample.FramePath = finalPath;
            targetPaths.Add(sample.SampleId, finalPath);
        }

        var samplesPath = Path.Combine(paths.StagingRoot, "training_samples.json");
        await PersonalGoldBrainFileService
            .WriteJsonAsync(
                samplesPath,
                selected,
                paths.LocalSafetyRoot,
                cancellationToken)
            .ConfigureAwait(false);

        var stagingDatabasePath = Path.Combine(paths.StagingRoot, "KnowledgeBase.db");
        PersonalGoldBrainFileService.EnsureMutationPathIsSafe(
            paths.LocalSafetyRoot,
            stagingDatabasePath);
        var databaseResult = await database
            .BuildAsync(
                paths.DatabasePath,
                stagingDatabasePath,
                selected,
                cancellationToken)
            .ConfigureAwait(false);
        PersonalGoldBrainFileService.EnsureMutationPathIsSafe(
            paths.LocalSafetyRoot,
            stagingDatabasePath);
        if (databaseResult.SourceSamples != sourceKnowledgeCount
            || databaseResult.Samples != selected.Count
            || databaseResult.Embeddings != selected.Count)
        {
            throw new InvalidDataException("Unerwartete Anzahl in der neuen Gold-Datenbank.");
        }

        await CopyOptionalFileAsync(
                paths.KnowledgeRoot,
                paths.StagingRoot,
                "yolo_class_map.json",
                paths.LocalSafetyRoot,
                cancellationToken)
            .ConfigureAwait(false);
        await CopyOptionalFileAsync(
                paths.KnowledgeRoot,
                paths.StagingRoot,
                "classes.txt",
                paths.LocalSafetyRoot,
                cancellationToken)
            .ConfigureAwait(false);
        await CopyOptionalFileAsync(
                paths.KnowledgeRoot,
                paths.StagingRoot,
                "training_settings.json",
                paths.LocalSafetyRoot,
                cancellationToken)
            .ConfigureAwait(false);
        await CopyOptionalFileAsync(
                paths.KnowledgeRoot,
                paths.StagingRoot,
                "measures_learning.json",
                paths.LocalSafetyRoot,
                cancellationToken)
            .ConfigureAwait(false);
        await CopyOptionalFileAsync(
                paths.KnowledgeRoot,
                paths.StagingRoot,
                "measures-model.zip",
                paths.LocalSafetyRoot,
                cancellationToken)
            .ConfigureAwait(false);

        await CopyOptionalTreeAsync(
                paths.KnowledgeRoot,
                paths.StagingRoot,
                "eval_set",
                paths.LocalSafetyRoot,
                cancellationToken)
            .ConfigureAwait(false);
        await CopyOptionalTreeAsync(
                paths.KnowledgeRoot,
                paths.StagingRoot,
                Path.Combine("training", "gold_inbox"),
                paths.LocalSafetyRoot,
                cancellationToken)
            .ConfigureAwait(false);
        await CopyOptionalTreeAsync(
                paths.KnowledgeRoot,
                paths.StagingRoot,
                Path.Combine("training", "gold_migrations"),
                paths.LocalSafetyRoot,
                cancellationToken)
            .ConfigureAwait(false);

        PersonalGoldBrainFileService.CreateDirectorySafe(
            paths.LocalSafetyRoot,
            Path.Combine(paths.StagingRoot, "frames"));
        PersonalGoldBrainFileService.CreateDirectorySafe(
            paths.LocalSafetyRoot,
            Path.Combine(paths.StagingRoot, "teacher_images", "crops"));
        PersonalGoldBrainFileService.CreateDirectorySafe(
            paths.LocalSafetyRoot,
            Path.Combine(paths.StagingRoot, "teacher_labels"));
        PersonalGoldBrainFileService.CreateDirectorySafe(
            paths.LocalSafetyRoot,
            Path.Combine(paths.StagingRoot, "training", "datasets"));
        PersonalGoldBrainFileService.CreateDirectorySafe(
            paths.LocalSafetyRoot,
            Path.Combine(paths.StagingRoot, "training", "reports"));
        var goldStandardDir = Path.Combine(paths.StagingRoot, "training", "gold_standard");
        PersonalGoldBrainFileService.CreateDirectorySafe(
            paths.LocalSafetyRoot,
            goldStandardDir);

        await PersonalGoldBrainFileService
            .WriteTextAsync(
                Path.Combine(paths.StagingRoot, "teacher_annotations.json"),
                "[]",
                paths.LocalSafetyRoot,
                cancellationToken)
            .ConfigureAwait(false);
        await PersonalGoldBrainFileService
            .WriteJsonAsync(
                Path.Combine(paths.StagingRoot, "protocol_training.json"),
                new { Samples = Array.Empty<object>() },
                paths.LocalSafetyRoot,
                cancellationToken)
            .ConfigureAwait(false);

        var inventoryRequest = new PersonalGoldFrameMigrationRequest(
            paths.KnowledgeRoot,
            request.ConfirmedByUser,
            request.RequiredMainCodes,
            request.StartedUtc,
            TargetMinimumPerMainCode: 30,
            TargetMaximumPerMainCode: 50);
        var statuses = PersonalGoldInventoryWriter.BuildStatus(
            inventoryRequest,
            selected,
            targetPaths);
        var inventoryPath = Path.Combine(
            goldStandardDir,
            "main_code_inventory_v1.json");
        PersonalGoldBrainFileService.EnsureMutationPathIsSafe(
            paths.LocalSafetyRoot,
            inventoryPath);
        await PersonalGoldInventoryWriter
            .WriteInventoryAsync(
                inventoryPath,
                inventoryRequest,
                selected,
                targetPaths,
                statuses,
                cancellationToken)
            .ConfigureAwait(false);
        PersonalGoldBrainFileService.EnsureMutationPathIsSafe(
            paths.LocalSafetyRoot,
            inventoryPath);

        var receipt = new
        {
            schema_version = 1,
            created_utc = request.StartedUtc,
            confirmed_by_user = request.ConfirmedByUser,
            selection_policy = "personal-manual-confirmed-only",
            active_root = paths.KnowledgeRoot,
            local_legacy_archive = paths.LocalArchiveRoot,
            external_legacy_archive = paths.ExternalArchiveRoot,
            source_samples = sourceSampleCount,
            personal_gold_samples = selected.Count,
            full_gold_samples = selected.Count(PersonalGoldInventoryWriter.IsFullGold),
            source_knowledge_samples = sourceKnowledgeCount,
            active_knowledge_samples = selected.Count,
            old_runtime_context = new
            {
                knowledge_database = "archived",
                protocol_training = File.Exists(paths.LegacyProtocolTrainingPath)
                    ? "archived-and-disconnected"
                    : "not-present"
            },
            active_runtime_context = new
            {
                knowledge_database = "personal-gold-only",
                protocol_training = "empty-new-store"
            }
        };
        await PersonalGoldBrainFileService
            .WriteJsonAsync(
                Path.Combine(goldStandardDir, SeparationReceiptFileName),
                receipt,
                paths.LocalSafetyRoot,
                cancellationToken)
            .ConfigureAwait(false);

        PersonalGoldBrainFileService.EnsureMutationTreeIsSafe(paths.StagingRoot);
        await PersonalGoldBrainManifestWriter
            .WriteAsync(paths.StagingRoot, cancellationToken)
            .ConfigureAwait(false);
        PersonalGoldBrainFileService.EnsureMutationTreeIsSafe(paths.StagingRoot);
    }

    internal void Validate(
        PersonalGoldBrainResolvedPaths paths,
        IReadOnlyList<TrainingSample> selected)
    {
        PersonalGoldBrainFileService.EnsureMutationTreeIsSafe(paths.StagingRoot);
        PersonalGoldBrainCommitJournalStore.ValidateCommitOwnership(
            paths.StagingRoot,
            paths.TransactionId);
        var saved = JsonSerializer.Deserialize<List<TrainingSample>>(
                        File.ReadAllBytes(Path.Combine(paths.StagingRoot, "training_samples.json")))
                    ?? throw new InvalidDataException("Neue training_samples.json ist nicht lesbar.");
        if (saved.Count != selected.Count
            || saved.Any(sample => !ManualGoldTrainingPolicy.IsManuallyConfirmed(
                sample,
                selected[0].ConfirmedByUser)))
        {
            throw new InvalidDataException(
                "Neue training_samples.json enthaelt nicht ausschliesslich persoenliche Goldsamples.");
        }

        foreach (var sample in saved)
        {
            if (!PersonalGoldBrainFileService.IsInside(paths.KnowledgeRoot, sample.FramePath))
            {
                throw new InvalidDataException(
                    $"Goldbildpfad liegt ausserhalb des neuen Stands: {sample.SampleId}");
            }

            var relativeFrame = Path.GetRelativePath(paths.KnowledgeRoot, sample.FramePath);
            var stagedFrame = Path.Combine(paths.StagingRoot, relativeFrame);
            if (!PersonalGoldBrainFileService.IsInside(paths.StagingRoot, stagedFrame))
            {
                throw new InvalidDataException(
                    $"Goldbildpfad ist im Arbeitsstand ungueltig: {sample.SampleId}");
            }
            if (!File.Exists(stagedFrame))
                throw new FileNotFoundException($"Goldbild fehlt im neuen Stand: {sample.SampleId}", stagedFrame);
        }

        database.Inspect(
            Path.Combine(paths.StagingRoot, "KnowledgeBase.db"),
            selected);
        var teacherJson = File.ReadAllText(Path.Combine(paths.StagingRoot, "teacher_annotations.json"));
        if (JsonSerializer.Deserialize<List<object>>(teacherJson) is not { Count: 0 })
            throw new InvalidDataException("Neue Teacher-Datei ist nicht leer.");

        var oldContextNames = new[]
        {
            "fewshot_examples.json",
            "selftraining_history.json",
            "review_queue.json",
            "positive_feedback.jsonl",
            "negative_feedback.jsonl"
        };
        if (oldContextNames.Any(name => File.Exists(Path.Combine(paths.StagingRoot, name))))
            throw new InvalidDataException("Alter Laufzeitkontext wurde in den Goldstand uebernommen.");
    }

    private static async Task CopyOptionalFileAsync(
        string sourceRoot,
        string targetRoot,
        string relativePath,
        string targetSafetyRoot,
        CancellationToken cancellationToken)
    {
        var source = Path.Combine(sourceRoot, relativePath);
        if (!File.Exists(source))
            return;
        await PersonalGoldBrainFileService
            .CopyVerifiedAsync(
                source,
                Path.Combine(targetRoot, relativePath),
                targetSafetyRoot,
                cancellationToken)
            .ConfigureAwait(false);
    }

    private static async Task CopyOptionalTreeAsync(
        string sourceRoot,
        string targetRoot,
        string relativePath,
        string targetSafetyRoot,
        CancellationToken cancellationToken)
    {
        var source = Path.Combine(sourceRoot, relativePath);
        if (!Directory.Exists(source))
            return;
        await PersonalGoldBrainFileService
            .CopyTreeVerifiedAsync(
                source,
                Path.Combine(targetRoot, relativePath),
                targetSafetyRoot,
                cancellationToken)
            .ConfigureAwait(false);
    }
}
