using System;
using System.Linq;
using System.Windows.Media.Imaging;
using AuswertungPro.Next.Domain.Models;

namespace AuswertungPro.Next.UI.Ai;

/// <summary>
/// Auto-Kalibrierung: Erkennt den Rohrdurchmesser aus einem Video-Frame.
/// Scannt horizontale Zeilen nahe der Bildmitte nach Helligkeitskanten (Rohrinnenwand).
/// Kein Sidecar noetig — reine Pixel-Analyse in C#.
/// </summary>
public static class AutoCalibrationService
{
    // Mindest-Gradient (Helligkeitsaenderung pro Pixel) fuer Kantenerkennung
    private const int MinGradientStrength = 25;

    // Plausibilitaet: Rohrdurchmesser muss zwischen 25% und 92% der Bildbreite liegen
    private const double MinDiameterRatio = 0.25;
    private const double MaxDiameterRatio = 0.92;

    // Zeilen die abgetastet werden (relativ zur Bildhoehe, 0.0-1.0)
    private static readonly double[] ScanLines = { 0.45, 0.475, 0.50, 0.525, 0.55 };

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

        // Mehrere horizontale Zeilen scannen und Rohrkanten finden
        var measurements = new System.Collections.Generic.List<(int left, int right)>();

        foreach (double scanY in ScanLines)
        {
            int y = (int)(scanY * height);
            if (y < 0 || y >= height) continue;

            var edges = FindPipeEdgesInRow(grayPixels, width, y);
            if (edges.HasValue)
                measurements.Add(edges.Value);
        }

        if (measurements.Count < 3) return null; // Mindestens 3 von 5 Zeilen muessen Kanten finden

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

        // Plausibilitaet
        double diameterRatio = (double)medianDiameter / width;
        if (diameterRatio < MinDiameterRatio || diameterRatio > MaxDiameterRatio)
            return null;

        // NormalizedDiameter: Verhaeltnis zur Bildbreite
        double normalizedDiameter = diameterRatio;

        // PipeCenter: X aus Median, Y aus der mittleren Scanline (0.50)
        double normalizedCenterX = medianCenterX / width;
        double normalizedCenterY = 0.50;

        return new PipeCalibration
        {
            NominalDiameterMm = nominalDiameterMm,
            NormalizedDiameter = normalizedDiameter,
            PipePixelDiameter = medianDiameter,
            PipeCenter = new NormalizedPoint(normalizedCenterX, normalizedCenterY)
        };
    }

    /// <summary>
    /// Findet die linke und rechte Rohrwand-Kante in einer horizontalen Zeile.
    /// Sucht nach starken Helligkeitsgradienten (dunkel→hell = Rohrinnenwand→Rohrwand).
    /// </summary>
    private static (int left, int right)? FindPipeEdgesInRow(byte[] gray, int width, int y)
    {
        int rowStart = y * width;

        // Von links scannen: staerksten Gradient finden (dunkel→hell = Rohrwand)
        int leftEdge = -1;
        int maxLeftGrad = 0;
        for (int x = 10; x < width / 2; x++)
        {
            // Gradient: Aenderung ueber 3 Pixel Fenster
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
        for (int x = width - 11; x > width / 2; x--)
        {
            // Gradient: diesmal negativ (hell→dunkel von rechts gesehen)
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
