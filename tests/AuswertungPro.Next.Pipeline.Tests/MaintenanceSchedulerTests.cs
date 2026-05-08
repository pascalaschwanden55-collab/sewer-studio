using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using AuswertungPro.Next.Application.Ai.Training;
using AuswertungPro.Next.Application.Maintenance;
using Xunit;

namespace AuswertungPro.Next.Pipeline.Tests;

/// <summary>
/// Tests fuer MaintenanceScheduler (Sprint 1: Nightly-Cleanup).
/// </summary>
[Trait("Category", "Integration")]
public class MaintenanceSchedulerTests : IDisposable
{
    private readonly string _tempDir;
    private readonly string _statePath;

    public MaintenanceSchedulerTests()
    {
        _tempDir = Path.Combine(Path.GetTempPath(), $"sewerstudio_maint_{Guid.NewGuid():N}");
        Directory.CreateDirectory(_tempDir);
        _statePath = Path.Combine(_tempDir, "maintenance.json");
    }

    public void Dispose()
    {
        try { Directory.Delete(_tempDir, recursive: true); } catch { }
    }

    private static FrameStoreCleanupResult OkFrameResult(int deleted = 5, long bytes = 5000)
        => new(
            FramesDir: "test",
            TotalFiles: 100,
            ActiveSampleIds: 95,
            OrphanFiles: deleted,
            OrphanBytes: bytes,
            DeletedFiles: deleted,
            DeletedBytes: bytes,
            DryRun: false,
            Errors: Array.Empty<string>());

    [Fact]
    public async Task RunIfDueAsync_FirstRun_BothJobsExecute()
    {
        var frameCalls = 0;
        var pruneCalls = 0;

        var scheduler = new MaintenanceScheduler(
            runFrameCleanup: _ => { frameCalls++; return Task.FromResult(OkFrameResult()); },
            runVersionsPrune: _ => { pruneCalls++; return Task.FromResult(7); },
            getStateFilePath: () => _statePath);

        var result = await scheduler.RunIfDueAsync();

        Assert.True(result.RanFrameCleanup);
        Assert.True(result.RanVersionsPrune);
        Assert.Equal(1, frameCalls);
        Assert.Equal(1, pruneCalls);
        Assert.Equal(5, result.FramesDeleted);
        Assert.Equal(7, result.VersionsPruned);
        Assert.False(result.HasErrors);
    }

    [Fact]
    public async Task RunIfDueAsync_RecentlyRun_NoExecution()
    {
        // Vorher beide Stages mit Lauf von "vor 1 Stunde" markieren
        var scheduler1 = new MaintenanceScheduler(
            runFrameCleanup: _ => Task.FromResult(OkFrameResult()),
            runVersionsPrune: _ => Task.FromResult(0),
            getStateFilePath: () => _statePath);
        scheduler1.SaveState(new MaintenanceState
        {
            LastFrameCleanupUtc = DateTime.UtcNow.AddHours(-1),
            LastVersionsPruneUtc = DateTime.UtcNow.AddHours(-1),
        });

        var frameCalls = 0;
        var pruneCalls = 0;
        var scheduler2 = new MaintenanceScheduler(
            runFrameCleanup: _ => { frameCalls++; return Task.FromResult(OkFrameResult()); },
            runVersionsPrune: _ => { pruneCalls++; return Task.FromResult(0); },
            getStateFilePath: () => _statePath);

        var result = await scheduler2.RunIfDueAsync();

        Assert.False(result.RanFrameCleanup);
        Assert.False(result.RanVersionsPrune);
        Assert.Equal(0, frameCalls);
        Assert.Equal(0, pruneCalls);
    }

    [Fact]
    public async Task RunIfDueAsync_OldRun_TriggersAfterMinInterval()
    {
        // Lauf von vor 25 Stunden — Default MinIntervalHours = 20 → faellig
        var scheduler1 = new MaintenanceScheduler(
            runFrameCleanup: _ => Task.FromResult(OkFrameResult()),
            runVersionsPrune: _ => Task.FromResult(0),
            getStateFilePath: () => _statePath);
        scheduler1.SaveState(new MaintenanceState
        {
            LastFrameCleanupUtc = DateTime.UtcNow.AddHours(-25),
            LastVersionsPruneUtc = DateTime.UtcNow.AddHours(-25),
        });

        var frameCalls = 0;
        var pruneCalls = 0;
        var scheduler2 = new MaintenanceScheduler(
            runFrameCleanup: _ => { frameCalls++; return Task.FromResult(OkFrameResult()); },
            runVersionsPrune: _ => { pruneCalls++; return Task.FromResult(3); },
            getStateFilePath: () => _statePath);

        var result = await scheduler2.RunIfDueAsync();

        Assert.True(result.RanFrameCleanup);
        Assert.True(result.RanVersionsPrune);
        Assert.Equal(1, frameCalls);
        Assert.Equal(1, pruneCalls);
    }

