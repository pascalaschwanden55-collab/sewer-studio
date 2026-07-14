using AuswertungPro.Next.Application.Ai;
using AuswertungPro.Next.Domain.Protocol;

namespace AuswertungPro.Next.UI.Ai;

public static class CodingAnalyzedFramePhotoAttacher
{
    public static string? Attach(
        ProtocolEntry entry,
        byte[]? frameBytes,
        string? videoPath)
        => AttachWithStore(
            entry,
            frameBytes,
            videoPath,
            CodingAiFramePhotoService.CompatibilityService);

    public static string? AttachWithStore(
        ProtocolEntry entry,
        byte[]? frameBytes,
        string? videoPath,
        ICodingFramePhotoStore framePhotoStore)
    {
        ArgumentNullException.ThrowIfNull(entry);
        ArgumentNullException.ThrowIfNull(framePhotoStore);

        return framePhotoStore.AttachAnalyzedFramePhoto(entry, frameBytes, videoPath);
    }

    public static string? Attach(
        ProtocolEntry entry,
        byte[]? frameBytes,
        string? videoPath,
        Func<ProtocolEntry, byte[]?, string?, string?> attachFramePhoto)
    {
        ArgumentNullException.ThrowIfNull(entry);
        ArgumentNullException.ThrowIfNull(attachFramePhoto);

        return attachFramePhoto(entry, frameBytes, videoPath);
    }
}
