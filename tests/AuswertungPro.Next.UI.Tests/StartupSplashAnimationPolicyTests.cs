using AuswertungPro.Next.UI.Views.Windows;
using System.Windows.Media;

namespace AuswertungPro.Next.UI.Tests;

public sealed class StartupSplashAnimationPolicyTests
{
    [Fact]
    public void Project_bildet_ohne_Drehung_auf_die_erwartete_Bildposition_ab()
    {
        var result = StartupSplashAnimationPolicy.Project(
            x: 0.5,
            y: -0.25,
            z: 0,
            cosY: 1,
            sinY: 0,
            cosX: 1,
            sinX: 0,
            cosZ: 1,
            sinZ: 0,
            cameraDistance: 4.6,
            projectionScale: 170,
            centerX: 220,
            centerY: 260);

        Assert.Equal(305, result.X, precision: 10);
        Assert.Equal(217.5, result.Y, precision: 10);
        Assert.Equal(0, result.Depth, precision: 10);
        Assert.Equal(1, result.Perspective, precision: 10);
    }

    [Theory]
    [InlineData(-1, 0)]
    [InlineData(0.35, 0.35)]
    [InlineData(2, 1)]
    public void Clamp01_begrenzt_Werte_auf_den_sichtbaren_Bereich(double value, double expected)
    {
        Assert.Equal(expected, StartupSplashAnimationPolicy.Clamp01(value));
    }

    [Fact]
    public void Blend_begrenzt_den_Anteil_und_mischt_die_Farbkanaele_wie_bisher()
    {
        var from = Color.FromRgb(10, 20, 30);
        var to = Color.FromRgb(110, 220, 130);

        Assert.Equal(from, StartupSplashAnimationPolicy.Blend(from, to, -0.2));
        Assert.Equal(to, StartupSplashAnimationPolicy.Blend(from, to, 1.2));
        Assert.Equal(Color.FromRgb(60, 120, 80), StartupSplashAnimationPolicy.Blend(from, to, 0.5));
    }
}
