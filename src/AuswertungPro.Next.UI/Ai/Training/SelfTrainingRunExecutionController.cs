using AuswertungPro.Next.Application.Ai.Training;

namespace AuswertungPro.Next.UI.Ai.Training;

public static class SelfTrainingRunExecutionController
{
    public static async Task<SelfTrainingResult> RunAsync(
        ISelfTrainingOrchestrator orchestrator,
        TrainingCaseInput input,
        IProgress<SelfTrainingStep> progress,
        Func<SelfTrainingResult, DateTime, SelfTrainingRunSnapshot?> buildSnapshot,
        Func<SelfTrainingRunSnapshot, Task> appendHistoryAsync,
        Func<DateTime> utcNow,
        CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(orchestrator);
        ArgumentNullException.ThrowIfNull(input);
        ArgumentNullException.ThrowIfNull(progress);
        ArgumentNullException.ThrowIfNull(buildSnapshot);
        ArgumentNullException.ThrowIfNull(appendHistoryAsync);
        ArgumentNullException.ThrowIfNull(utcNow);

        var result = await orchestrator.RunAsync(input, progress, ct);
        if (buildSnapshot(result, utcNow()) is { } snapshot)
            await appendHistoryAsync(snapshot);

        return result;
    }
}
