using AuswertungPro.Next.Application.Ai.Sanierung;
using AuswertungPro.Next.Application.Common;
using AuswertungPro.Next.Infrastructure.Ai.Sanierung;

namespace AuswertungPro.Next.Infrastructure.Tests;

[Collection("EnvironmentVars")]
public sealed class AiOptimizationSessionStoreTests
{
    [Fact]
    public async Task Store_PreservesReplacementFilterAndBackup()
    {
        var previous = Environment.GetEnvironmentVariable(AppDataPathResolver.AppDataDirEnvVar);
        var root = Path.Combine(
            Path.GetTempPath(),
            "sewer-ai-optimization-session-tests",
            Guid.NewGuid().ToString("N"));
        Environment.SetEnvironmentVariable(AppDataPathResolver.AppDataDirEnvVar, root);
        var path = Path.Combine(root, "ai_sanierung_sessions.json");

        try
        {
            var id = Guid.NewGuid();
            await AiOptimizationSessionStore.SaveAsync(new AiOptimizationSession
            {
                Id = id,
                HaltungId = "H-001",
                FinalAppliedMeasure = "Alt"
            });
            await AiOptimizationSessionStore.SaveAsync(new AiOptimizationSession
            {
                Id = id,
                HaltungId = "H-001",
                FinalAppliedMeasure = "Neu"
            });
            await AiOptimizationSessionStore.SaveAsync(new AiOptimizationSession
            {
                HaltungId = "H-002"
            });

            var all = await AiOptimizationSessionStore.LoadAllAsync();
            var holding = await AiOptimizationSessionStore.LoadForHaltungAsync("h-001");

            Assert.Equal(2, all.Count);
            Assert.Equal("Neu", Assert.Single(holding).FinalAppliedMeasure);
            Assert.True(File.Exists(path + ".bak"));
            Assert.Empty(Directory.EnumerateFiles(root, "*.tmp"));
        }
        finally
        {
            Environment.SetEnvironmentVariable(AppDataPathResolver.AppDataDirEnvVar, previous);
            try { Directory.Delete(root, recursive: true); } catch { }
        }
    }

    [Theory]
    [InlineData("{ keine gueltige JSON-Datei")]
    [InlineData("null")]
    public async Task SaveAsync_CorruptExistingFile_FailsClosedAndKeepsOriginal(string corruptJson)
    {
        var root = Path.Combine(
            Path.GetTempPath(),
            "sewer-ai-optimization-session-corrupt-tests",
            Guid.NewGuid().ToString("N"));
        var path = Path.Combine(root, "sessions.json");
        Directory.CreateDirectory(root);
        await File.WriteAllTextAsync(path, corruptJson);
        var store = new AiOptimizationSessionFileStore(path);

        try
        {
            var error = await Assert.ThrowsAsync<InvalidOperationException>(() =>
                store.SaveAsync(new AiOptimizationSession { HaltungId = "H-001" }));

            Assert.Contains("NICHT veraendert", error.Message, StringComparison.Ordinal);
            Assert.Equal(corruptJson, await File.ReadAllTextAsync(path));
        }
        finally
        {
            try { Directory.Delete(root, recursive: true); } catch { }
        }
    }

    [Fact]
    public async Task SaveAsync_LockedExistingFile_FailsClosedAndKeepsOriginal()
    {
        var root = Path.Combine(
            Path.GetTempPath(),
            "sewer-ai-optimization-session-lock-tests",
            Guid.NewGuid().ToString("N"));
        var path = Path.Combine(root, "sessions.json");
        var store = new AiOptimizationSessionFileStore(path);

        try
        {
            await store.SaveAsync(new AiOptimizationSession { HaltungId = "H-001" });
            var original = await File.ReadAllBytesAsync(path);

            using (new FileStream(path, FileMode.Open, FileAccess.ReadWrite, FileShare.None))
            {
                await Assert.ThrowsAsync<InvalidOperationException>(() =>
                    store.SaveAsync(new AiOptimizationSession { HaltungId = "H-002" }));
            }

            Assert.Equal(original, await File.ReadAllBytesAsync(path));
        }
        finally
        {
            try { Directory.Delete(root, recursive: true); } catch { }
        }
    }

    [Fact]
    public async Task FileStore_ParallelSavesDoNotLoseSessions()
    {
        var root = Path.Combine(
            Path.GetTempPath(),
            "sewer-ai-optimization-session-file-store-tests",
            Guid.NewGuid().ToString("N"));
        var path = Path.Combine(root, "sessions.json");
        var store = new AiOptimizationSessionFileStore(path);

        try
        {
            await Task.WhenAll(Enumerable.Range(0, 16).Select(index =>
                store.SaveAsync(new AiOptimizationSession
                {
                    HaltungId = $"H-{index}"
                })));

            var sessions = await store.LoadAllAsync();

            Assert.Equal(16, sessions.Count);
            Assert.Equal(16, sessions.Select(item => item.HaltungId).Distinct().Count());
        }
        finally
        {
            try { Directory.Delete(root, recursive: true); } catch { }
        }
    }
}
