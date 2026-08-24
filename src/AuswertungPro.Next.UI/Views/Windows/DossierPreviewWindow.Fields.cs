using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

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
                    break;

                case DossierPreviewFieldKind.File:
                    FieldPanel.Children.Add(BaueDateifeld(feld));
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
        };

        return box;
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

        knopf.Click += (_, _) =>
        {
            var dialog = new Microsoft.Win32.OpenFileDialog
            {
                Title = "Übersichtsplan wählen",
                Filter = "Bilder|*.png;*.jpg;*.jpeg;*.bmp|Alle Dateien|*.*"
            };

            if (dialog.ShowDialog(this) != true)
                return;

            feld.Write?.Invoke(dialog.FileName);
            anzeige.Text = dialog.FileName;
            _aktivesFeld = feld.Key;
            ZeichneBlatt();
            Hervorheben(feld.Key, blinken: true);
        };

        panel.Children.Add(knopf);
        panel.Children.Add(anzeige);
        return panel;
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
            Width = 28,
            Height = 24,
            Margin = new Thickness(4, 0, 0, 0),
            ToolTip = hinweis
        };

        knopf.Click += (_, _) => tue();
        return knopf;
    }

    private ZeilenTyp? ZeilenTypFuer(string key) => key switch
    {
        "Themen" => new ZeilenTyp(
            _dossier.Topics,
            () => new DossierTopicRow(),
            new[]
            {
                new ZeilenSpalte("Thema",
                    z => ((DossierTopicRow)z).Title,
                    (z, w) => ((DossierTopicRow)z).Title = w),
                new ZeilenSpalte("Text",
                    z => ((DossierTopicRow)z).Text,
                    (z, w) => ((DossierTopicRow)z).Text = w)
            }),

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
