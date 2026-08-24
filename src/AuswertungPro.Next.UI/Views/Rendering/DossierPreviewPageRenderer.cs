using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Media;
using System.Windows.Media.Imaging;

using AuswertungPro.Next.Application.Dossiers.Preview;

namespace AuswertungPro.Next.UI.Views.Rendering;

/// <summary>
/// Das Ergebnis einer Zeichnung: die Seite und die Stellen, an denen ein Feld
/// im Blatt erscheint. Ueber diese Stellen wird spaeter hervorgehoben.
/// </summary>
public sealed class DossierPreviewRenderResult
{
    public required FrameworkElement Root { get; init; }

    /// <summary>Rahmen je Platzhalter — ein Feld kann mehrfach vorkommen.</summary>
    public required IReadOnlyDictionary<string, IReadOnlyList<Border>> Frames { get; init; }
}

/// <summary>Der Rand eines Rahmens vor jeder Hervorhebung.</summary>
public sealed record DossierPreviewFrameOrigin(Brush? BorderBrush, Thickness BorderThickness);

/// <summary>
/// Zeichnet eine Vorschauseite als Blatt — in den Massen der Vorlage.
///
/// Blattformat, Raender, Spaltenbreiten, Zeilen- und Absatzabstaende, Schriften
/// und die Lage der schwebenden Kaesten stammen unveraendert aus der Worddatei.
/// Alles ist in Bildpunkten bei 96 dpi gerechnet, so wie Word bei 100 % zeigt.
///
/// Bewusst zustandslos: Werte und Tabellenzeilen kommen als Funktionen herein.
/// Dadurch zeichnet dasselbe Verfahren die Seite bei jeder Aenderung neu, und
/// die Tabellen wachsen mit ihrem Inhalt.
/// </summary>
public static class DossierPreviewPageRenderer
{
    private static readonly SolidColorBrush Papier = Fest(Color.FromRgb(0xFF, 0xFF, 0xFF));
    private static readonly SolidColorBrush Tinte = Fest(Color.FromRgb(0x00, 0x00, 0x00));
    private static readonly SolidColorBrush Blass = Fest(Color.FromRgb(0x90, 0x90, 0x90));

    private static SolidColorBrush Fest(Color farbe)
    {
        var brush = new SolidColorBrush(farbe);
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

        void Merke(string key, Border border)
        {
            if (!rahmen.TryGetValue(key, out var liste))
                rahmen[key] = liste = new List<Border>();

            // Der urspruengliche Rand wird mitgegeben. Ohne ihn wuerde das
            // Zuruecksetzen der Hervorhebung die Linien der Tabelle mitloeschen.
            border.Tag = new DossierPreviewFrameOrigin(border.BorderBrush, border.BorderThickness);
            liste.Add(border);
        }

        var blatt = new Grid
        {
            Width = page.Geometry.WidthPx,
            MinHeight = page.Geometry.HeightPx,
            Background = Papier
        };

        var fluss = new StackPanel
        {
            Margin = new Thickness(
                page.Geometry.Margin.Left,
                page.Geometry.Margin.Top,
                page.Geometry.Margin.Right,
                page.Geometry.Margin.Bottom),
            VerticalAlignment = VerticalAlignment.Top
        };

        foreach (var block in page.Blocks)
            fluss.Children.Add(ZeichneBlock(block, value, rows, Merke, page.Geometry.Margin.Left));

        blatt.Children.Add(fluss);

        return new DossierPreviewRenderResult
        {
            Root = blatt,
            Frames = rahmen.ToDictionary(
                p => p.Key, p => (IReadOnlyList<Border>)p.Value, StringComparer.Ordinal)
        };
    }

    private static FrameworkElement ZeichneBlock(
        DossierPreviewBlock block,
        Func<string, string> value,
        Func<string, IReadOnlyList<IReadOnlyDictionary<string, string>>> rows,
        Action<string, Border> merke,
        double randLinks = 0)
        => block switch
        {
            DossierPreviewParagraph absatz => MitKaesten(absatz, value, rows, merke, randLinks),
            DossierPreviewTable tabelle => ZeichneTabelle(tabelle, value, rows, merke),
            DossierPreviewPicture bild => ZeichneBild(bild),
            DossierPreviewImage stelle => ZeichneBildstelle(stelle, value, merke),
            _ => new Border()
        };

