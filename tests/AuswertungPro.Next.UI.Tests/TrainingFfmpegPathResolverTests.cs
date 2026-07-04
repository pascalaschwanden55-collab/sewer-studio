using System.IO;
using AuswertungPro.Next.UI.Ai.Training;

namespace AuswertungPro.Next.UI.Tests;

public sealed class TrainingFfmpegPathResolverTests
{
    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void Resolve_liefert_ffmpeg_fuer_leere_pfade(string? path)
    {
        Assert.Equal("ffmpeg", TrainingFfmpegPathResolver.Resolve(path));
    }

    [Theory]
    [InlineData("ffmpeg")]
    [InlineData("FFMPEG")]
    public void Resolve_bewahrt_ffmpeg_alias(string path)
    {
        Assert.Equal(path, TrainingFfmpegPathResolver.Resolve(path));
    }

    [Fact]
    public void Resolve_bewahrt_existierende_datei()
    {
        var path = Path.GetTempFileName();
        try
        {
            Assert.Equal(path, TrainingFfmpegPathResolver.Resolve(path));
        }
        finally
        {
            File.Delete(path);
        }
    }

    [Fact]
    public void Resolve_faellt_bei_unbekannter_datei_auf_ffmpeg_zurueck()
    {
        var path = Path.Combine(Path.GetTempPath(), Guid.NewGuid() + ".exe");

        Assert.Equal("ffmpeg", TrainingFfmpegPathResolver.Resolve(path));
    }

}
