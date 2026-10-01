using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Windows;
using System.Windows.Documents;
using System.Windows.Media;

using AuswertungPro.Next.Application.Dossiers;
using AuswertungPro.Next.Application.Dossiers.Preview;
using AuswertungPro.Next.Domain.Models.Dossiers;
using AuswertungPro.Next.UI.Views.Rendering;

using Xunit;

namespace AuswertungPro.Next.UI.Tests;

/// <summary>
/// Prueft, dass eine im Dossier gesetzte Schriftfarbe im gezeichneten Blatt
/// wirklich ankommt — nicht nur in den Daten.
/// </summary>
public sealed class DossierPreviewColorRenderTests
{
    private static DossierPreviewRunFormat Format(string? farbe = null)
        => DossierPreviewRunFormat.Default with { ColorHex = farbe };

    private static DossierPreviewTableCell Zelle(string text, string? farbe = null)
        => new(
            new[]
            {
                new DossierPreviewParagraph(
                    new[] { DossierPreviewRun.Literal(text, Format(farbe)) },
                    DossierPreviewParagraphFormat.Default)
            },
            DossierPreviewEdges.All(2),
            DossierPreviewEdges.All(1),
            null,
            1);

    private static DossierPreviewPage SeiteMitThemen()
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

    private static IEnumerable<Run> AlleRuns(DependencyObject wurzel)
    {
        for (var i = 0; i < VisualTreeHelper.GetChildrenCount(wurzel); i++)
        {
            var kind = VisualTreeHelper.GetChild(wurzel, i);

            if (kind is System.Windows.Controls.TextBlock block)
            {
                foreach (var run in block.Inlines.OfType<Run>())
                    yield return run;
            }

            foreach (var enkel in AlleRuns(kind))
                yield return enkel;
        }
    }

    private static IReadOnlyList<Run> Zeichne(
        IReadOnlyList<IReadOnlyDictionary<string, string>> zeilen)
    {
        var ergebnis = DossierPreviewPageRenderer.Render(
            SeiteMitThemen(),
            _ => string.Empty,
            _ => zeilen,
            _ => string.Empty);

        var blatt = ergebnis.Root;
        blatt.Measure(new Size(double.PositiveInfinity, double.PositiveInfinity));
        blatt.Arrange(new Rect(blatt.DesiredSize));
        blatt.UpdateLayout();

        return AlleRuns(blatt).ToList();
    }

    /// <summary>
    /// Die Farbe eines Textes im gezeichneten Blatt. Gemessen wird INNERHALB
    /// des STA-Fadens: WPF-Objekte gehoeren dem Faden, der sie erzeugt hat.
    /// </summary>
    private static Color FarbeVon(
        IReadOnlyList<IReadOnlyDictionary<string, string>> zeilen, string text)
    {
        var gefunden = Colors.Transparent;

        RunOnSta(() =>
        {
            var run = Zeichne(zeilen).FirstOrDefault(r => r.Text == text);
            Assert.True(run is not null, $"Der Text „{text}“ steht nicht im Blatt.");

            gefunden = Assert.IsType<SolidColorBrush>(run!.Foreground).Color;
        });

        return gefunden;
    }

    private static Dictionary<string, string> Zeile(string thema, string text, string farbe)
        => new(StringComparer.OrdinalIgnoreCase)
        {
            ["Thema"] = thema,
            ["Text"] = text,
            ["Text__Farbe"] = farbe
        };

    [Fact]
    public void Die_gesetzte_Schriftfarbe_erscheint_im_Blatt()
    {
        Assert.Equal(
            Color.FromRgb(0xC0, 0x00, 0x00),
            FarbeVon(new[] { Zeile("Schäden", "Leitung undicht", "C00000") }, "Leitung undicht"));
    }

    [Fact]
    public void Ohne_gesetzte_Farbe_bleibt_der_Text_schwarz()
    {
        Assert.Equal(
            Color.FromRgb(0x00, 0x00, 0x00),
            FarbeVon(new[] { Zeile("Schäden", "Leitung undicht", "") }, "Leitung undicht"));
    }

    [Fact]
    public void Die_Farbe_gilt_nur_fuer_ihre_eigene_Zeile()
    {
        var zeilen = new[]
        {
            Zeile("Schäden", "rot", "C00000"),
            Zeile("Sanierungskonzept", "schwarz", "")
        };

        Assert.Equal(Color.FromRgb(0xC0, 0x00, 0x00), FarbeVon(zeilen, "rot"));
        Assert.Equal(Color.FromRgb(0x00, 0x00, 0x00), FarbeVon(zeilen, "schwarz"));
    }

    [Fact]
    public void Gemischte_Formatierung_erscheint_in_Arial_im_Blatt()
    {
        var format = DossierTopicTextFormatting.Encode(new[]
        {
            new DossierTextStyleRange
            {
                Start = 0,
                Length = 3,
                ColorHex = "C00000",
                Bold = true,
                Italic = true,
                Underline = true
            }
        });
        var zeile = Zeile("Schäden", "rot normal", "");
        zeile["Text" + DossierTopicTextFormatting.StyleRangesSuffix] = format;

        RunOnSta(() =>
        {
            var run = Zeichne(new[] { zeile }).First(r => r.Text == "rot");

            Assert.Equal("Arial", run.FontFamily.Source);
            Assert.Equal(FontWeights.Bold, run.FontWeight);
            Assert.Equal(FontStyles.Italic, run.FontStyle);
            Assert.Contains(run.TextDecorations,
                d => d.Location == TextDecorationLocation.Underline);
            Assert.Equal(Color.FromRgb(0xC0, 0x00, 0x00),
                Assert.IsType<SolidColorBrush>(run.Foreground).Color);
        });
    }

    [Fact]
    public void Bearbeitete_Ueberschrift_zeigt_ihre_Formatierung_auch_in_der_Vorschau()
    {
        var paragraph = new DossierPreviewParagraph(
            new[] { DossierPreviewRun.Literal("Informationen", Format()) },
            DossierPreviewParagraphFormat.Default);
        var page = new DossierPreviewPage(
            1,
            "Informationen",
            new DossierPreviewGeometry(794, 1123, DossierPreviewEdges.All(76)),
            new DossierPreviewBlock[] { paragraph },
            Array.Empty<string>());
        var styles = new[]
        {
            new DossierTextStyleRange
            {
                Start = 0,
                Length = 3,
                ColorHex = "C00000",
                Bold = true
            }
        };

        RunOnSta(() =>
        {
            var render = DossierPreviewPageRenderer.Render(
                page,
                _ => string.Empty,
                _ => Array.Empty<IReadOnlyDictionary<string, string>>(),
                _ => string.Empty,
                _ => "Neu beschriftet",
                _ => styles);

            var run = AlleRuns(render.Root).First(r => r.Text == "Neu");
            Assert.Equal("Arial", run.FontFamily.Source);
            Assert.Equal(FontWeights.Bold, run.FontWeight);
            Assert.Equal(
                Color.FromRgb(0xC0, 0x00, 0x00),
                Assert.IsType<SolidColorBrush>(run.Foreground).Color);
        });
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
