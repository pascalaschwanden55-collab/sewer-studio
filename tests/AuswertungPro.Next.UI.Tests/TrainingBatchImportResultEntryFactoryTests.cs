using AuswertungPro.Next.Application.Ai.Training;
using AuswertungPro.Next.UI.Ai.Training;
using Xunit;

namespace AuswertungPro.Next.UI.Tests;

public sealed class TrainingBatchImportResultEntryFactoryTests
{
    [Fact]
    public void CreateSkippedCase_maps_case_summary_as_no_findings_result()
    {
        var entry = TrainingBatchImportResultEntryFactory.CreateSkippedCase(
            index: 7,
            caseId: "101.1-102.1",
            summary: "keine Eintraege");

        Assert.Equal(7, entry.Index);
        Assert.Equal("101.1-102.1", entry.VsaCode);
        Assert.Equal(0, entry.Meter);
        Assert.Equal(MatchLevel.NoFindings, entry.Level);
        Assert.Equal("keine Eintraege", entry.Summary);
    }

    [Fact]
    public void CreateSample_maps_training_sample_as_no_findings_result()
    {
        var sample = new TrainingSample
        {
            Code = "BAA",
            MeterStart = 12.34,
            Beschreibung = "Riss offen"
        };

        var entry = TrainingBatchImportResultEntryFactory.CreateSample(index: 3, sample);

        Assert.Equal(3, entry.Index);
        Assert.Equal("BAA", entry.VsaCode);
        Assert.Equal(12.34, entry.Meter);
        Assert.Equal(MatchLevel.NoFindings, entry.Level);
        Assert.Equal("Riss offen", entry.Summary);
    }
}
