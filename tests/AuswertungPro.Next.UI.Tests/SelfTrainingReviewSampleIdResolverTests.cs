using AuswertungPro.Next.Application.Ai.Training;
using AuswertungPro.Next.Infrastructure.Ai.SelfImproving;
using AuswertungPro.Next.UI.Ai.Training;

namespace AuswertungPro.Next.UI.Tests;

public sealed class SelfTrainingReviewSampleIdResolverTests
{
    [Fact]
    public async Task ResolveAsync_gibt_direkte_sample_id_ohne_store_load_zurueck()
    {
        var item = Item(sampleId: "direct");
        var loaded = false;

        var result = await SelfTrainingReviewSampleIdResolver.ResolveAsync(
            item,
            () =>
            {
                loaded = true;
                return Task.FromResult(new List<TrainingSample>());
            });

        Assert.Equal("direct", result);
        Assert.False(loaded);
    }

    [Fact]
    public async Task ResolveAsync_findet_altbestand_per_case_code_und_meter_toleranz()
    {
        var item = Item(sampleId: null, meter: 12.3);
        var samples = new List<TrainingSample>
        {
            Sample("wrong-code", "H-001", "BAA", 12.3),
            Sample("match", "H-001", "BAB", 12.45),
            Sample("too-far", "H-001", "BAB", 12.7)
        };

        var result = await SelfTrainingReviewSampleIdResolver.ResolveAsync(
            item,
            () => Task.FromResult(samples));

        Assert.Equal("match", result);
    }

    [Fact]
    public async Task ResolveAsync_gibt_null_zurueck_wenn_altbestand_nicht_passt()
    {
        var item = Item(sampleId: null, meter: 12.3);

        var result = await SelfTrainingReviewSampleIdResolver.ResolveAsync(
            item,
            () => Task.FromResult(new List<TrainingSample>
            {
                Sample("too-far", "H-001", "BAB", 12.51)
            }));

        Assert.Null(result);
    }

    private static ReviewQueueItem Item(string? sampleId, double meter = 12.3)
        => new("id", null, 0.9, DateTime.UtcNow)
        {
            SelfTrainingCaseId = "H-001",
            SelfTrainingVsaCode = "BAB",
            SelfTrainingMeter = meter,
            SelfTrainingSampleId = sampleId
        };

    private static TrainingSample Sample(string id, string caseId, string code, double meter)
        => new()
        {
            SampleId = id,
            CaseId = caseId,
            Code = code,
            MeterStart = meter
        };
}
