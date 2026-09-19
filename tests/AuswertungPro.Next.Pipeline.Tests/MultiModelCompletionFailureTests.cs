using System.Diagnostics;
using System.Net;
using System.Runtime.CompilerServices;
using System.Text.Json;
using AuswertungPro.Next.Application.Ai;
using AuswertungPro.Next.Application.Diagnostics;
using AuswertungPro.Next.Infrastructure.Ai;
using AuswertungPro.Next.Infrastructure.Ai.Pipeline;
using AuswertungPro.Next.Infrastructure.Ai.Shared;
using Xunit;

namespace AuswertungPro.Next.Pipeline.Tests;

[Collection(VsaCodeResolverTestCollection.Name)]
public sealed class MultiModelCompletionFailureTests : IDisposable, ITelemetryPathResolver
{
    private readonly string _root = Path.Combine(Path.GetTempPath(), "sewerstudio-analysis-completion-" + Guid.NewGuid());
    private string Video => Path.Combine(_root, "synthetic.mp4");

    public MultiModelCompletionFailureTests()
    {
        Directory.CreateDirectory(_root);
        VsaResolverTestCatalog.ConfigureDefault();
    }

    [FfmpegFact]
    public async Task EchtesFruehesEof_MeldetTeilvideoUndLaesstCheckpointOffen()
    {
        var ffmpeg = new FfmpegFileLocator().ResolveFfmpeg();
        await CreateVideoAsync(ffmpeg);
        using (var file = new FileStream(Video, FileMode.Open, FileAccess.Write))
            file.SetLength((long)(file.Length * 0.35));
        var progress = new ProgressLog();
        var result = await CreateService(new Client(), 10, realFfmpeg: ffmpeg).AnalyzeAsync(Video, progress);

        Assert.InRange(result.FramesAnalyzed, 1, 8);
        Assert.True(result.Degraded, result.DegradedReason);
        Assert.True(result.Incomplete);
        Assert.Contains("teilweise", result.DegradedReason!, StringComparison.OrdinalIgnoreCase);
        Assert.Contains($"{result.FramesAnalyzed}/10", result.DegradedReason);
        Assert.False(JournalContains("\"type\":\"completed\""));
        Assert.Contains("teilweise", progress.LastStatus!, StringComparison.OrdinalIgnoreCase);
    }

    [FfmpegFact]
    public async Task EchterFfmpegFehlerexit_DarfKeinenVollstaendigenLaufBestaetigen()
    {
        await File.WriteAllTextAsync(Video, "kein Video");
        var result = await CreateService(new Client(), 10,
            realFfmpeg: new FfmpegFileLocator().ResolveFfmpeg()).AnalyzeAsync(Video);

        Assert.Equal(0, result.FramesAnalyzed);
        Assert.True(result.Degraded);
        Assert.True(result.Incomplete);
        Assert.Contains("ffmpeg-Exit", result.DegradedReason!);
        Assert.False(JournalContains("\"type\":\"completed\""));
    }

    [FfmpegFact]
    public async Task EchtesVollstaendigesVideo_SchliesstGesundenCheckpointAb()
    {
        var ffmpeg = new FfmpegFileLocator().ResolveFfmpeg();
        await CreateVideoAsync(ffmpeg);
        var result = await CreateService(new Client(), 10, realFfmpeg: ffmpeg).AnalyzeAsync(Video);

        Assert.True(result.IsSuccess, result.Error);
        Assert.InRange(result.FramesAnalyzed, 9, 10);
        Assert.False(result.Degraded, result.DegradedReason);
        Assert.False(result.Incomplete);
        Assert.True(JournalContains("\"type\":\"completed\""));
    }

    [Theory]
    [InlineData("http503")]
    [InlineData("kaputtes-json")]
    [InlineData("timeout")]
    public async Task QwenZurueckgegebeneFehler_BleibenAuchNachSpaeteremErfolgSichtbar(string mode)
    {
        using var handler = new QwenHandler((call, _) => call <= 8 ? Response(mode) : Response("leer"));
        using var http = new HttpClient(handler);
        using var ollama = new OllamaClient(new Uri("http://localhost:11434"), http);
        var trace = new TraceLog();
        var service = CreateService(new Client(), 10, new EnhancedVisionAnalysisService(ollama, "test"), trace);

        var result = await service.AnalyzeAsync(Video);

        Assert.Equal(10, handler.Calls);
        Assert.True(result.IsSuccess, result.Error);
        Assert.True(result.Degraded);
        Assert.Contains("Qwen", result.DegradedReason!);
        Assert.NotEmpty(result.Detections);
        Assert.Equal(8, trace.Entries.Count(entry => entry.DropReason is "qwen_error" or "qwen_timeout"));
        Assert.False(JournalContains("\"type\":\"completed\""));
        Assert.True(JournalContains("\"kind\":\"retry_required\""));
    }

