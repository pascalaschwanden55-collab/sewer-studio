using System;
using System.Threading;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using AuswertungPro.Next.UI.Ai;

namespace AuswertungPro.Next.UI.Tests;

public sealed class VsaCodeExplorerBreadcrumbRendererTests
{
    [Fact]
    public void Apply_rendert_separatoren_und_breadcrumb_buttons()
    {
        RunSta(() =>
        {
            var targets = CreateTargets(_ => { });
            var presentation = new VsaCodeExplorerBreadcrumbPresentation(
                [
                    new VsaCodeExplorerBreadcrumbElement(false, "Start", 0, true, false),
                    new VsaCodeExplorerBreadcrumbElement(true, "\u203A", -1, false, false),
                    new VsaCodeExplorerBreadcrumbElement(false, "BA", 2, false, true)
                ]);

            VsaCodeExplorerBreadcrumbRenderer.Apply(presentation, targets);

            Assert.Equal(3, targets.BreadcrumbPanel.Items.Count);
            var first = Assert.IsType<Button>(targets.BreadcrumbPanel.Items[0]);
            var separator = Assert.IsType<TextBlock>(targets.BreadcrumbPanel.Items[1]);
            var current = Assert.IsType<Button>(targets.BreadcrumbPanel.Items[2]);

            Assert.Equal("Start", first.Content);
            Assert.Equal("\u203A", separator.Text);
            Assert.Equal("BA", current.Content);
            Assert.Equal(FontWeights.Normal, first.FontWeight);
            Assert.Equal(FontWeights.SemiBold, current.FontWeight);
            Assert.Same(targets.Brushes.MutedBrush, first.Foreground);
            Assert.Same(targets.Brushes.TextBrush, current.Foreground);
            Assert.Same(targets.Brushes.MutedBrush, separator.Foreground);
        });
    }

    [Fact]
    public void Apply_leert_vorherige_elemente()
    {
        RunSta(() =>
        {
            var targets = CreateTargets(_ => { });
            targets.BreadcrumbPanel.Items.Add(new TextBlock { Text = "alt" });

            VsaCodeExplorerBreadcrumbRenderer.Apply(
                new VsaCodeExplorerBreadcrumbPresentation([]),
                targets);

            Assert.Empty(targets.BreadcrumbPanel.Items);
        });
    }

    [Fact]
    public void Apply_verdrahtet_navigation_nur_fuer_navigierbare_breadcrumbs()
    {
        RunSta(() =>
        {
            var navigated = new List<int>();
            var targets = CreateTargets(navigated.Add);
            var presentation = new VsaCodeExplorerBreadcrumbPresentation(
                [
                    new VsaCodeExplorerBreadcrumbElement(false, "Start", 0, true, false),
                    new VsaCodeExplorerBreadcrumbElement(false, "Current", 1, false, true)
                ]);

            VsaCodeExplorerBreadcrumbRenderer.Apply(presentation, targets);

            var start = Assert.IsType<Button>(targets.BreadcrumbPanel.Items[0]);
            var current = Assert.IsType<Button>(targets.BreadcrumbPanel.Items[1]);

            start.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
            current.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));

            Assert.Equal([0], navigated);
        });
    }

    private static VsaCodeExplorerBreadcrumbRenderTargets CreateTargets(Action<int> navigate)
        => new(
            BreadcrumbPanel: new ItemsControl(),
            ToolbarButtonStyle: new Style(typeof(Button)),
            Brushes: new VsaCodeExplorerBreadcrumbRenderBrushes(
                TextBrush: Brushes.Black,
                MutedBrush: Brushes.Gray),
            FontFamily: new FontFamily("Consolas"),
            Navigate: navigate);

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
