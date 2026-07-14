using AuswertungPro.Next.Application.Ai;
using AuswertungPro.Next.Infrastructure.Ai.Shared;

namespace AuswertungPro.Next.Infrastructure.Tests.Ai.Shared;

public sealed class FfmpegFileLocatorTests : IDisposable
{
    private readonly string _root = Path.Combine(
        Path.GetTempPath(),
        "FfmpegFileLocatorTests_" + Guid.NewGuid().ToString("N"));

    [Fact]
    public void Findet_WinGet_Ffmpeg_und_danebenliegendes_Ffprobe()
    {
        var localAppData = Path.Combine(_root, "LocalAppData");
        var bin = Path.Combine(
            localAppData,
            "Microsoft",
            "WinGet",
            "Packages",
            "Gyan.FFmpeg.Test",
            "release",
            "bin");
        var ffmpeg = Path.Combine(bin, "ffmpeg.exe");
        var ffprobe = Path.Combine(bin, "ffprobe.exe");
        Directory.CreateDirectory(bin);
        File.WriteAllText(ffmpeg, string.Empty);
        File.WriteAllText(ffprobe, string.Empty);
        IFfmpegExecutableLocator locator = CreateLocator(localAppData);

        Assert.Equal(ffmpeg, locator.ResolveFfmpeg());
        Assert.Equal(ffprobe, locator.ResolveFfprobe());
        Assert.True(locator.IsFfmpegAvailable());
    }

    [Fact]
    public void Umgebungswert_hat_Vorrang_und_Path_Fallback_gilt_als_verfuegbar()
    {
        var configured = Path.Combine(_root, "Fehlt", "ffmpeg.exe");
        IFfmpegExecutableLocator fromEnvironment = new FfmpegFileLocator(
            key => key == FfmpegLocator.EnvKey ? $"  {configured}  " : null,
            _ => Path.Combine(_root, "Leer"),
            manualFfmpegPath: Path.Combine(_root, "Manuell", "ffmpeg.exe"));
        IFfmpegExecutableLocator fromPath = CreateLocator(
            Path.Combine(_root, "KeinLocalAppData"));

        Assert.Equal(configured, fromEnvironment.ResolveFfmpeg());
        Assert.False(fromEnvironment.IsFfmpegAvailable());
        Assert.Equal("ffmpeg", fromPath.ResolveFfmpeg());
        Assert.Equal("ffprobe", fromPath.ResolveFfprobe());
        Assert.True(fromPath.IsFfmpegAvailable());
    }

    private IFfmpegExecutableLocator CreateLocator(string localAppData)
        => new FfmpegFileLocator(
            _ => null,
            folder => folder == Environment.SpecialFolder.LocalApplicationData
                ? localAppData
                : Path.Combine(_root, "Leer", folder.ToString()),
            manualFfmpegPath: Path.Combine(_root, "Manuell", "ffmpeg.exe"));

    public void Dispose()
    {
        try
        {
            if (Directory.Exists(_root))
                Directory.Delete(_root, recursive: true);
        }
        catch
        {
            // Test-Aufraeumen darf das Ergebnis nicht verdecken.
        }
    }
}
