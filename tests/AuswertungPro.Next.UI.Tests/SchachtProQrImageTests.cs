using System.IO;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using AuswertungPro.Next.UI.Services;
using ZXing;
using ZXing.Common;
using Xunit;

namespace AuswertungPro.Next.UI.Tests;

public sealed class SchachtProQrImageTests : IDisposable
{
    private readonly string _path = Path.Combine(Path.GetTempPath(), Guid.NewGuid() + ".png");
    public void Dispose() => File.Delete(_path);

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void LiestDichtenQrAusBildAuchGedreht(bool rotated)
    {
        var text = "SPQR1:" + string.Concat(Enumerable.Range(0, 180).Select(i => $"{i:X4}A-az_")) + ":01234567";
        var writer = new BarcodeWriterPixelData
        {
            Format = BarcodeFormat.QR_CODE,
            Options = new EncodingOptions { Width = 950, Height = 950, Margin = 4 }
        };
        var pixels = writer.Write(text);
        BitmapSource bitmap = BitmapSource.Create(pixels.Width, pixels.Height, 96, 96,
            PixelFormats.Bgra32, null, pixels.Pixels, pixels.Width * 4);
        if (rotated) bitmap = new TransformedBitmap(bitmap, new RotateTransform(90));
        var encoder = new PngBitmapEncoder();
        encoder.Frames.Add(BitmapFrame.Create(bitmap));
        using (var stream = File.Create(_path)) encoder.Save(stream);
        var before = File.ReadAllBytes(_path);
        Assert.Equal(text, Assert.Single(new QrImageReader().Read(_path, CancellationToken.None)));
        Assert.Equal(before, File.ReadAllBytes(_path));
    }

    [Fact]
    public void LeeresBildLiefertKeinenCode()
    {
        var bitmap = BitmapSource.Create(100, 100, 96, 96, PixelFormats.Gray8, null,
            Enumerable.Repeat((byte)255, 10000).ToArray(), 100);
        var encoder = new PngBitmapEncoder();
        encoder.Frames.Add(BitmapFrame.Create(bitmap));
        using (var stream = File.Create(_path)) encoder.Save(stream);
        Assert.Empty(new QrImageReader().Read(_path, CancellationToken.None));
    }

    [Fact]
    public void AbbruchUndFalscherDateitypWerdenAbgelehnt()
    {
        var reader = new QrImageReader();
        Assert.Throws<OperationCanceledException>(() => reader.Read(_path, new CancellationToken(true)));
        Assert.Throws<InvalidDataException>(() => reader.Read(_path + ".pdf", CancellationToken.None));
    }
}
