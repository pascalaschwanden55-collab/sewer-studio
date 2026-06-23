using System.Reflection;
using System.Threading;
using System.Windows.Controls.Primitives;
using System.Windows.Threading;
using AuswertungPro.Next.UI.Player;

namespace AuswertungPro.Next.UI.Tests;

public sealed class PlayerWindowTimerStopperTests
{
    [Fact]
    public void StopPlaybackTimers_stops_all_player_timer_handles()
    {
        var method = FindStopPlaybackTimersMethod();
        Assert.NotNull(method);

        Exception? threadError = null;
        bool updateRunning = true;
        bool scrubRunning = true;
        bool detectionRunning = true;
        bool liveAnalysisRunning = true;
        bool liveBlinkRunning = true;
        bool osdRunning = true;

        var thread = new Thread(() =>
        {
            try
            {
                var update = StartTimer();
                var scrub = StartTimer();
                var detection = StartTimer();
                var osd = StartTimer();
                var live = new CodingLiveAiTimerController(new ToggleButton(), (_, _) => { }, () => true);
                live.Start();

                method.Invoke(null, [update, scrub, detection, live, osd]);

                updateRunning = update.IsEnabled;
                scrubRunning = scrub.IsEnabled;
                detectionRunning = detection.IsEnabled;
                liveAnalysisRunning = live.IsAnalysisTimerRunning;
                liveBlinkRunning = live.IsBlinkTimerRunning;
                osdRunning = osd.IsEnabled;
            }
            catch (TargetInvocationException ex)
            {
                threadError = ex.InnerException ?? ex;
            }
            catch (Exception ex)
            {
                threadError = ex;
            }
        });
        thread.SetApartmentState(ApartmentState.STA);
        thread.Start();
        thread.Join();

        Assert.Null(threadError);
        Assert.False(updateRunning);
        Assert.False(scrubRunning);
        Assert.False(detectionRunning);
        Assert.False(liveAnalysisRunning);
        Assert.False(liveBlinkRunning);
        Assert.False(osdRunning);
    }

    [Fact]
    public void StopPlaybackTimers_allows_missing_optional_timers()
    {
        var method = FindStopPlaybackTimersMethod();
        Assert.NotNull(method);

        Exception? threadError = null;
        bool updateRunning = true;
        bool scrubRunning = true;

        var thread = new Thread(() =>
        {
            try
            {
                var update = StartTimer();
                var scrub = StartTimer();

                method.Invoke(null, [update, scrub, null, null, null]);

                updateRunning = update.IsEnabled;
                scrubRunning = scrub.IsEnabled;
            }
            catch (TargetInvocationException ex)
            {
                threadError = ex.InnerException ?? ex;
            }
            catch (Exception ex)
            {
                threadError = ex;
            }
        });
        thread.SetApartmentState(ApartmentState.STA);
        thread.Start();
        thread.Join();

        Assert.Null(threadError);
        Assert.False(updateRunning);
        Assert.False(scrubRunning);
    }

    private static MethodInfo? FindStopPlaybackTimersMethod()
        => typeof(PlayerWindowTimerFactory).Assembly
            .GetType("AuswertungPro.Next.UI.Player.PlayerWindowTimerStopper")
            ?.GetMethod(
                "StopPlaybackTimers",
                BindingFlags.Public | BindingFlags.Static,
                binder: null,
                types:
                [
                    typeof(DispatcherTimer),
                    typeof(DispatcherTimer),
                    typeof(DispatcherTimer),
                    typeof(CodingLiveAiTimerController),
                    typeof(DispatcherTimer)
                ],
                modifiers: null);

    private static DispatcherTimer StartTimer()
    {
        var timer = new DispatcherTimer();
        timer.Start();
        return timer;
    }
}
