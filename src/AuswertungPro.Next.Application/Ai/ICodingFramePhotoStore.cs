using AuswertungPro.Next.Domain.Protocol;

namespace AuswertungPro.Next.Application.Ai;

/// <summary>
/// Speichert den analysierten Videoframe als Foto eines Protokolleintrags.
/// </summary>
public interface ICodingFramePhotoStore
{
    string? AttachAnalyzedFramePhoto(
        ProtocolEntry entry,
        byte[]? frameBytes,
        string? videoPath = null,
        string? photoRoot = null);
}
