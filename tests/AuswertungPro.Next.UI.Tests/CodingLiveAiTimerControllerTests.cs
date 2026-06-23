using System.Threading;
using System.Windows.Controls.Primitives;
using System.Windows.Media;
using AuswertungPro.Next.UI.Ai;
using AuswertungPro.Next.UI.Player;

namespace AuswertungPro.Next.UI.Tests;

public sealed class CodingLiveAiTimerControllerTests
{
    [Fact]
    public void Start_sets_active_background_and_starts_timers()
    {
        Exception? threadError = null;
        bool analysisRunning = false;
        bool blinkRunning = false;
        Color backgroundColor = default;

        var thread = new Thread(() =>
        {
            try
            {
                var button = new ToggleButton();
                var controller = new CodingLiveAiTimerController(button, (_, _) => { }, () => true);

                controller.Start();

                analysisRunning = controller.IsAnalysisTimerRunning;
                blinkRunning = controller.IsBlinkTimerRunning;
                backgroundColor = Assert.IsType<SolidColorBrush>(button.Background).Color;
                controller.Stop(resetButton: true);
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
        Assert.True(analysisRunning);
        Assert.True(blinkRunning);
        Assert.Equal(CodingLiveAiButtonDisplayPolicy.ActiveColor, backgroundColor);
    }

    [Fact]
    public void Stop_clears_background_and_stops_timers()
    {
        Exception? threadError = null;
        bool analysisRunning = true;
        bool blinkRunning = true;
        bool backgroundCleared = false;

        var thread = new Thread(() =>
        {
            try
            {
                var button = new ToggleButton();
                var controller = new CodingLiveAiTimerController(button, (_, _) => { }, () => true);

                controller.Start();
                controller.Stop(resetButton: true);

                analysisRunning = controller.IsAnalysisTimerRunning;
                blinkRunning = controller.IsBlinkTimerRunning;
                backgroundCleared = button.ReadLocalValue(ToggleButton.BackgroundProperty) == System.Windows.DependencyProperty.UnsetValue;
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
        Assert.False(analysisRunning);
        Assert.False(blinkRunning);
        Assert.True(backgroundCleared);
    }

    [Fact]
    public void StopTimers_keeps_background_for_window_shutdown_path()
    {
        Exception? threadError = null;
        bool analysisRunning = true;
        bool blinkRunning = true;
        bool backgroundStillSet = false;

        var thread = new Thread(() =>
        {
            try
            {
                var button = new ToggleButton();
                var controller = new CodingLiveAiTimerController(button, (_, _) => { }, () => true);

                controller.Start();
                controller.StopTimers();

                analysisRunning = controller.IsAnalysisTimerRunning;
                blinkRunning = controller.IsBlinkTimerRunning;
                backgroundStillSet = button.ReadLocalValue(ToggleButton.BackgroundProperty) != System.Windows.DependencyProperty.UnsetValue;
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
        Assert.False(analysisRunning);
        Assert.False(blinkRunning);
        Assert.True(backgroundStillSet);
    }
}
