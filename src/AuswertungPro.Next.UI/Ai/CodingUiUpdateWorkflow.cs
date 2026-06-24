using System;
using AuswertungPro.Next.UI.ViewModels.Windows;

namespace AuswertungPro.Next.UI.Ai;

public sealed record CodingUiUpdateActions(
    Action ApplyMeterTimeline,
    Action SyncVideoToCodingMeter,
    Action UpdateOverlayInfo,
    Action UpdateCurrentCode,
    Action UpdateStatistics);

public sealed record CodingUiUpdateResult(bool NavigationPending);

public static class CodingUiUpdateWorkflow
{
    public static CodingUiUpdateResult Apply(
        string? propertyName,
        bool navigationPending,
        CodingUiUpdateActions actions)
    {
        ArgumentNullException.ThrowIfNull(actions);

        actions.ApplyMeterTimeline();

        if (propertyName is nameof(CodingSessionViewModel.CurrentMeter) && navigationPending)
        {
            navigationPending = false;
            actions.SyncVideoToCodingMeter();
        }

        actions.UpdateOverlayInfo();
        actions.UpdateCurrentCode();

        if (CodingStatisticsRefreshPolicy.ShouldRefresh(propertyName))
            actions.UpdateStatistics();

        return new CodingUiUpdateResult(navigationPending);
    }
}
