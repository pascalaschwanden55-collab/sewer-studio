namespace AuswertungPro.Next.UI.Player;

public sealed record PlayerKeyboardActionControllerFactoryActions(
    Action CancelCodingOverlay,
    Action TogglePlayPause,
    Action StopPlayback,
    Action<bool> SetPause,
    Action EnsurePlaying,
    Action<float> ChangeSpeed,
    Action<int> JumpSeconds,
    Action ToggleDetection,
    Action ToggleMarkTool);

public static class PlayerKeyboardActionControllerFactory
{
    public static PlayerKeyboardActionController Create(
        PlayerKeyboardActionControllerFactoryActions actions)
    {
        ArgumentNullException.ThrowIfNull(actions);

        return new PlayerKeyboardActionController(new PlayerKeyboardActionBindings
        {
            CancelCodingOverlay = actions.CancelCodingOverlay,
            TogglePlayPause = actions.TogglePlayPause,
            Stop = () => PlayerKeyboardPlaybackCommandRunner.Stop(actions.StopPlayback),
            Pause = () => PlayerKeyboardPlaybackCommandRunner.Pause(actions.SetPause),
            Resume = () => PlayerKeyboardPlaybackCommandRunner.Resume(actions.EnsurePlaying, actions.SetPause),
            ChangeSpeed = actions.ChangeSpeed,
            JumpSeconds = actions.JumpSeconds,
            ToggleDetection = actions.ToggleDetection,
            ToggleMarkTool = actions.ToggleMarkTool
        });
    }
}
