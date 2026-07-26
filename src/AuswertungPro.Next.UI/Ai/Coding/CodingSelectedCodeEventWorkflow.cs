using AuswertungPro.Next.Application.Ai;
using AuswertungPro.Next.Domain.Models;
using AuswertungPro.Next.Domain.Protocol;

namespace AuswertungPro.Next.UI.Ai.Coding;

public static class CodingSelectedCodeEventWorkflow
{
    public static CodingEvent? Create(
        string? code,
        string? description,
        double meter,
        TimeSpan videoTime,
        OverlayGeometry? overlay,
        ICodingSessionService? codingSessionService,
        Func<ProtocolEntry, string?> captureSnapshot)
    {
        if (string.IsNullOrWhiteSpace(code) || codingSessionService is null)
            return null;

        ArgumentNullException.ThrowIfNull(captureSnapshot);

        var draft = CodingManualEventFactory.CreateUnconfirmed(
            code,
            description,
            meter,
            videoTime,
            overlay);

        var photoPath = captureSnapshot(draft.Entry);
        CodingProtocolEntryPhotoPathAppender.AddIfPresent(draft.Entry, photoPath);

        return CodingManualEventAppender.Apply(draft, overlay, codingSessionService);
    }
}
