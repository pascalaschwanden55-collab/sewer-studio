using AuswertungPro.Next.Domain.Protocol;
using AuswertungPro.Next.UI.Ai;

namespace AuswertungPro.Next.UI.Tests;

public sealed class CodingAnalyzedFramePhotoAttacherTests
{
    [Fact]
    public void Attach_delegates_entry_frame_and_video_path()
    {
        var entry = new ProtocolEntry();
        var frameBytes = new byte[] { 1, 2, 3 };
        ProtocolEntry? capturedEntry = null;
        byte[]? capturedFrameBytes = null;
        string? capturedVideoPath = null;

        var result = CodingAnalyzedFramePhotoAttacher.Attach(
            entry,
            frameBytes,
            @"C:\Videos\haltung.mp4",
            (delegateEntry, delegateFrameBytes, delegateVideoPath) =>
            {
                capturedEntry = delegateEntry;
                capturedFrameBytes = delegateFrameBytes;
                capturedVideoPath = delegateVideoPath;
                return "frame.png";
            });

        Assert.Equal("frame.png", result);
        Assert.Same(entry, capturedEntry);
        Assert.Same(frameBytes, capturedFrameBytes);
        Assert.Equal(@"C:\Videos\haltung.mp4", capturedVideoPath);
    }

    [Fact]
    public void Attach_returns_delegate_result()
    {
        var result = CodingAnalyzedFramePhotoAttacher.Attach(
            new ProtocolEntry(),
            frameBytes: null,
            videoPath: null,
            (_, _, _) => null);

        Assert.Null(result);
    }

    [Fact]
    public void Attach_throws_for_null_entry()
    {
        Assert.Throws<ArgumentNullException>(() => CodingAnalyzedFramePhotoAttacher.Attach(
            null!,
            frameBytes: null,
            videoPath: null,
            (_, _, _) => null));
    }

    [Fact]
    public void Attach_throws_for_null_attach_frame_photo()
    {
        Assert.Throws<ArgumentNullException>(() => CodingAnalyzedFramePhotoAttacher.Attach(
            new ProtocolEntry(),
            frameBytes: null,
            videoPath: null,
            null!));
    }
}
