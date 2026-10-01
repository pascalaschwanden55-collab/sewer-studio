using System.Collections.Generic;
using System.Linq;

using AuswertungPro.Next.Application.Dossiers;
using AuswertungPro.Next.Domain.Models.Dossiers;
using AuswertungPro.Next.Infrastructure.Dossiers;

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

    [Fact]
    public void Ein_Thementitel_kann_nur_fuer_dieses_Dossier_geaendert_und_formatiert_werden()
    {
        var area = Gebiet(("Unternehmer", "Implenia"));
        var dossier = Dossier();

        DossierTopicTitleEditing.Set(
            dossier,
            "Unternehmer",
            "Ausführende Firma",
            [new DossierTextStyleRange
            {
                Start = 0,
                Length = 11,
                ColorHex = "C00000",
                Bold = true
            }]);

        var resolved = Assert.Single(DossierTopicResolver.Resolve(area, dossier));
        Assert.Equal("Unternehmer", resolved.SourceTitle);
        Assert.Equal("Ausführende Firma", resolved.Title);
        Assert.Equal("Unternehmer", area.Topics[0].Title);

        var row = Assert.Single(DossierWordTemplateExportService.BuildTopicRows(area, dossier));
        Assert.Equal("Ausführende Firma", row["Thema"]);
        var style = Assert.Single(DossierTopicTextFormatting.Decode(
            row["Thema" + DossierTopicTextFormatting.StyleRangesSuffix]));
        Assert.Equal("C00000", style.ColorHex);
        Assert.True(style.Bold);
    }

    [Fact]
    public void Zuruecksetzen_stellt_den_urspruenglichen_Thementitel_wieder_her()
    {
        var dossier = Dossier();
        DossierTopicTitleEditing.Set(dossier, "Schäden", "Festgestellte Schäden", []);

        DossierTopicTitleEditing.Reset(dossier, "Schäden");

        Assert.Equal("Schäden", DossierTopicTitleEditing.DisplayTitle(dossier, "Schäden"));
        Assert.False(DossierTopicTitleEditing.IsOverridden(dossier, "Schäden"));
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

    /// <summary>
    /// Der Benutzer darf im Gebietsfenster alle Themen loeschen. Diese Entscheidung
    /// muss halten: Wuerde die Migration die Standardliste beim naechsten Laden
    /// erneut einsetzen, waere sie ueberhaupt nicht speicherbar. Unterschieden wird
    /// ueber <see cref="DossierAreaSettings.TopicsInitialized"/> - "noch nie
    /// eingerichtet" gegen "bewusst geleert".
    /// </summary>
    [Fact]
    public void Eine_bewusst_geleerte_Themenliste_bleibt_leer()
    {
        var document = new DossierDocument
        {
            SchemaVersion = DossierDocument.CurrentSchemaVersion,
            Area = new DossierAreaSettings { TopicsInitialized = true }
        };

        DossierDocumentMigration.MigrateToCurrent(document);

        Assert.Empty(document.Area.Topics);
    }

    /// <summary>
    /// Beim ersten Einrichten wird die Liste befuellt UND als eingerichtet
    /// vermerkt - sonst greift die Regel bei jedem Laden erneut.
    /// </summary>
    [Fact]
    public void Die_erste_Befuellung_merkt_sich_dass_eingerichtet_wurde()
    {
        var document = new DossierDocument
        {
            SchemaVersion = DossierDocument.CurrentSchemaVersion,
            Area = new DossierAreaSettings()
        };

        DossierDocumentMigration.MigrateToCurrent(document);

        Assert.NotEmpty(document.Area.Topics);
        Assert.True(document.Area.TopicsInitialized);
    }

    [Fact]
    public void Eine_leere_Themenliste_bekommt_die_Standardliste_ohne_Altfelder()
    {
        // Geaenderte Regel: Ein Gebiet ganz OHNE Themen erzeugte im fertigen Dossier
        // eine leere Tabelle „Informationen Sanierung" (real: Projekt Feldliweg).
        // Es bekommt jetzt dieselbe Standardliste, die das Gebietsfenster schon
        // immer anbietet.
        //
        // Die urspruengliche Absicht dieses Tests bleibt geschuetzt: die ABLEITUNG
        // aus den Altfeldern laeuft dabei NICHT erneut. „Ab Mai 2026" darf nicht
        // wieder auftauchen - sonst kaeme ein bewusst geleertes Feld zurueck.
        var document = new DossierDocument
        {
            SchemaVersion = DossierDocument.CurrentSchemaVersion,
            Area = new DossierAreaSettings { ExecutionDate = "Ab Mai 2026" }
        };

        DossierDocumentMigration.MigrateToCurrent(document);

        Assert.Equal(
            DossierDocumentMigration.DefaultTopicTitles,
            document.Area.Topics.Select(t => t.Title).ToList());

        var ausfuehrung = document.Area.Topics.Single(t => t.Title == "Ausführungstermin");
        Assert.True(
            string.IsNullOrEmpty(ausfuehrung.Text),
            $"Altfeld wurde erneut abgeleitet: \"{ausfuehrung.Text}\"");
    }
}
