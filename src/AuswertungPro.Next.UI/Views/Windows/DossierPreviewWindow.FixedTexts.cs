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
/// Die Beschriftungen und Ueberschriften der Word-Vorlage: eigene Fassungen
/// und Weglassen. In der Oberflaeche heissen sie bewusst nicht "fest", denn
/// der Benutzer kann jeden dieser Texte aendern.
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

            var text = _dossier.TextOverrides.TryGetValue(schluessel, out var eigen)
                ? eigen
                : schluessel;
            var styleKey = DossierTopicTextFormatting.LiteralStyleKey(schluessel);
            var target = DossierPreviewTarget.Literal(schluessel);
            var box = DossierTopicRichTextEditor.Create(new DossierTopicRow
            {
                Text = text,
                StyleRanges = Feldformat(styleKey, text)
            });
            box.AcceptsReturn = true;
            box.MinHeight = 40;

            box.GotKeyboardFocus += (_, _) => Betone(target);

            void Speichere()
            {
                var value = DossierTopicRichTextEditor.Read(box);
                if (string.Equals(value.Text, schluessel, StringComparison.Ordinal)
                    && value.StyleRanges.Count == 0)
                    _dossier.TextOverrides.Remove(schluessel);
                else
                    _dossier.TextOverrides[schluessel] = value.Text;

                _dossier.FieldStyles ??= new();
                if (value.StyleRanges.Count == 0)
                    _dossier.FieldStyles.Remove(styleKey);
                else
                    _dossier.FieldStyles[styleKey] = value.StyleRanges.ToList();
            }

            Button zurueck = null!;
            zurueck = Kleiner("Text der Vorlage", "Die eigene Fassung verwerfen", () =>
            {
                _dossier.TextOverrides.Remove(schluessel);
                _dossier.FieldStyles?.Remove(styleKey);

                _geladeneFormatfelder.Add(box);
                try
                {
                    DossierTopicRichTextEditor.SetValue(
                        box, new DossierTopicRow { Text = schluessel });
                }
                finally
                {
                    _geladeneFormatfelder.Remove(box);
                }

                ZeichneBlatt();
                Betone(target);
                ZeigeRueckweg();
            });

            zurueck.Margin = new Thickness(0, 4, 0, 0);
            zurueck.HorizontalAlignment = HorizontalAlignment.Left;

            void ZeigeRueckweg()
                => zurueck.Visibility = _dossier.TextOverrides.ContainsKey(schluessel)
                    || _dossier.FieldStyles?.ContainsKey(styleKey) == true
                    ? Visibility.Visible
                    : Visibility.Collapsed;

            box.TextChanged += (_, _) =>
            {
                if (_geladeneFormatfelder.Contains(box))
                    return;

                Speichere();
                ZeigeRueckweg();
                ZeichneBlatt();
            };

            ZeigeRueckweg();

            karte.Children.Add(box);
            karte.Children.Add(DossierTextFormattingToolbar.Create(box, () =>
            {
                Speichere();
                ZeichneBlatt();
                Betone(target);
            }));
            karte.Children.Add(zurueck);
            block.Children.Add(karte);

            // Unter dem Wortlaut: genau so merkt sich das Blatt seine Absaetze.
            MerkeStelle(target, box);
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

                // Die Zeilen des Inhaltsverzeichnisses gehoeren Word: es rechnet
                // sie aus Kapitelueberschrift und Seitenzahl. Als bearbeitbarer
                // Text angeboten, stand die Seitenzahl im Schluessel — und die
                // eigene Fassung war weg, sobald sich die Seiten verschoben.
                if (absatz.Format.IsTableOfContentsEntry)
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
