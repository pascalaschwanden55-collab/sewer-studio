using AuswertungPro.Next.Domain.Models;
using AuswertungPro.Next.Domain.Protocol;
using AuswertungPro.Next.UI.Ai;
using AuswertungPro.Next.UI.Ai.Coding;

namespace AuswertungPro.Next.UI.Tests;

public sealed class CodingBoundaryPresencePolicyTests
{
    [Fact]
    public void CountExisting_counts_matching_codes_in_view_and_session_events()
    {
        var presence = CodingBoundaryPresencePolicy.CountExisting(
            viewEvents: [Event("BCD"), Event("BAB"), Event("bcd")],
            sessionEvents: [Event("BCD"), Event("BCE")],
            code: "BCD");

        Assert.Equal(2, presence.ViewCount);
        Assert.Equal(1, presence.SessionCount);
        Assert.True(presence.Exists);
    }

    [Fact]
    public void CountExisting_treats_null_lists_as_empty()
    {
        var presence = CodingBoundaryPresencePolicy.CountExisting(
            viewEvents: null,
            sessionEvents: null,
            code: "BCE");

        Assert.Equal(0, presence.ViewCount);
        Assert.Equal(0, presence.SessionCount);
        Assert.False(presence.Exists);
    }

    [Fact]
    public void ExistsInView_returns_true_when_view_contains_code()
    {
        Assert.True(CodingBoundaryPresencePolicy.ExistsInView(
            [Event("BCE")],
            code: "bce"));
    }

    private static CodingEvent Event(string code)
        => new()
        {
            Entry = new ProtocolEntry { Code = code }
        };
}
