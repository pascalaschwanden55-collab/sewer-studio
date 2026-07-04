using AuswertungPro.Next.Application.Ai.Training;
using AuswertungPro.Next.Infrastructure.Ai.SelfImproving;
using AuswertungPro.Next.UI.Ai.Training;

namespace AuswertungPro.Next.UI.Tests;

public sealed class TrainingReviewSampleIdResolutionWorkflowTests
{
    [Fact]
    public async Task ResolveAsync_delegiert_sample_id_aufloesung_und_store_load()
    {
        var item = new ReviewQueueItem("review-1", null, 0.9, DateTime.UtcNow)
        {
            SelfTrainingCaseId = "H-001",
            SelfTrainingVsaCode = "BAB",
            SelfTrainingMeter = 12.3
        };
        var loaded = false;

        var result = await TrainingReviewSampleIdResolutionWorkflow.ResolveAsync(
            new TrainingReviewSampleIdResolutionWorkflowRequest(
                item,
                () =>
                {
                    loaded = true;
                    return Task.FromResult(new List<TrainingSample>
                    {
                        new()
                        {
                            SampleId = "sample-1",
                            CaseId = "H-001",
                            Code = "BAB",
                            MeterStart = 12.35
                        }
                    });
                }));

        Assert.True(loaded);
        Assert.Equal("sample-1", result);
    }
}
