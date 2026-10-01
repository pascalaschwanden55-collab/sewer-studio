using System;
using System.Linq;

using AuswertungPro.Next.Application.Dossiers.Preview;
using AuswertungPro.Next.Domain.Models.Dossiers;
using AuswertungPro.Next.Infrastructure.Dossiers;
using AuswertungPro.Next.Infrastructure.Dossiers.Preview;

using Xunit;

namespace AuswertungPro.Next.Infrastructure.Tests.Dossiers;

public sealed class DossierPreviewFieldCatalogTests
{
    private static DossierPreviewPage Seite(params string[] felder)
        => new(
            1, "Deckblatt",
            new DossierPreviewGeometry(794, 1123, DossierPreviewEdges.All(76)),
            System.Array.Empty<DossierPreviewBlock>(),
            felder);

    private static (DossierAreaSettings Area, DossierDefinition Dossier) Stand()
        => (new DossierAreaSettings { AreaTitle = "Sanierung Musterweg" },
            new DossierDefinition { ParcelNumbers = "30", Town = "Musterdorf" });

    [Fact]
    public void Ein_Feld_liest_und_schreibt_die_echte_Angabe()
    {
        var (area, dossier) = Stand();
        var felder = DossierPreviewFieldCatalog.Build(area, dossier);

        var titel = felder.Single(f => f.Key == "Gebietstitel");
        Assert.Equal("Sanierung Musterweg", titel.Read());

        titel.Write!("Sanierung Unterdorf");
        Assert.Equal("Sanierung Unterdorf", area.AreaTitle);
    }

    [Fact]
    public void Eine_sichtbare_Stelle_hat_genau_ein_Eingabefeld()
    {
        // "Musterweg 51" ist EINE Zeile im Dokument und wird in der
        // 1:1-Vorschau deshalb auch gemeinsam bearbeitet.
        var (area, dossier) = Stand();
        var felder = DossierPreviewFieldCatalog.Build(area, dossier);

        var adresse = Assert.Single(felder.Where(f => f.Key == "Adresse_Zeile"));

        Assert.Equal("Strasse und Haus-Nr.", adresse.Label);
        Assert.True(adresse.CanReset);
    }

    [Fact]
    public void Eine_berechnete_Stelle_zeigt_den_berechneten_Wert_und_sagt_woher()
    {
        var (area, dossier) = Stand();
        var felder = DossierPreviewFieldCatalog.Build(
            area, dossier, key => key == "Eigentuemer_Block" ? "Kurt Beispiel" : "");

        var block = felder.Single(f => f.Key == "Eigentuemer_Block");

        Assert.Equal("Kurt Beispiel", block.Read());
        Assert.False(block.Overridden);
        Assert.NotEqual("", block.Hint);
    }

    [Fact]
    public void Auch_eine_berechnete_Stelle_laesst_sich_von_Hand_setzen()
    {
        // Jedes Element muss aenderbar sein — auch das Erstellungsdatum oder
        // der Eigentuemerblock.
        var (area, dossier) = Stand();
        var felder = DossierPreviewFieldCatalog.Build(
            area, dossier, _ => "berechnet");

        var block = felder.Single(f => f.Key == "Eigentuemer_Block");

        Assert.NotNull(block.Write);
        block.Write!("Von Hand");

        Assert.Equal("Von Hand", block.Read());
        Assert.True(block.Overridden);
        Assert.Equal("Von Hand", dossier.FieldOverrides["Eigentuemer_Block"]);
    }

    [Fact]
    public void Eine_von_Hand_gesetzte_Stelle_laesst_sich_zuruecksetzen()
    {
        // Ohne Rueckweg waere jede Handeingabe eine Einbahnstrasse.
        var (area, dossier) = Stand();
        var felder = DossierPreviewFieldCatalog.Build(area, dossier, _ => "berechnet");

        var datum = felder.Single(f => f.Key == "Datum_Lang");
        datum.Write!("im Frühjahr");

        Assert.True(datum.Overridden);
        Assert.True(datum.CanReset);

        datum.Reset!();

        Assert.False(datum.Overridden);
        Assert.Equal("berechnet", datum.Read());
    }

    [Fact]
    public void Eine_leer_gesetzte_Stelle_bleibt_leer_statt_zu_rechnen()
    {
        var (area, dossier) = Stand();
        var felder = DossierPreviewFieldCatalog.Build(area, dossier, _ => "berechnet");

        var datum = felder.Single(f => f.Key == "Datum");
        datum.Write!("");

        Assert.Equal("", datum.Read());
        Assert.True(datum.Overridden);
    }

    [Fact]
    public void Eine_Seite_zeigt_nur_ihre_eigenen_Felder()
    {
        var (area, dossier) = Stand();
        var alle = DossierPreviewFieldCatalog.Build(area, dossier);

        var seite = Seite("Gebietstitel", "Revision");

        var felder = DossierPreviewFieldCatalog.ForPage(alle, seite);

        Assert.Equal(new[] { "Gebietstitel", "Revision" }, felder.Select(f => f.Key));
    }

