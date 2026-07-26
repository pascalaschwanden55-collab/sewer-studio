using System.Windows;
using AuswertungPro.Next.UI.Ai;
using AuswertungPro.Next.UI.Ai.Coding;

namespace AuswertungPro.Next.UI.Tests;

public sealed class CodingEingabemarkerGeometryPolicyTests
{
    [Fact]
    public void BuildPreviewRect_orders_drag_points()
    {
        var rect = CodingEingabemarkerGeometryPolicy.BuildPreviewRect(
            new Point(80, 70),
            new Point(20, 10));

        Assert.Equal(new Rect(20, 10, 60, 60), rect);
    }

    [Fact]
    public void BuildNormalizedSelection_orders_and_normalizes_drag_points()
    {
        var rect = CodingEingabemarkerGeometryPolicy.BuildNormalizedSelection(
            new Point(80, 70),
            new Point(20, 10),
            new Size(100, 200));

        Assert.NotNull(rect);
        Assert.Equal(0.2, rect.Value.X, precision: 6);
        Assert.Equal(0.05, rect.Value.Y, precision: 6);
        Assert.Equal(0.6, rect.Value.Width, precision: 6);
        Assert.Equal(0.3, rect.Value.Height, precision: 6);
    }

    [Theory]
    [InlineData(0, 100)]
    [InlineData(100, 0)]
    public void BuildNormalizedSelection_returns_null_for_invalid_canvas_size(
        double width,
        double height)
    {
        var rect = CodingEingabemarkerGeometryPolicy.BuildNormalizedSelection(
            new Point(10, 10),
            new Point(80, 70),
            new Size(width, height));

        Assert.Null(rect);
    }

    [Fact]
    public void BuildNormalizedSelection_returns_null_when_selection_is_too_small()
    {
        var rect = CodingEingabemarkerGeometryPolicy.BuildNormalizedSelection(
            new Point(10, 10),
            new Point(11, 50),
            new Size(100, 100));

        Assert.Null(rect);
    }
}
