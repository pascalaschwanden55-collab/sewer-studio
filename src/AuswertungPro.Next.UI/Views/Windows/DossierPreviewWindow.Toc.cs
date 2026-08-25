using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

using AuswertungPro.Next.Application.Dossiers.Preview;

namespace AuswertungPro.Next.UI.Views.Windows;

/// <summary>
/// Zusätzliche Punkte des Inhaltsverzeichnisses. Die drei echten Kapitel werden
/// als Vorlagentexte bearbeitet; diese Liste ist für externe Beilagen, die Word
/// selbst nicht als Kapitel und damit auch nicht mit einer Seite kennt.
/// </summary>
public partial class DossierPreviewWindow
{
    private UIElement BaueVerzeichnisEditor(DossierPreviewField feld)
    {
        var block = new StackPanel();
        block.Children.Add(new TextBlock
        {
            Text = "Zusätzliche Punkte stehen nach den Kapiteln. "
                + "Die Nummer setzt SewerStudio automatisch; eine Seitenzahl wird nicht erfunden.",
            FontStyle = FontStyles.Italic,
            TextWrapping = TextWrapping.Wrap,
            Margin = new Thickness(0, 0, 0, 8)
        });

        var liste = new StackPanel();
        block.Children.Add(liste);
        FuelleVerzeichnisEditor(liste, feld);
        return block;
    }

    private void FuelleVerzeichnisEditor(Panel wirt, DossierPreviewField feld)
    {
        wirt.Children.Clear();
        _dossier.TocAttachmentLines ??= new();

        for (var i = 0; i < _dossier.TocAttachmentLines.Count; i++)
        {
            var stelle = i;
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

            var kopf = new DockPanel { Margin = new Thickness(0, 0, 0, 4) };
            var werkzeuge = new StackPanel { Orientation = Orientation.Horizontal };
            DockPanel.SetDock(werkzeuge, Dock.Right);
            werkzeuge.Children.Add(Kleiner("▲", "Punkt nach oben", () =>
                VerschiebeVerzeichnispunkt(stelle, -1, wirt, feld)));
            werkzeuge.Children.Add(Kleiner("▼", "Punkt nach unten", () =>
                VerschiebeVerzeichnispunkt(stelle, +1, wirt, feld)));
            werkzeuge.Children.Add(Kleiner("✕", "Punkt entfernen", () =>
            {
                _dossier.TocAttachmentLines.RemoveAt(stelle);
                FuelleVerzeichnisEditor(wirt, feld);
                ZeichneBlatt();
                Betone(feld.Key);
            }));

            kopf.Children.Add(werkzeuge);
            kopf.Children.Add(new TextBlock
            {
                Text = "Zusätzlicher Punkt " + (stelle + 1),
                FontSize = 12,
                FontWeight = FontWeights.SemiBold,
                Foreground = (Brush)FindResource("TextBrush"),
                VerticalAlignment = VerticalAlignment.Center
            });
            inhalt.Children.Add(kopf);

            var box = new TextBox
            {
                Text = _dossier.TocAttachmentLines[stelle] ?? string.Empty,
                MinHeight = 34,
                Padding = new Thickness(6, 4, 6, 4),
                FontFamily = new FontFamily("Arial"),
                VerticalContentAlignment = VerticalAlignment.Center
            };
            box.GotKeyboardFocus += (_, _) => Betone(feld.Key);
            box.TextChanged += (_, _) =>
            {
                _dossier.TocAttachmentLines[stelle] = box.Text;
                ZeichneBlatt();
            };
            inhalt.Children.Add(box);
            wirt.Children.Add(karte);
        }

        var neu = Kleiner("+ Punkt ergänzen", "Einen zusätzlichen Verzeichnispunkt anhängen", () =>
        {
            _dossier.TocAttachmentLines.Add(string.Empty);
            FuelleVerzeichnisEditor(wirt, feld);
            ZeichneBlatt();
            Betone(feld.Key);

            if (wirt.Children.Count >= 2
                && wirt.Children[^2] is Border letzteKarte)
            {
                ErsteEingabe(letzteKarte)?.Focus();
            }
        });
        neu.HorizontalAlignment = HorizontalAlignment.Left;
        wirt.Children.Add(neu);
    }

    private void VerschiebeVerzeichnispunkt(
        int stelle,
        int richtung,
        Panel wirt,
        DossierPreviewField feld)
    {
        var ziel = stelle + richtung;
        if (ziel < 0 || ziel >= _dossier.TocAttachmentLines.Count)
            return;

        var text = _dossier.TocAttachmentLines[stelle];
        _dossier.TocAttachmentLines.RemoveAt(stelle);
        _dossier.TocAttachmentLines.Insert(ziel, text);
        FuelleVerzeichnisEditor(wirt, feld);
        ZeichneBlatt();
        Betone(feld.Key);
    }
}
