using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;

namespace AuswertungPro.Next.UI.Views.Windows;

public static class CodingEingabemarkerPopupControls
{
    public static void ShowInput(
        FrameworkElement popup,
        TextBox inputText,
        Selector quickSelection)
    {
        ArgumentNullException.ThrowIfNull(popup);
        ArgumentNullException.ThrowIfNull(inputText);
        ArgumentNullException.ThrowIfNull(quickSelection);

        popup.Visibility = Visibility.Visible;
        inputText.Text = string.Empty;
        quickSelection.SelectedIndex = -1;
    }

    public static void Hide(FrameworkElement popup)
    {
        ArgumentNullException.ThrowIfNull(popup);

        popup.Visibility = Visibility.Collapsed;
    }

    public static bool IsVisible(FrameworkElement popup)
    {
        ArgumentNullException.ThrowIfNull(popup);

        return popup.Visibility == Visibility.Visible;
    }
}