    [Fact]
    public void Mehrere_Kapitel_eines_Ausgabeblatts_zeigen_die_Fusszeile_nur_einmal()
    {
        // Ein Word-Blatt kann mehrere Kapitel enthalten. Die Fusszeile gehoert
        // zum Blatt und darf rechts deshalb nicht in jedem Kapitel erneut als
        // dasselbe Eingabefeld erscheinen.
        var (area, dossier) = Stand();
        var alle = DossierPreviewFieldCatalog.Build(area, dossier);
        var seiten = new[]
        {
            Seite("Gebietstitel", "Fusszeile"),
            Seite("Revision", "Fusszeile")
        };

        var gruppen = DossierPreviewFieldCatalog.ForPages(alle, seiten, dossier);

        Assert.Equal(2, gruppen.Count);
        Assert.Contains(gruppen[0].Felder, feld => feld.Key == "Gebietstitel");
        Assert.Contains(gruppen[0].Felder, feld => feld.Key == "Fusszeile");
        Assert.Contains(gruppen[1].Felder, feld => feld.Key == "Revision");
        Assert.DoesNotContain(gruppen[1].Felder, feld => feld.Key == "Fusszeile");
        Assert.Single(gruppen.SelectMany(gruppe => gruppe.Felder)
            .Where(feld => feld.Key == "Fusszeile"));
    }

    [Fact]
    public void Ein_unbekannter_Platzhalter_wird_sichtbar_als_berechnet_gemeldet()
    {
        // Sonst faende der Benutzer eine Stelle im Blatt, zu der es keine
        // Eingabe gibt — und wuesste nicht, ob er sie uebersehen hat.
        var (area, dossier) = Stand();
        var alle = DossierPreviewFieldCatalog.Build(area, dossier);

        var seite = Seite("Voellig_Neues_Feld");

        var feld = Assert.Single(DossierPreviewFieldCatalog.ForPage(alle, seite));

        Assert.Equal("Voellig_Neues_Feld", feld.Key);
        Assert.Equal(DossierPreviewFieldKind.Derived, feld.Kind);
        Assert.Null(feld.Write);
    }

    [Fact]
    public void Die_Rueckmeldefrist_des_Dossiers_gewinnt_ueber_die_des_Gebiets()
    {
        var (area, dossier) = Stand();
        area.ResponseDeadline = "Ende Mai";
        dossier.ResponseDeadlineOverride = "Ende Juni";

        var feld = DossierPreviewFieldCatalog.Build(area, dossier)
            .Single(f => f.Key == "Rueckmeldung");

        Assert.Equal("Ende Juni", feld.Read());

        // Leeren heisst: es gilt wieder das Gebiet.
        feld.Write!("");
        Assert.Null(dossier.ResponseDeadlineOverride);
        Assert.Equal("Ende Mai", feld.Read());
    }

    [Fact]
    public void Jede_Zeilenliste_der_Vorlage_hat_einen_Eintrag()
    {
        var (area, dossier) = Stand();
        var felder = DossierPreviewFieldCatalog.Build(area, dossier);

        foreach (var key in new[]
                 {
                     "Themen", "Eigentuemer", "Aenderungen", "Verzeichnis_Beilagen"
                 })
        {
            var feld = felder.Single(f => f.Key == key);
            Assert.Equal(DossierPreviewFieldKind.Rows, feld.Kind);
        }
    }

    [Fact]
    public void Jeder_Platzhalter_der_echten_Vorlage_hat_einen_bearbeitbaren_Weg()
    {
        var root = new AuswertungPro.Next.Infrastructure.Backup.RepositoryRootFileLocator()
            .Locate(AppContext.BaseDirectory);
        Assert.NotNull(root);
        var path = System.IO.Path.Combine(
            root!, "Export_Vorlage", DossierWordTemplate.TemplateFileName);
        Assert.True(System.IO.File.Exists(path), $"Dossiervorlage fehlt: {path}");

        var document = DossierPreviewBuilder.Build(path);
        var (area, dossier) = Stand();
        var alle = DossierPreviewFieldCatalog.Build(area, dossier, _ => "berechnet");

        foreach (var page in document.Pages)
        {
            var pageFields = DossierPreviewFieldCatalog.ForPage(
                alle, page, dossier, _ => "berechnet");

            foreach (var key in page.FieldKeys)
            {
                var passend = pageFields.Where(f => f.Key == key).ToList();
                Assert.NotEmpty(passend);
                Assert.All(passend, field =>
                    Assert.True(field.Write is not null
                        || field.Kind is DossierPreviewFieldKind.Rows,
                        $"Feld {key} auf Seite {page.Number} ist nicht bearbeitbar."));
            }
        }
    }

    [Fact]
    public void Die_Fusszeile_ist_auf_jeder_Vorlagenseite_bearbeitbar()
    {
        var root = new AuswertungPro.Next.Infrastructure.Backup.RepositoryRootFileLocator()
            .Locate(AppContext.BaseDirectory);
        Assert.NotNull(root);
        var path = System.IO.Path.Combine(
            root!, "Export_Vorlage", DossierWordTemplate.TemplateFileName);
        var document = DossierPreviewBuilder.Build(path);
        var (area, dossier) = Stand();
        var alle = DossierPreviewFieldCatalog.Build(area, dossier);

        Assert.NotEmpty(document.Pages);
        foreach (var page in document.Pages)
        {
            Assert.Contains("Fusszeile", page.FieldKeys);
            var feld = Assert.Single(DossierPreviewFieldCatalog.ForPage(
                alle, page, dossier).Where(f => f.Key == "Fusszeile"));
            Assert.NotNull(feld.Write);
        }
    }
}
