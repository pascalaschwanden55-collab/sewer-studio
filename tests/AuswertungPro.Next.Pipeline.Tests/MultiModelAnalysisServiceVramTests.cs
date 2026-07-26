using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;
using AuswertungPro.Next.Application.Ai;
using AuswertungPro.Next.Application.Ai.Startup;
using AuswertungPro.Next.Infrastructure.Ai;
using AuswertungPro.Next.Infrastructure.Ai.Pipeline;
using Xunit;

namespace AuswertungPro.Next.Pipeline.Tests;

/// <summary>
/// Verhaltenstests fuer den VRAM-Kapazitaetsfehler (Paket 2/A4) im Multi-Model-Lauf:
/// SidecarInsufficientVramException zaehlt wie ein Modellfehler (Skip-Quote, Review),
/// NIEMALS als Transport-Ausfall — kein Outage-Abbruch nach 8 Folgeframes, kein Neustart.
/// Harness-Muster wie MultiModelAnalysisServiceSidecarRestartTests.
/// </summary>
[Collection(VsaCodeResolverTestCollection.Name)]
public sealed class MultiModelAnalysisServiceVramTests
{
    public MultiModelAnalysisServiceVramTests()
    {
        VsaResolverTestCatalog.ConfigureDefault();
    }

    // ── Harness ──────────────────────────────────────────────────────────────

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

    private static readonly byte[] MinPng =
    [
        0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A,
        0x00, 0x00, 0x00, 0x0D, 0x49, 0x48, 0x44, 0x52,
        0x00, 0x00, 0x00, 0x01, 0x00, 0x00, 0x00, 0x01,
        0x08, 0x02, 0x00, 0x00, 0x00, 0x90, 0x77, 0x53,
        0xDE, 0x00, 0x00, 0x00, 0x0C, 0x49, 0x44, 0x41,
        0x54, 0x08, 0xD7, 0x63, 0xF8, 0xCF, 0xC0, 0x00,
        0x00, 0x00, 0x02, 0x00, 0x01, 0xE2, 0x21, 0xBC,
        0x33, 0x00, 0x00, 0x00, 0x00, 0x49, 0x45, 0x4E,
        0x44, 0xAE, 0x42, 0x60, 0x82
    ];

    private static async IAsyncEnumerable<FrameData> FrameSource(int count,
        [EnumeratorCancellation] CancellationToken ct = default)
    {
        for (var i = 0; i < count; i++)
        {
            ct.ThrowIfCancellationRequested();
            yield return new FrameData(i * 1.0, MinPng);
            await Task.Yield();
        }
    }

    private static MultiModelAnalysisService CreateService(
        IVisionPipelineClient client,
        int frameCount,
        ISidecarRestartService? restart)
        => new(
            client: client,
            config: MinimalConfig(),
            ffmpegPath: "ffmpeg",
            frameSource: (_, _, _, _, ct) => FrameSource(frameCount, ct),
            durationProbe: (_, _) => Task.FromResult((double)frameCount),
            sidecarRestart: restart)
        {
            FrameStepSeconds = 1.0,
            UseClsPrefilter = false,
            ClassifierOnlyStructuralEnabled = false
        };

    /// <summary>Neustart-Fake: liefert ein festes Ergebnis und zaehlt die Versuche.</summary>
    private sealed class FakeRestartService : ISidecarRestartService
    {
        private readonly SidecarRestartResult _result;

        public FakeRestartService(SidecarRestartResult result) => _result = result;

        public int Attempts { get; private set; }

        public Task<SidecarRestartResult> TryRestartAsync(
            IProgress<string>? progress = null,
            CancellationToken ct = default)
        {
            Attempts++;
            return Task.FromResult(_result);
        }
    }

    /// <summary>
    /// YOLO/DINO werfen bei den angegebenen 1-basierten Aufrufen einen VRAM-Kapazitaetsfehler,
    /// sonst gesunde Antworten (Box + Maske, wie der Restart-Test-Harness).
    /// </summary>
    private sealed class VramFailsClient : IVisionPipelineClient
    {
        private readonly HashSet<int> _yoloFailCalls;
        private readonly HashSet<int> _dinoFailCalls;

        public VramFailsClient(IEnumerable<int>? yoloFailCalls = null, IEnumerable<int>? dinoFailCalls = null)
        {
            _yoloFailCalls = new HashSet<int>(yoloFailCalls ?? Enumerable.Empty<int>());
            _dinoFailCalls = new HashSet<int>(dinoFailCalls ?? Enumerable.Empty<int>());
        }

        public int YoloCalls { get; private set; }
        public int DinoCalls { get; private set; }
        public int SamCalls { get; private set; }

        private static SidecarInsufficientVramException VramError(string endpoint)
            => new(endpoint, freeGb: 1.5, requiredGb: 4.0, reservedGb: 6.0);

        public Task<SidecarHealthResponse?> HealthCheckAsync(CancellationToken ct = default)
            => Task.FromResult<SidecarHealthResponse?>(new SidecarHealthResponse(
                Status: "ok",
                Version: "test",
                Gpu: null,
                DetectorQualification: new SidecarDetectorQualification(true, null)));

        public Task<PipelineHealthCheckResult> CheckHealthDetailedAsync(CancellationToken ct = default)
            => Task.FromResult(new PipelineHealthCheckResult(true, true, 200, null, null));

