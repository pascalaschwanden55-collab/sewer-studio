using AuswertungPro.Next.Application.Dossiers;
using AuswertungPro.Next.Application.Dossiers.Preview;
using AuswertungPro.Next.Domain.Models.Dossiers;

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
}
