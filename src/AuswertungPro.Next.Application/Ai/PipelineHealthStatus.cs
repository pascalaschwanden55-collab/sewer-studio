namespace AuswertungPro.Next.Application.Ai;

/// <summary>Ampel-Stufe der KI-Pipeline im Codiermodus.</summary>
public enum PipelineHealthLevel
{
    /// <summary>Volle Multi-Model-Pipeline aktiv (gruen).</summary>
    Full,
    /// <summary>Eingeschraenkter Betrieb, zum Beispiel Qwen-only oder DINO/SAM ohne freigegebenes YOLO.</summary>
    Degraded,
    /// <summary>KI aus oder gar nichts nutzbar (rot/grau).</summary>
    Down
}

/// <summary>
/// Ehrlicher Momentanzustand der KI-Pipeline. Reines Datenobjekt fuer UI + Monitor.
/// </summary>
public sealed record PipelineHealthStatus(
    PipelineHealthLevel Level,
    bool MultiModelActive,
    bool SidecarReachable,
    bool TokenValid,
    bool SidecarHealthy,
    bool QwenAvailable,
    bool YoloLoaded,
    bool DinoLoaded,
    bool SamLoaded,
    string Summary,
    string Detail,
    bool? DetectorQualified = null,
    string? DetectorQualificationReason = null)
{
    /// <summary>True, solange ueberhaupt eine KI-Analyse moeglich ist (Full oder Degraded).</summary>
    public bool AnalysisPossible => Level != PipelineHealthLevel.Down;
}
