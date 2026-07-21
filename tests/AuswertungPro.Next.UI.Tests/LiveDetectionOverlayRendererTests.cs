using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Shapes;
using AuswertungPro.Next.Application.Ai;
using AuswertungPro.Next.UI.Ai;

namespace AuswertungPro.Next.UI.Tests;

public sealed class LiveDetectionOverlayRendererTests
{
    [Fact]
    public void Render_ohne_Box_verwendet_klickbaren_Ringfallback()
    {
        StaTestRunner.Run(() =>
        {
            var canvas = ArrangedCanvas(320, 240);
            var finding = new LiveFrameFinding("Riss", 4, "3", 20, VsaCodeHint: "BAB");
            LiveFrameFinding? clicked = null;
            double? timestamp = null;

            LiveDetectionOverlayRenderer.Render(
                canvas,
                [finding],
                timestampSec: 7.5,
                (actual, seconds) =>
                {
                    clicked = actual;
                    timestamp = seconds;
                });

            Assert.Equal(17, canvas.Children.Count);
            var label = Assert.Single(canvas.Children.OfType<Border>());
            RaiseLeftClick(label);
            Assert.Same(finding, clicked);
            Assert.Equal(7.5, timestamp);
        });
    }

    [Fact]
    public void Render_gemischte_Boxen_behaelt_Boxmarken_und_Ringbefund()
    {
        StaTestRunner.Run(() =>
        {
            var canvas = ArrangedCanvas(320, 240);
            var boxed = new LiveFrameFinding(
                "Bruch",
                5,
                "12",
                30,
                VsaCodeHint: "BAC",
                BboxX1: 0.2,
                BboxY1: 0.2,
                BboxX2: 0.5,
                BboxY2: 0.6);
            var ring = new LiveFrameFinding("Wurzel", 3, "6", 10, VsaCodeHint: "BBA");
            var clicked = new List<LiveFrameFinding>();

            LiveDetectionOverlayRenderer.Render(
                canvas,
                [boxed, ring],
                timestampSec: 9,
                (finding, _) => clicked.Add(finding));

            Assert.Equal(8, canvas.Children.OfType<Line>().Count());
            Assert.Single(canvas.Children.OfType<Path>());
            Assert.Single(canvas.Children.OfType<Ellipse>());
            var labels = canvas.Children.OfType<Border>().ToArray();
            Assert.Equal(2, labels.Length);
            Assert.Equal(12, canvas.Children.Count);

            RaiseLeftClick(labels[0]);
            RaiseLeftClick(labels[1]);
            Assert.Equal(new[] { boxed, ring }, clicked);
        });
    }

    [Fact]
    public void Render_kleine_Boxflaeche_mit_langem_Label_bleibt_stabil()
    {
        StaTestRunner.Run(() =>
        {
            var canvas = ArrangedCanvas(60, 60);
            var finding = new LiveFrameFinding(
                new string('X', 200),
                5,
                "3",
                100,
                VsaCodeHint: new string('Y', 100),
                BboxX1: 0.1,
                BboxY1: 0.1,
                BboxX2: 0.9,
                BboxY2: 0.9);

            LiveDetectionOverlayRenderer.Render(canvas, [finding], 0, (_, _) => { });

            var label = Assert.Single(canvas.Children.OfType<Border>());
            Assert.True(double.IsFinite(Canvas.GetLeft(label)));
            Assert.True(double.IsFinite(Canvas.GetTop(label)));
            Assert.True(Canvas.GetLeft(label) >= 2);
            Assert.True(Canvas.GetTop(label) >= 2);
        });
    }

    private static Canvas ArrangedCanvas(double width, double height)
    {
        var canvas = new Canvas { Width = width, Height = height };
        canvas.Measure(new Size(width, height));
        canvas.Arrange(new Rect(0, 0, width, height));
        return canvas;
    }

    private static void RaiseLeftClick(UIElement element)
    {
        element.RaiseEvent(new MouseButtonEventArgs(
            Mouse.PrimaryDevice,
            Environment.TickCount,
            MouseButton.Left)
        {
            RoutedEvent = UIElement.MouseLeftButtonDownEvent,
            Source = element
        });
    }
}
