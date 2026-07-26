using AuswertungPro.Next.Domain.Models;
using AuswertungPro.Next.Domain.Protocol;
using AuswertungPro.Next.UI.Ai;
using AuswertungPro.Next.UI.Ai.Coding;

namespace AuswertungPro.Next.UI.Tests;

public sealed class CodingOpenStretchDamagePolicyTests
{
    [Fact]
    public void FindOpen_returns_only_stretch_damage_events_without_meter_end()
    {
        var open = Event("BAB", isStretch: true, meterStart: 1.0, meterEnd: null, meterAtCapture: 2.0);
        var closed = Event("BAC", isStretch: true, meterStart: 1.0, meterEnd: 2.5, meterAtCapture: 2.5);
        var point = Event("BAJ", isStretch: false, meterStart: 3.0, meterEnd: null, meterAtCapture: 3.0);

        var result = CodingOpenStretchDamagePolicy.FindOpen([closed, open, point]);

        Assert.Equal(new[] { open }, result);
    }

    [Fact]
    public void ResolveCloseMeter_uses_meter_at_capture_when_it_is_after_start()
    {
        var ev = Event("BAB", isStretch: true, meterStart: 1.0, meterEnd: null, meterAtCapture: 2.25);

        var meter = CodingOpenStretchDamagePolicy.ResolveCloseMeter(ev, currentMeter: 5.0);

        Assert.Equal(2.25, meter);
    }

    [Fact]
    public void ResolveCloseMeter_uses_current_meter_when_meter_at_capture_is_not_after_start()
    {
        var ev = Event("BAB", isStretch: true, meterStart: 3.0, meterEnd: null, meterAtCapture: 2.0);

        var meter = CodingOpenStretchDamagePolicy.ResolveCloseMeter(ev, currentMeter: 5.0);

        Assert.Equal(5.0, meter);
    }

    private static CodingEvent Event(
        string code,
        bool isStretch,
        double? meterStart,
        double? meterEnd,
        double meterAtCapture)
        => new()
        {
            Entry = new ProtocolEntry
            {
                Code = code,
                IsStreckenschaden = isStretch,
                MeterStart = meterStart,
                MeterEnd = meterEnd
            },
            MeterAtCapture = meterAtCapture
        };
}
