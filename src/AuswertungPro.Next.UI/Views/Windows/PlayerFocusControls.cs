using System;
using System.Windows;
using System.Windows.Input;

namespace AuswertungPro.Next.UI.Views.Windows;

public static class PlayerFocusControls
{
    public static bool FocusElement(UIElement element)
    {
        ArgumentNullException.ThrowIfNull(element);

        return element.Focus();
    }

    public static IInputElement? FocusWindowKeyboard(Window window)
    {
        ArgumentNullException.ThrowIfNull(window);

        window.Focus();
        return Keyboard.Focus(window);
    }

    public static bool ActivateWindow(Window window)
    {
        ArgumentNullException.ThrowIfNull(window);

        return window.Activate();
    }
}
