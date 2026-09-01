using System.Collections.Generic;
using AuswertungPro.Next.Infrastructure;

namespace AuswertungPro.Next.Infrastructure.Tests;

public sealed class HoldingIdNormalizerTests
{
    [Theory]
    [InlineData("23022-21598", "23022-21598")]
    [InlineData(" 07.7695 - 07.7078 ", "07.7695-07.7078")]
    [InlineData("ABC", "ABC")]
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
    [InlineData("1.2024-23022", false)]
    [InlineData("12.2024-23022", false)]
    [InlineData("04.201423022-215987", false)]
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

    [Theory]
    [InlineData(null, false)]
    [InlineData("1.2.2024", true)]
    [InlineData("2024-8-1", true)]
    [InlineData("01.2024", true)]
    [InlineData("12.2024", true)]
    [InlineData("00.2024", false)]
    [InlineData("13.2024", false)]
    [InlineData("23022", false)]
    public void IsDateLikeNode_Erkennt_Datumsfragmente(string? input, bool expected)
        => Assert.Equal(expected, HoldingIdNormalizer.IsDateLikeNode(input));

    [Fact]
    public void EnumerateHoldingLookupKeys_ReturnsBothDirections()
    {
        var keys = new List<string>(HoldingIdNormalizer.EnumerateHoldingLookupKeys("23022-21598"));
        Assert.Equal(["23022-21598", "21598-23022"], keys);
    }

    [Fact]
    public void EnumerateHoldingLookupKeys_Gibt_Gleiche_Richtung_Nur_Einmal_Zurueck()
    {
        var keys = new List<string>(HoldingIdNormalizer.EnumerateHoldingLookupKeys("23022-23022"));
        Assert.Equal(["23022-23022"], keys);
    }
}
