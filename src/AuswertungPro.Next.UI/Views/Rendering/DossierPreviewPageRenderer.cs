using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Media;

using AuswertungPro.Next.Application.Dossiers.Preview;

namespace AuswertungPro.Next.UI.Views.Rendering;

/// <summary>
/// Das Ergebnis einer Zeichnung: die Seite und die Stellen, an denen ein Feld
/// im Blatt erscheint. Ueber diese Stellen wird spaeter hervorgehoben.
/// </summary>
public sealed class DossierPreviewRenderResult
{
    public required Panel Root { get; init; }

    /// <summary>Rahmen je Platzhalter — ein Feld kann mehrfach vorkommen.</summary>
    public required IReadOnlyDictionary<string, IReadOnlyList<Border>> Frames { get; init; }

    /// <summary>Textstuecke je Platzhalter.</summary>
    public required IReadOnlyDictionary<string, IReadOnlyList<Run>> Runs { get; init; }
}

/// <summary>Der Rand eines Rahmens vor jeder Hervorhebung.</summary>
public sealed record DossierPreviewFrameOrigin(Brush? BorderBrush, Thickness BorderThickness);

/// <summary>
/// Zeichnet eine Vorschauseite als Blatt.
///
/// Bewusst zustandslos und ohne Kenntnis der Dossierdaten: Werte und
/// Tabellenzeilen kommen als Funktionen herein. Dadurch zeichnet dasselbe
/// Verfahren die Seite bei jeder Aenderung neu, und die Tabellen wachsen
/// einfach mit ihrem Inhalt.
/// </summary>
public static class DossierPreviewPageRenderer
{
    private static readonly SolidColorBrush Papier = Farbe(0xFF, 0xFF, 0xFF);
    private static readonly SolidColorBrush Tinte = Farbe(0x1A, 0x1A, 0x1A);
    private static readonly SolidColorBrush Blass = Farbe(0x70, 0x70, 0x70);
    private static readonly SolidColorBrush Linie = Farbe(0xBB, 0xBB, 0xBB);
    private static readonly SolidColorBrush Kopfzeile = Farbe(0xEC, 0xF1, 0xF7);

    private static SolidColorBrush Farbe(byte r, byte g, byte b)
    {
        var brush = new SolidColorBrush(Color.FromRgb(r, g, b));
        brush.Freeze();
        return brush;
    }

    public static DossierPreviewRenderResult Render(
        DossierPreviewPage page,
        Func<string, string> value,
        Func<string, IReadOnlyList<IReadOnlyDictionary<string, string>>> rows)
    {
        ArgumentNullException.ThrowIfNull(page);
        ArgumentNullException.ThrowIfNull(value);
        ArgumentNullException.ThrowIfNull(rows);

        var rahmen = new Dictionary<string, List<Border>>(StringComparer.Ordinal);
        var stuecke = new Dictionary<string, List<Run>>(StringComparer.Ordinal);

        // Der urspruengliche Rand wird mitgegeben. Ohne ihn wuerde das
        // Zuruecksetzen der Hervorhebung die Linien der Tabelle mitloeschen.
        void MerkeRahmen(string key, Border border)
        {
            if (!rahmen.TryGetValue(key, out var liste))
                rahmen[key] = liste = new List<Border>();

            border.Tag = new DossierPreviewFrameOrigin(border.BorderBrush, border.BorderThickness);
            liste.Add(border);
        }

        void MerkeRun(string key, Run run)
        {
            if (!stuecke.TryGetValue(key, out var liste))
                stuecke[key] = liste = new List<Run>();

            liste.Add(run);
        }

        var blatt = new StackPanel { Background = Papier };

        foreach (var block in page.Blocks)
        {
            switch (block)
            {
                case DossierPreviewParagraph absatz:
                    blatt.Children.Add(ZeichneAbsatz(absatz, value, MerkeRahmen, MerkeRun));
                    break;

                case DossierPreviewImage bild:
                    blatt.Children.Add(ZeichneBild(bild, value, MerkeRahmen));
                    break;

                case DossierPreviewTable tabelle:
                    blatt.Children.Add(ZeichneTabelle(tabelle, value, rows, MerkeRahmen, MerkeRun));
                    break;
            }
        }

        return new DossierPreviewRenderResult
        {
            Root = blatt,
            Frames = rahmen.ToDictionary(
                p => p.Key, p => (IReadOnlyList<Border>)p.Value, StringComparer.Ordinal),
            Runs = stuecke.ToDictionary(
                p => p.Key, p => (IReadOnlyList<Run>)p.Value, StringComparer.Ordinal)
        };
    }

