using System;

namespace AuswertungPro.Next.UI.Player;

public sealed class PlayerKeyboardActionController
{
    private readonly PlayerKeyboardActionBindings _bindings;

    public PlayerKeyboardActionController(PlayerKeyboardActionBindings bindings)
    {
        ArgumentNullException.ThrowIfNull(bindings);
        ArgumentNullException.ThrowIfNull(bindings.CancelCodingOverlay);
        ArgumentNullException.ThrowIfNull(bindings.TogglePlayPause);
        ArgumentNullException.ThrowIfNull(bindings.Stop);
        ArgumentNullException.ThrowIfNull(bindings.Pause);
        ArgumentNullException.ThrowIfNull(bindings.Resume);
        ArgumentNullException.ThrowIfNull(bindings.ChangeSpeed);
        ArgumentNullException.ThrowIfNull(bindings.JumpSeconds);
        ArgumentNullException.ThrowIfNull(bindings.ToggleDetection);
        ArgumentNullException.ThrowIfNull(bindings.ToggleMarkTool);

        _bindings = bindings;
    }

    public bool Execute(PlayerKeyboardAction? action)
    {
        if (action is not { } value)
            return false;

        switch (value)
        {
            case PlayerKeyboardAction.CancelCodingOverlay:
                _bindings.CancelCodingOverlay();
                break;
            case PlayerKeyboardAction.TogglePlayPause:
                _bindings.TogglePlayPause();
                break;
            case PlayerKeyboardAction.Stop:
                _bindings.Stop();
                break;
            case PlayerKeyboardAction.Pause:
                _bindings.Pause();
                break;
            case PlayerKeyboardAction.Resume:
                _bindings.Resume();
                break;
            case PlayerKeyboardAction.SpeedUp:
                _bindings.ChangeSpeed(+0.25f);
                break;
            case PlayerKeyboardAction.SpeedDown:
                _bindings.ChangeSpeed(-0.25f);
                break;
            case PlayerKeyboardAction.JumpForward:
                _bindings.JumpSeconds(5);
                break;
            case PlayerKeyboardAction.JumpBackward:
                _bindings.JumpSeconds(-5);
                break;
            case PlayerKeyboardAction.ToggleDetection:
                _bindings.ToggleDetection();
                break;
            case PlayerKeyboardAction.ToggleMarkTool:
                _bindings.ToggleMarkTool();
                break;
            default:
                throw new ArgumentOutOfRangeException(nameof(action), value, null);
        }

        return true;
    }
}

public sealed class PlayerKeyboardActionBindings
{
    public required Action CancelCodingOverlay { get; init; }

    public required Action TogglePlayPause { get; init; }

    public required Action Stop { get; init; }

    public required Action Pause { get; init; }

    public required Action Resume { get; init; }

    public required Action<float> ChangeSpeed { get; init; }

    public required Action<int> JumpSeconds { get; init; }

    public required Action ToggleDetection { get; init; }

    public required Action ToggleMarkTool { get; init; }
}
