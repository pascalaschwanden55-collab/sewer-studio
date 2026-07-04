using AuswertungPro.Next.UI.Ai.Training;

namespace AuswertungPro.Next.UI.Tests;

public sealed class TrainingBatchImportRunControlControllerTests
{
    [Fact]
    public void Cancel_bricht_lauf_ab_und_setzt_status()
    {
        var calls = new List<string>();

        TrainingBatchImportRunControlController.Cancel(
            () => calls.Add("cancel"),
            value => calls.Add($"status:{value}"));

        Assert.Equal(
            [
                "cancel",
                "status:Abbruch angefordert..."
            ],
            calls);
    }
}
