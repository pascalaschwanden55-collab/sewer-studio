namespace AuswertungPro.Next.Application.Ai.Training.ExportPlans;

public sealed record TrainingExportCompletionResult(
    int MarkedTrainingSamples,
    IReadOnlyList<string> MarkedSampleIds);

/// <summary>
/// Markiert nur TrainingSamples, deren geplantes Bild vom Ausfuehrer mit dem
/// passenden Plan-Hash bestaetigt wurde. Teacher- und Ausschlussquellen bleiben
/// unangetastet.
/// </summary>
public sealed class TrainingExportCompletionService : ITrainingExportCompletionService
{
    public TrainingExportCompletionResult Apply(
        TrainingExportPlan plan,
        TrainingExportExecutionResult execution,
        IReadOnlyList<TrainingSample> samples,
        DateTime exportedUtc)
    {
        ArgumentNullException.ThrowIfNull(plan);
        ArgumentNullException.ThrowIfNull(execution);
        ArgumentNullException.ThrowIfNull(samples);
        TrainingExportPlanValidator.Validate(plan);
        ValidateExecution(plan, execution);

        var byId = new Dictionary<string, TrainingSample>(StringComparer.OrdinalIgnoreCase);
        foreach (var sample in samples)
        {
            if (string.IsNullOrWhiteSpace(sample.SampleId))
                continue;
            if (!byId.TryAdd(sample.SampleId.Trim(), sample))
                throw new TrainingExportPlanException($"TrainingSample-ID '{sample.SampleId}' steht mehrfach.");
        }

        var writtenHashes = execution.WrittenImageSha256.ToHashSet(StringComparer.OrdinalIgnoreCase);
        var plannedSampleIds = plan.Images
            .Where(image => writtenHashes.Contains(image.ImageSha256))
            .SelectMany(image => image.Labels)
            .SelectMany(label => label.Sources)
            .Where(source => source.SourceType == TrainingExportSourceType.TrainingSample)
            .Select(source => source.SourceId)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderBy(id => id, StringComparer.OrdinalIgnoreCase)
            .ToArray();
        var missingIds = plannedSampleIds.Where(id => !byId.ContainsKey(id)).ToArray();
        if (missingIds.Length > 0)
        {
            throw new TrainingExportPlanException(
                $"Exportierte TrainingSamples fehlen im Speicher: {string.Join(", ", missingIds)}");
        }

        var utc = exportedUtc.Kind == DateTimeKind.Utc
            ? exportedUtc
            : exportedUtc.ToUniversalTime();
        foreach (var id in plannedSampleIds)
            byId[id].ExportedUtc = utc;
        return new TrainingExportCompletionResult(plannedSampleIds.Length, plannedSampleIds);
    }

    private static void ValidateExecution(
        TrainingExportPlan plan,
        TrainingExportExecutionResult execution)
    {
        if (!execution.PlanId.Equals(plan.PlanId, StringComparison.OrdinalIgnoreCase)
            || !execution.PlanSha256.Equals(plan.PlanId, StringComparison.OrdinalIgnoreCase))
        {
            throw new TrainingExportPlanException("Exportbestaetigung gehoert nicht zum aktuellen Plan.");
        }
        if (execution.TotalImages != plan.Images.Count
            || execution.TrainImages != plan.Images.Count(image => image.Target == TrainingExportTarget.Train)
            || execution.ValidationImages != plan.Images.Count(image => image.Target == TrainingExportTarget.Validation)
            || execution.ClassCount != plan.Classes.Count)
        {
            throw new TrainingExportPlanException("Exportbestaetigung hat unpassende Zaehler.");
        }

        var plannedHashes = plan.Images
            .Select(image => image.ImageSha256)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);
        var writtenHashes = execution.WrittenImageSha256
            .ToHashSet(StringComparer.OrdinalIgnoreCase);
        if (writtenHashes.Count != execution.WrittenImageSha256.Count
            || !plannedHashes.SetEquals(writtenHashes))
        {
            throw new TrainingExportPlanException(
                "Exportbestaetigung enthaelt nicht genau alle geplanten Bild-Hashes.");
        }
    }
}
