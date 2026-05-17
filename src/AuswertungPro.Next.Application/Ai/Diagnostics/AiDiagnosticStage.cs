namespace AuswertungPro.Next.Application.Ai.Diagnostics;

/// <summary>
/// Vordefinierte Stage-Namen fuer <see cref="AiDiagnosticEvent.Stage"/>.
/// Wildwuchs vermeiden — eine Stelle der Wahrheit fuer Filter, UI-Gruppierung
/// und Auswertung. Falls neue Stages noetig werden, hier als Konstante
/// ergaenzen und nicht im Aufruf-Code als String hartcodieren.
/// </summary>
public static class AiDiagnosticStage
{
    // Qwen (VLM) Pfad
    public const string QwenRaw         = "qwen.raw";         // Roh-JSON aus OllamaClient
    public const string QwenMapped      = "qwen.mapped";      // Nach MapToAnalysis (Domain-Findings)
    public const string QwenSuppressed  = "qwen.suppressed";  // ViewType-/Quality-Suppression
    public const string QwenError       = "qwen.error";       // Exception / Timeout

    // YOLO Pfad
    public const string YoloRaw         = "yolo.raw";         // YOLO-Detektionen vor Filter
    public const string YoloError       = "yolo.error";       // Sidecar-Fehler / Time-out

    // Multi-Model-Aggregation (DINO + SAM)
    public const string MultiModelRaw   = "multimodel.raw";

    // Coding-Filter-Pfad (UI-seitig, nach Qwen/Multi-Model)
    public const string CodingFilterDrop = "coding.filter.drop";

    // Geometrie (Rohrachse / Bogen-Detektor)
    public const string PipeAxisGeometry = "pipeaxis.geometry";

    // Event in KI-Befund-Liste eingetragen
    public const string EventCreated     = "event.created";
}
