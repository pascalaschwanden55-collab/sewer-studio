using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;
using AuswertungPro.Next.Application.Ai;
using AuswertungPro.Next.Application.Diagnostics;
using AuswertungPro.Next.Infrastructure.Ai;
using AuswertungPro.Next.Infrastructure.Ai.Pipeline;
using Microsoft.Extensions.Logging;
using Xunit;

namespace AuswertungPro.Next.Pipeline.Tests;

/// <summary>
/// Verhaltenstests fuer den einheitlichen Ausfallschutz (A) und das
/// Checkpoint-Journal mit Resume (B) des Multi-Model-Laufs.
/// Kernzusage: Ein abgebrochener und fortgesetzter Lauf liefert dieselben
/// Detections wie ein ununterbrochener Lauf.
/// </summary>
[Collection(VsaCodeResolverTestCollection.Name)]
public sealed class MultiModelAnalysisServiceResilienceTests
{
    public MultiModelAnalysisServiceResilienceTests()
    {
        VsaResolverTestCatalog.ConfigureDefault();
    }

    // ── Harness (Muster wie MultiModelAnalysisServiceE2ETests) ───────────────

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

    /// <summary>PNG mit angehaengtem Index-Byte: der Client-Fake kann so framebezogen antworten.</summary>
    private static byte[] MarkedPng(int frame)
    {
        var bytes = new byte[MinPng.Length + 1];
        MinPng.CopyTo(bytes, 0);
        bytes[^1] = (byte)frame;
        return bytes;
    }

    private static async IAsyncEnumerable<FrameData> MarkedFrameSource(int count,
        [EnumeratorCancellation] CancellationToken ct = default)
    {
        for (var i = 0; i < count; i++)
        {
            ct.ThrowIfCancellationRequested();
            yield return new FrameData(i * 1.0, MarkedPng(i));
            await Task.Yield();
        }
    }

    private static MultiModelAnalysisService CreateService(
        IVisionPipelineClient client,
        int frameCount,
        IAnalysisCheckpointJournal? journal = null)
        => new(
            client: client,
            config: MinimalConfig(),
            ffmpegPath: "ffmpeg",
            frameSource: (_, _, _, _, ct) => FrameSource(frameCount, ct),
            durationProbe: (_, _) => Task.FromResult((double)frameCount),
            checkpointJournal: journal)
        {
            FrameStepSeconds = 1.0,
            UseClsPrefilter = false,
            ClassifierOnlyStructuralEnabled = false
        };

    private static MultiModelAnalysisService CreateService(
        IVisionPipelineClient client,
        int frameCount,
        IAnalysisCheckpointJournal? journal,
        double frameStepSeconds)
    {
        var svc = CreateService(client, frameCount, journal);
        svc.FrameStepSeconds = frameStepSeconds;
        return svc;
    }

    private static MultiModelAnalysisService CreateMarkedService(
        IVisionPipelineClient client,
        int frameCount,
        IAnalysisCheckpointJournal? journal = null)
        => new(
            client: client,
            config: MinimalConfig(),
            ffmpegPath: "ffmpeg",
            frameSource: (_, _, _, _, ct) => MarkedFrameSource(frameCount, ct),
            durationProbe: (_, _) => Task.FromResult((double)frameCount),
            checkpointJournal: journal)
        {
            FrameStepSeconds = 1.0,
            UseClsPrefilter = false,
            ClassifierOnlyStructuralEnabled = false
        };

    /// <summary>Temp-Ordner-Ablage fuer das Journal (ITelemetryPathResolver-Fake).</summary>
    private sealed class TempTelemetryPaths : ITelemetryPathResolver
    {
        public TempTelemetryPaths()
            => Dir = Path.Combine(Path.GetTempPath(), "sewerstudio_ckpt_" + Guid.NewGuid().ToString("N"));

        public string Dir { get; }

        public string? ResolveFile(string fileName) => Path.Combine(Dir, fileName);

        public string SingleJournalPath()
            => Assert.Single(Directory.GetFiles(Dir, "analysis_checkpoint_*.jsonl"));

        public void Cleanup()
        {
            try { Directory.Delete(Dir, recursive: true); } catch { /* Best effort. */ }
        }
    }

    private sealed class ProgressRecorder : IProgress<VideoAnalysisProgress>
    {
        public List<VideoAnalysisProgress> Entries { get; } = new();
        public void Report(VideoAnalysisProgress value) => Entries.Add(value);
    }

    /// <summary>Logger-Fake: faengt Level und Meldung fuer die sichtbare Warnung.</summary>
    private sealed class ListLogger : ILogger
    {
        public List<(LogLevel Level, string Message)> Entries { get; } = new();

        IDisposable ILogger.BeginScope<TState>(TState state) => NullScope.Instance;
        public bool IsEnabled(LogLevel logLevel) => true;

        public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception,
            Func<TState, Exception?, string> formatter)
            => Entries.Add((logLevel, formatter(state, exception)));

