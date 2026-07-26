using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;
using AuswertungPro.Next.Application.Ai;
using AuswertungPro.Next.Application.Common;
using AuswertungPro.Next.Infrastructure.Ai;
using Xunit;

namespace AuswertungPro.Next.Pipeline.Tests;

/// <summary>
/// F4/F5: Der Ollama-Only-Frame-Loop (<see cref="VideoFullAnalysisService"/>) muss
/// ausgefallene Frame-Analysen (Outcome Timeout/Modellfehler) und ein nur teilweise
/// extrahiertes Video als "Analyse unvollstaendig" melden statt still "Fertig".
/// Getestet ueber die internen Test-Seams des Dienstes (Fake-Framequelle +
/// Vision-Override) — ohne echtes ffmpeg/Ollama.
/// </summary>
public sealed class VideoFullAnalysisFailureTests
{
    [Fact]
    public async Task AnalyzeAsync_FrameTimeout_MeldetAnalyseUnvollstaendigMitZaehler()
    {
        using var video = new TempVideoFile();
        var calls = 0;
        var service = CreateService(
            new FakeFrameSource(frameCount: 3)
            {
                Completion = new VideoFrameStreamCompletion(true, 3, 3, 0, null)
            },
            vision: (_, _) =>
            {
                var call = Interlocked.Increment(ref calls);
                return Task.FromResult(call == 2
                    ? EnhancedFrameAnalysis.Empty("Timeout (120s)", AnalysisOutcome.Timeout)
                    : OkNoFinding());
            });

        var progress = new ProgressCollector();
        var result = await service.AnalyzeAsync(video.Path, progress);

        Assert.True(result.IsSuccess);
        Assert.True(result.Degraded);
        Assert.NotNull(result.DegradedReason);
        Assert.Contains("Analyse unvollstaendig", result.DegradedReason);
        Assert.Contains("1 von 3 Frames fehlgeschlagen", result.DegradedReason);
        Assert.Contains("Timeout", result.DegradedReason);
        Assert.Equal(3, result.FramesAnalyzed);
        Assert.NotNull(result.Telemetry);
        Assert.Equal(1, result.Telemetry!.FailedFrames);

        Assert.NotNull(progress.LastStatus);
        Assert.Contains("Analyse unvollstaendig", progress.LastStatus);
        Assert.Contains("1 von 3", progress.LastStatus);
    }

    [Fact]
    public async Task AnalyzeAsync_ModellfehlerUndTimeout_GruendeWerdenAggregiert()
    {
        using var video = new TempVideoFile();
        var calls = 0;
        var service = CreateService(
            new FakeFrameSource(frameCount: 4)
            {
                Completion = new VideoFrameStreamCompletion(true, 4, 4, 0, null)
            },
            vision: (_, _) =>
            {
                var call = Interlocked.Increment(ref calls);
                return Task.FromResult(call switch
                {
                    1 => EnhancedFrameAnalysis.Empty("Timeout (120s)", AnalysisOutcome.Timeout),
                    3 => EnhancedFrameAnalysis.EmptyFromException(new InvalidOperationException("boom")),
                    _ => OkNoFinding()
                });
            });

        var progress = new ProgressCollector();
        var result = await service.AnalyzeAsync(video.Path, progress);

        Assert.True(result.Degraded);
        Assert.NotNull(result.DegradedReason);
        Assert.Contains("2 von 4 Frames fehlgeschlagen", result.DegradedReason);
        Assert.Contains("Timeout: 1", result.DegradedReason);
        Assert.Contains("Modellfehler: 1", result.DegradedReason);
        Assert.Equal(2, result.Telemetry!.FailedFrames);
    }

    [Fact]
    public async Task AnalyzeAsync_OhneAusfaelle_BleibtBisherigeErfolgsmeldung()
    {
        using var video = new TempVideoFile();
        var service = CreateService(
            new FakeFrameSource(frameCount: 3)
            {
                Completion = new VideoFrameStreamCompletion(true, 3, 3, 0, null)
            },
            vision: (_, _) => Task.FromResult(OkNoFinding()));

        var progress = new ProgressCollector();
        var result = await service.AnalyzeAsync(video.Path, progress);

        Assert.True(result.IsSuccess);
        Assert.False(result.Degraded);
        Assert.Null(result.DegradedReason);
        Assert.Equal(0, result.Telemetry!.FailedFrames);

        Assert.NotNull(progress.LastStatus);
        Assert.StartsWith("Fertig", progress.LastStatus);
        Assert.Contains("erkannt", progress.LastStatus);
        Assert.DoesNotContain("unvollstaendig", progress.LastStatus);
    }

