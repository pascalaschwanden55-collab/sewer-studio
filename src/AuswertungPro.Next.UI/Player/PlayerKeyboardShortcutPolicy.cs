using System.Windows.Input;

namespace AuswertungPro.Next.UI.Player;

public enum PlayerKeyboardAction
{
    CancelCodingOverlay,
    TogglePlayPause,
    Stop,
    Pause,
    Resume,
    SpeedUp,
    SpeedDown,
    JumpForward,
    JumpBackward,
    ToggleDetection,
    ToggleMarkTool
}

public static class PlayerKeyboardShortcutPolicy
{
    /// <summary>
    /// Welche Fenstertasten duerfen auch dann wirken, wenn ein Text- oder
    /// Auswahlfeld den Fokus hat? Nur F1: die Tastenuebersicht ist Hilfe und
    /// kein Schriftzeichen. Das Fragezeichen bleibt gesperrt, es gehoert in den Text.
    /// </summary>
    public static bool IsAllowedDuringTextInput(Key key) => key is Key.F1;

    public static PlayerKeyboardAction? Resolve(Key key, bool canCancelCodingOverlay)
        => key switch
        {
            Key.Escape when canCancelCodingOverlay => PlayerKeyboardAction.CancelCodingOverlay,
            Key.Space => PlayerKeyboardAction.TogglePlayPause,
            Key.S => PlayerKeyboardAction.Stop,
            Key.P => PlayerKeyboardAction.Pause,
            Key.R => PlayerKeyboardAction.Resume,
            Key.Add or Key.OemPlus => PlayerKeyboardAction.SpeedUp,
            Key.Subtract or Key.OemMinus => PlayerKeyboardAction.SpeedDown,
            Key.Right => PlayerKeyboardAction.JumpForward,
            Key.Left => PlayerKeyboardAction.JumpBackward,
            Key.D => PlayerKeyboardAction.ToggleDetection,
            Key.M => PlayerKeyboardAction.ToggleMarkTool,
            _ => null
        };
}
