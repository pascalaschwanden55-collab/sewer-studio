using AuswertungPro.Next.Domain.Models;
using AuswertungPro.Next.Domain.Protocol;
using AuswertungPro.Next.UI.Ai;

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
