using System.Runtime.ExceptionServices;
using System.Windows;
using System.Windows.Threading;
using AuswertungPro.Next.UI.Views.Windows;

namespace AuswertungPro.Next.UI.Tests;

public sealed class WindowOpenCloseSmokeTests
{
    [Fact]
    public void Einfache_fenster_lassen_sich_oeffnen_und_wieder_schliessen()
    {
        RunOnStaThread(() =>
        {
            OpenAndClose(new KarteWindow());
            OpenAndClose(new MeasureSelectionWindow());
        });
    }

    private static void OpenAndClose(Window window)
    {
        window.ShowActivated = false;
        window.ShowInTaskbar = false;
        window.WindowStartupLocation = WindowStartupLocation.Manual;
        window.Left = -10_000;
        window.Top = -10_000;

        try
        {
            window.Show();
            window.Dispatcher.Invoke(
                DispatcherPriority.ApplicationIdle,
                new Action(() => { }));
            Assert.True(window.IsVisible);
        }
        finally
        {
            window.Close();
        }

        Assert.False(window.IsVisible);
    }

    private static void RunOnStaThread(Action action)
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
            Name = "WPF-Fenster-Smoke"
        };
        thread.SetApartmentState(ApartmentState.STA);
        thread.Start();

        Assert.True(thread.Join(TimeSpan.FromSeconds(15)), "Der WPF-Fenstertest reagiert nicht mehr.");
        failure?.Throw();
    }
}
