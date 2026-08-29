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
/// Die Themen der Tabelle „Informationen": Karten, Leiste, Einfuegemarken und
/// das Anlegen eines neuen Themas.
///
/// Herausgeloest aus der Feld-Datei, ohne eine Zeile zu aendern — sie war mit
/// ueber 950 Zeilen zu gross, um sie beim Suchen noch zu ueberblicken.
/// </summary>
internal sealed partial class DossierPreviewFieldPanel
{

    /// <summary>
    /// Die Themen der Informationstabelle — je gedruckter Zeile eine Karte.
    ///
    /// Gezeigt wird die AUFGELOESTE Liste (Gebietsvorgabe plus Abweichungen
    /// dieses Dossiers), nicht nur die Abweichungen: sonst stuenden im Blatt
    /// elf Zeilen und daneben ein einziges leeres Kaestchen.
    /// </summary>
    private UIElement BaueThemenEditor(DossierPreviewField feld)
    {
        var wirt = new StackPanel();
        FuelleThemenEditor(wirt, feld);
        return wirt;
    }

    private void FuelleThemenEditor(Panel wirt, DossierPreviewField feld)
    {
        _textUndo.Reset();
        wirt.Children.Clear();
        EntferneAlteZeilenStellen(feld.Key);

        // Auch ein Gebiet ohne Standardthemen zeigt im Word eine Tabellenzeile.
        // Die dazugehoerigen zwei Eingaben muessen ohne Umweg vorhanden sein.
        DossierTopicRows.EnsureStarter(_area, _dossier);
        var themen = DossierTopicResolver.Resolve(_area, _dossier);
        var leereEntwuerfe = new Queue<DossierTopicRow>(_dossier.Topics
            .Where(topic => topic is not null && string.IsNullOrWhiteSpace(topic.Title)));

        var ausGebiet = new HashSet<string>(
            (_area.Topics ?? new List<DossierTopicRow>())
                .Where(t => t is not null && !string.IsNullOrWhiteSpace(t.Title))
                .Select(t => t.Title.Trim()),
            StringComparer.OrdinalIgnoreCase);

        for (var i = 0; i < themen.Count; i++)
        {
            var sourceTitle = DossierTopicTitleEditing.SourceTitle(themen[i]);
            var draft = sourceTitle.Length == 0 && leereEntwuerfe.Count > 0
                ? leereEntwuerfe.Dequeue()
                : null;

            // Jede Zeile hat ihre eigene Marke, damit im Blatt genau sie
            // aufblinkt und nicht die ganze Tabelle.
            wirt.Children.Add(BaueThemenKarte(
                themen[i], draft, i, ausGebiet.Contains(sourceTitle), wirt, feld));
        }

        wirt.Children.Add(DossierNewTopicFieldBuilder.Build(_dossier, Kleiner, () =>
        {
            FuelleThemenEditor(wirt, feld);
            _zeichneBlatt();
            Betone(feld.Key);
        }));
    }

