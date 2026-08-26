using AuswertungPro.Next.Application.Dossiers;
using AuswertungPro.Next.Application.Dossiers.Preview;
using AuswertungPro.Next.Domain.Models.Dossiers;
using AuswertungPro.Next.Infrastructure.Dossiers;

namespace AuswertungPro.Next.Infrastructure.Tests.Dossiers;

public sealed class DossierOutputPreviewInteractionMapperTests
{
    [Fact]
    public void Sichtbare_Planueberschrift_zeigt_den_Fotoknopf_auch_bei_fremdem_Seitennamen()
    {
        var output = new DossierOutputPreviewPage(
            3,
            612,
            792,
            "1. Übersichtsplan Werkleitungen",
            [new DossierOutputPreviewWord("Übersichtsplan", 40, 700, 130, 712)]);
        var information = new DossierPreviewPage(
            4,
            "Informationen Sanierung",
            new DossierPreviewGeometry(794, 1123, DossierPreviewEdges.Zero),
            [],
            ["Themen"]);

        Assert.True(DossierOutputPreviewInteractionMapper.ContainsPlanLocation(
            output,
            [information]));
    }

    [Fact]
    public void Inhaltsverzeichnis_zeigt_keinen_Fotoknopf_bei_der_Planzeile()
    {
        var output = new DossierOutputPreviewPage(
            2,
            612,
            792,
            "Inhaltsverzeichnis",
            [new DossierOutputPreviewWord("Uebersichtsplan", 40, 700, 130, 712)]);
        var toc = new DossierPreviewPage(
            2,
            "Inhaltsverzeichnis",
            new DossierPreviewGeometry(794, 1123, DossierPreviewEdges.Zero),
            [
                new DossierPreviewParagraph(
                    [DossierPreviewRun.Literal(
                        "Inhaltsverzeichnis",
                        DossierPreviewRunFormat.Default)],
                    DossierPreviewParagraphFormat.Default)
            ],
            []);

        Assert.False(DossierOutputPreviewInteractionMapper.ContainsPlanLocation(
            output,
            [toc]));
    }

    [Fact]
    public void Klickziele_gehoeren_nur_zum_sichtbaren_Blatt()
    {
        var deckblatt = new DossierPreviewPage(
            1,
            "Deckblatt",
            new DossierPreviewGeometry(794, 1123, DossierPreviewEdges.Zero),
            [
                new DossierPreviewParagraph(
                    [DossierPreviewRun.Literal(
                        "Eigentuemerdossier",
                        DossierPreviewRunFormat.Default)],
                    DossierPreviewParagraphFormat.Default)
            ],
            ["Parzellen_Zeile"]);
        var eigentuemer = new DossierPreviewPage(
            2,
            "Eigentumsverhaeltnisse",
            new DossierPreviewGeometry(794, 1123, DossierPreviewEdges.Zero),
            [],
            ["Eigentuemer"]);
        var deckblattParzelle = DossierPreviewTarget.Field("Parzellen_Zeile");
        var deckblattTitel = DossierPreviewTarget.Literal("Eigentuemerdossier");
        var eigentuemerParzelle = DossierPreviewTarget.RowCell(
            "Eigentuemer", 0, "ParcelNumber");

        var result = DossierOutputPreviewInteractionMapper.TargetsForPages(
            [deckblattParzelle, deckblattTitel, eigentuemerParzelle],
            [deckblatt]);

        Assert.Contains(deckblattParzelle, result);
        Assert.Contains(deckblattTitel, result);
        Assert.DoesNotContain(eigentuemerParzelle, result);

        Assert.Equal(
            [eigentuemerParzelle],
            DossierOutputPreviewInteractionMapper.TargetsForPages(
                [deckblattParzelle, deckblattTitel, eigentuemerParzelle],
                [eigentuemer]));
    }

    [Fact]
    public void Gleicher_Wert_auf_anderem_Kapitel_kapert_den_Klick_nicht()
    {
        var pageTemplate = new DossierPreviewPage(
            1,
            "Deckblatt",
            new DossierPreviewGeometry(794, 1123, DossierPreviewEdges.Zero),
            [],
            ["Parzellen_Zeile"]);
        var deckblatt = DossierPreviewTarget.Field("Parzellen_Zeile");
        var fremdeZelle = DossierPreviewTarget.RowCell("Eigentuemer", 0, "Pz_Nr");
        var fields = new[]
        {
            new DossierPreviewField(
                "Parzellen_Zeile",
                "Parzellen-Nr.",
                DossierPreviewFieldKind.Text,
                () => "439",
                _ => { })
        };
        var outputPage = new DossierOutputPreviewPage(
            1,
            612,
            792,
            "Parzelle 439",
            [new DossierOutputPreviewWord("439", 100, 700, 120, 712)]);
        var visibleTargets = DossierOutputPreviewInteractionMapper.TargetsForPages(
            [deckblatt, fremdeZelle],
            [pageTemplate]);
        var candidates = DossierOutputPreviewInteractionMapper.BuildCandidates(
            visibleTargets,
            fields,
            new Dictionary<string, string>(),
            new DossierDefinition(),
            _ =>
            [
                new Dictionary<string, string> { ["Pz_Nr"] = "439" }
            ]);

        var hits = DossierOutputPreviewHitMatcher.Match(outputPage.Words, candidates);

        Assert.Equal([deckblatt], hits[0]);
    }

