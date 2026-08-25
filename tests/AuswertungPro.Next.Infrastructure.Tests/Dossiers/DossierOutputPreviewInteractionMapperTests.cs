using AuswertungPro.Next.Application.Dossiers;
using AuswertungPro.Next.Application.Dossiers.Preview;
using AuswertungPro.Next.Domain.Models.Dossiers;
using AuswertungPro.Next.Infrastructure.Dossiers;

namespace AuswertungPro.Next.Infrastructure.Tests.Dossiers;

public sealed class DossierOutputPreviewInteractionMapperTests
{
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
