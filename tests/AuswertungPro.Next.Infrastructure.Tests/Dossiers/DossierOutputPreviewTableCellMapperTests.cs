using AuswertungPro.Next.Application.Dossiers;
using AuswertungPro.Next.Application.Dossiers.Preview;

namespace AuswertungPro.Next.Infrastructure.Tests.Dossiers;

public sealed class DossierOutputPreviewTableCellMapperTests
{
    [Fact]
    public void Gefuellte_Tabellenzelle_erhaelt_die_volle_physische_Flaeche()
    {
        var title = DossierPreviewTarget.RowCell("Themen", 0, "Thema");
        var text = DossierPreviewTarget.RowCell("Themen", 0, "Text");
        var page = TopicPage(
            3,
            Word("Thema", 20, 700),
            Word("Bemerkungen", 175, 700),
            Word("Ausgangslage", 24, 660),
            Word("kurz", 180, 660));

        var mappings = DossierOutputPreviewTableCellMapper.Build(
            [Input(page, TopicTemplatePage())],
            [title, text],
            _ => [TopicRow("Ausgangslage", "kurz")]);

        var area = Assert.Single(mappings[3].Areas, area => area.Target == text);
        var word = page.Words.Single(item => item.Text == "kurz");

        Assert.True(area.Left < word.Left - 10);
        Assert.True(area.Right > word.Right + 100);
        Assert.True(area.Top > word.Top);
        Assert.True(area.Bottom < word.Bottom);
    }

    [Fact]
    public void Gleicher_Text_in_zwei_Spalten_bleibt_der_richtigen_Zelle_zugeordnet()
    {
        var haus = DossierPreviewTarget.RowCell("Eigentuemer", 0, "Haus_Nr");
        var parzelle = DossierPreviewTarget.RowCell("Eigentuemer", 0, "Pz_Nr");
        var name = DossierPreviewTarget.RowCell("Eigentuemer", 0, "Eigentuemer_Zelle");
        var page = OwnerPage();

        // Der allgemeine Wortmatcher kann bei zwei gleichen Werten beide
        // Fundstellen beiden Zielen geben. Die physische Tabelle muss diese
        // Mehrdeutigkeit ueber ihre Spalten aufloesen.
        var ambiguousHits = new Dictionary<int, IReadOnlyList<DossierPreviewTarget>>
        {
            [3] = [haus, parzelle],
            [4] = [haus, parzelle]
        };
        var input = new DossierOutputPreviewTablePageInput(
            page,
            [OwnerTemplatePage()],
            ambiguousHits);

        var mappings = DossierOutputPreviewTableCellMapper.Build(
            [input],
            [haus, parzelle, name],
            _ =>
            [
                new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
                {
                    ["Haus_Nr"] = "30",
                    ["Pz_Nr"] = "30",
                    ["Eigentuemer_Zelle"] = "Hans Muster"
                }
            ]);

        var areas = mappings[3].Areas;
        var hausArea = Assert.Single(areas, area => area.Target == haus);
        var parzellenArea = Assert.Single(areas, area => area.Target == parzelle);

        Assert.True(hausArea.Right <= parzellenArea.Left);
        Assert.True(page.Words[3].Left >= hausArea.Left
            && page.Words[3].Right <= hausArea.Right);
        Assert.True(page.Words[4].Left >= parzellenArea.Left
            && page.Words[4].Right <= parzellenArea.Right);
    }

    [Fact]
    public void Eigentuemer_Beschriftung_bleibt_kleines_Ziel_in_gemeinsamer_Zelle()
    {
        var ownerCell = DossierPreviewTarget.RowCell(
            "Eigentuemer", 0, "Eigentuemer_Zelle");
        var phoneLabel = DossierPreviewTarget.RowCell(
            "Eigentuemer", 0, "Telefon_Beschriftung");
        var phone = DossierPreviewTarget.RowCell("Eigentuemer", 0, "Telefon");
        var page = OwnerPage() with
        {
            Words =
            [
                Word("Haus", 20, 700),
                Word("Parzelle", 95, 700),
                Word("Eigentuemer", 170, 700),
                Word("30", 24, 660),
                Word("439", 100, 660),
                Word("Hans", 175, 660),
                Word("Muster", 205, 660),
                Word("Tel.:", 175, 644),
                Word("041", 205, 644)
            ]
        };

        var mappings = DossierOutputPreviewTableCellMapper.Build(
            [Input(page, OwnerTemplatePage())],
            [
                DossierPreviewTarget.RowCell("Eigentuemer", 0, "Haus_Nr"),
                DossierPreviewTarget.RowCell("Eigentuemer", 0, "Pz_Nr"),
                ownerCell,
                phoneLabel,
                phone
            ],
            _ =>
            [
                new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
                {
                    ["Haus_Nr"] = "30",
                    ["Pz_Nr"] = "439",
                    ["Eigentuemer_Zelle"] = "Hans Muster Tel.: 041",
                    ["Telefon_Beschriftung"] = "Tel.:",
                    ["Telefon"] = "041"
                }
            ]);

        var areas = mappings[3].Areas;
        var cellArea = Assert.Single(areas, area => area.Target == ownerCell);
        var labelArea = Assert.Single(areas, area => area.Target == phoneLabel);

        Assert.True(labelArea.Left >= cellArea.Left);
        Assert.True(labelArea.Right <= cellArea.Right);
        Assert.True(labelArea.Top <= cellArea.Top);
        Assert.True(labelArea.Bottom >= cellArea.Bottom);
        Assert.True(labelArea.Right - labelArea.Left
            < cellArea.Right - cellArea.Left);
    }

