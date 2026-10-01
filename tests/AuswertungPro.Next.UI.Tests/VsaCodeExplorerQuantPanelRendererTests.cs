using System;
using System.Threading;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using AuswertungPro.Next.UI.Ai.Vsa;

namespace AuswertungPro.Next.UI.Tests;

public sealed class VsaCodeExplorerQuantPanelRendererTests
{
    [Fact]
    public void Apply_zeigt_beide_felder_mit_einheit_und_bereich()
    {
        RunSta(() =>
        {
            var noQuant = new TextBlock { Visibility = Visibility.Visible };
            var q1Panel = new StackPanel { Visibility = Visibility.Collapsed };
            var q2Panel = new StackPanel { Visibility = Visibility.Collapsed };
            var q1Label = new TextBlock();
            var q1Unit = new TextBlock();
            var q1Range = new TextBlock();
            var q1Badge = new Border { Child = new TextBlock() };
            var q2Label = new TextBlock();
            var q2Unit = new TextBlock();
            var q2Range = new TextBlock();

            VsaCodeExplorerQuantPanelRenderer.Apply(
                new VsaCodeExplorerQuantPanelPresentation(
                    ShowNoQuant: false,
                    Q1: new VsaCodeExplorerQuantFieldPresentation(
                        ShowPanel: true,
                        LabelText: "Q1: Anschlusshöhe",
                        UnitText: "mm",
                        RangeText: "[0–10000]",
                        ShowRequiredBadge: true,
                        RequiredBadge: new VsaCodeExplorerQuantRequiredBadgePresentation(
                            "PFLICHT",
                            VsaCodeExplorerQuantBrushRole.Danger,
                            0.12)),
                    Q2: new VsaCodeExplorerQuantFieldPresentation(
                        ShowPanel: true,
                        LabelText: "Q2: Anschlussbreite",
                        UnitText: "mm",
                        RangeText: "[0–10000]",
                        ShowRequiredBadge: false,
                        RequiredBadge: null)),
                new VsaCodeExplorerQuantPanelRenderTargets(
                    noQuant,
                    q1Panel,
                    q1Label,
                    q1Unit,
                    q1Range,
                    q1Badge,
                    q2Panel,
                    q2Label,
                    q2Unit,
                    q2Range),
                new VsaCodeExplorerQuantPanelRenderBrushes(
                    Colors.Red,
                    Brushes.Red));

            Assert.Equal(Visibility.Collapsed, noQuant.Visibility);
            Assert.Equal(Visibility.Visible, q1Panel.Visibility);
            Assert.Equal("Q1: Anschlusshöhe", q1Label.Text);
            Assert.Equal("mm", q1Unit.Text);
            Assert.Equal("[0–10000]", q1Range.Text);
            Assert.Equal(Visibility.Visible, q1Badge.Visibility);
            Assert.Equal("PFLICHT", Assert.IsType<TextBlock>(q1Badge.Child).Text);

            Assert.Equal(Visibility.Visible, q2Panel.Visibility);
            Assert.Equal("Q2: Anschlussbreite", q2Label.Text);
            Assert.Equal("mm", q2Unit.Text);
            Assert.Equal("[0–10000]", q2Range.Text);
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
