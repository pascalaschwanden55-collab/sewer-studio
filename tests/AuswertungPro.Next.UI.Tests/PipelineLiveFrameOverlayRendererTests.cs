using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Shapes;
using AuswertungPro.Next.Application.Ai;
using AuswertungPro.Next.UI.Views.Windows;

namespace AuswertungPro.Next.UI.Tests;

public sealed class PipelineLiveFrameOverlayRendererTests
{
    [Fact]
    public void Render_zu_kleine_Flaeche_laesst_bisherige_Anzeige_unveraendert()
    {
        StaTestRunner.Run(() =>
        {
            var existing = new Border();
            var canvas = new Canvas();
            canvas.Children.Add(existing);

            PipelineLiveFrameOverlayRenderer.Render(
                canvas,
                hasFrame: true,
                [],
                width: 59,
                height: 200);

            Assert.Single(canvas.Children);
            Assert.Same(existing, canvas.Children[0]);
        });
    }

    [Fact]
    public void Render_ohne_Frame_leert_gueltige_Zeichenflaeche()
    {
        StaTestRunner.Run(() =>
        {
            var canvas = new Canvas();
            canvas.Children.Add(new Border());

            PipelineLiveFrameOverlayRenderer.Render(
                canvas,
                hasFrame: false,
                [Finding("Riss", 3, "3", 20)],
                width: 300,
                height: 200);

            Assert.Empty(canvas.Children);
        });
    }

    [Fact]
    public void Render_mit_Frame_ohne_Befunde_zeichnet_Ringe_und_Uhrstriche()
    {
        StaTestRunner.Run(() =>
        {
            var canvas = new Canvas();

            PipelineLiveFrameOverlayRenderer.Render(
                canvas,
                hasFrame: true,
                [],
                width: 200,
                height: 100);

            Assert.Equal(14, canvas.Children.Count);
            var rings = canvas.Children.OfType<Ellipse>().ToArray();
            Assert.Equal(2, rings.Length);
            Assert.Equal(65.52, rings[0].Width, precision: 6);
            Assert.Equal(43.68, rings[1].Width, precision: 6);
            Assert.Equal(12, canvas.Children.OfType<Line>().Count());
        });
    }

    [Fact]
    public void Render_behaelt_Eingangsreihenfolge_und_zeigt_hoechstens_acht_Befunde()
    {
        StaTestRunner.Run(() =>
        {
            var canvas = new Canvas();
            var findings = Enumerable.Range(1, 9)
                .Select(index => Finding($"Befund {index}", index, index.ToString(), 10))
                .ToArray();

            PipelineLiveFrameOverlayRenderer.Render(
                canvas,
                hasFrame: true,
                findings,
                width: 500,
                height: 400);

            Assert.Equal(38, canvas.Children.Count);
            Assert.Equal(
                findings.Take(8).Select(finding => LiveFindingSummaryBuilder.BuildFindingLabel(finding)),
                LabelTexts(canvas));
            Assert.Equal(8, canvas.Children.OfType<Path>().Count());
        });
    }

    [Fact]
    public void Render_verwendet_Uhrlage_Ausdehnung_Farbe_und_Kompaktstil()
    {
        StaTestRunner.Run(() =>
        {
            var canvas = new Canvas();
            var finding = Finding("Kritischer Riss", 5, "3", 100, "BAB", 12, 8, 20);

            PipelineLiveFrameOverlayRenderer.Render(
                canvas,
                hasFrame: true,
                [finding],
                width: 300,
                height: 200);

            var sector = Assert.Single(canvas.Children.OfType<Path>());
            var fill = Assert.IsType<SolidColorBrush>(sector.Fill);
            var stroke = Assert.IsType<SolidColorBrush>(sector.Stroke);
            Assert.Equal(Color.FromArgb(98, 239, 68, 68), fill.Color);
            Assert.Equal(Color.FromArgb(220, 239, 68, 68), stroke.Color);

            var geometry = Assert.IsType<PathGeometry>(sector.Data);
            var start = Assert.Single(geometry.Figures).StartPoint;
            var ringOuter = 200 * 0.78 * 0.42;
            Assert.Equal(150 + Math.Cos(-80 * Math.PI / 180) * ringOuter, start.X, precision: 6);
            Assert.Equal(100 + Math.Sin(-80 * Math.PI / 180) * ringOuter, start.Y, precision: 6);

            var dot = canvas.Children.OfType<Ellipse>().Single(ellipse => ellipse.Width == 7);
            Assert.Equal(7, dot.Height);
            Assert.Equal(150 + ringOuter + 2 - 3.5, Canvas.GetLeft(dot), precision: 6);
            Assert.Equal(100 - 3.5, Canvas.GetTop(dot), precision: 6);

            var label = Assert.Single(canvas.Children.OfType<Border>());
            Assert.Equal(new Thickness(4, 2, 4, 2), label.Padding);
            Assert.Equal(Color.FromArgb(228, 14, 19, 28), Assert.IsType<SolidColorBrush>(label.Background).Color);
            var text = Assert.IsType<TextBlock>(label.Child);
            Assert.Equal(10, text.FontSize);
            Assert.Equal(LiveFindingSummaryBuilder.BuildFindingLabel(finding), text.Text);
        });
    }

