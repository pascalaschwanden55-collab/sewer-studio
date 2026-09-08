using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Media;
using System.Windows.Shapes;
using AuswertungPro.Next.UI.Controls;

namespace AuswertungPro.Next.UI.Tests;

/// <summary>
/// Der Zeichner der SVG-Teilmenge: Jedes Element wird zu einer WPF-Form an der richtigen Stelle,
/// jede Farbe kommt aus dem Theme, und alles Unbekannte faellt namentlich auf.
/// Laeuft auf einem eigenen STA-Thread mit einem kleinen Ressourcensatz statt der ganzen App.
/// </summary>
public sealed class SvgTeilmengeZeichnerTests
{
    private const string Kopf = "<svg xmlns='http://www.w3.org/2000/svg' width='200' height='100' viewBox='0 0 200 100'>";

    [Fact]
    public void Rechteck_Linie_Kreis_und_Ellipse_liegen_an_der_richtigen_Stelle()
    {
        Mit(host =>
        {
            var flaeche = Zeichne(host,
                "<rect x='10' y='20' width='30' height='40' rx='4' fill='#1F2937'/>"
                + "<line x1='1' y1='2' x2='3' y2='4' stroke='#D64541' stroke-width='2'/>"
                + "<circle cx='50' cy='60' r='5' fill='#006E9C'/>"
                + "<ellipse cx='70' cy='80' rx='6' ry='3' fill='none' stroke='#4A5568'/>");

            var rechteck = flaeche.Children.OfType<Rectangle>().Single();
            Assert.Equal(10d, Canvas.GetLeft(rechteck));
            Assert.Equal(20d, Canvas.GetTop(rechteck));
            Assert.Equal(30d, rechteck.Width);
            Assert.Equal(40d, rechteck.Height);
            Assert.Equal(4d, rechteck.RadiusX);
            Assert.Equal(Colors.Black, Farbe(rechteck.Fill));

            var linie = flaeche.Children.OfType<Line>().Single();
            Assert.Equal(new[] { 1d, 2d, 3d, 4d }, new[] { linie.X1, linie.Y1, linie.X2, linie.Y2 });
            Assert.Equal(2d, linie.StrokeThickness);
            Assert.Equal(Colors.Red, Farbe(linie.Stroke));

            var kreis = flaeche.Children.OfType<Ellipse>().First();
            Assert.Equal(45d, Canvas.GetLeft(kreis));
            Assert.Equal(55d, Canvas.GetTop(kreis));
            Assert.Equal(10d, kreis.Width);
            Assert.Equal(Colors.Blue, Farbe(kreis.Fill));

            var ellipse = flaeche.Children.OfType<Ellipse>().Last();
            Assert.Equal(64d, Canvas.GetLeft(ellipse));
            Assert.Equal(77d, Canvas.GetTop(ellipse));
            Assert.Equal(12d, ellipse.Width);
            Assert.Equal(6d, ellipse.Height);
            Assert.Null(ellipse.Fill);
        });
    }

    [Fact]
    public void Vieleck_und_Pfad_uebernehmen_ihre_Geometrie()
    {
        Mit(host =>
        {
            var flaeche = Zeichne(host,
                "<polygon points='1,2 3,4 5,6' fill='#D64541'/>"
                + "<polyline points='7,8 9,10' fill='none' stroke='#D64541'/>"
                + "<path d='M 10,10 L 20,20 Q 25,15 30,20 C 32,22 34,24 36,26 Z' fill='none' stroke='#006E9C'"
                + " stroke-width='2' stroke-linecap='round' stroke-linejoin='round'/>");

            var vieleck = flaeche.Children.OfType<Polygon>().Single();
            Assert.Equal(3, vieleck.Points.Count);
            Assert.Equal(new Point(3, 4), vieleck.Points[1]);

            var linienzug = flaeche.Children.OfType<Polyline>().Single();
            Assert.Equal(2, linienzug.Points.Count);

            var pfad = flaeche.Children.OfType<Path>().Single();
            Assert.NotNull(pfad.Data);
            Assert.False(pfad.Data!.Bounds.IsEmpty);
            Assert.Equal(PenLineCap.Round, pfad.StrokeStartLineCap);
            Assert.Equal(PenLineJoin.Round, pfad.StrokeLineJoin);
            Assert.Null(pfad.Fill);
        });
    }

