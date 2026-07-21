using AuswertungPro.Next.Application.Protocol;

namespace AuswertungPro.Next.Pipeline.Tests;

public sealed class ProtocolTimeParserTests
{
    [Fact]
    public void ParseMpegTime_verarbeitet_bisherige_Kurz_Lang_und_Millisekundenformate()
    {
        Assert.Equal(new TimeSpan(0, 2, 3), ProtocolTimeParser.ParseMpegTime(" 02:03 "));
        Assert.Equal(new TimeSpan(0, 2, 3), ProtocolTimeParser.ParseMpegTime("2:03"));
        Assert.Equal(new TimeSpan(1, 2, 3), ProtocolTimeParser.ParseMpegTime("1:02:03"));
        Assert.Equal(
            new TimeSpan(0, 1, 2, 3, 456),
            ProtocolTimeParser.ParseMpegTime("01:02:03.456"));
        Assert.Equal(
            new TimeSpan(0, 0, 2, 3, 456),
            ProtocolTimeParser.ParseMpegTime("02:03.456"));
    }

    [Fact]
    public void ParseMpegTime_behaelt_TimeSpan_Fallback_und_ungueltige_Werte()
    {
        Assert.Equal(
            new TimeSpan(days: 1, hours: 2, minutes: 3, seconds: 4),
            ProtocolTimeParser.ParseMpegTime("1.02:03:04"));
        Assert.Null(ProtocolTimeParser.ParseMpegTime(null));
        Assert.Null(ProtocolTimeParser.ParseMpegTime("   "));
        Assert.Null(ProtocolTimeParser.ParseMpegTime("99:99:99"));
        Assert.Null(ProtocolTimeParser.ParseMpegTime("ungueltig"));
    }

    [Fact]
    public void ParseMpegTime_behaelt_auch_ueberraschende_TimeSpan_Fallbacks()
    {
        Assert.Equal(TimeSpan.FromDays(24), ProtocolTimeParser.ParseMpegTime("24:00:00"));
        Assert.Equal(new TimeSpan(1, 2, 0), ProtocolTimeParser.ParseMpegTime("1:2"));
    }
}
