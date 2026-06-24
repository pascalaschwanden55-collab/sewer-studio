using System;
using System.Windows;
using System.Windows.Controls;

namespace AuswertungPro.Next.UI.Ai;

public static class CodingMeasurementPanelControls
{
    public static void Apply(
        TextBlock q1Text,
        TextBlock q2Text,
        TextBlock clockText,
        TextBlock arcText,
        TextBlock measurementText,
        FrameworkElement panel,
        CodingOverlayMeasurementPanelState state)
    {
        ArgumentNullException.ThrowIfNull(q1Text);
        ArgumentNullException.ThrowIfNull(q2Text);
        ArgumentNullException.ThrowIfNull(clockText);
        ArgumentNullException.ThrowIfNull(arcText);
        ArgumentNullException.ThrowIfNull(measurementText);
        ArgumentNullException.ThrowIfNull(panel);
        ArgumentNullException.ThrowIfNull(state);

        q1Text.Text = state.Q1Text;
        q2Text.Text = state.Q2Text;
        clockText.Text = state.ClockText;
        arcText.Text = state.ArcText;
        measurementText.Text = state.MeasurementText;
        panel.Visibility = state.IsVisible
            ? Visibility.Visible
            : Visibility.Collapsed;
    }
}
