namespace AuswertungPro.Next.Application.Ai.Training.ExportPlans;

/// <summary>
/// Entscheidet Klassen, Ausschluesse, Dateinamen und Haltungssplit genau einmal.
/// Sidecar und lokaler Export duerfen das Ergebnis danach nur noch ausfuehren.
/// </summary>
public interface ITrainingExportPlanService
{
    TrainingExportPlanBundle CreatePlan(TrainingExportPlanRequest request);
}
