using AuswertungPro.Next.Domain.Protocol;

namespace AuswertungPro.Next.UI.Ai;

public enum CodingAnalyzedFramePhotoAttachmentOutcome
{
    AttachedAnalyzedFrame,
    FallbackSnapshot
}

public sealed record CodingAnalyzedFramePhotoAttachmentActions(
    Func<byte[]?> GetPreferredFrameBytes,
    Func<byte[]?> GetBufferedFrameBytes,
    Func<byte[]?, string?> AttachAnalyzedFramePhoto,
    Func<string?> CaptureSnapshot);

public sealed record CodingAnalyzedFramePhotoAttachmentAsyncActions(
    Func<Task<byte[]?>> GetPreferredFrameBytesAsync,
    Func<byte[]?> GetBufferedFrameBytes,
    Func<byte[]?, string?> AttachAnalyzedFramePhoto,
    Func<string?> CaptureSnapshot);

public sealed record CodingAnalyzedFramePhotoAttachmentResult(
    CodingAnalyzedFramePhotoAttachmentOutcome Outcome,
    string? PhotoPath);

public static class CodingAnalyzedFramePhotoAttachmentWorkflow
{
    public static CodingAnalyzedFramePhotoAttachmentResult Execute(
        ProtocolEntry entry,
        CodingAnalyzedFramePhotoAttachmentActions actions)
    {
        ArgumentNullException.ThrowIfNull(entry);
        ArgumentNullException.ThrowIfNull(actions);

        var frameBytes = actions.GetPreferredFrameBytes() ?? actions.GetBufferedFrameBytes();
        var photoPath = actions.AttachAnalyzedFramePhoto(frameBytes);
        if (!string.IsNullOrWhiteSpace(photoPath))
        {
            return new CodingAnalyzedFramePhotoAttachmentResult(
                CodingAnalyzedFramePhotoAttachmentOutcome.AttachedAnalyzedFrame,
                photoPath);
        }

        var fallbackPath = actions.CaptureSnapshot();
        CodingProtocolEntryPhotoPathAppender.AddDistinctNonBlank(entry, fallbackPath);

        return new CodingAnalyzedFramePhotoAttachmentResult(
            CodingAnalyzedFramePhotoAttachmentOutcome.FallbackSnapshot,
            fallbackPath);
    }

    public static async Task<CodingAnalyzedFramePhotoAttachmentResult> ExecuteAsync(
        ProtocolEntry entry,
        CodingAnalyzedFramePhotoAttachmentAsyncActions actions)
    {
        ArgumentNullException.ThrowIfNull(entry);
        ArgumentNullException.ThrowIfNull(actions);

        var frameBytes = await actions.GetPreferredFrameBytesAsync() ?? actions.GetBufferedFrameBytes();
        var photoPath = actions.AttachAnalyzedFramePhoto(frameBytes);
        if (!string.IsNullOrWhiteSpace(photoPath))
        {
            return new CodingAnalyzedFramePhotoAttachmentResult(
                CodingAnalyzedFramePhotoAttachmentOutcome.AttachedAnalyzedFrame,
                photoPath);
        }

        var fallbackPath = actions.CaptureSnapshot();
        CodingProtocolEntryPhotoPathAppender.AddDistinctNonBlank(entry, fallbackPath);

        return new CodingAnalyzedFramePhotoAttachmentResult(
            CodingAnalyzedFramePhotoAttachmentOutcome.FallbackSnapshot,
            fallbackPath);
    }
}
