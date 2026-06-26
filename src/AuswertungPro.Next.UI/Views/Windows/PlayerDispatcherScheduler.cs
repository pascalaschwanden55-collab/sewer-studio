using System;
using System.Windows.Threading;

namespace AuswertungPro.Next.UI.Views.Windows;

public static class PlayerDispatcherScheduler
{
    public static DispatcherOperation ScheduleLoaded(Dispatcher dispatcher, Action action)
        => Schedule(dispatcher, DispatcherPriority.Loaded, action);

    public static DispatcherOperation ScheduleInput(Dispatcher dispatcher, Action action)
        => Schedule(dispatcher, DispatcherPriority.Input, action);

    private static DispatcherOperation Schedule(
        Dispatcher dispatcher,
        DispatcherPriority priority,
        Action action)
    {
        ArgumentNullException.ThrowIfNull(dispatcher);
        ArgumentNullException.ThrowIfNull(action);

        return dispatcher.BeginInvoke(priority, action);
    }
}
