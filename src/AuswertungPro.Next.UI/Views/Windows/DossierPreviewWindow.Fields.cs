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
/// Die Eingabeseite der Vorschau.
///
/// Gegliedert wie das Blatt daneben: zuerst die Seite, darin ihre Abschnitte,
/// darin je Zeile eine eigene Karte. Wer wissen will, wo etwas hingehoert,
/// soll es an der Gliederung ablesen und nicht an der Reihenfolge raten.
///
/// Bewusst getrennt vom Zeichnen: das Blatt wird bei jeder Eingabe neu gemalt,
/// die Felder nur beim Seitenwechsel. Entstuenden auch sie neu, verloere das
/// Textfeld bei jedem Tastendruck den Fokus.
/// </summary>
public partial class DossierPreviewWindow
{
    private readonly HashSet<RichTextBox> _geladeneFormatfelder = new();

    private sealed record ZeilenSpalte(
        string Label,
        string StyleKey,
        Func<object, string> Read,
        Action<object, string> Write);

    private sealed record ZeilenTyp(
        IList Liste,
        Func<object> Neu,
        IReadOnlyList<ZeilenSpalte> Spalten);

    private static readonly SolidColorBrush Randfarbe =
        new(Color.FromRgb(0xC8, 0xC8, 0xC8));

    // ── Aufbau ────────────────────────────────────────────────────────────

    private void BaueFelder(DossierPreviewPage seite, IReadOnlyList<DossierPreviewField> felder)
    {
        FieldPanel.Children.Clear();

        var angaben = felder.Where(f => f.Kind is not DossierPreviewFieldKind.Rows).ToList();

        if (angaben.Count > 0)
        {
            var inhalt = new StackPanel();

            foreach (var feld in angaben)
                inhalt.Children.Add(BaueAngabe(feld));

            FieldPanel.Children.Add(Abschnitt("Angaben", inhalt, offen: true));
        }

        // Jede Zeilenliste bekommt ihren eigenen Abschnitt mit ihrem Namen.
        foreach (var feld in felder.Where(f => f.Kind is DossierPreviewFieldKind.Rows))
        {
            var inhalt = feld.Key == "Themen"
                ? BaueThemenEditor(feld)
                : BaueZeilenEditor(feld);

            FieldPanel.Children.Add(Abschnitt(feld.Label, inhalt, offen: true));
        }

        var feste = FesteTexte(seite);
        if (feste.Count > 0)
            FieldPanel.Children.Add(Abschnitt("Feste Texte", BaueFesteTexte(feste), offen: false));

        if (FieldPanel.Children.Count == 0)
        {
            FieldPanel.Children.Add(new TextBlock
            {
                Text = "Auf dieser Seite gibt es nichts auszufüllen.",
                TextWrapping = TextWrapping.Wrap
            });
        }
    }

    /// <summary>Ein aufklappbarer Abschnitt mit Trennlinie.</summary>
    private Expander Abschnitt(string titel, UIElement inhalt, bool offen)
        => new()
        {
            Header = titel,
            IsExpanded = offen,
            Foreground = (Brush)FindResource("TextBrush"),
            FontSize = 13,
            FontWeight = FontWeights.SemiBold,
            Margin = new Thickness(0, 0, 0, 14),
            Content = new Border
            {
                BorderBrush = Randfarbe,
                BorderThickness = new Thickness(0, 1, 0, 0),
                Padding = new Thickness(0, 10, 0, 2),
                Margin = new Thickness(0, 5, 0, 0),
                Child = inhalt
            }
        };

