using AuswertungPro.Next.Application.Ai;

namespace AuswertungPro.Next.Pipeline.Tests;

public sealed class CodingDedupPolicyTests
{
    [Theory]
    [InlineData("BCD")]
    [InlineData("BCDA")]
    [InlineData("BCE")]
    [InlineData("BDC")]
    [InlineData("bdcxx")]
    public void IsOneTimeCode_RecognizesStartEndAndAbortCodes(string code)
    {
        Assert.True(CodingDedupPolicy.IsOneTimeCode(code));
    }

    [Theory]
    [InlineData("BCAEA", "BCA")]
    [InlineData("bcaea", "BCAAA")]
    [InlineData("BCD", "BCDA")]
    public void CodesMatch_UsesExactOrMainCode(string existingCode, string newCode)
    {
        Assert.True(CodingDedupPolicy.CodesMatch(existingCode, newCode));
    }

    [Fact]
    public void CodesMatch_RejectsDifferentMainCodes()
    {
        Assert.False(CodingDedupPolicy.CodesMatch("BCA", "BCC"));
    }
}
