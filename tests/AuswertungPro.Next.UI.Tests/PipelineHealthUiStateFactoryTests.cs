using System.Windows.Media;
using AuswertungPro.Next.Application.Ai;
using AuswertungPro.Next.UI.Ai;
using AuswertungPro.Next.UI.Ai.Pipeline;

namespace AuswertungPro.Next.UI.Tests;

public sealed class PipelineHealthUiStateFactoryTests
{
    [Fact]
    public void Create_maps_full_pipeline_to_enabled_green_multimodel_state()
    {
        var status = new PipelineHealthStatus(
            PipelineHealthLevel.Full,
            MultiModelActive: true,
            SidecarReachable: true,
            TokenValid: true,
            SidecarHealthy: true,
            QwenAvailable: true,
            YoloLoaded: true,
            DinoLoaded: true,
            SamLoaded: true,
            Summary: "bereit",
            Detail: "alles ok");

        var state = PipelineHealthUiStateFactory.Create(status);

        Assert.Equal("bereit", state.Summary);
        Assert.Equal("alles ok", state.Detail);
        Assert.Equal(Color.FromRgb(0x22, 0xC5, 0x5E), state.Color);
        Assert.True(state.AnalysisEnabled);
        Assert.Equal("Sidecar: OK", state.Details.Sidecar);
        Assert.Equal("Token: OK", state.Details.Token);
        Assert.Equal("YOLO: geladen", state.Details.Yolo);
        Assert.Equal("DINO: geladen", state.Details.Dino);
        Assert.Equal("SAM: geladen", state.Details.Sam);
        Assert.Equal("Modus: Multi-Model", state.Details.Mode);
    }

    [Fact]
    public void Create_maps_down_offline_pipeline_to_disabled_gray_state()
    {
        var status = new PipelineHealthStatus(
            PipelineHealthLevel.Down,
            MultiModelActive: false,
            SidecarReachable: false,
            TokenValid: false,
            SidecarHealthy: false,
            QwenAvailable: false,
            YoloLoaded: false,
            DinoLoaded: false,
            SamLoaded: false,
            Summary: "aus",
            Detail: "offline");

        var state = PipelineHealthUiStateFactory.Create(status);

        Assert.Equal(Color.FromRgb(0x94, 0xA3, 0xB8), state.Color);
        Assert.False(state.AnalysisEnabled);
        Assert.Equal("Sidecar: offline", state.Details.Sidecar);
        Assert.Equal("Token: -", state.Details.Token);
        Assert.Equal("YOLO: laedt bei Bedarf", state.Details.Yolo);
        Assert.Equal("DINO: laedt bei Bedarf", state.Details.Dino);
        Assert.Equal("SAM: laedt bei Bedarf", state.Details.Sam);
        Assert.Equal("Modus: KI aus", state.Details.Mode);
    }

    [Fact]
    public void Create_shows_unqualified_yolo_but_keeps_dino_sam_analysis_enabled()
    {
        var status = new PipelineHealthStatus(
            PipelineHealthLevel.Degraded,
            MultiModelActive: true,
            SidecarReachable: true,
            TokenValid: true,
            SidecarHealthy: true,
            QwenAvailable: true,
            YoloLoaded: true,
            DinoLoaded: true,
            SamLoaded: true,
            Summary: "KI eingeschraenkt (DINO + SAM)",
            Detail: "BBox-Kollaps. Ergebnisse pruefen.",
            DetectorQualified: false,
            DetectorQualificationReason: "BBox-Kollaps");

        var state = PipelineHealthUiStateFactory.Create(status);

        Assert.Equal(Color.FromRgb(0xF5, 0x9E, 0x0B), state.Color);
        Assert.True(state.AnalysisEnabled);
        Assert.Equal("YOLO: nicht qualifiziert", state.Details.Yolo);
        Assert.Equal("DINO: geladen", state.Details.Dino);
        Assert.Equal("SAM: geladen", state.Details.Sam);
        Assert.Equal("Modus: Multi-Model (DINO + SAM)", state.Details.Mode);
    }

    [Fact]
    public void Create_shows_missing_qualification_as_degraded_not_green()
    {
        var status = new PipelineHealthStatus(
            PipelineHealthLevel.Degraded,
            MultiModelActive: true,
            SidecarReachable: true,
            TokenValid: true,
            SidecarHealthy: true,
            QwenAvailable: true,
            YoloLoaded: false,
            DinoLoaded: true,
            SamLoaded: true,
            Summary: "KI eingeschraenkt (DINO + SAM)",
            Detail: "Qualifikationsstatus fehlt. Ergebnis pruefen.",
            DetectorQualified: null,
            DetectorQualificationReason: "Qualifikationsstatus fehlt");

        var state = PipelineHealthUiStateFactory.Create(status);

        Assert.Equal(Color.FromRgb(0xF5, 0x9E, 0x0B), state.Color);
        Assert.True(state.AnalysisEnabled);
        Assert.Equal("YOLO: Qualifikation fehlt", state.Details.Yolo);
        Assert.Equal("Modus: Multi-Model (DINO + SAM)", state.Details.Mode);
    }
}