    [Fact]
    public async Task QwenGueltigesLeeresErgebnis_ZaehltNichtAlsAusfall()
    {
        using var handler = new QwenHandler((_, _) => Response("leer"));
        using var http = new HttpClient(handler);
        using var ollama = new OllamaClient(new Uri("http://localhost:11434"), http);
        var result = await CreateService(new Client(), 3,
            new EnhancedVisionAnalysisService(ollama, "test")).AnalyzeAsync(Video);

        Assert.Equal(3, handler.Calls);
        Assert.False(result.Degraded, result.DegradedReason);
        Assert.False(result.Incomplete);
        Assert.True(JournalContains("\"type\":\"completed\""));
        Assert.False(JournalContains("\"kind\":\"retry_required\""));
    }

    [Fact]
    public async Task QwenNutzerabbruch_WirdWeitergereichtUndNichtAlsModellfehlerJournalisiert()
    {
        using var cancellation = new CancellationTokenSource();
        using var handler = new QwenHandler((_, ct) =>
        {
            cancellation.Cancel();
            throw new OperationCanceledException(ct);
        });
        using var http = new HttpClient(handler);
        using var ollama = new OllamaClient(new Uri("http://localhost:11434"), http);
        var service = CreateService(new Client(), 3, new EnhancedVisionAnalysisService(ollama, "test"));

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => service.AnalyzeAsync(Video, ct: cancellation.Token));

        Assert.False(JournalContains("\"type\":\"completed\""));
        Assert.False(JournalContains("\"kind\":\"retry_required\""));
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task SamTechnischerTeilOderVollverlust_BleibtSichtbarUndWirdBeimResumeErneutBearbeitet(bool partial)
    {
        var firstClient = new Client { Sam = call => call == 2 ? SamLoss(partial) : HealthySam() };
        var first = await CreateService(firstClient, 3).AnalyzeAsync(Video);

        Assert.True(first.IsSuccess, first.Error);
        Assert.True(first.Degraded);
        Assert.True(first.Incomplete);
        Assert.Contains("SAM", first.DegradedReason!);
        Assert.NotEmpty(first.Detections);
        Assert.True(JournalContains("\"kind\":\"retry_required\""));
        Assert.False(JournalContains("\"type\":\"completed\""));

        var healthyClient = new Client();
        var repeated = await CreateService(healthyClient, 3).AnalyzeAsync(Video);

        Assert.Equal(2, healthyClient.DinoCalls); // Nur der saubere Frame 1 darf wiederverwendet werden.
        Assert.False(repeated.Degraded, repeated.DegradedReason);
        Assert.False(repeated.Incomplete);
        Assert.True(JournalContains("\"type\":\"completed\""));
        Assert.False(JournalContains("\"kind\":\"retry_required\""));
    }

    [Fact]
    public async Task SamAlleMaskenTechnischVerloren_DarfNichtAlsGesundesNullergebnisEnden()
    {
        var result = await CreateService(new Client { Sam = _ => SamLoss(false) }, 3).AnalyzeAsync(Video);

        Assert.Empty(result.Detections);
        Assert.True(result.Degraded);
        Assert.True(result.Incomplete);
        Assert.Contains("SAM", result.DegradedReason!);
        Assert.False(JournalContains("\"type\":\"completed\""));
    }

    [Fact]
    public async Task SamBewusstSchwacheMasken_SindKeinTechnischerFehlerUndBrauchenKeinenRetry()
    {
        var client = new Client { Sam = _ => new SamResponse([], 640, 480, 1,
            Degraded: true, RequestedBoxes: 2, SkippedBoxes: 2, LowScoreBoxes: 2) };
        var result = await CreateService(client, 3).AnalyzeAsync(Video);

        Assert.Empty(result.Detections);
        Assert.False(result.Incomplete);
        Assert.True(JournalContains("\"type\":\"completed\""));
        Assert.False(JournalContains("\"kind\":\"retry_required\""));
    }

    private MultiModelAnalysisService CreateService(Client client, int frameCount,
        EnhancedVisionAnalysisService? qwen = null, TraceLog? trace = null, string? realFfmpeg = null)
        => new(trace ?? new TraceLog(), client, new PipelineConfig(true, new Uri("http://localhost:5001"), null,
                PipelineMode.MultiModel, 0.25, new Dictionary<string, double>(), 0.25, 0.20, 30, 300),
            ffmpegPath: realFfmpeg ?? "ffmpeg", qwenVision: qwen,
            frameSource: realFfmpeg is null ? (_, _, _, _, ct) => Frames(frameCount, ct) : null,
            durationProbe: (_, _) => Task.FromResult(frameCount * 3d),
            checkpointJournal: new AnalysisCheckpointJournal(this))
        { FrameStepSeconds = 3, UseClsPrefilter = false, ClassifierOnlyStructuralEnabled = false,
            ClassifierDecisionEnabled = false, EstimatedReachLengthM = 5 };

    private static async IAsyncEnumerable<FrameData> Frames(int count, [EnumeratorCancellation] CancellationToken ct)
    {
        for (var i = 0; i < count; i++)
        {
            ct.ThrowIfCancellationRequested();
            yield return new FrameData(i * 3d, [1, 2, 3]);
            await Task.Yield();
        }
    }

