using System;
using System.Windows;
using System.Windows.Controls;

using AuswertungPro.Next.Application.Dossiers;
using AuswertungPro.Next.Domain.Models.Dossiers;

namespace AuswertungPro.Next.UI.Views.Windows;

/// <summary>Die kompakte Eingabe zum Anhaengen einer neuen Themenzeile.</summary>
internal static class DossierNewTopicFieldBuilder
{
    public static UIElement Build(
        DossierDefinition dossier,
        Func<string, string, Action, Button> smallButton,
        Action refresh)
    {
        var block = new StackPanel { Margin = new Thickness(0, 2, 0, 0) };
        block.Children.Add(new TextBlock
        {
            Text = "Zusätzliche Zeile nur für dieses Dossier",
            Margin = new Thickness(0, 0, 0, 3),
            TextWrapping = TextWrapping.Wrap
        });

        var input = new TextBox();
        var row = new DockPanel();
        var button = smallButton("+ Zeile", "Zeile mit diesem Titel anlegen", () =>
        {
            var title = input.Text?.Trim() ?? string.Empty;
            if (title.Length == 0)
                return;

            DossierTopicEditing.SetForDossier(dossier, title, string.Empty);
            refresh();
        });

        DockPanel.SetDock(button, Dock.Right);
        row.Children.Add(button);
        row.Children.Add(input);
        block.Children.Add(row);
        return block;
    }
}
