using AuswertungPro.Next.UI.Ai.Training;
using Xunit;

namespace AuswertungPro.Next.UI.Tests;

public sealed class TrainingBatchImportCaseStateSaveControllerTests
{
    [Fact]
    public async Task SaveIfDueAsync_skips_when_processed_count_is_not_on_interval()
    {
        var saved = false;

        var didSave = await TrainingBatchImportCaseStateSaveController.SaveIfDueAsync(
            processedCount: 4,
            interval: 5,
            saveAsync: () =>
            {
                saved = true;
                return Task.CompletedTask;
            });

        Assert.False(didSave);
        Assert.False(saved);
    }

    [Fact]
    public async Task SaveIfDueAsync_saves_on_interval()
    {
        var saveCount = 0;

        var didSave = await TrainingBatchImportCaseStateSaveController.SaveIfDueAsync(
            processedCount: 5,
            interval: 5,
            saveAsync: () =>
            {
                saveCount++;
                return Task.CompletedTask;
            });

        Assert.True(didSave);
        Assert.Equal(1, saveCount);
    }

    [Fact]
    public async Task SaveIfDueAsync_swallows_best_effort_save_errors()
    {
        var didSave = await TrainingBatchImportCaseStateSaveController.SaveIfDueAsync(
            processedCount: 10,
            interval: 5,
            saveAsync: () => throw new InvalidOperationException("kaputt"));

        Assert.False(didSave);
    }
}
