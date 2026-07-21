using AuswertungPro.Next.Application.Ai.Training.ClassMaps;
using AuswertungPro.Next.Application.Ai.Training.Inventory;

namespace AuswertungPro.Next.Application.Ai.Training.ExportPlans;

/// <summary>
/// Verbindet einen einzigen Live-Inventarlauf mit dem reinen ExportPlanner.
/// Dateihashes werden geprueft, aber noch keine Exportdateien geschrieben.
/// </summary>
public interface ITrainingExportPlanInputBuilder
{
    Task<TrainingExportPlanRequest> BuildAsync(
        TrainingDataInventoryRuntimeSnapshot inventory,
        TrainingExportRegistrySnapshot registry,
        IReadOnlySet<string> approvedTrainingSampleIds,
        TrainingYoloClassMapSnapshot classMap,
        DateTimeOffset generatedUtc,
        CancellationToken cancellationToken = default);
}
