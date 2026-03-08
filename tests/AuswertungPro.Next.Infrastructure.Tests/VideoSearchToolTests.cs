using System;
using System.IO;
using AuswertungPro.Next.Domain.Models;
using AuswertungPro.Next.Infrastructure.Media;

namespace AuswertungPro.Next.Infrastructure.Tests;

public sealed class VideoSearchToolTests
{
    [Fact]
    public void ResolveForRecord_ReturnsUniqueDatePrefixedVideo()
    {
        using var dir = new TempDir();
        var holdingDir = Directory.CreateDirectory(Path.Combine(dir.Path, "100-200")).FullName;
        File.WriteAllText(Path.Combine(holdingDir, "20250101_100-200.pdf"), "pdf");
        var expectedVideo = Path.Combine(holdingDir, "20250101_100-200.mp4");
        File.WriteAllText(expectedVideo, "video");

        var record = new HaltungRecord();
        record.SetFieldValue("Haltungsname", "100-200", FieldSource.Manual, userEdited: false);

        var sut = new VideoSearchTool(dir.Path);
        var result = sut.ResolveForRecord(record);

        Assert.True(result.Success, result.Message);
        Assert.Equal(expectedVideo, result.VideoPath);
        Assert.Equal("20250101_100-200.pdf", Path.GetFileName(result.PdfPath));
    }

    [Fact]
    public void ResolveForRecord_DoesNotAutoLinkWhenMultipleVideosMatch()
    {
        using var dir = new TempDir();
        var holdingDir = Directory.CreateDirectory(Path.Combine(dir.Path, "100-200")).FullName;
        File.WriteAllText(Path.Combine(holdingDir, "report_100-200.pdf"), "pdf");
        File.WriteAllText(Path.Combine(holdingDir, "alpha_100-200.mp4"), "video-a");
        File.WriteAllText(Path.Combine(holdingDir, "beta_100-200.mp4"), "video-b");

        var record = new HaltungRecord();
        record.SetFieldValue("Haltungsname", "100-200", FieldSource.Manual, userEdited: false);

        var sut = new VideoSearchTool(dir.Path);
        var result = sut.ResolveForRecord(record);

        Assert.False(result.Success);
        Assert.Contains("Mehrdeutig", result.Message, StringComparison.OrdinalIgnoreCase);
        Assert.NotNull(result.Candidates);
        Assert.Equal(2, result.Candidates!.Count);
    }

    [Fact]
    public void ResolveForRecord_UsesUniqueUnmatchedCandidate()
    {
        using var dir = new TempDir();
        var holdingDir = Directory.CreateDirectory(Path.Combine(dir.Path, "100-200")).FullName;
        File.WriteAllText(Path.Combine(holdingDir, "20250101_100-200.pdf"), "pdf");

        var unmatchedDir = Directory.CreateDirectory(Path.Combine(dir.Path, "__UNMATCHED", "100-200")).FullName;
        var expectedVideo = Path.Combine(unmatchedDir, "legacy_clip.mp4");
        File.WriteAllText(expectedVideo, "video");

        var record = new HaltungRecord();
        record.SetFieldValue("Haltungsname", "100-200", FieldSource.Manual, userEdited: false);

        var sut = new VideoSearchTool(dir.Path);
        var result = sut.ResolveForRecord(record);

        Assert.True(result.Success, result.Message);
        Assert.Contains("UNMATCHED", result.Message, StringComparison.OrdinalIgnoreCase);
        Assert.Equal(expectedVideo, result.VideoPath);
    }

    private sealed class TempDir : IDisposable
    {
        public TempDir()
        {
            Path = System.IO.Path.Combine(System.IO.Path.GetTempPath(), "video_search_tool_" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(Path);
        }

        public string Path { get; }

        public void Dispose()
        {
            try
            {
                if (Directory.Exists(Path))
                    Directory.Delete(Path, recursive: true);
            }
            catch
            {
                // Best effort cleanup.
            }
        }
    }
}
