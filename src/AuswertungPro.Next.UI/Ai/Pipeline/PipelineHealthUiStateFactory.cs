using System.Windows.Media;
using AuswertungPro.Next.Application.Ai;

namespace AuswertungPro.Next.UI.Ai.Pipeline;

public sealed record PipelineHealthDetailsUiState(
    string Sidecar,
    string Token,
    string Yolo,
    string Dino,
    string Sam,
    string Mode);

public sealed record PipelineHealthUiState(
    string Summary,
    string Detail,
    Color Color,
    bool AnalysisEnabled,
    PipelineHealthDetailsUiState Details);

public static class PipelineHealthUiStateFactory
{
    public static PipelineHealthUiState Create(PipelineHealthStatus status)
    {
        var color = status.Level switch
        {
            PipelineHealthLevel.Full => Color.FromRgb(0x22, 0xC5, 0x5E),
            PipelineHealthLevel.Degraded => Color.FromRgb(0xF5, 0x9E, 0x0B),
            _ => Color.FromRgb(0x94, 0xA3, 0xB8)
        };

        return new PipelineHealthUiState(
            status.Summary,
            status.Detail,
            color,
            status.AnalysisPossible,
            CreateDetails(status));
    }

    private static PipelineHealthDetailsUiState CreateDetails(PipelineHealthStatus status)
    {
        static string OkBad(bool ok) => ok ? "OK" : "fehlt";
        static string Loaded(bool ok) => ok ? "geladen" : "laedt bei Bedarf";

        var sidecar = status.SidecarReachable
            ? status.SidecarHealthy ? "OK" : "antwortet, ungesund"
            : "offline";
        var detectorRestricted =
            status.MultiModelActive
            && status.Level == PipelineHealthLevel.Degraded
            && status.DetectorQualified != true;
        var mode = status.MultiModelActive
            ? detectorRestricted
                ? "Multi-Model (DINO + SAM)"
                : "Multi-Model"
            : status.QwenAvailable ? "Qwen-only" : "KI aus";
        var yolo = status.DetectorQualified switch
        {
            false => "nicht qualifiziert",
            null when detectorRestricted => "Qualifikation fehlt",
            _ => Loaded(status.YoloLoaded),
        };

        return new PipelineHealthDetailsUiState(
            $"Sidecar: {sidecar}",
            $"Token: {(status.SidecarReachable ? OkBad(status.TokenValid) : "-")}",
            $"YOLO: {yolo}",
            $"DINO: {Loaded(status.DinoLoaded)}",
            $"SAM: {Loaded(status.SamLoaded)}",
            $"Modus: {mode}");
    }
}
