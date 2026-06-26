using System.Threading;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Shapes;
using AuswertungPro.Next.Domain.Models;
using AuswertungPro.Next.UI.Player;

namespace AuswertungPro.Next.UI.Tests;

public sealed class CodingOverlayRenderControllerTests
{
    [Fact]
    public void RenderOverlayGeometry_uses_surface_dimensions_and_coordinate_mapper()
    {
        RunOnStaThread(() =>
        {
            var surface = new TestOverlaySurface(width: 200, height: 100);
            var mapper = new TestOverlayCoordinateMapper(width: 200, height: 100);
            var controller = new CodingOverlayRenderController(surface, mapper);
            var overlay = new OverlayGeometry
            {
                ToolType = OverlayToolType.Rectangle,
                Q1Mm = 12.3,
                Points =
                [
                    new NormalizedPoint(0.2, 0.2),
                    new NormalizedPoint(0.6, 0.2),
                    new NormalizedPoint(0.6, 0.5),
                    new NormalizedPoint(0.2, 0.5)
                ]
            };

            var rendered = controller.RenderOverlayGeometry(
                overlay,
                isPreview: false,
                labelAnchor: new NormalizedPoint(0.4, 0.4),
                calibration: null);

            Assert.True(rendered);
            Assert.True(mapper.CallCount > 0);
            Assert.Equal(2, surface.Canvas.Children.Count);
            Assert.Equal(OverlayTags.Manual, Assert.IsType<Rectangle>(surface.Canvas.Children[0]).Tag);
            Assert.Equal(OverlayTags.Manual, Assert.IsType<TextBlock>(surface.Canvas.Children[1]).Tag);
        });
    }

    [Fact]
    public void ClearTransient_delegates_to_surface()
    {
        RunOnStaThread(() =>
        {
            var surface = new TestOverlaySurface(width: 200, height: 100);
            var controller = new CodingOverlayRenderController(
                surface,
                new TestOverlayCoordinateMapper(width: 200, height: 100));

            controller.ClearTransient(clearManualOverlay: true);

            Assert.True(surface.LastClearManualOverlay);
        });
    }

    [Fact]
    public void RenderCalibrationPreview_maps_points_renders_line_and_returns_preview_state()
    {
        RunOnStaThread(() =>
        {
            var surface = new TestOverlaySurface(width: 200, height: 100);
            var mapper = new TestOverlayCoordinateMapper(width: 200, height: 100);
            var controller = new CodingOverlayRenderController(surface, mapper);

            var preview = controller.RenderCalibrationPreview(
                new NormalizedPoint(0.1, 0.2),
                new NormalizedPoint(0.4, 0.6));

            var line = Assert.IsType<Line>(Assert.Single(surface.Canvas.Children));
            Assert.Equal(2, mapper.CallCount);
            Assert.Equal(new Point(20, 20), preview.Start);
            Assert.Equal(new Point(80, 60), preview.End);
            Assert.Equal(OverlayTags.Preview, line.Tag);
            Assert.Equal(preview.Start.X, line.X1);
            Assert.Equal(preview.End.Y, line.Y2);
        });
    }

    private sealed class TestOverlaySurface : IOverlaySurface
    {
        public TestOverlaySurface(double width, double height)
        {
            Width = width;
            Height = height;
        }

        public Canvas Canvas { get; } = new();
        public double Width { get; }
        public double Height { get; }
        public bool? LastClearManualOverlay { get; private set; }

        public void ClearTransient(bool clearManualOverlay)
            => LastClearManualOverlay = clearManualOverlay;
    }

    private sealed class TestOverlayCoordinateMapper : IOverlayCoordinateMapper
    {
        private readonly double _width;
        private readonly double _height;

        public TestOverlayCoordinateMapper(double width, double height)
        {
            _width = width;
            _height = height;
        }

        public int CallCount { get; private set; }

        public Point ToPixel(NormalizedPoint point)
        {
            CallCount++;
            return new Point(point.X * _width, point.Y * _height);
        }
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
        });
        thread.SetApartmentState(ApartmentState.STA);
        thread.Start();
        thread.Join();

        if (exception is not null)
            throw exception;
    }
}
