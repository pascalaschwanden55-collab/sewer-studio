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
/// Verhaltenstests fuer den kontrollierten Sidecar-Neustart (Paket 3/A2) und das
/// Per-Request-Timeout (Paket 3/C) im Multi-Model-Lauf.
/// Harness-Muster wie MultiModelAnalysisServiceResilienceTests.
/// </summary>
[Collection(VsaCodeResolverTestCollection.Name)]
public sealed class MultiModelAnalysisServiceSidecarRestartTests
{
    public MultiModelAnalysisServiceSidecarRestartTests()
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
            progress?.Report("Neustart-Testschritt");
            return Task.FromResult(_result);
        }
    }

    /// <summary>DINO wirft bei den angegebenen 1-basierten Aufrufen, sonst Box+Maske.</summary>
    private sealed class DinoFailsOnCallsClient : IVisionPipelineClient
    {
        private readonly HashSet<int> _failCalls;
        private readonly Exception _failure;

        public DinoFailsOnCallsClient(IEnumerable<int> failCalls, Exception? failure = null)
        {
            _failCalls = new HashSet<int>(failCalls);
            _failure = failure ?? new System.Net.Http.HttpRequestException("Sidecar gestorben (Test).");
        }

        public int YoloCalls { get; private set; }
        public int DinoCalls { get; private set; }
        public int SamCalls { get; private set; }

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
            if (_failCalls.Contains(DinoCalls))
                throw _failure;
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

    // ── A2: Kontrollierter Neustart am Ausfall-Limit ─────────────────────────

    [Fact]
    public async Task Erster_outage_erfolgreicher_neustart_reset_und_fortsetzung()
    {
        // DINO stirbt bei den Aufrufen 1-8 (8 Folgeframes -> Ausfall-Limit),
        // nach dem Neustart ist der Sidecar wieder gesund (Aufrufe 9+ ok).
        var client = new DinoFailsOnCallsClient(Enumerable.Range(1, 8));
        var restart = new FakeRestartService(new SidecarRestartResult(true, true, null));
        var svc = CreateService(client, frameCount: 20, restart);

        var result = await svc.AnalyzeAsync("dummy/video.mp4");

        Assert.True(result.IsSuccess, result.Error);
        Assert.Equal(1, restart.Attempts);
        Assert.Equal(20, result.FramesAnalyzed);
        Assert.Equal(20, client.DinoCalls);
        Assert.False(result.Degraded,
            "Nach erfolgreichem Neustart darf kein Outage-Degraded stehen: " + result.DegradedReason);
        Assert.DoesNotContain("Sidecar antwortete", result.DegradedReason ?? "");
        // 8 von 20 Frames fehlerbedingt uebersprungen (40 % > 10 %) -> Incomplete bleibt ehrlich.
        Assert.True(result.Incomplete, "Die verlorenen Frames muessen als Incomplete sichtbar bleiben.");
        Assert.NotEmpty(result.Detections);
    }

    [Fact]
    public async Task Erster_outage_neustart_fehlgeschlagen_degraded_abbruch()
    {
        var client = new DinoFailsOnCallsClient(Enumerable.Range(1, 20));
        var restart = new FakeRestartService(new SidecarRestartResult(true, false, "Start fehlgeschlagen"));
        var svc = CreateService(client, frameCount: 20, restart);

        var result = await svc.AnalyzeAsync("dummy/video.mp4");

        Assert.True(result.IsSuccess, result.Error);
        Assert.Equal(1, restart.Attempts);
        Assert.True(result.Degraded);
        Assert.Contains("Sidecar", result.DegradedReason ?? "");
        Assert.True(result.FramesAnalyzed <= 8,
            $"Abbruch nach 8 Folgefehlern erwartet, tatsaechlich {result.FramesAnalyzed}.");
    }

    [Fact]
    public async Task Erster_outage_neustart_abgelehnt_fremder_sidecar_degraded()
    {
        var client = new DinoFailsOnCallsClient(Enumerable.Range(1, 20));
        var restart = new FakeRestartService(
            new SidecarRestartResult(false, false, "Sidecar wurde nicht von der App gestartet."));
        var svc = CreateService(client, frameCount: 20, restart);

        var result = await svc.AnalyzeAsync("dummy/video.mp4");

        Assert.Equal(1, restart.Attempts);
        Assert.True(result.Degraded);
        Assert.True(result.FramesAnalyzed <= 8);
    }

    [Fact]
    public async Task Zweiter_outage_nach_neustart_bricht_ab_kein_zweiter_versuch()
    {
        // Aufrufe 1-8 Fehler (Neustart erfolgreich), 9-12 ok, ab 13 dauerhaft Fehler:
        // der zweite Ausfall darf KEINEN zweiten Neustart ausloesen.
        var client = new DinoFailsOnCallsClient(
            Enumerable.Range(1, 8).Concat(Enumerable.Range(13, 13)));
        var restart = new FakeRestartService(new SidecarRestartResult(true, true, null));
        var svc = CreateService(client, frameCount: 25, restart);

        var result = await svc.AnalyzeAsync("dummy/video.mp4");

        Assert.Equal(1, restart.Attempts);
        Assert.True(result.Degraded);
        // Serie ab Aufruf 13: Limit bei 8 Folgefehlern -> Abbruch bei Frame 20 (von 25).
        Assert.Equal(20, result.FramesAnalyzed);
        Assert.Equal(20, client.DinoCalls);
    }

    [Fact]
    public async Task Ohne_restart_service_unveraendertes_verhalten()
    {
        var client = new DinoFailsOnCallsClient(Enumerable.Range(1, 20));
        var svc = CreateService(client, frameCount: 20, restart: null);

        var result = await svc.AnalyzeAsync("dummy/video.mp4");

        Assert.True(result.Degraded);
        Assert.Contains("Sidecar", result.DegradedReason ?? "");
        Assert.True(result.FramesAnalyzed <= 8);
    }

    // ── C: Per-Request-Timeout zaehlt als Transportfehler ────────────────────

    [Fact]
    public async Task Per_request_timeout_zaehlt_als_transportfehler_im_outage_guard()
    {
        var timeout = new SidecarRequestTimeoutException("/detect/dino", TimeSpan.FromSeconds(120));
        var client = new DinoFailsOnCallsClient(Enumerable.Range(1, 20), failure: timeout);
        var svc = CreateService(client, frameCount: 20, restart: null);

        var result = await svc.AnalyzeAsync("dummy/video.mp4");

        Assert.True(result.Degraded);
        Assert.Contains("Sidecar", result.DegradedReason ?? "");
        Assert.True(result.FramesAnalyzed <= 8,
            $"Timeout-Serie muss wie Transportfehler zum Abbruch fuehren, tatsaechlich {result.FramesAnalyzed}.");
    }

    [Fact]
    public async Task Per_request_timeout_nach_neustart_ebenfalls_nur_ein_versuch()
    {
        var timeout = new SidecarRequestTimeoutException("/detect/dino", TimeSpan.FromSeconds(120));
        var client = new DinoFailsOnCallsClient(Enumerable.Range(1, 20), failure: timeout);
        var restart = new FakeRestartService(new SidecarRestartResult(true, true, null));
        var svc = CreateService(client, frameCount: 20, restart);

        var result = await svc.AnalyzeAsync("dummy/video.mp4");

        // Neustart "erfolgreich", aber der Sidecar haengt weiter: zweite Serie -> Abbruch.
        Assert.Equal(1, restart.Attempts);
        Assert.True(result.Degraded);
        Assert.Equal(16, result.FramesAnalyzed);
    }

    // ── Guard-Unit-Test: ResetSeries ─────────────────────────────────────────

    [Fact]
    public void OutageGuard_reset_series_setzt_serie_zurueck_und_behaelt_skip_quote()
    {
        var guard = new SidecarOutageGuard(limit: 8);

        for (var frame = 1; frame <= 8; frame++)
            guard.RegisterTransportError(frame);
        Assert.True(guard.LimitReached);
        Assert.Equal(8, guard.ErrorSkipCount);

        guard.ResetSeries();

        Assert.False(guard.LimitReached);
        Assert.Equal(0, guard.ConsecutiveErrorFrames);
        Assert.Equal(8, guard.ErrorSkipCount);

        // Neue Serie beginnt sauber bei 1, die Skip-Quote laeuft weiter.
        guard.RegisterTransportError(9);
        Assert.Equal(1, guard.ConsecutiveErrorFrames);
        Assert.Equal(9, guard.ErrorSkipCount);
    }
}
