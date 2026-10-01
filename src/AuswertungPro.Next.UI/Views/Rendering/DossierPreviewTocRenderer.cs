using System;
using System.Collections.Generic;
using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Media;
using System.Windows.Shapes;

using AuswertungPro.Next.Application.Dossiers;
using AuswertungPro.Next.Application.Dossiers.Preview;
using AuswertungPro.Next.Domain.Models.Dossiers;

namespace AuswertungPro.Next.UI.Views.Rendering;

/// <summary>
/// Zeichnet echte Word-Verzeichniszeilen. Die eigene kleine Klasse hält den
/// allgemeinen Seitenzeichner frei von der Sonderregel für PAGEREF-Felder.
/// </summary>
internal static class DossierPreviewTocRenderer
{
    private static readonly SolidColorBrush Ink = Frozen(Color.FromRgb(0, 0, 0));
    private static readonly SolidColorBrush Empty = Frozen(Color.FromRgb(0xF0, 0xF0, 0xF0));

    /// <summary>
    /// Nummer, bearbeitbarer Titel und Word-Seitenzahl stehen in drei getrennten
    /// Spalten. Dadurch klebt die Seitenzahl weder optisch noch als Schlüssel am
    /// Titel, und ein Klick führt genau zum zugehörigen Textfeld.
    /// </summary>
    public static Border Render(
        DossierPreviewParagraph paragraph,
        DossierPreviewTocEntry entry,
        DossierPreviewRunFormat format,
        Func<string, string?>? literal,
        Func<string, IReadOnlyList<DossierTextStyleRange>>? literalStyles,
        Action<DossierPreviewTarget, Border> remember)
    {
        var ownTitle = literal?.Invoke(entry.Title) ?? entry.Title;
        var ranges = DossierTopicTextFormatting.Normalize(
            ownTitle, literalStyles?.Invoke(entry.Title));

        return RenderRow(
            paragraph,
            entry,
            format,
            ownTitle,
            ranges,
            DossierPreviewTarget.Literal(entry.Title),
            remember);
    }

    /// <summary>
    /// Zeichnet zusätzliche Verzeichnispunkte als einzelne Zeilen mit genau
    /// demselben Raster wie die Word-Einträge darüber. Jede Zeile verweist
    /// direkt auf ihre eigene Karte im Listen-Editor rechts.
    /// </summary>
    public static Border RenderAttachments(
        DossierPreviewParagraph paragraph,
        string value,
        DossierPreviewRunFormat format,
        Action<DossierPreviewTarget, Border> remember)
    {
        var stack = new StackPanel();
        var entries = ParseAttachments(value);

        if (entries.Count == 0)
        {
            stack.Children.Add(RenderRow(
                paragraph,
                new DossierPreviewTocEntry(string.Empty, string.Empty, string.Empty),
                format,
                string.Empty,
                Array.Empty<DossierTextStyleRange>(),
                DossierPreviewTarget.Field("Verzeichnis_Beilagen"),
                remember));

            return new Border { Child = stack };
        }

        for (var index = 0; index < entries.Count; index++)
        {
            var row = RenderRow(
                paragraph,
                entries[index],
                format,
                entries[index].Title,
                Array.Empty<DossierTextStyleRange>(),
                DossierPreviewTarget.Row("Verzeichnis_Beilagen", index),
                remember);

            // Der allgemeine Feldschlüssel bleibt für die Hervorhebung des
            // ganzen Listenabschnitts erhalten; beim Klick gewinnt die
            // genauere Zeilenadresse.
            remember(DossierPreviewTarget.Field("Verzeichnis_Beilagen"), row);
            stack.Children.Add(row);
        }

        return new Border { Child = stack };
    }

