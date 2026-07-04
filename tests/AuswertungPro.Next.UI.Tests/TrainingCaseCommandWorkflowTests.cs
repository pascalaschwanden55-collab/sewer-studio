using AuswertungPro.Next.UI.Ai.Training;

namespace AuswertungPro.Next.UI.Tests;

public sealed class TrainingCaseCommandWorkflowTests
{
    [Fact]
    public void Run_ignoriert_fehlende_auswahl_ohne_statusaenderung()
    {
        var statusCalls = new List<string>();

        TrainingCaseCommandWorkflow.Run(
            new TrainingCaseCommandWorkflowRequest(
                SelectedCase: null,
                Decision: TrainingCaseDecision.Approve,
                SetStatusText: statusCalls.Add));

        Assert.Empty(statusCalls);
    }

    [Theory]
    [InlineData(TrainingCaseDecision.Approve, TrainingCaseStatus.Approved, "Approved: H1")]
    [InlineData(TrainingCaseDecision.Reject, TrainingCaseStatus.Rejected, "Rejected: H1")]
    [InlineData(TrainingCaseDecision.SetNew, TrainingCaseStatus.New, "Status New: H1")]
    public void Run_wendet_entscheidung_an_und_meldet_status(
        TrainingCaseDecision decision,
        TrainingCaseStatus expectedStatus,
        string expectedStatusText)
    {
        var trainingCase = new TrainingCase { CaseId = "H1" };
        var statusCalls = new List<string>();

        TrainingCaseCommandWorkflow.Run(
            new TrainingCaseCommandWorkflowRequest(
                trainingCase,
                decision,
                statusCalls.Add));

        Assert.Equal(expectedStatus, trainingCase.Status);
        Assert.Equal([expectedStatusText], statusCalls);
    }
}
