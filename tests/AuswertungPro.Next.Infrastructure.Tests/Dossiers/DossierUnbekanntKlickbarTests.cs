using AuswertungPro.Next.Application.Dossiers;
using AuswertungPro.Next.Application.Dossiers.Preview;

namespace AuswertungPro.Next.Infrastructure.Tests.Dossiers;

/// <summary>
/// Seit fehlende Angaben als „unbekannt" gedruckt werden, tragen viele Zellen der
/// Themen-Tabelle denselben Text. Pascal konnte diese Zellen in der Vorschau nicht
/// mehr anklicken.
///
/// Die physische Tabellenzuordnung darf sich davon nicht beirren lassen: Sie kennt
/// Spalte und Zeilenband und braucht den Text nur als Anker innerhalb dieser Flaeche.
/// </summary>
public sealed class DossierUnbekanntKlickbarTests
{
    [Fact]
    public void Vier_Themen_mit_gleichem_Text_bleiben_einzeln_anklickbar()
    {
        var targets = Enumerable.Range(0, 4)
            .SelectMany(row => new[]
            {
                DossierPreviewTarget.RowCell("Themen", row, "Thema"),
                DossierPreviewTarget.RowCell("Themen", row, "Text")
            })
            .ToList();

        var page = TopicPage(
            Word("Thema", 20, 700),
            Word("Bemerkungen", 175, 700),
            Word("Ausführungstermin", 24, 660),
            Word("unbekannt", 180, 660),
            Word("Ansprechpartner", 24, 610),
            Word("unbekannt", 180, 610),
            Word("Unternehmer", 24, 560),
            Word("unbekannt", 180, 560),
            Word("Bauleitung", 24, 510),
            Word("unbekannt", 180, 510));

        var mapping = DossierOutputPreviewTableCellMapper.Build(
            [Input(page)],
            targets,
            _ =>
            [
                TopicRow("Ausführungstermin", "unbekannt"),
                TopicRow("Ansprechpartner", "unbekannt"),
                TopicRow("Unternehmer", "unbekannt"),
                TopicRow("Bauleitung", "unbekannt")
            ])[3];

        for (var row = 0; row < 4; row++)
        {
            var ziel = DossierPreviewTarget.RowCell("Themen", row, "Text");
            Assert.True(
                mapping.Areas.Any(area => area.Target == ziel),
                $"Zeile {row}: die Bemerkungszelle mit 'unbekannt' ist nicht anklickbar. "
                + "Vorhandene Flaechen: "
                + string.Join(", ", mapping.Areas.Select(a => a.Target.CellKey + "#" + a.Target.RowIndex)));
        }
    }

    [Fact]
    public void Jede_Flaeche_liegt_in_ihrer_eigenen_Zeile()
    {
        // Sonst zeigen vier Klicks auf dieselbe Zeile - schlimmer als gar kein Treffer.
        var targets = Enumerable.Range(0, 3)
            .Select(row => DossierPreviewTarget.RowCell("Themen", row, "Text"))
            .ToList();

        var page = TopicPage(
            Word("Thema", 20, 700),
            Word("Bemerkungen", 175, 700),
            Word("Erstes", 24, 660),
            Word("unbekannt", 180, 660),
            Word("Zweites", 24, 610),
            Word("unbekannt", 180, 610),
            Word("Drittes", 24, 560),
            Word("unbekannt", 180, 560));

        var mapping = DossierOutputPreviewTableCellMapper.Build(
            [Input(page)],
            targets,
            _ =>
            [
                TopicRow("Erstes", "unbekannt"),
                TopicRow("Zweites", "unbekannt"),
                TopicRow("Drittes", "unbekannt")
            ])[3];

        var oberkanten = Enumerable.Range(0, 3)
            .Select(row => mapping.Areas
                .Single(area => area.Target == DossierPreviewTarget.RowCell("Themen", row, "Text"))
                .Top)
            .ToList();

        // PDF zaehlt von unten: die erste Zeile hat die groesste Oberkante.
        Assert.Equal(oberkanten.Count, oberkanten.Distinct().Count());
        Assert.True(
            oberkanten[0] > oberkanten[1] && oberkanten[1] > oberkanten[2],
            "Die Zeilen stehen nicht in Leserichtung: " + string.Join(", ", oberkanten));
    }

    private static DossierOutputPreviewTablePageInput Input(DossierOutputPreviewPage page)
        => new(page, [TopicTemplatePage()], new Dictionary<int, IReadOnlyList<DossierPreviewTarget>>());

    private static DossierOutputPreviewPage TopicPage(params DossierOutputPreviewWord[] words)
        => new(3, 612, 792, "", words);

    private static DossierPreviewPage TopicTemplatePage()
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
            new DossierPreviewGeometry(816, 1056, DossierPreviewEdges.All(40)),
            [table],
            ["Themen"]);
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
