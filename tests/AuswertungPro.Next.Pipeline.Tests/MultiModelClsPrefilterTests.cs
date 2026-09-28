using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;
using AuswertungPro.Next.Application.Ai;
using AuswertungPro.Next.Infrastructure.Ai;
using AuswertungPro.Next.Infrastructure.Ai.Pipeline;
using Xunit;

namespace AuswertungPro.Next.Pipeline.Tests;

/// <summary>
/// Haelt fest, wie der YOLO-cls-Vorfilter im Videolauf entscheidet: unbrauchbare, leere und
/// normale Bilder werden vor YOLO/DINO/SAM uebersprungen und so im Trace benannt; alles
/// andere geht weiter zur Detektion.
/// </summary>
[Collection(VsaCodeResolverTestCollection.Name)]
public sealed class MultiModelClsPrefilterTests
{
    private const int FrameCount = 4;

    public MultiModelClsPrefilterTests()
    {
        VsaResolverTestCatalog.ConfigureDefault();
    }

    [Fact]
    public async Task Unbrauchbares_Bild_wird_vor_der_Detektion_uebersprungen()
    {
        var run = await RunAsync(new YoloClassifyResponse(
            [new YoloClassifyPrediction("LEER", 0.99)], 1, Usable: false, QualityReason: "dark",
            ClassifierLoaded: true), classifierDecision: true);

        Assert.All(run.Traces, t =>
        {
            Assert.Equal("cls_quality_skip", t.Path);
            Assert.Equal("frame_dark", t.DropReason);
            Assert.Equal(false, t.YoloRelevant);
            Assert.Null(t.ClassifierCode);
        });
        Assert.Equal(0, run.Client.YoloCalls);
        Assert.Equal(0, run.Client.DinoCalls);
        Assert.Contains(run.Messages, m => m.Contains("unbrauchbar (dark)", StringComparison.Ordinal));
    }

    [Fact]
    public async Task Leeres_Bild_wird_nur_im_Klassifikatorregime_uebersprungen()
    {
        var response = new YoloClassifyResponse(
            [new YoloClassifyPrediction("LEER", 0.9)], 1, ModelName: "cls11", ClassifierLoaded: true,
            ModelSha256: "abcdef0123456789");

        var skip = await RunAsync(response, classifierDecision: true);
        Assert.All(skip.Traces, t =>
        {
            Assert.Equal("cls_leer_skip", t.Path);
            Assert.Equal("classifier_leer", t.DropReason);
            Assert.Equal("LEER", t.ClassifierCode);
            Assert.Equal(0.9, t.ClassifierConfidence);
            Assert.Equal("cls11@abcdef012345", t.ClassifierModel);
            Assert.Equal(false, t.YoloRelevant);
        });
        Assert.Equal(0, skip.Client.YoloCalls);
        Assert.Contains(skip.Messages, m => m.Contains("Klassifikator: LEER", StringComparison.Ordinal));

        var weiter = await RunAsync(response, classifierDecision: false);
        Assert.DoesNotContain(weiter.Traces, t => t.Path == "cls_leer_skip");
        Assert.Equal(FrameCount, weiter.Client.YoloCalls);
    }

    [Theory]
    [InlineData("LEER", 0.70)]
    [InlineData("NORMAL", 0.70)]
    [InlineData("BAB", 0.99)]
    public async Task Ohne_klare_Leer_oder_Normalaussage_geht_das_Bild_weiter(string className, double confidence)
    {
        var run = await RunAsync(new YoloClassifyResponse(
            [new YoloClassifyPrediction(className, confidence)], 1, ClassifierLoaded: true), classifierDecision: true);

        Assert.DoesNotContain(run.Traces, t => t.Path is "cls_quality_skip" or "cls_leer_skip" or "yolo_cls_skip");
        Assert.Equal(FrameCount, run.Client.YoloCalls);
    }

