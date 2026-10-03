using System.Windows;
using System.Windows.Input;
using AuswertungPro.Next.UI.Helpers;

namespace AuswertungPro.Next.UI.Player;

/// <summary>Verbindet Tastaturfokus, Tastenhilfe und die vorhandenen Playeraktionen.</summary>
internal sealed class PlayerKeyboardPresenter
{
    private readonly PlayerShortcutOverlayController _overlay;
    private readonly PlayerKeyboardActionControllerOwner _owner;
    private readonly Func<PlayerKeyboardActionControllerFactoryActions> _createActions;
    private readonly Func<bool> _canCancelOverlay;
    private readonly Func<bool> _isTextInputFocused;

    internal PlayerKeyboardPresenter(
        PlayerShortcutOverlayController overlay,
        PlayerKeyboardActionControllerOwner owner,
        Func<PlayerKeyboardActionControllerFactoryActions> createActions,
        Func<bool> canCancelOverlay,
        Func<bool>? isTextInputFocused = null)
    {
        ArgumentNullException.ThrowIfNull(overlay);
        ArgumentNullException.ThrowIfNull(owner);
        ArgumentNullException.ThrowIfNull(createActions);
        ArgumentNullException.ThrowIfNull(canCancelOverlay);
        _overlay = overlay;
        _owner = owner;
        _createActions = createActions;
        _canCancelOverlay = canCancelOverlay;
        _isTextInputFocused = isTextInputFocused ?? KeyboardTextInputFocusGuard.IsTextInputFocused;
    }

    public void HandleKey(KeyEventArgs e)
    {
        var textInputFocused = _isTextInputFocused();
        if (textInputFocused && !PlayerKeyboardShortcutPolicy.IsAllowedDuringTextInput(e.Key))
            return;

        var overlayOutcome = _overlay.HandleKey(e.Key);
        if (overlayOutcome == PlayerShortcutOverlayKeyOutcome.Handled)
            e.Handled = true;
        if (overlayOutcome != PlayerShortcutOverlayKeyOutcome.Continue)
            return;
        if (textInputFocused)
            return;

        var keyboardActions = _owner.Ensure(_createActions());
        var action = PlayerKeyboardShortcutPolicy.Resolve(e.Key, _canCancelOverlay());
        PlayerKeyboardInputWorkflow.Execute(
            new PlayerKeyboardInputWorkflowRequest(action),
            new PlayerKeyboardInputWorkflowActions(
                ExecuteAction: keyboardActions.Execute,
                MarkHandled: () => { e.Handled = true; }));
    }

    public void Show(RoutedEventArgs e)
    {
        e.Handled = true;
        _overlay.Show();
    }

    public void Hide(RoutedEventArgs e)
    {
        e.Handled = true;
        _overlay.Hide();
    }
}
