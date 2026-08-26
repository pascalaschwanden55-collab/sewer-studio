using AuswertungPro.Next.Application.Dossiers;
using AuswertungPro.Next.Application.Dossiers.Preview;
using AuswertungPro.Next.Infrastructure.Backup;
using AuswertungPro.Next.Infrastructure.Dossiers;
using AuswertungPro.Next.Infrastructure.Dossiers.Preview;

namespace AuswertungPro.Next.Infrastructure.Tests.Dossiers;

public sealed class DossierOutputPreviewEmptyFixedCellMapperTests
{
    [Fact]
    public void Ausgelieferte_Vorlage_enthaelt_beide_sicher_zugeordneten_Festzellen()
    {
        var root = new RepositoryRootFileLocator().Locate(AppContext.BaseDirectory);
        Assert.NotNull(root);
        var document = DossierPreviewBuilder.Build(Path.Combine(
            root!,
            "Export_Vorlage",
            DossierWordTemplate.TemplateFileName));
        var informationPage = document.Pages.Single(page => page.FieldKeys.Contains(
            "Aktennotiz",
            StringComparer.OrdinalIgnoreCase));
        var output = new DossierOutputPreviewPage(
            3,
            612,
            792,
            "",
            [
                Word("Für", 65, 600),
                Word("die", 86, 600),
                Word("Aktennotiz", 104, 600),
                Word("Rückmeldung", 65, 500),
                Word("Einverständnis", 65, 484),
                Word("Eigentümer", 65, 468)
            ]);
        var aktennotiz = DossierPreviewTarget.Field("Aktennotiz");
        var rueckmeldung = DossierPreviewTarget.Field("Rueckmeldung");
        var hits = new Dictionary<int, IReadOnlyList<DossierPreviewTarget>>
        {
            [0] = [DossierPreviewTarget.Literal("Für die Aktennotiz")],
            [1] = [DossierPreviewTarget.Literal("Für die Aktennotiz")],
            [2] = [DossierPreviewTarget.Literal("Für die Aktennotiz")],
            [3] = [DossierPreviewTarget.Literal(
                "Rückmeldung / Einverständnis Eigentümer")],
            [4] = [DossierPreviewTarget.Literal(
                "Rückmeldung / Einverständnis Eigentümer")],
            [5] = [DossierPreviewTarget.Literal(
                "Rückmeldung / Einverständnis Eigentümer")]
        };

        var areas = DossierOutputPreviewEmptyFixedCellMapper.Build(
            output,
            [informationPage],
            [aktennotiz, rueckmeldung],
            [EditableField("Aktennotiz", ""), EditableField("Rueckmeldung", "")],
            hits);

        Assert.Equal([aktennotiz, rueckmeldung], areas.Select(area => area.Target));
    }

    [Fact]
    public void Leere_Aktennotiz_erhaelt_die_Klickflaeche_der_echten_rechten_Zelle()
    {
        var page = PageWithFileNoteAnchor();
        var target = DossierPreviewTarget.Field("Aktennotiz");

        var area = Assert.Single(DossierOutputPreviewEmptyFixedCellMapper.Build(
            page,
            [TemplatePage()],
            [target],
            [EditableField("Aktennotiz", "")],
            AnchorHits("Für die Aktennotiz", 0, 1, 2)));

        Assert.Equal(target, area.Target);
        Assert.True(area.Left > page.Words.Max(word => word.Right));
        Assert.True(area.Right - area.Left > 300);
        Assert.True(area.Top > area.Bottom);
    }

    [Fact]
    public void Gefuellte_oder_bereits_getroffene_Aktennotiz_wird_nicht_verdoppelt()
    {
        var page = PageWithFileNoteAnchor();
        var target = DossierPreviewTarget.Field("Aktennotiz");
        var anchorHits = AnchorHits("Für die Aktennotiz", 0, 1, 2);
        var existingHits = anchorHits.ToDictionary(
            pair => pair.Key,
            pair => pair.Value);
        existingHits[3] = [target];
        page = page with { Words = [.. page.Words, Word("Text", 190, 600)] };

        Assert.Empty(DossierOutputPreviewEmptyFixedCellMapper.Build(
            page,
            [TemplatePage()],
            [target],
            [EditableField("Aktennotiz", "vorhanden")],
            anchorHits));
        Assert.Empty(DossierOutputPreviewEmptyFixedCellMapper.Build(
            page,
            [TemplatePage()],
            [target],
            [EditableField("Aktennotiz", "")],
            existingHits));
    }

    [Fact]
    public void Doppelte_Aktennotizzeile_bleibt_fail_closed()
    {
        var page = PageWithFileNoteAnchor();

        var areas = DossierOutputPreviewEmptyFixedCellMapper.Build(
            page,
            [TemplatePage(), TemplatePage()],
            [DossierPreviewTarget.Field("Aktennotiz")],
            [EditableField("Aktennotiz", "")],
            AnchorHits("Für die Aktennotiz", 0, 1, 2));

        Assert.Empty(areas);
    }

    [Fact]
    public void Umbenannter_Anker_bleibt_ueber_sein_Literalziel_eindeutig()
    {
        var page = new DossierOutputPreviewPage(
            3,
            612,
            792,
            "",
            [Word("Interne", 20, 600), Word("Notiz", 55, 600)]);

        var area = Assert.Single(DossierOutputPreviewEmptyFixedCellMapper.Build(
            page,
            [TemplatePage()],
            [DossierPreviewTarget.Field("Aktennotiz")],
            [EditableField("Aktennotiz", "")],
            AnchorHits("Für die Aktennotiz", 0, 1),
            _ => "Interne Notiz"));

        Assert.Equal("Aktennotiz", area.Target.Key);
    }

