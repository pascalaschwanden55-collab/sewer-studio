using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

using AuswertungPro.Next.Application.Dossiers;
using AuswertungPro.Next.UI.Services;

using UglyToad.PdfPig;

namespace AuswertungPro.Next.UI.Views.Windows;

/// <summary>
/// Zeigt vor dem Gesamt-PDF alle Blätter auf einmal — jedes mit einem Haken,
/// alle gesetzt.
///
/// Gezeigt wird die fertig zusammengeführte Datei, also Word-Teil UND
/// Beilagen. Erst dort stehen die Blätter fest; vorher liesse sich gar nicht
/// zeigen, was am Ende in der Datei stünde.
///
/// Ein abgewähltes Blatt fehlt nur in dieser Ausgabe. Word-Datei und
/// Original-Protokolle bleiben unverändert — deshalb wird auch nur die
/// PDF-Kopie gefiltert und nie eine Quelle angefasst.
/// Die automatisch erkannten Erklärseiten werden sichtbar als Pflichtblätter
/// markiert und können nicht abgewählt werden.
/// </summary>
public partial class DossierPageSelectionWindow : Window
{
    private readonly byte[] _pdf;
    private readonly IDossierPreviewPageRasterizer _seiten;
    private readonly DossierPageSelection _auswahl;
    private readonly CancellationTokenSource _lebenszeit = new();

    private const uint Vorschaubreite = 260;

    private DossierPageSelectionWindow(
        byte[] pdf,
        int blaetter,
        IReadOnlySet<int> pflichtblaetter,
        IDossierPreviewPageRasterizer seiten)
    {
        InitializeComponent();

        _pdf = pdf;
        _seiten = seiten;
        _auswahl = new DossierPageSelection(blaetter, pflichtblaetter);

        Closed += (_, _) =>
        {
            _lebenszeit.Cancel();
            _lebenszeit.Dispose();
        };

        BaueKarten();
        ZeigeZusammenfassung();
        _ = FuelleVorschauenAsync();
    }

    /// <summary>
    /// Fragt die Blätter ab. Zurück kommen die Seitennummern, die NICHT in die
    /// Datei sollen — oder <c>null</c>, wenn abgebrochen wurde.
    /// </summary>
    public static IReadOnlySet<int>? Frage(
        byte[] pdf,
        IDossierPreviewPageRasterizer seiten,
        Window? besitzer)
    {
        ArgumentNullException.ThrowIfNull(pdf);
        ArgumentNullException.ThrowIfNull(seiten);

        int blaetter;
        IReadOnlySet<int> pflichtblaetter;
        try
        {
            using var dokument = PdfDocument.Open(pdf);
            blaetter = dokument.NumberOfPages;
            pflichtblaetter = FindePflichtblaetter(dokument);
        }
        catch (Exception)
        {
            // Ist die Datei nicht lesbar, wird nicht heimlich alles genommen —
            // der Zusammenbau meldet den Fehler an seiner Stelle.
            return new HashSet<int>();
        }

        if (blaetter <= 1)
            return new HashSet<int>();

        var fenster = new DossierPageSelectionWindow(pdf, blaetter, pflichtblaetter, seiten)
        {
            Owner = besitzer
        };

        return fenster.ShowDialog() == true ? fenster._auswahl.Ausgeschlossen : null;
    }

    internal static IReadOnlySet<int> FindePflichtblaetter(byte[] pdf)
    {
        ArgumentNullException.ThrowIfNull(pdf);
        using var dokument = PdfDocument.Open(pdf);
        return FindePflichtblaetter(dokument);
    }

    private static IReadOnlySet<int> FindePflichtblaetter(PdfDocument dokument)
    {
        var gefunden = new HashSet<int>();
        foreach (var seite in dokument.GetPages())
        {
            var text = string.Join(" ", seite.GetWords().Select(wort => wort.Text));
            if (text.Contains(
                    DossierConditionClassDefinitions.PdfRequiredPageMarker,
                    StringComparison.Ordinal))
            {
                gefunden.Add(seite.Number);
            }
        }

        return gefunden;
    }

    /// <summary>Für jedes Blatt eine Karte — zuerst ohne Bild, damit das Fenster sofort steht.</summary>
    private void BaueKarten()
    {
        for (var nummer = 1; nummer <= _auswahl.Blaetter; nummer++)
            Blaetter.Children.Add(BaueKarte(nummer));
    }

