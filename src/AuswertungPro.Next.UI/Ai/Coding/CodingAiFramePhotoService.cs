using AuswertungPro.Next.Application.Ai;
using AuswertungPro.Next.Domain.Protocol;
using AuswertungPro.Next.Infrastructure.Ai;

namespace AuswertungPro.Next.UI.Ai.Coding;

public static class CodingAiFramePhotoService
{
    private static readonly ICodingFramePhotoStore DefaultService = new CodingFramePhotoFileStore();

    internal static ICodingFramePhotoStore CompatibilityService => DefaultService;

    public static string? AttachAnalyzedFramePhoto(
        ProtocolEntry entry,
        byte[]? frameBytes,
        string? videoPath = null,
        string? photoRoot = null)
        => DefaultService.AttachAnalyzedFramePhoto(entry, frameBytes, videoPath, photoRoot);
}
