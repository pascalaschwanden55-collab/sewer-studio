using System.Collections.Generic;
using AuswertungPro.Next.Infrastructure;

namespace AuswertungPro.Next.Infrastructure.Tests;

public sealed class HoldingIdNormalizerTests
{
    [Theory]
    [InlineData("23022-21598", "23022-21598")]
    [InlineData(" 07.7695 - 07.7078 ", "07.7695-07.7078")]
    [InlineData(null, "UNKNOWN")]
    [InlineData("", "UNKNOWN")]
    public void NormalizeHaltungId_ProducesCanonicalForm(string? input, string expected)
        => Assert.Equal(expected, HoldingIdNormalizer.NormalizeHaltungId(input));

    [Theory]
    [InlineData("23022-21598", true)]
    [InlineData("07.7695-07.7078", true)]
    [InlineData("UNKNOWN", false)]
    [InlineData(null, false)]
    [InlineData("", false)]
    [InlineData("1234", false)]
    public void IsValidHaltungId_ValidatesCorrectly(string? input, bool expected)
        => Assert.Equal(expected, HoldingIdNormalizer.IsValidHaltungId(input));

    [Theory]
    [InlineData("23022-21598", "21598-23022")]
    [InlineData("A-B", "B-A")]
    [InlineData(null, "")]
    [InlineData("single", "")]
    public void ReverseHoldingId_SwitchesSides(string? input, string expected)
        => Assert.Equal(expected, HoldingIdNormalizer.ReverseHoldingId(input));

    [Theory]
    [InlineData("07.7695-07.7078", "7695-7078")]
    [InlineData("7695-7078", "7695-7078")]
    public void StripNodePrefixes_RemovesNumericDotPrefix(string input, string expected)
        => Assert.Equal(expected, HoldingIdNormalizer.StripNodePrefixes(input));

    [Fact]
    public void EnumerateHoldingLookupKeys_ReturnsBothDirections()
    {
        var keys = new List<string>(HoldingIdNormalizer.EnumerateHoldingLookupKeys("23022-21598"));
        Assert.Contains("23022-21598", keys);
        Assert.Contains("21598-23022", keys);
    }
}
