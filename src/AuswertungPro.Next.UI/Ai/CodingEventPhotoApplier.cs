using AuswertungPro.Next.Application.Ai;
using AuswertungPro.Next.Domain.Models;

namespace AuswertungPro.Next.UI.Ai;

public static class CodingEventPhotoApplier
{
    public static CodingPhotoSlotUpdate Apply(
        CodingEvent codingEvent,
        string photoPath,
        ICodingSessionService? codingSessionService)
    {
        ArgumentNullException.ThrowIfNull(codingEvent);

        var update = CodingPhotoSlotPolicy.Apply(codingEvent.Entry.FotoPaths, photoPath);
        codingSessionService?.UpdateEvent(codingEvent.EventId, codingEvent.Entry, codingEvent.Overlay);
        return update;
    }
}
