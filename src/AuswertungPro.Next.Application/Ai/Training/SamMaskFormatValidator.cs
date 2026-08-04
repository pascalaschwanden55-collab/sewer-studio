using System;
using System.Globalization;

namespace AuswertungPro.Next.Application.Ai.Training;

/// <summary>
/// Strikte FORMAT-Pruefung fuer SAM-RLE-Masken in der Application-Schicht (kein Decoder
/// noetig — die Pixelzahl wird aus den Run-Tokens bestimmt). Deckt defekte Tokens,
/// Dimensionsverletzungen und Leermasken ab. Die optionale Box-Pruefung arbeitet
/// direkt auf den RLE-Runs und verlangt, dass mindestens 80 Prozent aller
/// Vordergrundpixel innerhalb der Hand-Box liegen. Nur das Degraded-Flag bleibt
/// beim Infrastructure-Validator.
/// </summary>
public static class SamMaskFormatValidator
{
    private const long MinimumContainmentNumerator = 4;
    private const long MinimumContainmentDenominator = 5;
    private const int MinimumContainmentPercent = 80;

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
            out _,
            out reason);

    /// <summary>
    /// Liefert die Vordergrundflaeche direkt aus der streng geprueften RLE.
    /// Sidecar-Metadaten duerfen diese Zahl nicht ungeprueft vorgeben.
    /// </summary>
    public static bool TryGetForegroundPixelCount(
        string? rle,
        int? maskImageWidth,
        int? maskImageHeight,
        out int foregroundPixelCount,
        out string reason)
    {
        foregroundPixelCount = 0;
        if (!TryParse(
                rle,
                maskImageWidth,
                maskImageHeight,
                out _,
                out _,
                out _,
                out _,
                out var parsedForegroundPixelCount,
                out reason))
        {
            return false;
        }

        if (parsedForegroundPixelCount > int.MaxValue)
        {
            reason = "Maskenflaeche ist zu gross.";
            return false;
        }

        foregroundPixelCount = (int)parsedForegroundPixelCount;
        return true;
    }

    /// <summary>
    /// Prueft zusaetzlich, ob mindestens 80 Prozent aller echten Vordergrundpixel
    /// mit ihrem Pixelzentrum innerhalb der normalisierten Hand-Box liegen.
    /// Der bestehende Methodenname bleibt als oeffentliche Fassade erhalten.
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
                out var foregroundPixels,
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
        long foregroundPixelsInsideBox = 0;
        var currentIsMask = startValue != 0;
        foreach (var run in runs)
        {
            if (currentIsMask && run > 0)
            {
                foregroundPixelsInsideBox += CountRunPixelsInsideBox(
                    position,
                    position + run - 1L,
                    width,
                    minCol,
                    maxCol,
                    minRow,
                    maxRow);
            }

            position += run;
            currentIsMask = !currentIsMask;
        }

        if (foregroundPixelsInsideBox == 0)
        {
            reason =
                $"Maske gehoert nicht zur Hand-Box (kein Vordergrundpixel innerhalb der Box; "
                + $"mindestens {MinimumContainmentPercent} % erforderlich).";
            return false;
        }

        var requiredInsidePixels =
            (foregroundPixels / MinimumContainmentDenominator) * MinimumContainmentNumerator
            + ((foregroundPixels % MinimumContainmentDenominator) * MinimumContainmentNumerator
               + MinimumContainmentDenominator - 1)
            / MinimumContainmentDenominator;
        if (foregroundPixelsInsideBox < requiredInsidePixels)
        {
            var containmentPercent = (double)foregroundPixelsInsideBox / foregroundPixels * 100.0;
            reason =
                "Maske liegt zu weit ausserhalb der Hand-Box "
                + $"({containmentPercent.ToString("0.0", CultureInfo.InvariantCulture)} % innerhalb; "
                + $"mindestens {MinimumContainmentPercent} % erforderlich).";
            return false;
        }

        reason = string.Empty;
        return true;
    }

    private static bool TryParse(
        string? rle,
        int? maskImageWidth,
        int? maskImageHeight,
        out int startValue,
        out int[] runs,
        out int width,
        out int height,
        out long foregroundPixelCount,
        out string reason)
    {
        startValue = 0;
        runs = [];
        width = 0;
        height = 0;
        foregroundPixelCount = 0;

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
                foregroundPixelCount += run;
            currentIsMask = !currentIsMask;
        }

        var expected = (long)width * height;
        if (runSum != expected)
        {
            reason = $"Masken-RLE passt nicht zu den Bildmassen ({runSum} statt {expected} Pixel).";
            return false;
        }
        if (foregroundPixelCount == 0)
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

    private static long CountRunPixelsInsideBox(
        long runStart,
        long runEnd,
        int imageWidth,
        long minCol,
        long maxCol,
        long minRow,
        long maxRow)
    {
        var firstRow = Math.Max(minRow, runStart / imageWidth);
        var lastRow = Math.Min(maxRow, runEnd / imageWidth);
        if (firstRow > lastRow)
            return 0;

        if (firstRow == lastRow)
            return CountRowIntersection(firstRow);

        var inside = CountRowIntersection(firstRow) + CountRowIntersection(lastRow);
        var completeRows = lastRow - firstRow - 1L;
        if (completeRows > 0)
            inside += completeRows * (maxCol - minCol + 1L);

        return inside;

        long CountRowIntersection(long row)
        {
            var allowedStart = row * imageWidth + minCol;
            var allowedEnd = row * imageWidth + maxCol;
            var intersectionStart = Math.Max(runStart, allowedStart);
            var intersectionEnd = Math.Min(runEnd, allowedEnd);
            return intersectionStart <= intersectionEnd
                ? intersectionEnd - intersectionStart + 1L
                : 0L;
        }
    }
}
