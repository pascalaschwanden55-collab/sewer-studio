using System.Windows.Threading;
using AuswertungPro.Next.UI.Ai;
using AuswertungPro.Next.UI.Ai.Coding;

namespace AuswertungPro.Next.UI.Player;

public sealed class CodingAiOverlayAutoHideTimerOwner
{
    private DispatcherTimer? _timer;

    public CodingAiOverlayAutoHideRequest CreateRequest()
        => new(HasTimer: _timer is not null);

    public CodingAiOverlayAutoHideHostActions CreateActions(Action clearVisuals)
    {
        ArgumentNullException.ThrowIfNull(clearVisuals);

        return new CodingAiOverlayAutoHideHostActions(
            SetTimer,
            StopTimer,
            StartTimer,
            clearVisuals);
    }

    public void SetTimer(DispatcherTimer timer)
    {
        ArgumentNullException.ThrowIfNull(timer);

        _timer = timer;
    }

    private void StopTimer()
    {
        _timer?.Stop();
    }

    private void StartTimer()
    {
        _timer?.Start();
    }
}
