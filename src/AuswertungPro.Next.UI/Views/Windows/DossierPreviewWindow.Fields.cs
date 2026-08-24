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
/// Die Eingabeseite der Vorschau: je Feld ein Bedienelement, fuer Zeilenlisten
/// ein kleiner Editor mit Reihenfolge.
///
/// Bewusst getrennt vom Zeichnen: das Blatt wird bei jeder Eingabe neu gemalt,
/// die Felder nur beim Seitenwechsel. Wuerden auch sie neu entstehen, verloere
/// das Textfeld bei jedem Tastendruck den Fokus.
/// </summary>
public partial class DossierPreviewWindow
{
    private sealed record ZeilenSpalte(
        string Label,
        Func<object, string> Read,
        Action<object, string> Write);

    private sealed record ZeilenTyp(
        IList Liste,
        Func<object> Neu,
        IReadOnlyList<ZeilenSpalte> Spalten);

    private static readonly SolidColorBrush Randfarbe =
        new(Color.FromRgb(0x80, 0x80, 0x80));

    private void BaueFelder(IReadOnlyList<DossierPreviewField> felder)
    {
        FieldPanel.Children.Clear();

        if (felder.Count == 0)
        {
            FieldPanel.Children.Add(new TextBlock
            {
                Text = "Auf dieser Seite gibt es nichts auszufüllen.",
                TextWrapping = TextWrapping.Wrap,
                Margin = new Thickness(0, 6, 0, 0)
            });

            return;
        }

        foreach (var feld in felder)
        {
            FieldPanel.Children.Add(new TextBlock
            {
                Text = feld.Label,
                Margin = new Thickness(0, 12, 0, 3),
                TextWrapping = TextWrapping.Wrap
            });

            switch (feld.Kind)
            {
                case DossierPreviewFieldKind.Text:
                case DossierPreviewFieldKind.MultiLine:
                    FieldPanel.Children.Add(BaueTextfeld(feld));

                    if (feld.CanReset)
                        FieldPanel.Children.Add(BaueRueckweg(feld));

                    break;

                case DossierPreviewFieldKind.File:
                    FieldPanel.Children.Add(BaueDateifeld(feld));
                    break;

                case DossierPreviewFieldKind.Rows when feld.Key == "Themen":
                    FieldPanel.Children.Add(BaueThemenEditor(feld));
                    break;

                case DossierPreviewFieldKind.Rows:
                    FieldPanel.Children.Add(BaueZeilenEditor(feld));
                    break;

                default:
                    FieldPanel.Children.Add(new TextBlock
                    {
                        Text = feld.Hint.Length > 0 ? feld.Hint : "Wird berechnet.",
                        FontStyle = FontStyles.Italic,
                        TextWrapping = TextWrapping.Wrap
                    });
                    break;
            }
        }
    }

