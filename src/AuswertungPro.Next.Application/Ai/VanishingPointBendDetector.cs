namespace AuswertungPro.Next.Application.Ai;

/// <summary>Ergebnis der geometrischen Bogen-Erkennung.</summary>
/// <param name="IsBend">true, wenn der Fluchtpunkt seitlich genug verschoben ist (Bogen BCC).</param>
/// <param name="HorizontalShift">Horizontale Verschiebung des Fluchtpunkts von der Bildmitte, normiert (-0.5..+0.5).</param>
/// <param name="VanishX">Fluchtpunkt X normiert (0..1).</param>
/// <param name="VanishY">Fluchtpunkt Y normiert (0..1).</param>
public sealed record BendDetectionResult(bool IsBend, double HorizontalShift, double VanishX, double VanishY);

/// <summary>
/// Erkennt einen Bogen (VSA-KEK BCC) rein geometrisch ueber den Fluchtpunkt, statt ueber
/// unzuverlaessige DINO-Textlabels. Der dunkelste Bereich eines Kanal-Frames ist das
/// Rohr-Innere/Tunnelende: bei geradem Rohr zentral, bei einem Bogen seitlich verschoben.
///
/// Diagnose 2026-06-18: DINO klassifizierte den dunklen Bogen-Tunnel faelschlich als
/// "infiltration" (Wasser), weil "bend" gegen "infiltration" im selben Prompt verliert.
/// Geometrie ist robuster, braucht kein Training und keinen Label-Wettstreit.
///
/// Reine Application-Logik (testbar): arbeitet auf einer Helligkeits-Matrix, nicht auf Bitmaps.
/// </summary>
public static class VanishingPointBendDetector
{
    /// <summary>Anteil der dunkelsten Pixel, der als Tunnelende gilt (empirisch 15%).</summary>
    public const double DarkestFraction = 0.15;

    /// <summary>
    /// Schwelle fuer die horizontale Fluchtpunkt-Verschiebung, ab der ein Bogen vorliegt.
    /// Empirisch an echten Frames: gerades Rohr dx~0.00, Bogen |dx|>=0.13. 0.12 toleriert
    /// leicht schiefe Kameras, erkennt aber echte Boegen.
    /// </summary>
    public const double BendShiftThreshold = 0.12;

    /// <summary>
    /// Analysiert eine Helligkeits-Matrix [hoehe, breite] (0=schwarz..255=weiss) und bestimmt
    /// den Fluchtpunkt (Schwerpunkt der dunkelsten Pixel) sowie ob ein Bogen vorliegt.
    /// </summary>
    public static BendDetectionResult Analyze(double[,] brightness)
    {
        if (brightness == null) throw new System.ArgumentNullException(nameof(brightness));
        int h = brightness.GetLength(0);
        int w = brightness.GetLength(1);
        if (h == 0 || w == 0)
            return new BendDetectionResult(false, 0, 0.5, 0.5);

        // Schwellwert: Grenze der dunkelsten DarkestFraction aller Pixel.
        int total = h * w;
        var vals = new double[total];
        int k = 0;
        for (int y = 0; y < h; y++)
            for (int x = 0; x < w; x++)
                vals[k++] = brightness[y, x];
        System.Array.Sort(vals);
        int idx = System.Math.Min(total - 1, (int)(total * DarkestFraction));
        double threshold = vals[idx];

        // Schwerpunkt der dunkelsten Pixel. Striktes "<" gegen den Schwellwert, damit eine
        // flache/binaere Helligkeitsverteilung (viele Pixel exakt auf dem Schwellwert) nicht
        // das ganze Bild erfasst und den Schwerpunkt faelschlich in die Mitte zieht. Fallback
        // auf "<=" nur, falls "<" gar keine Pixel liefert (alle gleich hell).
        double sumX = 0, sumY = 0;
        long n = 0;
        for (int y = 0; y < h; y++)
            for (int x = 0; x < w; x++)
                if (brightness[y, x] < threshold)
                {
                    sumX += x;
                    sumY += y;
                    n++;
                }

        if (n == 0)
        {
            for (int y = 0; y < h; y++)
                for (int x = 0; x < w; x++)
                    if (brightness[y, x] <= threshold)
                    {
                        sumX += x;
                        sumY += y;
                        n++;
                    }
        }

        if (n == 0)
            return new BendDetectionResult(false, 0, 0.5, 0.5);

        double vanishX = (sumX / n) / w;
        double vanishY = (sumY / n) / h;
        double shift = vanishX - 0.5;
        bool isBend = System.Math.Abs(shift) >= BendShiftThreshold;
        return new BendDetectionResult(isBend, shift, vanishX, vanishY);
    }
}
