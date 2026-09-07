using AuswertungPro.Next.Application.UseCases.Uebersicht;
using Xunit;

namespace AuswertungPro.Next.Infrastructure.Tests;

/// <summary>
/// Nova-Fixwelle B3: Ein leeres Eckdatenfeld zeigt einen Gedankenstrich, nie eine nackte
/// Einheit (" m") oder einen einsamen Trenner (" · ").
/// </summary>
public sealed class HaltungFaktenTextTests
{
    [Theory]
    [InlineData("Beton", null, "Beton")]
    [InlineData("30", "m", "30 m")]
    [InlineData("  30  ", "m", "30 m")]
    public void Ein_gefuellter_Wert_bleibt_stehen(string wert, string? einheit, string erwartet)
        => Assert.Equal(erwartet, HaltungFaktenText.Wert(wert, einheit));

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void Ein_leerer_Wert_wird_zum_Gedankenstrich(string? wert)
    {
        Assert.Equal(HaltungFaktenText.Leer, HaltungFaktenText.Wert(wert));
        Assert.Equal(HaltungFaktenText.Leer, HaltungFaktenText.Wert(wert, "m"));
    }

    [Fact]
    public void Mehrere_Teile_werden_verbunden()
        => Assert.Equal("300 · Kreisprofil", HaltungFaktenText.Zusammen(new[] { "300", "Kreisprofil" }));

    [Fact]
    public void Ein_leerer_Teil_faellt_weg()
        => Assert.Equal("300", HaltungFaktenText.Zusammen(new[] { "300", "  " }));

    [Fact]
    public void Alle_Teile_leer_ergibt_den_Gedankenstrich()
        => Assert.Equal(HaltungFaktenText.Leer, HaltungFaktenText.Zusammen(new string?[] { null, "" }));

    /// <summary>
    /// Nova-Etappe 2b, Task 6: Ohne gewaehlte Zeile liefert WPF fuer eine Feldbindung
    /// <c>DependencyProperty.UnsetValue</c>. Wird das stur in Text verwandelt, steht in der
    /// Uebersicht "{DependencyProperty.UnsetValue}" — genau der Fehltext aus Pascals Bild vom
    /// 07.09. bei DN / Profil. Ein solcher Platzhalter ist kein Wert und faellt weg.
    /// </summary>
    [Theory]
    [InlineData("{DependencyProperty.UnsetValue}")]
    [InlineData("{Bindungsfehler}")]
    public void Der_Text_einer_nicht_gesetzten_Bindung_gilt_als_leer(string platzhalter)
    {
        Assert.True(HaltungFaktenText.IstLeer(platzhalter));
        Assert.Equal(HaltungFaktenText.Leer, HaltungFaktenText.Wert(platzhalter));
        Assert.Equal(HaltungFaktenText.Leer, HaltungFaktenText.Wert(platzhalter, "m"));
        Assert.Equal(HaltungFaktenText.Leer, HaltungFaktenText.Zusammen(new[] { platzhalter }));
    }

    [Fact]
    public void Ein_Platzhalter_neben_einem_echten_Wert_faellt_weg()
        => Assert.Equal("Kreisprofil", HaltungFaktenText.Zusammen(new[] { "{DependencyProperty.UnsetValue}", "Kreisprofil" }));

    /// <summary>Ein echter Wert bleibt ein echter Wert - die Regel greift nur am Zeilenanfang.</summary>
    [Fact]
    public void Ein_Wert_mit_geschweifter_Klammer_in_der_Mitte_bleibt_stehen()
        => Assert.Equal("Beton {alt}", HaltungFaktenText.Wert("Beton {alt}"));
}