    private UIElement BaueThemenKarte(
        DossierTopicRow thema,
        DossierTopicRow? draft,
        int rowIndex,
        bool vomGebiet,
        Panel wirt,
        DossierPreviewField feld)
    {
        var sourceTitle = DossierTopicTitleEditing.SourceTitle(thema);
        var rowTarget = DossierPreviewTarget.Row(feld.Key, rowIndex);
        var titleTarget = DossierPreviewTarget.RowCell(feld.Key, rowIndex, "Thema");
        var textTarget = DossierPreviewTarget.RowCell(feld.Key, rowIndex, "Text");
        var inhalt = new StackPanel();
        var actionHost = new ContentControl();
        RichTextBox box = null!;

        string ActionTitle() => draft?.Title ?? sourceTitle;
        void RefreshActions()
        {
            if (box is not null)
            {
                actionHost.Content = BaueThemenLeiste(
                    ActionTitle(), textTarget, box, vomGebiet, wirt, feld);
            }
        }

        var karte = new Border
        {
            BorderBrush = Randfarbe,
            BorderThickness = new Thickness(1),
            CornerRadius = new CornerRadius(3),
            Padding = new Thickness(9),
            Margin = new Thickness(0, 0, 0, 9),
            Child = inhalt
        };

        var kopf = new DockPanel { Margin = new Thickness(0, 0, 0, 5) };

        if (!vomGebiet)
        {
            var entfernen = Kleiner("✕", "Diese Zeile entfernen", () =>
            {
                if (draft is not null)
                {
                    DossierTopicTitleEditing.Reset(_dossier, draft.Title);
                    _dossier.Topics.Remove(draft);
                }
                else
                {
                    DossierTopicEditing.RemoveDossierOverride(_dossier, sourceTitle);
                    DossierTopicTitleEditing.Reset(_dossier, sourceTitle);
                }

                FuelleThemenEditor(wirt, feld);
                _zeichneBlatt();
                Betone(feld.Key);
            });

            DockPanel.SetDock(entfernen, Dock.Right);
            kopf.Children.Add(entfernen);
        }

        kopf.Children.Add(new TextBlock
        {
            Text = vomGebiet
                ? $"Zeile {rowIndex + 1} · Gebietsvorgabe"
                : $"Zeile {rowIndex + 1} · nur dieses Dossier",
            TextWrapping = TextWrapping.Wrap,
            FontSize = 12,
            FontWeight = FontWeights.SemiBold,
            Foreground = (Brush)_ressource("TextBrush"),
            VerticalAlignment = VerticalAlignment.Center
        });

        inhalt.Children.Add(kopf);

        inhalt.Children.Add(new TextBlock
        {
            Text = "Thema",
            Margin = new Thickness(0, 2, 0, 2)
        });

        var titleBox = DossierTopicRichTextEditor.Create(new DossierTopicRow
        {
            Text = thema.Title,
            StyleRanges = DossierTopicTitleEditing.Styles(
                _dossier,
                draft?.Title ?? sourceTitle,
                thema.Title).ToList()
        });
        titleBox.AcceptsReturn = true;
        titleBox.MinHeight = 34;
        titleBox.GotKeyboardFocus += (_, _) => Betone(titleTarget);

        void SpeichereTitel()
        {
            var value = DossierTopicRichTextEditor.Read(titleBox);
            if (draft is null)
            {
                DossierTopicTitleEditing.Set(
                    _dossier, sourceTitle, value.Text, value.StyleRanges);
                return;
            }

            // Die leere Grundzeile hat noch keinen fachlichen Schluessel. Beim
            // ersten Schreiben wird sie zur normalen Dossierzeile; bei jedem
            // weiteren Zeichen wandert die Titel-Formatierung auf den neuen
            // Namen, ohne verwaiste Eintraege zu hinterlassen.
            DossierTopicTitleEditing.Reset(_dossier, draft.Title);
            draft.Title = value.Text;
            DossierTopicTitleEditing.Set(
                _dossier, draft.Title, draft.Title, value.StyleRanges);
        }

        Button titelZurueck = null!;
        void ZeigeTitelRueckweg()
            => titelZurueck.Visibility = draft is null
                && DossierTopicTitleEditing.IsOverridden(_dossier, sourceTitle)
                ? Visibility.Visible
                : Visibility.Collapsed;

        titleBox.TextChanged += (_, _) =>
        {
            if (_geladeneFormatfelder.Contains(titleBox))
                return;

            SpeichereTitel();
            ZeigeTitelRueckweg();
            if (draft is not null)
                RefreshActions();
            _zeichneBlatt();
        };

        var titleHost = new StackPanel();
        titleHost.Children.Add(titleBox);
        var titleTools = DossierTextFormattingToolbar.Create(titleBox, () =>
        {
            SpeichereTitel();
            ZeigeTitelRueckweg();
            if (draft is not null)
                RefreshActions();
            _zeichneBlatt();
            Betone(titleTarget);
        });
        ZeigeWerkzeugeNurAmAktivenFeld(titleHost, titleTools);
        titleHost.Children.Add(titleTools);

        titelZurueck = Kleiner("Ursprünglichen Titel verwenden",
            "Eigene Fassung und Formatierung dieses Thementitels verwerfen", () =>
            {
                DossierTopicTitleEditing.Reset(_dossier, sourceTitle);

                _geladeneFormatfelder.Add(titleBox);
                try
                {
                    DossierTopicRichTextEditor.SetValue(
                        titleBox, new DossierTopicRow { Text = sourceTitle });
                }
                finally
                {
                    _geladeneFormatfelder.Remove(titleBox);
                }

                ZeigeTitelRueckweg();
                _zeichneBlatt();
                Betone(titleTarget);
            });
        titelZurueck.HorizontalAlignment = HorizontalAlignment.Left;
        titelZurueck.Margin = new Thickness(0, 3, 0, 5);
        ZeigeTitelRueckweg();

        titleHost.Children.Add(titelZurueck);
        inhalt.Children.Add(titleHost);

        inhalt.Children.Add(new TextBlock
        {
            Text = "Bemerkungen",
            Margin = new Thickness(0, 5, 0, 2)
        });

        box = DossierTopicRichTextEditor.Create(thema);

        box.GotKeyboardFocus += (_, _) => Betone(textTarget);

        box.TextChanged += (_, _) =>
        {
            if (_geladeneFormatfelder.Contains(box))
                return;

            if (draft is null)
            {
                SpeichereFormatiertenText(sourceTitle, box);
            }
            else
            {
                var value = DossierTopicRichTextEditor.Read(box);
                draft.Text = value.Text;
                draft.ColorHex = string.Empty;
                draft.StyleRanges = value.StyleRanges.ToList();
            }

            _zeichneBlatt();
        };

        var textHost = new StackPanel();
        textHost.Children.Add(box);
        var textTools = DossierTextFormattingToolbar.Create(box, () =>
        {
            if (draft is null)
            {
                SpeichereFormatiertenText(sourceTitle, box);
            }
            else
            {
                var value = DossierTopicRichTextEditor.Read(box);
                draft.Text = value.Text;
                draft.ColorHex = string.Empty;
                draft.StyleRanges = value.StyleRanges.ToList();
            }

            _zeichneBlatt();
            Betone(textTarget);
        });
        ZeigeWerkzeugeNurAmAktivenFeld(textHost, textTools);
        textHost.Children.Add(textTools);
        inhalt.Children.Add(textHost);

        RefreshActions();
        inhalt.Children.Add(actionHost);

        MerkeStelle(rowTarget, karte);
        MerkeStelle(titleTarget, titleBox);
        MerkeStelle(textTarget, box);

        return karte;
    }