    private static Border ZeichneAbsatz(
        DossierPreviewParagraph absatz,
        Func<string, string> value,
        Action<string, Border> merkeRahmen,
        Action<string, Run> merkeRun)
    {
        var text = new TextBlock
        {
            TextWrapping = TextWrapping.Wrap,
            Foreground = Tinte,
            FontFamily = new FontFamily("Arial"),
            FontSize = absatz.Style switch
            {
                DossierPreviewStyle.Title => 20,
                DossierPreviewStyle.Heading => 15,
                DossierPreviewStyle.Small => 11,
                _ => 12
            },
            FontWeight = absatz.Style is DossierPreviewStyle.Title or DossierPreviewStyle.Heading
                ? FontWeights.Bold
                : FontWeights.Normal
        };

        if (absatz.Style == DossierPreviewStyle.Small)
            text.Foreground = Blass;

        var rahmen = new Border
        {
            Child = text,
            Padding = new Thickness(2, 1, 2, 1),
            Margin = new Thickness(0, absatz.Style == DossierPreviewStyle.Heading ? 14 : 2, 0, 2),
            Background = Brushes.Transparent
        };

        foreach (var run in absatz.Runs)
        {
            if (!run.IsField)
            {
                text.Inlines.Add(new Run(run.Text));
                continue;
            }

            var inhalt = value(run.FieldKey!);
            var leer = string.IsNullOrWhiteSpace(inhalt);

            // Eine leere Stelle wird sichtbar gemacht: sonst sieht die Vorschau
            // fertig aus, obwohl das Feld im Dokument leer bleibt.
            var stueck = new Run(leer ? "———" : inhalt)
            {
                Foreground = leer ? Blass : Tinte
            };

            text.Inlines.Add(stueck);
            merkeRun(run.FieldKey!, stueck);
            merkeRahmen(run.FieldKey!, rahmen);
        }

        return rahmen;
    }

    private static Border ZeichneBild(
        DossierPreviewImage bild,
        Func<string, string> value,
        Action<string, Border> merkeRahmen)
    {
        var pfad = value(bild.FieldKey);

        var inhalt = new TextBlock
        {
            Text = string.IsNullOrWhiteSpace(pfad)
                ? "Kein Übersichtsplan gewählt"
                : System.IO.Path.GetFileName(pfad),
            Foreground = Blass,
            FontFamily = new FontFamily("Arial"),
            FontSize = 12,
            HorizontalAlignment = HorizontalAlignment.Center,
            VerticalAlignment = VerticalAlignment.Center
        };

        var rahmen = new Border
        {
            Child = inhalt,
            Height = 320,
            BorderBrush = Linie,
            BorderThickness = new Thickness(1),
            Margin = new Thickness(0, 8, 0, 8),
            Background = Brushes.Transparent
        };

        merkeRahmen(bild.FieldKey, rahmen);
        return rahmen;
    }

    private static Border ZeichneTabelle(
        DossierPreviewTable tabelle,
        Func<string, string> value,
        Func<string, IReadOnlyList<IReadOnlyDictionary<string, string>>> rows,
        Action<string, Border> merkeRahmen,
        Action<string, Run> merkeRun)
    {
        var spalten = Math.Max(1, tabelle.HeaderCells.Count);
        var raster = new Grid();

        for (var i = 0; i < spalten; i++)
        {
            // Die letzte Spalte traegt den Fliesstext und bekommt den Rest.
            raster.ColumnDefinitions.Add(new ColumnDefinition
            {
                Width = i == spalten - 1 ? new GridLength(1, GridUnitType.Star) : GridLength.Auto
            });
        }

        var zeile = 0;

        void Zelle(string text, int spalte, bool kopf, string? feldKey)
        {
            var block = new TextBlock
            {
                Text = text,
                TextWrapping = TextWrapping.Wrap,
                Foreground = Tinte,
                FontFamily = new FontFamily("Arial"),
                FontSize = 11,
                FontWeight = kopf ? FontWeights.Bold : FontWeights.Normal,
                MinWidth = kopf ? 60 : 0
            };

            var rahmen = new Border
            {
                Child = block,
                BorderBrush = Linie,
                BorderThickness = new Thickness(1, 1, 0, 0),
                Padding = new Thickness(5, 3, 5, 3),
                Background = kopf ? Kopfzeile : Brushes.Transparent
            };

            Grid.SetRow(rahmen, zeile);
            Grid.SetColumn(rahmen, spalte);
            raster.Children.Add(rahmen);

            if (feldKey is not null)
                merkeRahmen(feldKey, rahmen);
        }

        raster.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        for (var i = 0; i < tabelle.HeaderCells.Count; i++)
            Zelle(tabelle.HeaderCells[i], i, kopf: true, null);

        foreach (var feste in tabelle.FixedRowCells.Chunk(spalten))
        {
            zeile++;
            raster.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });

            for (var i = 0; i < feste.Length && i < spalten; i++)
            {
                var text = string.Concat(feste[i].Select(r =>
                    r.IsField ? value(r.FieldKey!) : r.Text));
                Zelle(text, i, kopf: false, feste[i].FirstOrDefault(r => r.IsField)?.FieldKey);
            }
        }

        if (tabelle.RepeatKey is not null)
        {
            var daten = rows(tabelle.RepeatKey);

            if (daten.Count == 0)
            {
                zeile++;
                raster.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
                Zelle("— noch keine Zeile —", 0, kopf: false, tabelle.RepeatKey);
            }

            foreach (var satz in daten)
            {
                zeile++;
                raster.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });

                for (var i = 0; i < spalten; i++)
                {
                    var key = i < tabelle.RepeatCellKeys.Count ? tabelle.RepeatCellKeys[i] : "";
                    var text = key.Length > 0 && satz.TryGetValue(key, out var wert) ? wert : "";
                    Zelle(text, i, kopf: false, tabelle.RepeatKey);
                }
            }
        }

        return new Border
        {
            Child = raster,
            BorderBrush = Linie,
            BorderThickness = new Thickness(0, 0, 1, 1),
            Margin = new Thickness(0, 6, 0, 10),
            Background = Brushes.Transparent
        };
    }
}
