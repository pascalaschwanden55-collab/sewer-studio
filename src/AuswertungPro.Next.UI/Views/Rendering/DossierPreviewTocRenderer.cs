using System;
using System.Collections.Generic;
using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Media;

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

        var ownTitle = literal?.Invoke(entry.Title) ?? entry.Title;
        var title = Base(string.Empty);
        title.TextWrapping = TextWrapping.Wrap;
        var ranges = DossierTopicTextFormatting.Normalize(
            ownTitle, literalStyles?.Invoke(entry.Title));

        var segments = ranges.Count > 0
            ? DossierTopicTextFormatting.Split(ownTitle, ranges)
            : new[]
            {
                new DossierTopicTextFormatting.Segment(
                    ownTitle,
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
        grid.Children.Add(title);
        grid.Children.Add(page);

        var frame = new Border
        {
            Child = grid,
            Background = ownTitle.Trim().Length == 0 ? Empty : Brushes.Transparent,
            Margin = new Thickness(
                paragraph.Format.Indent.Left,
                paragraph.Format.SpaceBeforePx,
                paragraph.Format.Indent.Right,
                paragraph.Format.SpaceAfterPx)
        };

        remember(DossierPreviewTarget.Literal(entry.Title), frame);
        return frame;
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
