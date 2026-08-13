using System;
using AuswertungPro.Next.Application.Protocol;
using Xunit;

namespace AuswertungPro.Next.Pipeline.Tests;

public sealed class ProtocolTimeParserTests
{
    [Theory]
    [InlineData("01:23:45", 1, 23, 45)]
    [InlineData("23:45", 0, 23, 45)]
    [InlineData("0:05:09", 0, 5, 9)]
    public void Liest_die_bekannten_Formate(string roh, int h, int m, int s)
        => Assert.Equal(new TimeSpan(h, m, s), ProtocolTimeParser.ParseMpegTime(roh));

    /// <summary>
    /// So liefern die VSA-KEK-XTF den Videozaehlerstand: vier Teile mit
    /// Einzelbildern am Schluss. Real gesehen in
    /// Altdorf_Feldliweg_41649_0626.xtf, dort in allen 263 Befunden.
    /// </summary>
    [Theory]
    [InlineData("00:00:15:00", 0, 0, 15)]
    [InlineData("00:01:38:00", 0, 1, 38)]
    [InlineData("01:02:03:24", 1, 2, 3)]
    public void Liest_den_vierteiligen_Zaehlerstand_und_verwirft_die_Einzelbilder(
        string roh, int h, int m, int s)
        => Assert.Equal(new TimeSpan(h, m, s), ProtocolTimeParser.ParseMpegTime(roh));

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("kein Zeitwert")]
    [InlineData("00:00:15:xx")]        // vierter Teil keine Zahl
    [InlineData("00:00:15:00:00")]     // fuenf Teile
    public void Unklares_bleibt_null_statt_geraten(string roh)
        => Assert.Null(ProtocolTimeParser.ParseMpegTime(roh));

    /// <summary>
    /// .NET liest "00:00:15:00" von sich aus als d:hh:mm:ss und macht daraus
    /// 15 MINUTEN. Genau das darf hier nie wieder passieren.
    /// </summary>
    [Fact]
    public void Der_Zaehlerstand_wird_nicht_als_Tage_gelesen()
    {
        var wert = ProtocolTimeParser.ParseMpegTime("00:00:15:00");
        Assert.Equal(TimeSpan.FromSeconds(15), wert);
        Assert.NotEqual(TimeSpan.FromMinutes(15), wert);
    }

    [Fact]
    public void Null_bleibt_null()
        => Assert.Null(ProtocolTimeParser.ParseMpegTime(null));
}
