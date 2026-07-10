using AuswertungPro.Next.Application.Ai;
using AuswertungPro.Next.Domain.Models;
using AuswertungPro.Next.Domain.Protocol;
using AuswertungPro.Next.Infrastructure.Ai.Pipeline;
using AuswertungPro.Next.UI.Ai;

namespace AuswertungPro.Next.UI.Tests;

public sealed class CodingMultiModelEventFactoryTests
{
    private static readonly QuantificationGate.ManifestQuantRule AllowAllQuantification = new(
        HasQ1: true,
        HasQ2: true,
        AllowClock: true);

    [Fact]
    public void Create_builds_entry_and_ai_context()
    {
        var videoTime = TimeSpan.FromSeconds(11);
        var segmented = Segmented();

        var draft = CodingMultiModelEventFactory.Create(
            "BCAEB",
            "Anschluss",
            segmented,
            meter: 4.8,
            videoTime,
            dinoConfidence: 0.73,
            compositeConfidence: 0.81,
            imageWidth: 100,
            imageHeight: 100,
            meterFromOsd: true,
            calibration: CalibratedPipe(),
            AllowAllQuantification);

        Assert.Equal(ProtocolEntrySource.Ai, draft.Entry.Source);
        Assert.Equal("BCAEB", draft.Entry.Code);
        Assert.Equal("Anschluss", draft.Entry.Beschreibung);
        Assert.Equal(4.8, draft.Entry.MeterStart);
        Assert.Equal(videoTime, draft.Entry.Zeit);
        Assert.Equal("BCAEB", draft.AiContext.SuggestedCode);
        Assert.Equal(0.81, draft.AiContext.Confidence);
        Assert.Equal("connection (DINO 73%)", draft.AiContext.Reason);
        Assert.Equal(0.73, draft.AiContext.Evidence!.DinoConf);
        Assert.Equal(0.87, draft.AiContext.Evidence.SamMaskStability);
        Assert.Equal("BCAEB", draft.AiContext.Evidence.DamageCategory);
        Assert.Equal("mask-rle", draft.AiContext.SamMaskRle);
        Assert.Equal(100, draft.AiContext.SamMaskImageWidth);
        Assert.Equal(100, draft.AiContext.SamMaskImageHeight);
        Assert.Equal(CodingUserDecision.Ignored, draft.AiContext.Decision);
    }

    [Fact]
    public void Create_marks_estimated_meter_and_writes_quantification_meta()
    {
        var draft = CodingMultiModelEventFactory.Create(
            "BCAEB",
            officialLabel: null,
            Segmented(),
            meter: 2.0,
            videoTime: TimeSpan.Zero,
            dinoConfidence: 0.7,
            compositeConfidence: 0.8,
            imageWidth: 100,
            imageHeight: 100,
            meterFromOsd: false,
            calibration: CalibratedPipe(),
            AllowAllQuantification);

        Assert.Equal("connection", draft.Entry.Beschreibung);
        Assert.Equal("geschaetzt", draft.Entry.CodeMeta!.Parameters["vsa.meter.quelle"]);
        Assert.Equal("12", draft.Entry.CodeMeta.Parameters["vsa.hoehe.mm"]);
        Assert.Equal("8", draft.Entry.CodeMeta.Parameters["vsa.breite.mm"]);
        Assert.Equal(QuantificationCodeMetaWriter.QuantStatusVorschlag, draft.Entry.CodeMeta.Parameters["vsa.quant.quelle"]);
        Assert.Equal("3:00", draft.Entry.CodeMeta.Parameters["vsa.uhr.von"]);
    }

    [Fact]
    public void Create_builds_rectangle_overlay_from_mask_bbox()
    {
        var draft = CodingMultiModelEventFactory.Create(
            "BCAEB",
            "Anschluss",
            Segmented(),
            meter: 1.0,
            videoTime: TimeSpan.Zero,
            dinoConfidence: 0.7,
            compositeConfidence: 0.8,
            imageWidth: 100,
            imageHeight: 100,
            meterFromOsd: true,
            calibration: CalibratedPipe(),
            AllowAllQuantification);

        Assert.NotNull(draft.Overlay);
        Assert.Equal(OverlayToolType.Rectangle, draft.Overlay.ToolType);
        Assert.Equal(
            new[] { (0.7, 0.4), (0.9, 0.4), (0.9, 0.6), (0.7, 0.6) },
            draft.Overlay.Points.Select(p => (p.X, p.Y)).ToArray());
    }

    private static SegmentedFinding Segmented()
    {
        var mask = new SamMaskResult(
            "connection",
            0.87,
            [70, 40, 90, 60],
            "mask-rle",
            MaskAreaPixels: 100,
            ImageAreaPixels: 10_000,
            HeightPixels: 40,
            WidthPixels: 60,
            CentroidX: 40,
            CentroidY: 40);

        var quant = new MaskQuantificationService.QuantifiedMask(
            Label: "connection",
            Confidence: 0.87,
            HeightMm: 12,
            WidthMm: 8,
            ExtentPercent: 4,
            CrossSectionReductionPercent: null,
            IntrusionPercent: null,
            ClockPosition: "3:00");

        var proximity = new MetrierungProximityResult(
            MetrierungProximity.Codierbar,
            "test",
            FillRatio: 0,
            DistToVanish: 0,
            OuterRadius: 0,
            WandNaehe: true,
            EnthaeltCenter: false);

        return new SegmentedFinding(Dino: null, mask, quant, proximity);
    }

    private static PipeCalibration CalibratedPipe()
        => new()
        {
            NominalDiameterMm = 300,
            NormalizedDiameter = 0.7,
            Source = CalibrationSource.Auto
        };
}
