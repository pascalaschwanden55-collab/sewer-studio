using AuswertungPro.Next.Application.Ai.Training;
using AuswertungPro.Next.UI.Ai.Training;

namespace AuswertungPro.Next.UI.Tests;

public sealed class TrainingBatchImportGeneratedCaseUiControllerTests
{
    [Fact]
    public void Apply_recorded_skipped_case_und_signalisiert_naechsten_case()
    {
        var summary = new TrainingBatchImportRunSummary();
        var skip = new TrainingCenterBatchSkipInfo(
            TrainingCenterBatchSkipKind.EmptyProtocol,
            "keine Eintraege",
            "  -> 0 Samples",
            "-",
            "keine Eintraege");
        var result = Result("skip-code", MatchLevel.NoFindings);
        var preview = new TrainingBatchImportLivePreview("case-1", "-", "keine Eintraege", "frame.jpg");
        var plan = new TrainingBatchImportGeneratedCasePlan(
            TrainingBatchImportGeneratedCaseKind.Skipped,
            skip,
            new TrainingBatchImportSkippedCaseUiPlan(preview, result),
            Array.Empty<TrainingBatchImportSampleUiPlan>(),
            Array.Empty<string>(),
            NewSampleCount: 0);
        var calls = new List<string>();

        var applyResult = TrainingBatchImportGeneratedCaseUiController.Apply(
            plan,
            summary,
            new TrainingBatchImportCaseUiSink(
                p => calls.Add($"preview:{p.CaseInfo}:{p.CodeInfo}:{p.MeterInfo}:{p.FramePath}"),
                action =>
                {
                    calls.Add("on-ui");
                    action();
                },
                entry => calls.Add($"add-result:{entry.VsaCode}"),
                (_, _) => calls.Add("distribution"),
                _ => { },
                _ => { },
                calls.Add));

        Assert.True(applyResult.ShouldContinueWithNextCase);
        Assert.Contains("1 ohne Eintraege.", summary.BuildNoNewStatus(processedCaseCount: 1));
        Assert.Equal(
            new[]
            {
                "  -> 0 Samples",
                "preview:case-1:-:keine Eintraege:frame.jpg",
                "on-ui",
                "add-result:skip-code"
            },
            calls);
    }

    [Fact]
    public void Apply_wendet_sample_plans_an_und_loggt_sample_zeilen()
    {
        var summary = new TrainingBatchImportRunSummary();
        var first = SamplePlan("BAB", MatchLevel.ExactMatch);
        var second = SamplePlan("BAA", MatchLevel.PartialMatch);
        var plan = new TrainingBatchImportGeneratedCasePlan(
            TrainingBatchImportGeneratedCaseKind.Samples,
            null,
            null,
            new[] { first, second },
            new[] { "sample-log" },
            NewSampleCount: 2);
        var calls = new List<string>();

        var result = TrainingBatchImportGeneratedCaseUiController.Apply(
            plan,
            summary,
            new TrainingBatchImportCaseUiSink(
                p => calls.Add($"preview:{p.CodeInfo}"),
                action =>
                {
                    calls.Add("on-ui");
                    action();
                },
                entry => calls.Add($"add-result:{entry.VsaCode}"),
                (code, level) => calls.Add($"distribution:{code}:{level}"),
                _ => { },
                _ => { },
                calls.Add));

        Assert.False(result.ShouldContinueWithNextCase);
        Assert.Contains("2 Kandidaten gespeichert", summary.BuildCompletionStatus());
        Assert.Equal(
            new[]
            {
                "preview:BAB",
                "on-ui",
                "add-result:BAB",
                "distribution:BAB:ExactMatch",
                "preview:BAA",
                "on-ui",
                "add-result:BAA",
                "distribution:BAA:PartialMatch",
                "sample-log"
            },
            calls);
    }

    private static TrainingBatchImportSampleUiPlan SamplePlan(string code, MatchLevel level)
        => new(
            new TrainingBatchImportLivePreview("case-1", code, "1.00 - 2.00 m", "frame.jpg"),
            Result(code, level));

    private static SelfTrainingEntryResult Result(string code, MatchLevel level)
        => new()
        {
            Index = 1,
            VsaCode = code,
            Meter = 1.2,
            Level = level,
            Summary = code
        };
}
