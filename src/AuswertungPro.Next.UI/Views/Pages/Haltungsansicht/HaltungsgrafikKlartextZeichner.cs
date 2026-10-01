using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Shapes;
using AuswertungPro.Next.Application.Protocol;
using AuswertungPro.Next.Application.Reports;
using AuswertungPro.Next.Domain.Models;
using AuswertungPro.Next.UI.Controls;

namespace AuswertungPro.Next.UI.Views.Pages.Haltungsansicht;

/// <summary>Bildschirmbreite Klartexte mit unverzerrter Meterachse und kollisionsfreien Bezugslinien.</summary>
internal static class HaltungsgrafikKlartextZeichner
{
    private const double RohrVersatz = 18;
    private const double TextX = 108;
    private const double Abstand = 8;
    private const double Oben = HaltungsgrafikSvgBuilder.MarginTop + HaltungsgrafikSvgBuilder.HeaderHeight + HaltungsgrafikSvgBuilder.NodeZone;

    internal static (Canvas Flaeche, int Anzahl)? Zeichne(HaltungRecord record, ICodeCatalogProvider? catalog,
        FrameworkElement quelle, double breite, double hoehe, bool? flowDown,
        Action<IReadOnlyList<string>>? fotoOeffnen = null)
    {
        breite = Math.Max(230, breite);
        var ansicht = HaltungsgrafikAnsichtBuilder.BaueUebersicht(record, catalog, (int)Math.Max(300, hoehe), flowDown);
        if (ansicht is null) return null;
        ansicht = ansicht with { Marken = ansicht.Marken.OrderBy(m => m.Y).ToArray() };
        var texte = ansicht.Marken.Select(m => Text(m.Tooltip, breite - TextX - 6)).ToArray();
        foreach (var text in texte) text.Measure(new Size(text.Width, double.PositiveInfinity));

        var mindestHoehe = Oben + HaltungsgrafikSvgBuilder.MarginBottom
            + texte.Sum(t => t.DesiredSize.Height + Abstand);
        if (mindestHoehe > ansicht.Hoehe)
        {
            ansicht = HaltungsgrafikAnsichtBuilder.BaueUebersicht(record, catalog, (int)Math.Ceiling(mindestHoehe), flowDown)!;
            ansicht = ansicht with { Marken = ansicht.Marken.OrderBy(m => m.Y).ToArray() };
        }

        var farbe = NutzungsartReportColors.Resolve(record.GetFieldValue(FieldKeys.UsageType)).Accent;
        var svg = ansicht.Svg.Replace("#2196F3", farbe).Replace("#1565C0", farbe);
        var rohr = SvgTeilmengeZeichner.Zeichne(svg, quelle);
        var flaeche = new Canvas { Width = breite, Height = ansicht.Hoehe };
        Canvas.SetLeft(rohr, RohrVersatz);
        flaeche.Children.Add(rohr);

        var nutzung = NutzungsartVokabular.Normalisieren(record.GetFieldValue(FieldKeys.UsageType));
        var legende = Text(string.IsNullOrWhiteSpace(nutzung) ? "Nutzungsart unbekannt" : nutzung, breite - TextX - 6);
        legende.FontWeight = FontWeights.SemiBold;
        legende.FontSize = 11;
        Setze(flaeche, legende, TextX, 2);
        var massstab = Text("Massstab in Metern", breite - TextX - 6);
        massstab.FontSize = 11;
        Setze(flaeche, massstab, TextX, 32);

        var positionen = Positionen(ansicht.Marken.Select(m => m.Y + m.Hoehe / 2).ToArray(),
            texte.Select(t => t.DesiredSize.Height).ToArray(), Oben, ansicht.Hoehe - HaltungsgrafikSvgBuilder.MarginBottom);
        for (var i = 0; i < texte.Length; i++)
        {
            var marke = ansicht.Marken[i];
            var x = marke.X + marke.Breite / 2 + RohrVersatz;
            var y = marke.Y + marke.Hoehe / 2;
            var bezug = new Polyline
            {
                Points = new PointCollection { new(x + 8, y), new(x + 18, y), new(TextX - 5, positionen[i] + texte[i].DesiredSize.Height / 2) },
                StrokeThickness = 1, IsHitTestVisible = false
            };
            bezug.SetResourceReference(Shape.StrokeProperty, "MutedBrush");
            flaeche.Children.Add(bezug);
            Setze(flaeche, texte[i], TextX, positionen[i]);
            var hinweis = new Rectangle { Width = marke.Breite, Height = marke.Hoehe,
                Fill = Brushes.Transparent, ToolTip = marke.Tooltip };
            HaltungsgrafikFotoAktion.Verbinde(hinweis, marke, fotoOeffnen);
            HaltungsgrafikFotoAktion.Verbinde(texte[i], marke, fotoOeffnen);
            Setze(flaeche, hinweis, marke.X + RohrVersatz, marke.Y);
        }
        return (flaeche, ansicht.Marken.Count);
    }

    // Die echten Meterpositionen bleiben am Rohr; nur die Texte werden bei Gedraenge verschoben.
    internal static double[] Positionen(double[] ziel, double[] hoehen, double oben, double unten)
    {
        var result = new double[ziel.Length];
        var ende = oben - Abstand;
        for (var i = 0; i < result.Length; i++)
        {
            result[i] = Math.Max(ziel[i] - hoehen[i] / 2, ende + Abstand);
            ende = result[i] + hoehen[i];
        }
        var naechstes = unten + Abstand;
        for (var i = result.Length - 1; i >= 0; i--)
        {
            result[i] = Math.Min(result[i], naechstes - Abstand - hoehen[i]);
            naechstes = result[i];
        }
        return result;
    }

    private static TextBlock Text(string text, double breite)
    {
        var block = new TextBlock { Text = text, Width = breite, FontSize = 12,
            TextWrapping = TextWrapping.Wrap, ToolTip = text };
        block.SetResourceReference(TextBlock.ForegroundProperty, "TextBrush");
        return block;
    }

    private static void Setze(Canvas flaeche, FrameworkElement element, double x, double y)
    {
        Canvas.SetLeft(element, x);
        Canvas.SetTop(element, y);
        flaeche.Children.Add(element);
    }
}
