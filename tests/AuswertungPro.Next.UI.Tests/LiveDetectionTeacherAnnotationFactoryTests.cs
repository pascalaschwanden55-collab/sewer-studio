using AuswertungPro.Next.Application.Ai;
using AuswertungPro.Next.Domain.Models;
using AuswertungPro.Next.Domain.Protocol;
using AuswertungPro.Next.UI.Ai;

namespace AuswertungPro.Next.UI.Tests;

public sealed class LiveDetectionTeacherAnnotationFactoryTests
{
    [Fact]
    public void CreateManualMark_builds_annotation_from_selected_entry_overlay_and_export()
    {
        var selectedEntry = new ProtocolEntry { Code = "BAB", Beschreibung = "Riss" };
        var overlay = new OverlayGeometry
        {
            ToolType = OverlayToolType.Rectangle,
            Q1Mm = 12,
            Q2Mm = 34,
            Points =
            [
                new NormalizedPoint(0.1, 0.2),
                new NormalizedPoint(0.4, 0.7)
            ]
        };
        var bbox = new NormalizedBoundingBox { XCenter = 0.25, YCenter = 0.45, Width = 0.3, Height = 0.5 };
        var export = ExportResult();

        var annotation = LiveDetectionTeacherAnnotationFactory.CreateManualMark(
            "ann1",
            selectedEntry,
            overlay,
            bbox,
            clockPosition: "3.5",
            captureMeter: 7.2,
            videoTimestamp: TimeSpan.FromSeconds(9),
            export);

        Assert.Equal("ann1", annotation.AnnotationId);
        Assert.Equal("BAB", annotation.VsaCode);
        Assert.Equal("Riss", annotation.Beschreibung);
        Assert.Equal(7.2, annotation.MeterPosition);
        Assert.Equal(TimeSpan.FromSeconds(9), annotation.VideoTimestamp);
        Assert.Equal(OverlayToolType.Rectangle, annotation.ToolType);
        Assert.Equal(2, annotation.Points.Count);
        Assert.Same(bbox, annotation.BoundingBox);
        Assert.Equal(3.5, annotation.ClockPosition);
        Assert.Equal("full.png", annotation.FullFramePath);
        Assert.Equal("crop.png", annotation.CroppedRegionPath);
        Assert.Equal("label.txt", annotation.YoloAnnotationPath);
        Assert.Equal(34, annotation.WidthMm);
        Assert.Equal(12, annotation.HeightMm);
    }

    [Fact]
    public void CreateDetection_builds_annotation_from_finding_and_code()
    {
        var finding = new LiveFrameFinding(
            "Wurzel",
            4,
            "9",
            20,
            WidthMm: 80,
            HeightMm: 30);
        var bbox = new NormalizedBoundingBox { XCenter = 0.5, YCenter = 0.5, Width = 0.2, Height = 0.2 };

        var annotation = LiveDetectionTeacherAnnotationFactory.CreateDetection(
            "det1",
            finding,
            "BBA",
            bbox,
            TimeSpan.FromSeconds(11),
            ExportResult());

        Assert.Equal("det1", annotation.AnnotationId);
        Assert.Equal("BBA", annotation.VsaCode);
        Assert.Equal("Wurzel", annotation.Beschreibung);
        Assert.Equal(OverlayToolType.None, annotation.ToolType);
        Assert.Empty(annotation.Points);
        Assert.Same(bbox, annotation.BoundingBox);
        Assert.Equal(9, annotation.ClockPosition);
        Assert.Equal(80, annotation.WidthMm);
        Assert.Equal(30, annotation.HeightMm);
    }

    [Fact]
    public void CreateCorrectedDetection_uses_selected_entry_description_and_source_geometry()
    {
        var sourceFinding = new LiveFrameFinding(
            "KI-Vorschlag",
            2,
            "12",
            15,
            WidthMm: 44,
            HeightMm: 22);
        var selectedEntry = new ProtocolEntry { Code = "BCA", Beschreibung = "Anschluss" };
        var bbox = new NormalizedBoundingBox { XCenter = 0.5, YCenter = 0.2, Width = 0.1, Height = 0.1 };

        var annotation = LiveDetectionTeacherAnnotationFactory.CreateCorrectedDetection(
            "corr1",
            sourceFinding,
            selectedEntry,
            bbox,
            TimeSpan.FromSeconds(13),
            ExportResult());

        Assert.Equal("corr1", annotation.AnnotationId);
        Assert.Equal("BCA", annotation.VsaCode);
        Assert.Equal("Anschluss", annotation.Beschreibung);
        Assert.Equal(TimeSpan.FromSeconds(13), annotation.VideoTimestamp);
        Assert.Equal(12, annotation.ClockPosition);
        Assert.Equal(44, annotation.WidthMm);
        Assert.Equal(22, annotation.HeightMm);
    }

    private static TrainingAnnotationResult ExportResult()
        => new()
        {
            FullFramePath = "full.png",
            CroppedRegionPath = "crop.png",
            YoloAnnotationPath = "label.txt"
        };
}
