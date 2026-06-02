using AuswertungPro.Next.Application.Ai.Training;
using Xunit;

namespace AuswertungPro.Next.Pipeline.Tests;

public sealed class BoundingBoxTests
{
    [Fact]
    public void TryCreate_akzeptiert_gueltige_normierte_Box()
    {
        Assert.True(BoundingBox.TryCreate(0.5, 0.5, 0.3, 0.2, out var box));
        Assert.Equal(0.5, box.XCenter);
        Assert.Equal(0.2, box.Height);
    }

    [Theory]
    [InlineData(0.5, 0.5, 0.0, 0.2)]   // Breite 0
    [InlineData(0.5, 0.5, 0.3, -0.1)]  // negative Hoehe
    [InlineData(1.2, 0.5, 0.3, 0.2)]   // Center ausserhalb 0-1
    [InlineData(0.95, 0.5, 0.2, 0.2)]  // Box ragt rechts raus (0.95 + 0.1 > 1)
    public void TryCreate_lehnt_ungueltige_Box_ab(double xc, double yc, double w, double h)
        => Assert.False(BoundingBox.TryCreate(xc, yc, w, h, out _));

    [Fact]
    public void ApplyTo_setzt_alle_vier_Felder_und_HasBbox()
    {
        var s = new TrainingSample { SampleId = "x" };
        Assert.True(BoundingBox.TryCreate(0.4, 0.6, 0.2, 0.2, out var box));
        box.ApplyTo(s);
        Assert.True(s.HasBbox);
        Assert.Equal(0.4, s.BboxXCenter);
        Assert.Equal(0.6, s.BboxYCenter);
        Assert.Equal(0.2, s.BboxWidth);
        Assert.Equal(0.2, s.BboxHeight);
    }
}
