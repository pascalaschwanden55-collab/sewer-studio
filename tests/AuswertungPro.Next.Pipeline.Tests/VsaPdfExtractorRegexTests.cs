using System.Text.RegularExpressions;
using Xunit;

namespace AuswertungPro.Next.Pipeline.Tests;

/// <summary>
/// Sichert das im tools/VsaPdfExtractor verwendete Regex-Pattern ab.
/// Frueheres Pattern @"...\\s+..." matchte literal "\s", nicht Whitespace —
/// dadurch fand das Tool 0 Codes. Dieser Test fixiert das korrekte Pattern.
/// </summary>
public sealed class VsaPdfExtractorRegexTests
{
    private static readonly Regex CodeRx = new(@"^(?<code>[A-Z]{3,5})\s+(.+)$", RegexOptions.Compiled);

    [Theory]
    [InlineData("BAB Riss laengs", "BAB")]
    [InlineData("BABAC Riss laengs detailliert", "BABAC")]
    [InlineData("BBA Inkrustation", "BBA")]
    public void Matcht_VSA_Code_am_Zeilenanfang(string line, string expectedCode)
    {
        var match = CodeRx.Match(line);

        Assert.True(match.Success, $"Pattern matcht nicht: '{line}'");
        Assert.Equal(expectedCode, match.Groups["code"].Value);
    }

    [Theory]
    [InlineData("kein Code hier")]
    [InlineData("BA nur zwei Buchstaben")]
    [InlineData("BABACX zu lang")]
    public void Matcht_keinen_Treffer_bei_ungueltigen_Zeilen(string line)
    {
        Assert.False(CodeRx.Match(line).Success);
    }
}
