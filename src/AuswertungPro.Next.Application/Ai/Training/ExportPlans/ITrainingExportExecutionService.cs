namespace AuswertungPro.Next.Application.Ai.Training.ExportPlans;

/// <summary>
/// Beschreibt den tatsaechlich verwendeten, fachlich gleichwertigen Ausfuehrungsweg.
/// </summary>
public enum TrainingExportExecutionRoute
{
    Sidecar,
    LocalRequested,
    LocalSidecarOffline,
    LocalRequestTooLarge,
    LocalAfterTransportFailure
}

/// <summary>
/// Ergebnis eines ausgefuehrten Plans samt dem verwendeten Weg.
/// </summary>
public sealed record TrainingExportExecutionOutcome(
    TrainingExportExecutionRoute Route,
    TrainingExportExecutionResult Result,
    string? SidecarVersion = null,
    string? Detail = null);

/// <summary>
/// Fuehrt einen bereits verbindlich erstellten Exportplan aus. Der Dienst darf
/// keine Klassen-, Split- oder Dateinamenentscheidung mehr treffen.
/// </summary>
public interface ITrainingExportExecutionService
{
    Task<TrainingExportExecutionOutcome> ExecuteAsync(
        TrainingExportPlanBundle bundle,
        CancellationToken cancellationToken = default);
}
