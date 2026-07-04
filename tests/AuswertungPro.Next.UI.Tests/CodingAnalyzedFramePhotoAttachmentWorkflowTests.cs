using AuswertungPro.Next.UI.Ai;
using AuswertungPro.Next.Domain.Protocol;

namespace AuswertungPro.Next.UI.Tests;

public sealed class CodingAnalyzedFramePhotoAttachmentWorkflowTests
{
    [Fact]
    public async Task ExecuteAsync_attaches_preferred_analyzed_frame_and_skips_snapshot_fallback()
    {
        var calls = new List<string>();
        var preferred = new byte[] { 1, 2, 3 };
        var buffered = new byte[] { 4, 5, 6 };
        var entry = new ProtocolEntry();

        var result = await CodingAnalyzedFramePhotoAttachmentWorkflow.ExecuteAsync(
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
    public async Task ExecuteAsync_uses_buffered_frame_when_preferred_frame_is_missing()
    {
        var calls = new List<string>();
        var buffered = new byte[] { 4, 5, 6 };
        var entry = new ProtocolEntry();

        var result = await CodingAnalyzedFramePhotoAttachmentWorkflow.ExecuteAsync(
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

    [Fact]
    public async Task ExecuteAsync_awaits_preferred_frame_without_using_sync_capture()
    {
        var calls = new List<string>();
        var preferred = new byte[] { 7, 8, 9 };
        var entry = new ProtocolEntry();
        var preferredCompletion = new TaskCompletionSource<byte[]?>(
            TaskCreationOptions.RunContinuationsAsynchronously);

        var pending = CodingAnalyzedFramePhotoAttachmentWorkflow.ExecuteAsync(
            entry,
            new CodingAnalyzedFramePhotoAttachmentAsyncActions(
                GetPreferredFrameBytesAsync: () =>
                {
                    calls.Add("preferred-start");
                    return preferredCompletion.Task;
                },
                GetBufferedFrameBytes: () =>
                {
                    calls.Add("buffered");
                    return null;
                },
                AttachAnalyzedFramePhoto: frameBytes =>
                {
                    Assert.Same(preferred, frameBytes);
                    calls.Add("attach:preferred");
                    return "async.png";
                },
                CaptureSnapshot: () =>
                {
                    calls.Add("snapshot");
                    return "snapshot.png";
                }));

        Assert.False(pending.IsCompleted);
        preferredCompletion.SetResult(preferred);

        var result = await pending;

        Assert.Equal(CodingAnalyzedFramePhotoAttachmentOutcome.AttachedAnalyzedFrame, result.Outcome);
        Assert.Equal("async.png", result.PhotoPath);
        Assert.Equal(["preferred-start", "attach:preferred"], calls);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData(" ")]
    public async Task ExecuteAsync_captures_snapshot_and_appends_fallback_when_ai_photo_is_missing(string? attachedPath)
    {
        var calls = new List<string>();
        var entry = new ProtocolEntry();

        var result = await CodingAnalyzedFramePhotoAttachmentWorkflow.ExecuteAsync(
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

    private static CodingAnalyzedFramePhotoAttachmentAsyncActions Actions(
        List<string> calls,
        Func<byte[]?>? getPreferredFrameBytes = null,
        Func<byte[]?>? getBufferedFrameBytes = null,
        Func<byte[]?, string?>? attachAnalyzedFramePhoto = null,
        Func<string?>? captureSnapshot = null)
        => new(
            GetPreferredFrameBytesAsync: () => Task.FromResult((getPreferredFrameBytes ?? (() =>
            {
                calls.Add("preferred");
                return null;
            }))()),
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
