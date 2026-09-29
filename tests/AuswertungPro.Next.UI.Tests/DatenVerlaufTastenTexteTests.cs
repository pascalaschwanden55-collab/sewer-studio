using AuswertungPro.Next.UI.Services;

namespace AuswertungPro.Next.UI.Tests;

/// <summary>
/// Optikanalyse 28.09.2026, Nachtrag: <see cref="DatenVerlaufTasten.MenuText"/> verdoppelt einen
/// Unterstrich in der Beschreibung bewusst - ausserhalb eines WPF-Menues (etwa in der Trefferliste
/// der globalen Suche, Strg+K, die als reiner <c>TextBlock</c> gebunden ist) waere dieselbe
/// Verdopplung ein sichtbarer Darstellungsfehler statt einer Zugriffstasten-Schutzmassnahme.
/// <see cref="DatenVerlaufTasten.SuchText"/> ist deshalb die unescapte Fassung fuer genau diese
/// Faelle (siehe <c>ShellViewModel.RueckgaengigSuchText</c>/<c>WiederholenSuchText</c> und
/// <see cref="GlobaleSucheRueckgaengigWiederholenTests"/> fuer die vollstaendige Verdrahtung).
/// </summary>
public sealed class DatenVerlaufTastenTexteTests
{
    [Fact]
    public void MenuText_verdoppelt_einen_unterstrich_in_der_beschreibung()
    {
        var text = DatenVerlaufTasten.MenuText("Rückgängig", "Feld_Name 10001-10002");

        Assert.Equal("Rückgängig: Feld__Name 10001-10002", text);
    }

    [Fact]
    public void SuchText_laesst_einen_unterstrich_in_der_beschreibung_unveraendert()
    {
        var text = DatenVerlaufTasten.SuchText("Rückgängig", "Feld_Name 10001-10002");

        Assert.Equal("Rückgängig: Feld_Name 10001-10002", text);
    }

    [Fact]
    public void Beide_liefern_denselben_text_ohne_unterstrich_in_der_beschreibung()
    {
        Assert.Equal(
            DatenVerlaufTasten.MenuText("Wiederholen", "Rohrmaterial 10001-10002"),
            DatenVerlaufTasten.SuchText("Wiederholen", "Rohrmaterial 10001-10002"));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void Ohne_beschreibung_liefern_beide_nur_die_aktion(string? beschreibung)
    {
        Assert.Equal("Rückgängig", DatenVerlaufTasten.MenuText("Rückgängig", beschreibung));
        Assert.Equal("Rückgängig", DatenVerlaufTasten.SuchText("Rückgängig", beschreibung));
    }
}
