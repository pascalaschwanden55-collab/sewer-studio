using System;
using System.Windows.Threading;

namespace AuswertungPro.Next.UI.Player;

public sealed record PlayerWindowTimerSet(
    DispatcherTimer UpdateTimer,
    DispatcherTimer ScrubTimer);

public static class PlayerWindowTimerSetFactory
{
    public static PlayerWindowTimerSet Create(
        Func<PlayerWindowTimerTickWorkflowRequest> createRequest,
        PlayerWindowTimerTickWorkflowActions actions)
    {
        ArgumentNullException.ThrowIfNull(createRequest);
        ArgumentNullException.ThrowIfNull(actions);

        var updateTimer = PlayerWindowTimerFactory.CreateUpdateTimer(() =>
            PlayerWindowTimerTickWorkflow.ExecuteUpdate(createRequest(), actions));
        var scrubTimer = PlayerWindowTimerFactory.CreateScrubTimer(() =>
            PlayerWindowTimerTickWorkflow.ExecuteScrub(createRequest(), actions));

        return new PlayerWindowTimerSet(updateTimer, scrubTimer);
    }
}
