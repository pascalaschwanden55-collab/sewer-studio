using System.Collections.Generic;
using AuswertungPro.Next.Application.Ai.Training;
using AuswertungPro.Next.UI.Ai.Training;

namespace AuswertungPro.Next.UI.Tests;

public sealed class SelfTrainingStepWorkflowTests
{
    [Fact]
    public void Apply_updates_step_fields_logs_and_live_frame()
    {
        var state = new StepState();

        SelfTrainingStepWorkflow.Apply(
            new SelfTrainingStepWorkflowRequest(
                Step: Step(
                    entryIndex: 1,
                    totalEntries: 4,
                    code: "BBA",
                    meter: 4.2,
                    stage: SelfTrainingStage.ExtractingFrame,
                    framePath: "frame.png"),
                ActiveVisionModel: "qwen3-vl",
                OnUi: action => action(),
                Ui: state.Ui,
                MatchRateTracker: state.MatchRateTracker,
                RefreshMatchRatePercents: state.RefreshMatchRatePercents,
                Results: state.Results,
                UpdateCodeDistribution: state.UpdateCodeDistribution));

        Assert.Equal((int)SelfTrainingStage.ExtractingFrame, state.PipelineActiveStep);
        Assert.Equal("BBA", state.CurrentEntryCode);
        Assert.Equal(4.2, state.CurrentEntryMeter);
        Assert.Equal(2, state.ProgressValue);
        Assert.Equal(4, state.ProgressMax);
        Assert.Equal("ffmpeg (CPU)", state.ActiveModelName);
        Assert.True(state.IsModelActive);
        Assert.Equal(["Frame extrahieren: BBA @ 4.2m"], state.Logs);
        Assert.Equal("frame.png", state.LiveFramePath);
        Assert.Empty(state.Results);
    }

    [Fact]
    public void Apply_completed_result_updates_match_rate_result_and_code_distribution()
    {
        var state = new StepState();
        var comparison = new ComparisonResult(
            MatchLevel.PartialMatch,
            ConfidenceScore: 0.73,
            Explanation: "Meter abweichend",
            CodeMatched: true,
            MeterMatched: false,
            SeverityPlausible: true,
            ClockMatched: true,
            BestMatchCode: "BBA",
            BestMatchMeter: 7.8);

        SelfTrainingStepWorkflow.Apply(
            new SelfTrainingStepWorkflowRequest(
                Step: Step(
                    entryIndex: 2,
                    totalEntries: 5,
                    code: "BBA",
                    meter: 7.7,
                    stage: SelfTrainingStage.Completed,
                    comparison: comparison),
                ActiveVisionModel: "qwen3-vl",
                OnUi: action => action(),
                Ui: state.Ui,
                MatchRateTracker: state.MatchRateTracker,
                RefreshMatchRatePercents: state.RefreshMatchRatePercents,
                Results: state.Results,
                UpdateCodeDistribution: state.UpdateCodeDistribution));

        Assert.Equal($"PartialMatch ({0.73:P0})", state.CurrentComparisonText);
        Assert.Equal(1.0, state.PartialPercent);
        var result = Assert.Single(state.Results);
        Assert.Equal(3, result.Index);
        Assert.Equal("BBA", result.VsaCode);
        Assert.Equal(7.7, result.Meter);
        Assert.Equal(MatchLevel.PartialMatch, result.Level);

        var distribution = Assert.Single(state.CodeDistribution);
        Assert.Equal("BBA", distribution.Code);
        Assert.Equal(1, distribution.Total);
        Assert.Equal(1, distribution.Partial);
    }

    private static SelfTrainingStep Step(
        int entryIndex = 0,
        int totalEntries = 1,
        string code = "BAA",
        double meter = 1.2,
        SelfTrainingStage stage = SelfTrainingStage.ExtractingFrame,
        ComparisonResult? comparison = null,
        string? framePath = null)
        => new(
            entryIndex,
            totalEntries,
            code,
            meter,
            stage,
            comparison,
            Technique: null,
            framePath);

    private sealed class StepState
    {
        public int PipelineActiveStep { get; private set; }
        public string CurrentEntryCode { get; private set; } = "";
        public double CurrentEntryMeter { get; private set; }
        public int ProgressValue { get; private set; }
        public int ProgressMax { get; private set; }
        public string ActiveModelName { get; private set; } = "";
        public bool IsModelActive { get; private set; }
        public string CurrentTechniqueGrade { get; private set; } = "";
        public string CurrentTechniqueDetails { get; private set; } = "";
        public string CurrentComparisonText { get; private set; } = "";
        public string? LiveFramePath { get; private set; }
        public double PartialPercent { get; private set; }
        public SelfTrainingMatchRateTracker MatchRateTracker { get; } = new();
        public List<string> Logs { get; } = new();
        public List<SelfTrainingEntryResult> Results { get; } = new();
        public List<CodeDistributionEntry> CodeDistribution { get; } = new();

        public SelfTrainingStepWorkflowUi Ui => new(
            value => PipelineActiveStep = value,
            value => CurrentEntryCode = value,
            value => CurrentEntryMeter = value,
            value => ProgressValue = value,
            value => ProgressMax = value,
            value => ActiveModelName = value,
            value => IsModelActive = value,
            value => CurrentTechniqueGrade = value,
            value => CurrentTechniqueDetails = value,
            value => CurrentComparisonText = value,
            Logs.Add,
            value => LiveFramePath = value);

        public void RefreshMatchRatePercents()
        {
            PartialPercent = MatchRateTracker.ComputePercents().Partial;
        }

        public void UpdateCodeDistribution(string code, MatchLevel level)
        {
            SelfTrainingCodeDistributionController.ApplyMatch(CodeDistribution, code, level);
        }
    }
}