    private UIElement BaueAngabe(DossierPreviewField feld)
    {
        var block = new StackPanel { Margin = new Thickness(0, 0, 0, 14) };

        block.Children.Add(new TextBlock
        {
            Text = feld.Label,
            Margin = new Thickness(0, 0, 0, 3),
            TextWrapping = TextWrapping.Wrap
        });

        switch (feld.Kind)
        {
            case DossierPreviewFieldKind.File:
                block.Children.Add(BaueDateifeld(feld));
                break;

            case DossierPreviewFieldKind.Derived:
                block.Children.Add(new TextBlock
                {
                    Text = feld.Hint.Length > 0 ? feld.Hint : "Wird berechnet.",
                    FontStyle = FontStyles.Italic,
                    TextWrapping = TextWrapping.Wrap
                });
                break;

            default:
                var box = BaueTextfeld(feld);
                block.Children.Add(box);
                block.Children.Add(DossierTextFormattingToolbar.Create(box, () =>
                {
                    SpeichereFormatiertesFeld(feld, box);
                    ZeichneBlatt();
                    Betone(feld.Key);
                }));

                if (feld.CanReset)
                    block.Children.Add(BaueRueckweg(feld, box));

                break;
        }

        return block;
    }

    // ── Einzelne Angabe ───────────────────────────────────────────────────

    private RichTextBox BaueTextfeld(DossierPreviewField feld)
    {
        var mehrzeilig = feld.Kind == DossierPreviewFieldKind.MultiLine;
        var text = feld.Read();
        var row = new DossierTopicRow
        {
            Text = text,
            StyleRanges = Feldformat(feld.FormattingKey, text)
        };
        var box = DossierTopicRichTextEditor.Create(row);
        box.AcceptsReturn = mehrzeilig;
        box.MinHeight = mehrzeilig ? 68 : 34;
        box.MaxHeight = mehrzeilig ? double.PositiveInfinity : 34;
        box.VerticalScrollBarVisibility = mehrzeilig
            ? ScrollBarVisibility.Auto
            : ScrollBarVisibility.Disabled;

        box.GotKeyboardFocus += (_, _) => Betone(feld.Key);

        box.TextChanged += (_, _) =>
        {
            if (_geladeneFormatfelder.Contains(box))
                return;

            SpeichereFormatiertesFeld(feld, box);
            ZeichneBlatt();
        };

        return box;
    }

    /// <summary>
    /// Der Rueckweg zur berechneten Angabe. Ohne ihn waere jede von Hand
    /// gesetzte Stelle eine Einbahnstrasse: das Erstellungsdatum bliebe fuer
    /// immer stehen, auch wenn es laengst das falsche ist.
    /// </summary>
    private UIElement BaueRueckweg(DossierPreviewField feld, RichTextBox box)
    {
        var zeile = new DockPanel { Margin = new Thickness(0, 4, 0, 0) };

        var knopf = Kleiner("Zurücksetzen", "Wieder den berechneten Wert nehmen", () =>
        {
            feld.Reset?.Invoke();
            _dossier.FieldStyles?.Remove(feld.FormattingKey);

            _geladeneFormatfelder.Add(box);
            try
            {
                DossierTopicRichTextEditor.SetValue(box, new DossierTopicRow { Text = feld.Read() });
            }
            finally
            {
                _geladeneFormatfelder.Remove(box);
            }

            ZeichneBlatt();
            Betone(feld.Key);
        });

        DockPanel.SetDock(knopf, Dock.Right);

        zeile.Children.Add(knopf);
        zeile.Children.Add(new TextBlock
        {
            Text = "Von Hand gesetzt.",
            FontStyle = FontStyles.Italic,
            TextWrapping = TextWrapping.Wrap,
            VerticalAlignment = VerticalAlignment.Center
        });

        void Aktualisiere()
            => zeile.Visibility = feld.Overridden ? Visibility.Visible : Visibility.Collapsed;

        box.TextChanged += (_, _) => Aktualisiere();
        Aktualisiere();

        return zeile;
    }

    private List<DossierTextStyleRange> Feldformat(string key, string text)
    {
        _dossier.FieldStyles ??= new();
        return _dossier.FieldStyles.TryGetValue(key, out var ranges)
            ? DossierTopicTextFormatting.Normalize(text, ranges)
            : new List<DossierTextStyleRange>();
    }

