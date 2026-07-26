using AuswertungPro.Next.Domain.Models;
using AuswertungPro.Next.Domain.Protocol;
using AuswertungPro.Next.UI.Ai;
using AuswertungPro.Next.UI.Ai.Coding;

namespace AuswertungPro.Next.UI.Tests;

public sealed class CodingTimelineMarkerAccessorsTests
{
    [Fact]
    public void Accessors_read_coding_event_timeline_values()
    {
        var ev = new CodingEvent
        {
            Entry = new ProtocolEntry { Code = "BAB" },
            MeterAtCapture = 12.34,
            AiContext = new CodingEventAiContext { Confidence = 0.87 }
        };

        Assert.Equal(12.34, CodingTimelineMarkerAccessors.Meter(ev));
        Assert.Equal("BAB", CodingTimelineMarkerAccessors.Code(ev));
        Assert.Equal(0.87, CodingTimelineMarkerAccessors.Confidence(ev));
    }

    [Fact]
    public void Accessors_keep_existing_fallbacks_for_non_coding_events()
    {
        var marker = new object();

        Assert.Equal(0, CodingTimelineMarkerAccessors.Meter(marker));
        Assert.Equal("?", CodingTimelineMarkerAccessors.Code(marker));
        Assert.Equal(-1, CodingTimelineMarkerAccessors.Confidence(marker));
        Assert.False(CodingTimelineMarkerAccessors.IsRejected(marker));
    }

    [Fact]
    public void IsRejected_uses_coding_event_defect_status()
    {
        var rejected = new CodingEvent
        {
            AiContext = new CodingEventAiContext { Decision = CodingUserDecision.Rejected }
        };
        var pending = new CodingEvent();

        Assert.True(CodingTimelineMarkerAccessors.IsRejected(rejected));
        Assert.False(CodingTimelineMarkerAccessors.IsRejected(pending));
    }
}
