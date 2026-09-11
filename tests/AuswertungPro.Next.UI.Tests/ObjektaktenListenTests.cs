using AuswertungPro.Next.Application.UseCases.Objektakten;
using AuswertungPro.Next.Domain.Models;
using AuswertungPro.Next.UI.ViewModels;

namespace AuswertungPro.Next.UI.Tests;

/// <summary>Die Aufklapplisten der Objektakte: was angelegt werden darf, sagt der Katalog.</summary>
public sealed class ObjektaktenListenTests
{
    private static (ObjektaktenBearbeitung Bearbeitung, ObjektakteViewModel Ansicht) Haltung()
    {
        var projekt = new Project();
        var haltung = new HaltungRecord();
        projekt.Data.Add(haltung);
        var bearbeitung = new ObjektaktenBearbeitung(projekt, haltung.Id, "haltung");
        return (bearbeitung, new ObjektakteViewModel(bearbeitung, new(), () => { }, () => true, () => { }));
    }

    private static (ObjektaktenBearbeitung Bearbeitung, ObjektakteViewModel Ansicht) Schacht()
    {
        var projekt = new Project();
        var schacht = new SchachtRecord();
        projekt.SchaechteData.Add(schacht);
        var bearbeitung = new ObjektaktenBearbeitung(projekt, schacht.Id, "schacht");
        return (bearbeitung, new ObjektakteViewModel(bearbeitung, new(), () => { }, () => true, () => { }));
    }

    [Fact]
    public void Der_Katalog_bestimmt_welche_Objektarten_angelegt_werden_duerfen()
    {
        var (haltung, _) = Haltung();
        var (schacht, _) = Schacht();

        // Deckel gehoeren zum Schacht - an der Haltung zeigt keine Liste darauf.
        Assert.False(haltung.DarfAnlegen("deckel"));
        Assert.True(schacht.DarfAnlegen("deckel"));

        // Beide fuehren Sanierungen, Unterhalt, Dichtheitspruefungen und Bauwerksteile.
        foreach (var art in new[] { "sanierung", "unterhalt", "dichtheitspruefung", "bauwerksteil", "massnahme" })
        {
            Assert.True(haltung.DarfAnlegen(art), $"Haltung sollte '{art}' anlegen duerfen.");
            Assert.True(schacht.DarfAnlegen(art), $"Schacht sollte '{art}' anlegen duerfen.");
        }

        // Inspektionen sind je Objekt eine eigene Tabelle im Kataster.
        Assert.True(haltung.DarfAnlegen("inspektion_haltung"));
        Assert.False(haltung.DarfAnlegen("inspektion_schacht"));
        Assert.True(schacht.DarfAnlegen("inspektion_schacht"));
        Assert.False(schacht.DarfAnlegen("inspektion_haltung"));

        // Nur lesende Listen legen nichts an, ebenso wenig eine erfundene Art.
        foreach (var art in new[] { "pumpe", "ueberlauf", "absperr_drossel", "einzugsgebiet", "haltung", "gibtsnicht" })
            Assert.False(schacht.DarfAnlegen(art), $"'{art}' darf am Schacht nicht angelegt werden.");
    }

    [Fact]
    public void Eine_unerlaubte_Objektart_wird_abgewiesen_und_legt_nichts_an()
    {
        var (bearbeitung, _) = Haltung();
        Assert.Throws<InvalidOperationException>(() => bearbeitung.Neu("deckel"));
        Assert.Throws<InvalidOperationException>(() => bearbeitung.Neu("pumpe"));
        Assert.Throws<InvalidOperationException>(() => bearbeitung.Neu("gibtsnicht"));
        Assert.Empty(bearbeitung.Projekt.Objektakten);
    }

