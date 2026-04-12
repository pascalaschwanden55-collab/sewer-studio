// AuswertungPro – Video-Selbsttraining Phase 2
using System;
using System.IO;
using System.Windows.Media.Imaging;

namespace AuswertungPro.Next.UI.Ai.Training.Services;

/// <summary>
/// Leichtgewichtiger Qualitaetsfilter fuer Video-Frames.
/// Prueft Schaerfe (Laplace-Varianz), Helligkeit und Duplikate (dHash).
/// Kein NuGet noetig — nutzt WPF BitmapSource fuer Bildoperationen.
/// </summary>
public sealed class FrameQualityFilter
{
    // Schwellen (empirisch, konfigurierbar)
    private readonly double _minLaplacianVariance;
    private readonly double _minLuminance;
    private readonly double _maxLuminance;
    private readonly int _maxHammingDistance;

    private ulong? _lastHash;

    public FrameQualityFilter(
        double minLaplacianVariance = 100.0,
        double minLuminance = 30.0,
        double maxLuminance = 240.0,
        int maxHammingDistance = 5)
    {
        _minLaplacianVariance = minLaplacianVariance;
        _minLuminance = minLuminance;
        _maxLuminance = maxLuminance;
        _maxHammingDistance = maxHammingDistance;
    }

    /// <summary>
    /// Prueft ob ein Frame akzeptabel ist (Schaerfe + Helligkeit + kein Duplikat).
    /// Aktualisiert den internen Hash fuer die naechste Duplikat-Pruefung.
    /// </summary>
    public bool IsAcceptable(byte[] pngBytes)
    {
        if (pngBytes is null || pngBytes.Length < 100) return false;

        byte[] grayscale;
        int width, height;

        try
        {
            (grayscale, width, height) = DecodeToGrayscale(pngBytes);
        }
        catch
        {
            return false; // Defektes Bild
        }

        if (width < 8 || height < 8) return false;

        // 1. Helligkeit pruefen
        var luminance = ComputeMeanLuminance(grayscale);
        if (luminance < _minLuminance || luminance > _maxLuminance)
            return false;

        // 2. Schaerfe pruefen (Laplacian-Varianz)
        var sharpness = ComputeLaplacianVariance(grayscale, width, height);
        if (sharpness < _minLaplacianVariance)
            return false;

        // 3. Duplikat pruefen (dHash)
        var hash = ComputeDHash(grayscale, width, height);
        if (_lastHash.HasValue && HammingDistance(hash, _lastHash.Value) <= _maxHammingDistance)
            return false; // Duplikat

        _lastHash = hash;
        return true;
    }

    /// <summary>Setzt den internen Hash zurueck (z.B. bei neuem Video).</summary>
    public void Reset() => _lastHash = null;

    /// <summary>Berechnet die mittlere Helligkeit eines Grayscale-Bildes.</summary>
    internal static double ComputeMeanLuminance(byte[] grayscale)
    {
        if (grayscale.Length == 0) return 0;
        long sum = 0;
        for (int i = 0; i < grayscale.Length; i++)
            sum += grayscale[i];
        return (double)sum / grayscale.Length;
    }

    /// <summary>
    /// Berechnet die Laplacian-Varianz als Schaerfe-Mass.
    /// Hoeherer Wert = schaerfer. Typisch: &lt;100 = unscharf, &gt;300 = scharf.
    /// </summary>
    internal static double ComputeLaplacianVariance(byte[] grayscale, int width, int height)
    {
        if (width < 3 || height < 3) return 0;

        // 3x3 Laplacian Kernel: [0,1,0 / 1,-4,1 / 0,1,0]
        long sum = 0;
        long sumSq = 0;
        int count = 0;

        for (int y = 1; y < height - 1; y++)
        {
            for (int x = 1; x < width - 1; x++)
            {
                int idx = y * width + x;
                int laplacian =
                    grayscale[idx - width] +        // oben
                    grayscale[idx - 1] +            // links
                    -4 * grayscale[idx] +           // mitte
                    grayscale[idx + 1] +            // rechts
                    grayscale[idx + width];          // unten

                sum += laplacian;
                sumSq += (long)laplacian * laplacian;
                count++;
            }
        }

        if (count == 0) return 0;
        double mean = (double)sum / count;
        double variance = (double)sumSq / count - mean * mean;
        return variance;
    }

