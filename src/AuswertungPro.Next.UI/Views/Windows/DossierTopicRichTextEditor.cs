using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Media;

using AuswertungPro.Next.Application.Dossiers;
using AuswertungPro.Next.Domain.Models.Dossiers;

namespace AuswertungPro.Next.UI.Views.Windows;

/// <summary>
/// Uebersetzt zwischen dem kleinen Dossier-Format und einem Word-aehnlichen
/// WPF-Editor. Eingefuegt wird nur Klartext, damit fremde Formatierungen aus
/// E-Mails nicht unbemerkt das Dossier veraendern.
/// </summary>
internal static class DossierTopicRichTextEditor
{
    private static readonly FontFamily Arial = new("Arial");

    internal sealed record Value(string Text, IReadOnlyList<DossierTextStyleRange> StyleRanges);

    internal sealed record SelectionStyle(
        string? ColorHex,
        bool? Bold,
        bool? Italic,
        bool? Underline);

    public static RichTextBox Create(DossierTopicRow row)
    {
        ArgumentNullException.ThrowIfNull(row);

        var box = new RichTextBox
        {
            AcceptsReturn = true,
            MinHeight = 68,
            VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
            Background = Brushes.White,
            Foreground = Brushes.Black,
            FontFamily = Arial,
            Padding = new Thickness(6),
            Document = BuildDocument(row)
        };

        DataObject.AddPastingHandler(box, OnPaste);
        return box;
    }

    public static void SetValue(RichTextBox box, DossierTopicRow row)
    {
        ArgumentNullException.ThrowIfNull(box);
        ArgumentNullException.ThrowIfNull(row);
        box.Document = BuildDocument(row);
    }

    public static void ApplyColor(RichTextBox box, string colorHex)
    {
        ArgumentNullException.ThrowIfNull(box);
        var color = ParseColor(colorHex) ?? Colors.Black;
        box.Selection.ApplyPropertyValue(TextElement.ForegroundProperty, new SolidColorBrush(color));
        KeepArial(box);
    }

    public static void ToggleBold(RichTextBox box)
    {
        var active = Selection(box).Bold == true;
        box.Selection.ApplyPropertyValue(TextElement.FontWeightProperty,
            active ? FontWeights.Normal : FontWeights.Bold);
        KeepArial(box);
    }

    public static void ToggleItalic(RichTextBox box)
    {
        var active = Selection(box).Italic == true;
        box.Selection.ApplyPropertyValue(TextElement.FontStyleProperty,
            active ? FontStyles.Normal : FontStyles.Italic);
        KeepArial(box);
    }

    public static void ToggleUnderline(RichTextBox box)
    {
        var active = Selection(box).Underline == true;
        box.Selection.ApplyPropertyValue(Inline.TextDecorationsProperty,
            active ? null : TextDecorations.Underline);
        KeepArial(box);
    }

    public static SelectionStyle Selection(RichTextBox box)
    {
        ArgumentNullException.ThrowIfNull(box);

        return new SelectionStyle(
            ColorHex(box.Selection.GetPropertyValue(TextElement.ForegroundProperty)),
            IsEqual(box.Selection.GetPropertyValue(TextElement.FontWeightProperty), FontWeights.Bold),
            IsEqual(box.Selection.GetPropertyValue(TextElement.FontStyleProperty), FontStyles.Italic),
            HasUnderline(box.Selection.GetPropertyValue(Inline.TextDecorationsProperty)));
    }

    public static void InsertAtSelection(RichTextBox box, string text)
    {
        ArgumentNullException.ThrowIfNull(box);

        var range = new TextRange(box.Selection.Start, box.Selection.End);
        range.Text = text ?? string.Empty;
        box.CaretPosition = range.End;
        KeepArial(box);
    }

    public static Value Read(RichTextBox box)
    {
        ArgumentNullException.ThrowIfNull(box);

        var text = new System.Text.StringBuilder();
        var styles = new List<DossierTextStyleRange>();
        var firstBlock = true;

        foreach (var paragraph in box.Document.Blocks.OfType<Paragraph>())
        {
            if (!firstBlock)
                text.Append('\n');

            AppendInlines(paragraph.Inlines, text, styles);
            firstBlock = false;
        }

        return new Value(text.ToString(), DossierTopicTextFormatting.Normalize(text.ToString(), styles));
    }

