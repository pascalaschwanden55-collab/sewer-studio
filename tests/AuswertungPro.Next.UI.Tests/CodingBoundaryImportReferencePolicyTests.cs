using AuswertungPro.Next.Domain.Models;
using AuswertungPro.Next.Domain.Protocol;
using AuswertungPro.Next.UI.Ai;
using AuswertungPro.Next.UI.Ai.Coding;

namespace AuswertungPro.Next.UI.Tests;

public sealed class CodingBoundaryImportReferencePolicyTests
{
    [Fact]
    public void ResolveStart_uses_import_bcd_when_available()
    {
        var result = CodingBoundaryImportReferencePolicy.ResolveStart(
            [Event("BAB", 2.0, 20), Event("BCD", 0.15, 3)]);

        Assert.Equal(0.15, result.Meter);
        Assert.Equal(TimeSpan.FromSeconds(3), result.VideoTime);
    }

    [Fact]
    public void ResolveStart_defaults_to_zero_without_import_bcd()
    {
        var result = CodingBoundaryImportReferencePolicy.ResolveStart(
            [Event("BAB", 2.0, 20)]);

        Assert.Equal(0.0, result.Meter);
        Assert.Equal(TimeSpan.Zero, result.VideoTime);
    }

    [Fact]
    public void ResolveEnd_corrects_implausible_osd_to_import_and_uses_import_time()
    {
        var result = CodingBoundaryImportReferencePolicy.ResolveEnd(
            [Event("BCE", 15.82, 90)],
            osdMeter: 114.13,
            fallbackEndMeter: 15.82,
            vmEndMeter: 15.82,
            fallbackVideoTime: TimeSpan.FromSeconds(80));

        Assert.Equal(15.82, result.Meter);
        Assert.Equal(TimeSpan.FromSeconds(90), result.VideoTime);
    }

    [Fact]
    public void ResolveEnd_keeps_plausible_osd_and_fallback_time()
    {
        var fallbackTime = TimeSpan.FromSeconds(80);

        var result = CodingBoundaryImportReferencePolicy.ResolveEnd(
            [Event("BCE", 15.82, 90)],
            osdMeter: 15.70,
            fallbackEndMeter: 15.82,
            vmEndMeter: 15.82,
            fallbackVideoTime: fallbackTime);

        Assert.Equal(15.70, result.Meter);
        Assert.Equal(fallbackTime, result.VideoTime);
    }

    private static CodingEvent Event(string code, double meter, double seconds)
        => new()
        {
            Entry = new ProtocolEntry { Code = code },
            MeterAtCapture = meter,
            VideoTimestamp = TimeSpan.FromSeconds(seconds)
        };
}
