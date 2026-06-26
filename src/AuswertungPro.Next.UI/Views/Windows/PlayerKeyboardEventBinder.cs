using System;
using System.Windows;
using System.Windows.Input;

namespace AuswertungPro.Next.UI.Views.Windows;

public static class PlayerKeyboardEventBinder
{
    public static void Bind(UIElement target, KeyEventHandler previewKeyDown)
    {
        ArgumentNullException.ThrowIfNull(target);
        ArgumentNullException.ThrowIfNull(previewKeyDown);

        target.AddHandler(Keyboard.PreviewKeyDownEvent, previewKeyDown, true);
    }
}
