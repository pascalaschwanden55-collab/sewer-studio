using System.Collections.Generic;
using AuswertungPro.Next.Application.Ai;
using AuswertungPro.Next.Infrastructure.Ai.Pipeline;
using Xunit;

namespace AuswertungPro.Next.Pipeline.Tests;

public class SegmentedFindingBuilderTests
{
    private static SamMaskResult Mask(string label, double x1, double y1, double x2, double y2)
        => new(label, 0.9, new[] { x1, y1, x2, y2 }, "0", 100, 10000, 10, 10,
               (x1 + x2) / 2, (y1 + y2) / 2);

    private static MaskQuantificationService.QuantifiedMask Quant(string label)
        => new(label, 0.9, null, null, null, null, null, null);

    private static DinoDetectionDto Dino(string label, double x1, double y1, double x2, double y2)
        => new(x1, y1, x2, y2, label, 0.77, label);

    private static SamResponse Sam(IReadOnlyList<SamMaskResult> masks)
        => new(masks, 1000, 1000, 5);

    [Fact]
    public void SkippedMiddleBox_ordnet_die_zwei_Masken_den_richtigen_DinoBoxen_zu()
    {
        // 3 DINO-Boxen; SAM liefert nur Maske fuer Box 1 und Box 3 (Box 2 uebersprungen).
        var dinos = new List<DinoDetectionDto>
        {
            Dino("crack", 0, 0, 100, 100),
            Dino("root", 200, 200, 300, 300),
            Dino("deposit", 400, 400, 500, 500),
        };
        var masks = new List<SamMaskResult>
        {
            Mask("crack", 0, 0, 100, 100),
            Mask("deposit", 400, 400, 500, 500),
        };
        var quant = new List<MaskQuantificationService.QuantifiedMask> { Quant("crack"), Quant("deposit") };

        var segs = SegmentedFindingBuilder.Build(
            Sam(masks), dinos, quant, 0.5, 0.5, 0.5, MetrierungProximityThresholds.Default);

        Assert.Equal(2, segs.Count);
        Assert.Equal("crack", segs[0].Dino!.Label);
        Assert.Equal("deposit", segs[1].Dino!.Label); // NICHT "root" (kein Index-Verrutschen)
    }

    [Fact]
    public void Maske_ohne_passende_DinoBox_hat_Dino_null()
    {
        var dinos = new List<DinoDetectionDto> { Dino("crack", 0, 0, 100, 100) };
        var masks = new List<SamMaskResult> { Mask("root", 800, 800, 900, 900) };
        var quant = new List<MaskQuantificationService.QuantifiedMask> { Quant("root") };

        var segs = SegmentedFindingBuilder.Build(
            Sam(masks), dinos, quant, 0.5, 0.5, 0.5, MetrierungProximityThresholds.Default);

        Assert.Single(segs);
        Assert.Null(segs[0].Dino);
        Assert.Equal("root", segs[0].Mask.Label); // Fallback ueber die Maske bleibt nutzbar
    }

    [Fact]
    public void GleichesLabel_zwei_Boxen_wird_ueber_IoU_nicht_Reihenfolge_zugeordnet()
    {
        var dinos = new List<DinoDetectionDto>
        {
            Dino("crack", 0, 0, 100, 100),
            Dino("crack", 600, 600, 700, 700),
        };
        var masks = new List<SamMaskResult> { Mask("crack", 600, 600, 700, 700) };
        var quant = new List<MaskQuantificationService.QuantifiedMask> { Quant("crack") };

        var segs = SegmentedFindingBuilder.Build(
            Sam(masks), dinos, quant, 0.5, 0.5, 0.5, MetrierungProximityThresholds.Default);

        Assert.Single(segs);
        Assert.Equal(600, segs[0].Dino!.X1); // die zweite crack-Box (hohe IoU), nicht die erste
    }
}
