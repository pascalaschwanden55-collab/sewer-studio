using System;
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
/// End-to-End-Tests fuer Fix #1:
/// DINO=0 + Klassifikator bestaetigt Grundgeruest-Code ueber Voting → box-loser Befund.
/// </summary>
[Collection(VsaCodeResolverTestCollection.Name)]
public sealed class MultiModelAnalysisServiceE2ETests
{
    public MultiModelAnalysisServiceE2ETests()
    {
        VsaResolverTestCatalog.ConfigureDefault();
    }

    // ── Hilfsmethoden ────────────────────────────────────────────────────────

    private static PipelineConfig MinimalConfig() => new(
        MultiModelEnabled: true,
        SidecarUrl: new Uri("http://localhost:5001"),
        SidecarToken: null,
        Mode: PipelineMode.MultiModel,
        YoloConfidence: 0.25,
        YoloClassConfidence: new Dictionary<string, double>(),
        DinoBoxThreshold: 0.25,
        DinoTextThreshold: 0.20,
        SidecarTimeoutSec: 30,
        PipeDiameterMmOverride: 300);

    /// <summary>
    /// Stub: liefert 10 Frames mit Timestamps 0..9 Sekunden.
    /// Jeder Frame enthaelt ein minimales 1x1 PNG (damit PngBytes != null).
    /// </summary>
    private static async IAsyncEnumerable<FrameData> TenFrameSource(
        string ffmpegPath, string videoPath, double step, double duration,
        [EnumeratorCancellation] CancellationToken ct)
    {
        // Minimales gueltiges 1x1 weisses PNG (67 Bytes)
        byte[] minPng =
        [
            0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A, // PNG-Signatur
            0x00, 0x00, 0x00, 0x0D, 0x49, 0x48, 0x44, 0x52, // IHDR-Laenge + Typ
            0x00, 0x00, 0x00, 0x01, 0x00, 0x00, 0x00, 0x01, // 1x1
            0x08, 0x02, 0x00, 0x00, 0x00, 0x90, 0x77, 0x53, // Bit depth/color/CRC
            0xDE, 0x00, 0x00, 0x00, 0x0C, 0x49, 0x44, 0x41, // IDAT-Laenge + Typ
            0x54, 0x08, 0xD7, 0x63, 0xF8, 0xCF, 0xC0, 0x00, // IDAT-Daten
            0x00, 0x00, 0x02, 0x00, 0x01, 0xE2, 0x21, 0xBC, // CRC
            0x33, 0x00, 0x00, 0x00, 0x00, 0x49, 0x45, 0x4E, // IEND-Laenge + Typ
            0x44, 0xAE, 0x42, 0x60, 0x82                    // IEND-CRC
        ];

        for (int i = 0; i < 10; i++)
        {
            ct.ThrowIfCancellationRequested();
            yield return new FrameData(i * 1.0, minPng);
            await Task.Yield();
        }
    }

    /// <summary>
    /// Stub-Client: cls=BCD hoch+usable, YOLO=IsRelevant/keine Boxen, DINO=keine Boxen, SAM=leer.
    /// </summary>
    private sealed class BcdOnlyClassifierStub : IVisionPipelineClient
    {
        public Task<SidecarHealthResponse?> HealthCheckAsync(CancellationToken ct = default)
            => Task.FromResult<SidecarHealthResponse?>(null);

        public Task<PipelineHealthCheckResult> CheckHealthDetailedAsync(CancellationToken ct = default)
            => Task.FromResult(new PipelineHealthCheckResult(
                IsReachable: true,
                IsAuthorized: true,
                StatusCode: 200,
                Health: null,
                Error: null));

        public Task<YoloResponse> DetectYoloAsync(YoloRequest request, CancellationToken ct = default)
            => Task.FromResult(new YoloResponse(
                IsRelevant: true,
                Detections: Array.Empty<YoloDetectionDto>(),
                FrameClass: "structural",
                InferenceTimeMs: 1));

        public Task<DinoResponse> DetectDinoAsync(DinoRequest request, CancellationToken ct = default)
            => Task.FromResult(new DinoResponse(
                Detections: Array.Empty<DinoDetectionDto>(),
                InferenceTimeMs: 1));