    [Theory]
    [InlineData("NORMAL")]
    [InlineData("other")]
    public async Task Normales_Bild_wird_in_beiden_Regimen_uebersprungen(string className)
    {
        foreach (var classifierDecision in new[] { false, true })
        {
            var run = await RunAsync(new YoloClassifyResponse(
                [new YoloClassifyPrediction(className, 0.8)], 1, ClassifierLoaded: true), classifierDecision);

            Assert.All(run.Traces, t =>
            {
                Assert.Equal("yolo_cls_skip", t.Path);
                Assert.Equal("yolo_cls_normal", t.DropReason);
                Assert.Equal(false, t.YoloRelevant);
            });
            Assert.Equal(0, run.Client.YoloCalls);
            Assert.Contains(run.Messages, m => m.Contains($"cls: {className}", StringComparison.Ordinal));
        }
    }

    [Fact]
    public async Task Nicht_geladener_Klassifikator_markiert_den_Frame_und_laeuft_weiter()
    {
        var run = await RunAsync(new YoloClassifyResponse(
            [new YoloClassifyPrediction("BAB", 0.9)], 1, ClassifierLoaded: false), classifierDecision: true);

        Assert.All(run.Traces, t =>
        {
            Assert.True(t.Degraded);
            Assert.Contains("classifier_not_loaded", t.DegradedReason);
        });
        Assert.Equal(FrameCount, run.Client.YoloCalls);
    }

    [Fact]
    public async Task Fehler_des_Vorfilters_faellt_auf_die_Detektion_zurueck()
    {
        var run = await RunAsync(response: null, classifierDecision: true);

        Assert.DoesNotContain(run.Traces, t => t.Path is "cls_quality_skip" or "cls_leer_skip" or "yolo_cls_skip");
        Assert.Equal(FrameCount, run.Client.YoloCalls);
    }

    [Fact]
    public async Task Nutzerabbruch_im_Vorfilter_wird_weitergereicht()
    {
        using var cts = new CancellationTokenSource();
        var client = new PrefilterClient(null) { CancelOnClassify = cts };
        var svc = CreateService(client, new RecordingTraceWriter(), classifierDecision: true);

        await Assert.ThrowsAnyAsync<OperationCanceledException>(
            () => svc.AnalyzeAsync("dummy/video.mp4", ct: cts.Token));
        Assert.Equal(0, client.YoloCalls);
    }

    [Theory]
    [InlineData(true, "NORMAL", 0.95, false, null)]
    [InlineData(true, "LEER", 0.95, true, null)]
    [InlineData(true, "LEER", 0.95, false, "cls_leer_skip")]
    [InlineData(false, "LEER", 0.95, false, null)]
    [InlineData(true, "leer", 0.71, false, "cls_leer_skip")]
    [InlineData(true, "LEER", 0.70, false, null)]
    [InlineData(false, "normal", 0.71, false, "yolo_cls_skip")]
    [InlineData(false, "OTHER", 0.70, false, null)]
    [InlineData(true, "BCD", 0.99, false, null)]
    public void Regel_entscheidet_nach_Qualitaet_Regime_und_Schwelle(
        bool unusable, string className, double confidence, bool classifierDecision, string? expectedPath)
    {
        var cls = new YoloClassifyResponse(
            [new YoloClassifyPrediction(className, confidence)], 1, Usable: !unusable, QualityReason: "blur");

        var skip = ClsPrefilterRule.Decide(cls, classifierDecision);

        Assert.Equal(unusable ? "cls_quality_skip" : expectedPath, skip?.TracePath);
    }

    [Fact]
    public void Regel_ohne_Vorhersage_laesst_brauchbares_Bild_weiter()
    {
        Assert.Null(ClsPrefilterRule.Decide(new YoloClassifyResponse([], 1), classifierDecisionEnabled: true));
    }

    private static async Task<RunResult> RunAsync(YoloClassifyResponse? response, bool classifierDecision)
    {
        var client = new PrefilterClient(response);
        var traces = new RecordingTraceWriter();
        var progress = new RecordingProgress();
        var svc = CreateService(client, traces, classifierDecision);

        var result = await svc.AnalyzeAsync("dummy/video.mp4", progress);

        Assert.True(result.IsSuccess, result.Error);
        Assert.Equal(FrameCount, traces.Entries.Count);
        return new RunResult(client, traces.Entries.OrderBy(e => e.FrameIndex).ToList(), progress.Messages.ToList());
    }