    /// <summary>SVG setzt die Grundlinie und den Anker, WPF die linke Oberkante.</summary>
    [Fact]
    public void Beschriftung_beachtet_Anker_und_Grundlinie()
    {
        Mit(host =>
        {
            var flaeche = Zeichne(host,
                "<text x='100' y='50' font-size='11' font-weight='bold' text-anchor='middle'"
                + " font-family='sans-serif' fill='#111827'>Meter</text>"
                + "<text x='100' y='70' font-size='11' text-anchor='end' font-family='sans-serif'>Ende</text>"
                + "<text x='10' y='90' font-size='11' font-family='sans-serif'>Start</text>");

            var texte = flaeche.Children.OfType<TextBlock>().ToList();
            Assert.Equal(3, texte.Count);
            Assert.Equal("Meter", texte[0].Text);
            Assert.Equal(FontWeights.Bold, texte[0].FontWeight);
            Assert.Equal(11d, texte[0].FontSize);
            Assert.Equal(Colors.Black, Farbe(texte[0].Foreground));

            // Mitte: die halbe Textbreite links vom Ankerpunkt.
            Assert.Equal(100d - texte[0].DesiredSize.Width / 2d, Canvas.GetLeft(texte[0]), 3);
            Assert.Equal(50d - texte[0].BaselineOffset, Canvas.GetTop(texte[0]), 3);

            // Rechtsbuendig: die ganze Breite links vom Ankerpunkt.
            Assert.Equal(100d - texte[1].DesiredSize.Width, Canvas.GetLeft(texte[1]), 3);

            // Standard ist linksbuendig.
            Assert.Equal(10d, Canvas.GetLeft(texte[2]), 3);

            // Ohne Farbangabe gilt die Textfarbe des Themes, nicht das schwarze SVG-Standard-
            // fuellen: Ein Wechsel des Tokens faerbt den Text mit.
            host.Resources["TextBrush"] = new SolidColorBrush(Colors.Purple);
            host.UpdateLayout();
            Assert.Equal(Colors.Purple, Farbe(texte[1].Foreground));
            Assert.Equal(Colors.Purple, Farbe(texte[0].Foreground));
        });
    }

    /// <summary>Ein gedrehter Text dreht um seinen SVG-Drehpunkt, nicht um die Ecke des Blocks.</summary>
    [Fact]
    public void Gedrehte_Beschriftung_dreht_um_den_angegebenen_Punkt()
    {
        Mit(host =>
        {
            var flaeche = Zeichne(host,
                "<text x='20' y='50' font-size='11' text-anchor='middle' font-family='sans-serif'"
                + " transform='rotate(90 20 50)'>Fliessrichtung</text>");

            var text = flaeche.Children.OfType<TextBlock>().Single();
            var drehung = Assert.IsType<RotateTransform>(text.RenderTransform);
            Assert.Equal(90d, drehung.Angle);
            Assert.Equal(20d - Canvas.GetLeft(text), drehung.CenterX, 3);
            Assert.Equal(50d - Canvas.GetTop(text), drehung.CenterY, 3);
        });
    }

