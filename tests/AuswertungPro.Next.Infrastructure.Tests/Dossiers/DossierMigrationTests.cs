using System.Linq;

using AuswertungPro.Next.Application.Dossiers;
using AuswertungPro.Next.Domain.Models.Dossiers;

using Xunit;

namespace AuswertungPro.Next.Infrastructure.Tests.Dossiers;

public sealed class DossierDocumentMigrationTests
{
    [Fact]
    public void Uebernimmt_den_bisherigen_einzelnen_Eigentuemer_in_die_erste_Zeile()
    {
        var document = new DossierDocument { SchemaVersion = 1 };
        document.Dossiers.Add(new DossierDefinition
        {
            HouseNumbers = "3",
            ParcelNumbers = "170",
            OwnerName = "Martin Muster",
            ContactPhone = "079 858 53 74",
            ContactMail = "markus@example.ch",
            Occupancy = "Einfamilienhaus"
        });

        var result = DossierDocumentMigration.MigrateToCurrent(document);

        var row = Assert.Single(result.Dossiers[0].Owners);
        Assert.Equal("3", row.HouseNumber);
        Assert.Equal("170", row.ParcelNumber);
        Assert.Equal("Martin Muster", row.Name);
        Assert.Equal("079 858 53 74", row.Phone);
        Assert.Equal("markus@example.ch", row.Mail);
        Assert.Equal("Einfamilienhaus", row.Occupancy);
        // War fest auf 2 verdrahtet; mit der Anhebung auf Version 3 (Task 6)
        // stimmt das nicht mehr. Der Test prueft "aktuelle Version nach der
        // Umstellung", nicht die Ableitungsgrenze — deshalb dynamisch pruefen.
        Assert.Equal(DossierDocument.CurrentSchemaVersion, result.SchemaVersion);
    }

    [Fact]
    public void Nimmt_Eigentuemeradresse_und_Zustaendigkeit_mit_statt_sie_zu_verlieren()
    {
        var document = new DossierDocument { SchemaVersion = 1 };
        document.Dossiers.Add(new DossierDefinition
        {
            OwnerName = "Lubag AG",
            OwnerAddress = "Landenbergstrasse 34, 6005 Luzern",
            ContactName = "Sandro Sigrist"
        });

        var result = DossierDocumentMigration.MigrateToCurrent(document);

        var row = Assert.Single(result.Dossiers[0].Owners);
        Assert.Contains("Lubag AG", row.Name);
        Assert.Contains("Landenbergstrasse 34", row.Name);
        Assert.Contains("Zuständigkeit: Sandro Sigrist", row.Name);
    }

    [Fact]
    public void Ein_Dossier_ohne_Eigentuemerangaben_bekommt_keine_Leerzeile()
    {
        var document = new DossierDocument { SchemaVersion = 1 };
        document.Dossiers.Add(new DossierDefinition { Name = "Nur ein Name" });

        var result = DossierDocumentMigration.MigrateToCurrent(document);

        Assert.Empty(result.Dossiers[0].Owners);
    }

    [Theory]
    [InlineData(1, true)]
    [InlineData(2, false)]
    [InlineData(3, false)]
    [InlineData(4, false)]
    public void Die_Ableitung_gilt_nur_fuer_Dateien_aus_Version_1(int version, bool erwartet)
    {
        // Diese Zusicherung ist eine Falle fuer spaeter, nicht fuer heute:
        // Solange die aktuelle Formatversion 2 ist, verhaelt sich
        // "kleiner als 2" gleich wie "kleiner als die aktuelle Version".
        // Steigt die Version auf 3, wuerde die zweite Fassung jede
        // Version-2-Datei erneut ableiten und geloeschte Eigentuemerzeilen
        // zurueckholen. Dann faellt dieser Test um — genau dort, wo es
        // gefunden werden muss.
        Assert.Equal(erwartet, DossierDocumentMigration.NeedsOwnerDerivation(version));
    }

    [Fact]
    public void Eine_Datei_der_aktuellen_Version_bekommt_keine_Zeile_nachgetragen()
    {
        var document = new DossierDocument { SchemaVersion = 2 };
        document.Dossiers.Add(new DossierDefinition
        {
            // Der Eigentuemer speist weiterhin das Deckblatt; die Zeile hat
            // Pascal bewusst geloescht und sie darf nicht wiederkommen.
            OwnerName = "Martin Muster",
            OwnerAddress = "Musterstrasse 1, 6472 Erstfeld"
        });

        var result = DossierDocumentMigration.MigrateToCurrent(document);

        Assert.Empty(result.Dossiers[0].Owners);
        Assert.Equal("Martin Muster", result.Dossiers[0].OwnerName);
    }

    [Fact]
    public void Bereits_vorhandene_Zeilen_werden_nicht_angetastet()
    {
        var document = new DossierDocument { SchemaVersion = 2 };
        var dossier = new DossierDefinition { OwnerName = "Alt" };
        dossier.Owners.Add(new DossierOwnerRow { Name = "Neu" });
        document.Dossiers.Add(dossier);

        var result = DossierDocumentMigration.MigrateToCurrent(document);

        var row = Assert.Single(result.Dossiers[0].Owners);
        Assert.Equal("Neu", row.Name);
    }

    [Fact]
    public void Eine_geloeschte_Zeile_wird_bei_bereits_aktueller_Version_nicht_neu_erzeugt()
    {
        // Genau das Fehlerszenario aus dem Fix-Brief: Pascal loescht die
        // automatisch erzeugte Zeile wieder. Beim naechsten Laden (hier: beim
        // zweiten Migrationslauf) darf sie nicht zurueckkommen, nur weil
        // OwnerName weiterhin im Datensatz steht.
        var document = new DossierDocument { SchemaVersion = 1 };
        document.Dossiers.Add(new DossierDefinition
        {
            OwnerName = "Martin Muster",
            ContactPhone = "079 858 53 74"
        });

        var afterFirstLoad = DossierDocumentMigration.MigrateToCurrent(document);
        Assert.Single(afterFirstLoad.Dossiers[0].Owners);

        // Pascal loescht die Zeile wieder, OwnerName bleibt (speist das Deckblatt).
        afterFirstLoad.Dossiers[0].Owners.Clear();

        var afterSecondLoad = DossierDocumentMigration.MigrateToCurrent(afterFirstLoad);

        Assert.Empty(afterSecondLoad.Dossiers[0].Owners);
    }

    [Fact]
    public void Version_2_bleibt_bei_jeder_Erhoehung_ohne_neue_Eigentuemerzeile()
    {
        // Die Falle aus der Pruefung: mit "kleiner als die aktuelle Version"
        // waere eine Version-2-Datei wieder Altbestand und die geloeschte Zeile
        // kaeme zurueck.
        var document = new DossierDocument { SchemaVersion = 2 };
        document.Dossiers.Add(new DossierDefinition { OwnerName = "Martin Muster" });

        var result = DossierDocumentMigration.MigrateToCurrent(document);

        Assert.Empty(result.Dossiers[0].Owners);
        // Bewusst gegen die Konstante: der Test soll bei der naechsten
        // Versionserhoehung die ABLEITUNG pruefen, nicht die Zahl.
        Assert.Equal(DossierDocument.CurrentSchemaVersion, result.SchemaVersion);
    }
}
