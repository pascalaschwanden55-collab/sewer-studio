using System;
using System.Windows.Controls;

namespace AuswertungPro.Next.UI.Ai.Coding;

public static class CodingSessionHeaderControls
{
    public static void ApplyCalibration(
        TextBlock dnText,
        TextBlock statusText,
        CodingDnCalibrationState state)
    {
        ArgumentNullException.ThrowIfNull(dnText);
        ArgumentNullException.ThrowIfNull(statusText);

        dnText.Text = state.DnText;
        statusText.Text = state.CalibrationStatusText;
    }

    public static void SetRangeText(TextBlock rangeText, double endMeter)
    {
        ArgumentNullException.ThrowIfNull(rangeText);
        rangeText.Text = $"/ {endMeter:F2}m";
    }
}
