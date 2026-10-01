using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using Line = System.Windows.Shapes.Line;

using AuswertungPro.Next.Application.Dossiers.Preview;
using AuswertungPro.Next.Infrastructure.Dossiers;
using AuswertungPro.Next.Infrastructure.Dossiers.Preview;
using AuswertungPro.Next.UI.Views.Rendering;

using Xunit;

namespace AuswertungPro.Next.UI.Tests;

/// <summary>
/// Prueft die TATSAECHLICH gezeichnete Lage auf dem Blatt, nicht die aus der
/// Vorlage gelesenen Zahlen. Ein Lesefehler faellt bei den Zahlen auf; ein
/// Fehler im Zusammenbau — doppelte Raender, verschluckte Leerzeilen, falsche
/// Zeilenhoehe — erst hier.
/// </summary>
public sealed class DossierPreviewRenderTests
{
    private static DossierPreviewPage Deckblatt()
    {
        var wurzel = new AuswertungPro.Next.Infrastructure.Backup.RepositoryRootFileLocator()
            .Locate(AppContext.BaseDirectory);
        Assert.NotNull(wurzel);

        var pfad = Path.Combine(wurzel!, "Export_Vorlage", DossierWordTemplate.TemplateFileName);
        Assert.True(File.Exists(pfad), $"'{pfad}' fehlt.");

        return DossierPreviewBuilder.Build(pfad).Pages.First();
    }

    private static DossierPreviewPage Inhaltsverzeichnis()
    {
        var wurzel = new AuswertungPro.Next.Infrastructure.Backup.RepositoryRootFileLocator()
            .Locate(AppContext.BaseDirectory);
        Assert.NotNull(wurzel);

        var pfad = Path.Combine(wurzel!, "Export_Vorlage", DossierWordTemplate.TemplateFileName);
        Assert.True(File.Exists(pfad), $"'{pfad}' fehlt.");

        return DossierPreviewBuilder.Build(pfad).Pages.Single(page => page.Blocks
            .OfType<DossierPreviewParagraph>()
            .Any(paragraph => paragraph.TocEntry is not null));
    }

    private static FrameworkElement Zeichne(DossierPreviewPage seite)
    {
        var ergebnis = DossierPreviewPageRenderer.Render(
            seite,
            _ => string.Empty,
            _ => Array.Empty<IReadOnlyDictionary<string, string>>(),
            _ => string.Empty);

        var blatt = ergebnis.Root;
        blatt.Measure(new Size(double.PositiveInfinity, double.PositiveInfinity));
        blatt.Arrange(new Rect(blatt.DesiredSize));
        blatt.UpdateLayout();
        return blatt;
    }

    /// <summary>Die Lage eines Elements im Blatt, in Bildpunkten.</summary>
    private static Rect LageIm(FrameworkElement blatt, FrameworkElement element)
    {
        var ecke = element.TransformToAncestor(blatt).Transform(new Point(0, 0));
        return new Rect(ecke, element.RenderSize);
    }

    private static IEnumerable<FrameworkElement> Alle(DependencyObject wurzel)
    {
        for (var i = 0; i < VisualTreeHelper.GetChildrenCount(wurzel); i++)
        {
            var kind = VisualTreeHelper.GetChild(wurzel, i);

            if (kind is FrameworkElement element)
                yield return element;

            foreach (var enkel in Alle(kind))
                yield return enkel;
        }
    }

    [Fact]
    public void Der_Fussstreifen_des_Deckblatts_liegt_im_Rahmen()
    {
        RunOnSta(() =>
        {
            var seite = Deckblatt();
            var blatt = Zeichne(seite);

            // Der Rahmen ist der grosse Kasten des Deckblatts.
            var rahmenModell = seite.Blocks
                .OfType<DossierPreviewParagraph>()
                .SelectMany(p => p.Floating)
                .First(f => f.WidthPx > 700);

            // Der Fussstreifen sind die sechs kleinen Kaesten an einem Absatz.
            var fussModell = seite.Blocks
                .OfType<DossierPreviewParagraph>()
                .First(p => p.Floating.Count >= 6)
                .Floating;

            var kaesten = Alle(blatt)
                .OfType<Border>()
                .Where(b => b.Parent is Canvas)
                .ToList();

            var rahmen = kaesten.Single(b => Math.Abs(b.Width - rahmenModell.WidthPx) < 1);
            var rahmenLage = LageIm(blatt, rahmen);

            Assert.True(
                Math.Abs(rahmenLage.Top - seite.Geometry.Margin.Top) < 2,
                $"Der Rahmen beginnt bei {rahmenLage.Top:0.0} statt am oberen Rand "
                + $"({seite.Geometry.Margin.Top:0.0}).");

            foreach (var modell in fussModell)
            {
                var kasten = kaesten.First(b =>
                    Math.Abs(b.Width - modell.WidthPx) < 1
                    && Math.Abs(Canvas.GetTop(b) - modell.TopPx) < 1);

                var lage = LageIm(blatt, kasten);

                Assert.True(
                    lage.Top >= rahmenLage.Top && lage.Bottom <= rahmenLage.Bottom + 1,
                    $"Ein Kasten des Fussstreifens liegt bei {lage.Top:0.0}–{lage.Bottom:0.0}, "
                    + $"der Rahmen bei {rahmenLage.Top:0.0}–{rahmenLage.Bottom:0.0}.");
            }
        });
    }

