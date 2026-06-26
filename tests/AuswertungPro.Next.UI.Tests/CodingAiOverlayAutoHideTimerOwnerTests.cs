using System.Threading;
using System.Windows.Threading;
using AuswertungPro.Next.UI.Player;

namespace AuswertungPro.Next.UI.Tests;

public sealed class CodingAiOverlayAutoHideTimerOwnerTests
{
    [Fact]
    public void CreateRequest_reports_timer_presence()
    {
        RunOnStaThread(() =>
        {
            var owner = new CodingAiOverlayAutoHideTimerOwner();

            Assert.False(owner.CreateRequest().HasTimer);

            owner.SetTimer(new DispatcherTimer());

            Assert.True(owner.CreateRequest().HasTimer);
        });
    }

    [Fact]
    public void CreateActions_controls_timer_and_clear_visuals()
    {
        RunOnStaThread(() =>
        {
            var owner = new CodingAiOverlayAutoHideTimerOwner();
            var timer = new DispatcherTimer();
            var clearCount = 0;
            var actions = owner.CreateActions(() => clearCount++);

            actions.SetTimer(timer);
            actions.StartTimer();

            Assert.True(timer.IsEnabled);

            actions.StopTimer();
            actions.ClearVisuals();

            Assert.False(timer.IsEnabled);
            Assert.Equal(1, clearCount);
        });
    }

    [Fact]
    public void SetTimer_throws_for_null_timer()
    {
        Assert.Throws<ArgumentNullException>(() => new CodingAiOverlayAutoHideTimerOwner().SetTimer(null!));
    }

    [Fact]
    public void CreateActions_throws_for_null_clear_visuals()
    {
        Assert.Throws<ArgumentNullException>(() => new CodingAiOverlayAutoHideTimerOwner().CreateActions(null!));
    }

    private static void RunOnStaThread(Action action)
    {
        Exception? exception = null;
        var thread = new Thread(() =>
        {
            try
            {
                action();
            }
            catch (Exception ex)
            {
                exception = ex;
            }
        });
        thread.SetApartmentState(ApartmentState.STA);
        thread.Start();
        thread.Join();

        if (exception is not null)
            throw exception;
    }
}
