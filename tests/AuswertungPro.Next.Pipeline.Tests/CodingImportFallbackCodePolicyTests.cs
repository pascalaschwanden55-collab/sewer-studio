using AuswertungPro.Next.Application.Ai;

namespace AuswertungPro.Next.Pipeline.Tests;

public sealed class CodingImportFallbackCodePolicyTests
{
    [Theory]
    [InlineData("BAAA")]
    [InlineData("BAFAA")]
    [InlineData("BAJB")]
    [InlineData("BCD")]
    [InlineData("BCE")]
    [InlineData("BCA")]
    [InlineData("BCC")]
    [InlineData("BBC")]
    [InlineData("BDDC")]
    [InlineData("BBA")]
    [InlineData("BBB")]
    [InlineData("BBD")]
    public void Allows_supported_fallback_families(string code)
    {
        Assert.True(CodingImportFallbackCodePolicy.IsAllowed(code));
    }

    [Theory]
    [InlineData("")]
    [InlineData(" ")]
    [InlineData("BDB")]
    [InlineData("BDC")]
    [InlineData("BAG")]
    [InlineData("BAK")]
    [InlineData("BBH")]
    public void Rejects_codes_outside_supported_fallback_families(string code)
    {
        Assert.False(CodingImportFallbackCodePolicy.IsAllowed(code));
    }

    [Fact]
    public void Allows_bend_only_very_near_current_meter()
    {
        Assert.True(CodingImportFallbackCodePolicy.IsWithinMeterWindow("BCC", 0.05));
        Assert.True(CodingImportFallbackCodePolicy.IsWithinMeterWindow("BCC", 0.25));
        Assert.False(CodingImportFallbackCodePolicy.IsWithinMeterWindow("BCC", 0.35));
    }

    [Fact]
    public void Keeps_existing_window_for_normal_damage_codes()
    {
        Assert.True(CodingImportFallbackCodePolicy.IsWithinMeterWindow("BAB", 1.5));
        Assert.False(CodingImportFallbackCodePolicy.IsWithinMeterWindow("BAB", 2.1));
    }
}