    private static FlowDocument BuildDocument(DossierTopicRow row)
    {
        var document = new FlowDocument
        {
            PagePadding = new Thickness(0),
            FontFamily = Arial,
            Foreground = Brushes.Black
        };
        var paragraph = new Paragraph { Margin = new Thickness(0), FontFamily = Arial };
        document.Blocks.Add(paragraph);

        foreach (var segment in DossierTopicTextFormatting.Split(
            row.Text,
            DossierTopicTextFormatting.EffectiveRanges(row)))
        {
            AddSegment(paragraph, segment);
        }

        if (paragraph.Inlines.Count == 0)
            paragraph.Inlines.Add(new Run(string.Empty) { FontFamily = Arial, Foreground = Brushes.Black });

        return document;
    }

    private static void AddSegment(Paragraph paragraph, DossierTopicTextFormatting.Segment segment)
    {
        var lines = Normalize(segment.Text).Split('\n');
        for (var i = 0; i < lines.Length; i++)
        {
            if (i > 0)
                paragraph.Inlines.Add(new LineBreak());

            if (lines[i].Length == 0)
                continue;

            var run = new Run(lines[i])
            {
                FontFamily = Arial,
                Foreground = BrushFor(segment.ColorHex),
                FontWeight = segment.Bold ? FontWeights.Bold : FontWeights.Normal,
                FontStyle = segment.Italic ? FontStyles.Italic : FontStyles.Normal,
                TextDecorations = segment.Underline ? TextDecorations.Underline : null
            };
            paragraph.Inlines.Add(run);
        }
    }

    private static void AppendInlines(
        InlineCollection inlines,
        System.Text.StringBuilder text,
        ICollection<DossierTextStyleRange> styles)
    {
        foreach (var inline in inlines)
        {
            switch (inline)
            {
                case Run run:
                    var value = Normalize(run.Text);
                    var start = text.Length;
                    text.Append(value);
                    if (value.Length > 0)
                        styles.Add(StyleOf(run, start, value.Length));
                    break;
                case LineBreak:
                    text.Append('\n');
                    break;
                case Span span:
                    AppendInlines(span.Inlines, text, styles);
                    break;
            }
        }
    }

    private static DossierTextStyleRange StyleOf(Run run, int start, int length)
        => new()
        {
            Start = start,
            Length = length,
            ColorHex = ColorHex(run.Foreground) ?? "000000",
            Bold = run.FontWeight == FontWeights.Bold,
            Italic = run.FontStyle == FontStyles.Italic,
            Underline = run.TextDecorations?.Any(d => d.Location == TextDecorationLocation.Underline) == true
        };

    private static bool? IsEqual(object value, object expected)
        => ReferenceEquals(value, DependencyProperty.UnsetValue) ? null : Equals(value, expected);

    private static bool? HasUnderline(object value)
    {
        if (ReferenceEquals(value, DependencyProperty.UnsetValue))
            return null;

        return value is TextDecorationCollection decorations
            && decorations.Any(d => d.Location == TextDecorationLocation.Underline);
    }

    private static string? ColorHex(object? brush)
        => brush is SolidColorBrush solid
            ? $"{solid.Color.R:X2}{solid.Color.G:X2}{solid.Color.B:X2}"
            : null;

    private static Brush BrushFor(string? colorHex)
        => new SolidColorBrush(ParseColor(colorHex) ?? Colors.Black);

    private static Color? ParseColor(string? colorHex)
    {
        var value = colorHex?.Trim();
        if (!DossierTopicTextFormatting.IsColor(value))
            return null;

        return Color.FromRgb(
            byte.Parse(value![0..2], NumberStyles.HexNumber, CultureInfo.InvariantCulture),
            byte.Parse(value[2..4], NumberStyles.HexNumber, CultureInfo.InvariantCulture),
            byte.Parse(value[4..6], NumberStyles.HexNumber, CultureInfo.InvariantCulture));
    }

    private static void KeepArial(RichTextBox box)
    {
        box.Selection.ApplyPropertyValue(TextElement.FontFamilyProperty, Arial);
        box.Focus();
    }

    private static string Normalize(string? text)
        => (text ?? string.Empty).Replace("\r\n", "\n").Replace('\r', '\n');

    private static void OnPaste(object sender, DataObjectPastingEventArgs e)
    {
        if (sender is not RichTextBox box || !e.DataObject.GetDataPresent(DataFormats.UnicodeText))
            return;

        var text = e.DataObject.GetData(DataFormats.UnicodeText) as string ?? string.Empty;
        e.CancelCommand();
        InsertAtSelection(box, Normalize(text));
    }
}
