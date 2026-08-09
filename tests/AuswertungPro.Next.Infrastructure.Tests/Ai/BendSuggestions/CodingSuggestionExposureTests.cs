using AuswertungPro.Next.Infrastructure.Ai.BendSuggestions;

namespace AuswertungPro.Next.Infrastructure.Tests.Ai.BendSuggestions;

/// <summary>
/// Sitzungsgedaechtnis der angesehenen Vorschlagslisten: vermerken, abfragen,
/// Normalisierung der Haltungsnummer. Nichts davon darf auf Platte landen.
/// </summary>
public sealed class CodingSuggestionExposureTests
{
    [Fact]
    public void Eine_vermerkte_Haltung_gilt_als_angesehen()
    {
        var gedaechtnis = new CodingSuggestionExposure();

        gedaechtnis.MarkExposed("36053-36052");

        Assert.True(gedaechtnis.WasExposed("36053-36052"));
        Assert.False(gedaechtnis.WasExposed("10261-10262"));
    }

    [Fact]
    public void Die_Haltungsnummer_wird_wie_ueblich_normalisiert()
    {
        // Mit Bereichs-Praefix vermerkt, kanonisch abgefragt — dasselbe Paar.
        var gedaechtnis = new CodingSuggestionExposure();

        gedaechtnis.MarkExposed("07.1028055-10.1064892");

        Assert.True(gedaechtnis.WasExposed("1028055-1064892"));
        Assert.True(gedaechtnis.WasExposed("07.1028055-10.1064892"));
    }

    [Fact]
    public void Eine_unbekannte_oder_leere_Haltung_gilt_als_nicht_angesehen()
    {
        var gedaechtnis = new CodingSuggestionExposure();
        gedaechtnis.MarkExposed("36053-36052");

        Assert.False(gedaechtnis.WasExposed("99999-88888"));
        Assert.False(gedaechtnis.WasExposed(""));
        Assert.False(gedaechtnis.WasExposed("   "));
    }

    [Fact]
    public void Ein_Vermerk_ohne_Haltung_ist_ein_Fehler()
    {
        var gedaechtnis = new CodingSuggestionExposure();

        Assert.ThrowsAny<ArgumentException>(() => gedaechtnis.MarkExposed(""));
    }
}
