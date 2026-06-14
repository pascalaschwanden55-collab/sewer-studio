using AuswertungPro.Next.Domain.Models;
using AuswertungPro.Next.UI.Ai;
using Xunit;

namespace AuswertungPro.Next.UI.Tests;

public sealed class ReferenceDnGeometryTests
{
    [Fact]
    public void BuildCircleRect_BleibtRundBeiBreitemVideobild()
    {
        var rect = ReferenceDnGeometry.BuildCircleRect(
            center: new NormalizedPoint(0.5, 0.5),
            normalizedDiameter: 0.60,
            canvasWidth: 1000,
            canvasHeight: 500);

        Assert.Equal(rect.Width, rect.Height, precision: 6);
        Assert.Equal(300, rect.Width, precision: 6);
        Assert.Equal(350, rect.Left, precision: 6);
        Assert.Equal(100, rect.Top, precision: 6);
    }
}