    private static MultiModelAnalysisService CreateService(
        PrefilterClient client, RecordingTraceWriter traces, bool classifierDecision) =>
        new(
            pipelineTraceWriter: traces,
            client: client,
            config: new PipelineConfig(
                MultiModelEnabled: true,
                SidecarUrl: new Uri("http://localhost:5001"),
                SidecarToken: null,
                Mode: PipelineMode.MultiModel,
                YoloConfidence: 0.25,
                YoloClassConfidence: new Dictionary<string, double>(),
                DinoBoxThreshold: 0.25,
                DinoTextThreshold: 0.20,
                SidecarTimeoutSec: 30,
                PipeDiameterMmOverride: 300),
            ffmpegPath: "ffmpeg",
            frameSource: Frames,
            durationProbe: (_, _) => Task.FromResult((double)FrameCount))
        {
            FrameStepSeconds = 1.0,
            EstimatedReachLengthM = 30.0,
            UseClsPrefilter = true,
            ClassifierDecisionEnabled = classifierDecision,
        };

    private static async IAsyncEnumerable<FrameData> Frames(
        string ffmpegPath, string videoPath, double step, double duration,
        [EnumeratorCancellation] CancellationToken ct)
    {
        for (var i = 0; i < FrameCount; i++)
        {
            ct.ThrowIfCancellationRequested();
            yield return new FrameData(i * 1.0, [0x89, 0x50, 0x4E, 0x47]);
            await Task.Yield();
        }
    }

    private sealed record RunResult(
        PrefilterClient Client, IReadOnlyList<PipelineTraceEntry> Traces, IReadOnlyList<string> Messages);

    private sealed class PrefilterClient(YoloClassifyResponse? response) : IVisionPipelineClient
    {
        public int YoloCalls { get; private set; }
        public int DinoCalls { get; private set; }
        public CancellationTokenSource? CancelOnClassify { get; init; }

        public Task<SidecarHealthResponse?> HealthCheckAsync(CancellationToken ct = default)
            => Task.FromResult<SidecarHealthResponse?>(new SidecarHealthResponse(
                "ok", "test", null, DetectorQualification: new SidecarDetectorQualification(true, null)));

        public Task<PipelineHealthCheckResult> CheckHealthDetailedAsync(CancellationToken ct = default)
            => Task.FromResult(new PipelineHealthCheckResult(true, true, 200, null, null));

        public Task<YoloClassifyResponse> ClassifyYoloAsync(YoloClassifyRequest request, CancellationToken ct = default)
        {
            if (CancelOnClassify is not null)
            {
                CancelOnClassify.Cancel();
                ct.ThrowIfCancellationRequested();
            }
            return response is null
                ? throw new InvalidOperationException("cls nicht verfuegbar (Test).")
                : Task.FromResult(response);
        }

        public Task<YoloResponse> DetectYoloAsync(YoloRequest request, CancellationToken ct = default)
        {
            YoloCalls++;
            return Task.FromResult(new YoloResponse(false, Array.Empty<YoloDetectionDto>(), "none", 1)
                with { DetectorQualified = true });
        }

        public Task<DinoResponse> DetectDinoAsync(DinoRequest request, CancellationToken ct = default)
        {
            DinoCalls++;
            return Task.FromResult(new DinoResponse(Array.Empty<DinoDetectionDto>(), 1));
        }

        public Task<SamResponse> SegmentSamAsync(SamRequest request, CancellationToken ct = default)
            => Task.FromResult(new SamResponse(Array.Empty<SamMaskResult>(), 640, 480, 1));
    }

    private sealed class RecordingTraceWriter : IPipelineTraceWriter
    {
        public ConcurrentBag<PipelineTraceEntry> Entries { get; } = [];

        public Task WriteAsync(PipelineTraceEntry entry)
        {
            Entries.Add(entry);
            return Task.CompletedTask;
        }

        public Task WriteSummaryAsync(string runId, TelemetrySummary summary) => Task.CompletedTask;
        public string? ResolvePath(string runId) => null;
        public string? ResolveSummaryPath(string runId) => null;
    }

    private sealed class RecordingProgress : IProgress<VideoAnalysisProgress>
    {
        public ConcurrentQueue<string> Messages { get; } = new();
        public void Report(VideoAnalysisProgress value) => Messages.Enqueue(value.Status);
    }
}
