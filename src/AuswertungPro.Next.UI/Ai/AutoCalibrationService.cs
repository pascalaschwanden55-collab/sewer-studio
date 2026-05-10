using System;
using System.Linq;
using System.Windows.Media.Imaging;
using AuswertungPro.Next.Domain.Models;

namespace AuswertungPro.Next.UI.Ai;

/// <summary>
/// Auto-Kalibrierung: Erkennt den Rohrdurchmesser aus einem Video-Frame.
/// Scannt horizontale Zeilen nahe der Bildmitte nach Helligkeitskanten (Rohrinnenwand).
/// Kein Sidecar noetig — reine Pixel-Analyse in C#.
///
/// Slice 8a.6.E (2026-05-10): Pillarbox-Erkennung. Wenn das Video als
/// Pillarbox/Letterbox in einem anderen Container-Aspect-Ratio gerendert
/// ist, hat der Frame schwarze Balken links/rechts. Diese Balken hatten
/// frueher die extremen Helligkeitsgradienten an ihren Kanten geliefert
/// — Algorithmus detektierte sie statt der echten Rohrwand. Jetzt:
/// erkennen + ROI auf Content-Bereich beschraenken.
/// </summary>
public static class AutoCalibrationService
{
    // Mindest-Gradient (Helligkeitsaenderung pro Pixel) fuer Kantenerkennung
    private const int MinGradientStrength = 25;

    // Plausibilitaet: Rohrdurchmesser muss zwischen 25% und 92% der CONTENT-Breite liegen
    private const double MinDiameterRatio = 0.25;
    private const double MaxDiameterRatio = 0.92;

    // Zeilen die abgetastet werden (relativ zur Bildhoehe, 0.0-1.0)
    private static readonly double[] ScanLines = { 0.30, 0.35, 0.40, 0.45, 0.50, 0.55, 0.60, 0.65, 0.70 };

    // Pillarbox-Detection: Pixel <= dieser Helligkeit gilt als "schwarz" (PNG-encoded
    // schwarze Balken sind oft 0..5; Sicherheitsabstand 15).
    private const byte BlackThreshold = 15;
    // Maximale Pillarbox-Breite: 35% der Bildseite. Mehr ist unrealistisch fuer
    // typische Videos (sonst stimmt was anderes nicht).
    private const double MaxPillarboxRatio = 0.35;

    /// <summary>
    /// Versucht den Rohrdurchmesser automatisch aus einem Frame zu erkennen.
    /// Ideal bei Rohranfang (BCD) oder Rohrverbindung (Muffe) wo das Profil gut sichtbar ist.
    /// </summary>
    /// <param name="frame">Video-Frame als BitmapSource.</param>
    /// <param name="nominalDiameterMm">Bekannter DN aus Haltungsdaten (z.B. 300).</param>
    /// <returns>PipeCalibration wenn erfolgreich, sonst null.</returns>
    public static PipeCalibration? TryAutoCalibrate(BitmapSource frame, int nominalDiameterMm)
    {
        if (frame == null || nominalDiameterMm <= 0) return null;

        int width = frame.PixelWidth;
        int height = frame.PixelHeight;
        if (width < 100 || height < 100) return null;

        // Frame in Graustufen-Byte-Array konvertieren
        byte[] grayPixels = ConvertToGrayscale(frame);

        // Slice 8a.6.E (2026-05-10): Pillarbox-Bereiche detektieren und beim
        // Edge-Scan ueberspringen. Sonst werden die Balken-Kanten als
        // "Rohrwand" interpretiert.
        var (leftPad, rightPad) = DetectPillarboxPadding(grayPixels, width, height);
        int contentLeft = leftPad;
        int contentRight = width - rightPad;
        int contentWidth = contentRight - contentLeft;
        if (contentWidth < 100) return null; // Content zu schmal — wahrscheinlich kein gueltiger Frame.

        // Mehrere horizontale Zeilen scannen und Rohrkanten finden
        var measurements = new System.Collections.Generic.List<(int left, int right)>();

        foreach (double scanY in ScanLines)
        {
            int y = (int)(scanY * height);
            if (y < 0 || y >= height) continue;

            var edges = FindPipeEdgesInRow(grayPixels, width, y, contentLeft, contentRight);
            if (edges.HasValue)
                measurements.Add(edges.Value);
        }

        if (measurements.Count < 5) return null; // Mindestens 5 von 9 Zeilen muessen Kanten finden

        // Median der Messungen fuer Robustheit
        var diameters = measurements
            .Select(m => m.right - m.left)
            .OrderBy(d => d)
            .ToArray();
        int medianDiameter = diameters[diameters.Length / 2];

        // Median der linken/rechten Kanten fuer Center-Berechnung
        var centers = measurements
            .Select(m => (m.left + m.right) / 2.0)
            .OrderBy(c => c)
            .ToArray();
        double medianCenterX = centers[centers.Length / 2];

        // Plausibilitaet: Diameter im Verhaeltnis zur Content-Breite (nicht zur
        // ganzen Bildbreite). Sonst wuerde Pillarbox die Ratios verfaelschen.
        double diameterRatio = (double)medianDiameter / contentWidth;
        if (diameterRatio < MinDiameterRatio || diameterRatio > MaxDiameterRatio)
            return null;

        // NormalizedDiameter: Verhaeltnis zur ganzen Bildbreite (im
        // Source-Frame-Koordinatensystem, weil PipeCalibration vom
        // ganzen Frame ausgeht).
        double normalizedDiameter = (double)medianDiameter / width;

        // PipeCenter: X aus Median, Y aus der mittleren Scanline (0.50)
        double normalizedCenterX = medianCenterX / width;
        double normalizedCenterY = 0.50;

        return new PipeCalibration
        {
            NominalDiameterMm = nominalDiameterMm,
            NormalizedDiameter = normalizedDiameter,
            PipePixelDiameter = medianDiameter,
            PipeCenter = new NormalizedPoint(normalizedCenterX, normalizedCenterY),
            WasManuallyCalibrated = true
        };
    }

