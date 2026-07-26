using System;
using System.Windows.Controls;
using AuswertungPro.Next.UI.Controls;

namespace AuswertungPro.Next.UI.Ai.Coding;

public static class CodingMeterTimelineControls
{
    public static void Apply(
        TextBlock meterText,
        PipeGraphTimeline timeline,
        double currentMeter)
    {
        ArgumentNullException.ThrowIfNull(meterText);
        ArgumentNullException.ThrowIfNull(timeline);

        SetText(meterText, currentMeter);
        timeline.CurrentMeter = currentMeter;
    }

    public static void SetText(TextBlock meterText, double currentMeter)
    {
        ArgumentNullException.ThrowIfNull(meterText);
        meterText.Text = $"{currentMeter:F2}m";
    }
}
