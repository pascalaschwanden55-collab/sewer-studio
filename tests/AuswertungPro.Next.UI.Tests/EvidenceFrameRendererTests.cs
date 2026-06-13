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

                    var saved = EvidenceFrameRenderer.SaveAnnotatedFrame(
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

    private static string Sha256(string path)
    {
        using var stream = File.OpenRead(path);
        return Convert.ToHexString(SHA256.HashData(stream));
    }
}