    /// <summary>
    /// Die festen Texte der Seite — Kapitelueberschriften, Spaltentitel, jede
    /// Zeile ohne Platzhalter.
    ///
    /// Damit ist wirklich jedes Element der Seite aenderbar und nicht nur die
    /// gefuellten Stellen. Wer eine Zeile leert, laesst sie weg; ein
    /// Zuruecksetzen bringt den Text der Vorlage zurueck.
    /// </summary>
    private void BaueFesteTexte(DossierPreviewPage seite)
    {
        var texte = FesteTexte(seite);
        if (texte.Count == 0)
            return;

        var ausklapper = new Expander
        {
            Header = "Feste Texte dieser Seite",
            Foreground = (System.Windows.Media.Brush)FindResource("TextBrush"),
            Margin = new Thickness(0, 18, 0, 0)
        };

        var inhalt = new StackPanel { Margin = new Thickness(0, 8, 0, 0) };
        ausklapper.Content = inhalt;

        inhalt.Children.Add(new TextBlock
        {
            Text = "Leeren heisst: die Zeile wird weggelassen.",
            TextWrapping = TextWrapping.Wrap,
            Margin = new Thickness(0, 0, 0, 6)
        });

        foreach (var urtext in texte)
        {
            var schluessel = urtext;

            inhalt.Children.Add(new TextBlock
            {
                Text = schluessel,
                TextWrapping = TextWrapping.Wrap,
                Margin = new Thickness(0, 10, 0, 2)
            });

            var box = new TextBox
            {
                Text = _dossier.TextOverrides.TryGetValue(schluessel, out var eigen)
                    ? eigen
                    : schluessel,
                TextWrapping = TextWrapping.Wrap,
                AcceptsReturn = false
            };

            box.GotKeyboardFocus += (_, _) => Hervorheben(schluessel, blinken: false);

            box.TextChanged += (_, _) =>
            {
                if (string.Equals(box.Text, schluessel, StringComparison.Ordinal))
                    _dossier.TextOverrides.Remove(schluessel);
                else
                    _dossier.TextOverrides[schluessel] = box.Text;

                ZeichneBlatt();
            };

            var zurueck = new Button
            {
                Content = "Text der Vorlage",
                Padding = new Thickness(8, 2, 8, 2),
                FontSize = 11,
                HorizontalAlignment = HorizontalAlignment.Left,
                Margin = new Thickness(0, 4, 0, 0)
            };

            zurueck.Click += (_, _) =>
            {
                _dossier.TextOverrides.Remove(schluessel);
                box.Text = schluessel;
                ZeichneBlatt();
            };

            inhalt.Children.Add(box);
            inhalt.Children.Add(zurueck);
        }

        FieldPanel.Children.Add(ausklapper);
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

    private TextBox BaueTextfeld(DossierPreviewField feld)
    {
        var box = new TextBox
        {
            Text = feld.Read(),
            AcceptsReturn = feld.Kind == DossierPreviewFieldKind.MultiLine,
            TextWrapping = feld.Kind == DossierPreviewFieldKind.MultiLine
                ? TextWrapping.Wrap
                : TextWrapping.NoWrap,
            MinHeight = feld.Kind == DossierPreviewFieldKind.MultiLine ? 64 : 0,
            VerticalScrollBarVisibility = feld.Kind == DossierPreviewFieldKind.MultiLine
                ? ScrollBarVisibility.Auto
                : ScrollBarVisibility.Disabled
        };

        box.GotKeyboardFocus += (_, _) =>
        {
            _aktivesFeld = feld.Key;
            Hervorheben(feld.Key, blinken: true);
        };

        box.TextChanged += (_, _) =>
        {
            feld.Write?.Invoke(box.Text);
            ZeichneBlatt();
            Hervorheben(feld.Key, blinken: false);

            // Der Rueckweg steht unmittelbar unter dem Feld.
            if (feld.CanReset
                && FieldPanel.Children.IndexOf(box) is var stelle and >= 0
                && stelle + 1 < FieldPanel.Children.Count
                && FieldPanel.Children[stelle + 1] is DockPanel rueckweg)
            {
                rueckweg.Visibility = feld.Overridden
                    ? Visibility.Visible
                    : Visibility.Collapsed;
            }
        };

        return box;
    }

    /// <summary>
    /// Der Rueckweg zur berechneten Angabe.
    ///
    /// Ohne ihn waere jede von Hand gesetzte Stelle eine Einbahnstrasse: das
    /// Erstellungsdatum bliebe fuer immer stehen, auch wenn es laengst das
    /// falsche ist.
    /// </summary>
    private UIElement BaueRueckweg(DossierPreviewField feld)
    {
        var zeile = new DockPanel { Margin = new Thickness(0, 4, 0, 0) };

        var knopf = new Button
        {
            Content = "Zurücksetzen",
            Padding = new Thickness(8, 2, 8, 2),
            FontSize = 11
        };

        DockPanel.SetDock(knopf, Dock.Right);

        var hinweis = new TextBlock
        {
            Text = "Von Hand gesetzt.",
            TextWrapping = TextWrapping.Wrap,
            FontSize = 11,
            VerticalAlignment = VerticalAlignment.Center
        };

        knopf.Click += (_, _) =>
        {
            feld.Reset?.Invoke();
            _aktivesFeld = feld.Key;
            ZeichneBlatt();

            // Die Felder danach neu aufbauen: das Textfeld traegt jetzt wieder
            // den berechneten Wert, und der Rueckweg wird ueberfluessig.
            if (PageList.SelectedItem is DossierPreviewPage seite)
            {
                BaueFelder(DossierPreviewFieldCatalog.ForPage(
                    _fields,
                    seite,
                    _dossier,
                    key => _values.TryGetValue(key, out var wert) ? wert : string.Empty));
            }

            Hervorheben(feld.Key, blinken: true);
        };

        zeile.Children.Add(knopf);
        zeile.Children.Add(hinweis);

        zeile.Visibility = feld.Overridden ? Visibility.Visible : Visibility.Collapsed;
        return zeile;
    }

    private UIElement BaueDateifeld(DossierPreviewField feld)
    {
        var panel = new DockPanel();

        var anzeige = new TextBlock
        {
            Text = feld.Read().Length == 0 ? "— keine Datei —" : feld.Read(),
            TextWrapping = TextWrapping.Wrap,
            VerticalAlignment = VerticalAlignment.Center
        };

        var knopf = new Button { Content = "Wählen…", Padding = new Thickness(10, 4, 10, 4) };
        DockPanel.SetDock(knopf, Dock.Right);

        knopf.Click += async (_, _) =>
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

            // Word nimmt nur PNG und JPEG. Ein Plan kommt aber meist als PDF —
            // er wird deshalb sofort umgewandelt, damit die Vorschau schon
            // zeigt, was im Dossier stehen wird.
            if (_planImages.NeedsConversion(pfad))
            {
                knopf.IsEnabled = false;
                StatusText.Text = "Plan wird in ein Bild umgewandelt…";

                try
                {
                    var ergebnis = await _planImages.ConvertAsync(pfad, _request.TargetFolder);

                    if (!ergebnis.Success)
                    {
                        StatusText.Text = ergebnis.Error ?? "Die Umwandlung ist fehlgeschlagen.";
                        return;
                    }

                    pfad = ergebnis.ImagePath!;
                    StatusText.Text = ergebnis.Error ?? "Plan übernommen.";
                }
                finally
                {
                    knopf.IsEnabled = true;
                }
            }

            feld.Write?.Invoke(pfad);
            anzeige.Text = pfad;
            _aktivesFeld = feld.Key;
            ZeichneBlatt();
            Hervorheben(feld.Key, blinken: true);
        };

        panel.Children.Add(knopf);
        panel.Children.Add(anzeige);

        var ganzes = new StackPanel();
        ganzes.Children.Add(panel);
        ganzes.Children.Add(BaueDrehleiste(feld, anzeige));
        return ganzes;
    }

