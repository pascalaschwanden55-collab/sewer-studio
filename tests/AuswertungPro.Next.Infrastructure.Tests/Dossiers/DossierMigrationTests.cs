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
        Assert.Equal(2, result.SchemaVersion);
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
}
