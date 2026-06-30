using AuswertungPro.Next.UI.Ai.Training;

namespace AuswertungPro.Next.UI.Tests;

public sealed class SelfTrainingVisualResetControllerTests
{
    [Fact]
    public void Reset_leert_visual_collections_und_liefert_leere_anzeigewerte()
    {
        var results = new List<SelfTrainingEntryResult> { new() { VsaCode = "BAB" } };
        var distribution = new List<CodeDistributionEntry> { new() { Code = "BAB" } };
        var logEntries = new List<string> { "Eintrag" };

        var state = SelfTrainingVisualResetController.Reset(
            results,
            distribution,
            logEntries,
            resetMatchRate: false);

        Assert.Empty(results);
        Assert.Empty(distribution);
        Assert.Empty(logEntries);
        Assert.Equal(0, state.PipelineActiveStep);
        Assert.Equal("", state.CurrentEntryCode);
        Assert.Equal(0, state.CurrentEntryMeter);
        Assert.Equal("", state.CurrentComparisonText);
        Assert.Equal("", state.CurrentTechniqueGrade);
        Assert.Equal("", state.CurrentTechniqueDetails);
        Assert.False(state.ShouldResetMatchRate);
    }

    [Fact]
    public void Reset_gibt_match_rate_reset_flag_unveraendert_zurueck()
    {
        var state = SelfTrainingVisualResetController.Reset(
            new List<SelfTrainingEntryResult>(),
            new List<CodeDistributionEntry>(),
            new List<string>(),
            resetMatchRate: true);

        Assert.True(state.ShouldResetMatchRate);
    }
}
