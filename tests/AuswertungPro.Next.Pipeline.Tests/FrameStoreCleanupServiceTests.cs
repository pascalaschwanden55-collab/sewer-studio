using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using AuswertungPro.Next.Application.Ai;
using AuswertungPro.Next.Application.Ai.Training;
using Xunit;

namespace AuswertungPro.Next.Pipeline.Tests;

/// <summary>
/// Tests fuer FrameStoreCleanupService (Audit Top-10: frames/-Cleanup-Job).
/// Identifiziert verwaiste PNGs unter C:\KI_BRAIN\frames die zu keinem
/// TrainingSample mehr gehoeren.
/// </summary>
[Trait("Category", "Integration")]
public class FrameStoreCleanupServiceTests
{
    [Fact]
    public async Task RunAsync_NoFramesDir_ReturnsEmptyResult()
    {
        // Setup: KnowledgeRoot zeigt auf leeren Temp-Pfad
        using var ctx = new TempKnowledgeRoot();

        var svc = new FrameStoreCleanupService(
            loadActiveSampleIds: () => Task.FromResult<IReadOnlyCollection<string>>(System.Array.Empty<string>()));

        var result = await svc.RunAsync();

        Assert.Equal(0, result.TotalFiles);
        Assert.Equal(0, result.OrphanFiles);
        Assert.True(result.DryRun);
    }

    [Fact]
    public async Task RunAsync_DryRunDefault_ReportsButDoesNotDelete()
    {
        using var ctx = new TempKnowledgeRoot();
        var framesDir = Path.Combine(ctx.Root, "frames");
        Directory.CreateDirectory(framesDir);

        // Erstelle 3 PNGs mit Namen die KEINEM aktiven Sample entsprechen
        var orphan1 = Path.Combine(framesDir, "orphan-1.png");
        var orphan2 = Path.Combine(framesDir, "orphan-2.png");
        var active = Path.Combine(framesDir, "active-1.png");

        File.WriteAllBytes(orphan1, new byte[] { 0x89, 0x50, 0x4E, 0x47 }); // PNG magic
        File.WriteAllBytes(orphan2, new byte[] { 0x89, 0x50, 0x4E, 0x47 });
        File.WriteAllBytes(active, new byte[] { 0x89, 0x50, 0x4E, 0x47 });

        // Make them old enough (cutoff is 7 days by default)
        var oldDate = DateTime.UtcNow.AddDays(-30);
        File.SetLastWriteTimeUtc(orphan1, oldDate);
        File.SetLastWriteTimeUtc(orphan2, oldDate);
        File.SetLastWriteTimeUtc(active, oldDate);

        var svc = new FrameStoreCleanupService(
            loadActiveSampleIds: () => Task.FromResult<IReadOnlyCollection<string>>(
                new HashSet<string> { "active-1" }));
        // Default: DryRun=true

        var result = await svc.RunAsync();

        Assert.Equal(3, result.TotalFiles);
        Assert.Equal(2, result.OrphanFiles); // orphan-1 + orphan-2
        Assert.Equal(0, result.DeletedFiles); // DryRun
        Assert.True(result.DryRun);

        // Files muessen alle noch existieren
        Assert.True(File.Exists(orphan1));
        Assert.True(File.Exists(orphan2));
        Assert.True(File.Exists(active));
    }

