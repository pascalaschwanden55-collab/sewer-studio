using System.Text.Json;
using AuswertungPro.Next.Application.Ai;
using AuswertungPro.Next.Infrastructure.Ai.Pipeline;
using AuswertungPro.Next.Infrastructure.Telemetry;

namespace AuswertungPro.Next.Pipeline.Tests;

[Collection("EnvironmentVars")]
public sealed class PipelineTraceWriterTests : IDisposable
{
    private readonly string? _previousTelemetryRoot =
        Environment.GetEnvironmentVariable(TelemetryPathResolver.TelemetryDirEnvVar);
    private readonly string _tempRoot = Path.Combine(
        Path.GetTempPath(),
        "sewer-pipeline-trace-tests",
        Guid.NewGuid().ToString("N"));

    public PipelineTraceWriterTests()
        => Environment.SetEnvironmentVariable(TelemetryPathResolver.TelemetryDirEnvVar, _tempRoot);

    [Fact]
    public async Task WriteAsync_schreibt_SnakeCase_Jsonl()
    {
        await PipelineTraceWriter.WriteAsync(new PipelineFrameTrace
        {
            RunId = "run-23",
            TimestampUtc = new DateTimeOffset(2026, 7, 14, 12, 30, 0, TimeSpan.Zero),
            FrameIndex = 7,
            TimeSec = 4.5,
            Meter = 2.25,
            Path = "processed",
            YoloDetectionCount = 2,
            DropReason = null
        });

        var path = PipelineTraceWriter.ResolvePath("run-23");
        Assert.NotNull(path);
        var line = Assert.Single(File.ReadLines(path));
        using var json = JsonDocument.Parse(line);

        Assert.Equal("run-23", json.RootElement.GetProperty("run_id").GetString());
        Assert.Equal(7, json.RootElement.GetProperty("frame_index").GetInt32());
        Assert.Equal(2.25, json.RootElement.GetProperty("meter").GetDouble());
        Assert.False(json.RootElement.TryGetProperty("drop_reason", out _));
    }

    [Fact]
    public async Task WriteSummaryAsync_schreibt_Atomare_Zusammenfassung()
    {
        var phase = new PhaseStat(1, 1, 1, 1);
        var summary = new TelemetrySummary(3, 1, phase, phase, phase, phase, phase, phase, 42);

        await PipelineTraceWriter.WriteSummaryAsync("run-summary", summary);

        var path = PipelineTraceWriter.ResolveSummaryPath("run-summary");
        Assert.NotNull(path);
        using var json = JsonDocument.Parse(await File.ReadAllTextAsync(path));
        Assert.Equal(3, json.RootElement.GetProperty("total_frames").GetInt32());
        Assert.Equal(42, json.RootElement.GetProperty("wall_clock_ms").GetInt64());
        Assert.Empty(Directory.EnumerateFiles(Path.GetDirectoryName(path)!, "*.tmp"));
    }

    [Fact]
    public void ResolvePath_ersetzt_ungueltige_Dateizeichen()
    {
        var path = PipelineTraceWriter.ResolvePath("run:23/teil");

        Assert.NotNull(path);
        var fileName = Path.GetFileName(path);
        Assert.StartsWith("pipeline_trace_", fileName, StringComparison.Ordinal);
        Assert.DoesNotContain(':', fileName);
        Assert.DoesNotContain('/', fileName);
    }

    [Fact]
    public async Task WriteAsync_bewahrt_den_bisherigen_Nullfehler()
    {
        await Assert.ThrowsAsync<NullReferenceException>(() =>
            PipelineTraceWriter.WriteAsync(null!));
    }

    [Fact]
    public async Task WriteAsync_laesst_Telemetriefehler_nicht_in_den_Hauptablauf_durch()
    {
        var exception = await Record.ExceptionAsync(() =>
            PipelineTraceWriteGuard.WriteAsync(
                new ThrowingPipelineTraceWriter(),
                new PipelineTraceEntry { RunId = "throwing-run" }));

        Assert.Null(exception);
    }

