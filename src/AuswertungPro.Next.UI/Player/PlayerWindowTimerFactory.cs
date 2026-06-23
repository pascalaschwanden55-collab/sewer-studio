using System;
using System.Windows.Threading;

namespace AuswertungPro.Next.UI.Player;

public static class PlayerWindowTimerFactory
{
    public static DispatcherTimer CreateUpdateTimer(Action onTick)
        => Create(TimeSpan.FromMilliseconds(250), onTick);

    public static DispatcherTimer CreateScrubTimer(Action onTick)
    {
        DispatcherTimer? timer = null;
        timer = Create(TimeSpan.FromMilliseconds(60), () =>
        {
            timer!.Stop();
            onTick();
        });
        return timer;
    }

    public static DispatcherTimer CreateLiveDetectionTimer(EventHandler onTick)
        => Create(TimeSpan.FromSeconds(5), onTick);

    public static DispatcherTimer CreateCodingOsdTimer(EventHandler onTick)
        => Create(TimeSpan.FromSeconds(3), onTick);

    public static DispatcherTimer CreateOneShotTimer(TimeSpan interval, Action onElapsed)
    {
        DispatcherTimer? timer = null;
        timer = Create(interval, () =>
        {
            timer!.Stop();
            onElapsed();
        });
        return timer;
    }

    private static DispatcherTimer Create(TimeSpan interval, Action onTick)
    {
        var timer = new DispatcherTimer { Interval = interval };
        timer.Tick += (_, __) => onTick();
        return timer;
    }

    private static DispatcherTimer Create(TimeSpan interval, EventHandler onTick)
    {
        var timer = new DispatcherTimer { Interval = interval };
        timer.Tick += onTick;
        return timer;
    }
}
