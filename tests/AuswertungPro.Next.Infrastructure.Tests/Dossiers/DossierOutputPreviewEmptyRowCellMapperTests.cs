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
