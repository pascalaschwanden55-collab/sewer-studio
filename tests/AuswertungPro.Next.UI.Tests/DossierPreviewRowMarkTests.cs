using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Windows.Controls;

using AuswertungPro.Next.Application.Dossiers.Preview;
using AuswertungPro.Next.UI.Views.Rendering;

using Xunit;

namespace AuswertungPro.Next.UI.Tests;

/// <summary>
/// Prueft, dass jede erzeugte Tabellenzeile im Blatt ihre EIGENE Marke traegt.
///
/// Vorher hingen alle Zeilen an der Marke der Tabelle. Beim Tippen in einem
/// einzelnen Thema blinkte deshalb die ganze Informationstabelle rot auf,
/// statt der Zeile, an der gerade gearbeitet wird.
/// </summary>
public sealed class DossierPreviewRowMarkTests
{
    private static DossierPreviewTableCell Zelle(string text)
        => new(
            new[]
            {
                new DossierPreviewParagraph(
                    new[] { DossierPreviewRun.Literal(text, DossierPreviewRunFormat.Default) },
                    DossierPreviewParagraphFormat.Default)
            },
            DossierPreviewEdges.All(2),
            DossierPreviewEdges.All(1),
            null,
            1);

    private static DossierPreviewPage Seite()
    {
        var kopf = new DossierPreviewTableRow(new[] { Zelle("Thema"), Zelle("Bemerkungen") });

        var bauplan = new DossierPreviewTableRow(new[]
        {
            Zelle("{{#Themen}}{{Thema}}"),
            Zelle("{{Text}}")
        });

        var tabelle = new DossierPreviewTable(
            new[] { 150.0, 400.0 },
            0,
            new[] { kopf },
            "Themen",
            new[] { "Thema", "Text" },
            bauplan,
            1);

        return new DossierPreviewPage(
            1,
            "Informationen",
            new DossierPreviewGeometry(794, 1123, DossierPreviewEdges.All(76)),
            new DossierPreviewBlock[] { tabelle },
            new[] { "Themen" });
    }

    private static Dictionary<string, string> Zeile(string thema, string text)
        => new(StringComparer.OrdinalIgnoreCase) { ["Thema"] = thema, ["Text"] = text };

    private static IReadOnlyDictionary<DossierPreviewTarget, IReadOnlyList<Border>> Marken(
        IReadOnlyList<IReadOnlyDictionary<string, string>> zeilen)
    {
        IReadOnlyDictionary<DossierPreviewTarget, IReadOnlyList<Border>> ergebnis =
            new Dictionary<DossierPreviewTarget, IReadOnlyList<Border>>();

        RunOnSta(() => ergebnis = DossierPreviewPageRenderer.Render(
            Seite(), _ => string.Empty, _ => zeilen, _ => string.Empty).Frames);

        return ergebnis;
    }

    [Fact]
    public void Jede_erzeugte_Zeile_traegt_ihre_eigene_Marke()
    {
        var marken = Marken(new[]
        {
            Zeile("Ansprechpartner", "Abwasser Uri"),
            Zeile("Schäden", "Leitung undicht"),
            Zeile("Kostenschätzung", "offen")
        });

        Assert.True(marken.ContainsKey(DossierPreviewTarget.Row("Themen", 0)));
        Assert.True(marken.ContainsKey(DossierPreviewTarget.Row("Themen", 1)));
        Assert.True(marken.ContainsKey(DossierPreviewTarget.Row("Themen", 2)));
        Assert.False(marken.ContainsKey(DossierPreviewTarget.Row("Themen", 3)));
    }

    [Fact]
    public void Eine_Zeilenmarke_umfasst_nur_die_Zellen_dieser_Zeile()
    {
        var marken = Marken(new[]
        {
            Zeile("Ansprechpartner", "Abwasser Uri"),
            Zeile("Schäden", "Leitung undicht")
        });

        // Zwei Spalten je Zeile, sechs Zellen waeren beide Zeilen plus Kopf.
        Assert.Equal(2, marken[DossierPreviewTarget.Row("Themen", 0)].Count);
        Assert.Equal(2, marken[DossierPreviewTarget.Row("Themen", 1)].Count);

        // Und keine Zelle gehoert zwei Zeilen zugleich.
        Assert.Empty(marken[DossierPreviewTarget.Row("Themen", 0)]
            .Intersect(marken[DossierPreviewTarget.Row("Themen", 1)]));
    }

    [Fact]
    public void Die_Tabellenmarke_umfasst_weiterhin_alle_Zeilen()
    {
        // Sie wird gebraucht, wenn eine Zeile hinzukommt oder wegfaellt: dann
        // gibt es noch keine einzelne Zeile zum Betonen.
        var marken = Marken(new[]
        {
            Zeile("Ansprechpartner", "Abwasser Uri"),
            Zeile("Schäden", "Leitung undicht")
        });

        Assert.Equal(4, marken[DossierPreviewTarget.Field("Themen")].Count);
    }

