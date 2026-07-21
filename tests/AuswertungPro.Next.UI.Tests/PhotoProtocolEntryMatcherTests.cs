using AuswertungPro.Next.Application.Media;
using AuswertungPro.Next.Domain.Protocol;

namespace AuswertungPro.Next.UI.Tests;

public sealed class PhotoProtocolEntryMatcherTests
{
    [Fact]
    public void FindNearestActiveEntry_findet_naechsten_aktiven_Eintrag_bis_einschliesslich_eins_Meter()
    {
        var deleted = Entry(10.1, deleted: true);
        var firstAtEqualDistance = Entry(9);
        var secondAtEqualDistance = Entry(11);

        var result = PhotoProtocolEntryMatcher.FindNearestActiveEntry(
            [deleted, firstAtEqualDistance, secondAtEqualDistance],
            meter: 10);

        Assert.Same(firstAtEqualDistance, result);
    }

    [Fact]
    public void FindNearestActiveEntry_ignoriert_Eintraege_ohne_Meter_und_ausserhalb_der_Grenze()
    {
        var withoutMeter = new ProtocolEntry();
        var tooFar = Entry(11.01);

        var result = PhotoProtocolEntryMatcher.FindNearestActiveEntry(
            [withoutMeter, tooFar],
            meter: 10);

        Assert.Null(result);
    }

    [Fact]
    public void FindNearestActiveEntry_ignoriert_ausschliesslich_geloeschte_Eintraege()
    {
        var result = PhotoProtocolEntryMatcher.FindNearestActiveEntry(
            [Entry(10, deleted: true)],
            meter: 10);

        Assert.Null(result);
    }

    private static ProtocolEntry Entry(double meter, bool deleted = false)
        => new()
        {
            MeterStart = meter,
            IsDeleted = deleted
        };
}
