using AuswertungPro.Next.UI.Controls;

namespace AuswertungPro.Next.UI.Tests;

public sealed class PipeFlightControlTests
{
    [Theory]
    [InlineData(0.0, 1.0)]
    [InlineData(1.0, 0.12 / 1.30)]
    [InlineData(-0.5, 1.0)]
    [InlineData(2.0, 0.12 / 1.30)]
    public void DepthToRadiusFactor_bildet_Tiefe_perspektivisch_ab_und_clamppt(double depth, double expected)
    {
        Assert.Equal(expected, PipeFlightControl.DepthToRadiusFactor(depth, 0.12, 1.30), precision: 10);
    }

    [Fact]
    public void DepthToRadiusFactor_faellt_monoton_mit_der_Tiefe()
    {
        var nah = PipeFlightControl.DepthToRadiusFactor(0.2, 0.12, 1.30);
        var mitte = PipeFlightControl.DepthToRadiusFactor(0.5, 0.12, 1.30);
        var fern = PipeFlightControl.DepthToRadiusFactor(0.8, 0.12, 1.30);

        Assert.True(nah > mitte && mitte > fern);
    }

    [Theory]
    [InlineData(0.0, 0.0)]
    [InlineData(1.0, 0.0)]
    [InlineData(0.5, 1.0)]
    public void DepthToAlpha_blendet_an_beiden_Enden_weich_aus(double depth, double expected)
    {
        Assert.Equal(expected, PipeFlightControl.DepthToAlpha(depth), precision: 6);
    }

    [Fact]
    public void DepthToAlpha_bleibt_im_gueltigen_Bereich()
    {
        for (var d = -0.2; d <= 1.2; d += 0.05)
        {
            var alpha = PipeFlightControl.DepthToAlpha(d);
            Assert.True(alpha is >= 0.0 and <= 1.0, $"Alpha ausserhalb 0..1 bei Tiefe {d}: {alpha}");
        }
    }
}
