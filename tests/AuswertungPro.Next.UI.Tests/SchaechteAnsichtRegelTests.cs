using AuswertungPro.Next.UI.DataPage;

namespace AuswertungPro.Next.UI.Tests;

/// <summary>
/// Nova, Aufklapp-Liste (Task 6): Die Schachtseite verwendet dieselbe
/// <see cref="HaltungenAnsichtRegel"/> wie die Haltungen — nur gefuettert mit
/// <c>AppSettings.SchaechteAnsicht</c> statt <c>AppSettings.HaltungenAnsicht</c>. Es gibt keine
/// zweite Regel: Drei Ansichten schliessen sich aus (Aufklapp-Liste, Tabelle, alte
/// Schachtansicht), und dieselbe Zuordnung gilt fuer beide Seiten (Spaltenchips und
/// Eingabefelder nur in der Tabelle, Uebersicht in beiden Nova-Ansichten). Einzig
/// <c>AbdockenMoeglich</c> bleibt an der Schachtseite ungenutzt — Abdocken gibt es dort nicht.
/// </summary>
public sealed class SchaechteAnsichtRegelTests
{
    [Theory]
    [InlineData(null, "liste")]
    [InlineData("", "liste")]
    [InlineData("liste", "liste")]
    [InlineData("Tabelle", "tabelle")]
    [InlineData("alte schachtansicht", "liste")]
    public void Nur_liste_und_tabelle_sind_gueltig_auch_fuer_die_Schachteinstellung(
        string? gespeichertesSchaechteAnsicht, string erwartet)
        => Assert.Equal(erwartet, HaltungenAnsichtRegel.Normalisiere(gespeichertesSchaechteAnsicht));

    [Fact]
    public void Ohne_Einstellung_ist_die_Aufklapp_Liste_auch_bei_Schaechten_der_Standard()
    {
        // AppSettings.SchaechteAnsicht ist standardmaessig "liste" (wie HaltungenAnsicht).
        var sicht = HaltungenAnsichtRegel.Bestimme(novaAktiv: true, ansicht: null);

        Assert.True(sicht.Liste);
        Assert.False(sicht.Tabelle);
        Assert.False(sicht.AlteAnsicht);
    }

    [Fact]
    public void In_der_Schacht_Liste_verschwinden_Spaltenchips_und_Eingabefelder_die_Uebersicht_bleibt()
    {
        var sicht = HaltungenAnsichtRegel.Bestimme(novaAktiv: true, ansicht: "liste");

        Assert.False(sicht.Spaltenchips);
        Assert.False(sicht.Eingabefelder);
        // Die Schachtansicht rechts (mit der Schachtgrafik) folgt in beiden Nova-Ansichten
        // der Auswahl — genau wie die Haltungs-Uebersicht.
        Assert.True(sicht.Uebersicht);
    }

    [Fact]
    public void In_der_Schacht_Tabelle_gilt_alles_wie_bisher()
    {
        var sicht = HaltungenAnsichtRegel.Bestimme(novaAktiv: true, ansicht: "tabelle");

        Assert.False(sicht.Liste);
        Assert.True(sicht.Tabelle);
        Assert.False(sicht.AlteAnsicht);
        Assert.True(sicht.Spaltenchips);
        Assert.True(sicht.Eingabefelder);
        Assert.True(sicht.Uebersicht);
    }

    [Theory]
    [InlineData("liste")]
    [InlineData("tabelle")]
    public void Ohne_Nova_Layout_gilt_immer_die_alte_Schachtansicht(string ansicht)
    {
        var sicht = HaltungenAnsichtRegel.Bestimme(novaAktiv: false, ansicht);

        Assert.True(sicht.AlteAnsicht);
        Assert.False(sicht.Liste);
        Assert.False(sicht.Tabelle);
        Assert.False(sicht.Uebersicht);
        Assert.False(sicht.Eingabefelder);
        Assert.False(sicht.Spaltenchips);
    }

    [Fact]
    public void Die_gespeicherte_Schacht_Ansicht_ueberlebt_einen_Ausflug_in_die_alte_Ansicht()
    {
        Assert.True(HaltungenAnsichtRegel.Bestimme(false, "tabelle").AlteAnsicht);
        Assert.True(HaltungenAnsichtRegel.Bestimme(true, "tabelle").Tabelle);
    }
}
