using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using AuswertungPro.Next.UI.PhotoMeasurement;
using Rectangle = System.Windows.Shapes.Rectangle;

namespace AuswertungPro.Next.UI.Tests;

public sealed class PhotoMeasurementOverlayExporterTests
{
    [Fact]
    public void Export_writes_cropped_overlay_as_png_in_original_size_without_changing_source()
        => StaTestRunner.Run(() =>
        {
            var root = CreateTempDirectory();
            try
            {
                var sourcePath = Path.Combine(root, "photo.v1.jpg");
                var sourceBytes = new byte[] { 10, 20, 30, 40 };
                File.WriteAllBytes(sourcePath, sourceBytes);
                var expectedOutputPath = Path.Combine(root, "photo.v1_overlay.png");
                File.WriteAllBytes(expectedOutputPath, [1, 2, 3]);
                var photo = CreateSolidBitmap(20, 10, Color.FromRgb(10, 20, 200));
                var overlay = CreateLetterboxedOverlay();

                var outputPath = new PhotoMeasurementOverlayExporter().Export(
                    photo,
                    overlay,
                    new Rect(0, 25, 100, 50),
                    sourcePath);

                Assert.Equal(expectedOutputPath, outputPath);
                Assert.Equal(sourceBytes, File.ReadAllBytes(sourcePath));
                var output = LoadBitmap(expectedOutputPath);
                Assert.Equal(20, output.PixelWidth);
                Assert.Equal(10, output.PixelHeight);
                Assert.InRange(output.DpiX, 95.9, 96.1);
                Assert.InRange(output.DpiY, 95.9, 96.1);

                var overlayPixel = ReadPixel(output, x: 2, y: 1);
                Assert.True(
                    overlayPixel.R > 220 && overlayPixel.G < 40 && overlayPixel.B < 40,
                    $"Overlay-Pixel muss rot sein: {overlayPixel}");
                var untouchedPixel = ReadPixel(output, x: 17, y: 1);
                Assert.True(
                    untouchedPixel.B > 180 && untouchedPixel.R < 40 && untouchedPixel.G < 40,
                    $"Transparenter Bereich muss das blaue Foto behalten: {untouchedPixel}");
            }
            finally
            {
                Directory.Delete(root, recursive: true);
            }
        });

    [Fact]
    public void Export_returns_null_without_bitmap_or_valid_rendered_rect_and_writes_nothing()
        => StaTestRunner.Run(() =>
        {
            var root = CreateTempDirectory();
            try
            {
                var sourcePath = Path.Combine(root, "photo.jpg");
                var overlay = Arrange(new Canvas { Width = 100, Height = 100 });
                var exporter = new PhotoMeasurementOverlayExporter();

                var withoutBitmap = exporter.Export(
                    null,
                    overlay,
                    new Rect(0, 0, 100, 100),
                    sourcePath);
                var withoutRenderedArea = exporter.Export(
                    CreateSolidBitmap(20, 10, Colors.Blue),
                    overlay,
                    new Rect(0, 0, 0, 100),
                    sourcePath);

                Assert.Null(withoutBitmap);
                Assert.Null(withoutRenderedArea);
                Assert.False(File.Exists(Path.Combine(root, "photo_overlay.png")));
            }
            finally
            {
                Directory.Delete(root, recursive: true);
            }
        });

    [Fact]
    public void Export_with_locked_target_propagates_io_error_without_truncating_existing_file()
        => StaTestRunner.Run(() =>
        {
            var root = CreateTempDirectory();
            try
            {
                var sourcePath = Path.Combine(root, "photo.jpg");
                var outputPath = Path.Combine(root, "photo_overlay.png");
                File.WriteAllBytes(outputPath, [1, 2, 3]);
                var overlay = Arrange(new Canvas { Width = 100, Height = 100 });
                using (File.Open(outputPath, FileMode.Open, FileAccess.Read, FileShare.None))
                {
                    Assert.Throws<IOException>(() => new PhotoMeasurementOverlayExporter().Export(
                        CreateSolidBitmap(20, 10, Colors.Blue),
                        overlay,
                        new Rect(0, 0, 100, 100),
                        sourcePath));
                }

                Assert.Equal(new byte[] { 1, 2, 3 }, File.ReadAllBytes(outputPath));
            }
            finally
            {
                Directory.Delete(root, recursive: true);
            }
        });

    private static Canvas CreateLetterboxedOverlay()
    {
        var canvas = new Canvas
        {
            Width = 100,
            Height = 100,
            Background = Brushes.Transparent
        };
        var visibleMarker = new Rectangle
        {
            Width = 50,
            Height = 50,
            Fill = Brushes.Red
        };
        Canvas.SetLeft(visibleMarker, 0);
        Canvas.SetTop(visibleMarker, 25);
        canvas.Children.Add(visibleMarker);

        var letterboxMarker = new Rectangle
        {
            Width = 100,
            Height = 20,
            Fill = Brushes.Lime
        };
        Canvas.SetTop(letterboxMarker, 0);
        canvas.Children.Add(letterboxMarker);
        return Arrange(canvas);
    }

    private static Canvas Arrange(Canvas canvas)
    {
        canvas.Measure(new Size(canvas.Width, canvas.Height));
        canvas.Arrange(new Rect(0, 0, canvas.Width, canvas.Height));
        canvas.UpdateLayout();
        return canvas;
    }

    private static BitmapSource CreateSolidBitmap(int width, int height, Color color)
    {
        var pixels = new byte[width * height * 4];
        for (var offset = 0; offset < pixels.Length; offset += 4)
        {
            pixels[offset] = color.B;
            pixels[offset + 1] = color.G;
            pixels[offset + 2] = color.R;
            pixels[offset + 3] = byte.MaxValue;
        }

        var bitmap = BitmapSource.Create(
            width,
            height,
            96,
            96,
            PixelFormats.Bgra32,
            palette: null,
            pixels,
            width * 4);
        bitmap.Freeze();
        return bitmap;
    }

    private static BitmapSource LoadBitmap(string path)
    {
        using var stream = File.OpenRead(path);
        var decoder = BitmapDecoder.Create(
            stream,
            BitmapCreateOptions.PreservePixelFormat,
            BitmapCacheOption.OnLoad);
        var frame = decoder.Frames[0];
        frame.Freeze();
        return frame;
    }

    private static Color ReadPixel(BitmapSource bitmap, int x, int y)
    {
        var converted = new FormatConvertedBitmap(bitmap, PixelFormats.Bgra32, null, 0);
        var pixel = new byte[4];
        converted.CopyPixels(new Int32Rect(x, y, 1, 1), pixel, 4, 0);
        return Color.FromArgb(pixel[3], pixel[2], pixel[1], pixel[0]);
    }

    private static string CreateTempDirectory()
    {
        var path = Path.Combine(
            Path.GetTempPath(),
            "sewerstudio-photo-overlay-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(path);
        return path;
    }
}
