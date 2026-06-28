using System.Globalization;

// Dekodiert ein RLE-komprimiertes Binärbild (kommaseparierte Runs) in eine bool[Hoehe, Breite]-Matrix.
internal static class RleMaskDecoder
{
    public static bool[,] Decode(string rle, int width, int height)
    {
        var mask = new bool[height, width];
        if (string.IsNullOrWhiteSpace(rle) || width <= 0 || height <= 0)
            return mask;

        var parts = rle.Split(',', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries);
        if (parts.Length < 2 || !int.TryParse(parts[0], NumberStyles.Integer, CultureInfo.InvariantCulture, out var startValue))
            return mask;

        var current = startValue != 0;
        var pos = 0;
        var total = width * height;

        for (var i = 1; i < parts.Length && pos < total; i++)
        {
            if (!int.TryParse(parts[i], NumberStyles.Integer, CultureInfo.InvariantCulture, out var runLength) || runLength <= 0)
            {
                current = !current;
                continue;
            }

            var end = Math.Min(pos + runLength, total);
            if (current)
            {
                for (var p = pos; p < end; p++)
                    mask[p / width, p % width] = true;
            }

            pos = end;
            current = !current;
        }

        return mask;
    }
}
