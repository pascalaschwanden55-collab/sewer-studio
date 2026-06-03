namespace AuswertungPro.Next.Application.Ai;

/// <summary>
/// Reine Eingabe fuer den <see cref="PipelineHealthEvaluator"/>. Entkoppelt die Logik
/// von HTTP/DTO-Details, damit die Application-Schicht nicht von Infrastructure abhaengt.
/// </summary>
public sealed record PipelineHealthInputs(
    bool AiEnabled,
    bool SidecarReachable,
    bool TokenValid,
    bool SidecarHealthy,
    bool QwenAvailable,
    bool YoloLoaded = false,
    bool DinoLoaded = false,
    bool SamLoaded = false);
