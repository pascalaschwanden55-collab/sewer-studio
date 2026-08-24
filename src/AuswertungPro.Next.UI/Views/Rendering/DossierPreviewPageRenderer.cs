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

    /// <summary>Blasser Grund fuer eine noch leere Stelle.</summary>
    private static readonly SolidColorBrush Luecke = Fest(Color.FromRgb(0xF0, 0xF0, 0xF0));

    private static SolidColorBrush Fest(Color farbe)
    {
        var brush = new SolidColorBrush(farbe);
        brush.Freeze();
        return brush;
    }

    public static DossierPreviewRenderResult Render(
        DossierPreviewPage page,
        Func<string, string> value,
        Func<string, IReadOnlyList<IReadOnlyDictionary<string, string>>> rows,
        Func<string, string> emptyRowText)
    {
        ArgumentNullException.ThrowIfNull(page);
        ArgumentNullException.ThrowIfNull(value);
        ArgumentNullException.ThrowIfNull(rows);
        ArgumentNullException.ThrowIfNull(emptyRowText);

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
        {
            fluss.Children.Add(ZeichneBlock(
                block, value, rows, emptyRowText, Merke, page.Geometry.Margin.Left));
        }

        blatt.Children.Add(fluss);

        // Der Umbruch auf eine Folgeseite bleibt Word ueberlassen. Laeuft der
        // Inhalt ueber den Satzspiegel, wird das SICHTBAR gemacht, statt das
        // Blatt stillschweigend wachsen zu lassen — sonst zeigte die Vorschau
        // eine Seite, die es so nie gibt.
        blatt.Children.Add(UeberlaufMarke(page));

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
        Func<string, string> emptyRowText,
        Action<string, Border> merke,
        double randLinks = 0)
        => block switch
        {
            DossierPreviewParagraph absatz
                => MitKaesten(absatz, value, rows, emptyRowText, merke, randLinks),
            DossierPreviewTable tabelle
                => ZeichneTabelle(tabelle, value, rows, emptyRowText, merke),
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
        Func<string, string> emptyRowText,
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
            var element = ZeichneKasten(kasten, value, rows, emptyRowText, merke);
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
        Func<string, string> emptyRowText,
        Action<string, Border> merke)
    {
        var inhalt = new StackPanel();
        foreach (var block in kasten.Blocks)
            inhalt.Children.Add(ZeichneBlock(block, value, rows, emptyRowText, merke));

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
        var schrift = new FontFamily(erster.FontFamily);

        var text = new TextBlock
        {
            TextWrapping = TextWrapping.Wrap,
            Foreground = Tinte,
            FontFamily = schrift,
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

        var offeneStelle = false;

        foreach (var run in absatz.Runs)
        {
            var inhalt = run.IsField ? value(run.FieldKey!) : run.Text ?? string.Empty;
            var leer = run.IsField && string.IsNullOrWhiteSpace(inhalt);

            // Eine leere Stelle bleibt LEER — genau wie im Dokument. Sichtbar
            // wird sie ueber den blassen Grund des Absatzes, nicht ueber
            // erfundene Zeichen; die stuenden so nie im fertigen Dossier.
            var stueck = new Run(inhalt)
            {
                FontFamily = new FontFamily(run.Format.FontFamily),
                FontSize = run.Format.FontSizePx,
                FontWeight = run.Format.Bold ? FontWeights.Bold : FontWeights.Normal,
                FontStyle = run.Format.Italic ? FontStyles.Italic : FontStyles.Normal,
                Foreground = leer ? Blass : (Pinsel(run.Format.ColorHex) ?? Tinte)
            };

            if (run.Format.Underline)
                stueck.TextDecorations = TextDecorations.Underline;

            if (leer)
                offeneStelle = true;

            text.Inlines.Add(stueck);
        }

        // Ein leerer Absatz traegt im Dokument den senkrechten Abstand; ohne
        // Mindesthoehe faellt er in der Vorschau auf null zusammen und alles
        // darunter rutscht nach oben.
        if (absatz.Runs.All(r => string.IsNullOrEmpty(r.IsField ? value(r.FieldKey!) : r.Text)))
        {
            // Die Hoehe einer leeren Zeile kommt aus der Schrift selbst — Word
            // rechnet mit denselben Metriken. Ein fester Faktor 1,2 klingt
            // harmlos, weicht bei Arial aber je Zeile um ein knappes Prozent ab;
            // ueber ein Deckblatt mit drei Dutzend Leerzeilen sind das mehr als
            // zwei Zentimeter, und der Fussstreifen faellt aus dem Rahmen.
            var zeilenmass = schrift.LineSpacing > 0 ? schrift.LineSpacing : 1.2;
            text.MinHeight = erster.FontSizePx * zeilenmass;
        }

        var rahmen = new Border
        {
            Child = text,
            Background = offeneStelle ? Luecke : Brushes.Transparent,
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

    /// <summary>
    /// Die Stelle des Uebersichtsplans. Breite und Seitenverhaeltnis folgen
    /// derselben Regel wie der Export: feste Breite, Hoehe aus dem echten
    /// Bild. Ohne Datei entsteht KEIN erfundener Kasten, sondern ein
    /// schmaler Hinweis — sonst zeigte die Vorschau Platz, den das Dossier
    /// nicht hat.
    /// </summary>
    private static FrameworkElement ZeichneBildstelle(
        DossierPreviewImage stelle,
        Func<string, string> value,
        Action<string, Border> merke)
    {
        var pfad = value(stelle.FieldKey);

        // Die Breite kann das Dossier bestimmen; sonst gilt die der Vorlage.
        var breite = double.TryParse(
            value(stelle.FieldKey + "_BreiteCm"),
            NumberStyles.Float,
            CultureInfo.InvariantCulture,
            out var cm) && cm > 0
                ? cm / 2.54 * 96.0
                : stelle.WidthPx;

        var rahmen = new Border
        {
            Width = breite,
            HorizontalAlignment = HorizontalAlignment.Left,
            BorderBrush = Blass,
            BorderThickness = new Thickness(1),
            Background = Brushes.Transparent,
            Margin = new Thickness(0, 8, 0, 8)
        };

        var bild = string.IsNullOrWhiteSpace(pfad) ? null : Lade(pfad);

        if (bild is null)
        {
            rahmen.Height = 26;
            rahmen.Child = new TextBlock
            {
                Text = string.IsNullOrWhiteSpace(pfad)
                    ? "Kein Übersichtsplan gewählt"
                    : "Übersichtsplan nicht lesbar: " + System.IO.Path.GetFileName(pfad),
                Foreground = Blass,
                FontFamily = new FontFamily("Arial"),
                FontSize = 12,
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center
            };
        }
        else
        {
            // Genau die Rechnung des Exports: Breite fest, Hoehe aus dem Bild.
            rahmen.Height = bild.PixelWidth > 0
                ? breite * bild.PixelHeight / bild.PixelWidth
                : breite;

            rahmen.BorderThickness = new Thickness(0);
            rahmen.Child = new Image { Source = bild, Stretch = Stretch.Fill };
        }

        merke(stelle.FieldKey, rahmen);
        return rahmen;
    }

    private static BitmapImage? Lade(string pfad)
    {
        try
        {
            var quelle = new BitmapImage();
            quelle.BeginInit();
            quelle.CacheOption = BitmapCacheOption.OnLoad;
            quelle.UriSource = new Uri(pfad, UriKind.Absolute);
            quelle.EndInit();
            quelle.Freeze();
            return quelle;
        }
        catch (Exception)
        {
            return null;
        }
    }

    private static FrameworkElement ZeichneTabelle(
        DossierPreviewTable tabelle,
        Func<string, string> value,
        Func<string, IReadOnlyList<IReadOnlyDictionary<string, string>>> rows,
        Func<string, string> emptyRowText,
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

        void SetzeWiederholung()
        {
            if (tabelle.RepeatKey is null || tabelle.RepeatTemplate is null)
                return;

            var daten = rows(tabelle.RepeatKey);

            if (daten.Count == 0)
            {
                // Derselbe Text, den auch der Export in die leere Zeile setzt.
                Setze(tabelle.RepeatTemplate,
                    i => i == 0 ? emptyRowText(tabelle.RepeatKey) : string.Empty,
                    tabelle.RepeatKey);
                return;
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

        // Die erzeugten Zeilen stehen dort, wo die Vorlage sie fuehrt — nicht
        // am Ende. Sonst rutschten Aktennotiz und Rueckmeldung darueber.
        var stelle = tabelle.RepeatIndex < 0 ? tabelle.Rows.Count : tabelle.RepeatIndex;

        for (var i = 0; i < tabelle.Rows.Count; i++)
        {
            if (i == stelle)
                SetzeWiederholung();

            Setze(tabelle.Rows[i], _ => null, null);
        }

        if (stelle >= tabelle.Rows.Count)
            SetzeWiederholung();

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

    /// <summary>
    /// Eine Linie am Ende des Satzspiegels samt Hinweis. Sie sagt ehrlich, dass
    /// Word ab dort umbricht — die Vorschau kann den Umbruch nicht nachrechnen.
    /// </summary>
    private static FrameworkElement UeberlaufMarke(DossierPreviewPage page)
        => new Border
        {
            BorderBrush = Fest(Color.FromRgb(0xC0, 0x50, 0x50)),
            BorderThickness = new Thickness(0, 1, 0, 0),
            VerticalAlignment = VerticalAlignment.Top,
            HorizontalAlignment = HorizontalAlignment.Stretch,
            Margin = new Thickness(
                0, page.Geometry.HeightPx - page.Geometry.Margin.Bottom, 0, 0),
            IsHitTestVisible = false,
            Child = new TextBlock
            {
                Text = "Ab hier bricht Word auf die Folgeseite um",
                FontFamily = new FontFamily("Arial"),
                FontSize = 10,
                Foreground = Fest(Color.FromRgb(0xC0, 0x50, 0x50)),
                HorizontalAlignment = HorizontalAlignment.Right,
                Margin = new Thickness(0, 2, 4, 0)
            }
        };

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
