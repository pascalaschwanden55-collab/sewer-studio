using AuswertungPro.Next.Application.Dossiers;
using AuswertungPro.Next.Application.Dossiers.Preview;

namespace AuswertungPro.Next.Infrastructure.Tests.Dossiers;

public sealed class DossierOutputPreviewEmptyRowCellMapperTests
{
    [Fact]
    public void Leere_Aenderungszeile_erhaelt_vier_getrennte_Klickflaechen()
    {
        var page = OutputPage();
        var template = TemplatePage();
        var targets = CellTargets(0);

        var areas = DossierOutputPreviewEmptyRowCellMapper.Build(
            page,
            [template],
            targets,
            _ => [EmptyRow()],
            new Dictionary<int, IReadOnlyList<DossierPreviewTarget>>());

        Assert.Equal(4, areas.Count);
        Assert.Equal(targets, areas.Select(area => area.Target));
        Assert.All(areas, area => Assert.True(area.Top <= 700));
        Assert.All(areas.Zip(areas.Skip(1)), pair =>
            Assert.True(pair.First.Right <= pair.Second.Left));
    }

    [Fact]
    public void Nur_leere_Zellen_ohne_Worttreffer_werden_ergaenzt()
    {
        var page = OutputPage() with
        {
            Words = [.. OutputPage().Words, Word("1", 24, 660)]
        };
        var targets = CellTargets(0);
        var rows = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["Version"] = "1",
            ["Datum"] = "",
            ["Visum"] = "",
            ["Aenderung"] = ""
        };
        var hits = new Dictionary<int, IReadOnlyList<DossierPreviewTarget>>
        {
            [6] = [targets[0]]
        };

        var areas = DossierOutputPreviewEmptyRowCellMapper.Build(
            page,
            [TemplatePage()],
            targets,
            _ => [rows],
            hits);

