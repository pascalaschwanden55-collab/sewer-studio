using AuswertungPro.Next.Application.Ai.Sanierung;
using AuswertungPro.Next.Application.Common;
using AuswertungPro.Next.Infrastructure.Ai.Sanierung;

namespace AuswertungPro.Next.Infrastructure.Tests;

[Collection("EnvironmentVars")]
public sealed class AiOptimizationSessionStoreTests
{
    [Fact]
    public async Task Store_PreservesReplacementFilterBackupAndCorruptFallback()
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

            await File.WriteAllTextAsync(path, "{ keine gueltige JSON-Datei");
            Assert.Empty(await AiOptimizationSessionStore.LoadAllAsync());
        }
        finally
        {
            Environment.SetEnvironmentVariable(AppDataPathResolver.AppDataDirEnvVar, previous);
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
