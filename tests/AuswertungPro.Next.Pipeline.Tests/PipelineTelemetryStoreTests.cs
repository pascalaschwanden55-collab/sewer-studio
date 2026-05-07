using System;
using System.IO;
using System.Threading.Tasks;
using AuswertungPro.Next.Application.Ai.Pipeline;
using AuswertungPro.Next.Application.Ai.Vision;
using AuswertungPro.Next.Infrastructure.Ai.Pipeline;
using Xunit;

namespace AuswertungPro.Next.Pipeline.Tests;

/// <summary>Tests fuer PipelineTelemetryStore (Sprint 2: SQLite-Persistierung).</summary>
public class PipelineTelemetryStoreTests : IDisposable
{
    private readonly string _tempDir;
    private readonly string _dbPath;

    public PipelineTelemetryStoreTests()
    {
        _tempDir = Path.Combine(Path.GetTempPath(), $"telemetry_{Guid.NewGuid():N}");
        Directory.CreateDirectory(_tempDir);
        _dbPath = Path.Combine(_tempDir, "telemetry.db");
    }

    public void Dispose()
    {
        try { Directory.Delete(_tempDir, recursive: true); } catch { }
    }

    private static TelemetrySummary BuildSummary(
        int totalFrames = 10,
        int skippedFrames = 0,
        long wallClockMs = 5000,
        double yoloMean = 25.5,
        double yoloP95 = 50.0)
    {
        return new TelemetrySummary(
            TotalFrames: totalFrames,
            SkippedFrames: skippedFrames,
            Extraction: new PhaseStat(15.0, 14.0, 18.0, 150),
            Yolo: new PhaseStat(yoloMean, 24.0, yoloP95, (long)(yoloMean * totalFrames)),
            Dino: new PhaseStat(0, 0, 0, 0),
            Sam: new PhaseStat(35.0, 32.0, 60.0, 350),
            Qwen: new PhaseStat(2500.0, 2400.0, 3000.0, 25000),
            Total: new PhaseStat(2575.0, 2470.0, 3128.0, 25750),
            WallClockMs: wallClockMs);
    }

    [Fact]
    public async Task SaveAndRead_RoundTrip_AllFieldsPreserved()
    {
        using var store = new PipelineTelemetryStore(_dbPath);
        var summary = BuildSummary(totalFrames: 42, yoloMean: 27.3, yoloP95: 55.5);

        await store.SaveRunAsync("test-run", summary);

        var runs = await store.GetRecentRunsAsync(limit: 10);

        Assert.Single(runs);
        var r = runs[0];
        Assert.Equal("test-run", r.Label);
        Assert.Equal(42, r.TotalFrames);
        Assert.Equal(0, r.SkippedFrames);
        Assert.Equal(5000, r.WallClockMs);
        Assert.Equal(27.3, r.YoloMeanMs, 1);
        Assert.Equal(55.5, r.YoloP95Ms, 1);
        Assert.Equal(2500.0, r.QwenMeanMs, 1);
    }

    [Fact]
    public async Task SaveMultiple_OrderByIdDescending()
    {
        using var store = new PipelineTelemetryStore(_dbPath);

        await store.SaveRunAsync("first", BuildSummary());
        await Task.Delay(20);
        await store.SaveRunAsync("second", BuildSummary());
        await Task.Delay(20);
        await store.SaveRunAsync("third", BuildSummary());

        var runs = await store.GetRecentRunsAsync(limit: 10);

        Assert.Equal(3, runs.Count);
        Assert.Equal("third", runs[0].Label);
        Assert.Equal("second", runs[1].Label);
        Assert.Equal("first", runs[2].Label);
    }

    [Fact]
    public async Task GetRecentRuns_LimitHonored()
    {
        using var store = new PipelineTelemetryStore(_dbPath);

        for (int i = 0; i < 25; i++)
            await store.SaveRunAsync($"run-{i}", BuildSummary());

        var runs = await store.GetRecentRunsAsync(limit: 5);
        Assert.Equal(5, runs.Count);
    }

    [Fact]
    public async Task GetRunsSince_FiltersByTimestamp()
    {
        using var store = new PipelineTelemetryStore(_dbPath);

        var before = DateTime.UtcNow.AddDays(-1);
        await store.SaveRunAsync("recent", BuildSummary());

        var sinceFuture = DateTime.UtcNow.AddDays(1);
        var none = await store.GetRunsSinceAsync(sinceFuture);
        Assert.Empty(none);

        var fromBefore = await store.GetRunsSinceAsync(before);
        Assert.Single(fromBefore);
    }

    [Fact]
    public async Task GetRunCount_ReflectsInsertions()
    {
        using var store = new PipelineTelemetryStore(_dbPath);

        Assert.Equal(0, await store.GetRunCountAsync());

        await store.SaveRunAsync("r1", BuildSummary());
        await store.SaveRunAsync("r2", BuildSummary());
        await store.SaveRunAsync("r3", BuildSummary());

        Assert.Equal(3, await store.GetRunCountAsync());
    }

    [Fact]
    public async Task SchemaCreatedOnFirstUse()
    {
        // 1. Store anlegen, schreiben
        using (var store1 = new PipelineTelemetryStore(_dbPath))
        {
            await store1.SaveRunAsync("first", BuildSummary());
        }

        // 2. Store erneut auf demselben Pfad: Schema bereits da, kein Crash
        using var store2 = new PipelineTelemetryStore(_dbPath);
        var runs = await store2.GetRecentRunsAsync();
        Assert.Single(runs);
    }

    [Fact]
    public async Task PipelineTelemetry_AdditionalPersister_ReceivesSummary()
    {
        var telemetry = new PipelineTelemetry();
        telemetry.RecordFrame(new FrameTiming(
            FrameIndex: 0, TimestampSec: 0, ExtractionMs: 10, YoloMs: 20, DinoMs: 0,
            SamMs: 30, QwenMs: 2000, TotalMs: 2060, Skipped: false));

        TelemetrySummary? captured = null;
        string? capturedLabel = null;
        telemetry.AdditionalPersister = (label, summary, _) =>
        {
            capturedLabel = label;
            captured = summary;
            return Task.CompletedTask;
        };

        var jsonlPath = Path.Combine(_tempDir, "telemetry.jsonl");
        await telemetry.PersistSummaryAsync("hook-test", customPath: jsonlPath);

        Assert.Equal("hook-test", capturedLabel);
        Assert.NotNull(captured);
        Assert.Equal(1, captured!.TotalFrames);
    }

    [Fact]
    public async Task PipelineTelemetry_AdditionalPersisterThrows_JsonlStillWritten()
    {
        var telemetry = new PipelineTelemetry();
        telemetry.RecordFrame(new FrameTiming(
            FrameIndex: 0, TimestampSec: 0, ExtractionMs: 10, YoloMs: 20, DinoMs: 0,
            SamMs: 30, QwenMs: 2000, TotalMs: 2060, Skipped: false));

        telemetry.AdditionalPersister = (_, _, _) => throw new InvalidOperationException("simulated DB lock");

        var jsonlPath = Path.Combine(_tempDir, "telemetry.jsonl");
        await telemetry.PersistSummaryAsync("error-test", customPath: jsonlPath);

        // JSONL muss trotz Persister-Fehler geschrieben sein
        Assert.True(File.Exists(jsonlPath));
        var lines = File.ReadAllLines(jsonlPath);
        Assert.Single(lines);
        Assert.Contains("error-test", lines[0]);
    }
}
