namespace AuswertungPro.Next.Application.Ai;

/// <summary>
/// Reine Auswertung: wandelt <see cref="PipelineHealthInputs"/> in einen
/// <see cref="PipelineHealthStatus"/>. Keine Seiteneffekte, voll testbar.
///
/// Ampel-Regeln (siehe Spec 2026-06-03):
/// - KI aus -> Down.
/// - Sidecar erreichbar + healthy + Token ok -> Full (Multi-Model).
/// - Sidecar nicht nutzbar (offline / Token / unhealthy), aber Qwen da -> Degraded.
/// - Sidecar nicht nutzbar und kein Qwen -> Down.
/// - Modelle wegen Lazy-Loading noch nicht resident -> bleibt Full, Detail "laedt bei Bedarf".
/// </summary>
public static class PipelineHealthEvaluator
{
    public static PipelineHealthStatus Evaluate(PipelineHealthInputs i)
    {
        if (!i.AiEnabled)
            return new PipelineHealthStatus(
                PipelineHealthLevel.Down, false,
                i.SidecarReachable, i.TokenValid, i.SidecarHealthy, i.QwenAvailable,
                i.YoloLoaded, i.DinoLoaded, i.SamLoaded,
                "Kuenstliche Intelligenz deaktiviert",
                "KI ist in den Einstellungen aus.");

        bool sidecarUsable = i.SidecarReachable && i.SidecarHealthy && i.TokenValid;

        if (sidecarUsable)
        {
            bool allLoaded = i.YoloLoaded && i.DinoLoaded && i.SamLoaded;
            var detail = allLoaded
                ? "YOLO + DINO + SAM aktiv."
                : "Pipeline bereit. Modelle laden bei Bedarf.";
            return new PipelineHealthStatus(
                PipelineHealthLevel.Full, true,
                true, true, true, i.QwenAvailable,
                i.YoloLoaded, i.DinoLoaded, i.SamLoaded,
                "KI bereit (Multi-Model)", detail);
        }

        // Sidecar nicht nutzbar -> Grund bestimmen.
        string grund;
        if (!i.SidecarReachable) grund = "Sidecar offline -> keine YOLO/DINO/SAM-Masken.";
        else if (!i.TokenValid) grund = "Sidecar Token ungueltig -> Qwen-only.";
        else grund = "Sidecar antwortet, ist aber nicht gesund -> Qwen-only.";

        if (i.QwenAvailable)
            return new PipelineHealthStatus(
                PipelineHealthLevel.Degraded, false,
                i.SidecarReachable, i.TokenValid, i.SidecarHealthy, true,
                i.YoloLoaded, i.DinoLoaded, i.SamLoaded,
                "KI bereit (Qwen)", grund);

        return new PipelineHealthStatus(
            PipelineHealthLevel.Down, false,
            i.SidecarReachable, i.TokenValid, i.SidecarHealthy, false,
            i.YoloLoaded, i.DinoLoaded, i.SamLoaded,
            "KI nicht verfuegbar", grund);
    }
}