        Assert.Equal(targets.Skip(1), areas.Select(area => area.Target));
    }

    [Fact]
    public void Fehlender_oder_doppelter_Tabellenkopf_erzeugt_keine_Flaeche()
    {
        var missing = OutputPage() with
        {
            Words = OutputPage().Words.Where(word => word.Text != "Visum").ToList()
        };
        var duplicate = OutputPage() with
        {
            Words = [.. OutputPage().Words, Word("Datum", 20, 500)]
        };

        Assert.Empty(Build(missing));
        Assert.Empty(Build(duplicate));
    }

    [Fact]
    public void Zwei_leere_Zeilen_erhalten_getrennte_Baender()
    {
        var firstTargets = CellTargets(0);
        var secondTargets = CellTargets(1);

        var areas = DossierOutputPreviewEmptyRowCellMapper.Build(
            OutputPage(),
            [TemplatePage()],
            [.. firstTargets, .. secondTargets],
            _ => [EmptyRow(), EmptyRow()],
            new Dictionary<int, IReadOnlyList<DossierPreviewTarget>>());

        Assert.Equal(8, areas.Count);
        var firstRow = areas.Where(area => area.Target.RowIndex == 0).ToList();
        var secondRow = areas.Where(area => area.Target.RowIndex == 1).ToList();
        Assert.Equal(4, firstRow.Count);
        Assert.Equal(4, secondRow.Count);
        Assert.True(secondRow.Max(area => area.Top)
            <= firstRow.Min(area => area.Bottom));
    }

    [Fact]
    public void Umbenannter_Tabellenkopf_bleibt_als_Anker_verwendbar()
    {
        var page = OutputPage() with
        {
            Words = OutputPage().Words
                .Select(word => word.Text == "Datum"
                    ? word with { Text = "Revisionsdatum" }
                    : word)
                .ToList()
        };
        var datumIndex = page.Words
            .Select((word, index) => (word, index))
            .Single(item => item.word.Text == "Revisionsdatum")
            .index;
        var hits = new Dictionary<int, IReadOnlyList<DossierPreviewTarget>>
        {
            [datumIndex] = [DossierPreviewTarget.Literal("Datum")]
        };

        var areas = DossierOutputPreviewEmptyRowCellMapper.Build(
            page,
            [TemplatePage()],
            CellTargets(0),
            _ => [EmptyRow()],
            hits);

        Assert.Equal(4, areas.Count);
    }

    [Fact]
    public void Leere_Bemerkungszellen_der_Informationstabelle_sind_direkt_anklickbar()
    {
        var page = TopicOutputPage();
        var targets = TopicCellTargets();
        var hits = new Dictionary<int, IReadOnlyList<DossierPreviewTarget>>
        {
            [2] = [DossierPreviewTarget.RowCell("Themen", 0, "Thema")],
            [3] = [DossierPreviewTarget.RowCell("Themen", 1, "Thema")],
            [4] = [DossierPreviewTarget.RowCell("Themen", 2, "Thema")],
            [5] = [DossierPreviewTarget.RowCell("Themen", 2, "Text")]
        };

        var areas = DossierOutputPreviewEmptyRowCellMapper.Build(
            page,
            [TopicTemplatePage()],
            targets,
            key => string.Equals(key, "Themen", StringComparison.OrdinalIgnoreCase)
                ? TopicRows()
                : [],
            hits);

        Assert.Equal(
            [
                DossierPreviewTarget.RowCell("Themen", 0, "Text"),
                DossierPreviewTarget.RowCell("Themen", 1, "Text")
            ],
            areas.Select(area => area.Target));

        // Die Klickflaeche deckt die ganze sichtbare Tabellenzeile ab und
        // nicht bloss einen schmalen Streifen neben dem Titelwort.
        Assert.True(areas[0].Top > page.Words[2].Top + 10);
        Assert.Equal(areas[0].Bottom, areas[1].Top, 6);
        Assert.All(areas, area => Assert.True(area.Left > 150));
    }

    [Fact]
    public void Letzte_leere_Bemerkungszeile_deckt_die_Mindesthoehe_der_Vorlage_ab()
    {
        var page = TopicOutputPage() with
        {
            Words = TopicOutputPage().Words.Take(3).ToList()
        };
        var target = DossierPreviewTarget.RowCell("Themen", 0, "Text");
        var hits = new Dictionary<int, IReadOnlyList<DossierPreviewTarget>>
        {
            [2] = [DossierPreviewTarget.RowCell("Themen", 0, "Thema")]
        };

        var area = Assert.Single(DossierOutputPreviewEmptyRowCellMapper.Build(
            page,
            [TopicTemplatePage()],
            [DossierPreviewTarget.RowCell("Themen", 0, "Thema"), target],
            _ => [TopicRow("Ausfuehrungstermin", "")],
            hits));

        Assert.Equal(target, area.Target);
        Assert.True(area.Top - area.Bottom >= 46.2);
    }

    [Fact]
    public void Mehrzeiliger_Thementitel_spannt_die_leere_Bemerkungszelle_ganz_auf()
    {
        var page = TopicOutputPage() with
        {
            Words =
            [
                Word("Thema", 20, 700),
                Word("Bemerkungen", 175, 700),
                Word("Behinderungen", 24, 660),
                Word("Zugaenge", 24, 646),
                Word("Verkehrsfuehrung", 24, 632),
                Word("Fussgaengerfuehrung", 24, 618)
            ]
        };
        var titleTarget = DossierPreviewTarget.RowCell("Themen", 0, "Thema");
        var textTarget = DossierPreviewTarget.RowCell("Themen", 0, "Text");
        var hits = Enumerable.Range(2, 4).ToDictionary(
            index => index,
            _ => (IReadOnlyList<DossierPreviewTarget>)[titleTarget]);

        var area = Assert.Single(DossierOutputPreviewEmptyRowCellMapper.Build(
            page,
            [TopicTemplatePage()],
            [titleTarget, textTarget],
            _ =>
            [
                TopicRow(
                    "Behinderungen Zugaenge Verkehrsfuehrung Fussgaengerfuehrung",
                    "")
            ],
            hits));

        Assert.Equal(textTarget, area.Target);
        Assert.True(area.Top > page.Words[2].Top);
        Assert.True(area.Bottom < page.Words[5].Bottom);
    }

    [Fact]
    public void Gefuellte_mehrzeilige_Bemerkung_erhaelt_keine_zusaetzliche_Leerflaeche()
    {
        var page = TopicOutputPage() with
        {
            Words =
            [
                Word("Thema", 20, 700),
                Word("Bemerkungen", 175, 700),
                Word("Ausgangslage", 24, 660),
                Word("Erste", 175, 660),
                Word("Zeile", 210, 660),
                Word("zweite", 175, 646),
                Word("Zeile", 215, 646)
            ]
        };
        var titleTarget = DossierPreviewTarget.RowCell("Themen", 0, "Thema");
        var textTarget = DossierPreviewTarget.RowCell("Themen", 0, "Text");
        var hits = new Dictionary<int, IReadOnlyList<DossierPreviewTarget>>
        {
            [2] = [titleTarget],
            [3] = [textTarget],
            [4] = [textTarget],
            [5] = [textTarget],
            [6] = [textTarget]
        };

        var additional = DossierOutputPreviewEmptyRowCellMapper.Build(
            page,
            [TopicTemplatePage()],
            [titleTarget, textTarget],
            _ => [TopicRow("Ausgangslage", "Erste Zeile zweite Zeile")],
            hits);

        Assert.Empty(additional);
        Assert.Single(DossierOutputPreviewHitAreaBuilder.Build(page, hits)
            .Where(area => area.Target == textTarget));
    }

    [Fact]
    public void Unvollstaendig_erkannte_hohe_Zeile_wird_nicht_uebersprungen()
    {
        var page = TopicOutputPage() with
        {
            Words =
            [
                Word("Thema", 20, 700),
                Word("Bemerkungen", 175, 700),
                Word("Ausgangslage", 24, 660),
                Word("Langer", 175, 660),
                Word("Ansprechpartner", 24, 590)
            ]
        };
        var firstTitle = DossierPreviewTarget.RowCell("Themen", 0, "Thema");
        var firstText = DossierPreviewTarget.RowCell("Themen", 0, "Text");
        var secondTitle = DossierPreviewTarget.RowCell("Themen", 1, "Thema");
        var secondText = DossierPreviewTarget.RowCell("Themen", 1, "Text");
        var hits = new Dictionary<int, IReadOnlyList<DossierPreviewTarget>>
        {
            [2] = [firstTitle],
            // Nur ein Suchanker des langen Textes: seine wirkliche Hoehe ist
            // damit unbekannt und die folgende Zeile darf nicht geraten werden.
            [3] = [firstText],
            [4] = [secondTitle]
        };

        var areas = DossierOutputPreviewEmptyRowCellMapper.Build(
            page,
            [TopicTemplatePage()],
            [firstTitle, firstText, secondTitle, secondText],
            _ =>
            [
                TopicRow("Ausgangslage", "Langer Text ueber mehrere Zeilen"),
                TopicRow("Ansprechpartner", "")
            ],
            hits);

        Assert.Empty(areas);
    }

    [Fact]
    public void Treffer_aus_der_falschen_Spalte_wird_nicht_als_Zeilenanker_verwendet()
    {
        var page = TopicOutputPage() with
        {
            Words =
            [
                Word("Thema", 20, 700),
                Word("Bemerkungen", 175, 700),
                Word("Ausfuehrungstermin", 175, 660)
            ]
        };
        var titleTarget = DossierPreviewTarget.RowCell("Themen", 0, "Thema");
        var textTarget = DossierPreviewTarget.RowCell("Themen", 0, "Text");

        var areas = DossierOutputPreviewEmptyRowCellMapper.Build(
            page,
            [TopicTemplatePage()],
            [titleTarget, textTarget],
            _ => [TopicRow("Ausfuehrungstermin", "")],
            new Dictionary<int, IReadOnlyList<DossierPreviewTarget>>
            {
                [2] = [titleTarget]
            });

        Assert.Empty(areas);
    }

    [Fact]
    public void Weit_entfernter_Treffer_wird_nicht_auf_eine_plausible_Zeile_gekuerzt()
    {
        var page = TopicOutputPage() with
        {
            Words =
            [
                Word("Thema", 20, 700),
                Word("Bemerkungen", 175, 700),
                Word("Ausfuehrungstermin", 24, 500)
            ]
        };
        var titleTarget = DossierPreviewTarget.RowCell("Themen", 0, "Thema");
        var textTarget = DossierPreviewTarget.RowCell("Themen", 0, "Text");

        var areas = DossierOutputPreviewEmptyRowCellMapper.Build(
            page,
            [TopicTemplatePage()],
            [titleTarget, textTarget],
            _ => [TopicRow("Ausfuehrungstermin", "")],
            new Dictionary<int, IReadOnlyList<DossierPreviewTarget>>
            {
                [2] = [titleTarget]
            });

        Assert.Empty(areas);
    }

    [Fact]
    public void Leere_Vorgaengerzeile_verschiebt_die_erwartete_Folgeposition()
    {
        var page = TopicOutputPage() with
        {
            Words =
            [
                Word("Thema", 20, 700),
                Word("Bemerkungen", 175, 700),
                // Dieser Treffer liegt noch in der ersten, leeren Zeile und
                // darf deshalb nicht als Anker der zweiten Zeile gelten.
                Word("Ansprechpartner", 24, 670)
            ]
        };
        var targets = Enumerable.Range(0, 2)
            .SelectMany(row => new[]
            {
                DossierPreviewTarget.RowCell("Themen", row, "Thema"),
                DossierPreviewTarget.RowCell("Themen", row, "Text")
            })
            .ToList();

        var areas = DossierOutputPreviewEmptyRowCellMapper.Build(
            page,
            [TopicTemplatePage()],
            targets,
            _ =>
            [
                TopicRow("", ""),
                TopicRow("Ansprechpartner", "")
            ],
            new Dictionary<int, IReadOnlyList<DossierPreviewTarget>>
            {
                [2] = [DossierPreviewTarget.RowCell("Themen", 1, "Thema")]
            });

        Assert.DoesNotContain(areas, area => area.Target.RowIndex == 1);
    }

    [Fact]
    public void Gefuellte_hohe_Zeile_und_folgende_Leerzelle_ueberlappen_nicht()
    {
        var page = TopicOutputPage() with
        {
            Words =
            [
                Word("Thema", 20, 700),
                Word("Bemerkungen", 175, 700),
                Word("Ausgangslage", 24, 660),
                Word("Erste", 175, 660),
                Word("Zeile", 210, 660),
                Word("zweite", 175, 646),
                Word("Zeile", 215, 646),
                Word("Ansprechpartner", 24, 580)
            ]
        };
        var firstTitle = DossierPreviewTarget.RowCell("Themen", 0, "Thema");
        var firstText = DossierPreviewTarget.RowCell("Themen", 0, "Text");
        var secondTitle = DossierPreviewTarget.RowCell("Themen", 1, "Thema");
        var secondText = DossierPreviewTarget.RowCell("Themen", 1, "Text");
        var hits = new Dictionary<int, IReadOnlyList<DossierPreviewTarget>>
        {
            [2] = [firstTitle],
            [3] = [firstText],
            [4] = [firstText],
            [5] = [firstText],
            [6] = [firstText],
            [7] = [secondTitle]
        };

        var blank = Assert.Single(DossierOutputPreviewEmptyRowCellMapper.Build(
            page,
            [TopicTemplatePage()],
            [firstTitle, firstText, secondTitle, secondText],
            _ =>
            [
                TopicRow("Ausgangslage", "Erste Zeile zweite Zeile"),
                TopicRow("Ansprechpartner", "")
            ],
            hits));
        var filled = Assert.Single(DossierOutputPreviewHitAreaBuilder.Build(page, hits)
            .Where(area => area.Target == firstText));

        Assert.Equal(secondText, blank.Target);
        Assert.True(filled.Bottom >= blank.Top);
    }

    [Fact]
    public void Aenderungen_und_Themen_auf_demselben_Blatt_erhalten_beide_Klickflaechen()
    {
        var changes = TemplatePage();
        var topics = TopicTemplatePage();
        var editorPage = changes with
        {
            Blocks = [.. changes.Blocks, .. topics.Blocks],
            FieldKeys = ["Aenderungen", "Themen"]
        };
        var page = OutputPage() with
        {
            Words =
            [
                .. OutputPage().Words,
                Word("Thema", 20, 500),
                Word("Bemerkungen", 175, 500),
                Word("Ausfuehrungstermin", 24, 460),
                Word("Ansprechpartner", 24, 410),
                Word("Unternehmer", 24, 360),
                Word("unbekannt", 175, 360)
            ]
        };
        var topicTargets = TopicCellTargets();
        var hits = new Dictionary<int, IReadOnlyList<DossierPreviewTarget>>
        {
            [8] = [DossierPreviewTarget.RowCell("Themen", 0, "Thema")],
            [9] = [DossierPreviewTarget.RowCell("Themen", 1, "Thema")],
            [10] = [DossierPreviewTarget.RowCell("Themen", 2, "Thema")],
            [11] = [DossierPreviewTarget.RowCell("Themen", 2, "Text")]
        };

        var areas = DossierOutputPreviewEmptyRowCellMapper.Build(
            page,
            [editorPage],
            [.. CellTargets(0), .. topicTargets],
            key => string.Equals(key, "Aenderungen", StringComparison.OrdinalIgnoreCase)
                ? [EmptyRow()]
                : TopicRows(),
            hits);

        Assert.Equal(4, areas.Count(area => area.Target.Key == "Aenderungen"));
        Assert.Equal(2, areas.Count(area => area.Target.Key == "Themen"));
    }

    [Fact]
    public void Mehrdeutige_Thementabelle_blockiert_eindeutige_Aenderungszellen_nicht()
    {
        var aenderungen = TemplatePage();
        var themen = TopicTemplatePage();
        var editorPage = aenderungen with
        {
            Blocks =
            [
                .. aenderungen.Blocks,
                .. themen.Blocks,
                .. themen.Blocks
            ],
            FieldKeys = ["Aenderungen", "Themen"]
        };

        var areas = DossierOutputPreviewEmptyRowCellMapper.Build(
            OutputPage(),
            [editorPage],
            CellTargets(0),
            key => string.Equals(key, "Aenderungen", StringComparison.OrdinalIgnoreCase)
                ? [EmptyRow()]
                : TopicRows(),
            new Dictionary<int, IReadOnlyList<DossierPreviewTarget>>());

        Assert.Equal(CellTargets(0), areas.Select(area => area.Target));
    }

    private static IReadOnlyList<DossierOutputPreviewHitArea> Build(
        DossierOutputPreviewPage page)
        => DossierOutputPreviewEmptyRowCellMapper.Build(
            page,
            [TemplatePage()],
            CellTargets(0),
            _ => [EmptyRow()],
            new Dictionary<int, IReadOnlyList<DossierPreviewTarget>>());

    private static DossierOutputPreviewPage OutputPage()
        => new(
            2,
            612,
            792,
            "",
            [
                Word("Version", 20, 700),
                Word("Datum", 70, 700),
                Word("Visum", 140, 700),
                Word("Art", 182, 700),
                Word("der", 202, 700),
                Word("Änderung", 222, 700)
            ]);

    private static DossierPreviewPage TemplatePage()
    {
        var header = new DossierPreviewTableRow(
        [
            Cell("Version"),
            Cell("Datum"),
            Cell("Visum"),
            Cell("Art der Änderung")
        ]);
        var repeat = new DossierPreviewTableRow(
        [
            Cell("{{Version}}"),
            Cell("{{Datum}}"),
            Cell("{{Visum}}"),
            Cell("{{Aenderung}}")
        ]);
        var table = new DossierPreviewTable(
            [66, 94, 57, 399],
            0,
            [header],
            "Aenderungen",
            ["Version", "Datum", "Visum", "Aenderung"],
            repeat,
            1);

        return new DossierPreviewPage(
            2,
            "Änderungswesen",
            new DossierPreviewGeometry(794, 1123, DossierPreviewEdges.Zero),
            [table],
            ["Aenderungen"]);
    }

    private static DossierOutputPreviewPage TopicOutputPage()
        => new(
            3,
            612,
            792,
            "",
            [
                Word("Thema", 20, 700),
                Word("Bemerkungen", 175, 700),
                Word("Ausfuehrungstermin", 24, 660),
                Word("Ansprechpartner", 24, 610),
                Word("Unternehmer", 24, 560),
                Word("unbekannt", 175, 560)
            ]);

    private static DossierPreviewPage TopicTemplatePage()
    {
        var header = new DossierPreviewTableRow(
        [
            Cell("Thema"),
            Cell("Bemerkungen")
        ]);
        var repeat = new DossierPreviewTableRow(
        [
            Cell("{{#Themen}}{{Thema}}"),
            Cell("{{Text}}")
        ],
        61.67);
        var table = new DossierPreviewTable(
            [200, 550],
            0,
            [header],
            "Themen",
            ["Thema", "Text"],
            repeat,
            1);

        return new DossierPreviewPage(
            3,
            "Informationen Sanierung",
            new DossierPreviewGeometry(794, 1123, DossierPreviewEdges.Zero),
            [table],
            ["Themen"]);
    }

    private static IReadOnlyList<IReadOnlyDictionary<string, string>> TopicRows()
        =>
        [
            TopicRow("Ausfuehrungstermin", ""),
            TopicRow("Ansprechpartner", ""),
            TopicRow("Unternehmer", "unbekannt")
        ];

    private static IReadOnlyDictionary<string, string> TopicRow(string title, string text)
        => new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["Thema"] = title,
            ["Text"] = text
        };

    private static List<DossierPreviewTarget> TopicCellTargets()
        => Enumerable.Range(0, 3)
            .SelectMany(row => new[]
            {
                DossierPreviewTarget.RowCell("Themen", row, "Thema"),
                DossierPreviewTarget.RowCell("Themen", row, "Text")
            })
            .ToList();

    private static DossierPreviewTableCell Cell(string text)
        => new(
            [new DossierPreviewParagraph(
                [DossierPreviewRun.Literal(text, DossierPreviewRunFormat.Default)],
                DossierPreviewParagraphFormat.Default)],
            DossierPreviewEdges.All(7.2),
            DossierPreviewEdges.All(0.5),
            null,
            1);

    private static IReadOnlyDictionary<string, string> EmptyRow()
        => new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["Version"] = "",
            ["Datum"] = "",
            ["Visum"] = "",
            ["Aenderung"] = ""
        };

    private static List<DossierPreviewTarget> CellTargets(int row)
        =>
        [
            DossierPreviewTarget.RowCell("Aenderungen", row, "Version"),
            DossierPreviewTarget.RowCell("Aenderungen", row, "Datum"),
            DossierPreviewTarget.RowCell("Aenderungen", row, "Visum"),
            DossierPreviewTarget.RowCell("Aenderungen", row, "Aenderung")
        ];

    private static DossierOutputPreviewWord Word(string text, double left, double bottom)
        => new(text, left, bottom, left + Math.Max(12, text.Length * 5), bottom + 11);
}
