namespace AuswertungPro.Next.Application.Ai.Training.ExportPlans;

/// <summary>
/// Alle Laufzeitangaben fuer genau einen plan-gesteuerten YOLO-Export.
/// </summary>
public sealed record TrainingYoloExportCommand(
    DateTimeOffset GeneratedUtc,
    TrainingYoloExportMode Mode = TrainingYoloExportMode.Execute,
    IReadOnlyList<TrainingSample>? UpdateTargets = null);

/// <summary>
/// Execute schreibt den geprueften Datensatz. PlanOnly fuehrt alle fachlichen
/// Pruefungen aus, veraendert aber weder Samples noch Exportdateien.
/// </summary>
public enum TrainingYoloExportMode
{
    Execute,
    PlanOnly
}

public enum TrainingYoloExportProgressStage
{
    PreparingSamples,
    InspectingInventory,
    CreatingPlan,
    ExecutingPlan,
    Completing,
    Completed,
    Planned,
    NoImages
}

/// <summary>
/// Typisierte Fortschrittsmeldung. Die UI entscheidet selbst, wie sie diese zeigt.
/// </summary>
public sealed record TrainingYoloExportProgress(
    TrainingYoloExportProgressStage Stage,
    string Message,
    int Processed = 0,
    int? Total = null);

public enum TrainingYoloExportResultStatus
{
    Completed,
    Planned,
    NoImages
}

/// <summary>
/// Gesamtergebnis eines Exportbefehls. Bei einem leeren Plan bleibt Execution null.
/// </summary>
public sealed record TrainingYoloExportResult(
    TrainingYoloExportResultStatus Status,
    TrainingExportPlan Plan,
    TrainingExportExecutionOutcome? Execution,
    TrainingExportCompletionResult Completion);

/// <summary>
/// Steuert Auswahl, Live-Inventar, Plan, Ausfuehrung und Abschlussmarkierung.
/// </summary>
public interface ITrainingYoloExportCoordinator
{
    Task<TrainingYoloExportResult> RunAsync(
        TrainingYoloExportCommand command,
        IProgress<TrainingYoloExportProgress>? progress = null,
        CancellationToken cancellationToken = default);
}
