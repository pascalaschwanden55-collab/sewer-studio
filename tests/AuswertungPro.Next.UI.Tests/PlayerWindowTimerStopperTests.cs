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

    [Fact]
    public void StopAndClear_stops_timer_and_returns_null()
    {
        var method = FindStopAndClearMethod();
        Assert.NotNull(method);

        Exception? threadError = null;
        bool timerRunning = true;
        object? result = new object();

        var thread = new Thread(() =>
        {
            try
            {
                var timer = StartTimer();

                result = method.Invoke(null, [timer]);

                timerRunning = timer.IsEnabled;
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
        Assert.Null(result);
        Assert.False(timerRunning);
    }

    [Fact]
    public void StopAndClear_handles_missing_timer()
    {
        var method = FindStopAndClearMethod();
        Assert.NotNull(method);

        var result = method.Invoke(null, [null]);

        Assert.Null(result);
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

    private static MethodInfo? FindStopAndClearMethod()
        => typeof(PlayerWindowTimerFactory).Assembly
            .GetType("AuswertungPro.Next.UI.Player.PlayerWindowTimerStopper")
            ?.GetMethod(
                "StopAndClear",
                BindingFlags.Public | BindingFlags.Static,
                binder: null,
                types: [typeof(DispatcherTimer)],
                modifiers: null);

    private static DispatcherTimer StartTimer()
    {
        var timer = new DispatcherTimer();
        timer.Start();
        return timer;
    }
}
