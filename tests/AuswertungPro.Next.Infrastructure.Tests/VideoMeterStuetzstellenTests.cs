using System;
using System.Linq;
using AuswertungPro.Next.Application.Video;
using AuswertungPro.Next.Domain.Protocol;
using Xunit;

namespace AuswertungPro.Next.Infrastructure.Tests;

/// <summary>
/// Sammelt aus dem aktuellen Protokoll die Stuetzstellen fuer die Videoposition:
/// jede codierte Beobachtung mit Videozeit UND Meterwert.
/// </summary>
public sealed class VideoMeterStuetzstellenTests
{
    private static ProtocolDocument Dokument(params ProtocolEntry[] entries)
    {
        var doc = new ProtocolDocument();
        doc.Current.Entries.AddRange(entries);
        return doc;
    }

    [Fact]
    public void Ein_Eintrag_mit_Zeit_und_Meter_wird_zur_Stuetzstelle()
    {
        var stellen = VideoMeterStuetzstellen.AusProtokoll(Dokument(
            new ProtocolEntry { Zeit = TimeSpan.FromSeconds(42), MeterStart = 12.5 }));

        var stelle = Assert.Single(stellen);
        Assert.Equal(TimeSpan.FromSeconds(42), stelle.Zeit);
        Assert.Equal(12.5, stelle.Meter, 3);
    }

    [Fact]
    public void Fehlt_die_Zeit_wird_der_Mpeg_Text_gelesen()
    {
        var stellen = VideoMeterStuetzstellen.AusProtokoll(Dokument(
            new ProtocolEntry { Mpeg = "00:01:23", MeterStart = 8.0 }));

        var stelle = Assert.Single(stellen);
        Assert.Equal(TimeSpan.FromSeconds(83), stelle.Zeit);
    }

    [Fact]
    public void Ohne_Meter_oder_ohne_Zeit_entsteht_keine_Stuetzstelle()
    {
        var stellen = VideoMeterStuetzstellen.AusProtokoll(Dokument(
            new ProtocolEntry { Zeit = TimeSpan.FromSeconds(10) },              // kein Meter
            new ProtocolEntry { MeterStart = 5.0 },                             // keine Zeit
            new ProtocolEntry { Mpeg = "kein Zeitwert", MeterStart = 5.0 }));   // unlesbar

        Assert.Empty(stellen);
    }

    [Fact]
    public void Geloeschte_Eintraege_zaehlen_nicht_mit()
    {
        var stellen = VideoMeterStuetzstellen.AusProtokoll(Dokument(
            new ProtocolEntry { Zeit = TimeSpan.FromSeconds(10), MeterStart = 3.0, IsDeleted = true },
            new ProtocolEntry { Zeit = TimeSpan.FromSeconds(20), MeterStart = 7.0 }));

        Assert.Equal(7.0, Assert.Single(stellen).Meter, 3);
    }

    [Fact]
    public void Ohne_Protokoll_gibt_es_keine_Stuetzstellen()
        => Assert.Empty(VideoMeterStuetzstellen.AusProtokoll(null));

    [Fact]
    public void Die_Stuetzstellen_kommen_in_zeitlicher_Reihenfolge()
    {
        var stellen = VideoMeterStuetzstellen.AusProtokoll(Dokument(
            new ProtocolEntry { Zeit = TimeSpan.FromSeconds(30), MeterStart = 9.0 },
            new ProtocolEntry { Zeit = TimeSpan.FromSeconds(10), MeterStart = 3.0 }));

        Assert.Equal(new[] { 3.0, 9.0 }, stellen.Select(s => s.Meter).ToArray());
    }
}