    private void SpeichereFormatiertesFeld(DossierPreviewField feld, RichTextBox box)
    {
        var value = DossierTopicRichTextEditor.Read(box);
        feld.Write?.Invoke(value.Text);

        _dossier.FieldStyles ??= new();
        if (value.StyleRanges.Count == 0)
            _dossier.FieldStyles.Remove(feld.FormattingKey);
        else
            _dossier.FieldStyles[feld.FormattingKey] = value.StyleRanges.ToList();
    }

    private UIElement BaueDateifeld(DossierPreviewField feld)
    {
        var block = new StackPanel();

        var anzeige = new TextBlock
        {
            Text = feld.Read().Length == 0 ? "— keine Datei —" : feld.Read(),
            TextWrapping = TextWrapping.Wrap,
            Margin = new Thickness(0, 0, 0, 5)
        };

        block.Children.Add(anzeige);

        var leiste = new WrapPanel();

        leiste.Children.Add(Kleiner("Wählen…", "Plan als PDF oder Bild wählen",
            async () => await WaehlePlanAsync(feld, anzeige)));

        leiste.Children.Add(Kleiner("⟲", "90° nach links", () => Drehe(feld, anzeige, 270)));
        leiste.Children.Add(Kleiner("⟳", "90° nach rechts", () => Drehe(feld, anzeige, 90)));
        leiste.Children.Add(Kleiner("180°", "Auf den Kopf stellen", () => Drehe(feld, anzeige, 180)));

        leiste.Children.Add(Kleiner("Zuschneiden…",
            "Ausschnitt wählen, drehen und die Breite im Dossier festlegen",
            () => BearbeitePlan(feld, anzeige)));

        leiste.Children.Add(Kleiner("Entfernen", "Ohne Plan ausgeben", () =>
        {
            feld.Write?.Invoke(string.Empty);
            anzeige.Text = "— keine Datei —";
            ZeichneBlatt();
            Betone(feld.Key);
        }));

        block.Children.Add(leiste);
        return block;
    }

    private async System.Threading.Tasks.Task WaehlePlanAsync(
        DossierPreviewField feld, TextBlock anzeige)
    {
        var dialog = new Microsoft.Win32.OpenFileDialog
        {
            Title = "Übersichtsplan wählen",
            Filter = "Plan (PDF und Bilder)|*.pdf;*.png;*.jpg;*.jpeg;*.bmp"
                + "|PDF|*.pdf|Bilder|*.png;*.jpg;*.jpeg;*.bmp|Alle Dateien|*.*"
        };

        if (dialog.ShowDialog(this) != true)
            return;

        var pfad = dialog.FileName;

        // Word nimmt nur PNG und JPEG. Ein Plan kommt aber meist als PDF — er
        // wird deshalb sofort umgewandelt, damit die Vorschau schon zeigt, was
        // im Dossier stehen wird.
        if (_planImages.NeedsConversion(pfad))
        {
            StatusText.Text = "Plan wird in ein Bild umgewandelt…";
            var ergebnis = await _planImages.ConvertAsync(pfad, _request.TargetFolder);

            if (!ergebnis.Success)
            {
                StatusText.Text = ergebnis.Error ?? "Die Umwandlung ist fehlgeschlagen.";
                return;
            }

            pfad = ergebnis.ImagePath!;
            StatusText.Text = ergebnis.Error ?? "Plan übernommen.";
        }

        UebernimmPlan(feld, anzeige, pfad);
    }

    private void Drehe(DossierPreviewField feld, TextBlock anzeige, int grad)
    {
        var ergebnis = _planAdjuster.Rotate(feld.Read(), _request.TargetFolder, grad);

        if (!ergebnis.Success)
        {
            StatusText.Text = ergebnis.Error ?? "Der Plan konnte nicht gedreht werden.";
            return;
        }

        StatusText.Text = "Plan gedreht.";
        UebernimmPlan(feld, anzeige, ergebnis.ImagePath!);
    }