    [Fact]
    public async Task RunAsync_DryRunFalse_DeletesOrphans()
    {
        using var ctx = new TempKnowledgeRoot();
        var framesDir = Path.Combine(ctx.Root, "frames");
        Directory.CreateDirectory(framesDir);

        var orphan = Path.Combine(framesDir, "orphan.png");
        var active = Path.Combine(framesDir, "active.png");
        File.WriteAllBytes(orphan, new byte[] { 0x89, 0x50, 0x4E, 0x47 });
        File.WriteAllBytes(active, new byte[] { 0x89, 0x50, 0x4E, 0x47 });

        var oldDate = DateTime.UtcNow.AddDays(-30);
        File.SetLastWriteTimeUtc(orphan, oldDate);
        File.SetLastWriteTimeUtc(active, oldDate);

        var svc = new FrameStoreCleanupService(
            loadActiveSampleIds: () => Task.FromResult<IReadOnlyCollection<string>>(
                new HashSet<string> { "active" }))
        {
            DryRun = false
        };

        var result = await svc.RunAsync();

        Assert.Equal(1, result.OrphanFiles);
        Assert.Equal(1, result.DeletedFiles);
        Assert.False(result.DryRun);

        // Orphan ist weg, Active noch da
        Assert.False(File.Exists(orphan));
        Assert.True(File.Exists(active));
    }

    [Fact]
    public async Task RunAsync_RecentlyCreatedFile_IsSpared()
    {
        using var ctx = new TempKnowledgeRoot();
        var framesDir = Path.Combine(ctx.Root, "frames");
        Directory.CreateDirectory(framesDir);

        var recentOrphan = Path.Combine(framesDir, "recent-orphan.png");
        File.WriteAllBytes(recentOrphan, new byte[] { 0x89, 0x50, 0x4E, 0x47 });
        // Datei ist juenger als MinimumAgeDays (Default 7)
        File.SetLastWriteTimeUtc(recentOrphan, DateTime.UtcNow.AddHours(-1));

        var svc = new FrameStoreCleanupService(
            loadActiveSampleIds: () => Task.FromResult<IReadOnlyCollection<string>>(
                new HashSet<string>()))
        {
            DryRun = false
        };

        var result = await svc.RunAsync();

        Assert.Equal(1, result.TotalFiles);
        Assert.Equal(0, result.OrphanFiles); // Verschont weil zu jung
        Assert.True(File.Exists(recentOrphan));
    }

    [Fact]
    public async Task RunAsync_CustomMinAgeDays_Honored()
    {
        using var ctx = new TempKnowledgeRoot();
        var framesDir = Path.Combine(ctx.Root, "frames");
        Directory.CreateDirectory(framesDir);

        var orphan = Path.Combine(framesDir, "orphan.png");
        File.WriteAllBytes(orphan, new byte[] { 0x89, 0x50, 0x4E, 0x47 });
        // 2 Tage alt
        File.SetLastWriteTimeUtc(orphan, DateTime.UtcNow.AddDays(-2));

        var svc = new FrameStoreCleanupService(
            loadActiveSampleIds: () => Task.FromResult<IReadOnlyCollection<string>>(
                new HashSet<string>()))
        {
            DryRun = false,
            MinimumAgeDays = 1, // 1 Tag → 2-Tage-alte Datei wird Orphan
        };

        var result = await svc.RunAsync();

        Assert.Equal(1, result.OrphanFiles);
        Assert.Equal(1, result.DeletedFiles);
    }

    [Fact]
    public async Task RunAsync_LoadActiveIdsThrows_FailsClosed()
    {
        // Setup: Loader wirft Exception (z.B. KB-Lock, korrupte JSON).
        using var ctx = new TempKnowledgeRoot();
        var framesDir = Path.Combine(ctx.Root, "frames");
        Directory.CreateDirectory(framesDir);

        // 50 alte PNGs wuerden ohne Schutz alle als Orphan eingestuft
        var oldDate = DateTime.UtcNow.AddDays(-30);
        for (int i = 0; i < 50; i++)
        {
            var p = Path.Combine(framesDir, $"frame-{i}.png");
            File.WriteAllBytes(p, new byte[] { 0x89, 0x50, 0x4E, 0x47 });
            File.SetLastWriteTimeUtc(p, oldDate);
        }

        var svc = new FrameStoreCleanupService(
            loadActiveSampleIds: () => throw new InvalidOperationException("KB locked"))
        {
            DryRun = false // selbst mit DryRun=false darf nichts geloescht werden
        };

        var result = await svc.RunAsync();

        Assert.Equal(0, result.DeletedFiles);
        Assert.True(result.DryRun, "Fail-closed muss DryRun erzwingen");
        Assert.NotEmpty(result.Errors);
        Assert.Contains("Fail-closed", result.Errors[0]);

        // Files muessen alle noch existieren
        Assert.Equal(50, Directory.GetFiles(framesDir, "*.png").Length);
    }

