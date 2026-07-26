using AuswertungPro.Next.Domain.Models;
using AuswertungPro.Next.Domain.Protocol;
using AuswertungPro.Next.UI.Ai;
using AuswertungPro.Next.UI.Ai.Coding;

namespace AuswertungPro.Next.UI.Tests;

public sealed class CodingEventPhotoTimestampScopeTests
{
    [Fact]
    public void Apply_sets_entry_time_and_event_timestamp_to_photo_time()
    {
        var ev = new CodingEvent
        {
            Entry = new ProtocolEntry { Zeit = TimeSpan.FromSeconds(4) },
            VideoTimestamp = TimeSpan.FromSeconds(5)
        };

        CodingEventPhotoTimestampScope.Apply(ev, TimeSpan.FromSeconds(12));

        Assert.Equal(TimeSpan.FromSeconds(12), ev.Entry.Zeit);
        Assert.Equal(TimeSpan.FromSeconds(12), ev.VideoTimestamp);
    }

    [Fact]
    public void RestoreOriginalTime_restores_entry_time_and_event_timestamp_after_failed_capture()
    {
        var ev = new CodingEvent
        {
            Entry = new ProtocolEntry { Zeit = TimeSpan.FromSeconds(4) },
            VideoTimestamp = TimeSpan.FromSeconds(5)
        };

        var scope = CodingEventPhotoTimestampScope.Apply(ev, TimeSpan.FromSeconds(12));

        scope.RestoreOriginalTime();

        Assert.Equal(TimeSpan.FromSeconds(4), ev.Entry.Zeit);
        Assert.Equal(TimeSpan.FromSeconds(5), ev.VideoTimestamp);
    }

    [Fact]
    public void Apply_without_photo_time_leaves_event_unchanged()
    {
        var ev = new CodingEvent
        {
            Entry = new ProtocolEntry { Zeit = TimeSpan.FromSeconds(4) },
            VideoTimestamp = TimeSpan.FromSeconds(5)
        };

        CodingEventPhotoTimestampScope.Apply(ev, photoTime: null);

        Assert.Equal(TimeSpan.FromSeconds(4), ev.Entry.Zeit);
        Assert.Equal(TimeSpan.FromSeconds(5), ev.VideoTimestamp);
    }
}
