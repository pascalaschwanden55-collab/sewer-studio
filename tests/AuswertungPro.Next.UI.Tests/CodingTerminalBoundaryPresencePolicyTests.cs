using AuswertungPro.Next.Domain.Models;
using AuswertungPro.Next.Domain.Protocol;
using AuswertungPro.Next.UI.Ai;

namespace AuswertungPro.Next.UI.Tests;

public sealed class CodingTerminalBoundaryPresencePolicyTests
{
    [Theory]
    [InlineData("BCE")]
    [InlineData("bce")]
    [InlineData("BCE1")]
    [InlineData("BDC")]
    [InlineData("BDCA")]
    public void HasEndOrAbortCode_accepts_rohrende_and_abort_main_codes(string code)
    {
        Assert.True(CodingTerminalBoundaryPresencePolicy.HasEndOrAbortCode([Event(code)]));
    }

    [Fact]
    public void HasEndOrAbortCode_ignores_non_terminal_codes_and_empty_lists()
    {
        Assert.False(CodingTerminalBoundaryPresencePolicy.HasEndOrAbortCode([Event("BCD"), Event("BCA")]));
        Assert.False(CodingTerminalBoundaryPresencePolicy.HasEndOrAbortCode([]));
        Assert.False(CodingTerminalBoundaryPresencePolicy.HasEndOrAbortCode(null));
    }

    private static CodingEvent Event(string code)
        => new()
        {
            Entry = new ProtocolEntry { Code = code }
        };
}
