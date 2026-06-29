using System;
using System.Collections.Generic;
using AuswertungPro.Next.Infrastructure;

namespace AuswertungPro.Next.Infrastructure.Tests;

public sealed class HoldingTextNormalizerTests
{
    [Theory]
    [InlineData("foo bar", "foo bar")]   // NBSP → Leerzeichen
    [InlineData("foo–bar", "foo-bar")]          // En-Dash → Minus
    [InlineData("foo—bar", "foo-bar")]          // Em-Dash → Minus
    [InlineData("foo\tbar", "foo bar")]         // Tab → Leerzeichen
    [InlineData("", "")]
    public void NormalizeText_ReplacesSpecialChars(string input, string expected)
        => Assert.Equal(expected, HoldingTextNormalizer.NormalizeText(input));

    [Theory]
    [InlineData("02.05.2023", 2023, 5, 2)]
    [InlineData("2023-05-02", 2023, 5, 2)]
    [InlineData("02/05/2023", 2023, 5, 2)]
    public void TryParseDateString_ParsesValidDates(string input, int year, int month, int day)
    {
        Assert.True(HoldingTextNormalizer.TryParseDateString(input, out var date));
        Assert.Equal(new DateTime(year, month, day), date);
    }

    [Fact]
    public void TryParseDateString_ReturnsFalse_ForInvalidInput()
        => Assert.False(HoldingTextNormalizer.TryParseDateString("not-a-date", out _));

    [Theory]
    [InlineData("Hello World", "helloworld")]
    [InlineData("Abc 123", "abc123")]
    [InlineData("", "")]
    public void NormalizeKey_ReturnsLowercaseAlphanumericOnly(string input, string expected)
        => Assert.Equal(expected, HoldingTextNormalizer.NormalizeKey(input));

    [Theory]
    [InlineData(new int[] { 3 }, "3")]
    [InlineData(new int[] { 3, 7 }, "3-7")]
    [InlineData(new int[] { 1, 2, 3 }, "1-3")]
    [InlineData(new int[] { }, "")]
    public void BuildPageRange_FormatsRange(int[] pages, string expected)
        => Assert.Equal(expected, HoldingTextNormalizer.BuildPageRange(pages));

    [Theory]
    [InlineData("Inhaltsverzeichnis", true)]
    [InlineData("Normaler Text", false)]
    public void IsContentsPage_DetectsInhaltsverzeichnis(string text, bool expected)
        => Assert.Equal(expected, HoldingTextNormalizer.IsContentsPage(text));

    [Theory]
    [InlineData("Fehler", null, "Fehler")]
    [InlineData(null, "B", "B")]
    [InlineData("A", "B", "A; B")]
    [InlineData(null, null, null)]
    public void MergeMessage_CombinesMessages(string? a, string? b, string? expected)
        => Assert.Equal(expected, HoldingTextNormalizer.MergeMessage(a, b));

    [Theory]
    [InlineData("\"video.mpg\"", "video.mpg")]
    [InlineData("video.mp4,", "video.mp4")]
    [InlineData("C:\\path\\to\\video.avi", "video.avi")]
    [InlineData(null, null)]
    public void NormalizeVideoFileName_StripsQuotesAndPath(string? input, string? expected)
        => Assert.Equal(expected, HoldingTextNormalizer.NormalizeVideoFileName(input));
}
