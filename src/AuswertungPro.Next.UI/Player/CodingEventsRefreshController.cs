using AuswertungPro.Next.Application.Ai;
using AuswertungPro.Next.Domain.Models;
using AuswertungPro.Next.UI.Ai;

namespace AuswertungPro.Next.UI.Player;

public sealed record CodingEventsRefreshControllerActions(
    Action<Action> ScheduleLoaded,
    Action ColorizeListItems);

public sealed class CodingEventsRefreshController
{
    private readonly ICodingSessionHost _sessionHost;
    private readonly CodingEventsListControls _listControls;
    private readonly CodingStatisticsControls _statisticsControls;
    private readonly Func<CodingEvent, DefectStatus> _statusResolver;
    private readonly CodingEventsRefreshControllerActions _actions;

    public CodingEventsRefreshController(
        ICodingSessionHost sessionHost,
        CodingEventsListControls listControls,
        CodingStatisticsControls statisticsControls,
        Func<CodingEvent, DefectStatus> statusResolver,
        CodingEventsRefreshControllerActions actions)
    {
        ArgumentNullException.ThrowIfNull(sessionHost);
        ArgumentNullException.ThrowIfNull(listControls);
        ArgumentNullException.ThrowIfNull(statisticsControls);
        ArgumentNullException.ThrowIfNull(statusResolver);
        ArgumentNullException.ThrowIfNull(actions);
        ArgumentNullException.ThrowIfNull(actions.ScheduleLoaded);
        ArgumentNullException.ThrowIfNull(actions.ColorizeListItems);

        _sessionHost = sessionHost;
        _listControls = listControls;
        _statisticsControls = statisticsControls;
        _statusResolver = statusResolver;
        _actions = actions;
    }

    public CodingEventsListRefreshCommandResult RefreshList()
        => CodingEventsListRefreshCommandWorkflow.Execute(
            new CodingEventsListRefreshCommandActions(
                RefreshListAndStatistics: () => CodingEventsRefreshWorkflow.RefreshListAndStatistics(
                    _sessionHost.EventCollection,
                    _listControls,
                    _statisticsControls,
                    _statusResolver),
                ScheduleColorize: () => _actions.ScheduleLoaded(_actions.ColorizeListItems)));

    public CodingStatisticsUpdateCommandResult RefreshStatistics()
        => CodingStatisticsUpdateCommandWorkflow.Execute(
            new CodingStatisticsUpdateCommandRequest(_sessionHost.HasViewModel),
            new CodingStatisticsUpdateCommandActions(
                RefreshStatistics: () => CodingEventsRefreshWorkflow.RefreshStatistics(
                    _sessionHost.Events,
                    _statisticsControls,
                    _statusResolver)));
}
