using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using AuswertungPro.Next.Application.Ai.Diagnostics;
using AuswertungPro.Next.Application.Ai.Pipeline;

namespace AuswertungPro.Next.Pipeline.Tests;

public class PipeAxisDiagnosticsServiceTests
{
    private static PipeAxisResult Axis(double vx, double vy = 0.5, double conf = 0.8)
        => new(
            VanishingX: vx,
            VanishingY: vy,
            PipeCenterX: 0.5,
            PipeCenterY: 0.5,
            PipeRadiusX: 0.25,
            PipeRadiusY: 0.25,
            Confidence: conf,
            HasJoint: false,
            InferenceTimeMs: 5.0);

    private static (PipeAxisDiagnosticsService svc, AiDiagnosticsRecorder rec, int[] callCount)
        Build(System.Func<int, PipeAxisResult?> axisForCallIndex,
              int historyMaxSize = 8)
    {
        var rec = new AiDiagnosticsRecorder(capacity: 50);
        var callCount = new[] { 0 };
        Task<PipeAxisResult?> Analyze(PipeAxisRequest _, CancellationToken __)
        {
            int idx = callCount[0]++;
            return Task.FromResult(axisForCallIndex(idx));
        }

        var svc = new PipeAxisDiagnosticsService(
            Analyze, rec,
            detector: new PipeAxisBendDetector { MinWindowSize = 5 },
            historyMaxSize: historyMaxSize);
        return (svc, rec, callCount);
    }

    [Fact]
    public async Task PushFrameAsync_BelowWindowSize_DoesNotRecord()
    {
        var (svc, rec, _) = Build(_ => Axis(0.5));

        // 4 Frames < MinWindowSize 5 → noch kein Recorder-Eintrag
        for (int i = 0; i < 4; i++)
            await svc.PushFrameAsync("png", i * 0.2, null, CancellationToken.None);

        Assert.Equal(4, svc.BufferedSampleCount);
        Assert.Empty(rec.Snapshot());
    }

    [Fact]
    public async Task PushFrameAsync_WindowReached_RecordsDiagnosticEvent()
    {
        // Klare Links-Drift damit ein BCC-Kandidat erkannt wird.
        double[] xs = { 0.50, 0.46, 0.40, 0.35, 0.30 };
        var (svc, rec, _) = Build(i => Axis(xs[i]));

        for (int i = 0; i < xs.Length; i++)
            await svc.PushFrameAsync("png", i * 0.2, null, CancellationToken.None);

        var snap = rec.Snapshot();
        Assert.Single(snap);
        Assert.Equal(AiDiagnosticStage.PipeAxisGeometry, snap[0].Stage);
        Assert.NotNull(snap[0].Metadata);
        Assert.Equal("5", snap[0].Metadata!["window"]);
        Assert.Equal("Left", snap[0].Metadata!["direction"]);
        Assert.Equal("BCCAY", snap[0].Metadata!["recommended_code"]);
    }

    [Fact]
    public async Task PushFrameAsync_BeyondHistoryMaxSize_DropsOldestSample()
    {
        var (svc, rec, _) = Build(_ => Axis(0.5), historyMaxSize: 6);

        for (int i = 0; i < 20; i++)
            await svc.PushFrameAsync("png", i * 0.2, null, CancellationToken.None);

        // Buffer-Cap haelt → kein Speicher-Leck (Audit C1)
        Assert.Equal(6, svc.BufferedSampleCount);
    }

    [Fact]
    public async Task PushFrameAsync_NullAnalyzerResult_DoesNothing()
    {
        var (svc, rec, _) = Build(_ => null);

        for (int i = 0; i < 10; i++)
            await svc.PushFrameAsync("png", i * 0.2, null, CancellationToken.None);

        Assert.Equal(0, svc.BufferedSampleCount);
        Assert.Empty(rec.Snapshot());
    }

    [Fact]
    public async Task PushFrameAsync_EmptyBase64_SkipsAnalyzerCall()
    {
        var (svc, rec, callCount) = Build(_ => Axis(0.5));

        await svc.PushFrameAsync("", 0.0, null, CancellationToken.None);
        await svc.PushFrameAsync(null!, 0.0, null, CancellationToken.None);

        Assert.Equal(0, callCount[0]);
        Assert.Empty(rec.Snapshot());
    }

    [Fact]
    public async Task PushFrameAsync_AnalyzerThrows_DoesNotPropagate()
    {
        var rec = new AiDiagnosticsRecorder(capacity: 10);
        Task<PipeAxisResult?> ThrowingAnalyze(PipeAxisRequest _, CancellationToken __)
            => throw new System.InvalidOperationException("Sidecar down");
        var svc = new PipeAxisDiagnosticsService(ThrowingAnalyze, rec);

        // darf NICHT werfen
        await svc.PushFrameAsync("png", 0.0, null, CancellationToken.None);

        Assert.Equal(0, svc.BufferedSampleCount);
        Assert.Empty(rec.Snapshot());
    }

    [Fact]
    public async Task Reset_ClearsRingBuffer()
    {
        var (svc, _, _) = Build(_ => Axis(0.5));
        for (int i = 0; i < 4; i++)
            await svc.PushFrameAsync("png", i * 0.2, null, CancellationToken.None);

        Assert.Equal(4, svc.BufferedSampleCount);
        svc.Reset();
        Assert.Equal(0, svc.BufferedSampleCount);
    }
}
