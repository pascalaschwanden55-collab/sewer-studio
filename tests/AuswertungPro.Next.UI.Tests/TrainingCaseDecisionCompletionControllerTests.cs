using AuswertungPro.Next.UI.Ai.Training;

namespace AuswertungPro.Next.UI.Tests;

public sealed class TrainingCaseDecisionCompletionControllerTests
{
    [Fact]
    public void Apply_setzt_status_text_aus_decision_result()
    {
        var values = new List<string>();

        TrainingCaseDecisionCompletionController.Apply(
            new TrainingCaseDecisionResult("Approved: 1.1-1.2"),
            values.Add);

        Assert.Equal(["Approved: 1.1-1.2"], values);
    }
}