    /// <summary>
    /// Der Absatz und die Kaesten, die Word an ihn haengt. Word zaehlt deren
    /// Hoehe ab diesem Absatz — deshalb liegen sie in einer Ebene GENAU hier
    /// und nicht auf einer Leinwand ueber der ganzen Seite. Waagrecht zaehlt
    /// der Blattrand, deshalb der Abzug des linken Randes.
    /// </summary>
    private static FrameworkElement MitKaesten(
        DossierPreviewParagraph absatz,
        Func<string, string> value,
        Func<string, IReadOnlyList<IReadOnlyDictionary<string, string>>> rows,
        Action<string, Border> merke,
        double randLinks)
    {
        var text = ZeichneAbsatz(absatz, value, merke);

        if (absatz.Floating.Count == 0)
            return text;

        var stapel = new Grid { HorizontalAlignment = HorizontalAlignment.Stretch };
        stapel.Children.Add(text);

        var leinwand = new Canvas
        {
            VerticalAlignment = VerticalAlignment.Top,
            HorizontalAlignment = HorizontalAlignment.Left,
            Width = 0,
            Height = 0
        };

        foreach (var kasten in absatz.Floating)
        {
            var element = ZeichneKasten(kasten, value, rows, merke);
            Canvas.SetLeft(element, kasten.LeftPx - randLinks);
            Canvas.SetTop(element, kasten.TopPx);
            leinwand.Children.Add(element);
        }

        stapel.Children.Add(leinwand);
        return stapel;
    }

    private static Border ZeichneKasten(
        DossierPreviewFloating kasten,
        Func<string, string> value,
        Func<string, IReadOnlyList<IReadOnlyDictionary<string, string>>> rows,
        Action<string, Border> merke)
    {
        var inhalt = new StackPanel();
        foreach (var block in kasten.Blocks)
            inhalt.Children.Add(ZeichneBlock(block, value, rows, merke));

        inhalt.Margin = new Thickness(2, 1, 2, 1);

        return new Border
        {
            Width = kasten.WidthPx > 0 ? kasten.WidthPx : double.NaN,
            MinHeight = kasten.HeightPx,
            Child = inhalt,
            BorderThickness = new Thickness(kasten.BorderWidthPx),
            BorderBrush = Pinsel(kasten.BorderColorHex),
            Background = Pinsel(kasten.FillHex) ?? Brushes.Transparent
        };
    }

    private static Border ZeichneAbsatz(
        DossierPreviewParagraph absatz,
        Func<string, string> value,
        Action<string, Border> merke)
    {
        var erster = absatz.Runs.FirstOrDefault()?.Format ?? DossierPreviewRunFormat.Default;

        var text = new TextBlock
        {
            TextWrapping = TextWrapping.Wrap,
            Foreground = Tinte,
            FontFamily = new FontFamily(erster.FontFamily),
            FontSize = erster.FontSizePx,
            TextAlignment = absatz.Format.Alignment switch
            {
                DossierPreviewAlignment.Center => TextAlignment.Center,
                DossierPreviewAlignment.Right => TextAlignment.Right,
                DossierPreviewAlignment.Justify => TextAlignment.Justify,
                _ => TextAlignment.Left
            }
        };

        if (absatz.Format.LineHeightPx is { } hoehe && hoehe > 0)
        {
            text.LineHeight = hoehe;
            text.LineStackingStrategy = LineStackingStrategy.BlockLineHeight;
        }

        foreach (var run in absatz.Runs)
        {
            var inhalt = run.IsField ? value(run.FieldKey!) : run.Text ?? string.Empty;
            var leer = run.IsField && string.IsNullOrWhiteSpace(inhalt);

            // Eine leere Stelle wird sichtbar gemacht: sonst sieht die Vorschau
            // fertig aus, obwohl das Feld im Dokument leer bleibt.
            var stueck = new Run(leer ? "———" : inhalt)
            {
                FontFamily = new FontFamily(run.Format.FontFamily),
                FontSize = run.Format.FontSizePx,
                FontWeight = run.Format.Bold ? FontWeights.Bold : FontWeights.Normal,
                FontStyle = run.Format.Italic ? FontStyles.Italic : FontStyles.Normal,
                Foreground = leer ? Blass : (Pinsel(run.Format.ColorHex) ?? Tinte)
            };

            if (run.Format.Underline)
                stueck.TextDecorations = TextDecorations.Underline;

            text.Inlines.Add(stueck);
        }

        // Ein leerer Absatz traegt im Dokument den senkrechten Abstand; ohne
        // Mindesthoehe faellt er in der Vorschau auf null zusammen und alles
        // darunter rutscht nach oben.
        if (absatz.Runs.All(r => string.IsNullOrEmpty(r.IsField ? value(r.FieldKey!) : r.Text)))
            text.MinHeight = erster.FontSizePx * 1.2;

        var rahmen = new Border
        {
            Child = text,
            Background = Brushes.Transparent,
            Margin = new Thickness(
                absatz.Format.Indent.Left,
                absatz.Format.SpaceBeforePx,
                absatz.Format.Indent.Right,
                absatz.Format.SpaceAfterPx)
        };

        foreach (var run in absatz.Runs.Where(r => r.IsField))
            merke(run.FieldKey!, rahmen);

        return rahmen;
    }

