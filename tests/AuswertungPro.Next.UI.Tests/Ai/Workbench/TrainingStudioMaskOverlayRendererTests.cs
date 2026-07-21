using System.Threading;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using AuswertungPro.Next.Application.Ai.Workbench;
using AuswertungPro.Next.UI.Ai.Pipeline;

namespace AuswertungPro.Next.UI.Tests;

public sealed class TrainingStudioMaskOverlayRendererTests
{
    [Fact]
    public void Gueltige_Maske_wird_als_Flaeche_und_deutliche_Kontur_gezeichnet()
    {
        Exception? threadError = null;

        var thread = new Thread(() =>
        {
            try
            {
                var canvas = new Canvas();
                var segmentation = new WorkbenchSegmentation(
                    MaskRle: "0,5,2,9",
                    MaskImageWidth: 4,
                    MaskImageHeight: 4,
                    AreaPercent: 12.5,
                    StatusText: "Maske erstellt.",
                    Degraded: false);

                var result = TrainingStudioMaskOverlayRenderer.Render(
                    canvas,
                    segmentation,
                    new Rect(10, 20, 200, 100));

                Assert.True(result.Rendered);
                Assert.Null(result.ErrorMessage);
                Assert.Equal(2, canvas.Children.Count);

                var fill = Assert.IsType<System.Windows.Shapes.Path>(canvas.Children[0]);
                var contour = Assert.IsType<System.Windows.Shapes.Path>(canvas.Children[1]);
                Assert.NotNull(fill.Fill);
                Assert.Equal(72, Assert.IsType<SolidColorBrush>(fill.Fill).Color.A);
                Assert.NotNull(contour.Stroke);
                Assert.Equal(3, contour.StrokeThickness);
                Assert.Equal(new Point(10, 20), contour.RenderTransform.Transform(new Point(0, 0)));
            }
            catch (Exception ex)
            {
                threadError = ex;
            }
        });
        thread.SetApartmentState(ApartmentState.STA);
        thread.Start();
        thread.Join();

        Assert.Null(threadError);
    }

    [Fact]
    public void Leere_Maske_bleibt_unsichtbar_und_liefert_eine_klare_Meldung()
    {
        Exception? threadError = null;

        var thread = new Thread(() =>
        {
            try
            {
                var canvas = new Canvas();
                var segmentation = new WorkbenchSegmentation(
                    MaskRle: "0,16",
                    MaskImageWidth: 4,
                    MaskImageHeight: 4,
                    AreaPercent: 0,
                    StatusText: "Maske erstellt.",
                    Degraded: false);

                var result = TrainingStudioMaskOverlayRenderer.Render(
                    canvas,
                    segmentation,
                    new Rect(0, 0, 200, 100));

                Assert.False(result.Rendered);
                Assert.Contains("leere Maske", result.ErrorMessage);
                Assert.Empty(canvas.Children);
            }
            catch (Exception ex)
            {
                threadError = ex;
            }
        });
        thread.SetApartmentState(ApartmentState.STA);
        thread.Start();
        thread.Join();

        Assert.Null(threadError);
    }
}
