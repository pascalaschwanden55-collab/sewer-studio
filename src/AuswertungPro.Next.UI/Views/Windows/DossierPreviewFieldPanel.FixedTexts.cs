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
internal sealed partial class DossierPreviewFieldPanel
{

    /// <summary>
    /// Die festen Texte der Seite — Kapiteltitel, Spaltentitel, jede Zeile ohne
    /// Platzhalter. Damit ist wirklich jedes Element aenderbar und nicht nur
    /// die gefuellten Stellen.
    /// </summary>
    private UIElement BaueFesteTexte(
        IReadOnlyList<string> texte,
        IReadOnlyList<string> verzeichnisTitel)
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

                _zeichneBlatt();
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
                _zeichneBlatt();
            };

            ZeigeRueckweg();

            // Die Seitenzahl einer Inhaltsverzeichniszeile liegt ebenfalls in
            // der Karte, formatiert aber nicht den Titel. Darum umfasst der
            // Fokusbereich der Titel-Werkzeuge nur Editor und Werkzeuge.
            var titelBereich = new StackPanel();
            titelBereich.Children.Add(box);
            var werkzeuge = DossierTextFormattingToolbar.Create(box, () =>
            {
                Speichere();
                _zeichneBlatt();
                Betone(target);
            });
            ZeigeWerkzeugeNurAmAktivenFeld(titelBereich, werkzeuge);
            titelBereich.Children.Add(werkzeuge);
            karte.Children.Add(titelBereich);
            karte.Children.Add(zurueck);

            karte.Children.Add(DossierTocChapterPageField.CreateFor(
                _dossier, schluessel, verzeichnisTitel, Kleiner, _zeichneBlatt,
                MerkeStelle, Betone));

            block.Children.Add(karte);

            // Unter dem Wortlaut: genau so merkt sich das Blatt seine Absaetze.
            MerkeStelle(target, box);
        }

        return block;
    }



}