    private static FrameworkElement ZeichneBild(DossierPreviewPicture bild)
    {
        try
        {
            var quelle = new BitmapImage();
            quelle.BeginInit();
            quelle.CacheOption = BitmapCacheOption.OnLoad;
            quelle.StreamSource = new System.IO.MemoryStream(bild.Bytes);
            quelle.EndInit();
            quelle.Freeze();

            return new Image
            {
                Source = quelle,
                Width = bild.WidthPx > 0 ? bild.WidthPx : double.NaN,
                Height = bild.HeightPx > 0 ? bild.HeightPx : double.NaN,
                Stretch = Stretch.Fill,
                HorizontalAlignment = HorizontalAlignment.Left
            };
        }
        catch (Exception)
        {
            // Ein unlesbares Bild darf die Vorschau nicht verhindern.
            return new Border { Width = bild.WidthPx, Height = bild.HeightPx };
        }
    }

    private static Border ZeichneBildstelle(
        DossierPreviewImage stelle,
        Func<string, string> value,
        Action<string, Border> merke)
    {
        var pfad = value(stelle.FieldKey);

        var rahmen = new Border
        {
            Width = stelle.WidthPx,
            Height = stelle.HeightPx,
            BorderBrush = Blass,
            BorderThickness = new Thickness(1),
            Background = Brushes.Transparent,
            Margin = new Thickness(0, 8, 0, 8)
        };

        if (string.IsNullOrWhiteSpace(pfad))
        {
            rahmen.Child = new TextBlock
            {
                Text = "Kein Übersichtsplan gewählt",
                Foreground = Blass,
                FontFamily = new FontFamily("Arial"),
                FontSize = 12,
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center
            };
        }
        else
        {
            rahmen.Child = LadeFuerVorschau(pfad, stelle);
        }

        merke(stelle.FieldKey, rahmen);
        return rahmen;
    }

    private static FrameworkElement LadeFuerVorschau(string pfad, DossierPreviewImage stelle)
    {
        try
        {
            var quelle = new BitmapImage();
            quelle.BeginInit();
            quelle.CacheOption = BitmapCacheOption.OnLoad;
            quelle.UriSource = new Uri(pfad);
            quelle.EndInit();
            quelle.Freeze();

            return new Image { Source = quelle, Stretch = Stretch.Uniform };
        }
        catch (Exception)
        {
            return new TextBlock
            {
                Text = System.IO.Path.GetFileName(pfad),
                Foreground = Blass,
                FontFamily = new FontFamily("Arial"),
                FontSize = 12,
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center
            };
        }
    }

