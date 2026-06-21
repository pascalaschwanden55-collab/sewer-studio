using AuswertungPro.Next.Domain.Models;
using AuswertungPro.Next.Domain.Protocol;
using AuswertungPro.Next.UI.Ai;

namespace AuswertungPro.Next.UI.Tests;

public sealed class CodingEventsSignatureBuilderTests
{
    [Fact]
    public void Build_is_stable_independent_of_input_order()
    {
        var first = Event("BAB", meter: 2, entryId: Guid.Parse("00000000-0000-0000-0000-000000000002"));
        var second = Event("BCA", meter: 1, entryId: Guid.Parse("00000000-0000-0000-0000-000000000001"));

        var a = CodingEventsSignatureBuilder.Build([first, second]);
        var b = CodingEventsSignatureBuilder.Build([second, first]);

        Assert.Equal(a, b);
    }

    [Fact]
    public void BuildEventSignature_sorts_code_meta_parameters_case_insensitive()
    {
        var ev = Event("BAB", meter: 1.2345, entryId: Guid.Parse("00000000-0000-0000-0000-000000000001"));
        ev.Entry.CodeMeta = new ProtocolEntryCodeMeta();
        ev.Entry.CodeMeta.Parameters["z"] = "last";
        ev.Entry.CodeMeta.Parameters["A"] = "first";

        var signature = CodingEventsSignatureBuilder.BuildEventSignature(ev);
        var fields = signature.Split('|');

        Assert.Contains("A=first;z=last", signature);
        Assert.Equal("1.235", fields[3]);
        Assert.Equal("1.235", fields[4]);
        Assert.Equal("1.235", fields[11]);
    }

    [Fact]
    public void BuildEventSignature_includes_deleted_and_stretch_flags()
    {
        var ev = Event("BAB", meter: 1, entryId: Guid.Parse("00000000-0000-0000-0000-000000000001"));
        ev.Entry.IsDeleted = true;
        ev.Entry.IsStreckenschaden = true;

        var signature = CodingEventsSignatureBuilder.BuildEventSignature(ev);

        Assert.Contains("|1|", signature);
        Assert.Contains("|Manual|1|", signature);
    }

    private static CodingEvent Event(string code, double meter, Guid entryId)
        => new()
        {
            Entry = new ProtocolEntry
            {
                EntryId = entryId,
                Code = code,
                Beschreibung = code,
                MeterStart = meter,
                MeterEnd = meter,
                Source = ProtocolEntrySource.Manual
            },
            MeterAtCapture = meter,
            VideoTimestamp = TimeSpan.FromSeconds(meter)
        };
}