    private static Border RenderRow(
        DossierPreviewParagraph paragraph,
        DossierPreviewTocEntry entry,
        DossierPreviewRunFormat format,
        string titleText,
        IReadOnlyList<DossierTextStyleRange> ranges,
        DossierPreviewTarget target,
        Action<DossierPreviewTarget, Border> remember)
    {
        TextBlock Base(string text)
        {
            var block = new TextBlock
            {
                Text = text,
                FontFamily = new FontFamily("Arial"),
                FontSize = format.FontSizePx,
                FontWeight = format.Bold ? FontWeights.Bold : FontWeights.Normal,
                FontStyle = format.Italic ? FontStyles.Italic : FontStyles.Normal,
                Foreground = Brush(format.ColorHex) ?? Ink,
                VerticalAlignment = VerticalAlignment.Top
            };

            if (paragraph.Format.LineHeightPx is { } lineHeight && lineHeight > 0)
            {
                block.LineHeight = lineHeight;
                block.LineStackingStrategy = LineStackingStrategy.BlockLineHeight;
            }

            return block;
        }

        var number = Base(entry.Number);
        number.Margin = new Thickness(0, 0, 7, 0);

        var page = Base(entry.PageNumber);
        page.Margin = new Thickness(7, 0, 0, 0);
        page.HorizontalAlignment = HorizontalAlignment.Right;

        var title = Base(string.Empty);
        title.TextWrapping = TextWrapping.Wrap;

        Line? leader = null;
        if (!string.IsNullOrWhiteSpace(entry.PageNumber))
        {
            leader = new Line
            {
                X1 = 0,
                Y1 = 0.5,
                X2 = 1,
                Y2 = 0.5,
                Stretch = Stretch.Fill,
                Height = 1,
                Stroke = Brush(format.ColorHex) ?? Ink,
                StrokeThickness = 1,
                StrokeDashArray = new DoubleCollection { 0, 2.5 },
                StrokeDashCap = PenLineCap.Round,
                VerticalAlignment = VerticalAlignment.Bottom,
                Margin = new Thickness(4, 0, 2, Math.Max(2, format.FontSizePx * 0.18))
            };

            // Die Linie liegt hinter dem Titel. Dessen eigene weisse Fläche
            // lässt die Punkte erst nach dem letzten Buchstaben beginnen.
            title.HorizontalAlignment = HorizontalAlignment.Left;
            title.Background = Brushes.White;
        }

        var segments = ranges.Count > 0
            ? DossierTopicTextFormatting.Split(titleText, ranges)
            : new[]
            {
                new DossierTopicTextFormatting.Segment(
                    titleText,
                    format.ColorHex,
                    format.Bold,
                    format.Italic,
                    format.Underline)
            };

        foreach (var segment in segments)
        {
            var run = new Run(segment.Text)
            {
                FontFamily = new FontFamily("Arial"),
                FontSize = format.FontSizePx,
                FontWeight = segment.Bold ? FontWeights.Bold : FontWeights.Normal,
                FontStyle = segment.Italic ? FontStyles.Italic : FontStyles.Normal,
                Foreground = Brush(segment.ColorHex) ?? Ink
            };

            if (segment.Underline)
                run.TextDecorations = TextDecorations.Underline;

            title.Inlines.Add(run);
        }

        var grid = new Grid();
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        grid.ColumnDefinitions.Add(new ColumnDefinition
        {
            Width = new GridLength(1, GridUnitType.Star)
        });
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        Grid.SetColumn(number, 0);
        Grid.SetColumn(title, 1);
        Grid.SetColumn(page, 2);
        grid.Children.Add(number);
        if (leader is not null)
        {
            Grid.SetColumn(leader, 1);
            grid.Children.Add(leader);
        }
        grid.Children.Add(title);
        grid.Children.Add(page);

        var frame = new Border
        {
            Child = grid,
            Background = titleText.Trim().Length == 0 ? Empty : Brushes.Transparent,
            Margin = new Thickness(
                paragraph.Format.Indent.Left,
                paragraph.Format.SpaceBeforePx,
                paragraph.Format.Indent.Right,
                paragraph.Format.SpaceAfterPx)
        };

        remember(target, frame);
        return frame;
    }

    private static List<DossierPreviewTocEntry> ParseAttachments(string value)
    {
        var result = new List<DossierPreviewTocEntry>();
        foreach (var rawLine in (value ?? string.Empty).Split(
                     new[] { '\r', '\n' },
                     StringSplitOptions.RemoveEmptyEntries))
        {
            var line = rawLine.Trim();
            if (line.Length == 0)
                continue;

            var separator = line.IndexOf('\t');
            if (separator < 0)
            {
                result.Add(new DossierPreviewTocEntry(string.Empty, line, string.Empty));
                continue;
            }

            var pageSeparator = line.IndexOf('\t', separator + 1);
            var title = pageSeparator < 0
                ? line[(separator + 1)..].Trim()
                : line[(separator + 1)..pageSeparator].Trim();
            var pageNumber = pageSeparator < 0
                ? string.Empty
                : line[(pageSeparator + 1)..].Trim();

            result.Add(new DossierPreviewTocEntry(
                line[..separator].Trim(),
                title,
                pageNumber));
        }

        return result;
    }

    private static SolidColorBrush Frozen(Color color)
    {
        var brush = new SolidColorBrush(color);
        brush.Freeze();
        return brush;
    }

    private static SolidColorBrush? Brush(string? hex)
    {
        if (string.IsNullOrWhiteSpace(hex))
            return null;

        var value = hex.Trim().TrimStart('#');
        if (value.Length != 6
            || !byte.TryParse(value[..2], NumberStyles.HexNumber, CultureInfo.InvariantCulture, out var r)
            || !byte.TryParse(value[2..4], NumberStyles.HexNumber, CultureInfo.InvariantCulture, out var g)
            || !byte.TryParse(value[4..6], NumberStyles.HexNumber, CultureInfo.InvariantCulture, out var b))
        {
            return null;
        }

        return Frozen(Color.FromRgb(r, g, b));
    }
}
