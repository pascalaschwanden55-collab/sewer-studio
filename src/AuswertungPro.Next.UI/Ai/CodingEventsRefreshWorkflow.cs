using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using AuswertungPro.Next.Domain.Models;
using AuswertungPro.Next.UI.ViewModels.Windows;

namespace AuswertungPro.Next.UI.Ai;

public static class CodingEventsRefreshWorkflow
{
    public static bool RefreshListAndStatistics(
        ObservableCollection<CodingEvent>? events,
        CodingEventsListControls listControls,
        CodingStatisticsControls statisticsControls,
        Func<CodingEvent, DefectStatus> statusResolver)
    {
        if (events is null)
            return false;

        ArgumentNullException.ThrowIfNull(listControls);

        var sorted = CodingEventDisplayOrderPolicy.Order(events);
        listControls.ApplyOrderedEvents(events, sorted);
        RefreshStatistics(events, statisticsControls, statusResolver);
        return true;
    }

    public static bool RefreshStatistics(
        IEnumerable<CodingEvent>? events,
        CodingStatisticsControls statisticsControls,
        Func<CodingEvent, DefectStatus> statusResolver)
    {
        if (events is null)
            return false;

        ArgumentNullException.ThrowIfNull(statisticsControls);
        ArgumentNullException.ThrowIfNull(statusResolver);

        var summary = CodingStatisticsPolicy.Build(events, statusResolver);
        statisticsControls.Apply(summary);
        return true;
    }
}
