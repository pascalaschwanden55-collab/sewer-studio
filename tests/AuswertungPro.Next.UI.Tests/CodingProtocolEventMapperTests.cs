using AuswertungPro.Next.Domain.Protocol;
using AuswertungPro.Next.UI.Ai;

namespace AuswertungPro.Next.UI.Tests;

public sealed class CodingProtocolEventMapperTests
{
    [Fact]
    public void BuildExistingEvents_filters_invalid_entries_and_sorts_by_meter()
    {
        var late = Entry("BAJ", meter: 5.0, TimeSpan.FromSeconds(5));
        var early = Entry("BAB", meter: 1.25, TimeSpan.FromSeconds(1));
        var deleted = Entry("DEL", meter: 0.5, TimeSpan.Zero);
        deleted.IsDeleted = true;
        var blank = Entry(" ", meter: 0.1, TimeSpan.Zero);
        var doc = new ProtocolDocument
        {
            Current = new ProtocolRevision
            {
                Entries = { late, deleted, blank, early }
            }
        };

        var events = CodingProtocolEventMapper.BuildExistingEvents(doc);

        Assert.Equal(2, events.Count);
        Assert.Same(early, events[0].Entry);
        Assert.Equal(1.25, events[0].MeterAtCapture);
        Assert.Equal(TimeSpan.FromSeconds(1), events[0].VideoTimestamp);
        Assert.Same(late, events[1].Entry);
        Assert.Equal(5.0, events[1].MeterAtCapture);
        Assert.Equal(TimeSpan.FromSeconds(5), events[1].VideoTimestamp);
    }

    [Fact]
    public void BuildExistingEvents_uses_zero_defaults_for_missing_meter_and_time()
    {
        var doc = new ProtocolDocument
        {
            Current = new ProtocolRevision
            {
                Entries = { new ProtocolEntry { Code = "BAB" } }
            }
        };

        var ev = Assert.Single(CodingProtocolEventMapper.BuildExistingEvents(doc));

        Assert.Equal(0, ev.MeterAtCapture);
        Assert.Equal(TimeSpan.Zero, ev.VideoTimestamp);
    }

    [Fact]
    public void BuildExistingEvents_returns_empty_list_without_protocol()
    {
        var events = CodingProtocolEventMapper.BuildExistingEvents(null);

        Assert.Empty(events);
    }

    [Fact]
    public void BuildMissingImportEvents_skips_existing_entries_and_uses_meter_end_fallback()
    {
        var existingEntry = Entry("BAB", meter: 1.0, TimeSpan.FromSeconds(1));
        var missingEntry = new ProtocolEntry
        {
            Code = "BAJ",
            MeterEnd = 2.75,
            Zeit = TimeSpan.FromSeconds(3)
        };
        var doc = new ProtocolDocument
        {
            Current = new ProtocolRevision
            {
                Entries = { existingEntry, missingEntry, new ProtocolEntry { Code = "" } }
            }
        };
        var existingEvents = new[]
        {
            new AuswertungPro.Next.Domain.Models.CodingEvent { Entry = existingEntry }
        };

        var events = CodingProtocolEventMapper.BuildMissingImportEvents(doc, existingEvents);

        var ev = Assert.Single(events);
        Assert.Same(missingEntry, ev.Entry);
        Assert.Equal(2.75, ev.MeterAtCapture);
        Assert.Equal(TimeSpan.FromSeconds(3), ev.VideoTimestamp);
    }

    private static ProtocolEntry Entry(string code, double meter, TimeSpan time)
        => new()
        {
            Code = code,
            MeterStart = meter,
            Zeit = time
        };
}