    /// <summary>
    /// Ausschnitt, Drehung und Breite im Dossier — alles am selben Bild, das
    /// die Vorschau daneben zeigt. Geschrieben wird nur in eine Kopie im
    /// Dossierordner; das Kundenoriginal bleibt unangetastet.
    /// </summary>
    private void BearbeitePlan(DossierPreviewField feld, TextBlock anzeige)
    {
        var ergebnis = DossierPlanWindow.ShowFor(
            _planAdjuster, feld.Read(), _request.TargetFolder, _dossier.OverviewPlanWidthCm);

        if (ergebnis is null)
            return;

        _dossier.OverviewPlanWidthCm = ergebnis.WidthCm;
        StatusText.Text = "Plan übernommen.";
        UebernimmPlan(feld, anzeige, ergebnis.ImagePath);
    }

    private void UebernimmPlan(DossierPreviewField feld, TextBlock anzeige, string pfad)
    {
        feld.Write?.Invoke(pfad);
        anzeige.Text = pfad.Length == 0 ? "— keine Datei —" : pfad;
        ZeichneBlatt();
        Betone(feld.Key);
    }

    // ── Themen ────────────────────────────────────────────────────────────

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
        wirt.Children.Clear();

        var themen = DossierTopicResolver.Resolve(_area, _dossier);

        var ausGebiet = new HashSet<string>(
            (_area.Topics ?? new List<DossierTopicRow>())
                .Where(t => t is not null && !string.IsNullOrWhiteSpace(t.Title))
                .Select(t => t.Title.Trim()),
            StringComparer.OrdinalIgnoreCase);

        for (var i = 0; i < themen.Count; i++)
        {
            // Jede Zeile hat ihre eigene Marke, damit im Blatt genau sie
            // aufblinkt und nicht die ganze Tabelle.
            wirt.Children.Add(BaueThemenKarte(
                themen[i], feld.Key + "#" + i, ausGebiet.Contains(themen[i].Title), wirt, feld));
        }