    [Fact]
    public void Folgeseite_zaehlt_Zeilen_weiter_und_macht_Leerzelle_anklickbar()
    {
        var targets = Enumerable.Range(0, 2)
            .SelectMany(row => new[]
            {
                DossierPreviewTarget.RowCell("Themen", row, "Thema"),
                DossierPreviewTarget.RowCell("Themen", row, "Text")
            })
            .ToList();
        var template = TopicTemplatePage();
        var first = TopicPage(
            3,
            Word("Thema", 20, 700),
            Word("Bemerkungen", 175, 700),
            Word("Ausgangslage", 24, 660),
            Word("bekannt", 180, 660));
        var continuation = TopicPage(
            4,
            // Word wiederholt diesen Tabellenkopf in der echten Vorlage nicht.
            Word("Ansprechpartner", 24, 730));

        var mappings = DossierOutputPreviewTableCellMapper.Build(
            [Input(first, template), Input(continuation, template)],
            targets,
            _ =>
            [
                TopicRow("Ausgangslage", "bekannt"),
                TopicRow("Ansprechpartner", "")
            ]);

        Assert.Contains(mappings[3].Areas, area => area.Target.RowIndex == 0);
        Assert.DoesNotContain(mappings[4].Areas, area => area.Target.RowIndex == 0);

        var blank = Assert.Single(
            mappings[4].Areas,
            area => area.Target == DossierPreviewTarget.RowCell("Themen", 1, "Text"));
        Assert.True(blank.Left > 150);
        Assert.True(blank.Top > blank.Bottom);
    }

    [Fact]
    public void Fehlender_Spaltenwert_wird_nicht_aus_der_Folgezeile_gemischt()
    {
        var targets = Enumerable.Range(0, 2)
            .SelectMany(row => new[]
            {
                DossierPreviewTarget.RowCell("Themen", row, "Thema"),
                DossierPreviewTarget.RowCell("Themen", row, "Text")
            })
            .ToList();
        var page = TopicPage(
            3,
            Word("Thema", 20, 700),
            Word("Bemerkungen", 175, 700),
            Word("A", 24, 660),
            // Der Text von Zeile 0 fehlt im PDF. Derselbe Text steht erst
            // zusammen mit B in der naechsten physischen Tabellenzeile.
            Word("B", 24, 610),
            Word("gleich", 180, 610));

        var mapping = DossierOutputPreviewTableCellMapper.Build(
            [Input(page, TopicTemplatePage())],
            targets,
            _ =>
            [
                TopicRow("A", "gleich"),
                TopicRow("B", "gleich")
            ])[3];

        Assert.Empty(mapping.Areas);
        Assert.Empty(mapping.ReplacedPhysicalTargets);
    }

    [Fact]
    public void Einzelner_gleicher_Wert_der_Folgezeile_wird_nicht_vorgezogen()
    {
        var targets = Enumerable.Range(0, 2)
            .Select(row => DossierPreviewTarget.RowCell("Themen", row, "Thema"))
            .ToList();
        var page = TopicPage(
            3,
            Word("Thema", 20, 700),
            Word("Bemerkungen", 175, 700),
            // Der physische Text der ersten Zeile fehlt. Nur die zweite
            // identische Zeile ist im PDF vorhanden.
            Word("gleich", 24, 610));

        var mapping = DossierOutputPreviewTableCellMapper.Build(
            [Input(page, TopicTemplatePage())],
            targets,
            _ =>
            [
                TopicRow("gleich", ""),
                TopicRow("gleich", "")
            ])[3];

        Assert.Empty(mapping.Areas);
        Assert.Empty(mapping.ReplacedPhysicalTargets);
    }

