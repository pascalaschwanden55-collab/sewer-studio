using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

using AuswertungPro.Next.Application.Dossiers;
using AuswertungPro.Next.UI.Behaviors;
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

    /// <summary>
    /// Die Eingabestelle je Schluessel. Ohne sie wuesste ein Klick ins Blatt
    /// zwar, WELCHES Feld gemeint ist, aber nicht, wohin er springen soll.
    /// </summary>
    private readonly Dictionary<DossierPreviewTarget, FrameworkElement> _feldStellen = new();

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
        _feldStellen.Clear();

        var angaben = felder.Where(f => f.Kind is not DossierPreviewFieldKind.Rows).ToList();

        if (angaben.Count > 0)
        {
            var inhalt = new StackPanel();

            foreach (var feld in angaben)
            {
                var karte = BaueAngabe(feld);
                inhalt.Children.Add(karte);
                MerkeStelle(DossierPreviewTarget.Field(feld.Key), karte);
            }

            FieldPanel.Children.Add(Abschnitt("Angaben", inhalt, offen: true));
        }

        var feste = DossierPreviewTextInventory.Literals(seite);
        var istVerzeichnis = seite.Blocks
            .OfType<DossierPreviewParagraph>()
            .Any(absatz => absatz.TocEntry is not null);

        // Auf der Verzeichnisseite sind die drei Kapiteltitel die Hauptarbeit.
        // Sie stehen deshalb offen und vor den zusätzlichen Punkten.
        if (istVerzeichnis && feste.Count > 0)
        {
            FieldPanel.Children.Add(Abschnitt(
                "Inhaltsverzeichnis bearbeiten", BaueFesteTexte(feste), offen: true));
        }

        // Jede Zeilenliste bekommt ihren eigenen Abschnitt mit ihrem Namen.
        foreach (var feld in felder.Where(f => f.Kind is DossierPreviewFieldKind.Rows))
        {
            var inhalt = feld.Key switch
            {
                "Themen" => BaueThemenEditor(feld),
                "Verzeichnis_Beilagen" => BaueVerzeichnisEditor(feld),
                _ => BaueZeilenEditor(feld)
            };

            var abschnitt = Abschnitt(feld.Label, inhalt, offen: true);
            FieldPanel.Children.Add(abschnitt);
            MerkeStelle(DossierPreviewTarget.Field(feld.Key), abschnitt);
        }

        if (!istVerzeichnis && feste.Count > 0)
            FieldPanel.Children.Add(Abschnitt(
                "Beschriftungen und Überschriften", BaueFesteTexte(feste), offen: false));

        if (FieldPanel.Children.Count == 0)
        {
            FieldPanel.Children.Add(new TextBlock
            {
                Text = "Auf dieser Seite gibt es nichts auszufüllen.",
                TextWrapping = TextWrapping.Wrap
            });
        }
    }

    /// <summary>
    /// Merkt sich, wo eine fachliche Zieladresse rechts zu finden ist. Wird
    /// eine dynamische Tabelle neu aufgebaut, ersetzt das neue sichtbare Feld
    /// die veraltete Control-Instanz derselben Adresse.
    /// </summary>
    private void MerkeStelle(DossierPreviewTarget target, UIElement stelle)
    {
        if (stelle is FrameworkElement element)
            _feldStellen[target] = element;
    }

    /// <summary>
    /// Springt zu der Stelle, die im Blatt angeklickt wurde: der Abschnitt wird
    /// aufgeklappt, das Feld sichtbar gescrollt und bekommt den Schreibfokus.
    ///
    /// Ohne Feld passiert nichts — ein Klick, der scheinbar reagiert und dann
    /// doch nirgends hinfuehrt, waere schlimmer als gar keiner.
    /// </summary>
    private bool SpringeZuFeld(DossierPreviewTarget target)
    {
        if (!_feldStellen.TryGetValue(target, out var stelle))
            return false;

        foreach (var expander in Vorfahren(stelle).OfType<Expander>())
            expander.IsExpanded = true;

        stelle.BringIntoView();
        stelle.Dispatcher.BeginInvoke(new Action(() =>
        {
            stelle.BringIntoView();
            ErsteEingabe(stelle)?.Focus();
        }), System.Windows.Threading.DispatcherPriority.Loaded);

        Betone(target);
        return true;
    }

    private static IEnumerable<DependencyObject> Vorfahren(DependencyObject start)
    {
        var aktuell = VisualTreeSafe.GetParentSafe(start);
        while (aktuell is not null)
        {
            yield return aktuell;
            aktuell = VisualTreeSafe.GetParentSafe(aktuell);
        }
    }

    /// <summary>Das erste beschreibbare Feld innerhalb einer Karte.</summary>
    private static Control? ErsteEingabe(DependencyObject wurzel)
    {
        if (wurzel is TextBox or RichTextBox)
            return (Control)wurzel;

        var anzahl = System.Windows.Media.VisualTreeHelper.GetChildrenCount(wurzel);
        for (var i = 0; i < anzahl; i++)
        {
            var treffer = ErsteEingabe(
                System.Windows.Media.VisualTreeHelper.GetChild(wurzel, i));

            if (treffer is not null)
                return treffer;
        }

        return null;
    }

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
    private void Betone(string fieldKey)
        => Betone(DossierPreviewTarget.Field(fieldKey));

    private void Betone(DossierPreviewTarget target)
    {
        _aktivesFeld = target;
        Hervorheben(target, blinken: true);
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
