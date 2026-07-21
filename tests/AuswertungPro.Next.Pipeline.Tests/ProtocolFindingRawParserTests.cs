using AuswertungPro.Next.Application.Protocol;

namespace AuswertungPro.Next.Pipeline.Tests;

public sealed class ProtocolFindingRawParserTests
{
    [Fact]
    public void Meterparser_liefert_ersten_und_zweiten_Treffer_mit_Komma_Punkt_und_Gross_M()
    {
        const string raw = "Start @ 12,34 m, Ende 15.6M";

        Assert.Equal(12.34, ProtocolFindingRawParser.TryParseMeterFromRaw(raw));
        Assert.Equal(15.6, ProtocolFindingRawParser.TryParseSecondMeterFromRaw(raw));
        Assert.Equal(2, ProtocolFindingRawParser.TryParseSecondMeterFromRaw("1m 2m 3m"));
    }

    [Fact]
    public void Meterparser_ignoriert_Millimeter_und_nimmt_fuer_den_zweiten_Wert_den_zweiten_gueltigen_Treffer()
    {
        const string raw = "1m, 2mm, 3m";

        Assert.Equal(1, ProtocolFindingRawParser.TryParseMeterFromRaw(raw));
        Assert.Equal(3, ProtocolFindingRawParser.TryParseSecondMeterFromRaw(raw));
        Assert.Null(ProtocolFindingRawParser.TryParseMeterFromRaw("7mm"));
        Assert.Null(ProtocolFindingRawParser.TryParseSecondMeterFromRaw("nur 7m"));
    }

    [Fact]
    public void Meterparser_behaelt_die_bisherige_unverankerte_Suffixauswertung()
    {
        Assert.Equal(2.3, ProtocolFindingRawParser.TryParseMeterFromRaw("ungueltig 1.2.3m"));
        Assert.Equal(12, ProtocolFindingRawParser.TryParseMeterFromRaw("TextX12m"));
        Assert.Equal(34.56, ProtocolFindingRawParser.TryParseMeterFromRaw("12.34.56m"));
    }

    [Fact]
    public void Meterparser_sucht_nach_einem_ausgeschlossenen_Millimeterwert_weiter()
    {
        Assert.Equal(3, ProtocolFindingRawParser.TryParseMeterFromRaw("12mm 3m"));
        Assert.Null(ProtocolFindingRawParser.TryParseSecondMeterFromRaw("12mm 3m"));
    }

    [Fact]
    public void Zeitparser_liefert_den_ersten_Wortgrenzen_Treffer_ohne_ihn_zu_validieren()
    {
        Assert.Equal("1:02", ProtocolFindingRawParser.TryParseTimeFromRaw("Start 1:02, spaeter 12:34:56"));
        Assert.Equal("12:34:56", ProtocolFindingRawParser.TryParseTimeFromRaw("Zeit 12:34:56"));
        Assert.Equal("2:03", ProtocolFindingRawParser.TryParseTimeFromRaw("x1:02, danach 2:03"));
        Assert.Equal("99:99", ProtocolFindingRawParser.TryParseTimeFromRaw("Zeit 99:99"));
        Assert.Equal("01:02", ProtocolFindingRawParser.TryParseTimeFromRaw("Zeit 01:02.345"));
        Assert.Equal("12:34", ProtocolFindingRawParser.TryParseTimeFromRaw("Zeit 12:34:5"));
        Assert.Null(ProtocolFindingRawParser.TryParseTimeFromRaw("ohne Zeit"));
    }

    [Fact]
    public void Rawparser_behalten_die_bisherige_Null_Exception()
    {
        Assert.Throws<ArgumentNullException>(() => ProtocolFindingRawParser.TryParseMeterFromRaw(null!));
        Assert.Throws<ArgumentNullException>(() => ProtocolFindingRawParser.TryParseSecondMeterFromRaw(null!));
        Assert.Throws<ArgumentNullException>(() => ProtocolFindingRawParser.TryParseTimeFromRaw(null!));
    }
}