    private static FrameworkElement ZeichneTabelle(
        DossierPreviewTable tabelle,
        Func<string, string> value,
        Func<string, IReadOnlyList<IReadOnlyDictionary<string, string>>> rows,
        Action<string, Border> merke)
    {
        var raster = new Grid
        {
            Margin = new Thickness(tabelle.IndentPx, 6, 0, 10),
            HorizontalAlignment = HorizontalAlignment.Left
        };

        foreach (var breite in tabelle.ColumnWidthsPx)
            raster.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(breite) });

        if (raster.ColumnDefinitions.Count == 0)
            raster.ColumnDefinitions.Add(new ColumnDefinition());

        var zeile = 0;

        void Setze(DossierPreviewTableRow satz, Func<int, string?> ueberschreiben, string? feldKey)
        {
            raster.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });

            var spalte = 0;
            for (var i = 0; i < satz.Cells.Count && spalte < raster.ColumnDefinitions.Count; i++)
            {
                var zelle = satz.Cells[i];
                var element = ZeichneZelle(zelle, value, ueberschreiben(i), feldKey, merke);

                Grid.SetRow(element, zeile);
                Grid.SetColumn(element, spalte);
                Grid.SetColumnSpan(element, Math.Max(1, zelle.GridSpan));
                raster.Children.Add(element);

                spalte += Math.Max(1, zelle.GridSpan);
            }

            zeile++;
        }

        foreach (var satz in tabelle.Rows)
            Setze(satz, _ => null, null);

        if (tabelle.RepeatKey is not null && tabelle.RepeatTemplate is not null)
        {
            var daten = rows(tabelle.RepeatKey);

            if (daten.Count == 0)
            {
                Setze(tabelle.RepeatTemplate,
                    i => i == 0 ? "— noch keine Zeile —" : string.Empty,
                    tabelle.RepeatKey);
            }

            foreach (var satz in daten)
            {
                Setze(
                    tabelle.RepeatTemplate,
                    i =>
                    {
                        var key = i < tabelle.RepeatCellKeys.Count
                            ? tabelle.RepeatCellKeys[i]
                            : string.Empty;

                        return key.Length > 0 && satz.TryGetValue(key, out var wert)
                            ? wert
                            : string.Empty;
                    },
                    tabelle.RepeatKey);
            }
        }

        return raster;
    }

    private static FrameworkElement ZeichneZelle(
        DossierPreviewTableCell zelle,
        Func<string, string> value,
        string? ersatztext,
        string? feldKey,
        Action<string, Border> merke)
    {
        var inhalt = new StackPanel();

        if (ersatztext is null)
        {
            foreach (var absatz in zelle.Paragraphs)
                inhalt.Children.Add(ZeichneAbsatz(absatz, value, merke));
        }
        else
        {
            // Eine erzeugte Zeile uebernimmt Schrift und Ausrichtung ihres
            // Bauplans, traegt aber den Text der Daten.
            var vorbild = zelle.Paragraphs.FirstOrDefault();
            var format = vorbild?.Runs.FirstOrDefault()?.Format ?? DossierPreviewRunFormat.Default;

            inhalt.Children.Add(ZeichneAbsatz(
                new DossierPreviewParagraph(
                    new[] { DossierPreviewRun.Literal(ersatztext, format) },
                    vorbild?.Format ?? DossierPreviewParagraphFormat.Default),
                value,
                merke));
        }

        var rahmen = new Border
        {
            Child = inhalt,
            Padding = new Thickness(
                zelle.Padding.Left, zelle.Padding.Top, zelle.Padding.Right, zelle.Padding.Bottom),
            BorderThickness = new Thickness(
                zelle.Borders.Left, zelle.Borders.Top, zelle.Borders.Right, zelle.Borders.Bottom),
            BorderBrush = Tinte,
            Background = Pinsel(zelle.ShadingHex) ?? Brushes.Transparent
        };

        if (feldKey is not null)
            merke(feldKey, rahmen);

        return rahmen;
    }

    private static SolidColorBrush? Pinsel(string? hex)
    {
        if (string.IsNullOrWhiteSpace(hex) || hex.Length != 6)
            return null;

        if (!int.TryParse(hex, NumberStyles.HexNumber, CultureInfo.InvariantCulture, out var wert))
            return null;

        return Fest(Color.FromRgb(
            (byte)((wert >> 16) & 0xFF), (byte)((wert >> 8) & 0xFF), (byte)(wert & 0xFF)));
    }
}
