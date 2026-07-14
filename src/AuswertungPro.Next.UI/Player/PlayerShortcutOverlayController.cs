using System.Windows;
using System.Windows.Input;

namespace AuswertungPro.Next.UI.Player;

public enum PlayerShortcutOverlayKeyOutcome
{
    Continue,
    Blocked,
    Handled
}

public sealed class PlayerShortcutOverlayController
{
    private readonly FrameworkElement _overlay;

    public PlayerShortcutOverlayController(FrameworkElement overlay)
    {
        ArgumentNullException.ThrowIfNull(overlay);
        _overlay = overlay;
    }

    public PlayerShortcutOverlayKeyOutcome HandleKey(Key key)
    {
        if (_overlay.Visibility == Visibility.Visible)
        {
            if (!IsToggleKey(key) && key != Key.Escape)
                return PlayerShortcutOverlayKeyOutcome.Blocked;

            Hide();
            return PlayerShortcutOverlayKeyOutcome.Handled;
        }

        if (!IsToggleKey(key))
            return PlayerShortcutOverlayKeyOutcome.Continue;

        Show();
        return PlayerShortcutOverlayKeyOutcome.Handled;
    }

    public void Show() => _overlay.Visibility = Visibility.Visible;

    public void Hide() => _overlay.Visibility = Visibility.Collapsed;

    private static bool IsToggleKey(Key key) => key is Key.F1 or Key.OemQuestion;
}
