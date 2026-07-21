using System;
using System.Diagnostics;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using AuswertungPro.Next.Application.Common;
using AuswertungPro.Next.Infrastructure.Ai;
using Xunit;

namespace AuswertungPro.Next.Infrastructure.Tests;

/// <summary>
/// Charakterisierungstests fuer den produktiven Ollama-Only-Fallback
/// <see cref="VideoFullAnalysisService"/>. Abgedeckt sind die Pfade VOR dem
/// ffmpeg-Frame-Loop (Video fehlt, Dauer nicht ermittelbar, Abbruch waehrend Probe) —
/// diese sind ueber den injizierbaren <see cref="IProcessOutputReader"/> ohne echtes
/// ffmpeg/Ollama testbar. Der Frame-Loop-Erfolgspfad haengt am statischen
/// VideoFrameStream.Open + echter Vision und ist ohne Produktionsumbau nicht faketestbar.
/// </summary>
public sealed class VideoFullAnalysisServiceTests
{
    // Dummy-Vision: wird in den hier getesteten Fehlerpfaden nie aufgerufen (kein echter Ollama-Server noetig).
    private static EnhancedVisionAnalysisService DummyVision()
        => new(new OllamaClient(new Uri("http://127.0.0.1:11434")), "test-model");

    private static VideoFullAnalysisService CreateService(IProcessOutputReader? reader)
        => new(DummyVision(), ffmpegPath: "ffmpeg", ffprobePath: "ffprobe", logger: null, processOutputs: reader);

    [Fact]
    public async Task AnalyzeAsync_VideoFehlt_liefert_Failed_ohne_Prozessaufruf()
    {
        var reader = new FakeProcessOutputReader();   // wird nicht erreicht
        var service = CreateService(reader);

        var missing = Path.Combine(Path.GetTempPath(), "vfa_missing_" + Guid.NewGuid().ToString("N") + ".mpg");
        var result = await service.AnalyzeAsync(missing);

        Assert.False(result.IsSuccess);
        Assert.NotNull(result.Error);
        Assert.Contains("Video nicht gefunden", result.Error);
        Assert.Equal(0, reader.CallCount);            // File.Exists-Guard schlaegt vor ffprobe zu
    }

    [Fact]
    public async Task AnalyzeAsync_DauerNichtErmittelbar_liefert_Failed_und_ruft_kein_Ollama()
    {
        // ffprobe UND ffmpeg-Fallback liefern kein verwertbares Ergebnis (null) -> duration <= 0.
        var reader = new FakeProcessOutputReader { Result = null };
        var service = CreateService(reader);

        using var video = new TempVideoFile();
        var result = await service.AnalyzeAsync(video.Path);

        Assert.False(result.IsSuccess);
        Assert.NotNull(result.Error);
        Assert.Contains("Videodauer konnte nicht ermittelt werden", result.Error);
        // Beide Probe-Stufen (ffprobe + ffmpeg) wurden versucht, dann sauber abgebrochen.
        Assert.True(reader.CallCount >= 1);
    }

    [Fact]
    public async Task AnalyzeAsync_UngueltigeProbeAusgabe_liefert_Failed()
    {
        // ffprobe liefert Muell (ExitCode 0, aber keine Zahl), ffmpeg-Fallback ebenso -> Failed.
        var reader = new FakeProcessOutputReader
        {
            Result = new ProcessOutputResult(0, "keine-zahl", "keine Duration-Zeile"),
        };
        var service = CreateService(reader);

        using var video = new TempVideoFile();
        var result = await service.AnalyzeAsync(video.Path);

        Assert.False(result.IsSuccess);
        Assert.Contains("Videodauer konnte nicht ermittelt werden", result.Error);
    }

    [Fact]
    public async Task AnalyzeAsync_AbbruchWaehrendProbe_wird_nicht_als_Failed_verschluckt()
    {
        // Abbruch ist KEIN Analysefehler: OperationCanceledException muss propagieren,
        // nicht als VideoAnalysisResult.Failed maskiert werden.
        var reader = new FakeProcessOutputReader { ObserveCancellation = true };
        var service = CreateService(reader);

        using var video = new TempVideoFile();
        using var cts = new CancellationTokenSource();
        cts.Cancel();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(
            () => service.AnalyzeAsync(video.Path, progress: null, ct: cts.Token));
    }

    // ── Fakes / Helfer ──────────────────────────────────────────────────────

    private sealed class FakeProcessOutputReader : IProcessOutputReader
    {
        public ProcessOutputResult? Result { get; set; }
        public bool ObserveCancellation { get; set; }
        public int CallCount { get; private set; }

        public Task<ProcessOutputResult?> ReadToExitAsync(
            ProcessStartInfo startInfo, CancellationToken cancellationToken, Action<int>? onStarted = null)
        {
            CallCount++;
            if (ObserveCancellation)
                cancellationToken.ThrowIfCancellationRequested();
            return Task.FromResult(Result);
        }
    }

    private sealed class TempVideoFile : IDisposable
    {
        public string Path { get; }

        public TempVideoFile()
        {
            Path = System.IO.Path.Combine(
                System.IO.Path.GetTempPath(), "vfa_video_" + Guid.NewGuid().ToString("N") + ".mpg");
            File.WriteAllBytes(Path, new byte[] { 0, 1, 2, 3 });
        }

        public void Dispose()
        {
            try { File.Delete(Path); } catch { /* Aufraeumen best effort */ }
        }
    }
}