    [Fact]
    public void Ein_neuer_Eintrag_erscheint_in_seiner_Liste_und_oeffnet_seine_Maske()
    {
        var (bearbeitung, ansicht) = Haltung();
        var liste = ansicht.Listen.Single(l => l.Titel == "Unterhaltsmassnahmen");
        Assert.True(liste.DarfAnlegen);
        Assert.Empty(liste.Zeilen);
        Assert.Equal("Noch keine Einträge.", liste.Hinweis);

        liste.NeuCommand.Execute(null);

        // Der neue Eintrag ist ausgewaehlt, also zeigt die Maske bereits seine Felder.
        Assert.Equal("unterhalt", ansicht.Auswahl.Art);
        Assert.All(ansicht.Gruppen.SelectMany(g => g.Felder), f => Assert.Equal("unterhalt", f.Feld.Art));

        // Zurueck auf die Haltung: die Zeile steht in ihrer Liste und laesst sich oeffnen.
        var neu = ansicht.Auswahl;
        ansicht.Auswahl = bearbeitung.Wurzel;
        var nachher = ansicht.Listen.Single(l => l.Titel == "Unterhaltsmassnahmen");
        var zeile = Assert.Single(nachher.Zeilen);
        Assert.True(zeile.Bearbeitbar);
        Assert.Equal(neu.Id, zeile.Akte!.Id);

        nachher.OeffnenCommand.Execute(zeile);
        Assert.Equal(neu.Id, ansicht.Auswahl.Id);
    }

    [Fact]
    public void Eine_nur_lesende_Liste_bietet_kein_Anlegen()
    {
        var (_, ansicht) = Schacht();
        foreach (var titel in new[] { "Pumpen", "Überläufe", "Absperr-/Drosselorgane", "Einläufe", "Einzugsgebiete SW" })
        {
            var liste = ansicht.Listen.Single(l => l.Titel == titel);
            Assert.True(liste.NurLesen, $"'{titel}' sollte nur lesend sein.");
            Assert.False(liste.DarfAnlegen);
            Assert.False(liste.NeuCommand.CanExecute(null));
            Assert.Contains("wird am zugehörigen Objekt gepflegt", liste.Hinweis);
        }
    }

    [Fact]
    public void Ohne_Schreibrecht_wird_nichts_angeboten()
    {
        var projekt = new Project();
        var haltung = new HaltungRecord();
        projekt.Data.Add(haltung);
        var bearbeitung = new ObjektaktenBearbeitung(projekt, haltung.Id, "haltung");
        var ansicht = new ObjektakteViewModel(bearbeitung, new(), () => { }, () => false, () => { });
        Assert.All(ansicht.Listen, l => Assert.False(l.DarfAnlegen));
    }

    [Fact]
    public void Die_Spalten_zeigen_die_Werte_des_Eintrags_und_keinen_technischen_Schluessel()
    {
        var (bearbeitung, ansicht) = Haltung();
        var liste = ansicht.Listen.Single(l => l.Titel == "Unterhaltsmassnahmen");

        // Der technische Schluessel des WebGIS hat kein Feld und erscheint nicht als Spalte.
        Assert.DoesNotContain("GlobalId", liste.Spaltentitel);
        Assert.Contains("Zeitpunkt", liste.Spaltentitel);

        var neu = bearbeitung.Neu("unterhalt");

        // Ein Feld, das keine Spalte ist, erscheint in der Zeile nicht.
        var bezeichnung = FieldCatalog.Objektfelder.Felder.First(f => f.Art == "unterhalt" && f.Label == "Bezeichnung");
        bearbeitung.Schreibe(neu, bezeichnung, "", "Spülung Frühling");
        ansicht.AktualisiereFelder();
        var ohne = Assert.Single(ansicht.Listen.Single(l => l.Titel == "Unterhaltsmassnahmen").Zeilen);
        Assert.Equal("(ohne Angaben)", ohne.Anzeige);

        // Ein Spaltenfeld dagegen schon.
        var ausfuehrender = FieldCatalog.Objektfelder.Felder.First(f => f.Art == "unterhalt" && f.Label == "Ausführender");
        bearbeitung.Schreibe(neu, ausfuehrender, "", "Muster AG");
        ansicht.AktualisiereFelder();
        var zeile = Assert.Single(ansicht.Listen.Single(l => l.Titel == "Unterhaltsmassnahmen").Zeilen);
        Assert.Contains("Muster AG", zeile.Anzeige);
        Assert.True(zeile.Bearbeitbar);
    }
}