    private Border BaueKarte(int nummer)
    {
        var istPflichtblatt = _auswahl.IstPflichtblatt(nummer);
        var bild = new Image
        {
            Width = Vorschaubreite,
            Height = Vorschaubreite * 1.414,
            Stretch = Stretch.Uniform,
            Margin = new Thickness(0, 0, 0, 6)
        };

        var haken = new CheckBox
        {
            Content = istPflichtblatt
                ? $"Blatt {nummer} · Dossier-Erklärung (Pflichtblatt)"
                : $"Blatt {nummer}",
            IsChecked = true,
            IsEnabled = !istPflichtblatt,
            ToolTip = istPflichtblatt
                ? "Diese Erklärseite gehört zu jedem Eigentümerdossier."
                : null,
            Foreground = (Brush)FindResource("TextBrush")
        };

        var rahmen = new Border
        {
            Margin = new Thickness(0, 0, 12, 12),
            Padding = new Thickness(8),
            BorderBrush = (Brush)FindResource("BorderBrush"),
            BorderThickness = new Thickness(1),
            Background = Brushes.White,
            Tag = bild,
            Child = new StackPanel { Children = { bild, haken } }
        };

        void Uebernimm(bool gewaehlt)
        {
            _auswahl.Setze(nummer, gewaehlt);

            // Abgewählt heisst sichtbar abgewählt: blass und ohne Rahmenfarbe.
            bild.Opacity = gewaehlt ? 1 : 0.28;
            rahmen.BorderBrush = gewaehlt
                ? (Brush)FindResource("BorderBrush")
                : Brushes.Transparent;

            ZeigeZusammenfassung();
        }

        haken.Checked += (_, _) => Uebernimm(true);
        haken.Unchecked += (_, _) => Uebernimm(false);

        // Ein Klick aufs Bild schaltet mit — bequemer als der kleine Haken.
        bild.MouseLeftButtonUp += (_, _) =>
        {
            if (!istPflichtblatt)
                haken.IsChecked = haken.IsChecked != true;
        };
        bild.Cursor = istPflichtblatt
            ? System.Windows.Input.Cursors.Arrow
            : System.Windows.Input.Cursors.Hand;

        return rahmen;
    }

    /// <summary>
    /// Zeichnet die Vorschaubilder nacheinander nach. So steht das Fenster
    /// sofort da, auch wenn ein Dossier viele Beilagen hat.
    /// </summary>
    private async Task FuelleVorschauenAsync()
    {
        for (var nummer = 1; nummer <= _auswahl.Blaetter; nummer++)
        {
            if (_lebenszeit.IsCancellationRequested)
                return;

            try
            {
                var bitmap = await _seiten
                    .RenderAsync(_pdf, nummer - 1, Vorschaubreite, _lebenszeit.Token)
                    .ConfigureAwait(true);

                if (_lebenszeit.IsCancellationRequested)
                    return;

                if (Blaetter.Children[nummer - 1] is Border { Tag: Image bild })
                    bild.Source = bitmap;
            }
            catch (OperationCanceledException)
            {
                return;
            }
            catch (Exception)
            {
                // Ein nicht zeichenbares Blatt bleibt leer — abwaehlen laesst
                // es sich trotzdem.
            }
        }
    }

    private void ZeigeZusammenfassung()
    {
        Zusammenfassung.Text = _auswahl.Beschreibung;
        BtnErzeugen.IsEnabled = _auswahl.DarfErzeugen;
    }

    private void OnAlle(object sender, RoutedEventArgs e)
    {
        _ = sender;
        _ = e;
        SetzeAlleHaken(true);
    }

    private void OnKeine(object sender, RoutedEventArgs e)
    {
        _ = sender;
        _ = e;
        SetzeAlleHaken(false);
    }

    private void SetzeAlleHaken(bool gewaehlt)
    {
        foreach (var karte in Blaetter.Children)
        {
            if (karte is Border { Child: StackPanel stapel })
            {
                foreach (var kind in stapel.Children)
                {
                    if (kind is CheckBox { IsEnabled: true } haken)
                        haken.IsChecked = gewaehlt;
                }
            }
        }
    }

    private void OnErzeugen(object sender, RoutedEventArgs e)
    {
        _ = sender;
        _ = e;

        if (!_auswahl.DarfErzeugen)
            return;

        DialogResult = true;
    }
}
