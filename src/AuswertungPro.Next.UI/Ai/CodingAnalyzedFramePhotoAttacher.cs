using AuswertungPro.Next.Domain.Protocol;

namespace AuswertungPro.Next.UI.Ai;

public static class CodingAnalyzedFramePhotoAttacher
{
    public static string? Attach(
        ProtocolEntry entry,
        byte[]? frameBytes,
        string? videoPath)
        => Attach(
            entry,
            frameBytes,
            videoPath,
            (targetEntry, targetFrameBytes, targetVideoPath) =>
                CodingAiFramePhotoService.AttachAnalyzedFramePhoto(
                    targetEntry,
                    targetFrameBytes,
                    targetVideoPath));

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
