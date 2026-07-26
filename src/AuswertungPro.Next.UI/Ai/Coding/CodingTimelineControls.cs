using System;
using System.Collections;
using System.Windows;
using System.Windows.Input;
using AuswertungPro.Next.UI.Controls;

namespace AuswertungPro.Next.UI.Ai.Coding;

public static class CodingTimelineControls
{
    public static void Configure(
        PipeGraphTimeline timeline,
        FrameworkElement timelinePanel,
        double endMeter,
        IEnumerable markers,
        ICommand navigateToMeterCommand,
        ICommand markerClickedCommand)
    {
        ArgumentNullException.ThrowIfNull(timeline);
        ArgumentNullException.ThrowIfNull(timelinePanel);
        ArgumentNullException.ThrowIfNull(markers);
        ArgumentNullException.ThrowIfNull(navigateToMeterCommand);
        ArgumentNullException.ThrowIfNull(markerClickedCommand);

        timeline.TotalLength = endMeter;
        timeline.MeterAccessor = CodingTimelineMarkerAccessors.Meter;
        timeline.CodeAccessor = CodingTimelineMarkerAccessors.Code;
        timeline.ConfidenceAccessor = CodingTimelineMarkerAccessors.Confidence;
        timeline.IsRejectedAccessor = CodingTimelineMarkerAccessors.IsRejected;
        timeline.Markers = markers;
        timeline.NavigateToMeterCommand = navigateToMeterCommand;
        timeline.MarkerClickedCommand = markerClickedCommand;
        timelinePanel.Visibility = Visibility.Visible;
    }
}
