using AuswertungPro.Next.Application.Ai;
using Xunit;

namespace AuswertungPro.Next.Pipeline.Tests;

public class PipelineHealthEvaluatorTests
{
    private static PipelineHealthInputs Base(
        bool ai = true, bool reach = true, bool token = true, bool healthy = true, bool qwen = true,
        bool yolo = true, bool dino = true, bool sam = true, bool? detectorQualified = true)
        => new(ai, reach, token, healthy, qwen, yolo, dino, sam, detectorQualified);

    [Fact]
    public void KiDeaktiviert_ergibt_Down()
    {
        var s = PipelineHealthEvaluator.Evaluate(Base(ai: false));
        Assert.Equal(PipelineHealthLevel.Down, s.Level);
        Assert.False(s.MultiModelActive);
    }

    [Fact]
    public void SidecarOk_und_TokenOk_ergibt_Full()
    {
        var s = PipelineHealthEvaluator.Evaluate(Base());
        Assert.Equal(PipelineHealthLevel.Full, s.Level);
        Assert.True(s.MultiModelActive);
    }

    [Fact]
    public void SidecarOffline_aber_Qwen_ergibt_Degraded()
    {
        var s = PipelineHealthEvaluator.Evaluate(Base(reach: false, healthy: false));
        Assert.Equal(PipelineHealthLevel.Degraded, s.Level);
        Assert.False(s.MultiModelActive);
        Assert.Contains("Sidecar", s.Detail);
    }

    [Fact]
    public void Token401_aber_Qwen_ergibt_Degraded_mit_TokenHinweis()
    {
        var s = PipelineHealthEvaluator.Evaluate(Base(token: false));
        Assert.Equal(PipelineHealthLevel.Degraded, s.Level);
        Assert.Contains("Token", s.Detail);
    }

    [Fact]
    public void Token401_ohne_Qwen_ergibt_Down()
    {
        var s = PipelineHealthEvaluator.Evaluate(Base(token: false, qwen: false));
        Assert.Equal(PipelineHealthLevel.Down, s.Level);
    }

    [Fact]
    public void SidecarOffline_ohne_Qwen_ergibt_Down()
    {
        var s = PipelineHealthEvaluator.Evaluate(Base(reach: false, healthy: false, qwen: false));
        Assert.Equal(PipelineHealthLevel.Down, s.Level);
    }

    [Fact]
    public void ModelleNochNichtGeladen_bleibt_Full_mit_LazyHinweis()
    {
        var s = PipelineHealthEvaluator.Evaluate(Base(yolo: false, dino: false, sam: false));
        Assert.Equal(PipelineHealthLevel.Full, s.Level);
        Assert.True(s.MultiModelActive);
        Assert.Contains("Bedarf", s.Detail);
    }

    [Fact]
    public void SidecarErreichbar_aber_nicht_healthy_ergibt_Degraded()
    {
        var s = PipelineHealthEvaluator.Evaluate(Base(healthy: false));
        Assert.Equal(PipelineHealthLevel.Degraded, s.Level);
    }

    [Fact]
    public void Unqualifizierter_Detektor_ergibt_Degraded_aber_Dino_und_Sam_bleiben_aktiv()
    {
        var input = Base() with
        {
            DetectorQualified = false,
            DetectorQualificationReason = "BBox-Kollaps"
        };

        var s = PipelineHealthEvaluator.Evaluate(input);

        Assert.Equal(PipelineHealthLevel.Degraded, s.Level);
        Assert.True(s.MultiModelActive);
        Assert.True(s.AnalysisPossible);
        Assert.Equal(false, s.DetectorQualified);
        Assert.Contains("DINO + SAM", s.Summary);
        Assert.Contains("BBox-Kollaps", s.Detail);
        Assert.Contains("geprueft", s.Detail);
    }

    [Fact]
    public void Fehlende_Detektorqualifikation_ist_fail_closed_und_review_pflichtig()
    {
        var s = PipelineHealthEvaluator.Evaluate(Base(detectorQualified: null));

        Assert.Equal(PipelineHealthLevel.Degraded, s.Level);
        Assert.True(s.MultiModelActive);
        Assert.Null(s.DetectorQualified);
        Assert.Contains("fehlt", s.Detail);
        Assert.Contains("geprueft", s.Detail);
    }
}
