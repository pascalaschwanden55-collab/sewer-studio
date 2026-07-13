using System.Runtime.ExceptionServices;
using System.Windows.Threading;

namespace AuswertungPro.Next.UI.Tests;

internal static class StaTestRunner
{
    public static void Run(Action action, TimeSpan? timeout = null)
    {
        ExceptionDispatchInfo? failure = null;
        var thread = new Thread(() =>
        {
            try
            {
                action();
            }
            catch (Exception ex)
            {
                failure = ExceptionDispatchInfo.Capture(ex);
            }
            finally
            {
                Dispatcher.CurrentDispatcher.InvokeShutdown();
            }
        })
        {
            IsBackground = true,
            Name = "WPF-STA-Test"
        };
        thread.SetApartmentState(ApartmentState.STA);
        thread.Start();

        var maximum = timeout ?? TimeSpan.FromSeconds(15);
        Assert.True(thread.Join(maximum), "Der WPF-Test reagiert nicht mehr.");
        failure?.Throw();
    }
}
