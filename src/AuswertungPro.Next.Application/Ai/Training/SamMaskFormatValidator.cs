using System;
using System.Globalization;

namespace AuswertungPro.Next.Application.Ai.Training;

/// <summary>
/// Strikte FORMAT-Pruefung fuer SAM-RLE-Masken in der Application-Schicht (kein Decoder
/// noetig — die Pixelzahl wird aus den Run-Tokens bestimmt). Deckt defekte Tokens,
/// Dimensionsverletzungen und Leermasken ab. Die optionale Box-Pruefung arbeitet
/// direkt auf den RLE-Runs und verlangt ein echtes Vordergrund-Pixelzentrum in der
/// Hand-Box. Nur das Degraded-Flag bleibt beim Infrastructure-Validator.
/// </summary>
public static class SamMaskFormatValidator
{
    /// <summary>
    /// Prueft RLE-String und Bildmasse strikt. <paramref name="reason"/> liefert bei
    /// Ungueltigkeit den deutschen Ablehnungsgrund (leer bei Gueltigkeit).
    /// </summary>
    public static bool IsValid(string? rle, int? maskImageWidth, int? maskImageHeight, out string reason)
        => TryParse(
            rle,
            maskImageWidth,
            maskImageHeight,
            out _,
            out _,
            out _,
            out _,
            out reason);

    /// <summary>
    /// Prueft zusaetzlich, ob mindestens ein echtes Vordergrundpixel mit seinem
    /// Pixelzentrum innerhalb der normalisierten Hand-Box liegt.
    /// </summary>
    public static bool HasForegroundPixelInsideBox(
        string? rle,
        int? maskImageWidth,
        int? maskImageHeight,
        BoundingBox box,
        out string reason)
    {
        if (!TryParse(
                rle,
                maskImageWidth,
                maskImageHeight,
                out var startValue,
                out var runs,
                out var width,
                out var height,
                out reason))
        {
            return false;
        }

        if (!TryGetBoxPixelRange(box, width, height, out var minCol, out var maxCol, out var minRow, out var maxRow))
        {
            reason = "Hand-Box ist ungueltig oder enthaelt kein Pixelzentrum.";
            return false;
        }

        long position = 0;
        var currentIsMask = startValue != 0;
        foreach (var run in runs)
        {
            if (currentIsMask
                && run > 0
                && RunIntersectsBox(
                    position,
                    position + run - 1L,
                    width,
                    minCol,
                    maxCol,
                    minRow,
                    maxRow))
            {
                reason = string.Empty;
                return true;
            }

            position += run;
            currentIsMask = !currentIsMask;
        }

        reason = "Maske gehoert nicht zur Hand-Box (kein Vordergrundpixel innerhalb der Box).";
        return false;
    }

    private static bool TryParse(
        string? rle,
        int? maskImageWidth,
        int? maskImageHeight,
        out int startValue,
        out int[] runs,
        out int width,
        out int height,
        out string reason)
    {
        startValue = 0;
        runs = [];
        width = 0;
        height = 0;

        if (string.IsNullOrWhiteSpace(rle))
        {
            reason = "Keine Masken-RLE vorhanden.";
            return false;
        }
        if (maskImageWidth is null or <= 0 || maskImageHeight is null or <= 0)
        {
            reason = "Masken-Bildmasse fehlen oder sind ungueltig.";
            return false;
        }

        width = maskImageWidth.Value;
        height = maskImageHeight.Value;
        var parts = rle.Split(',');
        // Der echte Sidecar-Encoder schreibt genau die vorhandenen Runs und haengt
        // keinen kuenstlichen Hintergrund-Run an. Deshalb sind gerade UND ungerade
        // Tokenzahlen gueltig. Startwert 0/1 und positive Runs entsprechen seinem
        // kanonischen Format; entscheidend bleibt ausserdem die exakte Laufsumme.
        if (parts.Length < 2
            || !int.TryParse(parts[0], NumberStyles.Integer, CultureInfo.InvariantCulture, out startValue)
            || startValue is not (0 or 1))
        {
            reason = "Masken-RLE nicht lesbar.";
            return false;
        }

        runs = new int[parts.Length - 1];
        long runSum = 0;
        long maskPixels = 0;
        var currentIsMask = startValue != 0;
        for (var i = 1; i < parts.Length; i++)
        {
            if (!int.TryParse(parts[i], NumberStyles.Integer, CultureInfo.InvariantCulture, out var run)
                || run <= 0)
            {
                reason = "Masken-RLE nicht lesbar.";
                return false;
            }
            runs[i - 1] = run;
            runSum += run;
            if (currentIsMask)
                maskPixels += run;
            currentIsMask = !currentIsMask;
        }

        var expected = (long)width * height;
        if (runSum != expected)
        {
            reason = $"Masken-RLE passt nicht zu den Bildmassen ({runSum} statt {expected} Pixel).";
            return false;
        }
        if (maskPixels == 0)
        {
            reason = "Maske enthaelt keine Pixel (Leermaske).";
            return false;
        }

        reason = string.Empty;
        return true;
    }

    private static bool TryGetBoxPixelRange(
        BoundingBox box,
        int imageWidth,
        int imageHeight,
        out long minCol,
        out long maxCol,
        out long minRow,
        out long maxRow)
    {
        minCol = maxCol = minRow = maxRow = 0;
        if (!double.IsFinite(box.XCenter)
            || !double.IsFinite(box.YCenter)
            || !double.IsFinite(box.Width)
            || !double.IsFinite(box.Height)
            || box.Width <= 0.0
            || box.Height <= 0.0)
        {
            return false;
        }

        var left = box.XCenter - box.Width / 2.0;
        var right = box.XCenter + box.Width / 2.0;
        var top = box.YCenter - box.Height / 2.0;
        var bottom = box.YCenter + box.Height / 2.0;
        const double normalizedEpsilon = 1e-9;
        if (left < -normalizedEpsilon
            || top < -normalizedEpsilon
            || right > 1.0 + normalizedEpsilon
            || bottom > 1.0 + normalizedEpsilon)
        {
            return false;
        }

        left = Math.Max(0.0, left);
        right = Math.Min(1.0, right);
        top = Math.Max(0.0, top);
        bottom = Math.Min(1.0, bottom);

        return TryGetPixelCenterRange(left, right, imageWidth, out minCol, out maxCol)
               && TryGetPixelCenterRange(top, bottom, imageHeight, out minRow, out maxRow);
    }

    private static bool TryGetPixelCenterRange(
        double normalizedStart,
        double normalizedEnd,
        int size,
        out long first,
        out long last)
    {
        const double pixelEpsilon = 1e-9;
        first = (long)Math.Ceiling(normalizedStart * size - 0.5 - pixelEpsilon);
        last = (long)Math.Floor(normalizedEnd * size - 0.5 + pixelEpsilon);
        first = Math.Max(0L, first);
        last = Math.Min(size - 1L, last);
        return first <= last;
    }

    private static bool RunIntersectsBox(
        long runStart,
        long runEnd,
        int imageWidth,
        long minCol,
        long maxCol,
        long minRow,
        long maxRow)
    {
        var row = Math.Max(minRow, runStart / imageWidth);
        if (row > maxRow)
            return false;

        var allowedStart = row * imageWidth + minCol;
        var allowedEnd = row * imageWidth + maxCol;
        if (allowedEnd < runStart)
        {
            row++;
            if (row > maxRow)
                return false;
            allowedStart = row * imageWidth + minCol;
            allowedEnd = row * imageWidth + maxCol;
        }

        return allowedStart <= runEnd && allowedEnd >= runStart;
    }
}