        wirt.Children.Add(BaueNeuesThema(wirt, feld));
    }

    private UIElement BaueThemenKarte(
        DossierTopicRow thema, string marke, bool vomGebiet, Panel wirt, DossierPreviewField feld)
    {
        var titel = thema.Title;
        var inhalt = new StackPanel();

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
                DossierTopicEditing.RemoveDossierOverride(_dossier, titel);
                FuelleThemenEditor(wirt, feld);
                ZeichneBlatt();
                Betone(feld.Key);
            });

            DockPanel.SetDock(entfernen, Dock.Right);
            kopf.Children.Add(entfernen);
        }

        kopf.Children.Add(new TextBlock
        {
            Text = vomGebiet ? titel : titel + "   (nur dieses Dossier)",
            TextWrapping = TextWrapping.Wrap,
            FontSize = 12,
            FontWeight = FontWeights.SemiBold,
            Foreground = (Brush)FindResource("TextBrush"),
            VerticalAlignment = VerticalAlignment.Center
        });

        inhalt.Children.Add(kopf);

        var box = DossierTopicRichTextEditor.Create(thema);

        box.GotKeyboardFocus += (_, _) => Betone(marke);

        box.TextChanged += (_, _) =>
        {
            SpeichereFormatiertenText(titel, box);
            ZeichneBlatt();
        };

        inhalt.Children.Add(box);
        inhalt.Children.Add(BaueThemenLeiste(titel, marke, box, vomGebiet, wirt, feld));

        return karte;
    }

    /// <summary>
    /// Die Werkzeuge einer Themenzeile in einer Reihe: Farbe, Einfuegen,
    /// Uebernehmen — statt drei uebereinandergestapelter Bloecke.
    /// </summary>
    private UIElement BaueThemenLeiste(
        string titel,
        string marke,
        RichTextBox box,
        bool vomGebiet,
        Panel wirt,
        DossierPreviewField feld)
    {
        var wurzel = new StackPanel();
        wurzel.Children.Add(DossierTextFormattingToolbar.Create(box, () =>
        {
            SpeichereFormatiertenText(titel, box);
            ZeichneBlatt();
            Betone(marke);
        }));

        var aktionen = new WrapPanel();
        wurzel.Children.Add(aktionen);

        if (DossierTopicEditing.IncludesComponentsAutomatically(titel))
        {
            var hinweis = new TextBlock
            {
                Text = "Die Liste wird automatisch nummeriert: zuerst alle Haltungen, danach alle Schächte.",
                TextWrapping = TextWrapping.Wrap,
                FontSize = 11,
                Margin = new Thickness(0, 3, 0, 5)
            };
            hinweis.SetResourceReference(TextBlock.ForegroundProperty, "MutedBrush");
            wurzel.Children.Add(hinweis);

            aktionen.Children.Add(Kleiner("Liste hier",
                "Setzt nur die Position der automatischen Liste; ohne Marke steht sie nach dem eigenen Text",
                () => Einfuegen(box, marke, DossierTopicComponentListComposer.Placeholder)));
        }
        else if (DossierTopicEditing.SupportsHoldingInsert(titel))
        {
            aktionen.Children.Add(Kleiner("+ Leitungen",
                "Setzt die betroffenen Leitungen an der Schreibmarke ein",
                () => Einfuegen(box, marke, "{{Haltungen_Text}}")));

            aktionen.Children.Add(Kleiner("+ Schächte",
                "Setzt die zugehörigen Schächte an der Schreibmarke ein",
                () => Einfuegen(box, marke, "{{Schaechte_Text}}")));
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
                ZeichneBlatt();
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
    /// Eingefuegt wird eine MARKE, nicht die fertige Liste: sonst veraltet der
    /// Text, sobald eine Leitung dazukommt. Im Blatt daneben steht sofort die
    /// aufgeloeste Liste.
    /// </summary>
    private void Einfuegen(RichTextBox box, string marke, string text)
    {
        DossierTopicRichTextEditor.InsertAtSelection(box, text);

        ZeichneBlatt();
        Betone(marke);
    }

    private void SpeichereFormatiertenText(string titel, RichTextBox box)
    {
        var wert = DossierTopicRichTextEditor.Read(box);
        DossierTopicEditing.SetFormattedForDossier(
            _dossier, titel, wert.Text, wert.StyleRanges);
    }

    private UIElement BaueNeuesThema(Panel wirt, DossierPreviewField feld)
    {
        var block = new StackPanel { Margin = new Thickness(0, 2, 0, 0) };

        block.Children.Add(new TextBlock
        {
            Text = "Zusätzliche Zeile nur für dieses Dossier",
            Margin = new Thickness(0, 0, 0, 3),
            TextWrapping = TextWrapping.Wrap
        });

        var eingabe = new TextBox();
        var zeile = new DockPanel();

        var knopf = Kleiner("+ Zeile", "Zeile mit diesem Titel anlegen", () =>
        {
            var titel = eingabe.Text?.Trim() ?? string.Empty;
            if (titel.Length == 0)
                return;

            DossierTopicEditing.SetForDossier(_dossier, titel, string.Empty);
            FuelleThemenEditor(wirt, feld);
            ZeichneBlatt();
            Betone(feld.Key);
        });

        DockPanel.SetDock(knopf, Dock.Right);
        zeile.Children.Add(knopf);
        zeile.Children.Add(eingabe);

        block.Children.Add(zeile);
        return block;
    }

    // ── Feste Texte ───────────────────────────────────────────────────────

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

    // ── Zeilenlisten (Eigentuemer, Aenderungswesen) ───────────────────────

    private UIElement BaueZeilenEditor(DossierPreviewField feld)
    {
        var wirt = new StackPanel();
        FuelleZeilenEditor(wirt, feld);
        return wirt;
    }

    private void FuelleZeilenEditor(Panel wirt, DossierPreviewField feld)
    {
        wirt.Children.Clear();

        var typ = ZeilenTypFuer(feld.Key);
        if (typ is null)
            return;

        for (var i = 0; i < typ.Liste.Count; i++)
        {
            var stelle = i;
            var zeile = typ.Liste[stelle]!;
            var marke = feld.Key + "#" + stelle;

            var inhalt = new StackPanel();

            var karte = new Border
            {
                BorderBrush = Randfarbe,
                BorderThickness = new Thickness(1),
                CornerRadius = new CornerRadius(3),
                Padding = new Thickness(9),
                Margin = new Thickness(0, 0, 0, 9),
                Child = inhalt
            };

            var kopf = new DockPanel { Margin = new Thickness(0, 0, 0, 3) };
            var werkzeuge = new StackPanel { Orientation = Orientation.Horizontal };
            DockPanel.SetDock(werkzeuge, Dock.Right);

            werkzeuge.Children.Add(Kleiner("▲", "Nach oben",
                () => Verschiebe(typ, stelle, -1, wirt, feld)));
            werkzeuge.Children.Add(Kleiner("▼", "Nach unten",
                () => Verschiebe(typ, stelle, +1, wirt, feld)));
            werkzeuge.Children.Add(Kleiner("✕", "Zeile entfernen", () =>
            {
                typ.Liste.RemoveAt(stelle);
                FuelleZeilenEditor(wirt, feld);
                ZeichneBlatt();
                Betone(feld.Key);
            }));

            kopf.Children.Add(werkzeuge);
            kopf.Children.Add(new TextBlock
            {
                Text = "Zeile " + (stelle + 1),
                FontSize = 12,
                FontWeight = FontWeights.SemiBold,
                Foreground = (Brush)FindResource("TextBrush"),
                VerticalAlignment = VerticalAlignment.Center
            });

            inhalt.Children.Add(kopf);

            foreach (var spalte in typ.Spalten)
            {
                inhalt.Children.Add(new TextBlock
                {
                    Text = spalte.Label,
                    Margin = new Thickness(0, 5, 0, 2),
                    TextWrapping = TextWrapping.Wrap
                });

                var mehrzeilig = spalte.Label is "Name" or "Art der Änderung";

                var text = spalte.Read(zeile);
                var box = DossierTopicRichTextEditor.Create(new DossierTopicRow
                {
                    Text = text,
                    StyleRanges = Zeilenformat(zeile, spalte.StyleKey, text)
                });
                box.AcceptsReturn = mehrzeilig;
                box.MinHeight = mehrzeilig ? 54 : 34;
                box.MaxHeight = mehrzeilig ? double.PositiveInfinity : 34;
                box.VerticalScrollBarVisibility = mehrzeilig
                    ? ScrollBarVisibility.Auto
                    : ScrollBarVisibility.Hidden;

                box.GotKeyboardFocus += (_, _) => Betone(marke);

                box.TextChanged += (_, _) =>
                {
                    SpeichereZeilenfeld(zeile, spalte, box);
                    ZeichneBlatt();
                };

                inhalt.Children.Add(box);
                inhalt.Children.Add(DossierTextFormattingToolbar.Create(box, () =>
                {
                    SpeichereZeilenfeld(zeile, spalte, box);
                    ZeichneBlatt();
                    Betone(marke);
                }));
            }

            wirt.Children.Add(karte);
        }

        var neu = Kleiner("+ Zeile", "Eine Zeile anhängen", () =>
        {
            typ.Liste.Add(typ.Neu());
            FuelleZeilenEditor(wirt, feld);
            ZeichneBlatt();
            Betone(feld.Key);
        });

        neu.HorizontalAlignment = HorizontalAlignment.Left;
        wirt.Children.Add(neu);
    }

    private static List<DossierTextStyleRange> Zeilenformat(
        object zeile, string key, string text)
    {
        var formate = Zeilenformate(zeile);
        return formate.TryGetValue(key, out var ranges)
            ? DossierTopicTextFormatting.Normalize(text, ranges)
            : new List<DossierTextStyleRange>();
    }

    private static void SpeichereZeilenfeld(
        object zeile, ZeilenSpalte spalte, RichTextBox box)
    {
        var value = DossierTopicRichTextEditor.Read(box);
        spalte.Write(zeile, value.Text);

        var formate = Zeilenformate(zeile);
        if (value.StyleRanges.Count == 0)
            formate.Remove(spalte.StyleKey);
        else
            formate[spalte.StyleKey] = value.StyleRanges.ToList();
    }

    private static Dictionary<string, List<DossierTextStyleRange>> Zeilenformate(object zeile)
        => zeile switch
        {
            DossierOwnerRow owner => owner.FieldStyles ??= new(),
            DossierChangeRow change => change.FieldStyles ??= new(),
            _ => throw new ArgumentException("Unbekannte Dossierzeile.", nameof(zeile))
        };

    private void Verschiebe(
        ZeilenTyp typ, int stelle, int richtung, Panel wirt, DossierPreviewField feld)
    {
        var ziel = stelle + richtung;
        if (ziel < 0 || ziel >= typ.Liste.Count)
            return;

        var zeile = typ.Liste[stelle];
        typ.Liste.RemoveAt(stelle);
        typ.Liste.Insert(ziel, zeile);

        FuelleZeilenEditor(wirt, feld);
        ZeichneBlatt();
        Betone(feld.Key);
    }

    // ── Werkzeuge ─────────────────────────────────────────────────────────

    /// <summary>Ein kleiner Knopf — alle Werkzeuge sehen gleich aus.</summary>
    private static Button Kleiner(string beschriftung, string hinweis, Action tue)
    {
        var knopf = new Button
        {
            Content = beschriftung,
            MinWidth = 30,
            Height = 25,
            Padding = new Thickness(9, 0, 9, 0),
            Margin = new Thickness(0, 0, 6, 5),
            FontSize = 11,
            ToolTip = hinweis
        };

        knopf.Click += (_, _) => tue();
        return knopf;
    }

    /// <summary>Merkt sich die bearbeitete Stelle und laesst sie aufblinken.</summary>
    private void Betone(string key)
    {
        _aktivesFeld = key;
        Hervorheben(key, blinken: true);
    }

    private ZeilenTyp? ZeilenTypFuer(string key) => key switch
    {
        "Eigentuemer" => new ZeilenTyp(
            _dossier.Owners,
            () => new DossierOwnerRow(),
            new[]
            {
                new ZeilenSpalte("Haus-Nr.", "HouseNumber",
                    z => ((DossierOwnerRow)z).HouseNumber,
                    (z, w) => ((DossierOwnerRow)z).HouseNumber = w),
                new ZeilenSpalte("Parzelle", "ParcelNumber",
                    z => ((DossierOwnerRow)z).ParcelNumber,
                    (z, w) => ((DossierOwnerRow)z).ParcelNumber = w),
                new ZeilenSpalte("Name", "Name",
                    z => ((DossierOwnerRow)z).Name,
                    (z, w) => ((DossierOwnerRow)z).Name = w),
                new ZeilenSpalte("Telefon", "Phone",
                    z => ((DossierOwnerRow)z).Phone,
                    (z, w) => ((DossierOwnerRow)z).Phone = w),
                new ZeilenSpalte("Mail", "Mail",
                    z => ((DossierOwnerRow)z).Mail,
                    (z, w) => ((DossierOwnerRow)z).Mail = w),
                new ZeilenSpalte("Objektbewohner", "Occupancy",
                    z => ((DossierOwnerRow)z).Occupancy,
                    (z, w) => ((DossierOwnerRow)z).Occupancy = w)
            }),

        "Aenderungen" => new ZeilenTyp(
            _dossier.Changes,
            () => new DossierChangeRow(),
            new[]
            {
                new ZeilenSpalte("Version", "Version",
                    z => ((DossierChangeRow)z).Version,
                    (z, w) => ((DossierChangeRow)z).Version = w),
                new ZeilenSpalte("Datum", "Date",
                    z => ((DossierChangeRow)z).Date,
                    (z, w) => ((DossierChangeRow)z).Date = w),
                new ZeilenSpalte("Visum", "Visum",
                    z => ((DossierChangeRow)z).Visum,
                    (z, w) => ((DossierChangeRow)z).Visum = w),
                new ZeilenSpalte("Art der Änderung", "Change",
                    z => ((DossierChangeRow)z).Change,
                    (z, w) => ((DossierChangeRow)z).Change = w)
            }),

        _ => null
    };
}
