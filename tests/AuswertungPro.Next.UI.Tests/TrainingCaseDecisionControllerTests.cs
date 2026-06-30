using AuswertungPro.Next.UI.Ai.Training;

namespace AuswertungPro.Next.UI.Tests;

public sealed class TrainingCaseDecisionControllerTests
{
    [Theory]
    [InlineData(TrainingCaseDecision.Approve, TrainingCaseStatus.Approved, "Approved: 1.1-1.2")]
    [InlineData(TrainingCaseDecision.Reject, TrainingCaseStatus.Rejected, "Rejected: 1.1-1.2")]
    [InlineData(TrainingCaseDecision.SetNew, TrainingCaseStatus.New, "Status New: 1.1-1.2")]
    public void Apply_setzt_status_und_liefert_bisherigen_statustext(
        TrainingCaseDecision decision,
        TrainingCaseStatus expectedStatus,
        string expectedStatusText)
    {
        var trainingCase = new TrainingCase
        {
            CaseId = "1.1-1.2",
            Status = TrainingCaseStatus.New
        };

        var result = TrainingCaseDecisionController.Apply(trainingCase, decision);

        Assert.Equal(expectedStatus, trainingCase.Status);
        Assert.Equal(expectedStatusText, result.StatusText);
    }

    [Fact]
    public void Apply_wirft_bei_unbekannter_entscheidung()
    {
        var trainingCase = new TrainingCase { CaseId = "1.1-1.2" };

        Assert.Throws<ArgumentOutOfRangeException>(() =>
            TrainingCaseDecisionController.Apply(trainingCase, (TrainingCaseDecision)99));
    }
}
