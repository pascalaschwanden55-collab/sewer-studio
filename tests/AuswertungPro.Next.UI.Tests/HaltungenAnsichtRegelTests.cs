using AuswertungPro.Next.UI.DataPage;

namespace AuswertungPro.Next.UI.Tests;

/// <summary>
/// Nova, Aufklapp-Liste (Task 2): Die Umschaltregel der Haltungsseite. Sie entscheidet, welche
/// der drei Ansichten sichtbar ist und was daran haengt (Spaltenchips, Eingabefelder,
/// Uebersicht, Abdocken).
/// </summary>
public sealed class HaltungenAnsichtRegelTests
{
    [Theory]
    [InlineData(null, "liste")]
    [InlineData("", "liste")]
    [InlineData("   ", "liste")]
    [InlineData("liste", "liste")]
    [InlineData("LISTE", "liste")]
    [InlineData("Tabelle", "tabelle")]
    [InlineData("  tabelle  ", "tabelle")]
    [InlineData("kachelansicht", "liste")]
    public void Nur_liste_und_tabelle_sind_gueltig(string? gespeichert, string erwartet)
        => Assert.Equal(erwartet, HaltungenAnsichtRegel.Normalisiere(gespeichert));

    [Fact]
    public void Ohne_Einstellung_ist_die_Aufklapp_Liste_der_Standard()
    {
        var sicht = HaltungenAnsichtRegel.Bestimme(novaAktiv: true, ansicht: null);

        Assert.True(sicht.Liste);
        Assert.False(sicht.Tabelle);
        Assert.False(sicht.AlteAnsicht);
    }

    [Fact]
    public void In_der_Liste_verschwinden_Spaltenchips_und_Eingabefelder_die_Uebersicht_bleibt()
    {
        var sicht = HaltungenAnsichtRegel.Bestimme(novaAktiv: true, ansicht: "liste");

        Assert.False(sicht.Spaltenchips);
        Assert.False(sicht.Eingabefelder);
        Assert.True(sicht.Uebersicht);
        // Abgedockt wird die Tabelle; in der Liste bleibt der Menuepunkt gesperrt.
        Assert.False(sicht.AbdockenMoeglich);
    }

    [Fact]
    public void In_der_Tabelle_gilt_alles_wie_bisher()
    {
        var sicht = HaltungenAnsichtRegel.Bestimme(novaAktiv: true, ansicht: "tabelle");

        Assert.False(sicht.Liste);
        Assert.True(sicht.Tabelle);
        Assert.False(sicht.AlteAnsicht);
        Assert.True(sicht.Spaltenchips);
        Assert.True(sicht.Eingabefelder);
        Assert.True(sicht.Uebersicht);
        Assert.True(sicht.AbdockenMoeglich);
    }

    [Theory]
    [InlineData("liste")]
    [InlineData("tabelle")]
    public void Ohne_Nova_Layout_gilt_immer_die_alte_Haltungsansicht(string ansicht)
    {
        var sicht = HaltungenAnsichtRegel.Bestimme(novaAktiv: false, ansicht);

        Assert.True(sicht.AlteAnsicht);
        Assert.False(sicht.Liste);
        Assert.False(sicht.Tabelle);
        // Die alte Ansicht bringt ihre eigenen Formularkarten mit: weder Uebersicht noch
        // Eingabefelder noch Spaltenchips gehoeren dort hin.
        Assert.False(sicht.Uebersicht);
        Assert.False(sicht.Eingabefelder);
        Assert.False(sicht.Spaltenchips);
    }

    [Fact]
    public void Die_gespeicherte_Ansicht_ueberlebt_einen_Ausflug_in_die_alte_Ansicht()
    {
        // Der Schalter der alten Ansicht loescht die Einstellung nicht — sie gilt wieder,
        // sobald das Nova-Layout zurueckkommt.
        Assert.True(HaltungenAnsichtRegel.Bestimme(false, "tabelle").AlteAnsicht);
        Assert.True(HaltungenAnsichtRegel.Bestimme(true, "tabelle").Tabelle);
    }
}
