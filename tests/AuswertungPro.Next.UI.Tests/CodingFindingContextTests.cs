using AuswertungPro.Next.Application.Ai;
using AuswertungPro.Next.Domain.Models;
using AuswertungPro.Next.Domain.Protocol;
using AuswertungPro.Next.UI.Ai;
using AuswertungPro.Next.UI.Ai.Coding;

namespace AuswertungPro.Next.UI.Tests;

public sealed class CodingFindingContextTests
{
    [Fact]
    public void FilterValid_uses_current_import_events_and_normalizes_the_code()
    {
        var firstImport = new List<CodingEvent> { Event("BCA", 1) };
        var currentImport = firstImport;
        IEnumerable<CodingEvent>? receivedImport = null;
        var trace = new List<string>();
        var context = new CodingFindingContext(
            sessionEvents: () => null,
            viewEvents: () => null,
            importEvents: () => currentImport,
            codeResolver: (_, _, importEvents) =>
            {
                receivedImport = importEvents;
                return "BAB";
            },
            labelLookup: _ => null,
            trace: trace.Add);
        var latestImport = new List<CodingEvent> { Event("BCA", 2) };
        currentImport = latestImport;

        var filtered = context.FilterValid(
            [new LiveFrameFinding("Riss", 2, null, null, VsaCodeHint: "???")],
            currentMeter: 4.2);

        Assert.Same(latestImport, receivedImport);
        Assert.Equal("BAB", Assert.Single(filtered).VsaCodeHint);
        Assert.NotEmpty(trace);
    }

    [Fact]
    public void IsKnown_reads_the_event_providers_at_call_time()
    {
        var sessionEvents = new List<CodingEvent>();
        var context = Context(sessionEvents: () => sessionEvents);
        var finding = new LiveFrameFinding("Riss", 2, null, null, VsaCodeHint: "BAB");

        Assert.False(context.IsKnown(finding, meter: 5.2));

        sessionEvents.Add(Event("BAB", 5));

        Assert.True(context.IsKnown(finding, meter: 5.2));
    }

    [Fact]
    public void LookupLabel_uses_the_configured_catalog_lookup()
    {
        var context = Context(labelLookup: code => code == "BAB" ? "Riss" : null);

        Assert.Equal("Riss", context.LookupLabel("BAB"));
        Assert.Null(context.LookupLabel("BCA"));
    }

    private static CodingFindingContext Context(
        Func<IEnumerable<CodingEvent>?>? sessionEvents = null,
        Func<string, string?>? labelLookup = null)
        => new(
            sessionEvents ?? (() => null),
            viewEvents: () => null,
            importEvents: () => [],
            codeResolver: (_, _, _) => null,
            labelLookup ?? (_ => null));

    private static CodingEvent Event(string code, double meter)
        => new()
        {
            Entry = new ProtocolEntry { Code = code, MeterStart = meter },
            MeterAtCapture = meter
        };
}
