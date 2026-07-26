using AuswertungPro.Next.Domain.Models;

namespace AuswertungPro.Next.UI.Ai.Coding;

public sealed class CodingEventPhotoTimestampScope
{
    private readonly CodingEvent _codingEvent;
    private readonly TimeSpan? _originalEntryTime;
    private readonly TimeSpan _originalVideoTimestamp;

    private CodingEventPhotoTimestampScope(CodingEvent codingEvent)
    {
        _codingEvent = codingEvent;
        _originalEntryTime = codingEvent.Entry.Zeit;
        _originalVideoTimestamp = codingEvent.VideoTimestamp;
    }

    public static CodingEventPhotoTimestampScope Apply(CodingEvent codingEvent, TimeSpan? photoTime)
    {
        ArgumentNullException.ThrowIfNull(codingEvent);

        var scope = new CodingEventPhotoTimestampScope(codingEvent);
        if (photoTime.HasValue)
        {
            codingEvent.Entry.Zeit = photoTime.Value;
            codingEvent.VideoTimestamp = photoTime.Value;
        }

        return scope;
    }

    public void RestoreOriginalTime()
    {
        _codingEvent.Entry.Zeit = _originalEntryTime;
        _codingEvent.VideoTimestamp = _originalVideoTimestamp;
    }
}
