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

    public static void ApplyQuickSelection(TextBox inputText, string text)
    {
        ArgumentNullException.ThrowIfNull(inputText);

        inputText.Text = text;
    }

    /// <summary>Setzt Schreibfokus und Schreibmarke ans Textende.</summary>
    public static void FocusInput(TextBox inputText)
    {
        ArgumentNullException.ThrowIfNull(inputText);

        inputText.Focus();
        inputText.CaretIndex = inputText.Text?.Length ?? 0;
    }

    public static bool IsVisible(FrameworkElement popup)
    {
        ArgumentNullException.ThrowIfNull(popup);

        return popup.Visibility == Visibility.Visible;
    }

    public static string? ResolveSelectedText(object? selectedItem)
    {
        return selectedItem is ComboBoxItem { Content: string text } && !string.IsNullOrEmpty(text)
            ? text
            : null;
    }
}
