using System;
using System.Windows.Threading;

namespace AuswertungPro.Next.UI.Views.Windows;

public static class PlayerDispatcherScheduler
{
    public static DispatcherOperation ScheduleLoaded(Dispatcher dispatcher, Action action)
        => Schedule(dispatcher, DispatcherPriority.Loaded, action);

    public static DispatcherOperation ScheduleInput(Dispatcher dispatcher, Action action)
        => Schedule(dispatcher, DispatcherPriority.Input, action);

    public static DispatcherOperation ScheduleNormal(Dispatcher dispatcher, Action action)
        => Schedule(dispatcher, DispatcherPriority.Normal, action);

    public static void Invoke(Dispatcher dispatcher, Action action)
    {
        ArgumentNullException.ThrowIfNull(dispatcher);
        ArgumentNullException.ThrowIfNull(action);

        dispatcher.Invoke(action);
    }

    public static bool HasAccess(Dispatcher dispatcher)
    {
        ArgumentNullException.ThrowIfNull(dispatcher);

        return dispatcher.CheckAccess();
    }

    public static bool HasShutdownStarted(Dispatcher dispatcher)
    {
        ArgumentNullException.ThrowIfNull(dispatcher);

        return dispatcher.HasShutdownStarted;
    }

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
