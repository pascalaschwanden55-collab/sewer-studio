using AuswertungPro.Next.UI.Ai.Training;

namespace AuswertungPro.Next.UI.Tests;

public sealed class TrainingCaseCommandRequestFactoryTests
{
    [Fact]
    public void Create_verdrahtet_case_command_request()
    {
        var trainingCase = new TrainingCase { CaseId = "H1" };
        var statusCalls = new List<string>();

        var request = TrainingCaseCommandRequestFactory.Create(
            new TrainingCaseCommandRequestFactoryRequest(
                SelectedCase: trainingCase,
                Decision: TrainingCaseDecision.Reject,
                SetStatusText: statusCalls.Add));

        Assert.Same(trainingCase, request.SelectedCase);
        Assert.Equal(TrainingCaseDecision.Reject, request.Decision);

        request.SetStatusText("Rejected: H1");

        Assert.Equal(["Rejected: H1"], statusCalls);
    }
}
