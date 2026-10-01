using System;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

using AuswertungPro.Next.Application.Dossiers;
using AuswertungPro.Next.Application.Dossiers.Preview;
using AuswertungPro.Next.Domain.Models.Dossiers;

namespace AuswertungPro.Next.UI.Views.Windows;

/// <summary>
/// Baut die drei bearbeitbaren Beschriftungen der Eigentuemerzelle. Sie gelten
/// fuer jede Zeile gemeinsam und bleiben deshalb ausserhalb der Zeilenkarten.
/// </summary>
internal static class DossierOwnerLabelFieldBuilder
{
    public static UIElement Build(
        DossierDefinition dossier,
        int rowCount,
        Brush borderBrush,
        Brush textBrush,
        Action redraw,
        Action<DossierPreviewTarget> emphasize,
        Action<DossierPreviewTarget, UIElement> remember)
    {
        var content = new StackPanel();
        var card = new Border
        {
            BorderBrush = borderBrush,
            BorderThickness = new Thickness(1),
            CornerRadius = new CornerRadius(3),
            Padding = new Thickness(9),
            Margin = new Thickness(0, 0, 0, 9),
            Child = content
        };

        content.Children.Add(new TextBlock
        {
            Text = "Beschriftungen in jeder Eigentümerzeile",
            FontSize = 12,
            FontWeight = FontWeights.SemiBold,
            Foreground = textBrush,
            Margin = new Thickness(0, 0, 0, 3)
        });

        foreach (var label in DossierOwnerCellLabels.All)
        {
            content.Children.Add(new TextBlock
            {
                Text = label.EditorLabel,
                Margin = new Thickness(0, 5, 0, 2),
                TextWrapping = TextWrapping.Wrap
            });

            var box = DossierTopicRichTextEditor.Create(new DossierTopicRow
            {
                Text = DossierOwnerCellLabels.Text(dossier, label),
                StyleRanges = DossierOwnerCellLabels.Styles(dossier, label).ToList()
            });
            box.AcceptsReturn = false;
            box.MinHeight = 34;
            box.MaxHeight = 34;
            box.VerticalScrollBarVisibility = ScrollBarVisibility.Hidden;

            var sharedTarget = DossierPreviewTarget.Field("Eigentuemer");
            box.GotKeyboardFocus += (_, _) => emphasize(sharedTarget);

            void Save()
            {
                var value = DossierTopicRichTextEditor.Read(box);
                DossierOwnerCellLabels.SetFormatted(
                    dossier, label, value.Text, value.StyleRanges);
            }

            box.TextChanged += (_, _) =>
            {
                Save();
                redraw();
            };

            var host = new StackPanel();
            host.Children.Add(box);
            var tools = DossierTextFormattingToolbar.Create(box, () =>
            {
                Save();
                redraw();
                emphasize(sharedTarget);
            });
            DossierFieldHighlight.ZeigeWerkzeugeNurAmAktivenFeld(host, tools);
            host.Children.Add(tools);
            content.Children.Add(host);

            for (var row = 0; row < rowCount; row++)
            {
                remember(DossierPreviewTarget.RowCell(
                    "Eigentuemer", row, label.CellKey), box);
            }
        }

        return card;
    }
}
