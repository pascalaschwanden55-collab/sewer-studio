using AuswertungPro.Next.Application.Ai;
using AuswertungPro.Next.Domain.Models;
using AuswertungPro.Next.Domain.Protocol;

namespace AuswertungPro.Next.Pipeline.Tests;

public sealed class CodingOneTimeCodeDuplicatePolicyTests
{
    [Fact]
    public void AlreadyExists_returns_false_for_regular_codes()
    {
        Assert.False(CodingOneTimeCodeDuplicatePolicy.AlreadyExists(
            "BAB",
            sessionEvents: [Event("BAB")],
            viewEvents: null));
    }

    [Fact]
    public void AlreadyExists_finds_one_time_code_in_session_events()
    {
        Assert.True(CodingOneTimeCodeDuplicatePolicy.AlreadyExists(
            "BCD",
            sessionEvents: [Event("BCD")],
            viewEvents: null));
    }

    [Fact]
    public void AlreadyExists_finds_one_time_code_in_view_events_with_main_code_match()
    {
        Assert.True(CodingOneTimeCodeDuplicatePolicy.AlreadyExists(
            "BCE",
            sessionEvents: null,
            viewEvents: [Event("BCE1")]));
    }

    private static CodingEvent Event(string code)
        => new()
        {
            Entry = new ProtocolEntry { Code = code }
        };
}
