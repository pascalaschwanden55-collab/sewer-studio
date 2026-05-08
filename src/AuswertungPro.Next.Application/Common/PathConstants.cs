using System.IO;
using AuswertungPro.Next.Application.Ai;

namespace AuswertungPro.Next.Application.Common;

/// <summary>
/// Sprint 1 (2026-05-07): Zentrale Anlaufstelle fuer alle Pfad-Strings unter
/// dem KnowledgeRoot- und AppData-Verzeichnis. Vorher waren die Datei-Namen
/// (training_samples.json, review_queue.json, ...) ueber den ganzen Code
/// verstreut — Tippfehler-Risiko + erschwert das Auffinden.
///
/// Konvention:
/// - Datei-Namen sind <c>const</c> (compile-time, im Diff sofort sichtbar).
/// - Helper-Methoden <c>InKnowledgeRoot</c>/<c>InAppData</c> liefern den
///   absoluten Pfad. Aufrufer nutzen <c>PathConstants.InKnowledgeRoot(PathConstants.TrainingSamplesFile)</c>.
/// - Bei neuen Dateien IMMER hier ergaenzen, nicht inline einstreuen.
/// </summary>
public static class PathConstants
{
    // ── Datei-Namen unter KnowledgeRoot ────────────────────────────

    public const string KnowledgeBaseDb = "KnowledgeBase.db";
    public const string TrainingSamplesFile = "training_samples.json";
    public const string ReviewQueueFile = "review_queue.json";
    public const string EscalationQueueFile = "escalation_queue.jsonl";
    public const string TeacherAnnotationsFile = "teacher_annotations.json";
    public const string YoloClassMapFile = "yolo_class_map.json";
    public const string FewShotExamplesFile = "fewshot_examples.json";
    public const string SelfTrainingHistoryFile = "selftraining_history.json";
    public const string TrainingRunsFile = "training_runs.json";
    public const string TrainingCenterFile = "training_center.json";
    public const string TrainingSettingsFile = "training_settings.json";
    public const string MirrorManifestFile = "manifest.json";

    // ── Datei-Namen unter AppData (LocalAppData/SewerStudio) ──────

    public const string AiSanierungSessionsFile = "ai_sanierung_sessions.json";
    public const string MaintenanceStateFile = "maintenance.json";
    public const string SidecarTokenFile = ".sidecar_token";
    public const string PipelineTelemetryDb = "pipeline_telemetry.db";

    // ── Verzeichnis-Namen ─────────────────────────────────────────

    public const string FramesDir = "frames";
    public const string TrainingFramesDir = "training_frames";
    public const string LogsDir = "logs";

    // ── Helper-Methoden ───────────────────────────────────────────

    /// <summary>Liefert {KnowledgeRoot}/{fileName}.</summary>
    public static string InKnowledgeRoot(string fileName)
        => Path.Combine(KnowledgeRootProvider.GetRoot(), fileName);

    /// <summary>Liefert {LocalAppData}/SewerStudio/{fileName}.</summary>
    public static string InAppData(string fileName)
        => Path.Combine(AppDataPathProvider.GetAppDataDir(), fileName);

    /// <summary>Liefert {LocalAppData}/SewerStudio/logs/{fileName}.</summary>
    public static string InLogsDir(string fileName)
        => Path.Combine(AppDataPathProvider.GetAppDataDir(), LogsDir, fileName);
}
