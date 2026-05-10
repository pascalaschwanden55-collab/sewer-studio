using System.Linq;
using AuswertungPro.Next.Application.Ai.PhotoAssistant;
using Xunit;

namespace AuswertungPro.Next.Pipeline.Tests.PhotoAssistant;

[Trait("Category", "PhotoAssistant")]
[Trait("Category", "Unit")]
public class DeformationToolServiceTests
{
    [Fact]
    public void QuerschnittPerfekterKreis_Liefert100Prozent()
    {
        var radii = Enumerable.Repeat(1.0, 16).ToArray();
        var prozent = DeformationToolService.ComputeQuerschnittPercent(radii);
        // 16-Eck-Polygonflaeche / pi sollte ~99.36% liegen, aber im polaren Trapezmodell
        // liefert die Formel 0.5*r*r*sin(2pi/16) summiert ueber 16 = 8 * sin(22.5°) = 3.0615
        // sollFlaeche = pi = 3.1416 -> ~97.45%. Wir akzeptieren ±3 Prozentpunkte.
        Assert.InRange(prozent, 97, 100.5);
    }

    [Fact]
    public void QuerschnittKomplettVerformt_LiefertKleineFlaeche()
    {
        var radii = Enumerable.Repeat(0.5, 16).ToArray();
        var prozent = DeformationToolService.ComputeQuerschnittPercent(radii);
        // 0.25 * Polygonflaeche (skaliert quadratisch) -> ~24-26%
        Assert.InRange(prozent, 22, 28);
    }

    [Fact]
    public void QuerschnittEinzelnerEinschnitt_LiefertCa95Prozent()
    {
        var radii = Enumerable.Repeat(1.0, 16).ToArray();
        radii[5] = 0.5;
        var prozent = DeformationToolService.ComputeQuerschnittPercent(radii);
        // Ein Stuetzpunkt eingedrueckt -> 2 angrenzende Trapeze betroffen
        Assert.InRange(prozent, 90, 99);
    }
}
