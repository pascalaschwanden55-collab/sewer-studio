using System.Globalization;
using AuswertungPro.Next.Application.Common;
using AuswertungPro.Next.Application.Costs;
using Xunit;

namespace AuswertungPro.Next.Infrastructure.Tests.Common;

/// <summary>
/// Der FachzahlParser muss auf JEDER Windows-Kultur identisch entscheiden —
/// sonst liest ein de-DE-Rechner "45.30" still als 4530 (Faktor-100-Falle).
/// Jeder Fall laeuft deshalb unter de-DE, de-CH und en-US.
/// </summary>
public sealed class FachzahlParserTests
{
    public static TheoryData<string> Kulturen => new() { "de-DE", "de-CH", "en-US" };

    private static (bool Ok, decimal Value) ParseMitKultur(string kultur, string? eingabe)
    {
        var original = CultureInfo.CurrentCulture;
        try
        {
            CultureInfo.CurrentCulture = new CultureInfo(kultur);
            return (FachzahlParser.TryParseDecimal(eingabe, out var wert), wert);
        }
        finally
        {
            CultureInfo.CurrentCulture = original;
        }
    }

    [Theory]
    [MemberData(nameof(Kulturen))]
    public void Beide_dezimaltrennzeichen_und_apostrophe_werden_akkzeptiert(string kultur)
    {
        Assert.Equal((true, 45.30m), ParseMitKultur(kultur, "45.30"));
        Assert.Equal((true, 45.30m), ParseMitKultur(kultur, "45,30"));
        Assert.Equal((true, 1300.50m), ParseMitKultur(kultur, "1'300.50"));
        Assert.Equal((true, 1300.50m), ParseMitKultur(kultur, "1’300.50"));   // typografisch
        Assert.Equal((true, 1300.50m), ParseMitKultur(kultur, "1'300,50"));
        Assert.Equal((true, 1300m), ParseMitKultur(kultur, "1'300"));
    }

    [Theory]
    [MemberData(nameof(Kulturen))]
    public void Gemischte_gruppen_und_dezimalpunkte_letztes_zeichen_zaehlt(string kultur)
    {
        Assert.Equal((true, 1300.50m), ParseMitKultur(kultur, "1.300,50"));   // de-Schreibweise
        Assert.Equal((true, 1300.50m), ParseMitKultur(kultur, "1,300.50"));   // en-Schreibweise
        Assert.Equal((true, 1300.50m), ParseMitKultur(kultur, "1.300,500"));  // Bruch mit 3 Stellen ist hier eindeutig
        Assert.Equal((true, 1300500m), ParseMitKultur(kultur, "1.300.500"));  // nur Tausenderpunkte
        Assert.Equal((true, 1300500m), ParseMitKultur(kultur, "1,300,500"));
    }

    [Theory]
    [MemberData(nameof(Kulturen))]
    public void Einfache_zahlen_und_vorzeichen(string kultur)
    {
        Assert.Equal((true, 1300m), ParseMitKultur(kultur, "1300"));
        Assert.Equal((true, -45.30m), ParseMitKultur(kultur, "-45.30"));
        Assert.Equal((true, 45m), ParseMitKultur(kultur, "+45"));
        Assert.Equal((true, 0.5m), ParseMitKultur(kultur, "0.5"));
        Assert.Equal((true, 0.155m), ParseMitKultur(kultur, "0,155"));
        Assert.Equal((true, 0.155m), ParseMitKultur(kultur, "0.155"));
        Assert.Equal((true, 12345.6m), ParseMitKultur(kultur, "12'345.6"));
    }

    [Theory]
    [MemberData(nameof(Kulturen))]
    public void Mehrdeutiges_wird_abgelehnt_statt_geraten(string kultur)
    {
        // Genau drei Nachkommastellen hinter EINEM Trennzeichen: Tausender- oder Dezimalpunkt?
        Assert.Equal((false, 0m), ParseMitKultur(kultur, "1.300"));
        Assert.Equal((false, 0m), ParseMitKultur(kultur, "1,300"));
        Assert.Equal((false, 0m), ParseMitKultur(kultur, "45.300"));
    }

    [Theory]
    [MemberData(nameof(Kulturen))]
    public void Messwerte_duerfen_explizit_drei_Dezimalstellen_haben(string kultur)
    {
        var original = CultureInfo.CurrentCulture;
        try
        {
            CultureInfo.CurrentCulture = new CultureInfo(kultur);
            Assert.True(FachzahlParser.TryParseMeasurement("45.678", out var dot));
            Assert.Equal(45.678m, dot);
            Assert.True(FachzahlParser.TryParseMeasurement("45,678", out var comma));
            Assert.Equal(45.678m, comma);
            Assert.True(FachzahlParser.TryParseMeasurement("1'300.500", out var grouped));
            Assert.Equal(1300.500m, grouped);
        }
        finally
        {
            CultureInfo.CurrentCulture = original;
        }
    }

    [Theory]
    [MemberData(nameof(Kulturen))]
    public void Ungueltiges_wird_abgelehnt(string kultur)
    {
        Assert.Equal((false, 0m), ParseMitKultur(kultur, null));
        Assert.Equal((false, 0m), ParseMitKultur(kultur, ""));
        Assert.Equal((false, 0m), ParseMitKultur(kultur, "   "));
        Assert.Equal((false, 0m), ParseMitKultur(kultur, "abc"));
        Assert.Equal((false, 0m), ParseMitKultur(kultur, "45."));
        Assert.Equal((false, 0m), ParseMitKultur(kultur, ".5"));
        Assert.Equal((false, 0m), ParseMitKultur(kultur, "1.2.3"));
        Assert.Equal((false, 0m), ParseMitKultur(kultur, "45,30,10"));     // Gruppierung ungueltig
        Assert.Equal((false, 0m), ParseMitKultur(kultur, "45,30.10"));     // Tausender nach Dezimalpunkt
        Assert.Equal((false, 0m), ParseMitKultur(kultur, "1.2.3,4.5"));    // mehrere beider Arten
        Assert.Equal((false, 0m), ParseMitKultur(kultur, "45,30,10.5"));   // Gruppierung ungueltig
    }

    [Theory]
    [MemberData(nameof(Kulturen))]
    public void Fehlerhafte_apostroph_und_leergruppen_werden_abgelehnt(string kultur)
    {
        Assert.Equal((false, 0m), ParseMitKultur(kultur, "1'30"));
        Assert.Equal((false, 0m), ParseMitKultur(kultur, "45'30"));
        Assert.Equal((false, 0m), ParseMitKultur(kultur, "1'300'50"));
        Assert.Equal((false, 0m), ParseMitKultur(kultur, "1 2"));
        Assert.Equal((false, 0m), ParseMitKultur(kultur, "12 34"));
        Assert.Equal((false, 0m), ParseMitKultur(kultur, "1 300 50"));
    }

    [Fact]
    public void Tabellenkosten_unterscheiden_leer_null_und_ungueltig()
    {
        Assert.True(TablePauschaleCostHelper.TryParseTableNetCost(null, out var nullValue));
        Assert.Equal(0m, nullValue);
        Assert.True(TablePauschaleCostHelper.TryParseTableNetCost("  ", out var emptyValue));
        Assert.Equal(0m, emptyValue);
        Assert.True(TablePauschaleCostHelper.TryParseTableNetCost("0", out var zeroValue));
        Assert.Equal(0m, zeroValue);

        Assert.False(TablePauschaleCostHelper.TryParseTableNetCost("45'30", out _));
        Assert.False(TablePauschaleCostHelper.TryParseTableNetCost("nicht lesbar", out _));
    }
}