    [Fact]
    public void Eindeutiger_Anker_nach_anderem_Seitentext_verwendet_seine_echte_Position()
    {
        var page = PageWithFileNoteAnchor() with
        {
            Words =
            [
                Word("Informationen", 20, 700),
                .. PageWithFileNoteAnchor().Words
            ]
        };
        var target = DossierPreviewTarget.Field("Aktennotiz");

        var area = Assert.Single(DossierOutputPreviewEmptyFixedCellMapper.Build(
            page,
            [TemplatePage()],
            [target],
            [EditableField("Aktennotiz", "")],
            new Dictionary<int, IReadOnlyList<DossierPreviewTarget>>()));

        Assert.Equal(target, area.Target);
        Assert.True(area.Top < page.Words[0].Bottom);
    }

    [Fact]
    public void Leere_Rueckmeldung_erhaelt_nur_den_oberen_Feldabsatz()
    {
        var page = PageWithResponseAnchor();
        var target = DossierPreviewTarget.Field("Rueckmeldung");

        var area = Assert.Single(DossierOutputPreviewEmptyFixedCellMapper.Build(
            page,
            [TemplatePage()],
            [target],
            [EditableField("Rueckmeldung", "")],
            AnchorHits(
                "Rückmeldung / Einverständnis Eigentümer",
                0,
                1,
                2)));

        Assert.Equal(target, area.Target);
        Assert.InRange(area.Top - area.Bottom, 10, 30);
        Assert.True(area.Bottom > 480);
    }

    [Fact]
    public void Rueckmeldung_ohne_eindeutige_Unterschriftsstruktur_wird_nicht_geraten()
    {
        var page = PageWithResponseAnchor();
        var template = TemplatePage(ResponseCell(withSignature: false));

        var areas = DossierOutputPreviewEmptyFixedCellMapper.Build(
            page,
            [template],
            [DossierPreviewTarget.Field("Rueckmeldung")],
            [EditableField("Rueckmeldung", "")],
            AnchorHits(
                "Rückmeldung / Einverständnis Eigentümer",
                0,
                1,
                2));

        Assert.Empty(areas);
    }

    private static DossierOutputPreviewPage PageWithFileNoteAnchor()
        => new(
            3,
            612,
            792,
            "",
            [
                Word("Für", 20, 600),
                Word("die", 40, 600),
                Word("Aktennotiz", 58, 600)
            ]);

    private static DossierOutputPreviewPage PageWithResponseAnchor()
        => new(
            3,
            612,
            792,
            "",
            [
                Word("Rückmeldung", 20, 500),
                Word("Einverständnis", 67, 500),
                Word("Eigentümer", 120, 500)
            ]);

    private static DossierPreviewPage TemplatePage(
        DossierPreviewTableCell? responseCell = null)
    {
        var table = new DossierPreviewTable(
            [220, 530],
            0,
            [
                new DossierPreviewTableRow(
                [
                    LiteralCell("Für die Aktennotiz"),
                    FieldCell("Aktennotiz")
                ]),
                new DossierPreviewTableRow(
                [
                    LiteralCell("Rückmeldung / Einverständnis Eigentümer"),
                    responseCell ?? ResponseCell(withSignature: true)
                ])
            ],
            "Themen",
            ["Thema", "Text"],
            null);

        return new DossierPreviewPage(
            3,
            "Informationen Sanierung",
            new DossierPreviewGeometry(794, 1123, DossierPreviewEdges.Zero),
            [table],
            ["Aktennotiz", "Rueckmeldung"]);
    }

    private static DossierPreviewTableCell ResponseCell(bool withSignature)
        => new(
            [
                Paragraph(DossierPreviewRun.Field(
                    "Rueckmeldung",
                    DossierPreviewRunFormat.Default)),
                Paragraph(),
                Paragraph(),
                Paragraph(),
                Paragraph(DossierPreviewRun.Literal(
                    "........................................",
                    DossierPreviewRunFormat.Default)),
                Paragraph(DossierPreviewRun.Literal(
                    withSignature ? "Ort/Datum Unterschrift(en)" : "Freier Text",
                    DossierPreviewRunFormat.Default))
            ],
            DossierPreviewEdges.All(7.2),
            DossierPreviewEdges.All(0.5),
            null,
            1);

    private static DossierPreviewTableCell LiteralCell(string text)
        => new(
            [Paragraph(DossierPreviewRun.Literal(
                text,
                DossierPreviewRunFormat.Default))],
            DossierPreviewEdges.All(7.2),
            DossierPreviewEdges.All(0.5),
            null,
            1);

    private static DossierPreviewTableCell FieldCell(string key)
        => new(
            [Paragraph(DossierPreviewRun.Field(
                key,
                DossierPreviewRunFormat.Default))],
            DossierPreviewEdges.All(7.2),
            DossierPreviewEdges.All(0.5),
            null,
            1);

    private static DossierPreviewParagraph Paragraph(params DossierPreviewRun[] runs)
        => new(runs, DossierPreviewParagraphFormat.Default);

    private static DossierPreviewField EditableField(string key, string value)
        => new(
            key,
            key,
            DossierPreviewFieldKind.MultiLine,
            () => value,
            _ => { });

    private static Dictionary<int, IReadOnlyList<DossierPreviewTarget>> AnchorHits(
        string original,
        params int[] indices)
        => indices.ToDictionary(
            index => index,
            _ => (IReadOnlyList<DossierPreviewTarget>)
                [DossierPreviewTarget.Literal(original)]);

    private static DossierOutputPreviewWord Word(
        string text,
        double left,
        double bottom)
        => new(text, left, bottom, left + Math.Max(12, text.Length * 4), bottom + 11);
}
