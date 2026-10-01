using AuswertungPro.Next.Domain.Models;
using AuswertungPro.Next.Domain.Protocol;
using AuswertungPro.Next.UI.Ai;
using AuswertungPro.Next.UI.Ai.Coding;

namespace AuswertungPro.Next.UI.Tests;

public sealed class CodingStreckenschadenActionInputBuilderTests
{
    [Fact]
    public void BuildOpenEntries_keeps_only_open_stretch_damage_events()
    {
        var openWithStart = Event("BBA", isStretch: true, meterStart: 12.3, meterEnd: null, capturedAt: 10);
        var openWithoutStart = Event("BBC", isStretch: true, meterStart: null, meterEnd: null, capturedAt: 14.5);
        var closed = Event("BBA", isStretch: true, meterStart: 1.0, meterEnd: 2.0, capturedAt: 1.0);
        var pointDamage = Event("BCA", isStretch: false, meterStart: 5.0, meterEnd: null, capturedAt: 5.0);

        var entries = CodingStreckenschadenActionInputBuilder.BuildOpenEntries(
            new[] { openWithStart, openWithoutStart, closed, pointDamage });

        Assert.Equal(2, entries.Count);
        Assert.Equal("BBA", entries[0].MainCode);
        Assert.Equal(12.3, entries[0].StartMeter);
        Assert.Same(openWithStart, entries[0].Reference);
        Assert.Equal("BBC", entries[1].MainCode);
        Assert.Equal(14.5, entries[1].StartMeter);
        Assert.Same(openWithoutStart, entries[1].Reference);
    }

    /// <summary>
    /// Die beim Schliessen erzeugte Endmarke traegt selbst IsStreckenschaden=true
    /// und kein MeterEnd - sie sieht also wie ein offener Anfang aus. Der Tracker
    /// wuerde sie sonst als offene Strecke behandeln: entweder einen neuen Anfang
    /// unterdruecken oder der Endmarke ein MeterEnd verpassen, womit im Protokoll
    /// eine zweite, erfundene Strecke steht.
    /// </summary>
    [Fact]
    public void BuildOpenEntries_haelt_die_Endmarke_eines_Streckenschadens_heraus()
    {
        var start = Event("BBA", isStretch: true, meterStart: 4.82, meterEnd: 9.88, capturedAt: 4.82);
        start.Entry.Beschreibung = "Wurzeln";

        // Genau so entsteht sie in CodingStreckenschadenEventFactory.CloseStart.
        var endMarker = CodingStreckenschadenEventFactory.CloseStart(start.Entry, 9.88);
        var endEvent = new CodingEvent
        {
            MeterAtCapture = 9.88,
            Entry = endMarker
        };

        var entries = CodingStreckenschadenActionInputBuilder.BuildOpenEntries(
            new[] { start, endEvent });

        Assert.Empty(entries);
    }

    /// <summary>
    /// Ein wirklich offener Anfang muss weiterhin durchkommen - auch wenn im
    /// selben Lauf eine fremde Endmarke danebensteht.
    /// </summary>
    [Fact]
    public void BuildOpenEntries_behaelt_den_offenen_Anfang_neben_einer_Endmarke()
    {
        var geschlossen = Event("BBA", isStretch: true, meterStart: 4.82, meterEnd: 9.88, capturedAt: 4.82);
        geschlossen.Entry.Beschreibung = "Wurzeln";
        var endEvent = new CodingEvent
        {
            MeterAtCapture = 9.88,
            Entry = CodingStreckenschadenEventFactory.CloseStart(geschlossen.Entry, 9.88)
        };
        var offen = Event("BBC", isStretch: true, meterStart: 14.5, meterEnd: null, capturedAt: 14.5);

        var entries = CodingStreckenschadenActionInputBuilder.BuildOpenEntries(
            new[] { geschlossen, endEvent, offen });

        var einziger = Assert.Single(entries);
        Assert.Equal("BBC", einziger.MainCode);
        Assert.Same(offen, einziger.Reference);
    }

    private static CodingEvent Event(
        string code,
        bool isStretch,
        double? meterStart,
        double? meterEnd,
        double capturedAt)
    {
        return new CodingEvent
        {
            MeterAtCapture = capturedAt,
            Entry = new ProtocolEntry
            {
                Code = code,
                IsStreckenschaden = isStretch,
                MeterStart = meterStart,
                MeterEnd = meterEnd
            }
        };
    }
}
