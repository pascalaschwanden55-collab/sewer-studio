using System.Text.Json;
using AuswertungPro.Next.Application.Ai.Training;
using AuswertungPro.Next.Application.Common;

namespace AuswertungPro.Next.Infrastructure.Ai.Training;

internal sealed record PersonalGoldMigrationCommitContext(
    PersonalGoldFrameMigrationRequest Request,
    string SamplesPath,
    string DatabasePath,
    string KnowledgeRoot,
    byte[] OriginalSamplesBytes,
    IReadOnlyList<TrainingSample> Samples,
    IReadOnlyList<TrainingSample> Selected,
    IReadOnlyDictionary<string, string> OldDatabasePaths,
    IReadOnlyDictionary<string, string> TargetPaths,
    IReadOnlyList<PersonalGoldMainCodeStatus> Statuses,
    int UniqueGoldFrames,
    int FullGoldSamples);

/// <summary>Schaltet JSON und SQLite gemeinsam auf die vorbereiteten Goldpfade um.</summary>
internal static class PersonalGoldMigrationCommitter
{
    public static async Task<PersonalGoldFrameMigrationResult> CommitAsync(
        PersonalGoldMigrationCommitContext context,
        CancellationToken cancellationToken)
    {
        var request = context.Request;
        var auditDirectory = Path.Combine(
            context.KnowledgeRoot,
            "training",
            "gold_migrations",
            $"personal_gold_{request.StartedUtc.UtcDateTime:yyyyMMdd_HHmmss}");
        var inventoryPath = Path.Combine(
            context.KnowledgeRoot,
            "training",
            "gold_standard",
            "main_code_inventory_v1.json");
        var databaseChanged = false;
        var samplesChanged = false;
        try
        {
            Directory.CreateDirectory(auditDirectory);
            await AtomicTextFileWriter.WriteAllBytesAsync(
                    Path.Combine(auditDirectory, "training_samples.before.json"),
                    context.OriginalSamplesBytes,
                    cancellationToken)
                .ConfigureAwait(false);
            await PersonalGoldInventoryWriter.WriteAuditMappingAsync(
                    Path.Combine(auditDirectory, "frame_path_mapping.json"),
                    context.Selected,
                    context.TargetPaths,
                    context.OldDatabasePaths,
                    request,
                    cancellationToken)
                .ConfigureAwait(false);

            foreach (var sample in context.Selected)
                sample.FramePath = context.TargetPaths[sample.SampleId];

            var updatedJson = JsonSerializer.Serialize(context.Samples, JsonDefaults.Indented);
            PersonalGoldMigrationFileService.ValidateUpdatedJson(
                updatedJson,
                context.Samples.Count,
                context.Selected);

            PersonalGoldMigrationDatabaseStore.UpdatePaths(
                context.DatabasePath,
                context.TargetPaths,
                context.OldDatabasePaths.Keys);
            databaseChanged = true;

            await AtomicTextFileWriter.WriteAllTextAsync(
                    context.SamplesPath,
                    updatedJson,
                    cancellationToken)
                .ConfigureAwait(false);
            samplesChanged = true;

            await PersonalGoldInventoryWriter.WriteInventoryAsync(
                    inventoryPath,
                    request,
                    context.Selected,
                    context.TargetPaths,
                    context.Statuses,
                    cancellationToken)
                .ConfigureAwait(false);
            VerifyMigration(context);
            await PersonalGoldBrainManifestWriter
                .WriteAsync(context.KnowledgeRoot, cancellationToken)
                .ConfigureAwait(false);

            return new PersonalGoldFrameMigrationResult(
                true,
                false,
                context.Selected.Count,
                context.Selected.Count,
                context.UniqueGoldFrames,
                context.FullGoldSamples,
                inventoryPath,
                auditDirectory,
                null,
                context.Statuses);
        }
        catch (OperationCanceledException)
        {
            RestoreMetadata(context, samplesChanged, databaseChanged);
            throw;
        }
        catch (Exception ex)
        {
            var rollbackError = RestoreMetadata(context, samplesChanged, databaseChanged);
            var message = rollbackError is null
                ? $"Goldmigration fehlgeschlagen; JSON und Datenbank wurden zurueckgesetzt: {ex.Message}"
                : $"Goldmigration fehlgeschlagen. Ruecksetzung unvollstaendig ({rollbackError}): {ex.Message}";
            return new PersonalGoldFrameMigrationResult(
                false,
                request.DryRun,
                context.Selected.Count,
                0,
                0,
                0,
                null,
                auditDirectory,
                message,
                context.Statuses);
        }
    }

    private static void VerifyMigration(PersonalGoldMigrationCommitContext context)
    {
        PersonalGoldMigrationFileService.VerifyJsonAndFrames(
            context.SamplesPath,
            context.Selected,
            context.TargetPaths);
        var databasePaths = PersonalGoldMigrationDatabaseStore.ReadPaths(
            context.DatabasePath,
            context.Selected);
        foreach (var sample in context.Selected)
        {
            if (sample.KbIndexState == KbIndexState.Indexed
                && (!databasePaths.TryGetValue(sample.SampleId, out var storedDatabasePath)
                    || !string.Equals(
                        storedDatabasePath,
                        context.TargetPaths[sample.SampleId],
                        StringComparison.OrdinalIgnoreCase)))
            {
                throw new InvalidDataException(
                    $"KB-Pfad-Pruefung fehlgeschlagen fuer '{sample.SampleId}'.");
            }
        }
    }

    private static string? RestoreMetadata(
        PersonalGoldMigrationCommitContext context,
        bool samplesChanged,
        bool databaseChanged)
    {
        var errors = new List<string>();
        if (databaseChanged)
        {
            try
            {
                PersonalGoldMigrationDatabaseStore.UpdatePaths(
                    context.DatabasePath,
                    context.OldDatabasePaths,
                    context.OldDatabasePaths.Keys);
            }
            catch (Exception ex)
            {
                errors.Add($"KB: {ex.Message}");
            }
        }
        if (samplesChanged)
        {
            try
            {
                AtomicTextFileWriter.WriteAllBytesAsync(
                        context.SamplesPath,
                        context.OriginalSamplesBytes)
                    .GetAwaiter()
                    .GetResult();
            }
            catch (Exception ex)
            {
                errors.Add($"JSON: {ex.Message}");
            }
        }

        return errors.Count == 0 ? null : string.Join(" | ", errors);
    }
}
