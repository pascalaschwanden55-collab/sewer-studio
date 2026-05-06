using System;
using System.Linq;
using System.Windows;
using AuswertungPro.Next.UI.Ai;
using Xunit;

namespace AuswertungPro.Next.Pipeline.Tests;

public class PhotoAssistantToolsTests
{
    [Fact]
    public void BuildClockHourLines_LiefertGenau12Strahlen()
    {
        var center = new Point(100, 100);
        var lines = PhotoAssistantTools.BuildClockHourLines(center, 50, innerRatio: 0.85);
        Assert.Equal(12, lines.Count);
    }

    [Fact]
    public void BuildClockHourLines_12UhrIstObenDhYNegativ()
    {
        var center = new Point(100, 100);
        var lines = PhotoAssistantTools.BuildClockHourLines(center, 50, innerRatio: 0.0);
        // Index 0 = 12 Uhr (oben) - displayHour wird auf 12 gemappt
        var twelve = lines[0];
        Assert.Equal(12, twelve.Hour);
        Assert.True(twelve.Outer.Y < center.Y, "12 Uhr muss oberhalb des Zentrums liegen (Y < center.Y)");
    }

    [Fact]
    public void BuildClockHourLines_3UhrIstRechts()
    {
        var center = new Point(100, 100);
        var lines = PhotoAssistantTools.BuildClockHourLines(center, 50, innerRatio: 0.0);
        var three = lines.First(l => l.Hour == 3);
        Assert.True(three.Outer.X > center.X, "3 Uhr muss rechts vom Zentrum liegen");
        Assert.True(Math.Abs(three.Outer.Y - center.Y) < 0.001, "3 Uhr ist auf der horizontalen Mittelachse");
    }

    [Fact]
    public void BuildClockHourLines_6UhrIstUnten()
    {
        var center = new Point(100, 100);
        var lines = PhotoAssistantTools.BuildClockHourLines(center, 50, innerRatio: 0.0);
        var six = lines.First(l => l.Hour == 6);
        Assert.True(six.Outer.Y > center.Y, "6 Uhr muss unterhalb des Zentrums liegen");
    }

    [Fact]
    public void Build3DPipeOverlay_LiefertVorderUndHintereEllipsenpunkte()
    {
        var pts = PhotoAssistantTools.Build3DPipeOverlay(
            centerFront: new Point(100, 100), radiusFront: 50,
            centerBack:  new Point(100, 100), radiusBack:  30,
            segments: 16);
        Assert.Equal(32, pts.Count); // 16 vorne + 16 hinten
    }

    [Fact]
    public void Build3DJointOffset_VersatzVektorIstNichtNull()
    {
        var (r1, r2, a, b) = PhotoAssistantTools.Build3DJointOffset(
            ring1Center: new Point(100, 100), ring1Radius: 50,
            ring2Center: new Point(120, 100), ring2Radius: 50);
        Assert.NotEmpty(r1);
        Assert.NotEmpty(r2);
        Assert.NotEqual(a, b); // Versatz vorhanden
    }

    [Fact]
    public void MeasurePipeSurfaceDistance_ZweiPunkteAufRohrumfang()
    {
        // 90° Bogen auf einem DN500-Rohr -> Bogenlaenge = π/2 * 250 = 392.7 mm
        var pipeCenter = new Point(0, 0);
        var pipeRadius = 100;
        var a = new Point(100, 0);  // 0 Uhr
        var b = new Point(0, 100);  // 3 Uhr (90°)
        var dist = PhotoAssistantTools.MeasurePipeSurfaceDistanceMm(a, b, pipeCenter, pipeRadius, pipeDiameterMm: 500);
        // erwartet: arc = π/2 * 250 ≈ 392.7 mm (axialer Versatz = 0)
        Assert.InRange(dist, 390, 395);
    }

    [Fact]
    public void UndistortBarrel_LiefertGleichgrossesBild()
    {
        var src = new System.Windows.Media.Imaging.WriteableBitmap(
            64, 64, 96, 96, System.Windows.Media.PixelFormats.Bgra32, null);
        var result = PhotoAssistantTools.UndistortBarrel(src, k1: -0.1);
        Assert.Equal(64, result.PixelWidth);
        Assert.Equal(64, result.PixelHeight);
    }

    [Fact]
    public void ScaleImage_FaktorNullWirftException()
    {
        var src = new System.Windows.Media.Imaging.WriteableBitmap(
            16, 16, 96, 96, System.Windows.Media.PixelFormats.Bgra32, null);
        Assert.Throws<ArgumentException>(() => PhotoAssistantTools.ScaleImage(src, 0, 1));
    }
}
