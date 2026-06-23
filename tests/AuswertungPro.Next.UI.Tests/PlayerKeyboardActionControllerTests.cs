using AuswertungPro.Next.UI.Player;

namespace AuswertungPro.Next.UI.Tests;

public sealed class PlayerKeyboardActionControllerTests
{
    [Theory]
    [InlineData(PlayerKeyboardAction.CancelCodingOverlay, "cancel")]
    [InlineData(PlayerKeyboardAction.TogglePlayPause, "toggle-play")]
    [InlineData(PlayerKeyboardAction.Stop, "stop")]
    [InlineData(PlayerKeyboardAction.Pause, "pause")]
    [InlineData(PlayerKeyboardAction.Resume, "resume")]
    [InlineData(PlayerKeyboardAction.SpeedUp, "speed:0.25")]
    [InlineData(PlayerKeyboardAction.SpeedDown, "speed:-0.25")]
    [InlineData(PlayerKeyboardAction.JumpForward, "jump:5")]
    [InlineData(PlayerKeyboardAction.JumpBackward, "jump:-5")]
    [InlineData(PlayerKeyboardAction.ToggleDetection, "toggle-detection")]
    [InlineData(PlayerKeyboardAction.ToggleMarkTool, "toggle-mark")]
    public void Execute_invokes_matching_callback(PlayerKeyboardAction action, string expected)
    {
        var calls = new List<string>();
        var controller = CreateController(calls);

        Assert.True(controller.Execute(action));
        Assert.Equal([expected], calls);
    }

    [Fact]
    public void Execute_ignores_missing_action()
    {
        var calls = new List<string>();
        var controller = CreateController(calls);

        Assert.False(controller.Execute(null));
        Assert.Empty(calls);
    }

    private static PlayerKeyboardActionController CreateController(List<string> calls)
        => new(new PlayerKeyboardActionBindings
        {
            CancelCodingOverlay = () => calls.Add("cancel"),
            TogglePlayPause = () => calls.Add("toggle-play"),
            Stop = () => calls.Add("stop"),
            Pause = () => calls.Add("pause"),
            Resume = () => calls.Add("resume"),
            ChangeSpeed = delta => calls.Add($"speed:{delta:0.##}"),
            JumpSeconds = seconds => calls.Add($"jump:{seconds}"),
            ToggleDetection = () => calls.Add("toggle-detection"),
            ToggleMarkTool = () => calls.Add("toggle-mark")
        });
}
