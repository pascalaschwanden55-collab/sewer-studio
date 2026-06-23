using System.IO;
using AuswertungPro.Next.UI.Player;

namespace AuswertungPro.Next.UI.Tests;

public sealed class PlayerVideoPathGuardTests
{
    [Fact]
    public void Validate_returns_video_path_and_file_display_name_when_file_exists()
    {
        var result = PlayerVideoPathGuard.Validate(
            @"C:\Videos\haltung.mp4",
            path => path == @"C:\Videos\haltung.mp4");

        Assert.Equal(@"C:\Videos\haltung.mp4", result.VideoPath);
        Assert.Equal("haltung.mp4", result.DisplayName);
    }

    [Fact]
    public void Validate_uses_generic_display_name_when_file_name_is_missing()
    {
        var result = PlayerVideoPathGuard.Validate(
            @"C:\Videos\",
            _ => true);

        Assert.Equal("Video", result.DisplayName);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData(@"C:\Videos\missing.mp4")]
    public void Validate_throws_file_not_found_when_path_is_missing_or_file_does_not_exist(string? videoPath)
    {
        var ex = Assert.Throws<FileNotFoundException>(() =>
            PlayerVideoPathGuard.Validate(videoPath, _ => false));

        Assert.Equal("Video nicht gefunden", ex.Message.Split(Environment.NewLine)[0]);
        Assert.Equal(videoPath, ex.FileName);
    }
}
