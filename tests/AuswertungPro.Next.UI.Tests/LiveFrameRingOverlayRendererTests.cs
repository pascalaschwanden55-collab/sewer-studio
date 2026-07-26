using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Shapes;
using AuswertungPro.Next.Application.Ai;
using AuswertungPro.Next.UI.Ai;
using AuswertungPro.Next.UI.Ai.Live;

namespace AuswertungPro.Next.UI.Tests;

public sealed class LiveFrameRingOverlayRendererTests
{
    [Fact]
    public void Draw_Detail_behaelt_Stil_und_laengeren_Text_des_abgedockten_Fensters()
    {
        StaTestRunner.Run(() =>
        {
            var canvas = new Canvas();
            var finding = new LiveFrameFinding(
                "Ein sehr langer Befundtext",
                3,
                "6",
                null,
                VsaCodeHint: "BAF",
                CrossSectionReductionPercent: 20);

            LiveFrameRingOverlayRenderer.Draw(
                canvas,
                [finding],
                LiveFrameRingOverlayMode.Detail,
                width: 320,
                height: 240);

            var dot = canvas.Children.OfType<Ellipse>().Single(ellipse => ellipse.Width == 8);
            Assert.Equal(8, dot.Height);

            var label = Assert.Single(canvas.Children.OfType<Border>());
            Assert.Equal(new Thickness(5, 2, 5, 2), label.Padding);
            Assert.Equal(Color.FromArgb(228, 17, 19, 24), Assert.IsType<SolidColorBrush>(label.Background).Color);
            var text = Assert.IsType<TextBlock>(label.Child);
            Assert.Equal(11, text.FontSize);
            Assert.Equal(LiveFindingSummaryBuilder.BuildFindingLabel(finding, titleLimit: 24), text.Text);
        });
    }

    [Fact]
    public void Draw_Interactive_behaelt_Playertext_Tooltip_und_Klickweitergabe()
    {
        StaTestRunner.Run(() =>
        {
            var canvas = new Canvas();
            var finding = new LiveFrameFinding("Riss", 4, "3", null, VsaCodeHint: "BAB");
            LiveFrameFinding? clickedFinding = null;
            double? clickedTimestamp = null;

            LiveFrameRingOverlayRenderer.Draw(
                canvas,
                [finding],
                LiveFrameRingOverlayMode.Interactive,
                width: 320,
                height: 240,
                timestampSeconds: 12.5,
                onFindingClicked: (clicked, timestamp) =>
                {
                    clickedFinding = clicked;
                    clickedTimestamp = timestamp;
                });

            var label = Assert.Single(canvas.Children.OfType<Border>());
            Assert.Equal(Cursors.Hand, label.Cursor);
            Assert.Equal(LiveDetectionDisplayPolicy.BuildFindingAssignmentTooltip(finding), label.ToolTip);
            Assert.Equal(
                LiveDetectionDisplayPolicy.BuildDetectionLabel(finding),
                Assert.IsType<TextBlock>(label.Child).Text);

            label.RaiseEvent(new MouseButtonEventArgs(
                Mouse.PrimaryDevice,
                Environment.TickCount,
                MouseButton.Left)
            {
                RoutedEvent = UIElement.MouseLeftButtonDownEvent,
                Source = label
            });

            Assert.Same(finding, clickedFinding);
            Assert.Equal(12.5, clickedTimestamp);
        });
    }

    [Theory]
    [InlineData(12, -90)]
    [InlineData(3, 0)]
    [InlineData(6, 90)]
    [InlineData(9, 180)]
    public void ClockHourToAngleDegrees_bildet_Hauptuhrlagen_einheitlich_ab(int hour, double expected)
    {
        Assert.Equal(expected, LiveDetectionGeometryMapper.ClockHourToAngleDegrees(hour));
    }

    [Fact]
    public void BuildFindingLabel_mit_Limit_24_bewahrt_abgedocktes_Textformat()
    {
        var finding = new LiveFrameFinding(
            "123456789012345678901234567890",
            3,
            "12",
            null,
            VsaCodeHint: "BAB",
            CrossSectionReductionPercent: 15);

        var compact = LiveFindingSummaryBuilder.BuildFindingLabel(finding);
        var detail = LiveFindingSummaryBuilder.BuildFindingLabel(finding, titleLimit: 24);

        Assert.Contains("1234567890123456...", compact);
        Assert.Contains("12345678901234567890...", detail);
        Assert.Contains("QV:15%", detail);
    }
}
