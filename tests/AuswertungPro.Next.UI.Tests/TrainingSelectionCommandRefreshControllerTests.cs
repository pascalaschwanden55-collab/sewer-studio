using AuswertungPro.Next.UI.Ai.Training;

namespace AuswertungPro.Next.UI.Tests;

public sealed class TrainingSelectionCommandRefreshControllerTests
{
    [Fact]
    public void RefreshCaseSelection_notifiziert_alle_case_commands()
    {
        var calls = new List<string>();

        TrainingSelectionCommandRefreshController.RefreshCaseSelection(
            new TrainingCaseSelectionCommandRefresh(
                () => calls.Add("approve"),
                () => calls.Add("reject"),
                () => calls.Add("set-new"),
                () => calls.Add("generate")));

        Assert.Equal(["approve", "reject", "set-new", "generate"], calls);
    }

    [Fact]
    public void RefreshSampleSelection_notifiziert_alle_sample_commands()
    {
        var calls = new List<string>();

        TrainingSelectionCommandRefreshController.RefreshSampleSelection(
            new TrainingSampleSelectionCommandRefresh(
                () => calls.Add("approve"),
                () => calls.Add("reject"),
                () => calls.Add("remove")));

        Assert.Equal(["approve", "reject", "remove"], calls);
    }
}
