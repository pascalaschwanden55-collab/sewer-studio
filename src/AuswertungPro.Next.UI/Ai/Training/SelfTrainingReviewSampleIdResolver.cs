using AuswertungPro.Next.Application.Ai.Training;
using InfraSelfImproving = AuswertungPro.Next.Infrastructure.Ai.SelfImproving;

namespace AuswertungPro.Next.UI.Ai.Training;

public static class SelfTrainingReviewSampleIdResolver
{
    public static async Task<string?> ResolveAsync(
        InfraSelfImproving.ReviewQueueItem item,
        Func<Task<List<TrainingSample>>> loadSamplesAsync)
    {
        ArgumentNullException.ThrowIfNull(item);
        ArgumentNullException.ThrowIfNull(loadSamplesAsync);

        if (!string.IsNullOrEmpty(item.SelfTrainingSampleId))
            return item.SelfTrainingSampleId;

        var allSamples = await loadSamplesAsync().ConfigureAwait(false);
        var itemMeter = item.SelfTrainingMeter ?? 0;
        return allSamples.FirstOrDefault(sample =>
            sample.CaseId == item.SelfTrainingCaseId
            && sample.Code == item.SelfTrainingVsaCode
            && Math.Abs(sample.MeterStart - itemMeter) < 0.2)?.SampleId;
    }
}
