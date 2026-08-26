using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

using AuswertungPro.Next.Application.Dossiers;
using AuswertungPro.Next.Application.Dossiers.Preview;
using AuswertungPro.Next.Domain.Models.Dossiers;

namespace AuswertungPro.Next.UI.Views.Windows;

/// <summary>
/// Die Zeilenlisten der Vorschau — Eigentuemer und Aenderungswesen.
/// </summary>
internal sealed partial class DossierPreviewFieldPanel
{

    private UIElement BaueZeilenEditor(DossierPreviewField feld)
    {
        var wirt = new StackPanel();
        FuelleZeilenEditor(wirt, feld);
        return wirt;
    }

    private void FuelleZeilenEditor(Panel wirt, DossierPreviewField feld)
    {
        _textUndo.Reset();
        wirt.Children.Clear();
        EntferneAlteZeilenStellen(feld.Key);

        // Word zeigt fuer beide Tabellen auch ohne Fachdaten eine sichtbare
        // Grundzeile. Genau diese Zeile muss rechts bereits ihre Eingaben
        // besitzen; eine leere Eingabehilfe wird vor dem Speichern entfernt.
        if (string.Equals(feld.Key, "Eigentuemer", StringComparison.OrdinalIgnoreCase))
            DossierOwnerRows.EnsureStarter(_dossier);
        else if (string.Equals(feld.Key, "Aenderungen", StringComparison.OrdinalIgnoreCase))
            DossierChangeRows.EnsureStarter(_dossier);

        var typ = ZeilenTypFuer(feld.Key);
        if (typ is null)
            return;

        if (string.Equals(feld.Key, "Eigentuemer", StringComparison.OrdinalIgnoreCase))
        {
            wirt.Children.Add(DossierOwnerLabelFieldBuilder.Build(
                _dossier,
                typ.Liste.Count,
                Randfarbe,
                (Brush)_ressource("TextBrush"),
                _zeichneBlatt,
                Betone,
                MerkeStelle));
        }

        for (var i = 0; i < typ.Liste.Count; i++)
        {
            var stelle = i;
            var zeile = typ.Liste[stelle]!;
            var rowTarget = DossierPreviewTarget.Row(feld.Key, stelle);

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
            var entfernen = Kleiner("✕", "Zeile entfernen", () =>
            {
                typ.Liste.RemoveAt(stelle);
                FuelleZeilenEditor(wirt, feld);
                _zeichneBlatt();
                Betone(feld.Key);
            });
            werkzeuge.Children.Add(entfernen);

            void AktualisiereEntfernen()
            {
                var istLeereGrundzeile = typ.Liste.Count == 1
                    && (zeile is DossierChangeRow change
                            && !DossierChangeRows.HasContent(change)
                        || zeile is DossierOwnerRow owner
                            && !owner.HasContent);

                entfernen.IsEnabled = !istLeereGrundzeile;
                entfernen.ToolTip = istLeereGrundzeile
                    ? "Die im Word sichtbare leere Grundzeile bleibt bestehen."
                    : "Zeile entfernen";
            }

            AktualisiereEntfernen();

            kopf.Children.Add(werkzeuge);
            kopf.Children.Add(new TextBlock
            {
                Text = "Zeile " + (stelle + 1),
                FontSize = 12,
                FontWeight = FontWeights.SemiBold,
                Foreground = (Brush)_ressource("TextBrush"),
                VerticalAlignment = VerticalAlignment.Center
            });

            inhalt.Children.Add(kopf);

            foreach (var spalte in typ.Spalten)
            {
                var feldKarte = new StackPanel();

                feldKarte.Children.Add(new TextBlock
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
                    StyleRanges = DossierEditableRowFormatting.ReadStyles(
                        zeile, spalte.StyleKey, text)
                });
                box.AcceptsReturn = mehrzeilig;
                box.MinHeight = mehrzeilig ? 54 : 34;
                box.MaxHeight = mehrzeilig ? double.PositiveInfinity : 34;
                box.VerticalScrollBarVisibility = mehrzeilig
                    ? ScrollBarVisibility.Auto
                    : ScrollBarVisibility.Hidden;

                var cellTarget = DossierPreviewTarget.RowCell(
                    feld.Key,
                    stelle,
                    DossierEditableRowFormatting.PreviewColumnKey(
                        feld.Key, spalte.StyleKey));

                box.GotKeyboardFocus += (_, _) => Betone(cellTarget);

                box.TextChanged += (_, _) =>
                {
                    DossierEditableRowFormatting.Save(
                        zeile, spalte.StyleKey, spalte.Write, box);
                    AktualisiereEntfernen();
                    _zeichneBlatt();
                };

                feldKarte.Children.Add(box);
                var formatWerkzeuge = DossierTextFormattingToolbar.Create(box, () =>
                {
                    DossierEditableRowFormatting.Save(
                        zeile, spalte.StyleKey, spalte.Write, box);
                    _zeichneBlatt();
                    Betone(cellTarget);
                });
                ZeigeWerkzeugeNurAmAktivenFeld(feldKarte, formatWerkzeuge);
                feldKarte.Children.Add(formatWerkzeuge);
                inhalt.Children.Add(feldKarte);

                MerkeStelle(cellTarget, box);
            }

            wirt.Children.Add(karte);
            MerkeStelle(rowTarget, karte);
        }

        var neu = Kleiner("+ Zeile", "Eine Zeile anhängen", () =>
        {
            typ.Liste.Add(typ.Neu());
            FuelleZeilenEditor(wirt, feld);
            _zeichneBlatt();
            Betone(feld.Key);
        });

        neu.HorizontalAlignment = HorizontalAlignment.Left;
        wirt.Children.Add(neu);
    }

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
        _zeichneBlatt();
        Betone(feld.Key);
    }
}