    [Fact]
    public void Render_ohne_Uhrlage_verteilt_Befunde_und_nutzt_18_Grad()
    {
        StaTestRunner.Run(() =>
        {
            var canvas = new Canvas();

            PipelineLiveFrameOverlayRenderer.Render(
                canvas,
                hasFrame: true,
                [Finding("A", 1, null, null), Finding("B", 2, null, null)],
                width: 300,
                height: 300);

            var ringOuter = 300 * 0.78 * 0.42;
            AssertSectorStart(canvas, Color.FromRgb(34, 197, 94), -90, 18, ringOuter);
            AssertSectorStart(canvas, Color.FromRgb(132, 204, 22), 90, 18, ringOuter);
        });
    }

    [Fact]
    public void Render_verteilt_acht_sichtbare_Befunde_unabhaengig_vom_neunten()
    {
        StaTestRunner.Run(() =>
        {
            var canvas = new Canvas();
            var findings = Enumerable.Range(1, 9)
                .Select(index => Finding($"Befund {index}", 1, null, null))
                .ToArray();

            PipelineLiveFrameOverlayRenderer.Render(
                canvas,
                hasFrame: true,
                findings,
                width: 300,
                height: 300);

            var sectors = canvas.Children.OfType<Path>().ToArray();
            Assert.Equal(8, sectors.Length);
            var lastGeometry = Assert.IsType<PathGeometry>(sectors[7].Data);
            var lastStart = Assert.Single(lastGeometry.Figures).StartPoint;
            var ringOuter = 300 * 0.78 * 0.42;
            var startRadians = (225 - 9) * Math.PI / 180.0;
            Assert.Equal(150 + Math.Cos(startRadians) * ringOuter, lastStart.X, precision: 6);
            Assert.Equal(150 + Math.Sin(startRadians) * ringOuter, lastStart.Y, precision: 6);
        });
    }

    [Fact]
    public void Render_kleine_gueltige_Flaeche_erzeugt_endliche_Labelpositionen()
    {
        StaTestRunner.Run(() =>
        {
            var canvas = new Canvas();

            PipelineLiveFrameOverlayRenderer.Render(
                canvas,
                hasFrame: true,
                [Finding(new string('X', 200), 5, "9", 100, "SEHRLANGERBEFUNDCODE")],
                width: 60,
                height: 60);

            var label = Assert.Single(canvas.Children.OfType<Border>());
            Assert.True(double.IsFinite(Canvas.GetLeft(label)));
            Assert.True(double.IsFinite(Canvas.GetTop(label)));
            Assert.True(Canvas.GetLeft(label) >= 2);
            Assert.True(Canvas.GetTop(label) >= 2);
        });
    }

    private static LiveFrameFinding Finding(
        string label,
        int severity,
        string? clock,
        int? extent,
        string? code = null,
        int? height = null,
        int? intrusion = null,
        int? crossSectionReduction = null)
        => new(
            label,
            severity,
            clock,
            extent,
            VsaCodeHint: code,
            HeightMm: height,
            IntrusionPercent: intrusion,
            CrossSectionReductionPercent: crossSectionReduction);

    private static string[] LabelTexts(Canvas canvas)
        => canvas.Children
            .OfType<Border>()
            .Select(border => Assert.IsType<TextBlock>(border.Child).Text)
            .ToArray();

    private static void AssertSectorStart(
        Canvas canvas,
        Color color,
        double centerDegrees,
        double sweepDegrees,
        double ringOuter)
    {
        var sector = canvas.Children
            .OfType<Path>()
            .Single(path => path.Fill is SolidColorBrush brush
                && brush.Color.R == color.R
                && brush.Color.G == color.G
                && brush.Color.B == color.B);
        var geometry = Assert.IsType<PathGeometry>(sector.Data);
        var start = Assert.Single(geometry.Figures).StartPoint;
        var startRadians = (centerDegrees - sweepDegrees / 2.0) * Math.PI / 180.0;

        Assert.Equal(150 + Math.Cos(startRadians) * ringOuter, start.X, precision: 6);
        Assert.Equal(150 + Math.Sin(startRadians) * ringOuter, start.Y, precision: 6);
    }
}
