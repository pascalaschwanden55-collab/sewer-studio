using System.Threading;
using System.Windows.Threading;
using AuswertungPro.Next.UI.Views.Windows;

namespace AuswertungPro.Next.UI.Tests;

public sealed class PlayerDispatcherSchedulerTests
{
    [Fact]
    public void ScheduleLoaded_dispatches_action_at_loaded_priority()
    {
        RunOnStaThread(() =>
        {
            var ran = false;
            var dispatcher = Dispatcher.CurrentDispatcher;

            var operation = PlayerDispatcherScheduler.ScheduleLoaded(
                dispatcher,
                () => ran = true);
            dispatcher.Invoke(DispatcherPriority.ApplicationIdle, new Action(() => { }));

            Assert.True(ran);
            Assert.Equal(DispatcherPriority.Loaded, operation.Priority);
        });
    }

    [Fact]
    public void ScheduleInput_dispatches_action_at_input_priority()
    {
        RunOnStaThread(() =>
        {
            var ran = false;
            var dispatcher = Dispatcher.CurrentDispatcher;

            var operation = PlayerDispatcherScheduler.ScheduleInput(
                dispatcher,
                () => ran = true);
            dispatcher.Invoke(DispatcherPriority.ApplicationIdle, new Action(() => { }));

            Assert.True(ran);
            Assert.Equal(DispatcherPriority.Input, operation.Priority);
        });
    }

    [Fact]
    public void ScheduleNormal_dispatches_action_at_normal_priority()
    {
        RunOnStaThread(() =>
        {
            var ran = false;
            var dispatcher = Dispatcher.CurrentDispatcher;

            var operation = PlayerDispatcherScheduler.ScheduleNormal(
                dispatcher,
                () => ran = true);
            dispatcher.Invoke(DispatcherPriority.ApplicationIdle, new Action(() => { }));

            Assert.True(ran);
            Assert.Equal(DispatcherPriority.Normal, operation.Priority);
        });
    }

    [Fact]
    public void Invoke_dispatches_action_synchronously()
    {
        RunOnStaThread(() =>
        {
            var ran = false;

            PlayerDispatcherScheduler.Invoke(
                Dispatcher.CurrentDispatcher,
                () => ran = true);

            Assert.True(ran);
        });
    }

    [Fact]
    public void Invoke_throws_for_null_dispatcher()
    {
        Assert.Throws<ArgumentNullException>(() => PlayerDispatcherScheduler.Invoke(null!, () => { }));
    }

    [Fact]
    public void Invoke_throws_for_null_action()
    {
        RunOnStaThread(() =>
        {
            Assert.Throws<ArgumentNullException>(() => PlayerDispatcherScheduler.Invoke(
                Dispatcher.CurrentDispatcher,
                null!));
        });
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
