using AuswertungPro.Next.Domain.Models;

namespace AuswertungPro.Next.Application.Ai;

public interface ICodingDefectPreviewRenderer
{
    string? BuildPreviewImagePath(CodingEvent codingEvent, string? previewRoot = null);
}