        private sealed class NullScope : IDisposable
        {
            public static readonly NullScope Instance = new();
            public void Dispose() { }
        }
    }

    private abstract class ClientBase : IVisionPipelineClient
    {
        private readonly bool _detectorQualified;

        protected ClientBase(bool detectorQualified) => _detectorQualified = detectorQualified;

        public int YoloCalls { get; protected set; }
        public int DinoCalls { get; protected set; }
        public int SamCalls { get; protected set; }

        public virtual Task<SidecarHealthResponse?> HealthCheckAsync(CancellationToken ct = default)
            => Task.FromResult<SidecarHealthResponse?>(new SidecarHealthResponse(
                Status: "ok",
                Version: "test",
                Gpu: null,
                DetectorQualification: new SidecarDetectorQualification(
                    _detectorQualified, _detectorQualified ? null : "Altmodell: BBox-Kollaps.")));

        public Task<PipelineHealthCheckResult> CheckHealthDetailedAsync(CancellationToken ct = default)
            => Task.FromResult(new PipelineHealthCheckResult(true, true, 200, null, null));

        public virtual Task<YoloResponse> DetectYoloAsync(YoloRequest request, CancellationToken ct = default)
        {
            YoloCalls++;
            return Task.FromResult(new YoloResponse(
                IsRelevant: true,
                Detections: Array.Empty<YoloDetectionDto>(),
                FrameClass: "damage",
                InferenceTimeMs: 1,
                DetectorQualified: _detectorQualified));
        }

        public abstract Task<DinoResponse> DetectDinoAsync(DinoRequest request, CancellationToken ct = default);

        public virtual Task<SamResponse> SegmentSamAsync(SamRequest request, CancellationToken ct = default)
        {
            SamCalls++;
            return Task.FromResult(new SamResponse(
                [Mask("crack")], 640, 480, 1));
        }

        public Task<YoloClassifyResponse> ClassifyYoloAsync(YoloClassifyRequest request, CancellationToken ct = default)
            => Task.FromResult(new YoloClassifyResponse(
                Array.Empty<YoloClassifyPrediction>(), 1, Usable: true, QualityReason: "ok"));

        protected static DinoResponse Box(string label = "crack")
            => new([new DinoDetectionDto(10, 10, 30, 30, label, 0.8, label)], 1);

        protected static SamMaskResult Mask(string label)
            => new(
                Label: label,
                Confidence: 0.8,
                Bbox: [10, 10, 30, 30],
                MaskRle: string.Empty,
                MaskAreaPixels: 400,
                ImageAreaPixels: 640 * 480,
                HeightPixels: 20,
                WidthPixels: 20,
                CentroidX: 20,
                CentroidY: 20);
    }

    /// <summary>Basis fuer Fakes, die pro Frame (nicht pro Aufruf) antworten muessen.</summary>
    private abstract class FrameAwareClientBase : ClientBase
    {
        protected FrameAwareClientBase() : base(detectorQualified: true) { }

        protected static int FrameIndexOf(string imageBase64) => Convert.FromBase64String(imageBase64)[^1];
    }

    /// <summary>Detektor unqualifiziert (Bypass): DINO wirft bei jedem Frame.</summary>
    private sealed class BypassDinoThrowingClient : ClientBase
    {
        public BypassDinoThrowingClient() : base(detectorQualified: false) { }

        public override Task<DinoResponse> DetectDinoAsync(DinoRequest request, CancellationToken ct = default)
        {
            DinoCalls++;
            throw new System.Net.Http.HttpRequestException("Sidecar nicht erreichbar (Test).");
        }
    }

    /// <summary>Detektor unqualifiziert (Bypass): DINO liefert Box, SAM wirft bei jedem Frame.</summary>
    private sealed class BypassSamThrowingClient : ClientBase
    {
        public BypassSamThrowingClient() : base(detectorQualified: false) { }

        public override Task<DinoResponse> DetectDinoAsync(DinoRequest request, CancellationToken ct = default)
        {
            DinoCalls++;
            return Task.FromResult(Box());
        }

        public override Task<SamResponse> SegmentSamAsync(SamRequest request, CancellationToken ct = default)
        {
            SamCalls++;
            throw new System.Net.Http.HttpRequestException("Sidecar nicht erreichbar (Test).");
        }
    }

    /// <summary>Qualifizierter Detektor: DINO wirft nur an den angegebenen 1-basierten Frames.</summary>
    private sealed class SporadicDinoFailureClient : ClientBase
    {
        private readonly HashSet<int> _failAtFrames;

        public SporadicDinoFailureClient(params int[] failAtFrames) : base(detectorQualified: true)
            => _failAtFrames = new HashSet<int>(failAtFrames);

        public override Task<DinoResponse> DetectDinoAsync(DinoRequest request, CancellationToken ct = default)
        {
            DinoCalls++;
            if (_failAtFrames.Contains(DinoCalls))
                throw new System.Net.Http.HttpRequestException("Sporadischer Ausfall (Test).");
            return Task.FromResult(Box());
        }
    }

    /// <summary>DINO liefert bis zu einem Limit Boxen, danach dauerhaft Transportfehler.</summary>
    private sealed class DinoFailsAfterClient : ClientBase
    {
        private readonly int _okCalls;
        private readonly string _label;

        public DinoFailsAfterClient(int okCalls, string label = "crack") : base(detectorQualified: true)
            => (_okCalls, _label) = (okCalls, label);

        public override Task<DinoResponse> DetectDinoAsync(DinoRequest request, CancellationToken ct = default)
        {
            DinoCalls++;
            if (DinoCalls > _okCalls)
                throw new System.Net.Http.HttpRequestException("Sidecar gestorben (Test).");
            return Task.FromResult(Box(_label));
        }

        public override Task<SamResponse> SegmentSamAsync(SamRequest request, CancellationToken ct = default)
        {
            SamCalls++;
            return Task.FromResult(new SamResponse([Mask(_label)], 640, 480, 1));
        }
    }

    /// <summary>Gesunder Sidecar: DINO/SAM liefern durchgehend einen Befund.</summary>
    private sealed class HealthyClient : ClientBase
    {
        private readonly string _label;

        public HealthyClient(string label = "deposit") : base(detectorQualified: true) => _label = label;

        public override Task<DinoResponse> DetectDinoAsync(DinoRequest request, CancellationToken ct = default)
        {
            DinoCalls++;
            return Task.FromResult(Box(_label));
        }

        public override Task<SamResponse> SegmentSamAsync(SamRequest request, CancellationToken ct = default)
        {
            SamCalls++;
            return Task.FromResult(new SamResponse([Mask(_label)], 640, 480, 1));
        }
    }

    /// <summary>
    /// Framebezogener DINO-Fake: Boxen nur auf den gewaehlten 0-basierten Frames,
    /// ab failFromFrame dauerhaft Transportfehler. Resume-sicher, weil die Antwort
    /// am Frame haengt und nicht an der Aufrufzahl.
    /// </summary>
    private sealed class SelectiveDinoClient : FrameAwareClientBase
    {
        private readonly HashSet<int> _boxFrames;
        private readonly int _failFromFrame;

        public SelectiveDinoClient(IEnumerable<int> boxFrames, int failFromFrame = int.MaxValue)
            => (_boxFrames, _failFromFrame) = (new HashSet<int>(boxFrames), failFromFrame);

        public override Task<DinoResponse> DetectDinoAsync(DinoRequest request, CancellationToken ct = default)
        {
            DinoCalls++;
            var frame = FrameIndexOf(request.ImageBase64);
            if (frame >= _failFromFrame)
                throw new System.Net.Http.HttpRequestException("Sidecar gestorben (Test).");
            return Task.FromResult(
                _boxFrames.Contains(frame) ? Box("deposit") : new DinoResponse(Array.Empty<DinoDetectionDto>(), 1));
        }
    }

    /// <summary>DINO bricht den Lauf per CancellationToken ab und honoriert das Token.</summary>
    private sealed class CancellingDinoClient : ClientBase
    {
        private readonly CancellationTokenSource _cts;

        public CancellingDinoClient(CancellationTokenSource cts) : base(detectorQualified: true)
            => _cts = cts;

        public override async Task<DinoResponse> DetectDinoAsync(DinoRequest request, CancellationToken ct = default)
        {
            DinoCalls++;
            if (DinoCalls == 3)
                _cts.Cancel();
            await Task.Delay(200, ct);   // wirft OCE, sobald der Nutzerabbruch ausgeloest wurde
            return Box();
        }
    }

    /// <summary>
    /// Vollstaendiger feldweiser Vergleichs-Schluessel einer Detection: saemtliche
    /// Felder von RawVideoDetection (Code, Label, Meter, Meterquelle, Schaetzflag,
    /// Schweregrad, Uhrlage, Ausdehnung, Hoehe/Breite, Eindringtiefe, Querschnitts-/
    /// Durchmesserverringerung) UND saemtliche Evidence-Felder inkl. FrameCount.
    /// </summary>
    private static string Fingerprint(RawVideoDetection d)
        => string.Join("|",
            d.VsaCodeHint ?? "",
            d.FindingLabel,
            d.MeterStart.ToString("R"),
            d.MeterEnd.ToString("R"),
            d.MeterSource ?? "",
            d.IsMeterEstimated,
            d.Severity,
            d.PositionClock ?? "",
            d.ExtentPercent?.ToString() ?? "",
            d.HeightMm?.ToString() ?? "",
            d.WidthMm?.ToString() ?? "",
            d.IntrusionPercent?.ToString() ?? "",
            d.CrossSectionReductionPercent?.ToString() ?? "",
            d.DiameterReductionMm?.ToString() ?? "",
            d.Evidence?.YoloConf?.ToString("R") ?? "",
            d.Evidence?.DinoConf?.ToString("R") ?? "",
            d.Evidence?.SamMaskStability?.ToString("R") ?? "",
            d.Evidence?.QwenVisionConf?.ToString("R") ?? "",
            d.Evidence?.LlmCodeConf?.ToString("R") ?? "",
            d.Evidence?.KbSimilarity?.ToString("R") ?? "",
            d.Evidence?.KbCodeAgreement?.ToString() ?? "",
            d.Evidence?.PlausibilityScore?.ToString("R") ?? "",
            d.Evidence?.DamageCategory ?? "",
            d.Evidence?.FrameCount?.ToString() ?? "");

    private static AnalysisCheckpointFrame UpdateFrame(int index, double meter = 0.0, string label = "crack")
        => new(
            CheckpointFrameKind.Update,
            FrameIndex: index,
            TimeSec: index - 1.0,
            Meter: meter,
            MeterSource: "LinearEstimate",
            IsMeterEstimated: true,
            Evidence: null,
            Findings:
            [
                new EnhancedFinding(
                    Label: label, VsaCodeHint: "BAB", Severity: 2, PositionClock: null,
                    ExtentPercent: null, HeightMm: null, WidthMm: null,
                    IntrusionPercent: null, CrossSectionReductionPercent: null,
                    DiameterReductionMm: null,
                    BboxX1: 0.1, BboxY1: 0.1, BboxX2: 0.2, BboxY2: 0.2,
                    Notes: "test")
            ]);

    private static AnalysisCheckpointFrame AdvanceFrame(int index, double meter = 0.0)
        => new(
            CheckpointFrameKind.Advance,
            FrameIndex: index,
            TimeSec: index - 1.0,
            Meter: meter,
            MeterSource: null,
            IsMeterEstimated: true,
            Evidence: null,
            Findings: Array.Empty<EnhancedFinding>());

    // ── A: Einheitlicher Ausfallschutz ───────────────────────────────────────

    [Fact]
    public async Task Dino_transportfehler_im_bypass_loesen_outage_abbruch_aus()
    {
        // Reine DINO-Transportfehler (ohne YOLO-Aufruf im Detektor-Bypass) muessen
        // denselben Abbruch ausloesen wie bisher nur YOLO-Fehler.
        var client = new BypassDinoThrowingClient();
        var svc = CreateService(client, frameCount: 10);

        var result = await svc.AnalyzeAsync("dummy/video.mp4");

        Assert.True(result.IsSuccess, result.Error);
        Assert.True(result.Degraded, "Lauf mit totem Sidecar muss degraded sein.");
        Assert.Contains("Sidecar", result.DegradedReason ?? "");
        Assert.Equal(0, client.YoloCalls);
        Assert.True(result.FramesAnalyzed <= 8,
            $"Erwartet Abbruch nach 8 Folgefehlern, tatsaechlich {result.FramesAnalyzed} Frames.");
        Assert.Equal(result.FramesAnalyzed, client.DinoCalls);
    }

    [Fact]
    public async Task Sam_transportfehler_im_bypass_loesen_outage_abbruch_aus()
    {
        var client = new BypassSamThrowingClient();
        var svc = CreateService(client, frameCount: 10);

        var result = await svc.AnalyzeAsync("dummy/video.mp4");

        Assert.True(result.IsSuccess, result.Error);
        Assert.True(result.Degraded);
        Assert.Contains("Sidecar", result.DegradedReason ?? "");
        Assert.Equal(0, client.YoloCalls);
        Assert.True(result.FramesAnalyzed <= 8,
            $"Erwartet Abbruch nach 8 Folgefehlern, tatsaechlich {result.FramesAnalyzed} Frames.");
        Assert.Equal(result.FramesAnalyzed, client.SamCalls);
    }

    [Fact]
    public async Task Sporadische_fehler_unter_schwelle_kein_abbruch_aber_incomplete()
    {
        // 4 von 20 Frames mit DINO-Transportfehler (nie in Serie): kein Abbruch,
        // aber 20 % fehlerbedingte Skips > 10 % -> Incomplete, nicht Degraded.
        var client = new SporadicDinoFailureClient(5, 10, 15, 20);
        var svc = CreateService(client, frameCount: 20);

        var result = await svc.AnalyzeAsync("dummy/video.mp4");

        Assert.True(result.IsSuccess, result.Error);
        Assert.Equal(20, result.FramesAnalyzed);
        Assert.True(result.Incomplete, "Skip-Quote > 10 % muss Incomplete setzen.");
        Assert.False(result.Degraded, "Sporadische Fehler ohne Serie duerfen nicht degraded sein.");
        Assert.NotEmpty(result.Detections);
    }

    [Fact]
    public async Task Wenige_fehler_unter_skip_quote_bleiben_ohne_incomplete()
    {
        // 1 von 20 Frames (5 % <= 10 %): weder Abbruch noch Incomplete.
        var client = new SporadicDinoFailureClient(7);
        var svc = CreateService(client, frameCount: 20);

        var result = await svc.AnalyzeAsync("dummy/video.mp4");

        Assert.True(result.IsSuccess, result.Error);
        Assert.Equal(20, result.FramesAnalyzed);
        Assert.False(result.Incomplete);
        Assert.False(result.Degraded);
    }

    [Fact]
    public async Task Nutzerabbruch_wird_sofort_weitergeworfen_und_nicht_als_sidecar_ausfall_gezaehlt()
    {
        // Der Nutzerabbruch per CancellationToken darf keinen Transportfehler-Eintrag
        // (retry_required) im Journal erzeugen und wird als OCE weitergeworfen.
        var paths = new TempTelemetryPaths();
        try
        {
            var cts = new CancellationTokenSource();
            var client = new CancellingDinoClient(cts);
            var svc = CreateService(client, frameCount: 10,
                journal: new AnalysisCheckpointJournal(paths));

            await Assert.ThrowsAnyAsync<OperationCanceledException>(
                () => svc.AnalyzeAsync("dummy/video.mp4", ct: cts.Token));

            var lines = File.ReadAllLines(paths.SingleJournalPath())
                .Where(l => !string.IsNullOrWhiteSpace(l)).ToArray();
            // Nur die sauberen Frames 1-2 stehen im Journal — der abgebrochene Frame 3 nicht.
            Assert.Equal(2, lines.Count(l => l.Contains("\"kind\":\"update\"")));
            Assert.DoesNotContain(lines, l => l.Contains("retry_required"));
            Assert.DoesNotContain(lines, l => l.Contains("\"type\":\"completed\""));
        }
        finally
        {
            paths.Cleanup();
        }
    }

    // ── Guard-Unit-Tests (Serienlogik des Ausfallschutzes) ───────────────────

    [Fact]
    public void OutageGuard_zaehlt_folgeframes_und_setzt_nach_sauberem_frame_neu()
    {
        var guard = new SidecarOutageGuard(limit: 8);

        for (var frame = 1; frame <= 7; frame++)
            guard.RegisterTransportError(frame);
        Assert.False(guard.LimitReached);
        Assert.Equal(7, guard.ConsecutiveErrorFrames);

        // Frame 8 fehlerfrei -> Serie startet beim naechsten Fehler-Frame neu.
        guard.RegisterTransportError(9);
        Assert.Equal(1, guard.ConsecutiveErrorFrames);
        Assert.False(guard.LimitReached);

        for (var frame = 10; frame <= 16; frame++)
            guard.RegisterTransportError(frame);
        Assert.Equal(8, guard.ConsecutiveErrorFrames);
        Assert.True(guard.LimitReached);
        Assert.Equal(15, guard.ErrorSkipCount);
    }

    [Fact]
    public void OutageGuard_modellfehler_zaehlt_nur_skip_quote_nicht_die_serie()
    {
        var guard = new SidecarOutageGuard(limit: 8);

        guard.RegisterFailureSkip();
        guard.RegisterFailureSkip();

        Assert.Equal(0, guard.ConsecutiveErrorFrames);
        Assert.Equal(2, guard.ErrorSkipCount);
        Assert.False(guard.LimitReached);
    }

    [Fact]
    public void QwenTracker_meldet_einmalig_und_reset_bei_erfolg()
    {
        var tracker = new QwenOutageTracker(limit: 3);

        Assert.False(tracker.RegisterFailure());
        tracker.RegisterSuccess();   // Erfolg setzt die Serie zurueck
        Assert.False(tracker.RegisterFailure());
        Assert.False(tracker.RegisterFailure());
        Assert.True(tracker.RegisterFailure(), "Dritter Folgefehler muss die Notiz ausloesen.");
        Assert.True(tracker.Noted);
        Assert.False(tracker.RegisterFailure(), "Die Notiz faellt nur einmalig an.");
    }

    [Fact]
    public void Qwen_tracker_warnzahl_bleibt_nach_spaeterem_erfolg_erhalten()
    {
        // Acht Folgefehler -> Notiz mit Warnzahl acht. Ein spaeterer Erfolg setzt die
        // laufende Serie zurueck, darf die gemerkte Warnzahl aber NICHT auf 0 drehen.
        var tracker = new QwenOutageTracker(limit: 8);

        for (var i = 0; i < 7; i++)
            Assert.False(tracker.RegisterFailure());
        Assert.True(tracker.RegisterFailure(), "Achter Folgefehler muss die Notiz ausloesen.");
        Assert.True(tracker.Noted);
        Assert.Equal(8, tracker.NotedErrorCount);

        tracker.RegisterSuccess();

        Assert.Equal(0, tracker.ConsecutiveErrors);
        Assert.True(tracker.Noted);
        Assert.Equal(8, tracker.NotedErrorCount);
    }

    // ── B: Checkpoint-Journal mit Resume ─────────────────────────────────────

    [Fact]
    public async Task Ununterbrochener_und_resume_lauf_liefern_identische_detections()
    {
        // Kernzusage Paket 1: Abbruch bei Frame 5 (8 Folgefehler) + Resume mit
        // gesundem Sidecar == ununterbrochener Lauf — feldweise, inkl. Evidence.
        var paths = new TempTelemetryPaths();
        try
        {
            var reference = await CreateService(new HealthyClient("deposit"), frameCount: 12)
                .AnalyzeAsync("dummy/video.mp4");
            Assert.True(reference.IsSuccess, reference.Error);
            Assert.False(reference.Degraded);

            var aborted = await CreateService(
                    new DinoFailsAfterClient(okCalls: 4, label: "deposit"), frameCount: 12,
                    journal: new AnalysisCheckpointJournal(paths))
                .AnalyzeAsync("dummy/video.mp4");
            Assert.True(aborted.Degraded, "Abbruch-Lauf muss degraded sein.");

            var resumed = await CreateService(new HealthyClient("deposit"), frameCount: 12,
                    journal: new AnalysisCheckpointJournal(paths))
                .AnalyzeAsync("dummy/video.mp4");

            Assert.True(resumed.IsSuccess, resumed.Error);
            Assert.Equal(
                reference.Detections.Select(Fingerprint),
                resumed.Detections.Select(Fingerprint));
        }
        finally
        {
            paths.Cleanup();
        }
    }

    [Fact]
    public async Task Uebersprungene_frames_zwischen_befundframes_bleiben_resume_identisch()
    {
        // advance-Sprung: Befunde nur auf den 0-basierten Frames 1 und 4, dazwischen
        // normal uebersprungene Frames. Ab 0-basiertem Frame 6 Transportfehler (Abbruch),
        // danach Resume. Das Endergebnis muss dem ununterbrochenen Lauf entsprechen.
        var paths = new TempTelemetryPaths();
        try
        {
            var reference = await CreateMarkedService(
                    new SelectiveDinoClient(new[] { 1, 4 }), frameCount: 14)
                .AnalyzeAsync("dummy/video.mp4");
            Assert.True(reference.IsSuccess, reference.Error);

            var aborted = await CreateMarkedService(
                    new SelectiveDinoClient(new[] { 1, 4 }, failFromFrame: 6), frameCount: 14,
                    journal: new AnalysisCheckpointJournal(paths))
                .AnalyzeAsync("dummy/video.mp4");
            Assert.True(aborted.Degraded, "Abbruch-Lauf muss degraded sein.");

            // Journal: update/advance-Prefix bis Frame 6 (1-basiert), danach retry-Schweif.
            var lines = File.ReadAllLines(paths.SingleJournalPath())
                .Where(l => !string.IsNullOrWhiteSpace(l)).ToArray();
            Assert.Equal(6, lines.Count(l => l.Contains("\"kind\":\"update\"")
                                          || l.Contains("\"kind\":\"advance\"")));
            Assert.Equal(2, lines.Count(l => l.Contains("\"kind\":\"update\"")));
            Assert.Equal(4, lines.Count(l => l.Contains("\"kind\":\"advance\"")));
            Assert.Equal(8, lines.Count(l => l.Contains("\"kind\":\"retry_required\"")));

            var resumed = await CreateMarkedService(
                    new SelectiveDinoClient(new[] { 1, 4 }), frameCount: 14,
                    journal: new AnalysisCheckpointJournal(paths))
                .AnalyzeAsync("dummy/video.mp4");

            Assert.True(resumed.IsSuccess, resumed.Error);
            Assert.Equal(
                reference.Detections.Select(Fingerprint),
                resumed.Detections.Select(Fingerprint));
        }
        finally
        {
            paths.Cleanup();
        }
    }

    [Fact]
    public async Task Abbruch_journalisiert_frames_und_folgelauf_setzt_ohne_neuinferenz_fort()
    {
        var paths = new TempTelemetryPaths();
        try
        {
            // Lauf 1: 12 Frames, DINO stirbt nach Frame 4 -> 8 Folgefehler -> Abbruch bei Frame 12.
            var client1 = new DinoFailsAfterClient(okCalls: 4);
            var svc1 = CreateService(client1, frameCount: 12,
                journal: new AnalysisCheckpointJournal(paths));

            var result1 = await svc1.AnalyzeAsync("dummy/video.mp4");

            Assert.True(result1.Degraded, "Abbruch-Lauf muss degraded sein.");
            var journalPath = paths.SingleJournalPath();
            var lines1 = File.ReadAllLines(journalPath).Where(l => !string.IsNullOrWhiteSpace(l)).ToArray();
            Assert.Contains(lines1, l => l.Contains("\"type\":\"header\""));
            // Jeder der 12 Frames traegt genau einen Zustand: 4 update, 8 retry_required.
            Assert.Equal(12, lines1.Count(l => l.Contains("\"type\":\"frame\"")));
            Assert.Equal(4, lines1.Count(l => l.Contains("\"kind\":\"update\"")));
            Assert.Equal(8, lines1.Count(l => l.Contains("\"kind\":\"retry_required\"")));
            Assert.DoesNotContain(lines1, l => l.Contains("\"type\":\"completed\""));

            // Lauf 2: gleiches Video, gesunder Sidecar -> Resume: Frames 1-4 NICHT erneut inferiert.
            var client2 = new HealthyClient("deposit");
            var progress2 = new ProgressRecorder();
            var svc2 = CreateService(client2, frameCount: 12,
                journal: new AnalysisCheckpointJournal(paths));

            var result2 = await svc2.AnalyzeAsync("dummy/video.mp4", progress2);

            Assert.True(result2.IsSuccess, result2.Error);
            Assert.False(result2.Degraded, result2.DegradedReason);
            // Nur Frames 5-12 werden inferiert. Frame 12 (t=11s) liegt in der BCE-Zone:
            // dort bypassed die Pipeline YOLO bewusst, DINO/SAM laufen trotzdem.
            Assert.True(8 == client2.DinoCalls && 8 == client2.SamCalls && 7 == client2.YoloCalls,
                $"Resume sollte nur Frames 5-12 inferieren: Yolo={client2.YoloCalls}, Dino={client2.DinoCalls}, Sam={client2.SamCalls}");
            Assert.Contains(progress2.Entries, e => e.Status.Contains("Fortsetzung"));

            // Endergebnis enthaelt alte (crack, aus dem Journal) und neue (deposit) Findings.
            var labels = result2.Detections.Select(d => d.FindingLabel).ToList();
            Assert.Contains("crack", labels);
            Assert.Contains("deposit", labels);

            // completed-Marker als letzte Zeile.
            var lines2 = File.ReadAllLines(journalPath).Where(l => !string.IsNullOrWhiteSpace(l)).ToArray();
            Assert.Contains("\"type\":\"completed\"", lines2[^1]);

            // Lauf 3: Journal ist abgeschlossen -> kein Resume, alle Frames frisch inferiert.
            var client3 = new HealthyClient("deposit");
            var svc3 = CreateService(client3, frameCount: 12,
                journal: new AnalysisCheckpointJournal(paths));

            var result3 = await svc3.AnalyzeAsync("dummy/video.mp4");

            Assert.True(result3.IsSuccess, result3.Error);
            Assert.Equal(12, client3.DinoCalls);
        }
        finally
        {
            paths.Cleanup();
        }
    }

    [Fact]
    public async Task Identitaets_mismatch_ignoriert_journal_und_startet_frisch()
    {
        var paths = new TempTelemetryPaths();
        try
        {
            // Lauf 1: Abbruch wie oben -> offenes Journal mit step_seconds = 1.0.
            var client1 = new DinoFailsAfterClient(okCalls: 4);
            var svc1 = CreateService(client1, frameCount: 12,
                journal: new AnalysisCheckpointJournal(paths));
            await svc1.AnalyzeAsync("dummy/video.mp4");
            Assert.Equal(12, File.ReadAllLines(paths.SingleJournalPath())
                .Count(l => l.Contains("\"type\":\"frame\"")));

            // Lauf 2 mit abweichendem stepSeconds -> Identitaets-Mismatch -> Journal frisch.
            var client2 = new HealthyClient("deposit");
            var svc2 = CreateService(client2, frameCount: 12,
                journal: new AnalysisCheckpointJournal(paths), frameStepSeconds: 2.0);

            var result2 = await svc2.AnalyzeAsync("dummy/video.mp4");

            Assert.True(result2.IsSuccess, result2.Error);
            Assert.Equal(12, client2.DinoCalls);   // kein Resume: alle Frames inferiert
            Assert.DoesNotContain(result2.Detections, d => d.FindingLabel == "crack");

            // Journal wurde frisch ueberschrieben: neuer Header mit step_seconds 2.0.
            var header = File.ReadLines(paths.SingleJournalPath()).First();
            Assert.Contains("\"step_seconds\":2", header);
        }
        finally
        {
            paths.Cleanup();
        }
    }

    [Fact]
    public async Task Journal_rundreise_header_frames_completed()
    {
        // Direkter Vertragstest des Journals ohne Pipeline.
        var paths = new TempTelemetryPaths();
        try
        {
            var journal = new AnalysisCheckpointJournal(paths);
            var state0 = await journal.OpenAsync("dummy/video.mp4", 1.0);
            Assert.False(state0.HasResume);

            await journal.AppendFrameAsync(UpdateFrame(1, meter: 0.0));
            await journal.AppendFrameAsync(AdvanceFrame(2, meter: 4.17));

            // Neues Journal-Objekt auf derselben Ablage: Resume-Sicht.
            var journal2 = new AnalysisCheckpointJournal(paths);
            var state = await journal2.OpenAsync("dummy/video.mp4", 1.0);

            Assert.True(state.HasResume);
            Assert.Equal(2, state.LastFrameIndex);
            Assert.Equal(2, state.Frames.Count);
            Assert.Equal(CheckpointFrameKind.Update, state.Frames[0].Kind);
            var finding = Assert.Single(state.Frames[0].Findings);
            Assert.Equal("crack", finding.Label);
            Assert.Equal("BAB", finding.VsaCodeHint);
            Assert.Equal(CheckpointFrameKind.Advance, state.Frames[1].Kind);
            Assert.Empty(state.Frames[1].Findings);

            await journal2.CompleteAsync();

            // Abgeschlossenes Journal: naechstes Open beginnt frisch.
            var journal3 = new AnalysisCheckpointJournal(paths);
            var state3 = await journal3.OpenAsync("dummy/video.mp4", 1.0);
            Assert.False(state3.HasResume);
            var header = File.ReadLines(paths.SingleJournalPath()).First();
            Assert.Contains("\"type\":\"header\"", header);
        }
        finally
        {
            paths.Cleanup();
        }
    }

    [Fact]
    public async Task Retry_frame_beendet_prefix_und_folgelauf_inferiert_ab_dort_neu()
    {
        // update(1), update(2), retry(3): nur die Frames 1-2 sind wiederverwendbar;
        // ab Frame 3 wird neu inferiert, der retry-Schweif wird abgeschnitten.
        var paths = new TempTelemetryPaths();
        try
        {
            var journal = new AnalysisCheckpointJournal(paths);
            await journal.OpenAsync("dummy/video.mp4", 1.0);
            await journal.AppendFrameAsync(UpdateFrame(1));
            await journal.AppendFrameAsync(UpdateFrame(2));
            await journal.AppendFrameAsync(new AnalysisCheckpointFrame(
                CheckpointFrameKind.RetryRequired, 3, 2.0, 8.34,
                null, true, null, Array.Empty<EnhancedFinding>()));

            var state = await new AnalysisCheckpointJournal(paths).OpenAsync("dummy/video.mp4", 1.0);

            Assert.True(state.HasResume);
            Assert.Equal(2, state.LastFrameIndex);
            Assert.Equal(2, state.Frames.Count);
        }
        finally
        {
            paths.Cleanup();
        }
    }

    [Fact]
    public async Task Unvollstaendige_letzte_zeile_wird_sicher_gekuerzt()
    {
        var paths = new TempTelemetryPaths();
        try
        {
            var journal = new AnalysisCheckpointJournal(paths);
            await journal.OpenAsync("dummy/video.mp4", 1.0);
            await journal.AppendFrameAsync(UpdateFrame(1));
            await journal.AppendFrameAsync(AdvanceFrame(2));

            // Absturz-Kante: halbe Zeile ohne Zeilenende anhaengen.
            var path = paths.SingleJournalPath();
            await File.AppendAllTextAsync(path, "{\"type\":\"frame\",\"frame_inde");

            var journal2 = new AnalysisCheckpointJournal(paths);
            var state = await journal2.OpenAsync("dummy/video.mp4", 1.0);

            Assert.True(state.HasResume);
            Assert.Equal(2, state.LastFrameIndex);
            Assert.Equal(2, state.Frames.Count);

            // Der Schweif ist weg: ein neuer Append schliesst sauber an.
            await journal2.AppendFrameAsync(UpdateFrame(3));
            var text = await File.ReadAllTextAsync(path);
            Assert.DoesNotContain("{\"type\":\"frame\",\"frame_inde", text);
            Assert.Contains("\"frame_index\":3", text);
        }
        finally
        {
            paths.Cleanup();
        }
    }

    [Fact]
    public async Task Beschaedigte_mittlere_zeile_verwirft_resume_mit_logwarnung()
    {
        var paths = new TempTelemetryPaths();
        try
        {
            var journal = new AnalysisCheckpointJournal(paths);
            await journal.OpenAsync("dummy/video.mp4", 1.0);
            await journal.AppendFrameAsync(UpdateFrame(1));
            await journal.AppendFrameAsync(UpdateFrame(2));

            // Kaputte Zeile MIT Zeilenende in die Mitte setzen (keine Absturz-Kante).
            var path = paths.SingleJournalPath();
            var lines = File.ReadAllLines(path);
            File.WriteAllLines(path, new[] { lines[0], lines[1], "{\"type\":\"frame\",KAPUTT", lines[2] });

            var logger = new ListLogger();
            var state = await new AnalysisCheckpointJournal(paths, logger).OpenAsync("dummy/video.mp4", 1.0);

            Assert.False(state.HasResume);
            Assert.Contains(logger.Entries, e => e.Level == LogLevel.Warning);
            // Frisch gestartet: die erste Zeile ist wieder ein neuer Header.
            Assert.Contains("\"type\":\"header\"", File.ReadLines(path).First());
        }
        finally
        {
            paths.Cleanup();
        }
    }

    [Theory]
    [InlineData(new[] { 1, 2, 4 }, "Luecke")]
    [InlineData(new[] { 1, 2, 2 }, "Duplikat")]
    [InlineData(new[] { 1, 2, 3, 2 }, "Ruecklauf")]
    public async Task Ungueltige_framenummern_verwerfen_resume(int[] frameNumbers, string fall)
    {
        var paths = new TempTelemetryPaths();
        try
        {
            var journal = new AnalysisCheckpointJournal(paths);
            await journal.OpenAsync("dummy/video.mp4", 1.0);
            foreach (var number in frameNumbers)
                await journal.AppendFrameAsync(UpdateFrame(number));

            var logger = new ListLogger();
            var state = await new AnalysisCheckpointJournal(paths, logger).OpenAsync("dummy/video.mp4", 1.0);

            Assert.False(state.HasResume, $"Resume muss bei {fall} verworfen werden.");
            Assert.Contains(logger.Entries, e => e.Level == LogLevel.Warning);
            Assert.Contains("\"type\":\"header\"", File.ReadLines(paths.SingleJournalPath()).First());
        }
        finally
        {
            paths.Cleanup();
        }
    }

    [Fact]
    public async Task Fehlende_framenummer_verwirft_resume()
    {
        var paths = new TempTelemetryPaths();
        try
        {
            var journal = new AnalysisCheckpointJournal(paths);
            await journal.OpenAsync("dummy/video.mp4", 1.0);
            await journal.AppendFrameAsync(UpdateFrame(1));
            // Frame-Zeile ohne frame_index (mit sauberem Zeilenende).
            await File.AppendAllTextAsync(paths.SingleJournalPath(), "{\"type\":\"frame\",\"kind\":\"update\"}\n");

            var logger = new ListLogger();
            var state = await new AnalysisCheckpointJournal(paths, logger).OpenAsync("dummy/video.mp4", 1.0);

            Assert.False(state.HasResume);
            Assert.Contains(logger.Entries, e => e.Level == LogLevel.Warning);
        }
        finally
        {
            paths.Cleanup();
        }
    }

    /// <summary>Frame-Zeilen mit fehlenden Pflichtfeldern (duerfen NICHT durch Standardwerte erfunden werden).</summary>
    public static IEnumerable<object[]> FehlendePflichtfelder()
    {
        yield return ["update ohne Zeit", "{\"type\":\"frame\",\"kind\":\"update\",\"frame_index\":2,\"meter\":4.17,\"is_meter_estimated\":true,\"meter_source\":\"LinearEstimate\",\"findings\":[]}"];
        yield return ["update ohne Meter", "{\"type\":\"frame\",\"kind\":\"update\",\"frame_index\":2,\"time_sec\":1,\"is_meter_estimated\":true,\"meter_source\":\"LinearEstimate\",\"findings\":[]}"];
        yield return ["update ohne Schaetzflag", "{\"type\":\"frame\",\"kind\":\"update\",\"frame_index\":2,\"time_sec\":1,\"meter\":4.17,\"meter_source\":\"LinearEstimate\",\"findings\":[]}"];
        yield return ["update ohne Findings", "{\"type\":\"frame\",\"kind\":\"update\",\"frame_index\":2,\"time_sec\":1,\"meter\":4.17,\"is_meter_estimated\":true,\"meter_source\":\"LinearEstimate\"}"];
        yield return ["update ohne Meterquelle", "{\"type\":\"frame\",\"kind\":\"update\",\"frame_index\":2,\"time_sec\":1,\"meter\":4.17,\"is_meter_estimated\":true,\"findings\":[]}"];
        yield return ["advance ohne Zeit", "{\"type\":\"frame\",\"kind\":\"advance\",\"frame_index\":2,\"meter\":4.17,\"is_meter_estimated\":true}"];
        yield return ["advance ohne Meter", "{\"type\":\"frame\",\"kind\":\"advance\",\"frame_index\":2,\"time_sec\":1,\"is_meter_estimated\":true}"];
        yield return ["advance ohne Schaetzflag", "{\"type\":\"frame\",\"kind\":\"advance\",\"frame_index\":2,\"time_sec\":1,\"meter\":4.17}"];
    }

    [Theory]
    [MemberData(nameof(FehlendePflichtfelder))]
    public async Task Fehlende_pflichtfelder_verwerfen_resume(string fall, string kaputteZeile)
    {
        var paths = new TempTelemetryPaths();
        try
        {
            // Ein gueltiger Frame 1, danach die Frame-2-Zeile mit fehlendem Pflichtfeld.
            var journal = new AnalysisCheckpointJournal(paths);
            await journal.OpenAsync("dummy/video.mp4", 1.0);
            await journal.AppendFrameAsync(UpdateFrame(1));
            await File.AppendAllTextAsync(paths.SingleJournalPath(), kaputteZeile + "\n");

            var logger = new ListLogger();
            var state = await new AnalysisCheckpointJournal(paths, logger).OpenAsync("dummy/video.mp4", 1.0);

            Assert.False(state.HasResume, $"Resume muss bei '{fall}' verworfen werden statt Werte zu erfinden.");
            Assert.Contains(logger.Entries, e => e.Level == LogLevel.Warning);
        }
        finally
        {
            paths.Cleanup();
        }
    }

    [Fact]
    public async Task Aufraeumregel_loescht_nur_abgeschlossene_alte_journale()
    {
        AnalysisCheckpointJournal.ResetCleanupThrottle();
        var paths = new TempTelemetryPaths();
        try
        {
            // a) abgeschlossen + alt -> loeschen
            var completedOld = new AnalysisCheckpointJournal(paths);
            await completedOld.OpenAsync("dummy/a.mp4", 1.0);
            await completedOld.AppendFrameAsync(UpdateFrame(1));
            await completedOld.CompleteAsync();
            var completedOldPath = paths.SingleJournalPath();

            // b) offen + alt -> behalten
            var open = new AnalysisCheckpointJournal(paths);
            await open.OpenAsync("dummy/b.mp4", 1.0);
            await open.AppendFrameAsync(UpdateFrame(1));
            var openPath = Directory.GetFiles(paths.Dir, "analysis_checkpoint_*.jsonl")
                .Single(f => f != completedOldPath);

            // c) beschaedigt + alt -> behalten
            var corruptPath = Path.Combine(paths.Dir, "analysis_checkpoint_kaputt.jsonl");
            File.WriteAllText(corruptPath, "{\"type\":\"frame\",KAPUTT\n");

            // d) abgeschlossen + frisch -> behalten
            var completedYoung = new AnalysisCheckpointJournal(paths);
            await completedYoung.OpenAsync("dummy/d.mp4", 1.0);
            await completedYoung.CompleteAsync();
            var completedYoungPath = Directory.GetFiles(paths.Dir, "analysis_checkpoint_*.jsonl")
                .Single(f => f != completedOldPath && f != openPath && f != corruptPath);

            var old = DateTime.UtcNow.AddDays(-30);
            File.SetLastWriteTimeUtc(completedOldPath, old);
            File.SetLastWriteTimeUtc(openPath, old);
            File.SetLastWriteTimeUtc(corruptPath, old);

            var deleted = AnalysisCheckpointJournal.CleanupCompletedJournals(paths, TimeSpan.FromDays(14));

            Assert.Equal(1, deleted);
            Assert.False(File.Exists(completedOldPath));
            Assert.True(File.Exists(openPath), "Offenes Journal darf nie geloescht werden.");
            Assert.True(File.Exists(corruptPath), "Beschaedigtes Journal darf nie geloescht werden.");
            Assert.True(File.Exists(completedYoungPath), "Frisch abgeschlossenes Journal bleibt erhalten.");
        }
        finally
        {
            paths.Cleanup();
        }
    }

    [Fact]
    public async Task Aufraeumregel_ist_prozessweit_gebremst()
    {
        // Die Begrenzung darf nicht bei jeder Videoanalyse laufen: hoechstens einmal
        // pro Intervall. Ein zweiter Aufruf innerhalb des Intervalls laesst selbst eine
        // loeschbare Datei unangetastet; nach Intervallende laeuft die Regel wieder.
        AnalysisCheckpointJournal.ResetCleanupThrottle();
        var paths = new TempTelemetryPaths();
        try
        {
            async Task<string> LegAbgeschlossenesAltesJournalAn(string name)
            {
                var journal = new AnalysisCheckpointJournal(paths);
                await journal.OpenAsync(name, 1.0);
                await journal.AppendFrameAsync(UpdateFrame(1));
                await journal.CompleteAsync();
                var path = Directory.GetFiles(paths.Dir, "analysis_checkpoint_*.jsonl")
                    .OrderByDescending(File.GetLastWriteTimeUtc)
                    .First();
                File.SetLastWriteTimeUtc(path, DateTime.UtcNow.AddDays(-30));
                return path;
            }

            // Erster Lauf: loescht das alte, abgeschlossene Journal und startet das Intervall.
            var firstPath = await LegAbgeschlossenesAltesJournalAn("dummy/a.mp4");
            var first = AnalysisCheckpointJournal.CleanupCompletedJournals(
                paths, TimeSpan.FromDays(14), TimeSpan.FromHours(1));
            Assert.Equal(1, first);
            Assert.False(File.Exists(firstPath));

            // Sofortiger Folgeaufruf: gebremst — die zweite loeschbare Datei bleibt stehen.
            var secondPath = await LegAbgeschlossenesAltesJournalAn("dummy/b.mp4");
            var second = AnalysisCheckpointJournal.CleanupCompletedJournals(
                paths, TimeSpan.FromDays(14), TimeSpan.FromHours(1));
            Assert.Equal(0, second);
            Assert.True(File.Exists(secondPath), "Gebremster Lauf darf nichts loeschen.");

            // Nach Intervallende (hier: Intervall 0) laeuft die Regel wieder normal.
            var third = AnalysisCheckpointJournal.CleanupCompletedJournals(
                paths, TimeSpan.FromDays(14), TimeSpan.Zero);
            Assert.Equal(1, third);
            Assert.False(File.Exists(secondPath));
        }
        finally
        {
            paths.Cleanup();
        }
    }

    [Fact]
    public async Task Aufraeumregel_uebersteht_gesperrte_ablage_und_stoppt_nichts()
    {
        // Schlaegt das Aufzaehlen der Ablage fehl (hier ueber die Aufzaehlungs-Naht,
        // keine echte Windows-ACL), darf NUR die Bereinigung uebersprungen werden:
        // Rueckgabe 0, sichtbare Warnung, keine Exception nach aussen — und die Bremse
        // wird nicht gesetzt, damit der naechste Lauf es erneut versuchen kann.
        AnalysisCheckpointJournal.ResetCleanupThrottle();
        var paths = new TempTelemetryPaths();
        try
        {
            Directory.CreateDirectory(paths.Dir);
            var logger = new ListLogger();

            var deleted = AnalysisCheckpointJournal.CleanupCompletedJournals(
                paths, TimeSpan.FromDays(14), TimeSpan.Zero, logger,
                fileEnumeration: _ => throw new UnauthorizedAccessException("Ablage gesperrt (Test)"));

            Assert.Equal(0, deleted);
            Assert.Contains(logger.Entries, e => e.Level == LogLevel.Warning);

            // Bremse wurde bei dem Fehlversuch nicht gesetzt: der Folgelauf arbeitet normal.
            var journal = new AnalysisCheckpointJournal(paths);
            await journal.OpenAsync("dummy/a.mp4", 1.0);
            await journal.AppendFrameAsync(UpdateFrame(1));
            await journal.CompleteAsync();
            var completedPath = paths.SingleJournalPath();
            File.SetLastWriteTimeUtc(completedPath, DateTime.UtcNow.AddDays(-30));

            var retry = AnalysisCheckpointJournal.CleanupCompletedJournals(
                paths, TimeSpan.FromDays(14), TimeSpan.Zero, logger);
            Assert.Equal(1, retry);
            Assert.False(File.Exists(completedPath));
        }
        finally
        {
            paths.Cleanup();
        }
    }
}
