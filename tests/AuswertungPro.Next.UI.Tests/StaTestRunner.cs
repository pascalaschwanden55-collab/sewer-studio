using System.Runtime.ExceptionServices;
using System.Windows.Threading;

namespace AuswertungPro.Next.UI.Tests;

internal static class StaTestRunner
{
    internal static readonly TimeSpan DefaultTimeout = TimeSpan.FromSeconds(60);

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

        var maximum = timeout ?? DefaultTimeout;
        Assert.True(
            thread.Join(maximum),
            $"Der WPF-Test wurde nicht innerhalb von {maximum.TotalSeconds:0.###} Sekunden beendet. " +
            "Er kann blockiert sein oder unter hoher Systemlast zu langsam laufen.");
        failure?.Throw();
    }
}