    /// <summary>
    /// Die Drehknoepfe. Gedreht wird die Datei selbst — dann stimmt der Plan
    /// in der Vorschau, im Word und in jedem PDF daraus. Eine nur gespeicherte
    /// Gradzahl muesste jede dieser Stellen erneut auswerten, und eine davon
    /// vergisst es.
    /// </summary>
    private UIElement BaueDrehleiste(DossierPreviewField feld, TextBlock anzeige)
    {
        var leiste = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            Margin = new Thickness(0, 6, 0, 0)
        };

        void Drehe(int grad)
        {
            var ergebnis = _planAdjuster.Rotate(feld.Read(), _request.TargetFolder, grad);

            if (!ergebnis.Success)
            {
                StatusText.Text = ergebnis.Error ?? "Der Plan konnte nicht gedreht werden.";
                return;
            }

            feld.Write?.Invoke(ergebnis.ImagePath!);
            anzeige.Text = ergebnis.ImagePath!;
            _aktivesFeld = feld.Key;
            StatusText.Text = "Plan gedreht.";
            ZeichneBlatt();
            Hervorheben(feld.Key, blinken: false);
        }

        leiste.Children.Add(Werkzeug("⟲", "90° nach links drehen", () => Drehe(270)));
        leiste.Children.Add(Werkzeug("⟳", "90° nach rechts drehen", () => Drehe(90)));
        leiste.Children.Add(Werkzeug("180°", "Auf den Kopf stellen", () => Drehe(180)));

        var bearbeiten = new Button
        {
            Content = "Zuschneiden…",
            Padding = new Thickness(8, 2, 8, 2),
            FontSize = 11,
            Margin = new Thickness(10, 0, 0, 0),
            ToolTip = "Ausschnitt wählen, drehen und die Breite im Dossier festlegen"
        };

