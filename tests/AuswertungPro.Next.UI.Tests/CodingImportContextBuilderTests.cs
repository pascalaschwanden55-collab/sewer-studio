using AuswertungPro.Next.Domain.Models;
using AuswertungPro.Next.Domain.Protocol;
using AuswertungPro.Next.UI.Ai;
using AuswertungPro.Next.UI.Ai.Coding;

namespace AuswertungPro.Next.UI.Tests;

public sealed class CodingImportContextBuilderTests
{
    [Fact]
    public void Build_returns_null_for_null_or_empty_input()
    {
        Assert.Null(CodingImportContextBuilder.Build(null));
        Assert.Null(CodingImportContextBuilder.Build([]));
    }

    [Fact]
    public void Build_skips_events_without_code()
    {
        var context = CodingImportContextBuilder.Build(new[]
        {
            Event(null, "ohne", 1.0),
            Event("  ", "leer", 2.0)
        });

        Assert.Null(context);
    }

    [Fact]
    public void Build_maps_code_description_and_meter()
    {
        var context = CodingImportContextBuilder.Build(new[]
        {
            Event("BAB", "Riss", 3.2),
            Event("BBA", null, 4.5)
        });

        Assert.NotNull(context);
        Assert.Equal(2, context!.Count);
        Assert.Equal(("BAB", "Riss", 3.2), context[0]);
        Assert.Equal(("BBA", "BBA", 4.5), context[1]);
    }

    private static CodingEvent Event(string? code, string? description, double meter)
        => new()
        {
            Entry = new ProtocolEntry { Code = code ?? "", Beschreibung = description! },
            MeterAtCapture = meter
        };
}
