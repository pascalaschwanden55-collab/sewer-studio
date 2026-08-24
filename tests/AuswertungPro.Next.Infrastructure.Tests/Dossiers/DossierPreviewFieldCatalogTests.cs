using System.Linq;

using AuswertungPro.Next.Application.Dossiers.Preview;
using AuswertungPro.Next.Domain.Models.Dossiers;

using Xunit;

namespace AuswertungPro.Next.Infrastructure.Tests.Dossiers;

public sealed class DossierPreviewFieldCatalogTests
{
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
    public void Zwei_Eingaben_duerfen_sich_dieselbe_Stelle_teilen()
    {
        // "Musterweg 51" ist EINE Zeile im Dokument, aber zwei Angaben.
        var (area, dossier) = Stand();
        var felder = DossierPreviewFieldCatalog.Build(area, dossier);

        var adresse = felder.Where(f => f.Key == "Adresse_Zeile").ToList();

        Assert.Equal(2, adresse.Count);
        Assert.Equal(new[] { "Strasse", "Haus-Nr." }, adresse.Select(f => f.Label));
    }

    [Fact]
    public void Berechnete_Stellen_sind_nicht_beschreibbar_und_sagen_warum()
    {
        var (area, dossier) = Stand();
        var felder = DossierPreviewFieldCatalog.Build(area, dossier);

        var block = felder.Single(f => f.Key == "Eigentuemer_Block");

        Assert.Equal(DossierPreviewFieldKind.Derived, block.Kind);
        Assert.Null(block.Write);
        Assert.NotEqual("", block.Hint);
    }

    [Fact]
    public void Eine_Seite_zeigt_nur_ihre_eigenen_Felder()
    {
        var (area, dossier) = Stand();
        var alle = DossierPreviewFieldCatalog.Build(area, dossier);

        var seite = new DossierPreviewPage(
            1, "Deckblatt",
            System.Array.Empty<DossierPreviewBlock>(),
            new[] { "Gebietstitel", "Revision" });

        var felder = DossierPreviewFieldCatalog.ForPage(alle, seite);

        Assert.Equal(new[] { "Gebietstitel", "Revision" }, felder.Select(f => f.Key));
    }

    [Fact]
    public void Ein_unbekannter_Platzhalter_wird_sichtbar_als_berechnet_gemeldet()
    {
        // Sonst faende der Benutzer eine Stelle im Blatt, zu der es keine
        // Eingabe gibt — und wuesste nicht, ob er sie uebersehen hat.
        var (area, dossier) = Stand();
        var alle = DossierPreviewFieldCatalog.Build(area, dossier);

        var seite = new DossierPreviewPage(
            1, "Deckblatt",
            System.Array.Empty<DossierPreviewBlock>(),
            new[] { "Voellig_Neues_Feld" });

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

        foreach (var key in new[] { "Themen", "Eigentuemer", "Aenderungen" })
        {
            var feld = felder.Single(f => f.Key == key);
            Assert.Equal(DossierPreviewFieldKind.Rows, feld.Kind);
        }
    }
}
