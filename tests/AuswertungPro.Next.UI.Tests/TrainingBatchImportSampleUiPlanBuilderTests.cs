using AuswertungPro.Next.Application.Ai.Training;
using AuswertungPro.Next.UI.Ai.Training;
using Xunit;

namespace AuswertungPro.Next.UI.Tests;

public sealed class TrainingBatchImportSampleUiPlanBuilderTests
{
    [Fact]
    public void Build_creates_preview_and_result_entries_with_sequential_indices()
    {
        var samples = new[]
        {
            new TrainingSample
            {
                Code = "BAA",
                Beschreibung = "Riss",
                MeterStart = 1.25,
                MeterEnd = 1.75,
                FramePath = @"C:\frames\a.jpg"
            },
            new TrainingSample
            {
                Code = "BBB",
                Beschreibung = "Ablagerung",
                MeterStart = 3,
                MeterEnd = 4,
                FramePath = ""
            }
        };

        var plans = TrainingBatchImportSampleUiPlanBuilder.Build(
            caseId: "101.1-102.1",
            samples,
            previewFrame: @"C:\frames\preview.jpg",
            firstResultIndex: 6);

        Assert.Equal(2, plans.Count);
        Assert.Equal("101.1-102.1", plans[0].Preview.CaseInfo);
        Assert.Equal("BAA", plans[0].Preview.CodeInfo);
        Assert.Equal("1.25 \u2013 1.75 m", plans[0].Preview.MeterInfo);
        Assert.Equal(@"C:\frames\a.jpg", plans[0].Preview.FramePath);
        Assert.Equal(6, plans[0].Result.Index);
        Assert.Equal("BAA", plans[0].Result.VsaCode);
        Assert.Equal(MatchLevel.NoFindings, plans[0].Result.Level);
        Assert.Equal("Riss", plans[0].Result.Summary);

        Assert.Equal("BBB", plans[1].Preview.CodeInfo);
        Assert.Equal(@"C:\frames\preview.jpg", plans[1].Preview.FramePath);
        Assert.Equal(7, plans[1].Result.Index);
        Assert.Equal("Ablagerung", plans[1].Result.Summary);
    }
}
