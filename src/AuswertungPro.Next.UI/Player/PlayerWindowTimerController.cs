using System.Windows.Threading;

namespace AuswertungPro.Next.UI.Player;

public sealed class PlayerWindowTimerController
{
    private readonly PlayerWindowTimerSet _timers;

    private PlayerWindowTimerController(PlayerWindowTimerSet timers)
    {
        _timers = timers ?? throw new ArgumentNullException(nameof(timers));
    }

    public bool IsUpdateTimerEnabled => _timers.UpdateTimer.IsEnabled;

    public bool IsScrubTimerEnabled => _timers.ScrubTimer.IsEnabled;

    public static PlayerWindowTimerController Create(
        Func<PlayerWindowTimerTickWorkflowRequest> createRequest,
        PlayerWindowTimerTickWorkflowActions actions)
    {
        ArgumentNullException.ThrowIfNull(createRequest);
        ArgumentNullException.ThrowIfNull(actions);

        return new PlayerWindowTimerController(
            PlayerWindowTimerSetFactory.Create(createRequest, actions));
    }

    public void StartUpdateTimer()
    {
        _timers.UpdateTimer.Start();
    }

    public void StartScrubTimer()
    {
        _timers.ScrubTimer.Start();
    }

    public void StopScrubTimer()
    {
        _timers.ScrubTimer.Stop();
    }

    public void StopPlaybackTimers(
        DispatcherTimer? detectionTimer,
        CodingLiveAiTimerController? codingLiveAiTimers,
        DispatcherTimer? codingOsdTimer)
        => PlayerWindowTimerStopper.StopPlaybackTimers(
            _timers.UpdateTimer,
            _timers.ScrubTimer,
            detectionTimer,
            codingLiveAiTimers,
            codingOsdTimer);
}
