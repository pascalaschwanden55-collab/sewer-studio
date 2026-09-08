using System.IO;
using AuswertungPro.Next.UI.DataPage;
using AuswertungPro.Next.UI.Views.Windows;
using static AuswertungPro.Next.UI.Tests.TestRepoPaths;

namespace AuswertungPro.Next.UI.Tests;

/// <summary>
/// Nova, Aufklapp-Liste (Task 1): Die Themenbildung der Eingabefelder liegt jetzt als eigene
/// Regel im DataPage-Namespace. Eingabefelder-Schublade UND Aufklapp-Liste lesen sie — es gibt
/// nur eine Reihenfolge, einen Zaehler und eine Regel dafuer, welches Thema zugeklappt startet.
/// </summary>
public sealed class HaltungThemenGruppierungTests
{
    private static RecordDetailItem Item(string label) => new(label, "wert", _ => { });

    private static RecordDetailGroup Gruppe(string titel, params string[] labels)
        => new(titel, string.Empty, labels.Select(Item).ToList());

    [Fact]
    public void Bilde_behaelt_die_Reihenfolge_der_Gruppen()
    {
        var themen = HaltungThemenGruppierung.Bilde(
        [
            Gruppe("Stammdaten", "Baujahr", "Material"),
            Gruppe("Bewertung", "Zustandsklasse"),
            Gruppe("Weitere Angaben", "Z_Extra")
        ]);

        Assert.Equal(["Stammdaten", "Bewertung", "Weitere Angaben"], themen.Select(t => t.Title));
    }

    [Fact]
    public void Jedes_Thema_traegt_genau_seine_eigene_Gruppe_und_zaehlt_ihre_Felder()
    {
        var themen = HaltungThemenGruppierung.Bilde([Gruppe("Stammdaten", "Strasse", "Baujahr")]);

        var thema = Assert.Single(themen);
        Assert.Equal(2, thema.Anzahl);
        Assert.Equal("Stammdaten", Assert.Single(thema.EinzelGruppe).Title);
    }

    [Fact]
    public void Weitere_Angaben_startet_zugeklappt_andere_Themen_bleiben_offen()
    {
        var themen = HaltungThemenGruppierung.Bilde(
            [Gruppe("Stammdaten", "Baujahr"), Gruppe("Weitere Angaben", "Z_Extra")]);

        Assert.True(themen.Single(t => t.Title == "Stammdaten").IstStandardAufgeklappt);
        Assert.False(themen.Single(t => t.Title == "Weitere Angaben").IstStandardAufgeklappt);
    }

    [Fact]
    public void Ohne_Gruppen_gibt_es_keine_Themen()
        => Assert.Empty(HaltungThemenGruppierung.Bilde(null));

    [Fact]
    public void Ein_leeres_Thema_erscheint_nicht()
        => Assert.Empty(HaltungThemenGruppierung.Bilde([Gruppe("Leer")]));

    [Fact]
    public void Filtere_laesst_nur_passende_Beschriftungen_stehen()
    {
        var themen = HaltungThemenGruppierung.Filtere(
            [Gruppe("Stammdaten", "Baujahr", "Material"), Gruppe("Bewertung", "Zustandsklasse")], "bauj");

        var thema = Assert.Single(themen);
        Assert.Equal("Stammdaten", thema.Title);
        Assert.Equal("Baujahr", Assert.Single(thema.EinzelGruppe[0].Items).Label);
    }

    [Fact]
    public void Ohne_Suchtext_bleibt_jedes_Feld_stehen()
    {
        var themen = HaltungThemenGruppierung.Filtere(
            [Gruppe("Stammdaten", "Baujahr", "Material")], "   ");

        Assert.Equal(2, Assert.Single(themen).Anzahl);
    }

    /// <summary>
    /// Fix-Runde 1: Der Auf-/Zuklappzustand gehoert dem Thema, nicht dem Expander — sonst faende
    /// der Benutzer nach jedem Scrollen wieder die Vorgabe vor.
    /// </summary>
    [Fact]
    public void Der_Zustand_eines_Themas_startet_auf_der_Vorgabe_und_bleibt_dann_stehen()
    {
        var themen = HaltungThemenGruppierung.Bilde(
            [Gruppe("Stammdaten", "Baujahr"), Gruppe("Weitere Angaben", "Z_Extra")]);

        var stammdaten = themen.Single(t => t.Title == "Stammdaten");
        var weitere = themen.Single(t => t.Title == "Weitere Angaben");
        Assert.True(stammdaten.IstAufgeklappt);
        Assert.False(weitere.IstAufgeklappt);

        stammdaten.IstAufgeklappt = false;
        weitere.IstAufgeklappt = true;

        Assert.False(stammdaten.IstAufgeklappt);
        Assert.True(weitere.IstAufgeklappt);
        // Die Vorgabe selbst bleibt unveraendert; sie ist eine Regel, kein Zustand.
        Assert.True(stammdaten.IstStandardAufgeklappt);
        Assert.False(weitere.IstStandardAufgeklappt);
    }

    /// <summary>
    /// Waechter: Die Eingabefelder-Schublade darf keine zweite Kopie der Gruppierung fuehren.
    /// Beide Ansichten muessen dieselbe Reihenfolge und dieselben Zaehler zeigen.
    /// </summary>
    [Fact]
    public void Die_Eingabefelder_Schublade_verwendet_dieselbe_Gruppierung()
    {
        var code = File.ReadAllText(RepoFile(
            "src", "AuswertungPro.Next.UI", "Views", "Pages", "Haltungsansicht", "HaltungFelderDrawer.xaml.cs"));

        Assert.Contains("HaltungThemenGruppierung.", code, StringComparison.Ordinal);
        Assert.DoesNotContain("public sealed record ThemaAnzeige", code, StringComparison.Ordinal);
    }
}
