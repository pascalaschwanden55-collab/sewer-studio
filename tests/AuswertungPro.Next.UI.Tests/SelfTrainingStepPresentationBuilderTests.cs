using AuswertungPro.Next.Application.Ai;
using AuswertungPro.Next.Application.Ai.Training;
using AuswertungPro.Next.UI.Ai.Training;

namespace AuswertungPro.Next.UI.Tests;

public sealed class SelfTrainingStepPresentationBuilderTests
{
    [Fact]
    public void Build_for_extracting_frame_keeps_progress_model_label_log_and_frame_path()
    {
        var presentation = SelfTrainingStepPresentationBuilder.Build(
            Step(
                entryIndex: 1,
                totalEntries: 4,
                code: "BBA",
                meter: 4.2,
                stage: SelfTrainingStage.ExtractingFrame,
                framePath: "frame.png"),
            activeVisionModel: "qwen3-vl");

        Assert.Equal((int)SelfTrainingStage.ExtractingFrame, presentation.PipelineActiveStep);
        Assert.Equal("BBA", presentation.CurrentEntryCode);
        Assert.Equal(4.2, presentation.CurrentEntryMeter);
        Assert.Equal(2, presentation.ProgressValue);
        Assert.Equal(4, presentation.ProgressMax);
        Assert.Equal("ffmpeg (CPU)", presentation.ActiveModelName);
        Assert.True(presentation.IsModelActive);
        Assert.Equal("frame.png", presentation.LiveFramePath);
        Assert.Equal(["Frame extrahieren: BBA @ 4.2m"], presentation.LogLines);
    }

    [Fact]
    public void Build_for_technique_stage_keeps_grade_details_and_log()
    {
        var technique = new TechniqueAssessment(
            OsdReadable: true,
            OsdDeltaMeters: 0.1,
            LightingQuality: "Gut",
            SharpnessQuality: "Mittel",
            CenteringQuality: "Gut",
            OverallGrade: "B",
            MeanLuminance: 120,
            LaplacianVariance: 80);

        var presentation = SelfTrainingStepPresentationBuilder.Build(
            Step(stage: SelfTrainingStage.AssessingTechnique, technique: technique),
            activeVisionModel: "qwen3-vl");

        Assert.Equal("B", presentation.CurrentTechniqueGrade);
        Assert.Equal("Licht: Gut | Schaerfe: Mittel", presentation.CurrentTechniqueDetails);
        Assert.Equal("qwen3-vl (GPU)", presentation.ActiveModelName);
        Assert.Equal(["Technik: B (Licht=Gut, Schaerfe=Mittel)"], presentation.LogLines);
    }

    [Fact]
    public void Build_for_completed_stage_keeps_comparison_text_result_and_log()
    {
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

        var presentation = SelfTrainingStepPresentationBuilder.Build(
            Step(
                entryIndex: 2,
                totalEntries: 5,
                code: "BBA",
                meter: 7.7,
                stage: SelfTrainingStage.Completed,
                comparison: comparison),
            activeVisionModel: "qwen3-vl");

        Assert.Equal("", presentation.ActiveModelName);
        Assert.False(presentation.IsModelActive);
        Assert.Equal($"PartialMatch ({0.73:P0})", presentation.CurrentComparisonText);
        Assert.Equal(MatchLevel.PartialMatch, presentation.CompletedMatchLevel);
        Assert.Equal([$"Ergebnis: BBA \u2192 PARTIAL ({0.73:P0}) Meter abweichend"], presentation.LogLines);

        Assert.NotNull(presentation.Result);
        var result = presentation.Result!;
        Assert.Equal(3, result.Index);
        Assert.Equal("BBA", result.VsaCode);
        Assert.Equal(7.7, result.Meter);
        Assert.Equal(MatchLevel.PartialMatch, result.Level);
        Assert.Equal("Meter abweichend", result.Summary);
    }

    [Fact]
    public void Build_keeps_existing_building_timeline_error_logs()
    {
        var presentation = SelfTrainingStepPresentationBuilder.Build(
            Step(stage: SelfTrainingStage.BuildingTimeline, error: "Timeline defekt"),
            activeVisionModel: "qwen3-vl");

        Assert.Equal(["Timeline defekt", "FEHLER: Timeline defekt"], presentation.LogLines);
    }

    private static SelfTrainingStep Step(
        int entryIndex = 0,
        int totalEntries = 1,
        string code = "BAA",
        double meter = 1.2,
        SelfTrainingStage stage = SelfTrainingStage.ExtractingFrame,
        ComparisonResult? comparison = null,
        TechniqueAssessment? technique = null,
        string? framePath = null,
        string? error = null)
        => new(
            entryIndex,
            totalEntries,
            code,
            meter,
            stage,
            comparison,
            technique,
            framePath,
            error);
}
