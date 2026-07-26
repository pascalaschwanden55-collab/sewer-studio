using System;
using System.IO;
using System.Threading;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using AuswertungPro.Next.UI.Ai;
using AuswertungPro.Next.UI.Ai.Coding;

namespace AuswertungPro.Next.UI.Tests;

public sealed class CodingOsdMeterReaderTests
{
    [Fact]
    public void ParseMeterReply_reads_decimal_meter_and_ignores_null_text()
    {
        Assert.Equal(1.68, CodingOsdMeterReader.ParseMeterReply("1.68 m"));
        Assert.Equal(2.64, CodingOsdMeterReader.ParseMeterReply("Meterstand: 2,64"));
        Assert.Null(CodingOsdMeterReader.ParseMeterReply("null"));
        Assert.Null(CodingOsdMeterReader.ParseMeterReply("kein meter"));
        Assert.Null(CodingOsdMeterReader.ParseMeterReply("1.68 oder 10.00"));
        // Datum/Uhrzeit duerfen NICHT als Meterstand gelesen werden (Quelle der falschen "10.00 m").
        Assert.Null(CodingOsdMeterReader.ParseMeterReply("10.06.2026"));
        Assert.Equal(1.68, CodingOsdMeterReader.ParseMeterReply("10.06.2026  1.68 m"));
        Assert.Equal(1.68, CodingOsdMeterReader.ParseMeterReply("14:30  1.68m"));
        // Zahl mit Einheit "m" wird gegenueber Zahl ohne Einheit bevorzugt.
        Assert.Equal(1.68, CodingOsdMeterReader.ParseMeterReply("1.68 m   12.50"));
    }

    [Fact]
    public void AcceptMeterCandidate_rejects_large_jump_from_recent_osd()
    {
        Assert.Equal(1.70, CodingOsdMeterReader.AcceptMeterCandidate(1.70, recentOsdMeter: 1.68));
        Assert.Null(CodingOsdMeterReader.AcceptMeterCandidate(10.00, recentOsdMeter: 1.68));
        Assert.Null(CodingOsdMeterReader.AcceptMeterCandidate(501.00, recentOsdMeter: 1.68));
        // Erste Messung (kein Vorwert) wird immer akzeptiert - auch ein grosser Wert.
        Assert.Equal(14.98, CodingOsdMeterReader.AcceptMeterCandidate(14.98, recentOsdMeter: null));
    }

    [Fact]
    public void BuildOsdSearchImage_includes_top_and_bottom_osd_regions()
    {
        Exception? threadError = null;

        var thread = new Thread(() =>
        {
            try
            {
                var png = CreateRegionPng(180, 100);
                var searchImage = CodingOsdMeterReader.BuildOsdSearchImage(png);
                var size = ReadSize(searchImage);

                Assert.True(size.Width >= 300, $"Suchbild muss fuer OCR hochskaliert werden, Breite={size.Width}.");
                Assert.True(size.Height >= 100, $"Suchbild muss fuer OCR hochskaliert werden, Hoehe={size.Height}.");

                AssertColorDominates(ReadPixelAt(searchImage, size.Width / 6, size.Height / 4), "red");
                AssertColorDominates(ReadPixelAt(searchImage, size.Width / 2, size.Height / 4), "lime");
                AssertColorDominates(ReadPixelAt(searchImage, size.Width * 5 / 6, size.Height / 4), "blue");
                AssertColorDominates(ReadPixelAt(searchImage, size.Width / 6, size.Height * 3 / 4), "yellow");
                AssertColorDominates(ReadPixelAt(searchImage, size.Width / 2, size.Height * 3 / 4), "magenta");
                AssertColorDominates(ReadPixelAt(searchImage, size.Width * 5 / 6, size.Height * 3 / 4), "cyan");
            }
            catch (Exception ex)
            {
                threadError = ex;
            }
        });
        thread.SetApartmentState(ApartmentState.STA);
        thread.Start();
        thread.Join();

        Assert.Null(threadError);
    }

    private static byte[] CreateRegionPng(int width, int height)
    {
        var pixels = new byte[width * height * 4];
        for (var y = 0; y < height; y++)
        {
            for (var x = 0; x < width; x++)
            {
                var offset = (y * width + x) * 4;
                var col = Math.Min(2, x * 3 / width);
                var row = y >= height / 2 ? 1 : 0;
                var color = (row, col) switch
                {
                    (0, 0) => Colors.Red,
                    (0, 1) => Colors.Lime,
                    (0, 2) => Colors.Blue,
                    (1, 0) => Colors.Yellow,
                    (1, 1) => Colors.Magenta,
                    _ => Colors.Cyan
                };
                pixels[offset + 0] = color.B;
                pixels[offset + 1] = color.G;
                pixels[offset + 2] = color.R;
                pixels[offset + 3] = color.A;
            }
        }

        var bitmap = BitmapSource.Create(
            width,
            height,
            96,
            96,
            PixelFormats.Bgra32,
            palette: null,
            pixels,
            stride: width * 4);

        var encoder = new PngBitmapEncoder();
        encoder.Frames.Add(BitmapFrame.Create(bitmap));
        using var stream = new MemoryStream();
        encoder.Save(stream);
        return stream.ToArray();
    }

    private static Color ReadPixelAt(byte[] png, int x, int y)
    {
        using var stream = new MemoryStream(png);
        var decoder = BitmapDecoder.Create(stream, BitmapCreateOptions.PreservePixelFormat, BitmapCacheOption.OnLoad);
        var frame = decoder.Frames[0];
        var converted = new FormatConvertedBitmap(frame, PixelFormats.Bgra32, null, 0);
        var pixel = new byte[4];
        converted.CopyPixels(
            new System.Windows.Int32Rect(x, y, 1, 1),
            pixel,
            4,
            0);
        return Color.FromArgb(pixel[3], pixel[2], pixel[1], pixel[0]);
    }

    private static (int Width, int Height) ReadSize(byte[] png)
    {
        using var stream = new MemoryStream(png);
        var decoder = BitmapDecoder.Create(stream, BitmapCreateOptions.PreservePixelFormat, BitmapCacheOption.OnLoad);
        var frame = decoder.Frames[0];
        return (frame.PixelWidth, frame.PixelHeight);
    }

    private static void AssertColorDominates(Color color, string expected)
    {
        var ok = expected switch
        {
            "red" => color.R > color.G + 40 && color.R > color.B + 40,
            "lime" => color.G > color.R + 40 && color.G > color.B + 40,
            "blue" => color.B > color.R + 40 && color.B > color.G + 40,
            "yellow" => color.R > 180 && color.G > 180 && color.B < 80,
            "magenta" => color.R > 180 && color.B > 180 && color.G < 80,
            "cyan" => color.G > 180 && color.B > 180 && color.R < 80,
            _ => false
        };

        Assert.True(ok, $"Erwartet {expected}, gelesen R={color.R} G={color.G} B={color.B}");
    }
}
