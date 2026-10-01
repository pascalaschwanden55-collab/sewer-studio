using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace AuswertungPro.Next.UI.Views.Rendering;

/// <summary>
/// Legt den gerade getippten Text sofort über die Stelle im Blatt.
///
/// Gemessen: Eine echte Word/PDF-Umwandlung dauert auf diesem Rechner 2,35 s,
/// mit geteiltem LibreOffice-Profil rund 1,0 s. Echtzeit ist darüber nicht zu
/// haben — eine kürzere Schreibpause würde nur mehr Umwandlungen stapeln.
///
/// Deshalb dieser Zwischenschritt: Der neue Wortlaut steht sofort da, das echte
/// Bild zieht still nach und ersetzt ihn. Die Sofortanzeige ist bewusst eine
/// Näherung — sie zeigt den Wortlaut, nicht den endgültigen Zeilenumbruch. Sie
/// erfindet aber nichts: Ohne Wörter im Blatt gibt es auch nichts zu zeigen.
/// </summary>
public static class DossierPreviewLiveText
{
    private const string Kennzeichen = "DossierSofortanzeige";

    /// <summary>Ist dieses Element eine Sofortanzeige?</summary>
    public static bool IstSofortanzeige(FrameworkElement element)
        => element is not null && Equals(element.Tag, Kennzeichen);

    /// <summary>
    /// Zeigt <paramref name="text"/> über den <paramref name="stellen"/>.
    /// Eine vorherige Sofortanzeige wird dabei ersetzt, nicht gestapelt.
    /// </summary>
    public static void Zeige(
        Canvas blatt,
        IReadOnlyList<Border> stellen,
        string text)
    {
        ArgumentNullException.ThrowIfNull(blatt);
        ArgumentNullException.ThrowIfNull(stellen);

        Entferne(blatt);

        if (stellen.Count == 0)
            return;

        var links = stellen.Min(stelle => Canvas.GetLeft(stelle));
        var oben = stellen.Min(stelle => Canvas.GetTop(stelle));
        var rechts = stellen.Max(stelle => Canvas.GetLeft(stelle) + Breite(stelle));
        var unten = stellen.Max(stelle => Canvas.GetTop(stelle) + Hoehe(stelle));

        var hoehe = Math.Max(6, unten - oben);

        var anzeige = new Border
        {
            Tag = Kennzeichen,
            Width = Math.Max(6, rechts - links),
            MinHeight = hoehe,

            // Weiss wie das Blatt: Der alte Text muss verschwinden, sonst
            // stuenden beide Fassungen uebereinander.
            Background = Brushes.White,
            IsHitTestVisible = false,
            Child = new TextBlock
            {
                Text = text ?? string.Empty,
                FontFamily = new FontFamily("Arial"),
                FontSize = Schriftgroesse(hoehe),
                Foreground = Brushes.Black,
                TextWrapping = TextWrapping.Wrap,
                VerticalAlignment = VerticalAlignment.Top
            }
        };

        Canvas.SetLeft(anzeige, links);
        Canvas.SetTop(anzeige, oben);
        Panel.SetZIndex(anzeige, 5);
        blatt.Children.Add(anzeige);
    }

    /// <summary>Nimmt eine vorhandene Sofortanzeige wieder weg.</summary>
    public static void Entferne(Canvas blatt)
    {
        ArgumentNullException.ThrowIfNull(blatt);

        foreach (var alt in blatt.Children.OfType<FrameworkElement>()
                     .Where(IstSofortanzeige)
                     .ToList())
        {
            blatt.Children.Remove(alt);
        }
    }

    /// <summary>
    /// Die Schrift folgt der Zeilenhoehe der ueberdeckten Woerter. Eine feste
    /// Groesse saehe auf dem Deckblatt winzig und in der Tabelle riesig aus.
    /// Die vier Pixel sind der Rand, den der Rahmen um das Wort legt.
    /// </summary>
    private static double Schriftgroesse(double hoehe)
        => Math.Clamp(hoehe - 4, 7, 40);

    private static double Breite(Border stelle)
        => double.IsNaN(stelle.Width) ? stelle.ActualWidth : stelle.Width;

    private static double Hoehe(Border stelle)
        => double.IsNaN(stelle.Height) ? stelle.ActualHeight : stelle.Height;
}