    [Fact]
    public void BuildNavigation_trennt_Originalbeilagen_von_bearbeitbaren_Dossierseiten()
    {
        var templatePage = new DossierPreviewPage(
            1,
            "Deckblatt",
            new DossierPreviewGeometry(794, 1123, DossierPreviewEdges.Zero),
            [],
            []);
        var templates = new[]
        {
            new DossierPreviewNavigationItem("Deckblatt", "Seite 1", templatePage)
        };
        var pages = new[]
        {
            new DossierOutputPreviewPage(1, 595, 842, "Deckblatt", []),
            new DossierOutputPreviewPage(2, 595, 842, "Original", [], IsAttachment: true)
        };

        var result = DossierOutputPreviewInteractionMapper.BuildNavigation(
            pages,
            templates,
            new DossierDefinition(),
            new Dictionary<string, string>(),
            _ => Array.Empty<IReadOnlyDictionary<string, string>>());

        Assert.Same(templatePage, result[0].EditorPage);
        Assert.Equal("Beilagen", result[1].ChapterTitle);
        Assert.Null(result[1].EditorPage);
    }

    [Fact]
    public void BuildCandidates_verwendet_den_bearbeiteten_Wortlaut_einer_Beschriftung()
    {
        var dossier = new DossierDefinition
        {
            TextOverrides = { ["Alter Titel"] = "Neuer Titel" }
        };
        var target = DossierPreviewTarget.Literal("Alter Titel");

        var candidates = DossierOutputPreviewInteractionMapper.BuildCandidates(
            [target],
            [],
            new Dictionary<string, string>(),
            dossier,
            _ => Array.Empty<IReadOnlyDictionary<string, string>>());

        var candidate = Assert.Single(candidates);
        Assert.Equal(target, candidate.Target);
        Assert.Equal("Neuer Titel", candidate.Text);
    }

    [Fact]
    public void Zusatzpunkt_Titel_und_Seite_haben_getrennte_Klickziele()
    {
        var dossier = new DossierDefinition
        {
            TocAttachments =
            [
                new DossierTocAttachment { Title = "TV-Protokolle", PageNumber = "12" }
            ]
        };
        var titel = DossierPreviewTarget.RowCell("Verzeichnis_Beilagen", 0, "Titel");
        var seite = DossierPreviewTarget.RowCell("Verzeichnis_Beilagen", 0, "Seite");

        var candidates = DossierOutputPreviewInteractionMapper.BuildCandidates(
            [titel, seite],
            [],
            new Dictionary<string, string>(),
            dossier,
            _ => Array.Empty<IReadOnlyDictionary<string, string>>());

        Assert.Contains(candidates, candidate => candidate.Target == titel
            && candidate.Text == "TV-Protokolle");
        Assert.Contains(candidates, candidate => candidate.Target == seite
            && candidate.Text == "12");
    }

    [Fact]
    public void Eigentuemer_Kontaktdaten_haben_in_der_gemeinsamen_Zelle_eigene_Klickziele()
    {
        var dossier = new DossierDefinition
        {
            Owners =
            [
                new DossierOwnerRow
                {
                    Name = "Muster AG",
                    Phone = "041 123 45 67",
                    Mail = "info@muster.ch",
                    Occupancy = "Mehrfamilienhaus"
                }
            ]
        };
        var rows = DossierWordTemplateExportService.BuildOwnerRows(dossier);
        var telefon = DossierPreviewTarget.RowCell("Eigentuemer", 0, "Telefon");
        var mail = DossierPreviewTarget.RowCell("Eigentuemer", 0, "Mail");
        var bewohner = DossierPreviewTarget.RowCell("Eigentuemer", 0, "Objektbewohner");

        var candidates = DossierOutputPreviewInteractionMapper.BuildCandidates(
            [telefon, mail, bewohner],
            [],
            new Dictionary<string, string>(),
            dossier,
            key => string.Equals(key, "Eigentuemer", StringComparison.OrdinalIgnoreCase)
                ? rows
                : Array.Empty<IReadOnlyDictionary<string, string>>());

        Assert.Contains(candidates, value => value.Target == telefon
            && value.Text == "041 123 45 67");
        Assert.Contains(candidates, value => value.Target == mail
            && value.Text == "info@muster.ch");
        Assert.Contains(candidates, value => value.Target == bewohner
            && value.Text == "Mehrfamilienhaus");
    }
}