    [Fact]
    public async Task AnalyzeAsync_Teilvideo_MeldetNurTeilweiseAnalysiert()
    {
        using var video = new TempVideoFile();
        var service = CreateService(
            new FakeFrameSource(frameCount: 2)
            {
                // ffmpeg brach nach 2 von 10 erwarteten Frames mit Fehler ab.
                Completion = new VideoFrameStreamCompletion(
                    false, 2, 10, 1, "ffmpeg-Exit 1: Conversion failed!")
            },
            vision: (_, _) => Task.FromResult(OkNoFinding()));

        var progress = new ProgressCollector();
        var result = await service.AnalyzeAsync(video.Path, progress);

        Assert.True(result.IsSuccess);
        Assert.True(result.Degraded);
        Assert.NotNull(result.DegradedReason);
        Assert.Contains("Video nur teilweise analysiert", result.DegradedReason);
        Assert.Contains("Frames 2/10", result.DegradedReason);
        Assert.Contains("ffmpeg-Exit 1", result.DegradedReason);
        Assert.Equal(0, result.Telemetry!.FailedFrames);   // kein KI-Ausfall, nur Extraktion

        Assert.NotNull(progress.LastStatus);
        Assert.Contains("Video nur teilweise analysiert (Frames 2/10, ffmpeg-Exit 1)", progress.LastStatus);
    }

    // ── Aufbau / Fakes ───────────────────────────────────────────────────

    private static VideoFullAnalysisService CreateService(
        IVideoFrameSource frameSource,
        Func<string, CancellationToken, Task<EnhancedFrameAnalysis>> vision)
    {
        return new VideoFullAnalysisService(
            new NullTraceWriter(),
            new EnhancedVisionAnalysisService(new OllamaClient(new Uri("http://127.0.0.1:11434")), "test-model"),
            ffmpegPath: "ffmpeg",
            ffprobePath: "ffprobe",
            logger: null,
            processOutputs: new FakeProcessOutputReader(durationStdout: "9.0"))
        {
            FrameSourceFactory = (_, _, _) => frameSource,
            VisionAnalyzeOverride = vision
        };
    }

    private static EnhancedFrameAnalysis OkNoFinding()
        => new(
            Meter: 1.0,
            PipeMaterial: "unbekannt",
            PipeDiameterMm: null,
            Findings: Array.Empty<EnhancedFinding>(),
            ImageQuality: "gut",
            IsEmptyFrame: false,
            Error: null,
            Outcome: AnalysisOutcome.NoFinding);

    private sealed class FakeFrameSource : IVideoFrameSource
    {
        private readonly int _frameCount;

        public FakeFrameSource(int frameCount) => _frameCount = frameCount;

        public VideoFrameStreamCompletion? Completion { get; set; }
        public bool Disposed { get; private set; }

        public async IAsyncEnumerable<FrameData> ReadFramesAsync(
            [EnumeratorCancellation] CancellationToken ct)
        {
            for (var i = 0; i < _frameCount; i++)
            {
                ct.ThrowIfCancellationRequested();
                // Bytes muessen nur nicht-leer sein; der Vision-Schritt ist gefaked.
                yield return new FrameData(i * 3.0, new byte[] { 0x89, 0x50, 0x4E, 0x47 });
                await Task.Yield();
            }
        }

        public ValueTask DisposeAsync()
        {
            Disposed = true;
            return ValueTask.CompletedTask;
        }
    }

    private sealed class FakeProcessOutputReader : IProcessOutputReader
    {
        private readonly string _durationStdout;

        public FakeProcessOutputReader(string durationStdout) => _durationStdout = durationStdout;

        public Task<ProcessOutputResult?> ReadToExitAsync(
            ProcessStartInfo startInfo, CancellationToken cancellationToken, Action<int>? onStarted = null)
            => Task.FromResult<ProcessOutputResult?>(new ProcessOutputResult(0, _durationStdout, ""));
    }

    private sealed class NullTraceWriter : IPipelineTraceWriter
    {
        public Task WriteAsync(PipelineTraceEntry entry) => Task.CompletedTask;
        public Task WriteSummaryAsync(string runId, TelemetrySummary summary) => Task.CompletedTask;
        public string? ResolvePath(string runId) => null;
        public string? ResolveSummaryPath(string runId) => null;
    }

    private sealed class ProgressCollector : IProgress<VideoAnalysisProgress>
    {
        private readonly List<VideoAnalysisProgress> _entries = new();
        public string? LastStatus => _entries.Count == 0 ? null : _entries[^1].Status;
        public void Report(VideoAnalysisProgress value) => _entries.Add(value);
    }

    private sealed class TempVideoFile : IDisposable
    {
        public string Path { get; }

        public TempVideoFile()
        {
            Path = System.IO.Path.Combine(
                System.IO.Path.GetTempPath(), "vfa_failure_" + Guid.NewGuid().ToString("N") + ".mpg");
            File.WriteAllBytes(Path, new byte[] { 0, 1, 2, 3 });
        }

        public void Dispose()
        {
            try { File.Delete(Path); } catch { /* Aufraeumen best effort */ }
        }
    }
}
