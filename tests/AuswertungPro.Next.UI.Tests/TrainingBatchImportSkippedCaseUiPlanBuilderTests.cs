using AuswertungPro.Next.Application.Ai.Training;
using AuswertungPro.Next.UI.Ai.Training;
using Xunit;

namespace AuswertungPro.Next.UI.Tests;

public sealed class TrainingBatchImportSkippedCaseUiPlanBuilderTests
{
    [Fact]
    public void Build_creates_preview_and_no_findings_result()
    {
        var skip = new TrainingCenterBatchSkipInfo(
            TrainingCenterBatchSkipKind.EmptyProtocol,
            "keine Eintraege",
            "  -> 0 Samples (keine Protokolleintraege erkannt)",
            "\u2014",
            "keine Eintraege");

        var plan = TrainingBatchImportSkippedCaseUiPlanBuilder.Build(
            caseId: "101.1-102.1",
            skip,
            previewFrame: @"C:\frames\preview.jpg",
            resultIndex: 4);

        Assert.Equal("101.1-102.1", plan.Preview.CaseInfo);
        Assert.Equal("\u2014", plan.Preview.CodeInfo);
        Assert.Equal("keine Eintraege", plan.Preview.MeterInfo);
        Assert.Equal(@"C:\frames\preview.jpg", plan.Preview.FramePath);
        Assert.Equal(4, plan.Result.Index);
        Assert.Equal("101.1-102.1", plan.Result.VsaCode);
        Assert.Equal(MatchLevel.NoFindings, plan.Result.Level);
        Assert.Equal("keine Eintraege", plan.Result.Summary);
    }
}
