using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace AuswertungPro.Next.UI.Views.Windows;

/// <summary>Die einheitliche Word-aehnliche Leiste aller Dossier-Textfelder.</summary>
internal static class DossierTextFormattingToolbar
{
    private sealed record ColorChoice(string Name, string Hex);

    private static readonly IReadOnlyList<ColorChoice> Colors = new[]
    {
        new ColorChoice("Schwarz", "000000"),
        new ColorChoice("Rot", "C00000"),
        new ColorChoice("Dunkelrot", "8B0000"),
        new ColorChoice("Blau", "0070C0"),
        new ColorChoice("Dunkelblau", "1F4E79"),
        new ColorChoice("Grün", "008000"),
        new ColorChoice("Orange", "E26B0A"),
        new ColorChoice("Violett", "7030A0"),
        new ColorChoice("Grau", "666666")
    };

    public static UIElement Create(RichTextBox editor, Action changed)
    {
        ArgumentNullException.ThrowIfNull(editor);
        ArgumentNullException.ThrowIfNull(changed);

        var root = new StackPanel { Margin = new Thickness(0, 5, 0, 0) };
        root.Children.Add(new TextBlock
        {
            Text = "Arial · Text markieren und Format wählen. Ohne Markierung gilt es für neuen Text.",
            FontSize = 11,
            TextWrapping = TextWrapping.Wrap,
            Margin = new Thickness(0, 0, 0, 4)
        });

        var bar = new WrapPanel();
        root.Children.Add(bar);

        var bold = Button("Fett", "Markierten Text fett formatieren");
        var italic = Button("Kursiv", "Markierten Text kursiv formatieren");
        var underline = Button("Unterstrichen", "Markierten Text unterstreichen");
        var black = Button("Schwarz", "Markierten Text schwarz färben");
        var red = Button("Rot", "Markierten Text rot färben");
        red.Foreground = new SolidColorBrush(Color.FromRgb(0xC0, 0x00, 0x00));

        var catalog = new ComboBox
        {
            Width = 132,
            Height = 25,
            Margin = new Thickness(0, 0, 6, 5),
            ToolTip = "Weitere Schriftfarbe wählen"
        };
        foreach (var choice in Colors)
        {
            var color = ColorOf(choice.Hex);
            catalog.Items.Add(new ComboBoxItem
            {
                Content = choice.Name,
                Tag = choice.Hex,
                Background = new SolidColorBrush(color),
                Foreground = IsDark(color) ? Brushes.White : Brushes.Black,
                Padding = new Thickness(6, 2, 6, 2)
            });
        }
        var status = new TextBlock
        {
            VerticalAlignment = VerticalAlignment.Center,
            FontSize = 11,
            Margin = new Thickness(0, 0, 6, 5)
        };

        void Refresh()
        {
            var style = DossierTopicRichTextEditor.Selection(editor);
            bold.FontWeight = style.Bold == true ? FontWeights.Bold : FontWeights.Normal;
            italic.FontWeight = style.Italic == true ? FontWeights.Bold : FontWeights.Normal;
            underline.FontWeight = style.Underline == true ? FontWeights.Bold : FontWeights.Normal;

            var color = Colors.FirstOrDefault(c =>
                string.Equals(c.Hex, style.ColorHex, StringComparison.OrdinalIgnoreCase));
            status.Text = style.ColorHex is null
                ? "Farbe: gemischt"
                : "Farbe: " + (color?.Name ?? "Eigene");
            black.BorderThickness = color?.Hex == "000000" ? new Thickness(2) : new Thickness(1);
            red.BorderThickness = color?.Hex == "C00000" ? new Thickness(2) : new Thickness(1);
        }

        void Apply(Action action)
        {
            action();
            changed();
            Refresh();
        }

        bold.Click += (_, _) => Apply(() => DossierTopicRichTextEditor.ToggleBold(editor));
        italic.Click += (_, _) => Apply(() => DossierTopicRichTextEditor.ToggleItalic(editor));
        underline.Click += (_, _) => Apply(() => DossierTopicRichTextEditor.ToggleUnderline(editor));
        black.Click += (_, _) => Apply(() => DossierTopicRichTextEditor.ApplyColor(editor, "000000"));
        red.Click += (_, _) => Apply(() => DossierTopicRichTextEditor.ApplyColor(editor, "C00000"));
        catalog.SelectionChanged += (_, _) =>
        {
            if (catalog.SelectedItem is ComboBoxItem item && item.Tag is string hex)
                Apply(() => DossierTopicRichTextEditor.ApplyColor(editor, hex));
            catalog.SelectedIndex = -1;
        };
        editor.SelectionChanged += (_, _) => Refresh();

        bar.Children.Add(bold);
        bar.Children.Add(italic);
        bar.Children.Add(underline);
        bar.Children.Add(black);
        bar.Children.Add(red);
        bar.Children.Add(new TextBlock
        {
            Text = "Farbkatalog:",
            FontSize = 11,
            VerticalAlignment = VerticalAlignment.Center,
            Margin = new Thickness(0, 0, 5, 5)
        });
        bar.Children.Add(catalog);
        bar.Children.Add(status);
        Refresh();
        return root;
    }

    private static Color ColorOf(string hex)
        => Color.FromRgb(
            Convert.ToByte(hex[0..2], 16),
            Convert.ToByte(hex[2..4], 16),
            Convert.ToByte(hex[4..6], 16));

    private static bool IsDark(Color color)
        => (color.R * 299 + color.G * 587 + color.B * 114) / 1000 < 140;

    private static Button Button(string text, string tooltip)
        => new()
        {
            Content = text,
            MinWidth = 30,
            Height = 25,
            Padding = new Thickness(9, 0, 9, 0),
            Margin = new Thickness(0, 0, 6, 5),
            FontSize = 11,
            ToolTip = tooltip
        };
}
