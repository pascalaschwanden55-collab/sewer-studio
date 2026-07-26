using System.Windows.Input;

namespace AuswertungPro.Next.UI.Ai.Vsa;

public enum VsaCodeExplorerKeyboardNavigationAction
{
    NavigateBack,
    ApplyAndClose
}

public static class VsaCodeExplorerKeyboardNavigationPolicy
{
    public static VsaCodeExplorerKeyboardNavigationAction? Resolve(
        Key key,
        ModifierKeys modifiers,
        bool isTextBoxFocused,
        bool showResultPanel,
        int currentLevel)
        => key switch
        {
            Key.Escape when showResultPanel || currentLevel > 0 => VsaCodeExplorerKeyboardNavigationAction.NavigateBack,
            Key.Back when !isTextBoxFocused => VsaCodeExplorerKeyboardNavigationAction.NavigateBack,
            Key.S when modifiers == ModifierKeys.Control => VsaCodeExplorerKeyboardNavigationAction.ApplyAndClose,
            _ => null
        };
}
