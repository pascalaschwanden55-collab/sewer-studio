using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Shapes;
using AuswertungPro.Next.UI.Views.Windows;

namespace AuswertungPro.Next.UI.Tests;

public sealed class PipelinePipeRadarRendererTests
{
    [Fact]
    public void Render_zu_kleine_Flaeche_laesst_bisherige_Anzeige_unveraendert()
    {
        StaTestRunner.Run(() =>
        {
            var existing = new Border();
            var canvas = new Canvas();
            canvas.Children.Add(existing);
            var emptyText = new TextBlock { Visibility = Visibility.Visible };

            PipelinePipeRadarRenderer.Render(
                canvas,
                emptyText,
                [Detection("A", 0.9, 1)],
                PipelinePipeRadarMode.Detail,
                width: 79,
                height: 200);

            Assert.Single(canvas.Children);
            Assert.Same(existing, canvas.Children[0]);
            Assert.Equal(Visibility.Visible, emptyText.Visibility);
        });
    }

    [Fact]
    public void Render_ohne_Befunde_zeichnet_Detailgrundlage_und_zeigt_Leerhinweis()
    {
        StaTestRunner.Run(() =>
        {
            var canvas = new Canvas();
            var emptyText = new TextBlock { Visibility = Visibility.Collapsed };

            PipelinePipeRadarRenderer.Render(
                canvas,
                emptyText,
                [],
                PipelinePipeRadarMode.Detail,
                width: 320,
                height: 320);

            Assert.Equal(Visibility.Visible, emptyText.Visibility);
            Assert.Equal(22, canvas.Children.Count);
            Assert.Equal(
                new[] { "12", "3", "6", "9" },
                canvas.Children.OfType<TextBlock>().Select(x => x.Text).ToArray());
        });
    }

    [Fact]
    public void Render_Kompakt_sortiert_und_begrenzt_auf_fuenf_Befunde()
    {
        StaTestRunner.Run(() =>
        {
            var canvas = new Canvas();
            var emptyText = new TextBlock();
            var detections = DetectionsForOrdering();

            PipelinePipeRadarRenderer.Render(
                canvas,
                emptyText,
                detections,
                PipelinePipeRadarMode.Compact,
                width: 420,
                height: 320);

            Assert.Equal(Visibility.Collapsed, emptyText.Visibility);
            Assert.Equal(
                new[] { "B Treffer", "A Treffer", "C Treffer", "D Treffer", "E Treffer" },
                LabelTitles(canvas));
            Assert.DoesNotContain(canvas.Children.OfType<TextBlock>(), x => x.Text is "12" or "3" or "6" or "9");
        });
    }

    [Fact]
    public void Render_Detail_sortiert_und_begrenzt_auf_acht_Befunde()
    {
        StaTestRunner.Run(() =>
        {
            var canvas = new Canvas();
            var emptyText = new TextBlock();

            PipelinePipeRadarRenderer.Render(
                canvas,
                emptyText,
                DetectionsForOrdering(),
                PipelinePipeRadarMode.Detail,
                width: 520,
                height: 420);

            Assert.Equal(
                new[] { "B Treffer", "A Treffer", "C Treffer", "D Treffer", "E Treffer", "F Treffer", "G Treffer", "H Treffer" },
                LabelTitles(canvas));
        });
    }

    [Fact]
    public void Render_Detail_verwendet_Uhrlage_Ausdehnung_und_Hochkonfidenz_Rand()
    {
        StaTestRunner.Run(() =>
        {
            var canvas = new Canvas();
            var emptyText = new TextBlock();
            var severityColor = Color.FromRgb(10, 20, 30);
            var detection = Detection(
                "C3",
                0.90,
                1,
                positionClock: "3",
                extentPercent: 100,
                severityColor: severityColor);

            PipelinePipeRadarRenderer.Render(
                canvas,
                emptyText,
                [detection],
                PipelinePipeRadarMode.Detail,
                width: 300,
                height: 300);

            var sector = canvas.Children
                .OfType<Path>()
                .Single(path => path.Fill is SolidColorBrush brush
                    && brush.Color.R == severityColor.R
                    && brush.Color.G == severityColor.G
                    && brush.Color.B == severityColor.B);
            var geometry = Assert.IsType<PathGeometry>(sector.Data);
            var start = Assert.Single(geometry.Figures).StartPoint;
            var ringOuter = 300 * 0.385;
            Assert.Equal(150 + Math.Cos(-75 * Math.PI / 180) * ringOuter, start.X, precision: 6);
            Assert.Equal(150 + Math.Sin(-75 * Math.PI / 180) * ringOuter, start.Y, precision: 6);

            Assert.Contains(canvas.Children.OfType<Path>(), path =>
                path.Stroke is SolidColorBrush brush
                && brush.Color == Color.FromArgb(130, 233, 245, 128));

            var connector = canvas.Children
                .OfType<Line>()
                .Single(line => line.Stroke is SolidColorBrush brush
                    && brush.Color == Color.FromArgb(210, 66, 93, 51));
            Assert.Equal(266.5, connector.X1, precision: 6);
            Assert.Equal(150, connector.Y1, precision: 6);

            var label = Assert.Single(LabelTexts(canvas));
            Assert.Contains("@ 3h", label);
            Assert.Contains("/ 100%", label);
            Assert.Contains("/ 90%", label);
        });
    }

