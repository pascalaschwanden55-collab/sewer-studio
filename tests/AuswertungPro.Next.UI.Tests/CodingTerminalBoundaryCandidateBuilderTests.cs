using AuswertungPro.Next.Domain.Models;
using AuswertungPro.Next.Domain.Protocol;
using AuswertungPro.Next.UI.Ai;
using AuswertungPro.Next.UI.Ai.Coding;

namespace AuswertungPro.Next.UI.Tests;

public sealed class CodingTerminalBoundaryCandidateBuilderTests
{
    [Fact]
    public void ToCandidate_prefers_entry_meter_and_time_over_capture_fallbacks()
    {
        var ev = Event("BCE");
        ev.Entry.MeterStart = 5.0;
        ev.Entry.Zeit = TimeSpan.FromSeconds(11);
        ev.MeterAtCapture = 7.0;
        ev.VideoTimestamp = TimeSpan.FromSeconds(99);

        var candidate = CodingTerminalBoundaryCandidateBuilder.ToCandidate(ev);

        Assert.Equal("BCE", candidate.Code);
        Assert.Equal(5.0, candidate.Meter);
        Assert.Equal(TimeSpan.FromSeconds(11), candidate.VideoTime);
    }

    [Fact]
    public void ToCandidate_uses_positive_capture_fallbacks_when_entry_values_are_missing()
    {
        var ev = Event("BDC");
        ev.MeterAtCapture = 7.5;
        ev.VideoTimestamp = TimeSpan.FromSeconds(42);

        var candidate = CodingTerminalBoundaryCandidateBuilder.ToCandidate(ev);

        Assert.Equal(7.5, candidate.Meter);
        Assert.Equal(TimeSpan.FromSeconds(42), candidate.VideoTime);
    }

    [Fact]
    public void ToCandidate_ignores_zero_capture_fallbacks()
    {
        var ev = Event("BCE");
        ev.MeterAtCapture = 0;
        ev.VideoTimestamp = TimeSpan.Zero;

        var candidate = CodingTerminalBoundaryCandidateBuilder.ToCandidate(ev);

        Assert.Null(candidate.Meter);
        Assert.Null(candidate.VideoTime);
    }

    [Fact]
    public void Enumerate_keeps_session_ui_import_order_and_accepts_null_sources()
    {
        var session = Event("BCE");
        var import = Event("BDC");

        var codes = CodingTerminalBoundaryCandidateBuilder
            .Enumerate([session], null, [import])
            .Select(candidate => candidate.Code)
            .ToArray();

        Assert.Equal(new[] { "BCE", "BDC" }, codes);
    }

    private static CodingEvent Event(string code)
        => new()
        {
            Entry = new ProtocolEntry
            {
                Code = code,
                Source = ProtocolEntrySource.Manual
            }
        };
}
