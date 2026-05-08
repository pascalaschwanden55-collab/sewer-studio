using System;
using AuswertungPro.Next.Application.Ai.Annotation;
using Xunit;

namespace AuswertungPro.Next.Pipeline.Tests;

[Trait("Category", "Unit")]
public sealed class OperateurAnnotationModelsTests
{
    [Fact]
    public void AnnotationRequest_AllFieldsRequired()
    {
        var req = new AnnotationRequest(
            CaseId: "haltung-100-200",
            Code: "BAB B",
            ProtocolMeterstand: 12.30,
            SuggestedFrameTimeSeconds: 145.5,
            ActualFrameTimeSeconds: 149.7,
            VideoFrameIndex: 3742,
            FramePath: @"C:\KI_BRAIN\frames\test.png",
            FrameWidth: 1920,
            FrameHeight: 1080,
            Box: new BoundingBoxNormalized(0.5, 0.5, 0.2, 0.3));

        Assert.Equal("BAB B", req.Code);
        Assert.Equal(0.5, req.Box.XCenter);
        Assert.Equal(149.7, req.ActualFrameTimeSeconds);
    }

    [Fact]
    public void MaskPreview_WithWarnings()
    {
        var preview = new MaskPreview(
            SamMaskRle: "rle-data",
            SamMaskEncoding: "sidecar-sam-rle-v1",
            PolygonJson: "[[1,2],[3,4]]",
            MaskWidth: 1920,
            MaskHeight: 1080,
            MaskAreaPixels: 5000,
            SamConfidence: 0.25,
            SamLatency: TimeSpan.FromMilliseconds(420),
            Warnings: new[] { "LowSamConfidence" });

        Assert.NotNull(preview.Warnings);
        Assert.Single(preview.Warnings!);
        Assert.Contains("LowSamConfidence", preview.Warnings!);
    }

    [Fact]
    public void CommitResult_IsSuccessEqualsStorePersisted_EvenIfYoloAndKbFail()
    {
        var result = new CommitResult(
            IsSuccess: true,
            SampleId: "abc-123",
            FramePath: "frame.png",
            LabelPath: null,
            StorePersisted: true,
            KbIndexed: false,
            YoloWritten: false,
            Error: null,
            Warnings: new[] { "KbDown", "YoloDown" });

        Assert.True(result.IsSuccess);
        Assert.True(result.StorePersisted);
        Assert.False(result.KbIndexed);
        Assert.False(result.YoloWritten);
    }

    [Fact]
    public void BoundingBoxNormalized_IsValueRecord()
    {
        var a = new BoundingBoxNormalized(0.1, 0.2, 0.3, 0.4);
        var b = new BoundingBoxNormalized(0.1, 0.2, 0.3, 0.4);
        Assert.Equal(a, b);
    }
}
