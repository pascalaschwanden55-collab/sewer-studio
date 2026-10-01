using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;

namespace AuswertungPro.Next.UI.Helpers;

/// <summary>
/// Entscheidet, ob gerade in ein Text- oder Auswahlfeld geschrieben wird.
/// Fensterweite Tastenkuerzel duerfen dann nicht zugreifen, sonst fehlen die
/// getippten Zeichen im Feld.
/// </summary>
public static class KeyboardTextInputFocusGuard
{
    public static bool IsTextInputFocused()
        => IsTextInput(Keyboard.FocusedElement);

    public static bool IsTextInput(IInputElement? focusedElement)
    {
        if (focusedElement is not (TextBoxBase or PasswordBox or ComboBox or ComboBoxItem))
            return false;

        // Ein ausgeblendetes oder gesperrtes Feld kann den Tastaturfokus behalten,
        // etwa das eingeklappte Eingabemarker-Feld im Player. Es darf die Kuerzel
        // danach nicht weiter stillegen.
        return focusedElement is not UIElement element
               || (element.Visibility == Visibility.Visible && element.IsEnabled);
    }
}
