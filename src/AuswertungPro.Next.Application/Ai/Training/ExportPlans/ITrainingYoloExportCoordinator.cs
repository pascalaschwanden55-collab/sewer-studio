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
    NoImages,
    /// <summary>
    /// Hinweisstufe: Das Pilot-Freigaberegister ist aktiv und vollstaendige
    /// Goldsamples ohne Registereintrag wurden nicht exportiert.
    /// </summary>
    RegistryGateNotice
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
/// RegistryGateSkippedSampleIds listet vollstaendige Goldsamples, die nur deshalb
/// nicht exportiert wurden, weil sie im aktiven Freigaberegister fehlen. Null,
/// wenn das Register leer ist (dann gilt kein Gate) oder nichts zurueckblieb.
/// </summary>
public sealed record TrainingYoloExportResult(
    TrainingYoloExportResultStatus Status,
    TrainingExportPlan Plan,
    TrainingExportExecutionOutcome? Execution,
    TrainingExportCompletionResult Completion,
    IReadOnlyList<string>? RegistryGateSkippedSampleIds = null);

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
