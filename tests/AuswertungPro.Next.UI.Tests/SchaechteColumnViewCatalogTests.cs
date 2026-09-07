using AuswertungPro.Next.Domain.Models;
using AuswertungPro.Next.UI.DataPage;
using Xunit;

namespace AuswertungPro.Next.UI.Tests;

public sealed class SchaechteColumnViewCatalogTests
{
    [Fact]
    public void Fuenf_Ansichten_mit_der_Schachtnummer_in_jeder()
    {
        Assert.Equal(new[] { "kompakt", "zustand", "sanierung", "medien", "alle" }, SchaechteColumnViewCatalog.Views.Select(v => v.Key).ToArray());
        foreach (var v in SchaechteColumnViewCatalog.Views.Where(v => v.Felder is not null))
            Assert.Contains("Schachtnummer", v.Felder!);
        Assert.Equal(9, SchaechteColumnViewCatalog.Resolve("kompakt").Felder!.Count);
    }

    [Fact]
    public void Unbekannter_Schluessel_faellt_auf_Alle_zurueck()
        => Assert.Equal("alle", SchaechteColumnViewCatalog.Resolve("gibtsnicht").Key);

    /// <summary>
    /// Nova-Fixwelle F2: Schachtspalten heissen nach der Kopfzeile der Excel-Vorlage
    /// ("Eigentümer" mit Umlaut), waehrend Import und Katalog "Eigentuemer" schreiben. Ein reiner
    /// Ordinalvergleich fand die Spalte nicht und blendete sie in der Ansicht "Sanierung und
    /// Kosten" still aus.
    /// </summary>
    [Theory]
    [InlineData("Eigentümer")]
    [InlineData("Eigentuemer")]
    [InlineData("EIGENTUEMER")]
    public void Die_Eigentuemer_Spalte_wird_in_jeder_Schreibweise_gefunden(string spalte)
        => Assert.True(SchaechteColumnViewCatalog.Resolve("sanierung").Enthaelt(spalte, SchachtFeldnamen.Falte));

    [Fact]
    public void Eine_fremde_Spalte_gehoert_nicht_zur_Ansicht()
        => Assert.False(SchaechteColumnViewCatalog.Resolve("sanierung").Enthaelt("Baujahr", SchachtFeldnamen.Falte));

    /// <summary>
    /// Der Chip-Zaehler darf keine Spalte versprechen, die es in diesem Projekt gar nicht gibt.
    /// </summary>
    [Fact]
    public void Der_Zaehler_zaehlt_nur_wirklich_vorhandene_Spalten()
    {
        var vorhanden = new[] { "Schachtnummer", "Eigentümer", "Kosten" };
        var ansicht = SchaechteColumnViewCatalog.Resolve("sanierung");

        Assert.Equal(3, ansicht.Anzahl(vorhanden, SchachtFeldnamen.Falte));
        Assert.Equal(7, ansicht.Anzahl(0));
    }

    [Fact]
    public void Alle_Spalten_zaehlt_den_ganzen_Bestand()
        => Assert.Equal(4, SchaechteColumnViewCatalog.Resolve("alle").Anzahl(new[] { "a", "b", "c", "d" }));

    /// <summary>
    /// Nova-Etappe 2b, Task 1: dieselbe NR-Regel gilt fuer die Schachtliste. Keine benannte
    /// Ansicht fuehrt "NR"; nur "Alle Spalten" zeigt sie.
    /// </summary>
    [Fact]
    public void Kompakt_zeigt_keine_NR_Spalte()
    {
        foreach (var v in SchaechteColumnViewCatalog.Views.Where(v => v.Key != "alle"))
            Assert.False(v.Enthaelt("NR"), $"{v.Key} zeigt NR");

        Assert.True(SchaechteColumnViewCatalog.Resolve("alle").Enthaelt("NR"));
    }

    /// <summary>
    /// Nova-Etappe 2b, Task 6: In "Kompakt" steht das Protokoll als Knopf (virtuelle Spalte),
    /// nicht als roher Dateipfad — dort zaehlt der Ueberblick.
    /// </summary>
    [Fact]
    public void In_Kompakt_ersetzt_der_Knopf_den_Protokollpfad()
    {
        var ansicht = SchaechteColumnViewCatalog.Resolve("kompakt");
        Assert.Contains(NovaStatusSpalten.Protokoll, ansicht.Felder!);
        Assert.DoesNotContain(FieldKeys.PdfPath, ansicht.Felder!);
    }

    /// <summary>
    /// Nova-Fixwelle 2b (F5, Ruling): In "Dokumente und Medien" stehen Knopf UND Pfad. Dort
    /// geht es um genau diese Dateien; ein falscher Pfad muss ohne Wechsel nach
    /// "Alle Spalten" zu korrigieren sein.
    /// </summary>
    [Fact]
    public void Dokumente_und_Medien_zeigt_Knopf_und_bearbeitbaren_Pfad()
    {
        var ansicht = SchaechteColumnViewCatalog.Resolve("medien");
        Assert.Contains(NovaStatusSpalten.Protokoll, ansicht.Felder!);
        Assert.Contains(FieldKeys.PdfPath, ansicht.Felder!);
        Assert.Contains(FieldKeys.PdfEigen, ansicht.Felder!);
    }

    /// <summary>"Alle Spalten" fuehrt keine eigene Liste und zeigt deshalb auch den Pfad.</summary>
    [Fact]
    public void Alle_Spalten_zeigt_den_Protokollpfad_weiterhin()
        => Assert.True(SchaechteColumnViewCatalog.Resolve("alle").Enthaelt(FieldKeys.PdfPath, SchachtFeldnamen.Falte));
}
