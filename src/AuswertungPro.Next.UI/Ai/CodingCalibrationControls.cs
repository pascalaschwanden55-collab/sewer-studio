using System;
using System.Windows;
using System.Windows.Controls;

namespace AuswertungPro.Next.UI.Ai;

public static class CodingCalibrationControls
{
    public static void ApplyToggle(
        FrameworkElement hintPanel,
        TextBlock hintText,
        CodingCalibrationToggleState state)
    {
        ArgumentNullException.ThrowIfNull(hintPanel);
        ArgumentNullException.ThrowIfNull(hintText);
        ArgumentNullException.ThrowIfNull(state);

        hintPanel.Visibility = state.ShowHint ? Visibility.Visible : Visibility.Collapsed;
        hintText.Text = state.HintText;
    }

    public static void ShowHint(TextBlock hintText, string text)
    {
        ArgumentNullException.ThrowIfNull(hintText);

        hintText.Text = text;
    }

    public static void ApplyManualResult(
        TextBlock statusText,
        TextBlock hintText,
        CodingManualCalibrationResult result)
    {
        ArgumentNullException.ThrowIfNull(statusText);
        ArgumentNullException.ThrowIfNull(hintText);
        ArgumentNullException.ThrowIfNull(result);

        statusText.Text = result.StatusText;
        hintText.Text = result.HintText;
    }

    public static void ApplyPreview(TextBlock hintText, CodingCalibrationPreviewState state)
    {
        ArgumentNullException.ThrowIfNull(hintText);
        ArgumentNullException.ThrowIfNull(state);

        hintText.Text = state.HintText;
    }

    public static void HideHint(FrameworkElement hintPanel)
    {
        ArgumentNullException.ThrowIfNull(hintPanel);

        hintPanel.Visibility = Visibility.Collapsed;
    }
}
