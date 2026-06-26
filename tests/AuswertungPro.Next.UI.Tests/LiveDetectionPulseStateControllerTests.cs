using AuswertungPro.Next.UI.Player;

namespace AuswertungPro.Next.UI.Tests;

public sealed class LiveDetectionPulseStateControllerTests
{
    [Fact]
    public void Defaults_to_not_running()
    {
        var controller = new LiveDetectionPulseStateController();

        Assert.False(controller.IsRunning);
    }

    [Fact]
    public void Workflow_actions_update_running_state()
    {
        var controller = new LiveDetectionPulseStateController();
        var calls = new List<string>();

        var startActions = controller.CreateStartActions(() => calls.Add("start"));
        startActions.SetRunning();
        startActions.StartPulse();

        Assert.True(controller.IsRunning);
        Assert.Equal(["start"], calls);

        var stopActions = controller.CreateStopActions(() => calls.Add("stop"));
        stopActions.ClearRunning();
        stopActions.StopPulse();

        Assert.False(controller.IsRunning);
        Assert.Equal(["start", "stop"], calls);
    }
}