    [Fact]
    public void Verlauf_Muster_Zuschnitt_und_Schatten_kommen_aus_den_Definitionen()
    {
        Mit(host =>
        {
            var flaeche = Zeichne(host,
                "<defs>"
                + "<linearGradient id='v' x1='0' y1='0' x2='1' y2='0'>"
                + "<stop offset='0%' stop-color='#006E9C' stop-opacity='0.3'/>"
                + "<stop offset='100%' stop-color='#006E9C' stop-opacity='1'/>"
                + "</linearGradient>"
                + "<pattern id='m' patternUnits='userSpaceOnUse' width='6' height='6' patternTransform='rotate(45)'>"
                + "<line x1='0' y1='0' x2='0' y2='6' stroke='#D64541' stroke-width='2'/>"
                + "</pattern>"
                + "<filter id='s' x='-30%' y='-30%' width='160%' height='160%'>"
                + "<feDropShadow dx='1' dy='1' stdDeviation='1.5' flood-color='#00000033'/>"
                + "</filter>"
                + "<clipPath id='c'><rect x='10' y='0' width='40' height='100'/></clipPath>"
                + "</defs>"
                + "<rect x='0' y='0' width='20' height='20' fill='url(#v)'/>"
                + "<rect x='20' y='0' width='20' height='20' fill='url(#m)'/>"
                + "<circle cx='60' cy='60' r='5' fill='#006E9C' filter='url(#s)'/>"
                + "<text x='12' y='30' font-size='11' font-family='sans-serif' clip-path='url(#c)'>Zelle</text>");

            var rechtecke = flaeche.Children.OfType<Rectangle>().ToList();
            var verlauf = Assert.IsType<LinearGradientBrush>(rechtecke[0].Fill);
            Assert.Equal(2, verlauf.GradientStops.Count);
            Assert.Equal(Colors.Blue.R, verlauf.GradientStops[1].Color.R);
            Assert.True(verlauf.GradientStops[0].Color.A < verlauf.GradientStops[1].Color.A);

            var muster = Assert.IsType<DrawingBrush>(rechtecke[1].Fill);
            Assert.Equal(TileMode.Tile, muster.TileMode);
            Assert.IsType<RotateTransform>(muster.Transform);

            var kreis = flaeche.Children.OfType<Ellipse>().Single();
            Assert.NotNull(kreis.Effect);

            var text = flaeche.Children.OfType<TextBlock>().Single();
            var zuschnitt = Assert.IsType<RectangleGeometry>(text.Clip);
            Assert.Equal(10d - Canvas.GetLeft(text), zuschnitt.Rect.X, 3);
            Assert.Equal(40d, zuschnitt.Rect.Width);
        });
    }

    /// <summary>Eine Gruppe verschiebt ihre Kinder gemeinsam.</summary>
    [Fact]
    public void Gruppe_uebernimmt_ihre_Verschiebung()
    {
        Mit(host =>
        {
            var flaeche = Zeichne(host, "<g transform='translate(5,7)'><rect x='1' y='1' width='2' height='2' fill='#111827'/></g>");

            var gruppe = flaeche.Children.OfType<Canvas>().Single();
            var verschiebung = Assert.IsType<TranslateTransform>(gruppe.RenderTransform);
            Assert.Equal(5d, verschiebung.X);
            Assert.Equal(7d, verschiebung.Y);
            Assert.Single(gruppe.Children.OfType<Rectangle>());
        });
    }

    /// <summary>Strichmuster: SVG misst absolut, WPF in Vielfachen der Strichstaerke.</summary>
    [Fact]
    public void Strichmuster_wird_auf_die_Strichstaerke_umgerechnet()
    {
        Mit(host =>
        {
            var flaeche = Zeichne(host, "<line x1='0' y1='0' x2='10' y2='0' stroke='#6B7280' stroke-width='2' stroke-dasharray='4,2'/>");

            var linie = flaeche.Children.OfType<Line>().Single();
            Assert.Equal(new[] { 2d, 1d }, linie.StrokeDashArray.ToArray());
        });
    }

    /// <summary>Die Farben haengen am Theme: derselbe Wert, anderes Design, andere Farbe.</summary>
    [Fact]
    public void Farben_folgen_dem_Theme()
    {
        Mit(host =>
        {
            var flaeche = Zeichne(host, "<rect x='0' y='0' width='10' height='10' fill='#FFFFFF'/>");
            var rechteck = flaeche.Children.OfType<Rectangle>().Single();
            Assert.Equal(Colors.White, Farbe(rechteck.Fill));

            host.Resources["CardBrush"] = new SolidColorBrush(Colors.DarkSlateGray);
            host.UpdateLayout();
            Assert.Equal(Colors.DarkSlateGray, Farbe(rechteck.Fill));
        });
    }

