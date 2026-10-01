using System.Windows.Input;

namespace AuswertungPro.Next.UI.Views.Windows;

public enum TrainingStudioKeyboardShortcutAction
{
    Accept,
    Correct,
    Discard,
    NextItem,
    PreviousItem
}

public readonly record struct TrainingStudioKeyboardShortcutDecision(
    bool ShouldHandle,
    TrainingStudioKeyboardShortcutAction? Action);

public static class TrainingStudioKeyboardShortcutPolicy
{
    public static TrainingStudioKeyboardShortcutDecision Resolve(
        Key key,
        ModifierKeys modifiers,
        bool isTextInputFocused,
        bool isBusy)
    {
        if (isTextInputFocused || modifiers != ModifierKeys.None)
            return new TrainingStudioKeyboardShortcutDecision(false, null);

        var action = key switch
        {
            Key.A => TrainingStudioKeyboardShortcutAction.Accept,
            Key.K => TrainingStudioKeyboardShortcutAction.Correct,
            Key.V => TrainingStudioKeyboardShortcutAction.Discard,
            Key.Right => TrainingStudioKeyboardShortcutAction.NextItem,
            Key.Left => TrainingStudioKeyboardShortcutAction.PreviousItem,
            _ => (TrainingStudioKeyboardShortcutAction?)null
        };

        if (action is null)
            return new TrainingStudioKeyboardShortcutDecision(false, null);

        return isBusy
            ? new TrainingStudioKeyboardShortcutDecision(true, null)
            : new TrainingStudioKeyboardShortcutDecision(true, action);
    }
}
