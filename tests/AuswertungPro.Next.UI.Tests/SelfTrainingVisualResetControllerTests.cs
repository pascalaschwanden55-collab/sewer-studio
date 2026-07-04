using System.Collections.ObjectModel;
using AuswertungPro.Next.Application.Ai.Training;
using AuswertungPro.Next.UI.Ai.Training;

namespace AuswertungPro.Next.UI.Tests;

public sealed class SelfTrainingVisualResetControllerTests
{
    [Fact]
    public void Reset_clears_visual_collections_and_default_fields()
    {
        var state = new VisualState
        {
            PipelineActiveStep = 4,
            CurrentEntryCode = "BAB",
            CurrentEntryMeter = 12.3,
            CurrentComparisonText = "Match",
            CurrentTechniqueGrade = "A",
            CurrentTechniqueDetails = "Details"
        };
        state.Results.Add(new SelfTrainingEntryResult
        {
            Index = 1,
            VsaCode = "BAB",
            Meter = 1.2,
            Level = MatchLevel.ExactMatch,
            Summary = "ok"
        });
        state.CodeDistribution.Add(new CodeDistributionEntry { Code = "BAB" });
        state.LogEntries.Add("log");

        SelfTrainingVisualResetController.Reset(Request(state));

        Assert.Empty(state.Results);
        Assert.Empty(state.CodeDistribution);
        Assert.Empty(state.LogEntries);
        Assert.Equal(0, state.PipelineActiveStep);
        Assert.Equal("", state.CurrentEntryCode);
        Assert.Equal(0, state.CurrentEntryMeter);
        Assert.Equal("", state.CurrentComparisonText);
        Assert.Equal("", state.CurrentTechniqueGrade);
        Assert.Equal("", state.CurrentTechniqueDetails);
        Assert.Equal(0, state.ResetMatchRateCalls);
        Assert.Equal(0, state.RefreshMatchRateCalls);
    }

    [Fact]
    public void Reset_with_match_rate_resets_and_refreshes_match_rate()
    {
        var state = new VisualState();

        SelfTrainingVisualResetController.Reset(Request(state), resetMatchRate: true);

        Assert.Equal(1, state.ResetMatchRateCalls);
        Assert.Equal(1, state.RefreshMatchRateCalls);
    }

    private static SelfTrainingVisualResetRequest Request(VisualState state)
        => new(
            Results: state.Results,
            CodeDistribution: state.CodeDistribution,
            LogEntries: state.LogEntries,
            SetPipelineActiveStep: value => state.PipelineActiveStep = value,
            SetCurrentEntryCode: value => state.CurrentEntryCode = value,
            SetCurrentEntryMeter: value => state.CurrentEntryMeter = value,
            SetCurrentComparisonText: value => state.CurrentComparisonText = value,
            SetCurrentTechniqueGrade: value => state.CurrentTechniqueGrade = value,
            SetCurrentTechniqueDetails: value => state.CurrentTechniqueDetails = value,
            ResetMatchRate: () => state.ResetMatchRateCalls++,
            RefreshMatchRatePercents: () => state.RefreshMatchRateCalls++);

    private sealed class VisualState
    {
        public ObservableCollection<SelfTrainingEntryResult> Results { get; } = new();
        public ObservableCollection<CodeDistributionEntry> CodeDistribution { get; } = new();
        public ObservableCollection<string> LogEntries { get; } = new();
        public int PipelineActiveStep { get; set; }
        public string CurrentEntryCode { get; set; } = "";
        public double CurrentEntryMeter { get; set; }
        public string CurrentComparisonText { get; set; } = "";
        public string CurrentTechniqueGrade { get; set; } = "";
        public string CurrentTechniqueDetails { get; set; } = "";
        public int ResetMatchRateCalls { get; set; }
        public int RefreshMatchRateCalls { get; set; }
    }
}
