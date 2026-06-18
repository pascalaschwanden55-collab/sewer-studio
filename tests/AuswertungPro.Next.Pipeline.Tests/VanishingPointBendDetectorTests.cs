using AuswertungPro.Next.Application.Ai;
using Xunit;

namespace AuswertungPro.Next.Pipeline.Tests;

/// <summary>
/// Geometrie-basierte Bogen-Erkennung (BCC): Der dunkelste Bereich eines Frames ist das
/// Rohr-Innere/Tunnelende (Fluchtpunkt). Bei einem geraden Rohr liegt er zentral, bei einem
/// Bogen seitlich verschoben. Empirisch verifiziert an echten Frames der Haltung 1077586-1077458
/// (Bogen dx=-0.13, gerades Rohr dx=0.00). Ersetzt die unzuverlaessige DINO-Label-Erkennung,
/// die den dunklen Bogen-Tunnel faelschlich als "infiltration"/Wasser klassifizierte.
/// </summary>
public class VanishingPointBendDetectorTests
{
    // Hilfsfunktion: erzeugt eine Helligkeits-Matrix (0=schwarz..255=weiss) mit einem
    // dunklen Bereich (das Tunnelende) an Position (cxNorm, cyNorm). Realistischer
    // radialer Verlauf (dunkel in der Mitte, heller nach aussen) statt hartem Binaerbild -
    // so wie echte Kanal-Frames; der dunkelste Schwerpunkt liegt eindeutig beim Zentrum.
    private static double[,] FrameWithDarkSpot(int w, int h, double cxNorm, double cyNorm, double radiusNorm = 0.18)
    {
        var m = new double[h, w];
        double cx = cxNorm * w, cy = cyNorm * h, r = radiusNorm * w;
        for (int y = 0; y < h; y++)
            for (int x = 0; x < w; x++)
            {
                double d = System.Math.Sqrt((x - cx) * (x - cx) + (y - cy) * (y - cy));
                // 0 im Zentrum, ansteigend bis 255 am Rand des dunklen Bereichs, dann hell.
                double val = d >= r ? 255.0 : 255.0 * (d / r);
                m[y, x] = System.Math.Min(255.0, val);
            }
        return m;
    }

    [Fact]
    public void Gerades_Rohr_dunkler_Fleck_zentral_ist_kein_Bogen()
    {
        var frame = FrameWithDarkSpot(96, 72, cxNorm: 0.50, cyNorm: 0.50);
        var r = VanishingPointBendDetector.Analyze(frame);
        Assert.False(r.IsBend);
        Assert.InRange(r.HorizontalShift, -0.05, 0.05);
    }

    [Fact]
    public void Bogen_dunkler_Fleck_links_verschoben_ist_Bogen()
    {
        var frame = FrameWithDarkSpot(96, 72, cxNorm: 0.37, cyNorm: 0.50);
        var r = VanishingPointBendDetector.Analyze(frame);
        Assert.True(r.IsBend);
        Assert.True(r.HorizontalShift < 0, "Tunnel links -> negative Verschiebung");
    }

    [Fact]
    public void Bogen_dunkler_Fleck_rechts_verschoben_ist_Bogen()
    {
        var frame = FrameWithDarkSpot(96, 72, cxNorm: 0.63, cyNorm: 0.50);
        var r = VanishingPointBendDetector.Analyze(frame);
        Assert.True(r.IsBend);
        Assert.True(r.HorizontalShift > 0, "Tunnel rechts -> positive Verschiebung");
    }

    [Fact]
    public void Leicht_dezentral_unter_Schwelle_ist_noch_kein_Bogen()
    {
        // Verschiebung knapp unter der Schwelle (0.12) -> noch gerade. Verhindert
        // Fehlalarm bei leicht schiefer Kamera.
        var frame = FrameWithDarkSpot(96, 72, cxNorm: 0.58, cyNorm: 0.50);
        var r = VanishingPointBendDetector.Analyze(frame);
        Assert.False(r.IsBend);
    }

    [Fact]
    public void Liefert_Fluchtpunkt_normiert_zurueck()
    {
        var frame = FrameWithDarkSpot(96, 72, cxNorm: 0.37, cyNorm: 0.50);
        var r = VanishingPointBendDetector.Analyze(frame);
        // Schwerpunkt des dunklen Flecks ~ bei der gesetzten Position.
        Assert.InRange(r.VanishX, 0.32, 0.42);
        Assert.InRange(r.VanishY, 0.45, 0.55);
    }
}