    [Fact]
    public void Natuerlicher_Seitenumbruch_erlaubt_identische_Folgezeilen()
    {
        var targets = Enumerable.Range(0, 3)
            .SelectMany(row => new[]
            {
                DossierPreviewTarget.RowCell("Themen", row, "Thema"),
                DossierPreviewTarget.RowCell("Themen", row, "Text")
            })
            .ToList();
        var template = TopicTemplatePage(pageHeightPx: 167, marginPx: 40);
        var first = new DossierOutputPreviewPage(
            3,
            612,
            125,
            "",
            [
                Word("Thema", 20, 110),
                Word("Bemerkungen", 175, 110),
                Word("gleich", 24, 70),
                Word("gleich", 180, 70)
            ]);
        var continuation = new DossierOutputPreviewPage(
            4,
            612,
            200,
            "",
            [
                Word("gleich", 24, 145),
                Word("gleich", 180, 145),
                Word("gleich", 24, 95),
                Word("gleich", 180, 95)
            ]);

        var mappings = DossierOutputPreviewTableCellMapper.Build(
            [Input(first, template), Input(continuation, template)],
            targets,
            _ =>
            [
                TopicRow("gleich", "gleich"),
                TopicRow("gleich", "gleich"),
                TopicRow("gleich", "gleich")
            ]);

        Assert.Equal(2, mappings[3].Areas.Count(area => area.Target.RowIndex == 0));
        Assert.Equal(2, mappings[4].Areas.Count(area => area.Target.RowIndex == 1));
        Assert.Equal(2, mappings[4].Areas.Count(area => area.Target.RowIndex == 2));
    }

    [Fact]
    public void Vollstaendig_leere_Zeile_auf_Folgeseite_bleibt_anklickbar()
    {
        var template = TopicTemplatePage(pageHeightPx: 267, marginPx: 80);
        var targets = Enumerable.Range(0, 2)
            .SelectMany(row => new[]
            {
                DossierPreviewTarget.RowCell("Themen", row, "Thema"),
                DossierPreviewTarget.RowCell("Themen", row, "Text")
            })
            .ToList();
        var first = new DossierOutputPreviewPage(
            3,
            612,
            200,
            "",
            [
                Word("Thema", 20, 150),
                Word("Bemerkungen", 175, 150),
                Word("Ausgangslage", 24, 110),
                Word("bekannt", 180, 110)
            ]);
        var continuation = new DossierOutputPreviewPage(4, 612, 200, "", []);

        var mappings = DossierOutputPreviewTableCellMapper.Build(
            [Input(first, template), Input(continuation, template)],
            targets,
            _ =>
            [
                TopicRow("Ausgangslage", "bekannt"),
                TopicRow("", "")
            ]);

        Assert.DoesNotContain(mappings[3].Areas, area => area.Target.RowIndex == 1);
        Assert.Equal(
            2,
            mappings[4].Areas.Count(area => area.Target.RowIndex == 1));
    }

    [Fact]
    public void Unsicherer_Seitenuebergang_ratet_keine_identischen_Folgezeilen()
    {
        var targets = Enumerable.Range(0, 2)
            .SelectMany(row => new[]
            {
                DossierPreviewTarget.RowCell("Themen", row, "Thema"),
                DossierPreviewTarget.RowCell("Themen", row, "Text")
            })
            .ToList();
        var template = TopicTemplatePage();
        var first = TopicPage(
            3,
            Word("Thema", 20, 700),
            Word("Bemerkungen", 175, 700),
            Word("PDF-Abweichung", 24, 660));
        var continuation = TopicPage(4, Word("gleich", 24, 730));

        var mappings = DossierOutputPreviewTableCellMapper.Build(
            [Input(first, template), Input(continuation, template)],
            targets,
            _ =>
            [
                TopicRow("gleich", ""),
                TopicRow("gleich", "")
            ]);

        Assert.Empty(mappings[3].Areas);
        Assert.Empty(mappings[4].Areas);
    }

    [Fact]
    public void Gemappte_Tabelle_entfernt_unsichere_Wortziele_dieser_Tabelle()
    {
        var wrong = DossierPreviewTarget.RowCell("Themen", 0, "Text");
        var coarseRow = DossierPreviewTarget.Row("Themen", 0);
        var editableLabel = DossierPreviewTarget.RowCell(
            "Themen",
            0,
            "Telefon_Beschriftung");
        var field = DossierPreviewTarget.Field("Aktennotiz");
        var hits = new Dictionary<int, IReadOnlyList<DossierPreviewTarget>>
        {
            [0] = [wrong, coarseRow, editableLabel, field]
        };

        var filtered = DossierOutputPreviewTableCellMapper.RemoveMappedTableTargets(
            hits,
            new HashSet<DossierPreviewTarget> { wrong });

        Assert.Equal([editableLabel, field], filtered[0]);
    }

