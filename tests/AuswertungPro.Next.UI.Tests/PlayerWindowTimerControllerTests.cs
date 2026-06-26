using System.Threading;
using AuswertungPro.Next.UI.Player;

namespace AuswertungPro.Next.UI.Tests;

public sealed class PlayerWindowTimerControllerTests
{
    [Fact]
    public void Create_owns_update_and_scrub_timer_handles()
    {
        Exception? threadError = null;
        bool updateEnabledBefore = true;
        bool scrubEnabledBefore = true;
        bool updateEnabledAfterStart = false;
        bool scrubEnabledAfterStart = false;
        bool scrubEnabledAfterStop = true;

        var thread = new Thread(() =>
        {
            try
            {
                var controller = CreateController();

                updateEnabledBefore = controller.IsUpdateTimerEnabled;
                scrubEnabledBefore = controller.IsScrubTimerEnabled;

                controller.StartUpdateTimer();
                controller.StartScrubTimer();
                updateEnabledAfterStart = controller.IsUpdateTimerEnabled;
                scrubEnabledAfterStart = controller.IsScrubTimerEnabled;

                controller.StopScrubTimer();
                scrubEnabledAfterStop = controller.IsScrubTimerEnabled;
                controller.StopPlaybackTimers(null, null, null);
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
        Assert.False(updateEnabledBefore);
        Assert.False(scrubEnabledBefore);
        Assert.True(updateEnabledAfterStart);
        Assert.True(scrubEnabledAfterStart);
        Assert.False(scrubEnabledAfterStop);
    }

    [Fact]
    public void StopPlaybackTimers_stops_owned_timer_handles()
    {
        Exception? threadError = null;
        bool updateEnabledAfterStop = true;
        bool scrubEnabledAfterStop = true;

        var thread = new Thread(() =>
        {
            try
            {
                var controller = CreateController();
                controller.StartUpdateTimer();
                controller.StartScrubTimer();

                controller.StopPlaybackTimers(null, null, null);

                updateEnabledAfterStop = controller.IsUpdateTimerEnabled;
                scrubEnabledAfterStop = controller.IsScrubTimerEnabled;
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
        Assert.False(updateEnabledAfterStop);
        Assert.False(scrubEnabledAfterStop);
    }

    [Fact]
    public void Create_throws_for_null_dependencies()
    {
        Assert.Throws<ArgumentNullException>(() => PlayerWindowTimerController.Create(
            null!,
            new PlayerWindowTimerTickWorkflowActions(() => { }, () => { })));

        Assert.Throws<ArgumentNullException>(() => PlayerWindowTimerController.Create(
            () => new PlayerWindowTimerTickWorkflowRequest(false, false, false),
            null!));
    }

    private static PlayerWindowTimerController CreateController()
        => PlayerWindowTimerController.Create(
            () => new PlayerWindowTimerTickWorkflowRequest(
                IsClosing: false,
                IsPlaybackDisposed: false,
                IsDragging: false),
            new PlayerWindowTimerTickWorkflowActions(
                UpdateUi: () => { },
                ScrubSeekToSlider: () => { }));
}