    [Fact]
    public async Task WriteAsync_leitet_alle_Felder_an_den_injizierten_Dienst_weiter()
    {
        var writer = new RecordingPipelineTraceWriter();
        var source = new PipelineFrameTrace
        {
            RunId = "mapping-run",
            TimestampUtc = new DateTimeOffset(2026, 7, 14, 15, 0, 0, TimeSpan.Zero),
            FrameIndex = 9,
            TimeSec = 12.5,
            Meter = 8.75,
            Path = "dino_error",
            YoloBypass = true,
            YoloRelevant = false,
            YoloDetectionCount = 3,
            DinoBoxCount = 2,
            SamMaskCount = 1,
            FindingsBuilt = 4,
            CodesFromLabel = 2,
            ClassifierCode = "BAB",
            ClassifierConfidence = 0.91,
            ClassifierSource = "Test",
            ClassifierModel = "model@123",
            ClassifierVoteConfirmed = true,
            QwenCalled = true,
            QwenImageQuality = "good",
            QwenRawFindingCount = 5,
            CodesAfterQwen = 4,
            FindingsEndOfFrame = 3,
            ActiveCount = 2,
            DetectionsTotal = 7,
            DropReason = "dino_error",
            Degraded = true,
            DegradedReason = "test_degraded"
        };

        await PipelineTraceWriteGuard.WriteAsync(
            writer,
            PipelineTraceEntryMapper.Map(source));

        var mapped = Assert.Single(writer.Entries);
        foreach (var sourceProperty in typeof(PipelineFrameTrace).GetProperties())
        {
            var targetProperty = typeof(PipelineTraceEntry).GetProperty(sourceProperty.Name);
            Assert.NotNull(targetProperty);
            Assert.Equal(sourceProperty.GetValue(source), targetProperty.GetValue(mapped));
        }
    }

    [Fact]
    public async Task FileWriter_serialisiert_parallele_Trace_Eintraege()
    {
        const int count = 32;
        var writer = new PipelineTraceFileWriter();
        var runId = "parallel-run";

        await Task.WhenAll(Enumerable.Range(1, count).Select(frameIndex =>
            writer.WriteAsync(new PipelineTraceEntry
            {
                RunId = runId,
                FrameIndex = frameIndex,
                Path = "processed"
            })));

        var path = writer.ResolvePath(runId);
        Assert.NotNull(path);
        var lines = await File.ReadAllLinesAsync(path);
        Assert.Equal(count, lines.Length);
        Assert.Equal(
            Enumerable.Range(1, count).Order(),
            lines.Select(line => JsonDocument.Parse(line).RootElement
                .GetProperty("frame_index").GetInt32()).Order());
    }

    public void Dispose()
    {
        Environment.SetEnvironmentVariable(
            TelemetryPathResolver.TelemetryDirEnvVar,
            _previousTelemetryRoot);
        try
        {
            if (Directory.Exists(_tempRoot))
                Directory.Delete(_tempRoot, recursive: true);
        }
        catch
        {
            // Test-Aufraeumen ist Best-Effort.
        }
    }

    private sealed class RecordingPipelineTraceWriter : IPipelineTraceWriter
    {
        public List<PipelineTraceEntry> Entries { get; } = [];

        public Task WriteAsync(PipelineTraceEntry entry)
        {
            Entries.Add(entry);
            return Task.CompletedTask;
        }

        public Task WriteSummaryAsync(string runId, TelemetrySummary summary)
            => Task.CompletedTask;

        public string? ResolvePath(string runId) => null;

        public string? ResolveSummaryPath(string runId) => null;
    }

    private sealed class ThrowingPipelineTraceWriter : IPipelineTraceWriter
    {
        public Task WriteAsync(PipelineTraceEntry entry)
            => throw new InvalidOperationException("Testfehler");

        public Task WriteSummaryAsync(string runId, TelemetrySummary summary)
            => throw new InvalidOperationException("Testfehler");

        public string? ResolvePath(string runId)
            => throw new InvalidOperationException("Testfehler");

        public string? ResolveSummaryPath(string runId)
            => throw new InvalidOperationException("Testfehler");
    }
}
