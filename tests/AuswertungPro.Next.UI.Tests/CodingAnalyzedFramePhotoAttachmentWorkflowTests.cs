using AuswertungPro.Next.UI.Ai;
using AuswertungPro.Next.Domain.Protocol;

namespace AuswertungPro.Next.UI.Tests;

public sealed class CodingAnalyzedFramePhotoAttachmentWorkflowTests
{
    [Fact]
    public void Execute_attaches_preferred_analyzed_frame_and_skips_snapshot_fallback()
    {
        var calls = new List<string>();
        var preferred = new byte[] { 1, 2, 3 };
        var buffered = new byte[] { 4, 5, 6 };
        var entry = new ProtocolEntry();

        var result = CodingAnalyzedFramePhotoAttachmentWorkflow.Execute(
            entry,
            Actions(
                calls,
                getPreferredFrameBytes: () =>
                {
                    calls.Add("preferred");
                    return preferred;
                },
                getBufferedFrameBytes: () =>
                {
                    calls.Add("buffered");
                    return buffered;
                },
                attachAnalyzedFramePhoto: frameBytes =>
                {
                    Assert.Same(preferred, frameBytes);
                    calls.Add("attach:preferred");
                    return "ai.png";
                }));

        Assert.Equal(CodingAnalyzedFramePhotoAttachmentOutcome.AttachedAnalyzedFrame, result.Outcome);
        Assert.Equal("ai.png", result.PhotoPath);
        Assert.Empty(entry.FotoPaths);
        Assert.Equal(["preferred", "attach:preferred"], calls);
    }

    [Fact]
    public void Execute_uses_buffered_frame_when_preferred_frame_is_missing()
    {
        var calls = new List<string>();
        var buffered = new byte[] { 4, 5, 6 };
        var entry = new ProtocolEntry();

        var result = CodingAnalyzedFramePhotoAttachmentWorkflow.Execute(
            entry,
            Actions(
                calls,
                getPreferredFrameBytes: () =>
                {
                    calls.Add("preferred");
                    return null;
                },
                getBufferedFrameBytes: () =>
                {
                    calls.Add("buffered");
                    return buffered;
                },
                attachAnalyzedFramePhoto: frameBytes =>
                {
                    Assert.Same(buffered, frameBytes);
                    calls.Add("attach:buffered");
                    return "buffered.png";
                }));

        Assert.Equal(CodingAnalyzedFramePhotoAttachmentOutcome.AttachedAnalyzedFrame, result.Outcome);
        Assert.Equal("buffered.png", result.PhotoPath);
        Assert.Empty(entry.FotoPaths);
        Assert.Equal(["preferred", "buffered", "attach:buffered"], calls);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData(" ")]
    public void Execute_captures_snapshot_and_appends_fallback_when_ai_photo_is_missing(string? attachedPath)
    {
        var calls = new List<string>();
        var entry = new ProtocolEntry();

        var result = CodingAnalyzedFramePhotoAttachmentWorkflow.Execute(
            entry,
            Actions(
                calls,
                attachAnalyzedFramePhoto: _ =>
                {
                    calls.Add("attach");
                    return attachedPath;
                },
                captureSnapshot: () =>
                {
                    calls.Add("snapshot");
                    return "snapshot.png";
                }));

        Assert.Equal(CodingAnalyzedFramePhotoAttachmentOutcome.FallbackSnapshot, result.Outcome);
        Assert.Equal("snapshot.png", result.PhotoPath);
        Assert.Equal(["snapshot.png"], entry.FotoPaths);
        Assert.Equal(["preferred", "buffered", "attach", "snapshot"], calls);
    }

    private static CodingAnalyzedFramePhotoAttachmentActions Actions(
        List<string> calls,
        Func<byte[]?>? getPreferredFrameBytes = null,
        Func<byte[]?>? getBufferedFrameBytes = null,
        Func<byte[]?, string?>? attachAnalyzedFramePhoto = null,
        Func<string?>? captureSnapshot = null)
        => new(
            GetPreferredFrameBytes: getPreferredFrameBytes ?? (() =>
            {
                calls.Add("preferred");
                return null;
            }),
            GetBufferedFrameBytes: getBufferedFrameBytes ?? (() =>
            {
                calls.Add("buffered");
                return null;
            }),
            AttachAnalyzedFramePhoto: attachAnalyzedFramePhoto ?? (_ =>
            {
                calls.Add("attach");
                return "ai.png";
            }),
            CaptureSnapshot: captureSnapshot ?? (() =>
            {
                calls.Add("snapshot");
                return "snapshot.png";
            }));
}
