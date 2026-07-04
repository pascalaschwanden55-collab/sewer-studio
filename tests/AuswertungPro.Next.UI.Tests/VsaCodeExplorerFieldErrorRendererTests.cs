using System;
using System.Threading;
using System.Windows;
using System.Windows.Controls;
using AuswertungPro.Next.UI.Ai;

namespace AuswertungPro.Next.UI.Tests;

public sealed class VsaCodeExplorerFieldErrorRendererTests
{
    [Fact]
    public void Apply_setzt_text_und_zeigt_fehler()
    {
        RunSta(() =>
        {
            var target = new TextBlock { Visibility = Visibility.Collapsed };

            VsaCodeExplorerFieldErrorRenderer.Apply(
                new VsaCodeExplorerFieldErrorPresentation("Zahl fehlt", Show: true),
                target);

            Assert.Equal("Zahl fehlt", target.Text);
            Assert.Equal(Visibility.Visible, target.Visibility);
        });
    }

    [Fact]
    public void Apply_leert_text_und_versteckt_fehler()
    {
        RunSta(() =>
        {
            var target = new TextBlock
            {
                Text = "alt",
                Visibility = Visibility.Visible
            };

            VsaCodeExplorerFieldErrorRenderer.Apply(
                new VsaCodeExplorerFieldErrorPresentation("", Show: false),
                target);

            Assert.Equal("", target.Text);
            Assert.Equal(Visibility.Collapsed, target.Visibility);
        });
    }

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
