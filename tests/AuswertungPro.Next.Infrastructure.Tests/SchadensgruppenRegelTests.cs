using AuswertungPro.Next.Application.Common;
using Xunit;

namespace AuswertungPro.Next.Infrastructure.Tests;

/// <summary>
/// Nova-Fixwelle F1: Die eine Regel, was als Schaden zaehlt. Sie gilt gleichermassen fuer die
/// Uebersichtskarte des Cockpits, den Rohrring und die Liste "Primaere Schaeden".
/// </summary>
public sealed class SchadensgruppenRegelTests
{
    [Theory]
    [InlineData("BABAA", "BAB")]
    [InlineData("bab", "BAB")]
    [InlineData(" BBC.AA ", "BBC")]
    [InlineData("", "")]
    [InlineData(null, "")]
    public void Hauptcode_kuerzt_auf_drei_Zeichen(string? roh, string erwartet)
        => Assert.Equal(erwartet, SchadensgruppenRegel.Hauptcode(roh));

    [Theory]
    [InlineData("BAB")]
    [InlineData("BAC")]
    [InlineData("BBC")]
    public void Bauliche_und_betriebliche_Codes_sind_Schaeden(string code)
        => Assert.True(SchadensgruppenRegel.IstSchadensgruppe(code));

    [Theory]
    [InlineData("BCD")]
    [InlineData("BCE")]
    [InlineData("BCA")]
    [InlineData("BCC")]
    [InlineData("BDA")]
    [InlineData("BA")]
    [InlineData("")]
    public void Bestandsaufnahme_und_Unfug_sind_keine_Schaeden(string code)
        => Assert.False(SchadensgruppenRegel.IstSchadensgruppe(code));

    [Fact]
    public void IstSchaden_normalisiert_selbst()
    {
        Assert.True(SchadensgruppenRegel.IstSchaden("BABAA"));
        Assert.False(SchadensgruppenRegel.IstSchaden("BCCAA"));
    }
}
