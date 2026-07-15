using AuswertungPro.Next.UI.Player;

namespace AuswertungPro.Next.UI.Tests;

public sealed class LiveDetectionPulseControllerTests
{
    [Fact]
    public void Start_and_stop_keep_running_state_and_animation_in_sync()
    {
        var state = new LiveDetectionPulseStateController();
        var calls = new List<string>();
        var controller = new LiveDetectionPulseController(
            state,
            new LiveDetectionPulseControllerActions(
                StartAnimation: () => calls.Add("start"),
                StopAnimation: () => calls.Add("stop")));

        controller.Start();
        controller.Start();

        Assert.True(state.IsRunning);
        Assert.Equal(["start"], calls);

        controller.Stop();

        Assert.False(state.IsRunning);
        Assert.Equal(["start", "stop"], calls);
    }
}
