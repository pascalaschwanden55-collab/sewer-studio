using System.Windows.Input;
using AuswertungPro.Next.Domain.Models;
using CommunityToolkit.Mvvm.Input;

namespace AuswertungPro.Next.UI.Ai;

internal sealed record CodingTimelineCommandBindings(
    Func<bool> HasCodingSessionService,
    Func<bool> IsRunningOrPaused,
    Action<double> MoveToMeter,
    Action MarkNavigationPending,
    Action SyncVideoToCodingMeter,
    Action<CodingEvent> JumpToDefect,
    Action<CodingEvent> SelectEvent);

internal sealed record CodingTimelineCommands(
    ICommand NavigateToMeter,
    ICommand MarkerClicked);

internal static class CodingTimelineCommandFactory
{
    internal static CodingTimelineCommands Create(CodingTimelineCommandBindings bindings)
    {
        ArgumentNullException.ThrowIfNull(bindings);
        ArgumentNullException.ThrowIfNull(bindings.HasCodingSessionService);
        ArgumentNullException.ThrowIfNull(bindings.IsRunningOrPaused);
        ArgumentNullException.ThrowIfNull(bindings.MoveToMeter);
        ArgumentNullException.ThrowIfNull(bindings.MarkNavigationPending);
        ArgumentNullException.ThrowIfNull(bindings.SyncVideoToCodingMeter);
        ArgumentNullException.ThrowIfNull(bindings.JumpToDefect);
        ArgumentNullException.ThrowIfNull(bindings.SelectEvent);

        return new CodingTimelineCommands(
            new RelayCommand<double>(meter =>
                CodingTimelineCommandWorkflow.NavigateToMeter(
                    new CodingTimelineNavigateRequest(
                        bindings.HasCodingSessionService(),
                        bindings.IsRunningOrPaused(),
                        meter),
                    new CodingTimelineNavigateActions(
                        bindings.MoveToMeter,
                        bindings.MarkNavigationPending,
                        bindings.SyncVideoToCodingMeter))),
            new RelayCommand<object>(item =>
                CodingTimelineCommandWorkflow.MarkerClicked(
                    item,
                    new CodingTimelineMarkerActions(
                        bindings.JumpToDefect,
                        bindings.SelectEvent))));
    }
}
