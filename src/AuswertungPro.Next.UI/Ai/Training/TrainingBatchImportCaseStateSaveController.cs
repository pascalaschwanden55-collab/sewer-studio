namespace AuswertungPro.Next.UI.Ai.Training;

public static class TrainingBatchImportCaseStateSaveController
{
    public static async Task<bool> SaveIfDueAsync(
        int processedCount,
        int interval,
        Func<Task> saveAsync)
    {
        ArgumentNullException.ThrowIfNull(saveAsync);

        if (interval <= 0 || processedCount <= 0 || processedCount % interval != 0)
            return false;

        try
        {
            await saveAsync().ConfigureAwait(false);
            return true;
        }
        catch
        {
            return false;
        }
    }
}
