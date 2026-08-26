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

    [Fact]
    public void Match_erkennt_getrennte_und_zusammengefuegte_PDF_Woerter_exakt()
    {
        var target = DossierPreviewTarget.Field("Hinweis");

        var result = DossierOutputPreviewHitMatcher.Match(
            Words(
                "Abwasser",
                "anlagen",
                "und",
                "Kanalisationsleitungenwerden",
                "regelmässig",
                "kontrolliert"),
            [new DossierPreviewTextCandidate(
                target,
                "Abwasseranlagen und Kanalisationsleitungen werden regelmässig kontrolliert")]);

        Assert.Equal(6, result.Count);
        Assert.All(result.Values, targets => Assert.Contains(target, targets));
    }

    [Fact]
    public void Match_nutzt_bei_langem_Text_einen_eindeutigen_Fuenf_Wort_Anker()
    {
        var target = DossierPreviewTarget.Field("Langer_Hinweis");

        var result = DossierOutputPreviewHitMatcher.Match(
            Words(
                "Abweichend",
                "Zugänge",
                "sollten",
                "normal",
                "möglich",
                "sein",
                "wenn",
                "nötig",
                "werden",
                "Provisorien",
                "für",
                "die",
                "Zugänge",
                "erstellt"),
            [new DossierPreviewTextCandidate(
                target,
                "Die Zugänge sollten normal möglich sein wenn nötig werden "
                + "Provisorien für die Zugänge erstellt")]);

        Assert.Equal(13, result.Count);
        Assert.DoesNotContain(0, result.Keys);
        Assert.All(result.Values, targets => Assert.Contains(target, targets));
    }

    [Fact]
    public void Match_verwendet_bei_kurzem_Text_keinen_Anker_Rueckfall()
    {
        var target = DossierPreviewTarget.Field("Kurzer_Hinweis");

        var result = DossierOutputPreviewHitMatcher.Match(
            Words("Anders", "Leitungen", "werden", "regelmässig", "kontrolliert"),
            [new DossierPreviewTextCandidate(
                target,
                "Private Leitungen werden regelmässig kontrolliert")]);

        Assert.Empty(result);
    }

    [Fact]
    public void Match_verwechselt_zwei_lange_aehnliche_Kandidaten_nicht()
    {
        var erster = DossierPreviewTarget.Field("Erster_Hinweis");
        var zweiter = DossierPreviewTarget.Field("Zweiter_Hinweis");
        var candidates = new[]
        {
            new DossierPreviewTextCandidate(
                erster,
                "Gemeinden und Private müssen ihre Leitungen regelmässig prüfen Alpha Ende"),
            new DossierPreviewTextCandidate(
                zweiter,
                "Gemeinden und Private müssen ihre Leitungen regelmässig prüfen Beta Schluss")
        };

        var result = DossierOutputPreviewHitMatcher.Match(
            Words(
                "Gemeinden",
                "und",
                "Private",
                "müssen",
                "ihre",
                "Leitungen",
                "regelmässig",
                "prüfen",
                "Gamma",
                "Aus"),
            candidates);

        Assert.Empty(result);
    }

    [Fact]
    public void Match_ordnet_uneindeutige_Wortgrenzen_nicht_zwei_Zielen_zu()
    {
        var getrennt = DossierPreviewTarget.Field("Getrennt");
        var verbunden = DossierPreviewTarget.Field("Verbunden");

        var result = DossierOutputPreviewHitMatcher.Match(
            Words("Abwasser", "Rohr", "Prüfung"),
            [
                new DossierPreviewTextCandidate(getrennt, "Ab Wasser Rohr Prüfung"),
                new DossierPreviewTextCandidate(verbunden, "Abwasser Rohr Prüfung")
            ]);

        Assert.All(result.Values, targets =>
        {
            Assert.Contains(verbunden, targets);
            Assert.DoesNotContain(getrennt, targets);
        });
    }

    [Fact]
    public void Match_erkennt_Tabellenzelle_trotz_eingeschobener_Nachbarspalte()
    {
        var target = DossierPreviewTarget.RowCell("Themen", 4, "Thema");

        var result = DossierOutputPreviewHitMatcher.Match(
            Words(
                "Behinderungen",
                "fremdeSpalte",
                "Zugänge",
                "nochEtwas",
                "Verkehrsführung",
                "andererText",
                "Fussgängerführung"),
            [new DossierPreviewTextCandidate(
                target,
                "Behinderungen, Zugänge, Verkehrsführung, Fussgängerführung")]);

        Assert.Contains(target, result[0]);
        Assert.Contains(target, result[4]);
        Assert.Contains(target, result[6]);
        Assert.False(result.ContainsKey(1));
        Assert.False(result.ContainsKey(3));
        Assert.False(result.ContainsKey(5));
    }

    private static IReadOnlyList<DossierOutputPreviewWord> Words(params string[] texts)
        => texts
            .Select((text, index) => Word(text, 10 + (index * 40)))
            .ToList();

    private static DossierOutputPreviewWord Word(string text, double left)
        => new(text, left, 20, left + 30, 35);
}
