using AuswertungPro.Next.Application.Ai.Training;
using AuswertungPro.Next.Infrastructure.Ai.KnowledgeBase;
using AuswertungPro.Next.Infrastructure.Ai.Training;

namespace AuswertungPro.Next.Infrastructure.Tests;

[Collection("EnvironmentVars")]
public sealed class TrainingCenterSettingsStoreTests
{
    [Fact]
    public async Task SaveAndLoadAsync_PreservesSettings()
    {
        await WithTempKnowledgeRoot(async path =>
        {
            await TrainingCenterSettingsStore.SaveAsync(new TrainingCenterSettings
            {
                GpuConcurrency = 3,
                RangeSampleCount = 7,
                RequireHumanReview = false
            });

            var loaded = await TrainingCenterSettingsStore.LoadAsync();

            Assert.Equal(3, loaded.GpuConcurrency);
            Assert.Equal(7, loaded.RangeSampleCount);
            Assert.False(loaded.RequireHumanReview);
            Assert.True(File.Exists(path));
        });
    }

    [Fact]
    public async Task LoadAsync_MovesCorruptFileAsideAndReturnsDefaults()
    {
        await WithTempKnowledgeRoot(async path =>
        {
            await File.WriteAllTextAsync(path, "{ keine gueltige JSON-Datei");

            var loaded = await TrainingCenterSettingsStore.LoadAsync();

            Assert.Equal(1, loaded.GpuConcurrency);
            Assert.True(loaded.RequireHumanReview);
            Assert.False(File.Exists(path));
            Assert.Single(Directory.EnumerateFiles(
                Path.GetDirectoryName(path)!,
                "training_settings.json.bad_*"));
        });
    }

    [Fact]
    public async Task FileStore_SecondSaveKeepsBackupAndNoTempFile()
    {
        await WithTempKnowledgeRoot(async path =>
        {
            var store = new TrainingCenterSettingsFileStore(path);
            await store.SaveAsync(new TrainingCenterSettings { GpuConcurrency = 2 });
            await store.SaveAsync(new TrainingCenterSettings { GpuConcurrency = 4 });

            var loaded = await store.LoadAsync();

            Assert.Equal(4, loaded.GpuConcurrency);
            Assert.True(File.Exists(path + ".bak"));
            Assert.Empty(Directory.EnumerateFiles(Path.GetDirectoryName(path)!, "*.tmp"));
        });
    }

    [Fact]
    public async Task FileStore_ParallelSavesLeaveReadableSettings()
    {
        await WithTempKnowledgeRoot(async path =>
        {
            var store = new TrainingCenterSettingsFileStore(path);
            await Task.WhenAll(Enumerable.Range(1, 12).Select(value =>
                store.SaveAsync(new TrainingCenterSettings { RangeSampleCount = value })));

            var loaded = await store.LoadAsync();

            Assert.InRange(loaded.RangeSampleCount, 1, 12);
        });
    }

    private static async Task WithTempKnowledgeRoot(Func<string, Task> body)
    {
        var previous = Environment.GetEnvironmentVariable(KnowledgeBasePaths.EnvironmentVariableName);
        var root = Path.Combine(
            Path.GetTempPath(),
            "sewer-training-center-settings-tests",
            Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        Environment.SetEnvironmentVariable(KnowledgeBasePaths.EnvironmentVariableName, root);
        KnowledgeBasePaths.ConfigureSettingsRoot(null);
        KnowledgeBasePaths.InvalidateCache();

        try
        {
            await body(KnowledgeBasePaths.GetTrainingSettingsPath());
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
