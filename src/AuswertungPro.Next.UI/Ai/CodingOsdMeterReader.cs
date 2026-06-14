using System.Globalization;
using System.IO;
using System.Text.RegularExpressions;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace AuswertungPro.Next.UI.Ai;

public static class CodingOsdMeterReader
{
    public const string Prompt = """
        Das Bild ist ein Suchbild aus typischen OSD-Zonen: oben/unten und links/mitte/rechts.
        Lies NUR den eindeutig sichtbaren Meterstand der Kanalinspektion.
        Der Wert ist eine Dezimalzahl wie 1.68, 2.64 oder 14.98, meistens direkt bei "m".
        Ignoriere Dateipfade, Datum, DN, Uhrzeit, Codes und andere Zahlen.
        Antworte nur mit der Zahl. Kein Text.
        Wenn kein eindeutiger Meterstand sichtbar ist: null
        """;

    private static readonly Regex MeterPattern = new(
        @"(?<!\d)(\d{1,3}[.,]\d{1,2})\s*(m)?\b",
        RegexOptions.Compiled | RegexOptions.IgnoreCase);

    // Datum (10.06.2026) und Uhrzeit (14:30) werden VOR der Meter-Suche entfernt,
    // sonst wird z.B. ein Datum faelschlich als Meterstand 10.06 gelesen.
    private static readonly Regex DateTimePattern = new(
        @"\b\d{1,2}[.,]\d{1,2}[.,]\d{2,4}\b|\b\d{1,2}:\d{2}(?::\d{2})?\b",
        RegexOptions.Compiled);

    private const double MaxPlausibleMeter = 500.0;
    private const double MaxRecentJumpMeters = 3.0;
    private const double BandHeightRatio = 0.26;
    private const double TileScale = 2.0;
    private const int TileGap = 6;

    public static double? ParseMeterReply(string? raw)
    {
        if (string.IsNullOrWhiteSpace(raw))
            return null;

        var text = raw.Trim();
        if (text.Equals("null", StringComparison.OrdinalIgnoreCase))
            return null;

        // Komma -> Punkt, danach Datum/Uhrzeit entfernen, bevor nach dem Meterstand gesucht wird.
        var normalized = DateTimePattern.Replace(text.Replace(',', '.'), " ");

        var withUnit = new List<double>();
        var withoutUnit = new List<double>();
        foreach (Match match in MeterPattern.Matches(normalized))
        {
            if (!double.TryParse(match.Groups[1].Value, NumberStyles.Float, CultureInfo.InvariantCulture, out var meter))
                continue;
            if (meter is < 0 or > MaxPlausibleMeter)
                continue;

            var rounded = Math.Round(meter, 2);
            if (match.Groups[2].Success)
                withUnit.Add(rounded);
            else
                withoutUnit.Add(rounded);
        }

        // Der Meterstand steht laut OSD-Konvention direkt bei "m" -> Zahlen mit Einheit bevorzugen.
        var candidates = (withUnit.Count > 0 ? withUnit : withoutUnit).Distinct().ToList();
        if (candidates.Count != 1)
            return null;

        return candidates[0];
    }

    public static double? AcceptMeterCandidate(double? candidate, double? recentOsdMeter)
    {
        if (candidate is not (>= 0 and <= MaxPlausibleMeter))
            return null;

        if (recentOsdMeter is >= 0 and <= MaxPlausibleMeter
            && Math.Abs(candidate.Value - recentOsdMeter.Value) > MaxRecentJumpMeters)
        {
            return null;
        }

        return Math.Round(candidate.Value, 2);
    }

    public static byte[] BuildOsdSearchImage(byte[] pngBytes)
    {
        if (pngBytes.Length == 0)
            return pngBytes;

        using var stream = new MemoryStream(pngBytes);
        var decoder = BitmapDecoder.Create(stream, BitmapCreateOptions.PreservePixelFormat, BitmapCacheOption.OnLoad);
        var frame = decoder.Frames[0];
        var width = frame.PixelWidth;
        var height = frame.PixelHeight;
        if (width <= 0 || height <= 0)
            return pngBytes;

        var regions = BuildSearchRegions(width, height);
        var tileWidth = Math.Max(1, (int)Math.Round(regions.Max(r => r.Width) * TileScale));
        var tileHeight = Math.Max(1, (int)Math.Round(regions.Max(r => r.Height) * TileScale));
        var outputWidth = tileWidth * 3 + TileGap * 2;
        var outputHeight = tileHeight * 2 + TileGap;

        var visual = new DrawingVisual();
        using (var dc = visual.RenderOpen())
        {
            dc.DrawRectangle(Brushes.Black, null, new Rect(0, 0, outputWidth, outputHeight));
            for (var i = 0; i < regions.Count; i++)
            {
                var col = i % 3;
                var row = i / 3;
                var target = new Rect(
                    col * (tileWidth + TileGap),
                    row * (tileHeight + TileGap),
                    tileWidth,
                    tileHeight);

                var crop = new CroppedBitmap(frame, regions[i]);
                crop.Freeze();
                dc.DrawImage(crop, target);
                dc.DrawRectangle(null, new Pen(Brushes.DimGray, 1), target);
            }
        }

        var rendered = new RenderTargetBitmap(outputWidth, outputHeight, 96, 96, PixelFormats.Pbgra32);
        rendered.Render(visual);
        rendered.Freeze();

        var encoder = new PngBitmapEncoder();
        encoder.Frames.Add(BitmapFrame.Create(rendered));
        using var output = new MemoryStream();
        encoder.Save(output);
        return output.ToArray();
    }

    private static IReadOnlyList<Int32Rect> BuildSearchRegions(int width, int height)
    {
        var bandHeight = Math.Max(1, (int)Math.Round(height * BandHeightRatio));
        var tileWidth = Math.Max(1, (int)Math.Round(width * 0.40));
        var yTop = 0;
        var yBottom = Math.Max(0, height - bandHeight);
        var xLeft = 0;
        var xCenter = Math.Clamp((width - tileWidth) / 2, 0, Math.Max(0, width - tileWidth));
        var xRight = Math.Max(0, width - tileWidth);

        return
        [
            ClampRect(xLeft, yTop, tileWidth, bandHeight, width, height),
            ClampRect(xCenter, yTop, tileWidth, bandHeight, width, height),
            ClampRect(xRight, yTop, tileWidth, bandHeight, width, height),
            ClampRect(xLeft, yBottom, tileWidth, bandHeight, width, height),
            ClampRect(xCenter, yBottom, tileWidth, bandHeight, width, height),
            ClampRect(xRight, yBottom, tileWidth, bandHeight, width, height)
        ];
    }

    private static Int32Rect ClampRect(int x, int y, int w, int h, int imageWidth, int imageHeight)
    {
        x = Math.Clamp(x, 0, Math.Max(0, imageWidth - 1));
        y = Math.Clamp(y, 0, Math.Max(0, imageHeight - 1));
        w = Math.Clamp(w, 1, imageWidth - x);
        h = Math.Clamp(h, 1, imageHeight - y);
        return new Int32Rect(x, y, w, h);
    }
}