        public Task<YoloResponse> DetectYoloAsync(YoloRequest request, CancellationToken ct = default)
        {
            YoloCalls++;
            if (_yoloFailCalls.Contains(YoloCalls))
                throw VramError("/detect/yolo");
            return Task.FromResult(new YoloResponse(
                IsRelevant: true,
                Detections: Array.Empty<YoloDetectionDto>(),
                FrameClass: "damage",
                InferenceTimeMs: 1,
                DetectorQualified: true));
        }

        public Task<DinoResponse> DetectDinoAsync(DinoRequest request, CancellationToken ct = default)
        {
            DinoCalls++;
            if (_dinoFailCalls.Contains(DinoCalls))
                throw VramError("/detect/dino");
            return Task.FromResult(new DinoResponse(
                [new DinoDetectionDto(10, 10, 30, 30, "crack", 0.8, "crack")], 1));
        }

        public Task<SamResponse> SegmentSamAsync(SamRequest request, CancellationToken ct = default)
        {
            SamCalls++;
            return Task.FromResult(new SamResponse(
                [
                    new SamMaskResult(
                        Label: "crack", Confidence: 0.8, Bbox: [10, 10, 30, 30],
                        MaskRle: string.Empty, MaskAreaPixels: 400,
                        ImageAreaPixels: 640 * 480, HeightPixels: 20, WidthPixels: 20,
                        CentroidX: 20, CentroidY: 20)
                ], 640, 480, 1));
        }

        public Task<YoloClassifyResponse> ClassifyYoloAsync(YoloClassifyRequest request, CancellationToken ct = default)
            => Task.FromResult(new YoloClassifyResponse(
                Array.Empty<YoloClassifyPrediction>(), 1, Usable: true, QualityReason: "ok"));
    }

    // ── A4: VRAM-Mangel = Modellfehler, kein Ausfall ─────────────────────────

    [Fact]
    public async Task Vram_mangel_dino_kein_outage_abbruch_kein_neustart()
    {
        // DINO meldet bei ALLEN 20 Frames VRAM-Mangel: ein Transport-Ausfall wuerde nach
        // 8 Folgeframes abbrechen (und einen Neustart ausloesen) — ein Kapazitaetsfehler nicht.
        var client = new VramFailsClient(dinoFailCalls: Enumerable.Range(1, 20));
        var restart = new FakeRestartService(new SidecarRestartResult(true, true, null));
        var svc = CreateService(client, frameCount: 20, restart);

        var result = await svc.AnalyzeAsync("dummy/video.mp4");

        Assert.True(result.IsSuccess, result.Error);
        Assert.Equal(0, restart.Attempts);                 // NIE ein Neustart bei VRAM-Mangel
        Assert.Equal(20, result.FramesAnalyzed);           // kein Outage-Abbruch nach 8 Frames
        Assert.Equal(20, client.DinoCalls);
        Assert.True(result.Degraded, "VRAM-Mangel muss als Degraded sichtbar sein.");
        Assert.Contains("VRAM", result.DegradedReason);
        Assert.Contains("1.5", result.DegradedReason);     // frei
        Assert.Contains("benoetigt", result.DegradedReason);
        Assert.DoesNotContain("Sidecar antwortete", result.DegradedReason ?? "");
        Assert.True(result.Incomplete, "20/20 fehlerbedingte Skips muessen Incomplete liefern.");
    }

    [Fact]
    public async Task Vram_mangel_nur_am_anfang_lauf_kommt_weiter()
    {
        // Kapazitaet erholt sich (anderer Prozess gibt VRAM frei): ab Aufruf 4 gesund.
        var client = new VramFailsClient(dinoFailCalls: Enumerable.Range(1, 3));
        var restart = new FakeRestartService(new SidecarRestartResult(true, true, null));
        var svc = CreateService(client, frameCount: 20, restart);

        var result = await svc.AnalyzeAsync("dummy/video.mp4");

        Assert.True(result.IsSuccess, result.Error);
        Assert.Equal(0, restart.Attempts);
        Assert.Equal(20, result.FramesAnalyzed);
        Assert.Equal(20, client.DinoCalls);
        Assert.NotEmpty(result.Detections);                // ab Frame 4 normale Befunde
        Assert.True(result.Degraded, "Der VRAM-Vorfall bleibt im Ergebnis sichtbar.");
        Assert.Contains("VRAM", result.DegradedReason);
        Assert.True(result.Incomplete, "3/20 Skips (15 %) liegen ueber der 10-%-Schwelle.");
    }

    [Fact]
    public async Task Vram_mangel_beim_yolo_ebenfalls_kein_outage_kein_neustart()
    {
        // Hinweis zum Harness: der letzte Frame liegt in der BCE-Zone (Rohrende) und
        // umgeht YOLO bewusst (Telemetrie-Bypass) — darum 19 YOLO-Aufrufe statt 20.
        var client = new VramFailsClient(yoloFailCalls: Enumerable.Range(1, 20));
        var restart = new FakeRestartService(new SidecarRestartResult(true, true, null));
        var svc = CreateService(client, frameCount: 20, restart);

        var result = await svc.AnalyzeAsync("dummy/video.mp4");

        Assert.True(result.IsSuccess, result.Error);
        Assert.Equal(0, restart.Attempts);
        Assert.Equal(20, result.FramesAnalyzed);
        Assert.Equal(19, client.YoloCalls);
        Assert.Equal(1, client.DinoCalls);                 // nur der BCE-Bypass-Frame erreicht DINO
        Assert.True(result.Degraded);
        Assert.Contains("VRAM", result.DegradedReason);
        Assert.DoesNotContain("Sidecar antwortete", result.DegradedReason ?? "");
        Assert.True(result.Incomplete);
    }
}
