using System.Collections.Generic;
using System.Linq;

using AuswertungPro.Next.Application.Dossiers;
using AuswertungPro.Next.Domain.Models.Dossiers;

using Xunit;

namespace AuswertungPro.Next.Infrastructure.Tests.Dossiers;

public sealed class DossierTopicResolverTests
{
    private static DossierAreaSettings Gebiet(params (string Titel, string Text)[] themen)
        => new()
        {
            Topics = themen
                .Select(t => new DossierTopicRow { Title = t.Titel, Text = t.Text })
                .ToList()
        };

    private static DossierDefinition Dossier(params (string Titel, string Text)[] themen)
        => new()
        {
            Topics = themen
                .Select(t => new DossierTopicRow { Title = t.Titel, Text = t.Text })
                .ToList()
        };

    [Fact]
    public void Ohne_eigene_Themen_gilt_die_Gebietsliste_unveraendert()
    {
        var themen = DossierTopicResolver.Resolve(
            Gebiet(("Unternehmer", "Implenia"), ("Bemerkungen", "")),
            Dossier());

        Assert.Equal(new[] { "Unternehmer", "Bemerkungen" }, themen.Select(t => t.Title));
        Assert.Equal("Implenia", themen[0].Text);
    }

    [Fact]
    public void Ein_gleichnamiges_Dossierthema_ersetzt_nur_den_Text()
    {
        var themen = DossierTopicResolver.Resolve(
            Gebiet(("Unternehmer", "Implenia"), ("Bemerkungen", "Standard")),
            Dossier(("Bemerkungen", "Hier ist alles anders")));

        Assert.Equal(new[] { "Unternehmer", "Bemerkungen" }, themen.Select(t => t.Title));
        Assert.Equal("Implenia", themen[0].Text);
        Assert.Equal("Hier ist alles anders", themen[1].Text);
    }

    [Fact]
    public void Eigene_Themen_ohne_Vorbild_kommen_hinten_dran()
    {
        var themen = DossierTopicResolver.Resolve(
            Gebiet(("Unternehmer", "Implenia")),
            Dossier(("Schäden Pz. 30", "Leitung undicht")));

        Assert.Equal(new[] { "Unternehmer", "Schäden Pz. 30" }, themen.Select(t => t.Title));
    }

    [Fact]
    public void Ein_leerer_Titel_erzeugt_keine_Zeile()
    {
        var themen = DossierTopicResolver.Resolve(
            Gebiet(("Unternehmer", "Implenia"), ("   ", "verwaister Text")),
            Dossier());

        Assert.Single(themen);
    }

    [Fact]
    public void Zwei_gleichnamige_Gebietsthemen_teilen_sich_nicht_denselben_Ersatz()
    {
        // Sonst stuende derselbe Dossiertext zweimal da und der zweite
        // Standardtext waere spurlos verschwunden.
        var themen = DossierTopicResolver.Resolve(
            Gebiet(("Bemerkungen", "erster"), ("Bemerkungen", "zweiter")),
            Dossier(("Bemerkungen", "eigener")));

        Assert.Equal(2, themen.Count);
        Assert.Equal("eigener", themen[0].Text);
        Assert.Equal("zweiter", themen[1].Text);
    }

    [Fact]
    public void Ohne_jede_Angabe_bleibt_die_Liste_leer_statt_zu_werfen()
    {
        Assert.Empty(DossierTopicResolver.Resolve(null, null));
    }
}

public sealed class DossierTopicMigrationTests
{
    [Fact]
    public void Altdokumente_erhalten_die_Standardthemen_mit_ihren_bisherigen_Texten()
    {
        var document = new DossierDocument
        {
            SchemaVersion = 3,
            Area = new DossierAreaSettings
            {
                ExecutionDate = "Ab Mai 2026",
                Contractor = "Musterbau AG",
                HouseConnectionText = "Erklärung zum Hausanschluss"
            }
        };

        DossierDocumentMigration.MigrateToCurrent(document);

        var titel = document.Area.Topics.Select(t => t.Title).ToList();
        Assert.Equal(DossierDocumentMigration.DefaultTopicTitles.First(), titel.First());
        Assert.Contains("Unternehmer", titel);

        Assert.Equal("Ab Mai 2026",
            document.Area.Topics.Single(t => t.Title == "Ausführungstermin").Text);
        Assert.Equal("Musterbau AG",
            document.Area.Topics.Single(t => t.Title == "Unternehmer").Text);

        // Der Erklaertext des alten Aufbaus darf nicht verloren gehen.
        Assert.Equal("Erklärung zum Hausanschluss",
            document.Area.Topics.Single(t => t.Title == "Hausanschluss Abwasser").Text);
    }

    [Fact]
    public void Gefuellte_Dossierfelder_werden_zu_eigenen_Themen()
    {
        var document = new DossierDocument
        {
            SchemaVersion = 3,
            Dossiers =
            {
                new DossierDefinition
                {
                    ConstructionProcess = "Inliner",
                    Remarks = "Versicherung klären",
                    Attachments = ""
                }
            }
        };

        DossierDocumentMigration.MigrateToCurrent(document);

        var themen = document.Dossiers[0].Topics;
        Assert.Equal(new[] { "Bauvorgang", "Bemerkungen" }, themen.Select(t => t.Title));
        Assert.DoesNotContain(themen, t => t.Title == "Beilagen");
    }

    [Fact]
    public void Ein_geloeschtes_Thema_kehrt_beim_naechsten_Laden_nicht_zurueck()
    {
        // Dieselbe Falle wie bei den Eigentuemerzeilen: die Ableitung haengt an
        // einer FESTEN Version, nicht an "kleiner als die aktuelle".
        var document = new DossierDocument
        {
            SchemaVersion = DossierDocument.CurrentSchemaVersion,
            Area = new DossierAreaSettings
            {
                ExecutionDate = "Ab Mai 2026",
                Topics = { new DossierTopicRow { Title = "Unternehmer", Text = "Musterbau AG" } }
            }
        };

        DossierDocumentMigration.MigrateToCurrent(document);

        Assert.Single(document.Area.Topics);
        Assert.Equal("Unternehmer", document.Area.Topics[0].Title);
    }

    [Fact]
    public void Eine_leere_Themenliste_einer_aktuellen_Datei_bleibt_leer()
    {
        var document = new DossierDocument
        {
            SchemaVersion = DossierDocument.CurrentSchemaVersion,
            Area = new DossierAreaSettings { ExecutionDate = "Ab Mai 2026" }
        };

        DossierDocumentMigration.MigrateToCurrent(document);

        Assert.Empty(document.Area.Topics);
    }
}
