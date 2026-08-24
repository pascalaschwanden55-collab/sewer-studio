using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

using AuswertungPro.Next.Application.Dossiers;
using AuswertungPro.Next.Application.Dossiers.Preview;
using AuswertungPro.Next.Domain.Models.Dossiers;

namespace AuswertungPro.Next.UI.Views.Windows;

/// <summary>
/// Die festen Texte der Word-Vorlage: eigene Fassungen und Weglassen.
/// </summary>
public partial class DossierPreviewWindow
{

    /// <summary>
    /// Die festen Texte der Seite — Kapiteltitel, Spaltentitel, jede Zeile ohne
    /// Platzhalter. Damit ist wirklich jedes Element aenderbar und nicht nur
    /// die gefuellten Stellen.
    /// </summary>
    private UIElement BaueFesteTexte(IReadOnlyList<string> texte)
    {
        var block = new StackPanel();

        block.Children.Add(new TextBlock
        {
            Text = "Leeren heisst: die Zeile wird weggelassen.",
            FontStyle = FontStyles.Italic,
            TextWrapping = TextWrapping.Wrap,
            Margin = new Thickness(0, 0, 0, 4)
        });

        foreach (var urtext in texte)
        {
            var schluessel = urtext;
            var karte = new StackPanel { Margin = new Thickness(0, 10, 0, 0) };

            karte.Children.Add(new TextBlock
            {
                Text = schluessel,
                TextWrapping = TextWrapping.Wrap,
                Margin = new Thickness(0, 0, 0, 3)
            });

            var box = new TextBox
            {
                Text = _dossier.TextOverrides.TryGetValue(schluessel, out var eigen)
                    ? eigen
                    : schluessel,
                TextWrapping = TextWrapping.Wrap
            };

            var zurueck = Kleiner("Text der Vorlage", "Die eigene Fassung verwerfen", () =>
            {
                _dossier.TextOverrides.Remove(schluessel);
                box.Text = schluessel;
                ZeichneBlatt();
            });

            zurueck.Margin = new Thickness(0, 4, 0, 0);
            zurueck.HorizontalAlignment = HorizontalAlignment.Left;

            void ZeigeRueckweg()
                => zurueck.Visibility = _dossier.TextOverrides.ContainsKey(schluessel)
                    ? Visibility.Visible
                    : Visibility.Collapsed;

            box.TextChanged += (_, _) =>
            {
                if (string.Equals(box.Text, schluessel, StringComparison.Ordinal))
                    _dossier.TextOverrides.Remove(schluessel);
                else
                    _dossier.TextOverrides[schluessel] = box.Text;

                ZeigeRueckweg();
                ZeichneBlatt();
            };

            ZeigeRueckweg();

            karte.Children.Add(box);
            karte.Children.Add(zurueck);
            block.Children.Add(karte);
        }

        return block;
    }

    /// <summary>
    /// Die festen Texte einer Seite, ohne Wiederholungen. Zeilen MIT
    /// Platzhalter gehoeren ihrem Feld und stehen deshalb nicht hier.
    /// </summary>
    private static IReadOnlyList<string> FesteTexte(DossierPreviewPage seite)
    {
        var ergebnis = new List<string>();

        void Sammle(IEnumerable<DossierPreviewParagraph> absaetze)
        {
            foreach (var absatz in absaetze)
            {
                if (absatz.Runs.Any(r => r.IsField))
                    continue;

                var text = string.Concat(absatz.Runs.Select(r => r.Text)).Trim();

                if (text.Length > 0 && !ergebnis.Contains(text, StringComparer.Ordinal))
                    ergebnis.Add(text);
            }
        }

        foreach (var block in seite.Blocks)
        {
            switch (block)
            {
                case DossierPreviewParagraph absatz:
                    Sammle(new[] { absatz });
                    Sammle(absatz.Floating
                        .SelectMany(f => f.Blocks)
                        .OfType<DossierPreviewParagraph>());
                    break;

                case DossierPreviewTable tabelle:
                    Sammle(tabelle.Rows
                        .SelectMany(z => z.Cells)
                        .SelectMany(z => z.Paragraphs));
                    break;
            }
        }

        return ergebnis;
    }
}
