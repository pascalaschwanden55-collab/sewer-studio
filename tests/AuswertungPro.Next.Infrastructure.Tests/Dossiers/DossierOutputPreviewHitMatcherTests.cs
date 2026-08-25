using AuswertungPro.Next.Application.Dossiers;
using AuswertungPro.Next.Application.Dossiers.Preview;

namespace AuswertungPro.Next.Infrastructure.Tests.Dossiers;

public sealed class DossierOutputPreviewHitMatcherTests
{
    [Fact]
    public void Match_verbindet_mehrzeiligen_PDF_Text_mit_dem_passenden_Feld()
    {
        var target = DossierPreviewTarget.Field("Eigentuemer_Block");
        var words = new[]
        {
            Word("Hans", 10),
            Word("Muster", 50),
            Word("Bahnhofstrasse", 100),
            Word("7", 190)
        };

        var result = DossierOutputPreviewHitMatcher.Match(
            words,
            [new DossierPreviewTextCandidate(target, "Hans Muster\nBahnhofstrasse 7")]);

        Assert.Equal(4, result.Count);
        Assert.All(result.Values, targets => Assert.Contains(target, targets));
    }

    [Fact]
    public void Match_bewahrt_mehrere_Ziele_am_selben_Wort_fuer_die_genaueste_Auswahl()
    {
        var row = DossierPreviewTarget.Row("Themen", 0);
        var cell = DossierPreviewTarget.RowCell("Themen", 0, "Text");

        var result = DossierOutputPreviewHitMatcher.Match(
            [Word("unbekannt", 10)],
            [
                new DossierPreviewTextCandidate(row, "unbekannt"),
                new DossierPreviewTextCandidate(cell, "unbekannt")
            ]);

        var targets = Assert.Single(result).Value;
        Assert.Equal(cell, DossierPreviewTarget.SelectMostSpecific(targets, _ => true));
    }

    private static DossierOutputPreviewWord Word(string text, double left)
        => new(text, left, 20, left + 30, 35);
}
