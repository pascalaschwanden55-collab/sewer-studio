using System;
using System.Threading;
using System.Windows.Controls;
using AuswertungPro.Next.UI.Ai;
using AuswertungPro.Next.UI.Ai.Vsa;

namespace AuswertungPro.Next.UI.Tests;

public sealed class VsaCodeExplorerPhotoCaptureButtonsRendererTests
{
    [Fact]
    public void Apply_deaktiviert_beide_capture_buttons_waehrend_capture_laeuft()
    {
        RunSta(() =>
        {
            var targets = CreateTargets();

            VsaCodeExplorerPhotoCaptureButtonsRenderer.Apply(
                isCaptureRunning: true,
                targets);

            Assert.False(targets.CaptureFoto1Button.IsEnabled);
            Assert.False(targets.CaptureFoto2Button.IsEnabled);
        });
    }

    [Fact]
    public void Apply_aktiviert_beide_capture_buttons_nach_capture()
    {
        RunSta(() =>
        {
            var targets = CreateTargets();
            targets.CaptureFoto1Button.IsEnabled = false;
            targets.CaptureFoto2Button.IsEnabled = false;

            VsaCodeExplorerPhotoCaptureButtonsRenderer.Apply(
                isCaptureRunning: false,
                targets);

            Assert.True(targets.CaptureFoto1Button.IsEnabled);
            Assert.True(targets.CaptureFoto2Button.IsEnabled);
        });
    }

    private static VsaCodeExplorerPhotoCaptureButtonsRenderTargets CreateTargets()
        => new(new Button(), new Button());

    private static void RunSta(Action action)
    {
        Exception? failure = null;
        var thread = new Thread(() =>
        {
            try
            {
                action();
            }
            catch (Exception ex)
            {
                failure = ex;
            }
        });

        thread.SetApartmentState(ApartmentState.STA);
        thread.Start();
        thread.Join();

        if (failure is not null)
            throw failure;
    }
}