    [Fact]
    public async Task RunIfDueAsync_FrameHookThrows_StateNotUpdatedForFrame_PruneStillRuns()
    {
        var scheduler = new MaintenanceScheduler(
            runFrameCleanup: _ => throw new InvalidOperationException("disk full"),
            runVersionsPrune: _ => Task.FromResult(2),
            getStateFilePath: () => _statePath);

        var result = await scheduler.RunIfDueAsync();

        Assert.True(result.RanFrameCleanup); // wurde versucht
        Assert.True(result.RanVersionsPrune);
        Assert.True(result.HasErrors);
        Assert.Contains(result.Errors, e => e.Contains("FrameCleanup") && e.Contains("disk full"));

        // State: Prune wurde aktualisiert, FrameCleanup nicht (damit naechster Start es nochmal versucht)
        var state = scheduler.LoadState();
        Assert.Null(state.LastFrameCleanupUtc);
        Assert.NotNull(state.LastVersionsPruneUtc);
    }

    [Fact]
    public async Task RunIfDueAsync_OperationCanceled_PropagatesNotSwallowed()
    {
        var scheduler = new MaintenanceScheduler(
            runFrameCleanup: _ => throw new OperationCanceledException(),
            runVersionsPrune: _ => Task.FromResult(0),
            getStateFilePath: () => _statePath);

        await Assert.ThrowsAsync<OperationCanceledException>(() => scheduler.RunIfDueAsync());

        // Nichts persistiert
        var state = scheduler.LoadState();
        Assert.Null(state.LastFrameCleanupUtc);
    }

    [Fact]
    public async Task RunIfDueAsync_FrameCleanupHasErrors_StateStillUpdated()
    {
        // Fail-closed-Result vom FrameCleanup ist KEIN Hook-Fehler — Lauf gilt als "stattgefunden"
        var failClosedResult = new FrameStoreCleanupResult(
            FramesDir: "test",
            TotalFiles: 100,
            ActiveSampleIds: 0,
            OrphanFiles: 0,
            OrphanBytes: 0,
            DeletedFiles: 0,
            DeletedBytes: 0,
            DryRun: true,
            Errors: new[] { "Fail-closed: Sample-Liste leer" });

        var scheduler = new MaintenanceScheduler(
            runFrameCleanup: _ => Task.FromResult(failClosedResult),
            runVersionsPrune: _ => Task.FromResult(0),
            getStateFilePath: () => _statePath);

        var result = await scheduler.RunIfDueAsync();

        Assert.True(result.RanFrameCleanup);
        Assert.True(result.HasErrors);
        Assert.Contains(result.Errors, e => e.Contains("Fail-closed"));

        // State wurde aktualisiert (kein Hook-Fehler) — naechster Lauf erst nach MinInterval
        var state = scheduler.LoadState();
        Assert.NotNull(state.LastFrameCleanupUtc);
    }

    [Fact]
    public async Task RunIfDueAsync_KeepsCumulativeCounters()
    {
        var scheduler = new MaintenanceScheduler(
            runFrameCleanup: _ => Task.FromResult(OkFrameResult(deleted: 10)),
            runVersionsPrune: _ => Task.FromResult(5),
            getStateFilePath: () => _statePath);

        await scheduler.RunIfDueAsync();

        // State zuruecksetzen damit faellig
        var state1 = scheduler.LoadState();
        Assert.Equal(10, state1.TotalFramesDeleted);
        Assert.Equal(5, state1.TotalVersionsPruned);

        // Zweiten Lauf erzwingen
        scheduler.SaveState(state1 with
        {
            LastFrameCleanupUtc = DateTime.UtcNow.AddHours(-25),
            LastVersionsPruneUtc = DateTime.UtcNow.AddHours(-25),
        });

        await scheduler.RunIfDueAsync();
        var state2 = scheduler.LoadState();

        Assert.Equal(20, state2.TotalFramesDeleted); // 10 + 10
        Assert.Equal(10, state2.TotalVersionsPruned); // 5 + 5
    }

    [Fact]
    public void LoadState_CorruptFile_ReturnsDefault()
    {
        File.WriteAllText(_statePath, "{ this is not valid json");

        var scheduler = new MaintenanceScheduler(
            runFrameCleanup: _ => Task.FromResult(OkFrameResult()),
            runVersionsPrune: _ => Task.FromResult(0),
            getStateFilePath: () => _statePath);

        var state = scheduler.LoadState();
        Assert.Null(state.LastFrameCleanupUtc);
        Assert.Null(state.LastVersionsPruneUtc);
        Assert.Equal(0, state.TotalFramesDeleted);
    }
}
