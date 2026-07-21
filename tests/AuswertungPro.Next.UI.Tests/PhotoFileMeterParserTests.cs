using AuswertungPro.Next.Application.Media;

namespace AuswertungPro.Next.UI.Tests;

public sealed class PhotoFileMeterParserTests
{
    [Theory]
    [InlineData(@"C:\Fotos\foto_12.5m.jpg", 12.5)]
    [InlineData(@"C:\Fotos\foto_12,5M.JPG", 12.5)]
    [InlineData(@"C:\Fotos\foto_7 m.png", 7)]
    [InlineData(@"C:\Fotos\123.webp", 123)]
    public void TryParseFromPath_erkennt_bisherige_Meterformate(string path, double expected)
    {
        Assert.Equal(expected, PhotoFileMeterParser.TryParseFromPath(path));
    }

    [Theory]
    [InlineData(@"C:\Fotos\foto_1000m.jpg", 0)]
    [InlineData(@"C:\Fotos\foto_12.345m.jpg", 345)]
    public void TryParseFromPath_bewahrt_bisheriges_dreistelliges_Suffixverhalten(
        string path,
        double expected)
    {
        Assert.Equal(expected, PhotoFileMeterParser.TryParseFromPath(path));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData(@"C:\Fotos\ohne_meter.jpg")]
    public void TryParseFromPath_gibt_null_fuer_ungueltige_Namen(string? path)
    {
        Assert.Null(PhotoFileMeterParser.TryParseFromPath(path));
    }
}
