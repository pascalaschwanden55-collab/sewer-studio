using System.IO;
using System.Runtime.ExceptionServices;
using System.Threading;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using AuswertungPro.Next.UI.Ai;

namespace AuswertungPro.Next.UI.Tests;

public sealed class CodingOsdMeterServiceTests
{
    [Fact]
    public async Task ReadMeterAsync_empty_image_does_not_call_model()
    {
        var called = false;
        using var service = new CodingOsdMeterService((_, _) =>
        {
            called = true;
            return Task.FromResult("1.68 m");
        });

        var result = await service.ReadMeterAsync(
            Array.Empty<byte>(),
            frameTimestampSec: 1,
            recentOsdMeter: null,
            recentOsdTimestampSec: null,
            CancellationToken.None);

        Assert.Null(result.Meter);
        Assert.False(called);
    }

    [Fact]
    public void ReadMeterAsync_accepts_model_reply()
    {
        RunSta(async () =>
        {
            var called = false;
            using var service = new CodingOsdMeterService((searchImageBytes, _) =>
            {
                called = true;
                Assert.True(searchImageBytes.Length > 0);
                return Task.FromResult("1.68 m");
            });

            var result = await service.ReadMeterAsync(
                CreatePng(180, 100),
                frameTimestampSec: 5,
                recentOsdMeter: null,
                recentOsdTimestampSec: null,
                CancellationToken.None);

            Assert.True(called);
            Assert.Equal(1.68, result.Meter);
            Assert.Equal(1.68, result.Candidate);
        });
    }

    [Fact]
    public void ReadMeterAsync_rejects_large_jump_without_seek()
    {
        RunSta(async () =>
        {
            using var service = new CodingOsdMeterService((_, _) => Task.FromResult("10.00 m"));

            var result = await service.ReadMeterAsync(
                CreatePng(180, 100),
                frameTimestampSec: 2,
                recentOsdMeter: 1.68,
                recentOsdTimestampSec: 1,
                CancellationToken.None);

            Assert.Null(result.Meter);
            Assert.Equal(10.00, result.Candidate);
            Assert.Equal(1.68, result.RecentMeter);
        });
    }

    [Fact]
    public void ReadMeterAsync_allows_large_jump_after_seek()
    {
        RunSta(async () =>
        {
            using var service = new CodingOsdMeterService((_, _) => Task.FromResult("10.00 m"));

            var result = await service.ReadMeterAsync(
                CreatePng(180, 100),
                frameTimestampSec: 10,
                recentOsdMeter: 1.68,
                recentOsdTimestampSec: 1,
                CancellationToken.None);

            Assert.Equal(10.00, result.Meter);
            Assert.Null(result.RecentMeter);
        });
    }

    private static void RunSta(Func<Task> action)
    {
        Exception? threadError = null;
        var thread = new Thread(() =>
        {
            try
            {
                action().GetAwaiter().GetResult();
            }
            catch (Exception ex)
            {
                threadError = ex;
            }
        });

        thread.SetApartmentState(ApartmentState.STA);
        thread.Start();
        thread.Join();

        if (threadError is not null)
            ExceptionDispatchInfo.Capture(threadError).Throw();
    }

    private static byte[] CreatePng(int width, int height)
    {
        var pixels = new byte[width * height * 4];
        for (var i = 0; i < pixels.Length; i += 4)
        {
            pixels[i + 0] = Colors.Black.B;
            pixels[i + 1] = Colors.Black.G;
            pixels[i + 2] = Colors.Black.R;
            pixels[i + 3] = Colors.Black.A;
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
}
