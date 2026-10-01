using AuswertungPro.Next.Application.Dossiers;
using AuswertungPro.Next.Application.Dossiers.Preview;

namespace AuswertungPro.Next.Infrastructure.Tests.Dossiers;

/// <summary>
/// Die Zuordnung erkennt eine Zelle bisher an ihrem TEXT. Findet sie den Text
/// nicht sicher wieder - weil er fehlt, weil er in der Nachbarspalte verschachtelt
/// steht oder weil dreizehn Zellen denselben Text tragen - liefert sie bewusst
/// nichts. Die Zelle ist dann nicht anklickbar.
///
/// Die unsichtbaren Word-Textmarken beenden das Raten: Sie stehen als benannte
/// Ziele mit exakter Position in der PDF. Wo eine Marke vorliegt, gilt sie.
/// Wo keine vorliegt, bleibt alles wie bisher.
/// </summary>
public sealed class DossierAnkerZuordnungTests
{
    [Fact]
    public void Ohne_Marke_bleibt_eine_unauffindbare_Zeile_unzugeordnet()
    {
        // Ausgangslage festhalten: Der Text der Zeilen steht nicht im Blatt.
        var mapping = Baue(mitMarken: false);

        Assert.Empty(mapping.Areas);
    }

    [Fact]
    public void Mit_Marke_wird_dieselbe_Zeile_anklickbar()
    {
        var mapping = Baue(mitMarken: true);

        foreach (var zeile in new[] { 0, 1, 2 })
        {
            var ziel = DossierPreviewTarget.RowCell("Themen", zeile, "Text");
            Assert.True(
                mapping.Areas.Any(flaeche => flaeche.Target == ziel),
                $"Zeile {zeile} ist trotz Marke nicht anklickbar.");
        }
    }

    [Fact]
    public void Jede_Zeile_bekommt_ihre_eigene_Flaeche()
    {
        var mapping = Baue(mitMarken: true);

        var oberkanten = new[] { 0, 1, 2 }
            .Select(zeile => mapping.Areas
                .Single(flaeche => flaeche.Target == DossierPreviewTarget.RowCell("Themen", zeile, "Text"))
                .Top)
            .ToList();

        // PDF zaehlt von unten: die erste Zeile hat die groesste Oberkante.
        Assert.Equal(3, oberkanten.Distinct().Count());
        Assert.True(
            oberkanten[0] > oberkanten[1] && oberkanten[1] > oberkanten[2],
            "Die Zeilen stehen nicht in Leserichtung: " + string.Join(", ", oberkanten));
    }

    [Fact]
    public void Eine_hohe_Zeile_bricht_die_Folgezeilen_nicht_ab()
    {
        // In der echten Ausgabe gemessen: Die Zeile „Ausgangslage" traegt einen
        // langen Text und ist rund 170 Punkte hoch, die uebrigen rund 47. Wer die
        // Zeilenhoehe aus der Vorlage fortschreibt, verliert ab dort jede Zeile.
        var mapping = Baue(mitMarken: true, markenYs: [660, 614, 450]);

        foreach (var zeile in new[] { 0, 1, 2 })
        {
            var ziel = DossierPreviewTarget.RowCell("Themen", zeile, "Text");
            Assert.True(
                mapping.Areas.Any(flaeche => flaeche.Target == ziel),
                $"Zeile {zeile} fehlt - eine hohe Vorgaengerzeile hat die Kette abgerissen.");
        }
    }

    [Fact]
    public void Eine_hohe_Zeile_bekommt_ihre_ganze_Hoehe()
    {
        var mapping = Baue(mitMarken: true, markenYs: [660, 614, 450]);

        var hoch = mapping.Areas.Single(flaeche =>
            flaeche.Target == DossierPreviewTarget.RowCell("Themen", 1, "Text"));

        // Von 614 bis 450 sind 164 Punkte - deutlich mehr als die Vorlagenzeile.
        Assert.True(
            hoch.Top - hoch.Bottom > 100,
            $"Die hohe Zeile ist nur {hoch.Top - hoch.Bottom:F0} Punkte hoch.");
    }

    [Fact]
    public void Eine_Marke_ausserhalb_des_Zeilenbands_wird_nicht_verwendet()
    {
        // Lieber keine Flaeche als eine an der falschen Stelle: Steht die Marke
        // weit oberhalb der erwarteten Zeile, gehoert sie zu einem anderen Blatt
        // oder einer anderen Tabelle.
        var mapping = Baue(mitMarken: true, markenY: 780);

        Assert.Empty(mapping.Areas);
    }

    private static DossierOutputPreviewTablePageMapping Baue(
        bool mitMarken,
        double? markenY = null,
        double[]? markenYs = null)
    {
        var targets = Enumerable.Range(0, 3)
            .SelectMany(zeile => new[]
            {
                DossierPreviewTarget.RowCell("Themen", zeile, "Thema"),
                DossierPreviewTarget.RowCell("Themen", zeile, "Text")
            })
            .ToList();

        // Nur der Tabellenkopf steht im Blatt - die Zeilentexte fehlen.
        var page = new DossierOutputPreviewPage(
            3, 612, 792, "",
            [Wort("Thema", 20, 700), Wort("Bemerkungen", 175, 700)]);

        var anker = new List<DossierPdfFieldAnchor>();
        if (mitMarken)
        {
            var starts = markenYs
                ?? (markenY is { } fest
                    ? [fest, fest, fest]
                    : new[] { 660d, 614d, 568d });
            for (var zeile = 0; zeile < 3; zeile++)
            {
                anker.Add(new DossierPdfFieldAnchor(
                    DossierPdfFieldMarker.Name(DossierPreviewTarget.RowCell("Themen", zeile, "Text")),
                    PageNumber: 3,
                    X: 180,
                    Y: starts[zeile]));
            }
        }

        var input = new DossierOutputPreviewTablePageInput(
            page,
            [Vorlagenseite()],
            new Dictionary<int, IReadOnlyList<DossierPreviewTarget>>(),
            anker);

        return DossierOutputPreviewTableCellMapper.Build(
            [input],
            targets,
            _ =>
            [
                Zeile("Ausführungstermin", "unbekannt"),
                Zeile("Ansprechpartner", "unbekannt"),
                Zeile("Unternehmer", "unbekannt")
            ])[3];
    }

    private static DossierPreviewPage Vorlagenseite()
    {
        var table = new DossierPreviewTable(
            [200, 550],
            0,
            [new DossierPreviewTableRow([Zelle("Thema"), Zelle("Bemerkungen")])],
            "Themen",
            ["Thema", "Text"],
            new DossierPreviewTableRow(
                [Zelle("{{#Themen}}{{Thema}}"), Zelle("{{Text}}")],
                61.67),
            1);

        return new DossierPreviewPage(
            3,
            "Informationen Sanierung",
            new DossierPreviewGeometry(816, 1056, DossierPreviewEdges.All(40)),
            [table],
            ["Themen"]);
    }

    private static DossierPreviewTableCell Zelle(string text)
        => new(
            [new DossierPreviewParagraph(
                [DossierPreviewRun.Literal(text, DossierPreviewRunFormat.Default)],
                DossierPreviewParagraphFormat.Default)],
            DossierPreviewEdges.All(7.2),
            DossierPreviewEdges.All(0.5),
            null,
            1);

    private static IReadOnlyDictionary<string, string> Zeile(string thema, string text)
        => new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["Thema"] = thema,
            ["Text"] = text
        };

    private static DossierOutputPreviewWord Wort(string text, double left, double bottom)
        => new(text, left, bottom, left + Math.Max(12, text.Length * 5), bottom + 11);
}
