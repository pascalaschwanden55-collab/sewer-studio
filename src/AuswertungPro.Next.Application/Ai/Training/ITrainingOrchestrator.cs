namespace AuswertungPro.Next.Application.Ai.Training;

/// <summary>
/// Sprint 1 (2026-05-07): Marker-Interface fuer Trainings-Orchestratoren.
///
/// Vereint die Familie der Trainings-Pipelines unter einem gemeinsamen Typ:
/// <list type="bullet">
/// <item><see cref="ISelfTrainingOrchestrator"/> — Self-Training pro Fall (PDF-basiert)</item>
/// <item><see cref="IBatchSelfTrainingOrchestrator"/> — Batch-Lauf ueber Ordner-Tree</item>
/// <item><c>IVideoSelfTrainingOrchestrator</c> — Video-basiertes Selbsttraining</item>
/// <item><c>IQwenLoraOrchestrator</c> — Qwen-LoRA-Nachtraining</item>
/// <item><c>IYoloRetrainOrchestrator</c> — YOLO-Modell-Nachtraining</item>
/// <item><c>IInitialTrainingOrchestrator</c> — Erstaufbau der KB</item>
/// </list>
///
/// Nutzen:
/// - DI kann via <c>services.AddTransient&lt;ITrainingOrchestrator&gt;</c> alle Orchestratoren
///   gesammelt aufzaehlen (z.B. fuer ein Trainings-Dashboard).
/// - Trainings-Center-ViewModels koennen orchestrator-agnostisch starten/stoppen.
/// - Konvention dokumentiert: jeder neue Orchestrator implementiert dieses Interface.
///
/// Das Interface ist intentionsweise leer (Marker), weil die concrete Run-Signaturen
/// pro Orchestrator unterschiedlich sind (PDF/Video/LoRA-spezifische Inputs + Results).
/// Eine generische Run-Methode wuerde nur den Lowest-Common-Denominator abbilden.
/// </summary>
public interface ITrainingOrchestrator
{
    /// <summary>Klar lesbarer Name fuer Logs/UI/Telemetry.</summary>
    string Name { get; }
}