    [Fact]
    public void Jede_Zelle_hat_eine_eigene_semantische_Marke()
    {
        var marken = Marken(new[]
        {
            Zeile("Ansprechpartner", "Abwasser Uri"),
            Zeile("Schaeden", "Leitung undicht")
        });

        Assert.Single(marken[DossierPreviewTarget.RowCell("Themen", 0, "Thema")]);
        Assert.Single(marken[DossierPreviewTarget.RowCell("Themen", 0, "Text")]);
        Assert.Single(marken[DossierPreviewTarget.RowCell("Themen", 1, "Thema")]);
        Assert.Single(marken[DossierPreviewTarget.RowCell("Themen", 1, "Text")]);
    }

    [Fact]
    public void Beim_Klick_gewinnt_die_genaue_Zelle_vor_Zeile_und_Tabelle()
    {
        var tabelle = DossierPreviewTarget.Field("Themen");
        var zeile = DossierPreviewTarget.Row("Themen", 4);
        var zelle = DossierPreviewTarget.RowCell("Themen", 4, "Text");

        var ziel = DossierPreviewTarget.SelectMostSpecific(
            new[] { tabelle, zeile, zelle },
            target => target == zelle || target == zeile || target == tabelle);

        Assert.Equal(zelle, ziel);
    }

    [Fact]
    public void Ein_bearbeiteter_Vorlagentext_bleibt_anklickbar()
    {
        var paragraph = new DossierPreviewParagraph(
            new[]
            {
                DossierPreviewRun.Literal(
                    "Informationen Sanierung", DossierPreviewRunFormat.Default)
            },
            DossierPreviewParagraphFormat.Default);
        var page = new DossierPreviewPage(
            1,
            "Informationen",
            new DossierPreviewGeometry(794, 1123, DossierPreviewEdges.All(76)),
            new DossierPreviewBlock[] { paragraph },
            Array.Empty<string>());

        IReadOnlyDictionary<DossierPreviewTarget, IReadOnlyList<Border>> marken =
            new Dictionary<DossierPreviewTarget, IReadOnlyList<Border>>();

        RunOnSta(() => marken = DossierPreviewPageRenderer.Render(
            page,
            _ => string.Empty,
            _ => Array.Empty<IReadOnlyDictionary<string, string>>(),
            _ => string.Empty,
            original => original == "Informationen Sanierung" ? "Informationen Baustelle" : null)
            .Frames);

        Assert.Single(marken[DossierPreviewTarget.Literal("Informationen Sanierung")]);
    }

    [Fact]
    public void Ein_Titel_im_Inhaltsverzeichnis_springt_zu_seinem_Textfeld()
    {
        var format = DossierPreviewParagraphFormat.Default with
        {
            IsTableOfContentsEntry = true
        };
        var paragraph = new DossierPreviewParagraph(
            new[]
            {
                DossierPreviewRun.Literal(
                    "1.Übersichtsplan Werkleitungen3", DossierPreviewRunFormat.Default)
            },
            format,
            TocEntry: new DossierPreviewTocEntry(
                "1.", "Übersichtsplan Werkleitungen", "3"));
        var page = new DossierPreviewPage(
            1,
            "Inhaltsverzeichnis",
            new DossierPreviewGeometry(794, 1123, DossierPreviewEdges.All(76)),
            new DossierPreviewBlock[] { paragraph },
            Array.Empty<string>());

        IReadOnlyDictionary<DossierPreviewTarget, IReadOnlyList<Border>> marken =
            new Dictionary<DossierPreviewTarget, IReadOnlyList<Border>>();

        RunOnSta(() => marken = DossierPreviewPageRenderer.Render(
            page,
            _ => string.Empty,
            _ => Array.Empty<IReadOnlyDictionary<string, string>>(),
            _ => string.Empty,
            original => original == "Übersichtsplan Werkleitungen"
                ? "Situationsplan Werkleitungen"
                : null)
            .Frames);

        Assert.Single(marken[DossierPreviewTarget.Literal("Übersichtsplan Werkleitungen")]);
    }

    private static void RunOnSta(Action action)
    {
        Exception? fehler = null;

        var thread = new Thread(() =>
        {
            try
            {
                action();
            }
            catch (Exception ex)
            {
                fehler = ex;
            }
        });

        thread.SetApartmentState(ApartmentState.STA);
        thread.Start();
        thread.Join();

        if (fehler is not null)
            throw new Xunit.Sdk.XunitException(fehler.ToString());
    }
}
