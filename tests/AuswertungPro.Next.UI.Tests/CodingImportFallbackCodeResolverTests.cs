using AuswertungPro.Next.Domain.Models;
using AuswertungPro.Next.Domain.Protocol;
using AuswertungPro.Next.UI.Ai;

namespace AuswertungPro.Next.UI.Tests;

public sealed class CodingImportFallbackCodeResolverTests
{
    [Fact]
    public void RefineGenericCode_returns_nearest_allowed_code_in_same_family()
    {
        var events = new[]
        {
            Event("BAB", 10.0),
            Event("BAB12", 10.4),
            Event("BBA", 10.1)
        };

        var code = CodingImportFallbackCodeResolver.RefineGenericCode(events, "BAB", currentMeter: 10.35);

        Assert.Equal("BAB12", code);
    }

    [Fact]
    public void RefineGenericCode_ignores_codes_outside_family_or_meter_window()
    {
        var events = new[]
        {
            Event("BAB", 15.0),
            Event("BBA", 10.0)
        };

        var code = CodingImportFallbackCodeResolver.RefineGenericCode(events, "BAB", currentMeter: 10.0);

        Assert.Null(code);
    }

    [Fact]
    public void ResolveFallbackCode_returns_best_allowed_import_candidate()
    {
        var events = new[]
        {
            Event("XYZ", 10.0),
            Event("BBA", 10.6),
            Event("BAB", 10.1)
        };

        var code = CodingImportFallbackCodeResolver.ResolveFallbackCode(events, currentMeter: 10.0);

        Assert.Equal("BAB", code);
    }

    [Fact]
    public void ResolveFallbackCode_uses_tighter_window_for_bcc_bends()
    {
        var events = new[]
        {
            Event("BCC", 10.3),
            Event("BAB", 11.0)
        };

        var code = CodingImportFallbackCodeResolver.ResolveFallbackCode(events, currentMeter: 10.0);

        Assert.Equal("BAB", code);
    }

    private static CodingEvent Event(string code, double meter)
        => new()
        {
            Entry = new ProtocolEntry { Code = code },
            MeterAtCapture = meter
        };
}
