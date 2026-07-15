using System;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Threading;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using AuswertungPro.Next.UI.Ai;
using Xunit;

namespace AuswertungPro.Next.UI.Tests;

public sealed class EvidenceFrameRendererTests
{
    [Fact]
    public void SaveAnnotatedFrame_ErzeugtMarkiertesBildOhneRohbildZuAendern()
    {
        Exception? threadError = null;

        var thread = new Thread(() =>
        {
            try
            {
                var root = Path.Combine(Path.GetTempPath(), "sewerstudio-evidence-" + Guid.NewGuid().ToString("N"));
                Directory.CreateDirectory(root);
                try
                {
                    var rawPath = Path.Combine(root, "raw.png");
                    var annotatedPath = Path.Combine(root, "out", "annotated.png");
                    WriteSolidPng(rawPath, width: 80, height: 60);
                    var rawHashBefore = Sha256(rawPath);

                    var saved = new EvidenceFrameImageRenderer().SaveAnnotatedFrame(
                        rawPath,
                        annotatedPath,
                        new EvidenceFrameAnnotation(
                            Code: "BCA",
                            Confidence: 0.82,
                            BboxXCenter: 0.5,
                            BboxYCenter: 0.5,
                            BboxWidth: 0.4,
                            BboxHeight: 0.3));

                    Assert.True(saved);
                    Assert.True(File.Exists(annotatedPath));
                    Assert.Equal(rawHashBefore, Sha256(rawPath));
                    Assert.NotEqual(rawHashBefore, Sha256(annotatedPath));
                }
                finally
                {
                    Directory.Delete(root, recursive: true);
                }
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

    [Fact]
    public void SaveAnnotatedFrame_BrenntSamSegmentierungInsBeweisbild()
    {
        Exception? threadError = null;

        var thread = new Thread(() =>
        {
            try
            {
                var root = Path.Combine(Path.GetTempPath(), "sewerstudio-evidence-mask-" + Guid.NewGuid().ToString("N"));
                Directory.CreateDirectory(root);
                try
                {
                    var rawPath = Path.Combine(root, "raw.png");
                    var annotatedPath = Path.Combine(root, "annotated.png");
                    WriteSolidPng(rawPath, width: 60, height: 60);

                    var saved = EvidenceFrameRenderer.SaveAnnotatedFrame(
                        rawPath,
                        annotatedPath,
                        new EvidenceFrameAnnotation(
                            Code: "BBA",
                            Confidence: 0.91,
                            BboxXCenter: 0.5,
                            BboxYCenter: 0.5,
                            BboxWidth: 0.4,
                            BboxHeight: 0.4,
                            MaskRle: EncodeMask(60, 60, x => x is >= 25 and <= 34, y => y is >= 25 and <= 34),
                            MaskImageWidth: 60,
                            MaskImageHeight: 60));

                    Assert.True(saved);
                    var center = ReadPixel(annotatedPath, 30, 30);
                    Assert.True(center.G > center.R + 20, $"Maskenpixel muss gruen markiert sein: R={center.R} G={center.G} B={center.B}");
                }
                finally
                {
                    Directory.Delete(root, recursive: true);
                }
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

    private static void WriteSolidPng(string path, int width, int height)
    {
        var pixels = Enumerable.Repeat((byte)245, width * height * 4).ToArray();
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
        using var stream = File.Create(path);
        encoder.Save(stream);
    }

    private static string EncodeMask(int width, int height, Func<int, bool> xMatch, Func<int, bool> yMatch)
    {
        var runs = new List<int>();
        var current = false;
        var run = 0;

        for (var y = 0; y < height; y++)
        {
            for (var x = 0; x < width; x++)
            {
                var value = xMatch(x) && yMatch(y);
                if (value == current)
                {
                    run++;
                    continue;
                }

                runs.Add(run);
                current = value;
                run = 1;
            }
        }

        runs.Add(run);
        return "0," + string.Join(",", runs);
    }

    private static Color ReadPixel(string path, int x, int y)
    {
        using var stream = File.OpenRead(path);
        var decoder = BitmapDecoder.Create(stream, BitmapCreateOptions.PreservePixelFormat, BitmapCacheOption.OnLoad);
        var frame = decoder.Frames[0];
        var converted = new FormatConvertedBitmap(frame, PixelFormats.Bgra32, null, 0);
        var pixel = new byte[4];
        converted.CopyPixels(new System.Windows.Int32Rect(x, y, 1, 1), pixel, 4, 0);
        return Color.FromArgb(pixel[3], pixel[2], pixel[1], pixel[0]);
    }

    private static string Sha256(string path)
    {
        using var stream = File.OpenRead(path);
        return Convert.ToHexString(SHA256.HashData(stream));
    }
}
