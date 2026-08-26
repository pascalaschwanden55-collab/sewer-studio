using AuswertungPro.Next.Application.Dossiers;
using AuswertungPro.Next.Application.Dossiers.Preview;

namespace AuswertungPro.Next.Infrastructure.Tests.Dossiers;

public sealed class DossierOutputPreviewHitAreaBuilderTests
{
    [Fact]
    public void Mehrere_Woerter_werden_zu_einer_grossen_Klickflaeche()
    {
        var target = DossierPreviewTarget.Field("Bemerkung");
        var page = Page(
            new DossierOutputPreviewWord("Die", 100, 700, 120, 712),
            new DossierOutputPreviewWord("Zugaenge", 130, 700, 180, 712),
            new DossierOutputPreviewWord("bleiben", 100, 680, 145, 692));

        var areas = DossierOutputPreviewHitAreaBuilder.Build(
            page,
            new Dictionary<int, IReadOnlyList<DossierPreviewTarget>>
            {
                [0] = [target],
                [1] = [target],
                [2] = [target]
            });

        var area = Assert.Single(areas);
        Assert.Equal(target, area.Target);
        Assert.True(area.Left < 100);
        Assert.True(area.Right > 180);
        Assert.True(area.Bottom < 680);
        Assert.True(area.Top > 712);
    }

    [Fact]
    public void Getrennte_Vorkommen_bleiben_getrennt()
    {
        var target = DossierPreviewTarget.Literal("Kapitel");
        var page = Page(
            new DossierOutputPreviewWord("Kapitel", 50, 700, 90, 712),
            new DossierOutputPreviewWord("anderer", 50, 500, 90, 512),
            new DossierOutputPreviewWord("Text", 95, 500, 120, 512),
            new DossierOutputPreviewWord("Kapitel", 50, 300, 90, 312));

        var areas = DossierOutputPreviewHitAreaBuilder.Build(
            page,
            new Dictionary<int, IReadOnlyList<DossierPreviewTarget>>
            {
                [0] = [target],
                [3] = [target]
            });

        Assert.Equal(2, areas.Count);
    }

    [Fact]
    public void Kurze_Tabellenzelle_ist_breiter_als_nur_ihr_Wort()
    {
        var target = DossierPreviewTarget.RowCell("Eigentuemer", 0, "Pz_Nr");
        var page = Page(new DossierOutputPreviewWord("439", 100, 700, 118, 712));

        var area = Assert.Single(DossierOutputPreviewHitAreaBuilder.Build(
            page,
            new Dictionary<int, IReadOnlyList<DossierPreviewTarget>>
            {
                [0] = [target]
            }));

        Assert.True(area.Right - area.Left >= 36);
    }

    private static DossierOutputPreviewPage Page(params DossierOutputPreviewWord[] words)
        => new(1, 612, 792, string.Join(' ', words.Select(word => word.Text)), words);
}
