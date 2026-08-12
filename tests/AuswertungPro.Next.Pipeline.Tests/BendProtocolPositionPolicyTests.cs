using AuswertungPro.Next.Application.UseCases.BendSuggestions;
using Xunit;

namespace AuswertungPro.Next.Pipeline.Tests;

public sealed class BendProtocolPositionPolicyTests
{
    [Theory]
    [InlineData("BCCAA")]
    [InlineData("BCCAB")]
    [InlineData("BCCAY")]
    [InlineData("BCCBA")]
    [InlineData("BCCBB")]
    [InlineData("BCCBY")]
    [InlineData("BCCYA")]
    [InlineData("BCCYB")]
    public void Alle_acht_BCC_Untercodes_werden_gemeinsam_gemessen(string code)
    {
        Assert.True(BendProtocolPositionPolicy.IsSupportedCode(code));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("BCC")]
    [InlineData("BCCZ")]
    [InlineData("BCAAA")]
    [InlineData("BCC.YB")]
    public void Unvollstaendige_oder_fremde_Codes_werden_nicht_geraten(string? code)
    {
        Assert.False(BendProtocolPositionPolicy.IsSupportedCode(code));
    }

    [Fact]
    public void BCC_Punkt_YB_schliesst_die_gesamte_Haltung_aus()
    {
        Assert.True(BendProtocolPositionPolicy.ExcludesHolding(" BCC.YB "));
        Assert.False(BendProtocolPositionPolicy.ExcludesHolding("BCCYB"));
    }
}
