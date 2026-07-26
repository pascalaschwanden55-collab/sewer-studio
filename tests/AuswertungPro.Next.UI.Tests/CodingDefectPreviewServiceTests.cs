using System;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Threading;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using AuswertungPro.Next.Domain.Models;
using AuswertungPro.Next.Domain.Protocol;
using AuswertungPro.Next.UI.Ai;
using AuswertungPro.Next.UI.Ai.Coding;
using AuswertungPro.Next.UI.Ai.Evidence;
using Xunit;

namespace AuswertungPro.Next.UI.Tests;

public sealed class CodingDefectPreviewServiceTests
{
    [Fact]
    public void BuildPreviewImagePath_NutztInjiziertenBildRenderer()
    {
        var root = Path.Combine(Path.GetTempPath(), "sewerstudio-preview-injected-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        try
        {
            var rawPath = Path.Combine(root, "raw.png");
            File.WriteAllBytes(rawPath, [1]);
            var previewRoot = Path.Combine(root, "preview");
            var renderer = new RecordingEvidenceFrameRenderer();
            var service = new CodingDefectPreviewRenderer(renderer);
            var ev = new CodingEvent
            {
                EventId = Guid.Parse("33333333-3333-3333-3333-333333333333"),
                Entry = new ProtocolEntry { Code = "BCA" }
            };
            ev.Entry.FotoPaths.Add(rawPath);

            var previewPath = service.BuildPreviewImagePath(ev, previewRoot);

            var expectedPath = Path.Combine(previewRoot, "33333333333333333333333333333333_preview.png");
            Assert.Equal(expectedPath, previewPath);
            Assert.Equal(rawPath, renderer.SourcePath);
            Assert.Equal(expectedPath, renderer.OutputPath);
            Assert.Equal("BCA", renderer.Annotation?.Code);
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }

    [Fact]
    public void BuildPreviewImagePath_ErzeugtMarkierteVorschauAusBefundfoto()
    {
        Exception? threadError = null;

        var thread = new Thread(() =>
        {
            try
            {
                var root = Path.Combine(Path.GetTempPath(), "sewerstudio-preview-" + Guid.NewGuid().ToString("N"));
                Directory.CreateDirectory(root);
                try
                {
                    var rawPath = Path.Combine(root, "raw.png");
                    var previewRoot = Path.Combine(root, "preview");
                    WriteSolidPng(rawPath, 100, 70);
                    var rawHash = Sha256(rawPath);

                    var ev = new CodingEvent
                    {
                        EventId = Guid.Parse("11111111-1111-1111-1111-111111111111"),
                        Entry = new ProtocolEntry { Code = "BCA", Beschreibung = "Anschluss" },
                        AiContext = new CodingEventAiContext { Confidence = 0.82 },
                        Overlay = new OverlayGeometry
                        {
                            ToolType = OverlayToolType.Rectangle,
                            Points =
                            [
                                new NormalizedPoint(0.2, 0.2),
                                new NormalizedPoint(0.6, 0.2),
                                new NormalizedPoint(0.6, 0.6),
                                new NormalizedPoint(0.2, 0.6)
                            ]
                        }
                    };
                    ev.Entry.FotoPaths.Add(rawPath);

                    var previewPath = CodingDefectPreviewService.BuildPreviewImagePath(ev, previewRoot);

                    Assert.NotNull(previewPath);
                    Assert.True(File.Exists(previewPath));
                    Assert.NotEqual(rawPath, previewPath);
                    Assert.Equal(rawHash, Sha256(rawPath));
                    Assert.NotEqual(rawHash, Sha256(previewPath!));
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
    public void BuildPreviewImagePath_GibtNullZurueckWennKeinFotoVorhandenIst()
    {
        var ev = new CodingEvent
        {
            Entry = new ProtocolEntry { Code = "BCA" },
            AiContext = new CodingEventAiContext { Confidence = 0.5 }
        };

        var previewPath = CodingDefectPreviewService.BuildPreviewImagePath(
            ev,
            Path.Combine(Path.GetTempPath(), "sewerstudio-preview-empty"));

        Assert.Null(previewPath);
    }

    [Fact]
    public void BuildPreviewImagePath_NutztSamMaskeAusAiContext()
    {
        Exception? threadError = null;

        var thread = new Thread(() =>
        {
            try
            {
                var root = Path.Combine(Path.GetTempPath(), "sewerstudio-preview-mask-" + Guid.NewGuid().ToString("N"));
                Directory.CreateDirectory(root);
                try
                {
                    var rawPath = Path.Combine(root, "raw.png");
                    var previewRoot = Path.Combine(root, "preview");
                    WriteSolidPng(rawPath, 60, 60);

                    var ev = new CodingEvent
                    {
                        EventId = Guid.Parse("22222222-2222-2222-2222-222222222222"),
                        Entry = new ProtocolEntry { Code = "BBA", Beschreibung = "Wurzeln" },
                        AiContext = new CodingEventAiContext
                        {
                            Confidence = 0.91,
                            SamMaskRle = EncodeMask(60, 60, x => x is >= 25 and <= 34, y => y is >= 25 and <= 34),
                            SamMaskImageWidth = 60,
                            SamMaskImageHeight = 60
                        }
                    };
                    ev.Entry.FotoPaths.Add(rawPath);

                    var previewPath = CodingDefectPreviewService.BuildPreviewImagePath(ev, previewRoot);

                    Assert.NotNull(previewPath);
                    var center = ReadPixel(previewPath!, 30, 30);
                    Assert.True(center.G > center.R && center.G > center.B, $"Maskenpixel muss gruen markiert sein: R={center.R} G={center.G} B={center.B}");
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
        var pixels = Enumerable.Repeat((byte)230, width * height * 4).ToArray();
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

    private static string Sha256(string path)
    {
        using var stream = File.OpenRead(path);
        return Convert.ToHexString(SHA256.HashData(stream));
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

    private sealed class RecordingEvidenceFrameRenderer : IEvidenceFrameRenderer
    {
        public string? SourcePath { get; private set; }

        public string? OutputPath { get; private set; }

        public EvidenceFrameAnnotation? Annotation { get; private set; }

        public bool SaveAnnotatedFrame(
            string sourceImagePath,
            string outputImagePath,
            EvidenceFrameAnnotation annotation)
        {
            SourcePath = sourceImagePath;
            OutputPath = outputImagePath;
            Annotation = annotation;
            return true;
        }
    }
}
