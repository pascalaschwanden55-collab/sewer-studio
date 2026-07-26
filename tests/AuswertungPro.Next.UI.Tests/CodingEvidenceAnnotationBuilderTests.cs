using AuswertungPro.Next.Domain.Models;
using AuswertungPro.Next.Domain.Protocol;
using AuswertungPro.Next.UI.Ai;
using AuswertungPro.Next.UI.Ai.Coding;

namespace AuswertungPro.Next.UI.Tests;

public sealed class CodingEvidenceAnnotationBuilderTests
{
    [Fact]
    public void Build_maps_code_confidence_bbox_and_sam_mask()
    {
        var ev = new CodingEvent
        {
            Entry = new ProtocolEntry { Code = "BAB" },
            AiContext = new CodingEventAiContext
            {
                Confidence = 0.82,
                SamMaskRle = "mask",
                SamMaskImageWidth = 640,
                SamMaskImageHeight = 480
            },
            Overlay = new OverlayGeometry
            {
                Points =
                {
                    new NormalizedPoint(0.2, 0.3),
                    new NormalizedPoint(0.6, 0.9),
                    new NormalizedPoint(0.4, 0.4)
                }
            }
        };

        var annotation = CodingEvidenceAnnotationBuilder.Build(ev);

        Assert.Equal("BAB", annotation.Code);
        Assert.Equal(0.82, annotation.Confidence);
        Assert.Equal(0.4, annotation.BboxXCenter!.Value, precision: 3);
        Assert.Equal(0.6, annotation.BboxYCenter!.Value, precision: 3);
        Assert.Equal(0.4, annotation.BboxWidth!.Value, precision: 3);
        Assert.Equal(0.6, annotation.BboxHeight!.Value, precision: 3);
        Assert.Equal("mask", annotation.MaskRle);
        Assert.Equal(640, annotation.MaskImageWidth);
        Assert.Equal(480, annotation.MaskImageHeight);
    }

    [Fact]
    public void ExtractBbox_returns_empty_values_for_missing_or_degenerate_overlay()
    {
        var empty = CodingEvidenceAnnotationBuilder.ExtractBbox(null);
        var degenerate = CodingEvidenceAnnotationBuilder.ExtractBbox(new OverlayGeometry
        {
            Points =
            {
                new NormalizedPoint(0.5, 0.5),
                new NormalizedPoint(0.5, 0.5)
            }
        });

        Assert.Null(empty.XCenter);
        Assert.Null(empty.YCenter);
        Assert.Null(empty.Width);
        Assert.Null(empty.Height);
        Assert.Null(degenerate.XCenter);
        Assert.Null(degenerate.YCenter);
        Assert.Null(degenerate.Width);
        Assert.Null(degenerate.Height);
    }
}
