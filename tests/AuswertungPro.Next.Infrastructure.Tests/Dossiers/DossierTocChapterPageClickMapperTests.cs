using AuswertungPro.Next.Application.Dossiers;
using AuswertungPro.Next.Application.Dossiers.Preview;

namespace AuswertungPro.Next.Infrastructure.Tests.Dossiers;

public sealed class DossierTocChapterPageClickMapperTests
{
    [Fact]
    public void Gleiche_Seitenzahlen_werden_ueber_ihre_Kapitelzeile_unterschieden()
    {
        var firstTitle = DossierPreviewTarget.Literal("Eigentumsverhältnisse");
        var secondTitle = DossierPreviewTarget.Literal("Informationen Sanierung");
        var words = new[]
        {
            Word("2.", 30, 700),
            Word("Eigentumsverhältnisse", 60, 700),
            Word("4", 530, 700),
            Word("fremde", 300, 650),
            Word("4", 530, 650),
            Word("3.", 30, 600),
            Word("Informationen", 60, 600),
            Word("Sanierung", 150, 600),
            Word("4", 530, 600)
        };
        var page = new DossierOutputPreviewPage(2, 612, 792, "", words);
        var hits = new Dictionary<int, IReadOnlyList<DossierPreviewTarget>>
        {
            [1] = [firstTitle],
            [6] = [secondTitle],
            [7] = [secondTitle]
        };

        var result = DossierTocChapterPageClickMapper.AddPageTargets(
            page,
            hits,
            ["Eigentumsverhältnisse", "Informationen Sanierung"]);

        Assert.Contains(
            DossierTocChapterPageClickMapper.PageTarget("Eigentumsverhältnisse"),
            result[2]);
        Assert.Contains(
            DossierTocChapterPageClickMapper.PageTarget("Informationen Sanierung"),
            result[8]);
        Assert.False(result.TryGetValue(4, out var foreign)
            && foreign.Any(DossierTocChapterPageClickMapper.IsPageTarget));
    }

    [Fact]
    public void Seitenziel_ist_nur_auf_der_Verzeichnisseite_sichtbar()
    {
        var toc = new DossierPreviewPage(
            2,
            "Inhaltsverzeichnis",
            new DossierPreviewGeometry(794, 1123, DossierPreviewEdges.Zero),
            [
                new DossierPreviewParagraph(
                    [DossierPreviewRun.Literal("Eigentumsverhältnisse", DossierPreviewRunFormat.Default)],
                    DossierPreviewParagraphFormat.Default,
                    TocEntry: new DossierPreviewTocEntry("2.", "Eigentumsverhältnisse", "4"))
            ],
            []);
        var other = toc with
        {
            Number = 3,
            Title = "Eigentumsverhältnisse",
            Blocks = []
        };
        var target = DossierTocChapterPageClickMapper.PageTarget("Eigentumsverhältnisse");

        Assert.Equal(
            [target],
            DossierOutputPreviewInteractionMapper.TargetsForPages([target], [toc]));
        Assert.Empty(DossierOutputPreviewInteractionMapper.TargetsForPages([target], [other]));
    }

    [Fact]
    public void Gleicher_Titel_als_Kapitelueberschrift_kapert_die_Toc_Zahl_nicht()
    {
        var title = DossierPreviewTarget.Literal("Eigentumsverhältnisse");
        var words = new[]
        {
            Word("2.", 30, 700),
            Word("Eigentumsverhältnisse", 60, 700),
            Word("4a", 530, 700),
            Word("Eigentumsverhältnisse", 60, 400)
        };
        var page = new DossierOutputPreviewPage(2, 612, 792, "", words);

        var result = DossierTocChapterPageClickMapper.AddPageTargets(
            page,
            new Dictionary<int, IReadOnlyList<DossierPreviewTarget>>
            {
                [1] = [title],
                [3] = [title]
            },
            ["Eigentumsverhältnisse"]);

        Assert.Contains(
            DossierTocChapterPageClickMapper.PageTarget("Eigentumsverhältnisse"),
            result[2]);
        Assert.DoesNotContain(
            result,
            pair => pair.Key == 3
                && pair.Value.Any(DossierTocChapterPageClickMapper.IsPageTarget));
    }

    private static DossierOutputPreviewWord Word(string text, double left, double bottom)
        => new(text, left, bottom, left + Math.Max(12, text.Length * 6), bottom + 12);
}
