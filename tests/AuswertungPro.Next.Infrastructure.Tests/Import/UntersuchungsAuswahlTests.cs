using AuswertungPro.Next.Application.UseCases.Import.Quellen;

namespace AuswertungPro.Next.Infrastructure.Tests.Import;

/// <summary>
/// Die gemeinsame Auswahlregel fuer mehrere Untersuchungen derselben Haltung (WinCan und
/// VSA-KEK-XTF). Das Verhalten im WinCan-Import sichern zusaetzlich die WinCan-Tests.
/// </summary>
public sealed class UntersuchungsAuswahlTests
{
    [Theory]
    [InlineData(2007, 12, 31)]
    [InlineData(1989, 12, 31)]
    [InlineData(1900, 1, 1)]
    public void Platzhalter_sind_nicht_glaubwuerdig(int jahr, int monat, int tag)
        => Assert.Null(UntersuchungsAuswahl.Glaubwuerdig(new DateTime(jahr, monat, tag, 23, 27, 20)));

    [Fact]
    public void Ein_echter_Aufnahmetag_ist_glaubwuerdig()
        => Assert.Equal(new DateTime(1990, 1, 1), UntersuchungsAuswahl.Glaubwuerdig(new DateTime(1990, 1, 1)));

    [Fact]
    public void Der_Sortierschluessel_faellt_ueber_das_zweite_Datum_auf_den_Zeitstempel_zurueck()
    {
        var zeitstempel = new DateTime(2024, 5, 1);

        Assert.Equal(new DateTime(2020, 2, 2), UntersuchungsAuswahl.Sortierschluessel(
            new DateTime?[] { new DateTime(2007, 12, 31), new DateTime(2020, 2, 2) }, zeitstempel));
        Assert.Equal(zeitstempel, UntersuchungsAuswahl.Sortierschluessel(
            new DateTime?[] { new DateTime(2007, 12, 31), null }, zeitstempel));
        Assert.Equal(DateTime.MinValue, UntersuchungsAuswahl.Sortierschluessel(new DateTime?[] { null }));
    }

    [Fact]
    public void Ordne_nimmt_das_neueste_zuerst_und_behaelt_bei_Gleichstand_die_Quellreihenfolge()
    {
        var kandidaten = new[]
        {
            ("a", DateTime.MinValue), ("b", new DateTime(2025, 3, 12)),
            ("c", new DateTime(2025, 3, 15)), ("d", new DateTime(2025, 3, 15))
        };

        var geordnet = UntersuchungsAuswahl.Ordne(kandidaten, k => k.Item2);

        Assert.Equal(new[] { "c", "d", "b", "a" }, geordnet.Select(k => k.Item1));
    }
}