    [Fact]
    public void Render_ohne_Uhrlage_und_Ausdehnung_verteilt_Befunde_und_berechnet_Ersatzbreite()
    {
        StaTestRunner.Run(() =>
        {
            var canvas = new Canvas();
            var emptyText = new TextBlock();
            var firstColor = Color.FromRgb(11, 21, 31);
            var secondColor = Color.FromRgb(12, 22, 32);

            PipelinePipeRadarRenderer.Render(
                canvas,
                emptyText,
                [
                    Detection("A", 0.90, 1, severityColor: firstColor),
                    Detection("B", 0.80, 2, severityColor: secondColor)
                ],
                PipelinePipeRadarMode.Detail,
                width: 300,
                height: 300);

            var ringOuter = 300 * 0.385;
            AssertSectorStart(canvas, firstColor, centerDegrees: -90, sweepDegrees: 41.3, ringOuter);
            AssertSectorStart(canvas, secondColor, centerDegrees: 90, sweepDegrees: 39.1, ringOuter);
        });
    }

    [Fact]
    public void Render_kleine_gueltige_Flaeche_erzeugt_endliche_Labelpositionen()
    {
        StaTestRunner.Run(() =>
        {
            var canvas = new Canvas();
            var emptyText = new TextBlock();
            var detection = Detection(
                "SEHRLANGERBEFUNDCODE",
                0.90,
                1,
                label: "Sehr lange Befundbeschreibung",
                positionClock: "9");

            PipelinePipeRadarRenderer.Render(
                canvas,
                emptyText,
                [detection],
                PipelinePipeRadarMode.Detail,
                width: 80,
                height: 80);

            var label = Assert.Single(canvas.Children.OfType<Border>());
            Assert.True(double.IsFinite(Canvas.GetLeft(label)));
            Assert.True(double.IsFinite(Canvas.GetTop(label)));
            Assert.True(Canvas.GetLeft(label) >= 2);
            Assert.True(Canvas.GetTop(label) >= 2);
        });
    }

    private static IReadOnlyList<DetectionItem> DetectionsForOrdering()
        =>
        [
            Detection("A", 0.95, 5),
            Detection("B", 0.95, 2),
            Detection("C", 0.90, 9),
            Detection("D", 0.80, 8),
            Detection("E", 0.70, 7),
            Detection("F", 0.60, 6),
            Detection("G", 0.50, 5),
            Detection("H", 0.40, 4),
            Detection("I", 0.30, 3)
        ];

    private static DetectionItem Detection(
        string code,
        double confidence,
        double meter,
        string label = "Treffer",
        string? positionClock = null,
        int? extentPercent = null,
        Color? severityColor = null)
        => new()
        {
            Code = code,
            Label = label,
            Confidence = confidence,
            MeterStart = meter,
            MeterEnd = meter + 0.5,
            PositionClock = positionClock,
            ExtentPercent = extentPercent,
            SeverityColor = severityColor ?? Color.FromRgb(100, 120, 140)
        };

    private static string[] LabelTitles(Canvas canvas)
        => LabelTexts(canvas)
            .Select(text => text.Split('\n')[0])
            .ToArray();

    private static IEnumerable<string> LabelTexts(Canvas canvas)
        => canvas.Children
            .OfType<Border>()
            .Select(border => Assert.IsType<TextBlock>(border.Child).Text);

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