        public Task<SamResponse> SegmentSamAsync(SamRequest request, CancellationToken ct = default)
            => Task.FromResult(new SamResponse(
                Masks: Array.Empty<SamMaskResult>(),
                ImageWidth: 640,
                ImageHeight: 480,
                InferenceTimeMs: 1));

        public Task<YoloClassifyResponse> ClassifyYoloAsync(YoloClassifyRequest request, CancellationToken ct = default)
            => Task.FromResult(new YoloClassifyResponse(
                Predictions: new[]
                {
                    new YoloClassifyPrediction(ClassName: "BCD", Confidence: 0.95)
                },
                InferenceTimeMs: 1,
                Usable: true,
                QualityReason: "ok"));
    }

    /// <summary>Sidecar tot: YOLO wirft bei jedem Frame (befund-2: Totalausfall mitten im Video).</summary>
    private sealed class DeadSidecarYoloClient : IVisionPipelineClient
    {
        public Task<SidecarHealthResponse?> HealthCheckAsync(CancellationToken ct = default)
            => Task.FromResult<SidecarHealthResponse?>(null);

        public Task<PipelineHealthCheckResult> CheckHealthDetailedAsync(CancellationToken ct = default)
            => Task.FromResult(new PipelineHealthCheckResult(true, true, 200, null, null));

        public Task<YoloResponse> DetectYoloAsync(YoloRequest request, CancellationToken ct = default)
            => throw new InvalidOperationException("Sidecar nicht erreichbar (Test).");

        public Task<DinoResponse> DetectDinoAsync(DinoRequest request, CancellationToken ct = default)
            => Task.FromResult(new DinoResponse(Array.Empty<DinoDetectionDto>(), 1));

        public Task<SamResponse> SegmentSamAsync(SamRequest request, CancellationToken ct = default)
            => Task.FromResult(new SamResponse(Array.Empty<SamMaskResult>(), 640, 480, 1));

        public Task<YoloClassifyResponse> ClassifyYoloAsync(YoloClassifyRequest request, CancellationToken ct = default)
            => Task.FromResult(new YoloClassifyResponse(
                Array.Empty<YoloClassifyPrediction>(), 1, Usable: true, QualityReason: "ok"));

    }

    private sealed class ThrowingRecordingPipelineTraceWriter : IPipelineTraceWriter
    {
        private int _writeCalls;
        private int _summaryCalls;
        private int _resolvePathCalls;

        public int WriteCalls => Volatile.Read(ref _writeCalls);
        public int SummaryCalls => Volatile.Read(ref _summaryCalls);
        public int ResolvePathCalls => Volatile.Read(ref _resolvePathCalls);

        public Task WriteAsync(PipelineTraceEntry entry)
        {
            Interlocked.Increment(ref _writeCalls);
            throw new InvalidOperationException("Testfehler beim Trace-Schreiben");
        }

        public Task WriteSummaryAsync(string runId, TelemetrySummary summary)
        {
            Interlocked.Increment(ref _summaryCalls);
            throw new InvalidOperationException("Testfehler beim Summary-Schreiben");
        }

        public string? ResolvePath(string runId)
        {
            Interlocked.Increment(ref _resolvePathCalls);
            throw new InvalidOperationException("Testfehler beim Pfad-Aufloesen");
        }

        public string? ResolveSummaryPath(string runId) => null;
    }

    // ── Tests ────────────────────────────────────────────────────────────────

    [Fact]
    public async Task Sidecar_dauerhaft_tot_bricht_ab_und_markiert_degraded()
    {
        var svc = new MultiModelAnalysisService(
            client: new DeadSidecarYoloClient(),
            config: MinimalConfig(),
            ffmpegPath: "ffmpeg",
            frameSource: TenFrameSource,
            durationProbe: (_, _) => Task.FromResult(10.0));
        svc.FrameStepSeconds = 1.0;

        var result = await svc.AnalyzeAsync("dummy/video.mp4");

        // Kein harter Fehler (Ergebnis ist nutzbar), aber ehrlich als degraded gekennzeichnet und
        // nach genug Folgefehlern abgebrochen — statt still alle 10 Frames als "Erfolg" durchzureichen.
        Assert.True(result.IsSuccess, $"Unerwarteter Fehler: {result.Error}");
        Assert.True(result.Degraded, "Lauf mit totem Sidecar muss degraded sein.");
        Assert.Contains("Sidecar", result.DegradedReason ?? "");
        Assert.True(result.FramesAnalyzed <= 8,
            $"Erwartet Abbruch nach 8 Folgefehlern, tatsaechlich {result.FramesAnalyzed} Frames.");
    }

