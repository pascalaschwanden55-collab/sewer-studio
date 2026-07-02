using System.IO;
using AuswertungPro.Next.UI.ViewModels.Windows;
using static AuswertungPro.Next.UI.Tests.TestRepoPaths;

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

    [Fact]
    public void TrainingCenterViewModel_enthaelt_keinen_ffmpeg_path_resolver_mehr()
    {
        var root = FindRepositoryRoot();
        var source = File.ReadAllText(Path.Combine(
            root,
            "src",
            "AuswertungPro.Next.UI",
            "ViewModels",
            "Windows",
            "TrainingCenterViewModel.cs"));

        Assert.DoesNotContain("private static string ResolveFfmpegPath", source, StringComparison.Ordinal);
    }

}
