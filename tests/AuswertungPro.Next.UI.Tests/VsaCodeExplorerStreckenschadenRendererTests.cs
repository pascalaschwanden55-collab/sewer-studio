using System;
using System.Threading;
using System.Windows;
using System.Windows.Controls;
using AuswertungPro.Next.UI.Ai;

namespace AuswertungPro.Next.UI.Tests;

public sealed class VsaCodeExplorerStreckenschadenRendererTests
{
    [Fact]
    public void Apply_zeigt_typ_panel_und_waehlt_index()
    {
        RunSta(() =>
        {
            var targets = CreateTargets();

            VsaCodeExplorerStreckenschadenRenderer.Apply(
                new VsaCodeExplorerStreckenschadenPresentation(
                    ShowTypPanel: true,
                    SelectedTypIndex: 1),
                targets);

            Assert.Equal(Visibility.Visible, targets.TypPanel.Visibility);
            Assert.Equal(1, targets.TypList.SelectedIndex);
        });
    }

    [Fact]
    public void Apply_blendet_typ_panel_aus_ohne_index_zu_aendern()
    {
        RunSta(() =>
        {
            var targets = CreateTargets();
            targets.TypList.SelectedIndex = 1;

            VsaCodeExplorerStreckenschadenRenderer.Apply(
                new VsaCodeExplorerStreckenschadenPresentation(
                    ShowTypPanel: false,
                    SelectedTypIndex: null),
                targets);

            Assert.Equal(Visibility.Collapsed, targets.TypPanel.Visibility);
            Assert.Equal(1, targets.TypList.SelectedIndex);
        });
    }

    private static VsaCodeExplorerStreckenschadenRenderTargets CreateTargets()
    {
        var list = new ListBox();
        list.Items.Add(new ListBoxItem { Content = "Anfang" });
        list.Items.Add(new ListBoxItem { Content = "Ende" });
        return new VsaCodeExplorerStreckenschadenRenderTargets(new StackPanel(), list);
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