    /// <summary>
    /// Die fachlichen Aktionen einer Themenzeile. Die Formatleiste sitzt direkt
    /// beim jeweiligen Textfeld und erscheint nur, solange dort gearbeitet
    /// wird; Import und gebietsweite Uebernahme bleiben dagegen sichtbar.
    /// </summary>
    private UIElement BaueThemenLeiste(
        string titel,
        DossierPreviewTarget textTarget,
        RichTextBox box,
        bool vomGebiet,
        Panel wirt,
        DossierPreviewField feld)
    {
        var wurzel = new StackPanel();
        var aktionen = new WrapPanel();
        wurzel.Children.Add(aktionen);

        if (DossierTopicEditing.SupportsComponentListImport(titel))
        {
            var hinweis = new TextBlock
            {
                Text = "Kopiert die aktuelle Liste hierher: zuerst alle Haltungen, danach alle Schächte. Zustandsklassen werden in ihrer Farbe markiert. Die Kopie kann anschliessend frei bearbeitet werden.",
                TextWrapping = TextWrapping.Wrap,
                FontSize = 11,
                Margin = new Thickness(0, 3, 0, 5)
            };
            hinweis.SetResourceReference(TextBlock.ForegroundProperty, "MutedBrush");
            wurzel.Children.Add(hinweis);

            var import = Kleiner("Import aus Liste",
                "Ersetzt dieses Feld durch eine bearbeitbare Kopie der aktuellen Haltungen und Schächte. Z0 bis Z4 erhalten ihre Zustandsfarbe. Die Projektdaten bleiben unverändert.",
                () => ImportiereBauteilliste(titel, box, textTarget));

            import.IsEnabled = !string.IsNullOrWhiteSpace(
                DossierTopicComponentListComposer.ComponentText(_werte()));
            aktionen.Children.Add(import);
        }
        else if (DossierTopicEditing.SupportsHoldingInsert(titel))
        {
            aktionen.Children.Add(Kleiner("+ Leitungen",
                "Setzt die betroffenen Leitungen an der Schreibmarke ein",
                () => Einfuegen(box, textTarget, "{{Haltungen_Text}}")));

            aktionen.Children.Add(Kleiner("+ Schächte",
                "Setzt die zugehörigen Schächte an der Schreibmarke ein",
                () => Einfuegen(box, textTarget, "{{Schaechte_Text}}")));
        }

        if (!vomGebiet)
            return wurzel;

        var uebernehmen = Kleiner("Für alle übernehmen",
            "Gilt danach für jede Liegenschaft dieses Gebiets", () =>
            {
                var wert = DossierTopicRichTextEditor.Read(box);
                DossierTopicEditing.PromoteToArea(
                    _area, _dossier, titel, wert.Text, wert.StyleRanges, string.Empty);
                FuelleThemenEditor(wirt, feld);
                _zeichneBlatt();
                Betone(feld.Key);
            });

        void ZeigeUebernehmen()
            => uebernehmen.Visibility = DossierTopicEditing.HasDossierOverride(_dossier, titel)
                ? Visibility.Visible
                : Visibility.Collapsed;

        box.TextChanged += (_, _) => ZeigeUebernehmen();
        ZeigeUebernehmen();

        aktionen.Children.Add(uebernehmen);
        return wurzel;
    }

