namespace AuswertungPro.Next.UI.Ai.Training;

public static class SelfTrainingPostRunRefreshController
{
    public static async Task RefreshAsync(
        Func<Task> loadSamplesAsync,
        Func<Task> refreshKbStatusAsync)
    {
        ArgumentNullException.ThrowIfNull(loadSamplesAsync);
        ArgumentNullException.ThrowIfNull(refreshKbStatusAsync);

        await loadSamplesAsync();
        await refreshKbStatusAsync();
    }
}