        bearbeiten.Click += (_, _) =>
        {
            var ergebnis = DossierPlanWindow.ShowFor(
                _planAdjuster, feld.Read(), _request.TargetFolder, _dossier.OverviewPlanWidthCm);

            if (ergebnis is null)
                return;

            feld.Write?.Invoke(ergebnis.ImagePath);
            _dossier.OverviewPlanWidthCm = ergebnis.WidthCm;
            anzeige.Text = ergebnis.ImagePath;
            _aktivesFeld = feld.Key;
            StatusText.Text = "Plan übernommen.";
            ZeichneBlatt();
            Hervorheben(feld.Key, blinken: true);
        };

        leiste.Children.Add(bearbeiten);
        return leiste;
    }

    /// <summary>
    /// Die Themen der Informationstabelle — ein eigenes Eingabefeld je Zeile,
    /// die im Dossier wirklich gedruckt wird.
    ///
    /// Gezeigt wird die AUFGELOESTE Liste (Gebietsvorgabe plus Abweichungen
    /// dieses Dossiers), nicht nur die Abweichungen: sonst stuenden im Blatt
    /// elf Zeilen und daneben ein einziges leeres Kaestchen.
    ///
    /// Was hier getippt wird, gehoert diesem Dossier. Der Titel einer
    /// Gebietszeile bleibt fest — er wird in den Gebietsangaben geaendert;
    /// ihn hier umzubenennen wuerde die Zeile vom Gebiet loesen und beide
    /// nebeneinander drucken.
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

        foreach (var thema in themen)
        {
            var titel = thema.Title;
            var vomGebiet = ausGebiet.Contains(titel);

            var karte = new Border
            {
                BorderBrush = Randfarbe,
                BorderThickness = new Thickness(1),
                CornerRadius = new CornerRadius(3),
                Padding = new Thickness(8),
                Margin = new Thickness(0, 0, 0, 6)
            };

            var inhalt = new StackPanel();
            karte.Child = inhalt;

            var kopf = new DockPanel();

            if (!vomGebiet)
            {
                var entfernen = Werkzeug("✕", "Diese Zeile entfernen", () =>
                {
                    DossierTopicEditing.RemoveDossierOverride(_dossier, titel);
                    FuelleThemenEditor(wirt, feld);
                    ZeichneBlatt();
                    Hervorheben(feld.Key, blinken: true);
                });

                DockPanel.SetDock(entfernen, Dock.Right);
                kopf.Children.Add(entfernen);
            }

            kopf.Children.Add(new TextBlock
            {
                Text = titel + (vomGebiet ? string.Empty : "   (nur dieses Dossier)"),
                TextWrapping = TextWrapping.Wrap,
                FontSize = 12,
                VerticalAlignment = VerticalAlignment.Center
            });

            inhalt.Children.Add(kopf);

            var box = new TextBox
            {
                Text = thema.Text,
                AcceptsReturn = true,
                TextWrapping = TextWrapping.Wrap,
                MinHeight = 52,
                VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
                VerticalContentAlignment = VerticalAlignment.Top,
                Margin = new Thickness(0, 4, 0, 0)
            };

            box.GotKeyboardFocus += (_, _) =>
            {
                _aktivesFeld = feld.Key;
                Hervorheben(feld.Key, blinken: true);
            };

            box.TextChanged += (_, _) =>
            {
                DossierTopicEditing.SetForDossier(_dossier, titel, box.Text);
                ZeichneBlatt();
                Hervorheben(feld.Key, blinken: false);
            };

            inhalt.Children.Add(box);
            inhalt.Children.Add(BaueFarbleiste(titel, box, feld));

            if (DossierTopicEditing.SupportsHoldingInsert(titel))
                inhalt.Children.Add(BaueEinfuegeleiste(box, feld));

            if (vomGebiet)
                inhalt.Children.Add(BaueGebietsHinweis(titel, box, wirt, feld));

            wirt.Children.Add(karte);
        }

        var neuesThema = new TextBox
        {
            Margin = new Thickness(0, 4, 0, 4),
            ToolTip = "Titel einer zusätzlichen Zeile nur für dieses Dossier"
        };

        var hinzufuegen = new Button
        {
            Content = "+ Eigene Zeile",
            Padding = new Thickness(10, 4, 10, 4),
            HorizontalAlignment = HorizontalAlignment.Left
        };

        hinzufuegen.Click += (_, _) =>
        {
            var titel = neuesThema.Text?.Trim() ?? string.Empty;
            if (titel.Length == 0)
                return;

            DossierTopicEditing.SetForDossier(_dossier, titel, string.Empty);
            FuelleThemenEditor(wirt, feld);
            ZeichneBlatt();
            Hervorheben(feld.Key, blinken: true);
        };

        wirt.Children.Add(new TextBlock
        {
            Text = "Zusätzliche Zeile",
            Margin = new Thickness(0, 10, 0, 2)
        });

        wirt.Children.Add(neuesThema);
        wirt.Children.Add(hinzufuegen);
    }

    /// <summary>
    /// Die waehlbaren Schriftfarben. Bewusst eine kleine feste Auswahl statt
    /// eines Farbmischers: ein Dossier ist ein Brief an den Eigentuemer, kein
    /// Plakat, und jede Farbe muss auf Papier lesbar bleiben.
    /// </summary>
    private static readonly (string Name, string Hex)[] Schriftfarben =
    {
        ("Schwarz", ""),
        ("Rot", "C00000")
    };

    /// <summary>
    /// Die Farbwahl eines Themas. Gesetzt wird sie als Abweichung dieses
    /// Dossiers — wie der Text auch.
    /// </summary>
    private UIElement BaueFarbleiste(string titel, TextBox box, DossierPreviewField feld)
    {
        var leiste = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            Margin = new Thickness(0, 4, 0, 0)
        };

        leiste.Children.Add(new TextBlock
        {
            Text = "Farbe",
            VerticalAlignment = VerticalAlignment.Center,
            Margin = new Thickness(0, 0, 6, 0)
        });

        foreach (var (name, hex) in Schriftfarben)
        {
            var knopf = new Button
            {
                Width = 22,
                Height = 20,
                Margin = new Thickness(0, 0, 4, 0),
                ToolTip = name,
                Content = hex.Length == 0 ? "A" : string.Empty,
                FontSize = 10,
                Background = hex.Length == 0
                    ? Brushes.Transparent
                    : new SolidColorBrush(AusHex(hex))
            };

            var eigene = hex;

            knopf.Click += (_, _) =>
            {
                DossierTopicEditing.SetForDossier(_dossier, titel, box.Text);
                SetzeThemenFarbe(titel, eigene);
                ZeichneBlatt();
                _aktivesFeld = feld.Key;
                Hervorheben(feld.Key, blinken: true);
            };

            leiste.Children.Add(knopf);
        }

        return leiste;
    }

    private void SetzeThemenFarbe(string titel, string hex)
    {
        var zeile = _dossier.Topics.FirstOrDefault(t =>
            string.Equals(t.Title, titel, StringComparison.OrdinalIgnoreCase));

        if (zeile is not null)
            zeile.ColorHex = hex;
    }

    private static Color AusHex(string hex)
        => Color.FromRgb(
            Convert.ToByte(hex.Substring(0, 2), 16),
            Convert.ToByte(hex.Substring(2, 2), 16),
            Convert.ToByte(hex.Substring(4, 2), 16));

    /// <summary>
    /// Zwei Knoepfe, die die betroffenen Leitungen und Schaechte in den Text
    /// setzen.
    ///
    /// Eingefuegt wird eine MARKE, nicht die fertige Liste: sonst veraltet der
    /// Text, sobald eine Leitung dazukommt. Im Blatt daneben steht sofort die
    /// aufgeloeste Liste, damit sichtbar ist, was die Marke bedeutet.
    /// </summary>
    private UIElement BaueEinfuegeleiste(TextBox box, DossierPreviewField feld)
    {
        var leiste = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            Margin = new Thickness(0, 4, 0, 0)
        };

        void Einfuegen(string marke)
        {
            var stelle = box.SelectionStart;
            box.Text = box.Text.Insert(stelle, marke);
            box.SelectionStart = stelle + marke.Length;
            box.Focus();

            _aktivesFeld = feld.Key;
            ZeichneBlatt();
            Hervorheben(feld.Key, blinken: true);
        }

        var leitungen = new Button
        {
            Content = "Leitungen einfügen",
            Padding = new Thickness(8, 2, 8, 2),
            FontSize = 11,
            Margin = new Thickness(0, 0, 6, 0),
            ToolTip = "Setzt die betroffenen Leitungen an dieser Stelle ein"
        };

        var schaechte = new Button
        {
            Content = "Schächte einfügen",
            Padding = new Thickness(8, 2, 8, 2),
            FontSize = 11,
            ToolTip = "Setzt die zugehörigen Schächte an dieser Stelle ein"
        };

        leitungen.Click += (_, _) => Einfuegen("{{Haltungen_Text}}");
        schaechte.Click += (_, _) => Einfuegen("{{Schaechte_Text}}");

        leiste.Children.Add(leitungen);
        leiste.Children.Add(schaechte);
        return leiste;
    }

    /// <summary>
    /// Der Hinweis unter einem Gebietsthema. Er erscheint nur, wenn dieses
    /// Dossier vom Gebiet abweicht, und bietet an, den Text fuer alle
    /// Liegenschaften zu uebernehmen.
    ///
    /// Ohne ihn waere nicht zu sehen, dass ein hier getippter Ansprechpartner
    /// oder Unternehmer nur fuer DIESE Liegenschaft gilt — beim naechsten
    /// Dossier staende das Feld wieder leer.
    /// </summary>
    private UIElement BaueGebietsHinweis(
        string titel, TextBox box, Panel wirt, DossierPreviewField feld)
    {
        var zeile = new DockPanel { Margin = new Thickness(0, 4, 0, 0) };

        var uebernehmen = new Button
        {
            Content = "Für alle übernehmen",
            Padding = new Thickness(8, 2, 8, 2),
            FontSize = 11
        };

        DockPanel.SetDock(uebernehmen, Dock.Right);

        var text = new TextBlock
        {
            Text = "Weicht vom Gebiet ab — gilt nur für diese Liegenschaft.",
            TextWrapping = TextWrapping.Wrap,
            FontSize = 11,
            VerticalAlignment = VerticalAlignment.Center
        };

        uebernehmen.Click += (_, _) =>
        {
            DossierTopicEditing.PromoteToArea(_area, _dossier, titel, box.Text);
            FuelleThemenEditor(wirt, feld);
            ZeichneBlatt();
            Hervorheben(feld.Key, blinken: true);
        };

        zeile.Children.Add(uebernehmen);
        zeile.Children.Add(text);

        void Aktualisiere()
            => zeile.Visibility = DossierTopicEditing.HasDossierOverride(_dossier, titel)
                ? Visibility.Visible
                : Visibility.Collapsed;

        box.TextChanged += (_, _) => Aktualisiere();
        Aktualisiere();

        return zeile;
    }

    /// <summary>
    /// Der Editor einer Zeilenliste. Er baut sich nach jeder Aenderung an der
    /// Reihenfolge selbst neu auf; die Textfelder bleiben davon unberuehrt,
    /// solange nur getippt wird.
    /// </summary>
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

            var karte = new Border
            {
                BorderBrush = Randfarbe,
                BorderThickness = new Thickness(1),
                CornerRadius = new CornerRadius(3),
                Padding = new Thickness(8),
                Margin = new Thickness(0, 0, 0, 6)
            };

            var inhalt = new StackPanel();
            karte.Child = inhalt;

            var kopf = new DockPanel { Margin = new Thickness(0, 0, 0, 4) };
            var werkzeuge = new StackPanel { Orientation = Orientation.Horizontal };
            DockPanel.SetDock(werkzeuge, Dock.Right);

            werkzeuge.Children.Add(Werkzeug("▲", "Nach oben", () => Verschiebe(typ, stelle, -1, wirt, feld)));
            werkzeuge.Children.Add(Werkzeug("▼", "Nach unten", () => Verschiebe(typ, stelle, +1, wirt, feld)));
            werkzeuge.Children.Add(Werkzeug("✕", "Zeile entfernen", () =>
            {
                typ.Liste.RemoveAt(stelle);
                FuelleZeilenEditor(wirt, feld);
                ZeichneBlatt();
                Hervorheben(feld.Key, blinken: true);
            }));

            kopf.Children.Add(werkzeuge);
            kopf.Children.Add(new TextBlock
            {
                Text = "Zeile " + (stelle + 1),
                VerticalAlignment = VerticalAlignment.Center
            });

            inhalt.Children.Add(kopf);

            foreach (var spalte in typ.Spalten)
            {
                inhalt.Children.Add(new TextBlock
                {
                    Text = spalte.Label,
                    Margin = new Thickness(0, 4, 0, 2)
                });

                var mehrzeilig = spalte.Label is "Text" or "Name";
                var box = new TextBox
                {
                    Text = spalte.Read(zeile),
                    AcceptsReturn = mehrzeilig,
                    TextWrapping = mehrzeilig ? TextWrapping.Wrap : TextWrapping.NoWrap,
                    MinHeight = mehrzeilig ? 52 : 0
                };

                box.GotKeyboardFocus += (_, _) =>
                {
                    _aktivesFeld = feld.Key;
                    Hervorheben(feld.Key, blinken: true);
                };

                box.TextChanged += (_, _) =>
                {
                    spalte.Write(zeile, box.Text);
                    ZeichneBlatt();
                    Hervorheben(feld.Key, blinken: false);
                };

                inhalt.Children.Add(box);
            }

            wirt.Children.Add(karte);
        }

        var neu = new Button
        {
            Content = "+ Zeile",
            Padding = new Thickness(10, 4, 10, 4),
            HorizontalAlignment = HorizontalAlignment.Left
        };

        neu.Click += (_, _) =>
        {
            typ.Liste.Add(typ.Neu());
            FuelleZeilenEditor(wirt, feld);
            ZeichneBlatt();
            Hervorheben(feld.Key, blinken: true);
        };

        wirt.Children.Add(neu);
    }

    private void Verschiebe(ZeilenTyp typ, int stelle, int richtung, Panel wirt, DossierPreviewField feld)
    {
        var ziel = stelle + richtung;
        if (ziel < 0 || ziel >= typ.Liste.Count)
            return;

        var zeile = typ.Liste[stelle];
        typ.Liste.RemoveAt(stelle);
        typ.Liste.Insert(ziel, zeile);

        FuelleZeilenEditor(wirt, feld);
        ZeichneBlatt();
        Hervorheben(feld.Key, blinken: true);
    }

    private static Button Werkzeug(string zeichen, string hinweis, Action tue)
    {
        var knopf = new Button
        {
            Content = zeichen,
            MinWidth = 28,
            Padding = new Thickness(6, 0, 6, 0),
            Height = 24,
            Margin = new Thickness(4, 0, 0, 0),
            ToolTip = hinweis
        };

        knopf.Click += (_, _) => tue();
        return knopf;
    }

    private ZeilenTyp? ZeilenTypFuer(string key) => key switch
    {
        "Eigentuemer" => new ZeilenTyp(
            _dossier.Owners,
            () => new DossierOwnerRow(),
            new[]
            {
                new ZeilenSpalte("Haus-Nr.",
                    z => ((DossierOwnerRow)z).HouseNumber,
                    (z, w) => ((DossierOwnerRow)z).HouseNumber = w),
                new ZeilenSpalte("Parzelle",
                    z => ((DossierOwnerRow)z).ParcelNumber,
                    (z, w) => ((DossierOwnerRow)z).ParcelNumber = w),
                new ZeilenSpalte("Name",
                    z => ((DossierOwnerRow)z).Name,
                    (z, w) => ((DossierOwnerRow)z).Name = w),
                new ZeilenSpalte("Telefon",
                    z => ((DossierOwnerRow)z).Phone,
                    (z, w) => ((DossierOwnerRow)z).Phone = w),
                new ZeilenSpalte("Mail",
                    z => ((DossierOwnerRow)z).Mail,
                    (z, w) => ((DossierOwnerRow)z).Mail = w),
                new ZeilenSpalte("Objektbewohner",
                    z => ((DossierOwnerRow)z).Occupancy,
                    (z, w) => ((DossierOwnerRow)z).Occupancy = w)
            }),

        "Aenderungen" => new ZeilenTyp(
            _dossier.Changes,
            () => new DossierChangeRow(),
            new[]
            {
                new ZeilenSpalte("Version",
                    z => ((DossierChangeRow)z).Version,
                    (z, w) => ((DossierChangeRow)z).Version = w),
                new ZeilenSpalte("Datum",
                    z => ((DossierChangeRow)z).Date,
                    (z, w) => ((DossierChangeRow)z).Date = w),
                new ZeilenSpalte("Visum",
                    z => ((DossierChangeRow)z).Visum,
                    (z, w) => ((DossierChangeRow)z).Visum = w),
                new ZeilenSpalte("Art der Änderung",
                    z => ((DossierChangeRow)z).Change,
                    (z, w) => ((DossierChangeRow)z).Change = w)
            }),

        _ => null
    };
}
