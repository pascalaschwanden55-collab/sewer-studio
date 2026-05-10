using System.Linq;
using AuswertungPro.Next.Application.Ai.PhotoAssistant;
using Xunit;

namespace AuswertungPro.Next.Pipeline.Tests.PhotoAssistant;

[Trait("Category", "PhotoAssistant")]
[Trait("Category", "Unit")]
public class BendAngleToolServiceTests
{
    [Fact]
    public void ProjektionBeiBendNull_LiefertKonzentrischeRinge()
    {
        var (tube1, tube2, _) = BendAngleToolService.BuildProjectedRings(
            bendAngleDegrees: 0,
            bendScale: 1.0,
            cameraHeightPercent: 50,
            canvasWidth: 800, canvasHeight: 600);

        // Bei bendAngle=0 ist Rohr 2 in Linie mit Rohr 1 -> alle Achsen-Mittelpunkte
        // liegen sehr nahe an der Bildmitte (cx=400). Toleranz: ein paar Pixel wegen Float.
        var cx = 400.0;
        var allCenters = tube1.Concat(tube2).Where(r => !double.IsNaN(r.AxisCenterScreen.X)).ToList();
        Assert.NotEmpty(allCenters);
        foreach (var c in allCenters)
            Assert.InRange(c.AxisCenterScreen.X, cx - 5, cx + 5);
    }

    [Fact]
    public void ProjektionBei90Grad_VersetztRingeNachLinks()
    {
        var (_, tube2, _) = BendAngleToolService.BuildProjectedRings(
            bendAngleDegrees: 90,
            bendScale: 1.0,
            cameraHeightPercent: 50,
            canvasWidth: 800, canvasHeight: 600);

        // Das letzte (entfernteste) Rohr-2-Element liegt bei 90° Knick weit links der Mitte.
        var last = tube2.Last(r => !double.IsNaN(r.AxisCenterScreen.X));
        Assert.True(last.AxisCenterScreen.X < 380, $"Erwartet x<380, war {last.AxisCenterScreen.X}");
    }

    [Fact]
    public void RingHatExakt32Punkte()
    {
        var (tube1, _, _) = BendAngleToolService.BuildProjectedRings(
            bendAngleDegrees: 0,
            bendScale: 1.0,
            cameraHeightPercent: 50,
            canvasWidth: 800, canvasHeight: 600);

        // Erster Ring vom Anfang von Rohr 1 sollte alle 32 Segmente projiziert haben.
        Assert.Equal(BendAngleToolService.RingSegments, tube1[0].RingPoints.Count);
    }

    [Fact]
    public void ClampScale_BegrenztAuf03Bis25()
    {
        Assert.Equal(0.3, BendAngleToolService.ClampScale(0.1));
        Assert.Equal(2.5, BendAngleToolService.ClampScale(5.0));
        Assert.Equal(1.0, BendAngleToolService.ClampScale(1.0));
    }
}