    [Fact]
    public async Task DinoEmpty_WithConfirmedBcd_ProducesStructuralFinding()
    {
        // ARRANGE
        var cfg = MinimalConfig();
        var stub = new BcdOnlyClassifierStub();

        var svc = new MultiModelAnalysisService(
            client: stub,
            config: cfg,
            ffmpegPath: "ffmpeg",
            frameSource: TenFrameSource,
            durationProbe: (_, _) => Task.FromResult(10.0));

        // EstimatedReachLengthM=3m → Meter bleiben klein (0..3m), Voting bestaetig BCD
        // nach Frame 2 (beide < 1.5m Abstand).
        svc.EstimatedReachLengthM = 3.0;
        svc.FrameStepSeconds = 1.0;
        svc.ClassifierOnlyStructuralEnabled = true;
        svc.UseClsPrefilter = true;

        // ACT
        var result = await svc.AnalyzeAsync("dummy/video.mp4");

        // ASSERT
        Assert.True(result.IsSuccess, $"Pipeline fehlgeschlagen: {result.Error}");
        var bcdDetections = result.Detections
            .Where(d => string.Equals(d.VsaCodeHint, "BCD", StringComparison.OrdinalIgnoreCase))
            .ToList();
        Assert.True(bcdDetections.Count >= 1,
            $"Erwartet mindestens 1 BCD-Befund, gefunden: {result.Detections.Count} Befunde total. " +
            $"Codes: [{string.Join(", ", result.Detections.Select(d => d.VsaCodeHint ?? d.FindingLabel))}]");
    }

    [Fact]
    public async Task DinoEmpty_FlagOff_ProducesNoStructuralFinding()
    {
        // ARRANGE
        var cfg = MinimalConfig();
        var stub = new BcdOnlyClassifierStub();

        var svc = new MultiModelAnalysisService(
            client: stub,
            config: cfg,
            ffmpegPath: "ffmpeg",
            frameSource: TenFrameSource,
            durationProbe: (_, _) => Task.FromResult(10.0));

        svc.EstimatedReachLengthM = 3.0;
        svc.FrameStepSeconds = 1.0;
        // Fix #1 deaktiviert → kein box-loser Befund erwartet
        svc.ClassifierOnlyStructuralEnabled = false;
        svc.UseClsPrefilter = true;

        // ACT
        var result = await svc.AnalyzeAsync("dummy/video.mp4");

        // ASSERT
        Assert.True(result.IsSuccess, $"Pipeline fehlgeschlagen: {result.Error}");
        var bcdDetections = result.Detections
            .Where(d => string.Equals(d.VsaCodeHint, "BCD", StringComparison.OrdinalIgnoreCase))
            .ToList();
        Assert.Empty(bcdDetections);
    }

    [Fact]
    public async Task AnalyzeAsync_verwendet_injizierten_Trace_ohne_den_Hauptablauf_abzubrechen()
    {
        var traceWriter = new ThrowingRecordingPipelineTraceWriter();
        var svc = new MultiModelAnalysisService(
            pipelineTraceWriter: traceWriter,
            client: new BcdOnlyClassifierStub(),
            config: MinimalConfig(),
            ffmpegPath: "ffmpeg",
            frameSource: TenFrameSource,
            durationProbe: (_, _) => Task.FromResult(10.0))
        {
            EstimatedReachLengthM = 3.0,
            FrameStepSeconds = 1.0,
            ClassifierOnlyStructuralEnabled = true,
            UseClsPrefilter = true
        };

        var result = await svc.AnalyzeAsync("dummy/video.mp4");

        Assert.True(result.IsSuccess, result.Error);
        Assert.True(traceWriter.WriteCalls > 0);
        Assert.Equal(1, traceWriter.SummaryCalls);
        Assert.Equal(1, traceWriter.ResolvePathCalls);
    }
}
