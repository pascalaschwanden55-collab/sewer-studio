using AuswertungPro.Next.Application.Reports;

namespace AuswertungPro.Next.Pipeline.Tests;

/// <summary>Charakterisierungs-Tests fuer HoldingNodeParser (IST-Verhalten).</summary>
public sealed class HoldingNodeParserTests
{
    // --- SplitHoldingNodes ---

    [Fact]
    public void SplitHoldingNodes_typisches_label_liefert_start_und_end()
    {
        var (start, end) = HoldingNodeParser.SplitHoldingNodes("865-864");
        Assert.Equal("865", start);
        Assert.Equal("864", end);
    }

    [Fact]
    public void SplitHoldingNodes_nur_start_ohne_strich_liefert_start_und_null_end()
    {
        var (start, end) = HoldingNodeParser.SplitHoldingNodes("865");
        Assert.Equal("865", start);
        Assert.Null(end);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void SplitHoldingNodes_leeres_label_liefert_beide_null(string? label)
    {
        var (start, end) = HoldingNodeParser.SplitHoldingNodes(label);
        Assert.Null(start);
        Assert.Null(end);
    }

    [Fact]
    public void SplitHoldingNodes_drei_teile_liefert_nur_ersten_und_zweiten()
    {
        var (start, end) = HoldingNodeParser.SplitHoldingNodes("A-B-C");
        Assert.Equal("A", start);
        Assert.Equal("B", end);
    }

    [Fact]
    public void SplitHoldingNodes_leerzeichen_um_strich_werden_getrimmt()
    {
        var (start, end) = HoldingNodeParser.SplitHoldingNodes("865 - 864");
        Assert.Equal("865", start);
        Assert.Equal("864", end);
    }

    // --- ParseFlowDirection ---

    [Theory]
    [InlineData("in Fliessrichtung",         true)]
    [InlineData("In Fliessrichtung",         true)]
    [InlineData("IN FLIESSRICHTUNG",         true)]
    public void ParseFlowDirection_in_text_liefert_true(string text, bool expected)
        => Assert.Equal(expected, HoldingNodeParser.ParseFlowDirection(text));

    [Theory]
    [InlineData("gegen Fliessrichtung",       false)]
    [InlineData("Gegen Fliessrichtung",       false)]
    [InlineData("GEGEN FLIESSRICHTUNG",       false)]
    public void ParseFlowDirection_gegen_text_liefert_false(string text, bool expected)
        => Assert.Equal(expected, HoldingNodeParser.ParseFlowDirection(text));

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("unbekannt")]
    public void ParseFlowDirection_kein_bekanntes_schluesselwort_liefert_null(string? text)
        => Assert.Null(HoldingNodeParser.ParseFlowDirection(text));
}