    /// <summary>
    /// Berechnet einen 64-bit Differenz-Hash (dHash) fuer Duplikat-Erkennung.
    /// Robust gegen Skalierung und leichte Helligkeitsaenderungen.
    /// </summary>
    internal static ulong ComputeDHash(byte[] grayscale, int width, int height)
    {
        // Auf 9x8 herunterskalieren (9 Spalten × 8 Zeilen → 8×8 = 64 Vergleiche)
        var small = DownscaleGrayscale(grayscale, width, height, 9, 8);

        ulong hash = 0;
        for (int y = 0; y < 8; y++)
        {
            for (int x = 0; x < 8; x++)
            {
                int idx = y * 9 + x;
                if (small[idx] < small[idx + 1])
                    hash |= 1UL << (y * 8 + x);
            }
        }

        return hash;
    }

    /// <summary>Berechnet die Hamming-Distanz zwischen zwei Hashes.</summary>
    internal static int HammingDistance(ulong a, ulong b)
    {
        ulong xor = a ^ b;
        int count = 0;
        while (xor != 0)
        {
            count += (int)(xor & 1);
            xor >>= 1;
        }
        return count;
    }

    /// <summary>Dekodiert PNG-Bytes in ein Grayscale-Array.</summary>
    private static (byte[] Grayscale, int Width, int Height) DecodeToGrayscale(byte[] pngBytes)
    {
        using var ms = new MemoryStream(pngBytes);
        var decoder = BitmapDecoder.Create(ms, BitmapCreateOptions.None, BitmapCacheOption.OnLoad);
        var frame = decoder.Frames[0];

        // In Bgra32 konvertieren
        var bgra = new FormatConvertedBitmap(frame, System.Windows.Media.PixelFormats.Bgra32, null, 0);
        int w = bgra.PixelWidth;
        int h = bgra.PixelHeight;
        int stride = w * 4;
        var pixels = new byte[stride * h];
        bgra.CopyPixels(pixels, stride, 0);

        // Grayscale (Luminance-Formel: 0.299R + 0.587G + 0.114B)
        var gray = new byte[w * h];
        for (int i = 0; i < w * h; i++)
        {
            int pi = i * 4;
            gray[i] = (byte)(0.114 * pixels[pi] + 0.587 * pixels[pi + 1] + 0.299 * pixels[pi + 2]);
        }

        return (gray, w, h);
    }

    /// <summary>Bilineare Herunterskalierung eines Grayscale-Bildes.</summary>
    private static byte[] DownscaleGrayscale(byte[] src, int srcW, int srcH, int dstW, int dstH)
    {
        var dst = new byte[dstW * dstH];
        double scaleX = (double)srcW / dstW;
        double scaleY = (double)srcH / dstH;

        for (int dy = 0; dy < dstH; dy++)
        {
            for (int dx = 0; dx < dstW; dx++)
            {
                // Mittelwert im Quellblock
                int sx0 = (int)(dx * scaleX);
                int sy0 = (int)(dy * scaleY);
                int sx1 = Math.Min((int)((dx + 1) * scaleX), srcW);
                int sy1 = Math.Min((int)((dy + 1) * scaleY), srcH);

                long sum = 0;
                int count = 0;
                for (int sy = sy0; sy < sy1; sy++)
                {
                    for (int sx = sx0; sx < sx1; sx++)
                    {
                        sum += src[sy * srcW + sx];
                        count++;
                    }
                }

                dst[dy * dstW + dx] = count > 0 ? (byte)(sum / count) : (byte)0;
            }
        }

        return dst;
    }
}