    [Fact]
    public void Unsichere_Zeile_ersetzt_keine_bisherigen_Wortziele()
    {
        var target = DossierPreviewTarget.RowCell("Themen", 0, "Text");
        var page = TopicPage(
            3,
            Word("Thema", 20, 700),
            Word("Bemerkungen", 175, 700),
            Word("Ausgangslage", 24, 660),
            Word("nur", 180, 660));

        var mapping = DossierOutputPreviewTableCellMapper.Build(
            [Input(page, TopicTemplatePage())],
            [DossierPreviewTarget.RowCell("Themen", 0, "Thema"), target],
            _ => [TopicRow("Ausgangslage", "vollstaendiger anderer Text")])[3];

        Assert.Empty(mapping.Areas);
        Assert.Empty(mapping.ReplacedPhysicalTargets);
    }

    [Fact]
    public void Nur_tatsaechlich_gemappte_Zeile_ersetzt_ihre_alten_Wortziele()
    {
        var first = DossierPreviewTarget.RowCell("Themen", 0, "Text");
        var uncertainNext = DossierPreviewTarget.RowCell("Themen", 1, "Text");
        var filtered = DossierOutputPreviewTableCellMapper.RemoveMappedTableTargets(
            new Dictionary<int, IReadOnlyList<DossierPreviewTarget>>
            {
                [0] = [first],
                [1] = [uncertainNext]
            },
            new HashSet<DossierPreviewTarget> { first });

        Assert.False(filtered.ContainsKey(0));
        Assert.Equal([uncertainNext], filtered[1]);
    }

    private static DossierOutputPreviewTablePageInput Input(
        DossierOutputPreviewPage page,
        DossierPreviewPage template)
        => new(
            page,
            [template],
            new Dictionary<int, IReadOnlyList<DossierPreviewTarget>>());

    private static DossierOutputPreviewPage TopicPage(
        int number,
        params DossierOutputPreviewWord[] words)
        => new(number, 612, 792, "", words);

    private static DossierOutputPreviewPage OwnerPage()
        => new(
            3,
            612,
            792,
            "",
            [
                Word("Haus", 20, 700),
                Word("Parzelle", 95, 700),
                Word("Eigentuemer", 170, 700),
                Word("30", 24, 660),
                Word("30", 100, 660),
                Word("Hans", 175, 660),
                Word("Muster", 205, 660)
            ]);

    private static DossierPreviewPage TopicTemplatePage(
        double pageHeightPx = 1056,
        double marginPx = 40)
    {
        var table = new DossierPreviewTable(
            [200, 550],
            0,
            [new DossierPreviewTableRow([Cell("Thema"), Cell("Bemerkungen")])],
            "Themen",
            ["Thema", "Text"],
            new DossierPreviewTableRow(
                [Cell("{{#Themen}}{{Thema}}"), Cell("{{Text}}")],
                61.67),
            1);

        return new DossierPreviewPage(
            3,
            "Informationen Sanierung",
            new DossierPreviewGeometry(
                816,
                pageHeightPx,
                DossierPreviewEdges.All(marginPx)),
            [table],
            ["Themen"]);
    }

    private static DossierPreviewPage OwnerTemplatePage()
    {
        var table = new DossierPreviewTable(
            [100, 100, 300],
            0,
            [new DossierPreviewTableRow(
            [
                Cell("Haus"),
                Cell("Parzelle"),
                Cell("Eigentuemer")
            ])],
            "Eigentuemer",
            ["Haus_Nr", "Pz_Nr", "Eigentuemer_Zelle"],
            new DossierPreviewTableRow(
            [
                Cell("{{#Eigentuemer}}{{Haus_Nr}}"),
                Cell("{{Pz_Nr}}"),
                Cell("{{Eigentuemer_Zelle}}")
            ]),
            1);

        return new DossierPreviewPage(
            3,
            "Eigentumsverhaeltnisse",
            new DossierPreviewGeometry(
                816,
                1056,
                DossierPreviewEdges.All(40)),
            [table],
            ["Eigentuemer"]);
    }

    private static DossierPreviewTableCell Cell(string text)
        => new(
            [new DossierPreviewParagraph(
                [DossierPreviewRun.Literal(text, DossierPreviewRunFormat.Default)],
                DossierPreviewParagraphFormat.Default)],
            DossierPreviewEdges.All(7.2),
            DossierPreviewEdges.All(0.5),
            null,
            1);

    private static IReadOnlyDictionary<string, string> TopicRow(string title, string text)
        => new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["Thema"] = title,
            ["Text"] = text
        };

    private static DossierOutputPreviewWord Word(string text, double left, double bottom)
        => new(text, left, bottom, left + Math.Max(12, text.Length * 5), bottom + 11);
}
