using AuswertungPro.Next.Application.Ai;
using AuswertungPro.Next.Domain.Models;
using AuswertungPro.Next.Domain.Protocol;
using AuswertungPro.Next.Infrastructure.Ai;

namespace AuswertungPro.Next.Pipeline.Tests;

[Collection(VsaCodeResolverTestCollection.Name)]
public sealed class CodingFindingCodeResolverTests
{
    public CodingFindingCodeResolverTests()
    {
        VsaResolverTestCatalog.ConfigureDefault();
    }

    [Fact]
    public void Resolve_uses_normalized_hint_and_refines_with_import_event()
    {
        var finding = Finding(label: "unknown", codeHint: "BAB");
        var importEvents = new[] { Event("BAB12", 10.1), Event("BBA", 10.0) };

        var code = CodingFindingCodeResolver.Resolve(finding, currentMeter: 10.1, importEvents);

        Assert.Equal("BAB12", code);
    }

    [Fact]
    public void Resolve_uses_label_inference_when_hint_is_missing()
    {
        var finding = Finding(label: "crack in pipe", codeHint: null);
        var importEvents = new[] { Event("BAB34", 4.0), Event("BBA", 4.0) };

        var code = CodingFindingCodeResolver.Resolve(finding, currentMeter: 4.0, importEvents);

        Assert.Equal("BAB34", code);
    }

    [Fact]
    public void Resolve_uses_import_fallback_when_hint_and_label_do_not_resolve()
    {
        var finding = Finding(label: "unknown object", codeHint: null);
        var importEvents = new[] { Event("BCA", 7.1) };

        var code = CodingFindingCodeResolver.Resolve(finding, currentMeter: 7.0, importEvents);

        Assert.Equal("BCA", code);
    }

    [Fact]
    public void Resolve_returns_null_when_no_source_resolves()
    {
        var finding = Finding(label: "unknown object", codeHint: "???");

        var code = CodingFindingCodeResolver.Resolve(finding, currentMeter: 7.0, importEvents: []);

        Assert.Null(code);
    }

    [Fact]
    public void Resolve_maps_yolo_bbd_class_to_persistable_bbd_subcode()
    {
        var finding = Finding(label: "BBD_boden", codeHint: null);

        var code = CodingFindingCodeResolver.Resolve(finding, currentMeter: 7.0, importEvents: []);

        Assert.Equal("BBDZ", code);
        Assert.NotEqual("BBD", code);
    }

    private static CodingEvent Event(string code, double meter)
        => new()
        {
            Entry = new ProtocolEntry { Code = code },
            MeterAtCapture = meter
        };

    private static LiveFrameFinding Finding(string label, string? codeHint)
        => new(
            Label: label,
            Severity: 2,
            PositionClock: null,
            ExtentPercent: null,
            VsaCodeHint: codeHint);
}
