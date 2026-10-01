using System.Linq;

using AuswertungPro.Next.Application.Dossiers;

using Xunit;

namespace AuswertungPro.Next.Infrastructure.Tests.Dossiers;

/// <summary>
/// Welche Blätter ins Gesamt-PDF kommen.
///
/// Standard ist: alle. Wer nichts anfasst, bekommt das vollständige Dossier —
/// eine Auswahl, die man erst treffen muss, wäre eine Falle.
/// </summary>
public sealed class DossierPageSelectionTests
{
    [Fact]
    public void Am_Anfang_sind_alle_Blaetter_gewaehlt()
    {
        var auswahl = new DossierPageSelection(5);

        Assert.Empty(auswahl.Ausgeschlossen);
        Assert.Equal(5, auswahl.GewaehlteAnzahl);
        Assert.True(Enumerable.Range(1, 5).All(auswahl.IstGewaehlt));
    }

    [Fact]
    public void Ein_abgewaehltes_Blatt_erscheint_im_Ausschluss()
    {
        var auswahl = new DossierPageSelection(4);

        auswahl.Setze(2, gewaehlt: false);

        Assert.Equal([2], auswahl.Ausgeschlossen.OrderBy(nummer => nummer));
        Assert.False(auswahl.IstGewaehlt(2));
        Assert.Equal(3, auswahl.GewaehlteAnzahl);
    }

    [Fact]
    public void Wieder_anwaehlen_nimmt_es_zurueck()
    {
        var auswahl = new DossierPageSelection(3);

        auswahl.Setze(1, gewaehlt: false);
        auswahl.Setze(1, gewaehlt: true);

        Assert.Empty(auswahl.Ausgeschlossen);
    }

    [Fact]
    public void Keine_und_Alle_wirken_auf_den_ganzen_Stapel()
    {
        var auswahl = new DossierPageSelection(3);

        auswahl.Keine();
        Assert.Equal(0, auswahl.GewaehlteAnzahl);

        auswahl.Alle();
        Assert.Equal(3, auswahl.GewaehlteAnzahl);
        Assert.Empty(auswahl.Ausgeschlossen);
    }

    [Fact]
    public void Ohne_gewaehltes_Blatt_darf_nicht_erzeugt_werden()
    {
        // Ein PDF ohne Seiten waere kaputt; der Knopf bleibt gesperrt, statt
        // dass hinterher eine unbrauchbare Datei entsteht.
        var auswahl = new DossierPageSelection(2);

        Assert.True(auswahl.DarfErzeugen);

        auswahl.Keine();

        Assert.False(auswahl.DarfErzeugen);
    }

    [Fact]
    public void Eine_Nummer_ausserhalb_des_Stapels_wird_ignoriert()
    {
        var auswahl = new DossierPageSelection(2);

        auswahl.Setze(0, gewaehlt: false);
        auswahl.Setze(9, gewaehlt: false);

        Assert.Empty(auswahl.Ausgeschlossen);
    }

    [Fact]
    public void Der_Text_sagt_was_erzeugt_wird()
    {
        var auswahl = new DossierPageSelection(7);
        Assert.Equal("Alle 7 Blätter", auswahl.Beschreibung);

        auswahl.Setze(3, gewaehlt: false);
        Assert.Equal("6 von 7 Blättern", auswahl.Beschreibung);

        auswahl.Keine();
        Assert.Equal("Kein Blatt gewählt", auswahl.Beschreibung);
    }

    [Fact]
    public void Ein_Pflichtblatt_bleibt_auch_bei_Keine_gewaehlt()
    {
        var auswahl = new DossierPageSelection(3, new HashSet<int> { 2 });

        auswahl.Setze(2, gewaehlt: false);
        auswahl.Keine();

        Assert.True(auswahl.IstPflichtblatt(2));
        Assert.True(auswahl.IstGewaehlt(2));
        Assert.Equal([1, 3], auswahl.Ausgeschlossen.OrderBy(nummer => nummer));
        Assert.True(auswahl.DarfErzeugen);
    }
}
