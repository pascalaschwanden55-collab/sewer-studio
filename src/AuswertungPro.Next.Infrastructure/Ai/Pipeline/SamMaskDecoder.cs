using System;
using System.Globalization;

namespace AuswertungPro.Next.Infrastructure.Ai.Pipeline;

/// <summary>
/// Dekodiert SAM-RLE-Masken und stellt Hilfsoperationen auf Masken-Bitmaps bereit.
/// RLE-Format: "start_value,run1,run2,..." (aus sam_wrapper.py _rle_encode).
/// </summary>
public static class SamMaskDecoder
{
    /// <summary>Obergrenze fuer Masken-Pixel (Schutz gegen absurde Dimensionen vom Sidecar). ~50 MB bool.</summary>
    public const long MaxMaskPixels = 50_000_000;

    /// <summary>
    /// Dekodiert RLE-String zu Masken-Bitmap.
    /// Format: "start_value,run1,run2,..." mit C-order (row-major).
    /// </summary>
    public static bool[,] DecodeRle(string rle, int width, int height)
    {
        // Defensiv: Dimensionen kommen ungeprueft vom Sidecar. Ungueltige oder absurd
        // grosse Werte abweisen, bevor allokiert wird (sonst OutOfMemoryException).
        if (width <= 0 || height <= 0 || (long)width * height > MaxMaskPixels)
            return new bool[0, 0];

        var mask = new bool[height, width];
        if (string.IsNullOrWhiteSpace(rle)) return mask;

        var parts = rle.Split(',');
        if (parts.Length < 2) return mask;

        // Start-Token defensiv parsen; bei Fehler leere (aber dimensionierte) Maske
        if (!int.TryParse(parts[0], NumberStyles.Integer, CultureInfo.InvariantCulture, out int startVal))
            return mask;

        bool currentVal = startVal != 0;
        int pos = 0;
        int totalPixels = width * height;

        for (int i = 1; i < parts.Length && pos < totalPixels; i++)
        {
            // Defektes oder negatives Run-Token: Dekodierung abbrechen, bereits
            // gesetzte Pixel behalten, statt zu werfen.
            if (!int.TryParse(parts[i], NumberStyles.Integer, CultureInfo.InvariantCulture, out int runLength)
                || runLength < 0)
                break;

            // long-Arithmetik: ein korruptes Riesen-Run-Token (nahe int.MaxValue) wuerde
            // sonst pos ueberlaufen lassen -> negativer Index -> IndexOutOfRangeException.
            int end = (int)Math.Min((long)pos + runLength, totalPixels);
            if (currentVal)
            {
                for (int p = pos; p < end; p++)
                {
                    int row = p / width;
                    int col = p % width;
                    mask[row, col] = true;
                }
            }
            pos = end;
            currentVal = !currentVal;
        }

        return mask;
    }

    /// <summary>
    /// Skaliert eine Masken-Bitmap auf kleinere Zieldimensionen herunter (Nearest-Neighbour).
    /// Wird fuer Konturberechnung zur Performance-Reduktion verwendet.
    /// </summary>
    public static bool[,] Downsample(bool[,] src, int srcH, int srcW, int dstH, int dstW)
    {
        if (dstH >= srcH && dstW >= srcW) return src;

        var dst = new bool[dstH, dstW];
        double yScale = (double)srcH / dstH;
        double xScale = (double)srcW / dstW;

        for (int r = 0; r < dstH; r++)
        {
            int srcR = Math.Min((int)(r * yScale), srcH - 1);
            for (int c = 0; c < dstW; c++)
            {
                int srcC = Math.Min((int)(c * xScale), srcW - 1);
                dst[r, c] = src[srcR, srcC];
            }
        }
        return dst;
    }

    /// <summary>
    /// Prueft ob in einer gegebenen Zeile der Maske im Spaltenbereich [colStart, colEnd)
    /// mindestens ein Pixel gesetzt ist.
    /// </summary>
    public static bool HasOverlap(bool[,] ds, int row, int colStart, int colEnd)
    {
        int w = ds.GetLength(1);
        if (row < 0 || row >= ds.GetLength(0)) return false;
        for (int c = Math.Max(0, colStart); c < Math.Min(w, colEnd); c++)
            if (ds[row, c]) return true;
        return false;
    }
}
