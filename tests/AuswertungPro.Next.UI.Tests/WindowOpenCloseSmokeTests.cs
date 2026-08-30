using System.Windows;
using System.Windows.Threading;
using AuswertungPro.Next.UI.Views.Windows;

namespace AuswertungPro.Next.UI.Tests;

public sealed class WindowOpenCloseSmokeTests
{
    [Fact]
    public void Fachfenster_lassen_sich_oeffnen_und_wieder_schliessen()
    {
        StaTestRunner.Run(() =>
        {
            OpenAndClose(new HydraulikPanelWindow());
            OpenAndClose(new FloatingGridWindow());
            OpenAndClose(new LiveFrameWindow());
            OpenAndClose(new TextPreviewWindow("Test", "Inhalt"));
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

}