    [Fact]
    public async Task RunAsync_EmptyActiveIdsManyFiles_FailsClosed()
    {
        // Setup: activeIds=[] und mehr Files als FailClosedThreshold (Default 10).
        using var ctx = new TempKnowledgeRoot();
        var framesDir = Path.Combine(ctx.Root, "frames");
        Directory.CreateDirectory(framesDir);

        var oldDate = DateTime.UtcNow.AddDays(-30);
        for (int i = 0; i < 20; i++)
        {
            var p = Path.Combine(framesDir, $"frame-{i}.png");
            File.WriteAllBytes(p, new byte[] { 0x89, 0x50, 0x4E, 0x47 });
            File.SetLastWriteTimeUtc(p, oldDate);
        }

        var svc = new FrameStoreCleanupService(
            loadActiveSampleIds: () => Task.FromResult<IReadOnlyCollection<string>>(System.Array.Empty<string>()))
        {
            DryRun = false
        };

        var result = await svc.RunAsync();

        Assert.Equal(20, result.TotalFiles);
        Assert.Equal(0, result.OrphanFiles);
        Assert.Equal(0, result.DeletedFiles);
        Assert.True(result.DryRun, "Fail-closed muss DryRun erzwingen");
        Assert.NotEmpty(result.Errors);
        Assert.Contains("Fail-closed", result.Errors[0]);

        // Alle 20 Files muessen noch da sein
        Assert.Equal(20, Directory.GetFiles(framesDir, "*.png").Length);
    }

    [Fact]
    public async Task RunAsync_EmptyActiveIdsButFewFiles_ProceedsBelowThreshold()
    {
        // Test-Edge-Case: leere Liste, aber wenige Files (unter Default-Schwelle 10)
        // — Cleanup soll laufen (z.B. nach Wipe-and-Restart-Szenario).
        using var ctx = new TempKnowledgeRoot();
        var framesDir = Path.Combine(ctx.Root, "frames");
        Directory.CreateDirectory(framesDir);

        var oldDate = DateTime.UtcNow.AddDays(-30);
        for (int i = 0; i < 3; i++)
        {
            var p = Path.Combine(framesDir, $"orphan-{i}.png");
            File.WriteAllBytes(p, new byte[] { 0x89, 0x50, 0x4E, 0x47 });
            File.SetLastWriteTimeUtc(p, oldDate);
        }

        var svc = new FrameStoreCleanupService(
            loadActiveSampleIds: () => Task.FromResult<IReadOnlyCollection<string>>(System.Array.Empty<string>()))
        {
            DryRun = false
        };

        var result = await svc.RunAsync();

        Assert.Equal(3, result.OrphanFiles);
        Assert.Equal(3, result.DeletedFiles);
        Assert.False(result.DryRun);
        Assert.Empty(result.Errors);
    }

    /// <summary>RAII: setzt KnowledgeRootProvider auf einen Temp-Pfad und raeumt am Ende auf.</summary>
    private sealed class TempKnowledgeRoot : IDisposable
    {
        public string Root { get; }

        public TempKnowledgeRoot()
        {
            Root = Path.Combine(Path.GetTempPath(), $"sewerstudio_frametest_{Guid.NewGuid():N}");
            Directory.CreateDirectory(Root);
            KnowledgeRootProvider.SetResolver(() => Root);
        }

        public void Dispose()
        {
            try { Directory.Delete(Root, recursive: true); } catch { }
        }
    }
}
