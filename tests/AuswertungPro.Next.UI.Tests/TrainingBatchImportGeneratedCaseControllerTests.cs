using AuswertungPro.Next.Application.Ai.Training;
using AuswertungPro.Next.Infrastructure.Ai.Training;
using AuswertungPro.Next.UI.Ai.Training;
using Xunit;

namespace AuswertungPro.Next.UI.Tests;

public sealed class TrainingBatchImportGeneratedCaseControllerTests
{
    [Fact]
    public void CreatePlan_returns_skip_plan_without_touching_signatures_when_no_samples_were_generated()
    {
        var signatures = new HashSet<string>(StringComparer.Ordinal) { "old" };
        var generation = new TrainingSampleGenerationResult(
            [],
            ParsedEntries: 0,
            DuplicateSkipped: 0,
            TrainingSampleGenerationOutcome.ProtocolFileMissing);

        var plan = TrainingBatchImportGeneratedCaseController.CreatePlan(
            "101.1-102.1",
            generation,
            previewFrame: @"C:\frames\preview.jpg",
            firstResultIndex: 5,
            signatures);

        Assert.Equal(TrainingBatchImportGeneratedCaseKind.Skipped, plan.Kind);
        Assert.Equal(TrainingCenterBatchSkipKind.MissingProtocol, plan.Skip!.Kind);
        Assert.NotNull(plan.SkippedCase);
        Assert.Equal("101.1-102.1", plan.SkippedCase!.Preview.CaseInfo);
        Assert.Equal("\u2014", plan.SkippedCase.Preview.CodeInfo);
        Assert.Equal("Protokoll fehlt", plan.SkippedCase.Preview.MeterInfo);
        Assert.Equal(@"C:\frames\preview.jpg", plan.SkippedCase.Preview.FramePath);
        Assert.Equal(5, plan.SkippedCase!.Result.Index);
        Assert.Equal("101.1-102.1", plan.SkippedCase.Result.VsaCode);
        Assert.Equal(MatchLevel.NoFindings, plan.SkippedCase.Result.Level);
        Assert.Equal("Protokoll fehlt", plan.SkippedCase.Result.Summary);
        Assert.Empty(plan.SampleUiPlans);
        Assert.Empty(plan.SampleLogLines);
        Assert.Equal(0, plan.NewSampleCount);
        Assert.Equal(new[] { "old" }, signatures.ToArray());
    }

    [Fact]
    public void CreatePlan_registers_samples_and_returns_sample_ui_plan()
    {
        var signatures = new HashSet<string>(StringComparer.Ordinal) { "old" };
        var samples = new List<TrainingSample>
        {
            new()
            {
                Code = "BAA",
                Beschreibung = "Riss",
                MeterStart = 1.25,
                MeterEnd = 1.75,
                FramePath = @"C:\frames\a.jpg",
                Signature = "sig-a",
                Status = TrainingSampleStatus.Approved
            },
            new()
            {
                Code = "BBB",
                Beschreibung = "Ablagerung",
                MeterStart = 3,
                MeterEnd = 4,
                Signature = "sig-b",
                Status = TrainingSampleStatus.Rejected
            }
        };
        var generation = new TrainingSampleGenerationResult(
            samples,
            ParsedEntries: 2,
            DuplicateSkipped: 0,
            TrainingSampleGenerationOutcome.Success);

        var plan = TrainingBatchImportGeneratedCaseController.CreatePlan(
            "101.1-102.1",
            generation,
            previewFrame: @"C:\frames\preview.jpg",
            firstResultIndex: 8,
            signatures);

        Assert.Equal(TrainingBatchImportGeneratedCaseKind.Samples, plan.Kind);
        Assert.Null(plan.Skip);
        Assert.Null(plan.SkippedCase);
        Assert.Equal(2, plan.NewSampleCount);
        Assert.All(samples, sample => Assert.Equal(TrainingSampleStatus.New, sample.Status));
        Assert.Equal(
            new[] { "old", "sig-a", "sig-b" },
            signatures.Order(StringComparer.Ordinal).ToArray());
        Assert.Equal(2, plan.SampleUiPlans.Count);
        Assert.Equal(8, plan.SampleUiPlans[0].Result.Index);
        Assert.Equal("BAA", plan.SampleUiPlans[0].Preview.CodeInfo);
        Assert.Equal(@"C:\frames\a.jpg", plan.SampleUiPlans[0].Preview.FramePath);
        Assert.Equal(9, plan.SampleUiPlans[1].Result.Index);
        Assert.Equal("BBB", plan.SampleUiPlans[1].Preview.CodeInfo);
        Assert.Equal(@"C:\frames\preview.jpg", plan.SampleUiPlans[1].Preview.FramePath);
        Assert.Equal(
            new[]
            {
                "  -> 2 Samples (Status: Neu, Freigabe ueber Review):",
                "     BAA @ 1.25m [New] - Riss",
                "     BBB @ 3.00m [New] - Ablagerung"
            },
            plan.SampleLogLines);
    }
}
