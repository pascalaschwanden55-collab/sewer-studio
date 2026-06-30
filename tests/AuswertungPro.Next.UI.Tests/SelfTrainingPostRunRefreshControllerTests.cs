using AuswertungPro.Next.UI.Ai.Training;

namespace AuswertungPro.Next.UI.Tests;

public sealed class SelfTrainingPostRunRefreshControllerTests
{
    [Fact]
    public async Task RefreshAsync_laedt_samples_vor_kb_status()
    {
        var calls = new List<string>();

        await SelfTrainingPostRunRefreshController.RefreshAsync(
            () =>
            {
                calls.Add("load-samples");
                return Task.CompletedTask;
            },
            () =>
            {
                calls.Add("refresh-kb-status");
                return Task.CompletedTask;
            });

        Assert.Equal(new[] { "load-samples", "refresh-kb-status" }, calls);
    }
}
