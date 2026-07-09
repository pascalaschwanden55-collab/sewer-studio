using System;
using System.Linq;
using System.Threading;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Shapes;
using System.Windows.Threading;
using AuswertungPro.Next.UI.Controls;

namespace AuswertungPro.Next.UI.Tests;

public sealed class CockpitChartControlTests
{
    [Fact]
    public void DonutChart_rendert_center_text_und_label()
    {
        RunOnStaThread(() =>
        {
            var chart = new DonutChart
            {
                Width = 170,
                Height = 170,
                CenterText = "3",
                CenterLabel = "Haltungen",
                ItemsSource = new[] { new { Key = "0", Label = "Z0", Count = 3, Percent = 100d } }
            };

            var centerPanel = Assert.IsType<StackPanel>(chart.Children[^1]);
            var labels = centerPanel.Children.OfType<TextBlock>().Select(t => t.Text).ToArray();

            Assert.Equal(new[] { "3", "Haltungen" }, labels);
        });
    }

    [Fact]
    public void DonutChart_nutzt_borderlightbrush_fuer_empty_ring()
    {
        RunOnStaThread(() =>
        {
            var expected = new SolidColorBrush(Color.FromRgb(1, 2, 3));
            var chart = new DonutChart
            {
                Width = 170,
                Height = 170,
                ItemsSource = Array.Empty<object>()
            };
            chart.Resources["BorderLightBrush"] = expected;
            chart.ItemsSource = new object[] { };

            var ellipse = Assert.IsType<Ellipse>(Assert.Single(chart.Children));

            Assert.Same(expected, ellipse.Stroke);
        });
    }

    [Fact]
    public void CategoryBars_nutzt_borderlightbrush_fuer_track()
    {
        RunOnStaThread(() =>
        {
            var expected = new SolidColorBrush(Color.FromRgb(4, 5, 6));
            var bars = new CategoryBars
            {
                ItemsSource = new[] { new { Key = "BAB", Label = "BAB", Count = 2, Percent = 50d } }
            };
            bars.Resources["BorderLightBrush"] = expected;
            bars.ItemsSource = new[] { new { Key = "BAB", Label = "BAB", Count = 2, Percent = 50d } };

            var row = Assert.IsType<Grid>(Assert.Single(bars.Children));
            var track = row.Children.OfType<Grid>().Single();
            var background = Assert.IsType<Border>(track.Children[0]);

            Assert.Same(expected, background.Background);
        });
    }

    private static void RunOnStaThread(Action action)
    {
        Exception? exception = null;
        var thread = new Thread(() =>
        {
            try
            {
                action();
            }
            catch (Exception ex)
            {
                exception = ex;
            }
            finally
            {
                Dispatcher.CurrentDispatcher.InvokeShutdown();
            }
        });
        thread.SetApartmentState(ApartmentState.STA);
        thread.Start();
        thread.Join();

        if (exception is not null)
            throw exception;
    }
}
