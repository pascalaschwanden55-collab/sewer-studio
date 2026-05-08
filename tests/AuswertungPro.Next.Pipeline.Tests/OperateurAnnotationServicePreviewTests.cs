using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using AuswertungPro.Next.Application.Ai.Annotation;
using AuswertungPro.Next.Application.Ai.Pipeline;
using AuswertungPro.Next.Domain.Ai.Training;
using AuswertungPro.Next.Infrastructure.Ai.Annotation;
using Xunit;

namespace AuswertungPro.Next.Pipeline.Tests;

/// <summary>
/// Slice 1 (Operateur-Annotation) — PreviewMaskAsync ruft das Sidecar
/// mit return_polygon=true an und uebersetzt die SAM-Antwort in eine
/// MaskPreview. Persistiert NICHTS.
/// </summary>
[Trait("Category", "Integration")]
public sealed class OperateurAnnotationServicePreviewTests : IDisposable
{
    private readonly string _tempDir;

    public OperateurAnnotationServicePreviewTests()
    {
        _tempDir = Path.Combine(Path.GetTempPath(), "OperPreviewTests-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_tempDir);
    }

    public void Dispose()
    {
        try { if (Directory.Exists(_tempDir)) Directory.Delete(_tempDir, recursive: true); }
        catch { /* best-effort */ }
    }

    [Fact]
    public async Task PreviewMaskAsync_HappyPath_BuildsMaskPreviewFromFirstMask()
    {
        var framePath = WriteFakeFrame();
        SamRequest? capturedRequest = null;

        var svc = NewService(samDelegate: (req, ct) =>
        {
            capturedRequest = req;
            return Task.FromResult(new SamResponse(
                Masks: new[]
                {
                    new SamMaskResult(
                        Label: "user_box",
                        Confidence: 0.91,
                        Bbox: new[] { 192.0, 108.0, 1728.0, 972.0 },
                        MaskRle: "rle-data",
                        MaskAreaPixels: 555_555,
                        ImageAreaPixels: 1920 * 1080,
                        HeightPixels: 864,
                        WidthPixels: 1536,
                        CentroidX: 960.0,
                        CentroidY: 540.0,
                        PolygonPoints: new IReadOnlyList<double>[]
                        {
                            new[] { 200.0, 110.0 },
                            new[] { 1700.0, 110.0 },
                            new[] { 1700.0, 970.0 },
                            new[] { 200.0, 970.0 },
                        }),
                },
                ImageWidth: 1920,
                ImageHeight: 1080,
                InferenceTimeMs: 123.4));
        });

        var preview = await svc.PreviewMaskAsync(NewRequest(framePath), CancellationToken.None);

        Assert.Equal("rle-data", preview.SamMaskRle);
        Assert.Equal(1920, preview.MaskWidth);
        Assert.Equal(1080, preview.MaskHeight);
        Assert.Equal(555_555, preview.MaskAreaPixels);
        Assert.Equal(0.91, preview.SamConfidence);
        Assert.False(string.IsNullOrWhiteSpace(preview.PolygonJson));
        Assert.Contains("200", preview.PolygonJson);
        Assert.Null(preview.Warnings);

        // SAM-Request muss return_polygon=true setzen, sonst kommt das Polygon nie an.
        Assert.NotNull(capturedRequest);
        Assert.True(capturedRequest!.ReturnPolygon);
        Assert.Single(capturedRequest.BoundingBoxes);
    }

    [Fact]
    public async Task PreviewMaskAsync_BboxIsDenormalizedToPixelCoords()
    {
        var framePath = WriteFakeFrame();
        SamRequest? capturedRequest = null;

        var svc = NewService(samDelegate: (req, ct) =>
        {
            capturedRequest = req;
            return Task.FromResult(MakeSimpleResponse());
        });

        // Box in der Frame-Mitte, halbe Groesse: xCenter=0.5, yCenter=0.5, width=0.5, height=0.5
        var request = NewRequest(framePath, box: new BoundingBoxNormalized(0.5, 0.5, 0.5, 0.5),
            frameWidth: 1000, frameHeight: 800);

        await svc.PreviewMaskAsync(request, CancellationToken.None);

        var box = capturedRequest!.BoundingBoxes[0];
        // (0.5 - 0.25) * 1000 = 250 ; (0.5 + 0.25) * 1000 = 750
        Assert.Equal(250.0, box.X1, precision: 1);
        Assert.Equal(750.0, box.X2, precision: 1);
        // (0.5 - 0.25) * 800 = 200 ; (0.5 + 0.25) * 800 = 600
        Assert.Equal(200.0, box.Y1, precision: 1);
        Assert.Equal(600.0, box.Y2, precision: 1);
    }

    [Fact]
    public async Task PreviewMaskAsync_NoMasks_ThrowsInvalidOperation()
    {
        var framePath = WriteFakeFrame();
        var svc = NewService(samDelegate: (req, ct) => Task.FromResult(new SamResponse(
            Masks: Array.Empty<SamMaskResult>(),
            ImageWidth: 1920, ImageHeight: 1080, InferenceTimeMs: 12.3)));

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            svc.PreviewMaskAsync(NewRequest(framePath), CancellationToken.None));
    }

