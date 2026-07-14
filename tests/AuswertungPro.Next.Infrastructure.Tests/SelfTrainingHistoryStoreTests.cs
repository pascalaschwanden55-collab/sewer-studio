using AuswertungPro.Next.Application.Ai.Training;
using AuswertungPro.Next.Infrastructure.Ai.KnowledgeBase;
using AuswertungPro.Next.Infrastructure.Ai.Training;

namespace AuswertungPro.Next.Infrastructure.Tests;

[Collection("EnvironmentVars")]
public sealed class SelfTrainingHistoryStoreTests
{
    [Fact]
    public async Task AppendRunAsync_KeepsNewestTwentyRuns()
    {
        await WithTempKnowledgeRoot(async _ =>
        {
            for (var index = 0; index < 25; index++)
                await SelfTrainingHistoryStore.AppendRunAsync(CreateSnapshot(index));

            var runs = await SelfTrainingHistoryStore.LoadAsync();

            Assert.Equal(20, runs.Count);
            Assert.Equal("case-5", runs[0].CaseId);
            Assert.Equal("case-24", runs[^1].CaseId);
        });
    }

    [Fact]
    public async Task LoadAsync_CorruptFileReturnsEmptyAndKeepsForensicCopy()
    {
        await WithTempKnowledgeRoot(async path =>
        {
            await File.WriteAllTextAsync(path, "{ keine gueltige JSON-Datei");

            var runs = await SelfTrainingHistoryStore.LoadAsync();

            Assert.Empty(runs);
            Assert.True(File.Exists(path));
            Assert.Single(Directory.EnumerateFiles(
                Path.GetDirectoryName(path)!,
                "selftraining_history.json.corrupt_*"));
        });
    }

    [Fact]
    public async Task AppendRunAsync_SecondWriteKeepsBackupAndNoTempFile()
    {
        await WithTempKnowledgeRoot(async path =>
        {
            await SelfTrainingHistoryStore.AppendRunAsync(CreateSnapshot(1));
            await SelfTrainingHistoryStore.AppendRunAsync(CreateSnapshot(2));

            Assert.True(File.Exists(path + ".bak"));
            Assert.False(File.Exists(path + ".tmp"));
        });
    }

    [Fact]
    public async Task FileStore_ParallelAppendsDoNotLoseRuns()
    {
        await WithTempKnowledgeRoot(async path =>
        {
            var store = new SelfTrainingHistoryFileStore(path);
            await Task.WhenAll(Enumerable.Range(0, 12).Select(index =>
                store.AppendRunAsync(CreateSnapshot(index))));

            var runs = await store.LoadAsync();

            Assert.Equal(12, runs.Count);
            Assert.Equal(12, runs.Select(run => run.CaseId).Distinct().Count());
        });
    }

    private static SelfTrainingRunSnapshot CreateSnapshot(int index) =>
        new(
            TimestampUtc: DateTime.UnixEpoch.AddMinutes(index),
            CaseId: $"case-{index}",
            TotalEntries: index,
            ExactPercent: index,
            PartialPercent: 0,
            MismatchPercent: 0,
            NoFindingsPercent: 0);

    private static async Task WithTempKnowledgeRoot(Func<string, Task> body)
    {
        var previous = Environment.GetEnvironmentVariable(KnowledgeBasePaths.EnvironmentVariableName);
        var root = Path.Combine(
            Path.GetTempPath(),
            "sewer-selftraining-history-tests",
            Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        Environment.SetEnvironmentVariable(KnowledgeBasePaths.EnvironmentVariableName, root);
        KnowledgeBasePaths.ConfigureSettingsRoot(null);
        KnowledgeBasePaths.InvalidateCache();

        try
        {
            await body(Path.Combine(KnowledgeBasePaths.GetRoot(), "selftraining_history.json"));
        }
        finally
        {
            Environment.SetEnvironmentVariable(KnowledgeBasePaths.EnvironmentVariableName, previous);
            KnowledgeBasePaths.ConfigureSettingsRoot(null);
            KnowledgeBasePaths.InvalidateCache();
            try { Directory.Delete(root, recursive: true); } catch { }
        }
    }
}