    private static SamMaskResult Mask(string label, double x, double y) => new(label, 0.8,
        [x, y, x + 70, y + 70], "", 4900, 640 * 480, 70, 70, x + 35, y + 35);
    private static SamResponse HealthySam() => new([Mask("crack", 10, 10), Mask("roots", 540, 350)], 640, 480, 1);
    private static SamResponse SamLoss(bool partial) => new(partial ? [Mask("crack", 10, 10)] : [], 640, 480, 1,
        Degraded: true, RequestedBoxes: 2, SkippedBoxes: partial ? 1 : 2, Error: "synthetischer SAM-Inferenzfehler");

    private static HttpResponseMessage Response(string mode)
    {
        if (mode == "timeout") throw new TimeoutException("synthetischer Timeout");
        return new HttpResponseMessage(mode == "http503" ? HttpStatusCode.ServiceUnavailable : HttpStatusCode.OK)
        {
            Content = new StringContent(mode == "kaputtes-json" ? "{kaputt" : mode == "http503" ? "synthetisch offline" :
                JsonSerializer.Serialize(new { message = new { content = "{\"meter\":null,\"findings\":[],\"image_quality\":\"gut\",\"is_empty_frame\":true}" } }))
        };
    }

    private async Task CreateVideoAsync(string ffmpeg)
    {
        var start = new ProcessStartInfo(ffmpeg) { UseShellExecute = false, CreateNoWindow = true, RedirectStandardError = true };
        foreach (var argument in new[] { "-hide_banner", "-loglevel", "error", "-f", "lavfi", "-i", "testsrc=size=160x120:rate=10",
                     "-t", "30", "-c:v", "mpeg4", "-q:v", "4", "-movflags", "+faststart", "-y", Video })
            start.ArgumentList.Add(argument);
        using var process = Process.Start(start)!;
        var error = process.StandardError.ReadToEndAsync();
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(30));
        try { await process.WaitForExitAsync(timeout.Token); }
        catch { if (!process.HasExited) process.Kill(entireProcessTree: true); throw; }
        Assert.True(process.ExitCode == 0, await error);
    }

    private bool JournalContains(string text) => Directory.GetFiles(_root, AnalysisCheckpointJournal.FilePattern)
        .Any(path => File.ReadAllText(path).Contains(text, StringComparison.Ordinal));
    public string? ResolveFile(string fileName) => Path.Combine(_root, fileName);
    public void Dispose() { if (Directory.Exists(_root)) Directory.Delete(_root, recursive: true); }

    private sealed class QwenHandler(Func<int, CancellationToken, HttpResponseMessage> respond) : HttpMessageHandler
    {
        public int Calls { get; private set; }
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct)
            => Task.FromResult(respond(++Calls, ct));
    }

    private sealed class ProgressLog : IProgress<VideoAnalysisProgress>
    {
        public string? LastStatus { get; private set; }
        public void Report(VideoAnalysisProgress value) => LastStatus = value.Status;
    }

    private sealed class TraceLog : IPipelineTraceWriter
    {
        public List<PipelineTraceEntry> Entries { get; } = [];
        public Task WriteAsync(PipelineTraceEntry entry) { Entries.Add(entry); return Task.CompletedTask; }
        public Task WriteSummaryAsync(string runId, TelemetrySummary summary) => Task.CompletedTask;
        public string? ResolvePath(string runId) => null;
        public string? ResolveSummaryPath(string runId) => null;
    }

    private sealed class Client : IVisionPipelineClient
    {
        public int DinoCalls { get; private set; }
        private int _samCalls;
        public Func<int, SamResponse> Sam { get; init; } = _ => HealthySam();
        public Task<SidecarHealthResponse?> HealthCheckAsync(CancellationToken ct = default) =>
            Task.FromResult<SidecarHealthResponse?>(new SidecarHealthResponse("ok", "test", null,
                DetectorQualification: new(true, null)));
        public Task<PipelineHealthCheckResult> CheckHealthDetailedAsync(CancellationToken ct = default) =>
            Task.FromResult(new PipelineHealthCheckResult(true, true, 200, null, null));
        public Task<YoloResponse> DetectYoloAsync(YoloRequest request, CancellationToken ct = default) =>
            Task.FromResult(new YoloResponse(true, [], "damage", 1, DetectorQualified: true));
        public Task<DinoResponse> DetectDinoAsync(DinoRequest request, CancellationToken ct = default)
        {
            DinoCalls++;
            return Task.FromResult(new DinoResponse([new(10, 10, 80, 80, "crack", 0.8, "crack"),
                new(540, 350, 610, 420, "roots", 0.8, "roots")], 1));
        }
        public Task<SamResponse> SegmentSamAsync(SamRequest request, CancellationToken ct = default) => Task.FromResult(Sam(++_samCalls));
        public Task<YoloClassifyResponse> ClassifyYoloAsync(YoloClassifyRequest request, CancellationToken ct = default) =>
            Task.FromResult(new YoloClassifyResponse([], 1));
    }
}
