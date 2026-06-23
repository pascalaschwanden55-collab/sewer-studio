using System.Threading;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Shapes;
using AuswertungPro.Next.Domain.Models;
using AuswertungPro.Next.Infrastructure.Ai;
using AuswertungPro.Next.UI.Player;

namespace AuswertungPro.Next.UI.Tests;

public sealed class CodingActivePipeBendSchemaRendererTests
{
    [Fact]
    public void Render_adds_confirmed_bend_preview_guide_and_radius_handle()
    {
        RunOnStaThread(() =>
        {
            var canvas = new Canvas();
            var schema = new PipeBendSchema
            {
                Center = new NormalizedPoint(0.5, 0.5),
                AngleDeg = 90,
                RotationDeg = -90,
                ArmLength = 0.2
            };
            var overlay = CodingSchemaOverlayBuilder.BuildGeometry(schema);

            var rendered = CodingActivePipeBendSchemaRenderer.Render(
                canvas,
                schema,
                overlay,
                effect: null,
                toPixel: ToPixel);

            Assert.True(rendered);
            Assert.Equal(9, canvas.Children.Count);
            Assert.IsType<Line>(canvas.Children[0]);
            Assert.IsType<Line>(canvas.Children[1]);
            Assert.IsType<Ellipse>(canvas.Children[2]);
            Assert.IsType<Ellipse>(canvas.Children[3]);
            Assert.IsType<Ellipse>(canvas.Children[4]);
            Assert.IsType<Path>(canvas.Children[5]);
            Assert.IsType<TextBlock>(canvas.Children[6]);

            var guide = Assert.IsType<Line>(canvas.Children[7]);
            Assert.Equal(100, guide.X1);
            Assert.Equal(50, guide.Y1);
            Assert.Equal(100, guide.X2, 3);
            Assert.Equal(30, guide.Y2, 3);
            Assert.Equal(OverlayTags.Preview, guide.Tag);
            Assert.Equal(4, guide.StrokeDashArray[0]);
            Assert.Equal(3, guide.StrokeDashArray[1]);

            var handle = Assert.IsType<Ellipse>(canvas.Children[8]);
            Assert.Equal(OverlayTags.Preview, handle.Tag);
        });
    }

    [Fact]
    public void Render_adds_only_guide_and_radius_handle_without_confirmed_overlay()
    {
        RunOnStaThread(() =>
        {
            var canvas = new Canvas();
            var schema = new PipeBendSchema
            {
                Center = new NormalizedPoint(0.5, 0.5),
                AngleDeg = 60,
                RotationDeg = 0,
                ArmLength = 0.1
            };

            var rendered = CodingActivePipeBendSchemaRenderer.Render(
                canvas,
                schema,
                overlay: null,
                effect: null,
                toPixel: ToPixel);

            Assert.True(rendered);
            Assert.Equal(2, canvas.Children.Count);
            Assert.IsType<Line>(canvas.Children[0]);
            Assert.IsType<Ellipse>(canvas.Children[1]);
        });
    }

    private static Point ToPixel(NormalizedPoint point)
        => new(point.X * 200, point.Y * 100);

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
        });
        thread.SetApartmentState(ApartmentState.STA);
        thread.Start();
        thread.Join();

        if (exception is not null)
            throw exception;
    }
}
