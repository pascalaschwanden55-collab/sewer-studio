using AuswertungPro.Next.Domain.Models;

namespace AuswertungPro.Next.Infrastructure.Tests.Import;

/// <summary>
/// SIA405 verlangt Millimeter als ganze Zahl. Im Programm stehen Meter (Schacht)
/// und Millimeter (Rohr) nebeneinander - ein pauschales mal 1000 waere selbst der
/// Fehler. Die Regel ist dieselbe, die die SchachtPro-Zeichnung benutzt: ein Wert
/// ueber 10 ist bereits ein Millimeterwert.
/// </summary>
public sealed class SiaAbmessungTests
{
    [Theory]
    [InlineData("1.00", 1000)]      // Schacht in Metern
    [InlineData("0.60", 600)]
    [InlineData("1,00", 1000)]      // Komma als Trenner
    [InlineData("0,80", 800)]
    [InlineData("4.00", 4000)]
    public void Meterwerte_werden_auf_Millimeter_gebracht(string wert, int mm)
    {
        Assert.Equal(mm, SiaAbmessung.NachMillimeter(wert));
    }

    [Theory]
    [InlineData("800", 800)]        // Schacht bereits in Millimetern
    [InlineData("200", 200)]        // Rohr-DN
    [InlineData("110", 110)]
    [InlineData("1600", 1600)]
    [InlineData("200.0", 200)]
    [InlineData("1000", 1000)]      // die Falle: "1000" statt "1.00" getippt
    public void Millimeterwerte_bleiben_unveraendert(string wert, int mm)
    {
        // "1000" bleibt 1000 mm. Ein stures mal 1000 machte daraus 1000000 -
        // und zwar genau bei den Protokollen, bei denen in der App nie etwas auffiel.
        Assert.Equal(mm, SiaAbmessung.NachMillimeter(wert));
    }

    [Theory]
    [InlineData("DN 200", 200)]
    [InlineData("DN200", 200)]
    [InlineData(" 250 mm ", 250)]
    public void Uebliche_Zusaetze_werden_toleriert(string wert, int mm)
    {
        Assert.Equal(mm, SiaAbmessung.NachMillimeter(wert));
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData(null)]
    [InlineData("unbekannt")]
    [InlineData("rund")]
    [InlineData("0")]
    [InlineData("0.00")]
    [InlineData("-5")]
    public void Ohne_brauchbaren_Wert_wird_nichts_geschrieben(string? wert)
    {
        // Lieber kein Wert als eine erfundene Zahl.
        Assert.Null(SiaAbmessung.NachMillimeter(wert));
    }

    [Theory]
    [InlineData("0.60/1.00", 600, 1000)]     // SchachtPro schreibt Laenge/Breite so
    [InlineData("800/600", 800, 600)]
    [InlineData("1.00", 1000, 1000)]         // rund: ein Wert, beide Masse gleich
    [InlineData("800", 800, 800)]
    public void Ein_Wertepaar_wird_auf_zwei_Masse_verteilt(string wert, int d1, int d2)
    {
        var (erstes, zweites) = SiaAbmessung.NachMillimeterPaar(wert);
        Assert.Equal(d1, erstes);
        Assert.Equal(d2, zweites);
    }

    [Fact]
    public void Ein_unbrauchbares_Paar_liefert_zweimal_nichts()
    {
        var (erstes, zweites) = SiaAbmessung.NachMillimeterPaar("rund/unbekannt");
        Assert.Null(erstes);
        Assert.Null(zweites);
    }

    [Theory]
    [InlineData("900 x 1100 mm", 900, 1100)]   // so steht es in Zone 1.15, 36 Schaechte
    [InlineData("900x1100", 900, 1100)]
    [InlineData("0.60 × 1.00", 600, 1000)]     // mit dem echten Malzeichen
    [InlineData("800 * 600", 800, 600)]
    public void Auch_das_Mal_Zeichen_trennt_ein_Wertepaar(string wert, int d1, int d2)
    {
        // Gefunden an echten Daten: die Trennung mit "/" allein reicht nicht.
        // "900 x 1100 mm" waere sonst als 900/900 gelesen worden.
        var (erstes, zweites) = SiaAbmessung.NachMillimeterPaar(wert);
        Assert.Equal(d1, erstes);
        Assert.Equal(d2, zweites);
    }

    [Theory]
    [InlineData("800 mm", 800)]
    [InlineData("1000 mm", 1000)]
    public void Werte_aus_einem_echten_Projekt_werden_richtig_gelesen(string wert, int mm)
    {
        Assert.Equal(mm, SiaAbmessung.NachMillimeter(wert));
    }
}
