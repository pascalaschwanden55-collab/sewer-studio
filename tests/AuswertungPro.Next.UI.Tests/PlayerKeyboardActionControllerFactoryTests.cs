using AuswertungPro.Next.UI.Player;

namespace AuswertungPro.Next.UI.Tests;

public sealed class PlayerKeyboardActionControllerFactoryTests
{
    [Fact]
    public void Create_maps_playback_bindings_through_keyboard_runner()
    {
        var calls = new List<string>();
        var controller = PlayerKeyboardActionControllerFactory.Create(
            new PlayerKeyboardActionControllerFactoryActions(
                CancelCodingOverlay: () => calls.Add("cancel"),
                TogglePlayPause: () => calls.Add("toggle"),
                StopPlayback: () => calls.Add("stop"),
                SetPause: pause => calls.Add($"pause:{pause}"),
                EnsurePlaying: () => calls.Add("ensure"),
                ChangeSpeed: delta => calls.Add($"speed:{delta:0.##}"),
                JumpSeconds: seconds => calls.Add($"jump:{seconds}"),
                ToggleDetection: () => calls.Add("detection"),
                ToggleMarkTool: () => calls.Add("mark")));

        controller.Execute(PlayerKeyboardAction.Stop);
        controller.Execute(PlayerKeyboardAction.Pause);
        controller.Execute(PlayerKeyboardAction.Resume);

        Assert.Equal(
            ["stop", "pause:True", "ensure", "pause:False"],
            calls);
    }
}