    /// <summary>
    /// Der ausdrueckliche Gesamtimport ersetzt den Feldinhalt durch eine
    /// bearbeitbare Momentaufnahme. Die getrennten Altknopfe fuer die
    /// Kostenschaetzung duerfen weiterhin dynamische Marken einsetzen.
    /// </summary>
    private void ImportiereBauteilliste(
        string titel,
        RichTextBox box,
        DossierPreviewTarget textTarget)
    {
        var imported = DossierTopicEditing.ImportFormattedComponentListForDossier(
            _dossier, titel, _werte());

        _geladeneFormatfelder.Add(box);
        try
        {
            DossierTopicRichTextEditor.SetValue(box, new DossierTopicRow
            {
                Text = imported.Text,
                StyleRanges = imported.StyleRanges.ToList()
            });
        }
        finally
        {
            _geladeneFormatfelder.Remove(box);
        }

        _zeichneBlatt();
        Betone(textTarget);
        box.Focus();
    }

    private void Einfuegen(
        RichTextBox box, DossierPreviewTarget textTarget, string text)
    {
        DossierTopicRichTextEditor.InsertAtSelection(box, text);

        _zeichneBlatt();
        Betone(textTarget);
    }

    private void SpeichereFormatiertenText(string titel, RichTextBox box)
    {
        var wert = DossierTopicRichTextEditor.Read(box);
        DossierTopicEditing.SetFormattedForDossier(
            _dossier, titel, wert.Text, wert.StyleRanges);
    }

}