    [Fact]
    public void Das_Blatt_bleibt_auf_dem_Deckblatt_innerhalb_von_A4()
    {
        RunOnSta(() =>
        {
            var seite = Deckblatt();
            var blatt = Zeichne(seite);

            Assert.Equal(seite.Geometry.WidthPx, blatt.RenderSize.Width, 0);

            Assert.True(
                blatt.RenderSize.Height <= seite.Geometry.HeightPx + 1,
                $"Das Deckblatt ist {blatt.RenderSize.Height:0} Punkte hoch, "
                + $"A4 hat {seite.Geometry.HeightPx:0}.");
        });
    }

    [Fact]
    public void Logo_und_Wappen_stehen_oben_und_auf_verschiedenen_Seiten()
    {
        RunOnSta(() =>
        {
            var seite = Deckblatt();
            var blatt = Zeichne(seite);

            var bilder = Alle(blatt).OfType<Image>().ToList();
            Assert.Equal(2, bilder.Count);

            var lagen = bilder.Select(b => LageIm(blatt, b)).OrderBy(r => r.Left).ToList();

            Assert.All(lagen, l => Assert.True(
                l.Top < 200, $"Ein Deckblattbild liegt bei {l.Top:0.0} statt oben."));

            Assert.True(
                lagen[1].Left - lagen[0].Right > 200,
                "Logo und Wappen stehen nicht auf gegenüberliegenden Seiten.");
        });
    }

    [Fact]
    public void Zusaetzliche_Verzeichnispunkte_sind_einzelne_gleich_ausgerichtete_Zeilen()
    {
        RunOnSta(() =>
        {
            var seite = Inhaltsverzeichnis();
            var ergebnis = DossierPreviewPageRenderer.Render(
                seite,
                key => key == "Verzeichnis_Beilagen"
                    ? "4.\tProtokolle\t8\n5.\tPläne\t12"
                    : string.Empty,
                _ => Array.Empty<IReadOnlyDictionary<string, string>>(),
                _ => string.Empty);
            var blatt = ergebnis.Root;
            blatt.Measure(new Size(double.PositiveInfinity, double.PositiveInfinity));
            blatt.Arrange(new Rect(blatt.DesiredSize));
            blatt.UpdateLayout();

            var drittesKapitel = Assert.Single(ergebnis.Frames[
                DossierPreviewTarget.Literal("Informationen Sanierung")]);
            var zweitesKapitel = Assert.Single(ergebnis.Frames[
                DossierPreviewTarget.Literal("Eigentumsverhältnisse")]);
            var beilageZeilen = ergebnis.Frames[
                DossierPreviewTarget.Field("Verzeichnis_Beilagen")];

            Assert.Equal(2, beilageZeilen.Count);
            Assert.Single(ergebnis.Frames[
                DossierPreviewTarget.Row("Verzeichnis_Beilagen", 0)]);
            Assert.Single(ergebnis.Frames[
                DossierPreviewTarget.Row("Verzeichnis_Beilagen", 1)]);
            var kapitelAbstand = LageIm(blatt, drittesKapitel).Top
                - LageIm(blatt, zweitesKapitel).Bottom;
            var beilageAbstand = LageIm(blatt, beilageZeilen[0]).Top
                - LageIm(blatt, drittesKapitel).Bottom;
            Assert.Equal(kapitelAbstand, beilageAbstand, precision: 1);

            var kapitelTitel = Titelspalte(drittesKapitel);
            var beilageTitel = Titelspalte(beilageZeilen[0]);
            Assert.InRange(
                Math.Abs(
                    LageIm(blatt, kapitelTitel).Left
                    - LageIm(blatt, beilageTitel).Left),
                0,
                1.5);
            Assert.Equal(kapitelTitel.FontFamily.Source, beilageTitel.FontFamily.Source);
            Assert.Equal(kapitelTitel.FontSize, beilageTitel.FontSize, precision: 1);

            var ersteSeite = Seitenspalte(beilageZeilen[0]);
            var zweiteSeite = Seitenspalte(beilageZeilen[1]);
            Assert.Equal("8", ersteSeite.Text);
            Assert.Equal("12", zweiteSeite.Text);
            Assert.Equal(HorizontalAlignment.Right, ersteSeite.HorizontalAlignment);

            var punktlinie = Punktlinie(beilageZeilen[0]);
            Assert.Equal(PenLineCap.Round, punktlinie.StrokeDashCap);
            Assert.NotEmpty(punktlinie.StrokeDashArray);
        });
    }

    private static TextBlock Titelspalte(Border row)
    {
        var grid = Assert.IsType<Grid>(row.Child);
        return grid.Children
            .OfType<TextBlock>()
            .Single(block => Grid.GetColumn(block) == 1);
    }

    private static TextBlock Seitenspalte(Border row)
    {
        var grid = Assert.IsType<Grid>(row.Child);
        return grid.Children
            .OfType<TextBlock>()
            .Single(block => Grid.GetColumn(block) == 2);
    }

    private static Line Punktlinie(Border row)
    {
        var grid = Assert.IsType<Grid>(row.Child);
        return grid.Children
            .OfType<Line>()
            .Single(line => Grid.GetColumn(line) == 1);
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