    /// <summary>Eine unbekannte Farbe wird sichtbar in der Textfarbe gezeichnet, nicht verschluckt.</summary>
    [Fact]
    public void Unbekannte_Farbe_faellt_auf_die_Textfarbe_zurueck()
    {
        Mit(host =>
        {
            var flaeche = Zeichne(host, "<rect x='0' y='0' width='10' height='10' fill='#123456'/>");
            var rechteck = flaeche.Children.OfType<Rectangle>().Single();
            Assert.Equal(Colors.Black, Farbe(rechteck.Fill));
            Assert.Equal("TextBrush", SvgFarbZuordnung.TokenFuer("#123456"));
            Assert.False(SvgFarbZuordnung.IstBekannt("#123456"));
        });
    }

    [Fact]
    public void Ein_unbekanntes_Element_wirft_mit_Namen()
    {
        Mit(host =>
        {
            var fehler = Assert.Throws<NotSupportedException>(() => Zeichne(host, "<foreignObject x='1'/>"));
            Assert.Contains("foreignObject", fehler.Message, StringComparison.Ordinal);
        });
    }

    [Fact]
    public void Ein_unbekanntes_Attribut_wirft_mit_Namen()
    {
        Mit(host =>
        {
            var fehler = Assert.Throws<NotSupportedException>(
                () => Zeichne(host, "<rect x='0' y='0' width='1' height='1' style='fill:red'/>"));
            Assert.Contains("style", fehler.Message, StringComparison.Ordinal);
        });
    }

    private static Canvas Zeichne(Grid host, string inhalt)
    {
        var flaeche = SvgTeilmengeZeichner.Zeichne(Kopf + inhalt + "</svg>", host);
        host.Children.Clear();
        host.Children.Add(flaeche);
        host.Measure(new Size(400, 300));
        host.Arrange(new Rect(0, 0, 400, 300));
        host.UpdateLayout();
        return flaeche;
    }

    private static Color Farbe(Brush? pinsel) => Assert.IsType<SolidColorBrush>(pinsel).Color;

    /// <summary>
    /// Kleiner Ressourcensatz statt der ganzen App: Die Farbtoken bekommen bewusst deutlich
    /// unterscheidbare Werte, damit die Zuordnung nachweisbar ist.
    /// </summary>
    private static void Mit(Action<Grid> pruefung)
    {
        StaTestRunner.Run(() =>
        {
            var host = new Grid();
            host.Resources["CardBrush"] = new SolidColorBrush(Colors.White);
            host.Resources["BgLightBrush"] = new SolidColorBrush(Colors.WhiteSmoke);
            host.Resources["BorderBrush"] = new SolidColorBrush(Colors.LightGray);
            host.Resources["FaintBrush"] = new SolidColorBrush(Colors.DarkGray);
            host.Resources["MutedBrush"] = new SolidColorBrush(Colors.Gray);
            host.Resources["TextSecondaryBrush"] = new SolidColorBrush(Colors.DimGray);
            host.Resources["TextBrush"] = new SolidColorBrush(Colors.Black);
            host.Resources["AccentBrush"] = new SolidColorBrush(Colors.Blue);
            host.Resources["AccentTextBrush"] = new SolidColorBrush(Colors.DarkBlue);
            host.Resources["DangerBrush"] = new SolidColorBrush(Colors.Red);
            host.Resources["WarningBrush"] = new SolidColorBrush(Colors.Goldenrod);
            host.Resources["SuccessBrush"] = new SolidColorBrush(Colors.Green);
            host.Resources["Severity4Brush"] = new SolidColorBrush(Colors.Orange);
            TextElement.SetFontFamily(host, new FontFamily("Segoe UI"));
            pruefung(host);
        });
    }
}
