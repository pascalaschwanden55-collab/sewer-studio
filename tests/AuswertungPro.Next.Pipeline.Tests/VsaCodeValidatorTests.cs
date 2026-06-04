using AuswertungPro.Next.Domain.VsaCatalog;

namespace AuswertungPro.Next.Pipeline.Tests;

public sealed class VsaCodeValidatorTests
{
    [Theory]
    [InlineData("BAB")]
    [InlineData("BABBB")]
    [InlineData("BBAA")]
    [InlineData("bca.eb")]
    public void IsKnownCode_accepts_known_main_code_and_subcodes(string code)
    {
        Assert.True(VsaCodeValidator.IsKnownCode(code));
    }

    [Theory]
    [InlineData("")]
    [InlineData(" ")]
    [InlineData("BA")]
    [InlineData("BB")]
    [InlineData("ABC")]
    [InlineData("XY")]
    [InlineData("BBZ")]
    [InlineData("BA-")]
    [InlineData("B A B")]
    public void IsKnownCode_rejects_groups_unknown_codes_and_noise(string code)
    {
        Assert.False(VsaCodeValidator.IsKnownCode(code));
    }
}