    /// <summary>
    /// Findet die linke und rechte Rohrwand-Kante in einer horizontalen Zeile.
    /// Sucht nach starken Helligkeitsgradienten (dunkel→hell = Rohrinnenwand→Rohrwand).
    /// Slice 8a.6.E: contentLeft/contentRight begrenzen den Scan-Bereich auf
    /// den nicht-Pillarbox-Teil.
    /// </summary>
    private static (int left, int right)? FindPipeEdgesInRow(
        byte[] gray, int width, int y, int contentLeft, int contentRight)
    {
        int rowStart = y * width;
        int contentMid = (contentLeft + contentRight) / 2;
        int leftBound = Math.Max(contentLeft + 10, 1);
        int rightBound = Math.Min(contentRight - 10, width - 2);
        if (rightBound <= leftBound) return null;

        // Von links scannen: staerksten Gradient finden (dunkel→hell = Rohrwand)
        int leftEdge = -1;
        int maxLeftGrad = 0;
        for (int x = leftBound; x < contentMid; x++)
        {
            int grad = gray[rowStart + x + 1] - gray[rowStart + x - 1];
            if (grad > maxLeftGrad && grad > MinGradientStrength)
            {
                maxLeftGrad = grad;
                leftEdge = x;
            }
        }

        // Von rechts scannen: staerksten Gradient finden
        int rightEdge = -1;
        int maxRightGrad = 0;
        for (int x = rightBound; x > contentMid; x--)
        {
            int grad = gray[rowStart + x - 1] - gray[rowStart + x + 1];
            if (grad > maxRightGrad && grad > MinGradientStrength)
            {
                maxRightGrad = grad;
                rightEdge = x;
            }
        }

        if (leftEdge < 0 || rightEdge < 0) return null;
        if (rightEdge <= leftEdge + 50) return null; // Mindestbreite 50px

        return (leftEdge, rightEdge);
    }

    /// <summary>Pillarbox/Letterbox-Padding-Detection (Slice 8a.6.E 2026-05-10).
    /// Tastet auf der mittleren Bild-Hoehe Pixel von links bzw. rechts ab und
    /// liefert die Anzahl Pixel die als "schwarzer Balken" erkannt wurden.
    /// Statisch — testbar mit synthetischen Bildern.</summary>
    /// <returns>(leftPad, rightPad) in Pixeln. Beide 0 wenn kein Pillarbox.</returns>
    public static (int leftPad, int rightPad) DetectPillarboxPadding(
        byte[] gray, int width, int height)
    {
        if (gray == null || width <= 0 || height <= 0) return (0, 0);
        if (gray.Length < width * height) return (0, 0);

        int midY = height / 2;
        int rowStart = midY * width;
        int maxPad = (int)(width * MaxPillarboxRatio);

        int leftPad = 0;
        for (int x = 0; x < maxPad; x++)
        {
            if (gray[rowStart + x] > BlackThreshold) break;
            leftPad = x + 1;
        }

        int rightPad = 0;
        for (int x = width - 1; x >= width - maxPad; x--)
        {
            if (gray[rowStart + x] > BlackThreshold) break;
            rightPad = (width - 1 - x) + 1;
        }

        // Sanity: wenn das Content-Fenster zu klein wird, kein Pillarbox annehmen
        // (vermutlich ist das Bild grossteils dunkel).
        if (width - leftPad - rightPad < width / 2)
            return (0, 0);

        return (leftPad, rightPad);
    }

    /// <summary>
    /// Konvertiert ein BitmapSource in ein Graustufen-Byte-Array (1 Byte pro Pixel).
    /// Unterstuetzt verschiedene Pixelformate (Bgr32, Bgra32, Pbgra32, Gray8).
    /// </summary>
    private static byte[] ConvertToGrayscale(BitmapSource source)
    {
        int width = source.PixelWidth;
        int height = source.PixelHeight;

        // Erst in Bgr32 konvertieren falls noetig
        if (source.Format != System.Windows.Media.PixelFormats.Bgr32 &&
            source.Format != System.Windows.Media.PixelFormats.Bgra32 &&
            source.Format != System.Windows.Media.PixelFormats.Pbgra32)
        {
            var converted = new FormatConvertedBitmap(source, System.Windows.Media.PixelFormats.Bgr32, null, 0);
            converted.Freeze();
            source = converted;
        }

        int stride = width * 4; // 4 Bytes pro Pixel (BGRA)
        byte[] pixels = new byte[stride * height];
        source.CopyPixels(pixels, stride, 0);

        // In Graustufen konvertieren (Luminanz: 0.299R + 0.587G + 0.114B)
        byte[] gray = new byte[width * height];
        for (int i = 0; i < width * height; i++)
        {
            int offset = i * 4;
            byte b = pixels[offset];
            byte g = pixels[offset + 1];
            byte r = pixels[offset + 2];
            gray[i] = (byte)(0.299 * r + 0.587 * g + 0.114 * b);
        }

        return gray;
    }
}
