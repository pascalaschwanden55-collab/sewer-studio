using System.Linq;
using AuswertungPro.Next.Application.Ai.Annotation;
using Xunit;

namespace AuswertungPro.Next.Pipeline.Tests;

/// <summary>
/// Slice 1: Pure-Text-Parser fuer PDF-Volltext -&gt; (Code, Meter, Description).
/// Format-Varianten: Standard (zwei optionale Code-Tokens) + Fretz
/// (Foto-Nr + HH:MM:SS vorgesetzt).
/// </summary>
[Trait("Category", "Unit")]
public sealed class BeobachtungParserTests
{
    [Fact]
    public void Parse_StandardRow_TwoTokens_ReturnsCombinedCode()
    {
        var input = "  12.30   BAB B   Riss laengs";
        var hits = BeobachtungParser.Parse(input);

        Assert.Single(hits);
        Assert.Equal("BAB B", hits[0].Code);
        Assert.Equal(12.3, hits[0].Meter);
        Assert.Equal("Riss laengs", hits[0].Description);
    }

    [Fact]
    public void Parse_StandardRow_OneToken_ReturnsCode()
    {
        var input = "5.00   BCD   Rohranfang sichtbar";
        var hits = BeobachtungParser.Parse(input);

        Assert.Single(hits);
        Assert.Equal("BCD", hits[0].Code);
        Assert.Equal(5.0, hits[0].Meter);
    }

    [Fact]
    public void Parse_FretzFormat_PhotoAndTimestamp_StripsPrefix()
    {
        var input = "  4711   00:01:31   4.60   BCC.Y.B   Bogen rechts";
        var hits = BeobachtungParser.Parse(input);

        Assert.Single(hits);
        Assert.Equal("BCC.Y.B", hits[0].Code);
        Assert.Equal(4.60, hits[0].Meter);
        Assert.Equal("Bogen rechts", hits[0].Description);
    }

    [Fact]
    public void Parse_CommaDecimal_IsNormalizedToDot()
    {
        var input = "00:00:55   8,75   BAB B   Riss";
        var hits = BeobachtungParser.Parse(input);

        Assert.Single(hits);
        Assert.Equal(8.75, hits[0].Meter);
    }

    [Fact]
    public void Parse_MultiLine_ReturnsOnePerRow()
    {
        var input = """
            12.30  BAB B   Riss laengs
            18.50  BAC A   Bruch partiell
            25.00  BBB Z   Wurzelbild
            """;
        var hits = BeobachtungParser.Parse(input);

        Assert.Equal(3, hits.Count);
        Assert.Equal(new[] { 12.30, 18.50, 25.00 }, hits.Select(h => h.Meter));
    }

    [Fact]
    public void Parse_NoiseLines_AreIgnored()
    {
        var input = """
            Seite 1
            Inspektionsprotokoll Haltung 100 - 200
            12.30  BAB B   Riss laengs

            Page 2
            ABCD-EF12-...
            18.50  BAC A   Bruch
            """;
        var hits = BeobachtungParser.Parse(input);

        Assert.Equal(2, hits.Count);
        Assert.Equal("BAB B", hits[0].Code);
        Assert.Equal("BAC A", hits[1].Code);
    }

    [Fact]
    public void Parse_EmptyOrNull_ReturnsEmpty()
    {
        Assert.Empty(BeobachtungParser.Parse(""));
        Assert.Empty(BeobachtungParser.Parse("   \n  \n  "));
        Assert.Empty(BeobachtungParser.Parse(null!));
    }

    [Fact]
    public void Parse_TrailingTimestamp_IsStripped()
    {
        var input = "12.30  BAB B   Riss   00:01:35";
        var hits = BeobachtungParser.Parse(input);

        Assert.Single(hits);
        Assert.Equal("Riss", hits[0].Description);
    }

    [Fact]
    public void Parse_ImplausibleMeter_IsRejected()
    {
        // 99999.50 ist unrealistisch; Sanity-Check soll werfen-frei skippen.
        var input = "99999.50  BAB B   Bogus";
        var hits = BeobachtungParser.Parse(input);
        Assert.Empty(hits);
    }
}