    [Fact]
    public async Task PreviewMaskAsync_NullPolygon_AddsWarningButReturnsPreview()
    {
        // Wenn das Sidecar polygon_points=null liefert (cv2-Fallback, degenerierte
        // Maske), soll der Service trotzdem ein MaskPreview liefern, damit der
        // Operateur die Maske visuell pruefen kann — aber mit Warning.
        var framePath = WriteFakeFrame();
        var svc = NewService(samDelegate: (req, ct) => Task.FromResult(new SamResponse(
            Masks: new[]
            {
                new SamMaskResult(
                    Label: "x", Confidence: 0.5,
                    Bbox: new[] { 0.0, 0.0, 100.0, 100.0 },
                    MaskRle: "rle", MaskAreaPixels: 1000, ImageAreaPixels: 10000,
                    HeightPixels: 100, WidthPixels: 100,
                    CentroidX: 50, CentroidY: 50,
                    PolygonPoints: null),
            },
            ImageWidth: 1920, ImageHeight: 1080, InferenceTimeMs: 12.3)));

        var preview = await svc.PreviewMaskAsync(NewRequest(framePath), CancellationToken.None);

        Assert.NotNull(preview.Warnings);
        Assert.Contains("PolygonMissing", preview.Warnings!);
    }

    [Fact]
    public async Task PreviewMaskAsync_RequestsReturnPolygonTrue()
    {
        var framePath = WriteFakeFrame();
        SamRequest? captured = null;

        var svc = NewService(samDelegate: (req, ct) =>
        {
            captured = req;
            return Task.FromResult(MakeSimpleResponse());
        });

        await svc.PreviewMaskAsync(NewRequest(framePath), CancellationToken.None);

        Assert.NotNull(captured);
        Assert.True(captured!.ReturnPolygon);
    }

    private OperateurAnnotationService NewService(
        Func<SamRequest, CancellationToken, Task<SamResponse>> samDelegate)
    {
        return new OperateurAnnotationService(
            samDelegate: samDelegate,
            writer: new NoopWriter(),
            indexer: new NoopIndexer(),
            yolo: new NoopYolo(),
            clock: () => DateTime.UtcNow);
    }

    private string WriteFakeFrame()
    {
        var path = Path.Combine(_tempDir, "frame-" + Guid.NewGuid().ToString("N") + ".png");
        File.WriteAllBytes(path, new byte[] { 0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A });
        return path;
    }

    private static AnnotationRequest NewRequest(
        string framePath,
        BoundingBoxNormalized? box = null,
        int frameWidth = 1920,
        int frameHeight = 1080)
        => new(
            CaseId: "case-1",
            Code: "BAB B",
            ProtocolMeterstand: 12.3,
            SuggestedFrameTimeSeconds: 100.0,
            ActualFrameTimeSeconds: 100.0,
            VideoFrameIndex: 1000,
            FramePath: framePath,
            FrameWidth: frameWidth,
            FrameHeight: frameHeight,
            Box: box ?? new BoundingBoxNormalized(0.5, 0.5, 0.4, 0.3));

    private static SamResponse MakeSimpleResponse() => new(
        Masks: new[]
        {
            new SamMaskResult(
                Label: "x", Confidence: 0.7,
                Bbox: new[] { 10.0, 10.0, 90.0, 90.0 },
                MaskRle: "rle", MaskAreaPixels: 5000, ImageAreaPixels: 10000,
                HeightPixels: 80, WidthPixels: 80,
                CentroidX: 50, CentroidY: 50,
                PolygonPoints: new IReadOnlyList<double>[]
                {
                    new[] { 10.0, 10.0 },
                    new[] { 90.0, 10.0 },
                    new[] { 90.0, 90.0 },
                }),
        },
        ImageWidth: 1920, ImageHeight: 1080, InferenceTimeMs: 42.0);

    private sealed class NoopWriter : ITrainingSamplesWriter
    {
        public Task AppendAsync(TrainingSample s, CancellationToken ct) => Task.CompletedTask;
        public Task UpdateIndexStateAsync(string sampleId, KbIndexState state, CancellationToken ct) => Task.CompletedTask;
        public Task<IReadOnlyList<TrainingSample>> FindCommittedAsync(
            string caseId, string sourceType, string code, double meter, double meterTolerance, CancellationToken ct)
            => Task.FromResult<IReadOnlyList<TrainingSample>>(Array.Empty<TrainingSample>());
    }

    private sealed class NoopIndexer : IKnowledgeBaseIndexer
    {
        public Task IndexSampleAsync(TrainingSample s, CancellationToken ct) => Task.CompletedTask;
    }

    private sealed class NoopYolo : IYoloDatasetWriter
    {
        public Task<string> AppendSampleAsync(TrainingSample s, MaskPreview p, CancellationToken ct)
            => Task.FromResult("");
    }
}
