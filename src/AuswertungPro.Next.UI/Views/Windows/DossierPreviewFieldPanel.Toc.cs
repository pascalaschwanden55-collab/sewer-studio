using System;
using System.Globalization;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

using AuswertungPro.Next.Application.Dossiers;
using AuswertungPro.Next.Application.Dossiers.Preview;
using AuswertungPro.Next.Domain.Models.Dossiers;

namespace AuswertungPro.Next.UI.Views.Windows;

/// <summary>
/// Zusätzliche Punkte des Inhaltsverzeichnisses. Die drei echten Kapitel werden
/// als Vorlagentexte bearbeitet; diese Liste ist für externe Beilagen, die Word
/// selbst nicht als Kapitel und damit auch nicht mit einer Seite kennt. Darum
/// besitzt jeder Zusatzpunkt eine eigene, bearbeitbare Seitenzahl.
/// </summary>
internal sealed partial class DossierPreviewFieldPanel
{
    private UIElement BaueVerzeichnisEditor(DossierPreviewField feld)
    {
        var block = new StackPanel();
        block.Children.Add(new TextBlock
        {
            Text = "Zusätzliche Punkte stehen nach den Kapiteln. "
                + "Die Nummer setzt SewerStudio automatisch. Die vorgeschlagene Seitenzahl kann geändert werden.",
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
        _dossier.TocAttachments ??= new();
        ErgaenzeFehlendeVerzeichnisSeitenzahlen();

        foreach (var target in _feldStellen.Keys
                     .Where(target => target.Kind == DossierPreviewTargetKind.Row
                         && string.Equals(
                             target.Key,
                             feld.Key,
                             StringComparison.OrdinalIgnoreCase))
                     .ToList())
        {
            _feldStellen.Remove(target);
        }

        for (var i = 0; i < _dossier.TocAttachments.Count; i++)
        {
            var stelle = i;
            var punkt = _dossier.TocAttachments[stelle];
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
                _dossier.TocAttachments.RemoveAt(stelle);
                FuelleVerzeichnisEditor(wirt, feld);
                _zeichneBlatt();
                Betone(feld.Key);
            }));

            kopf.Children.Add(werkzeuge);
            kopf.Children.Add(new TextBlock
            {
                Text = "Zusätzlicher Punkt " + (stelle + 1),
                FontSize = 12,
                FontWeight = FontWeights.SemiBold,
                Foreground = (Brush)_ressource("TextBrush"),
                VerticalAlignment = VerticalAlignment.Center
            });
            inhalt.Children.Add(kopf);

            var eingaben = new Grid();
            eingaben.ColumnDefinitions.Add(new ColumnDefinition
            {
                Width = new GridLength(1, GridUnitType.Star)
            });
            eingaben.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            eingaben.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            eingaben.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });

            var titelLabel = new TextBlock { Text = "Titel", Margin = new Thickness(0, 0, 0, 3) };
            var seitenLabel = new TextBlock
            {
                Text = "Seite",
                Margin = new Thickness(10, 0, 0, 3),
                HorizontalAlignment = HorizontalAlignment.Right
            };
            Grid.SetColumn(seitenLabel, 1);
            eingaben.Children.Add(titelLabel);
            eingaben.Children.Add(seitenLabel);

            var box = new TextBox
            {
                Text = punkt.Title ?? string.Empty,
                MinHeight = 34,
                Padding = new Thickness(6, 4, 6, 4),
                FontFamily = new FontFamily("Arial"),
                VerticalContentAlignment = VerticalAlignment.Center
            };
            box.GotKeyboardFocus += (_, _) => Betone(feld.Key);
            box.TextChanged += (_, _) =>
            {
                punkt.Title = box.Text;
                _zeichneBlatt();
            };
            Grid.SetRow(box, 1);
            eingaben.Children.Add(box);

            var seite = new TextBox
            {
                Text = punkt.PageNumber ?? string.Empty,
                Width = 64,
                MinHeight = 34,
                MaxLength = 4,
                Margin = new Thickness(10, 0, 0, 0),
                Padding = new Thickness(6, 4, 6, 4),
                FontFamily = new FontFamily("Arial"),
                HorizontalContentAlignment = HorizontalAlignment.Right,
                VerticalContentAlignment = VerticalAlignment.Center,
                ToolTip = "Seitenzahl am rechten Rand des Inhaltsverzeichnisses"
            };
            seite.GotKeyboardFocus += (_, _) => Betone(feld.Key);
            seite.TextChanged += (_, _) =>
            {
                punkt.PageNumber = seite.Text;
                _zeichneBlatt();
            };
            Grid.SetRow(seite, 1);
            Grid.SetColumn(seite, 1);
            eingaben.Children.Add(seite);

            inhalt.Children.Add(eingaben);
            wirt.Children.Add(karte);
            MerkeStelle(DossierPreviewTarget.Row(feld.Key, stelle), karte);
        }

        var neu = Kleiner("+ Punkt ergänzen", "Einen zusätzlichen Verzeichnispunkt anhängen", () =>
        {
            _dossier.TocAttachments.Add(new DossierTocAttachment
            {
                PageNumber = NaechsteVerzeichnisSeite()
            });
            FuelleVerzeichnisEditor(wirt, feld);
            _zeichneBlatt();
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
        if (ziel < 0 || ziel >= _dossier.TocAttachments.Count)
            return;

        var punkt = _dossier.TocAttachments[stelle];
        _dossier.TocAttachments.RemoveAt(stelle);
        _dossier.TocAttachments.Insert(ziel, punkt);
        FuelleVerzeichnisEditor(wirt, feld);
        _zeichneBlatt();
        Betone(feld.Key);
    }

    private void ErgaenzeFehlendeVerzeichnisSeitenzahlen()
    {
        foreach (var punkt in _dossier.TocAttachments.Where(punkt => punkt.PageNumber is null))
            punkt.PageNumber = NaechsteVerzeichnisSeite();
    }

    private string NaechsteVerzeichnisSeite()
    {
        var start = AktuellerVerzeichnisStart();
        var hoechsteSeite = _dossier.TocAttachments
            .Select(punkt => punkt.PageNumber)
            .Select(text => int.TryParse(
                text,
                NumberStyles.Integer,
                CultureInfo.InvariantCulture,
                out var nummer)
                    ? nummer
                    : 0)
            .DefaultIfEmpty(0)
            .Append(start.FirstPageNumber - 1)
            .Max();

        return (hoechsteSeite + 1).ToString(CultureInfo.InvariantCulture);
    }

    private DossierTocAttachmentStart AktuellerVerzeichnisStart()
        => VerzeichnisStart(_document, _dossier);

    /// <summary>
    /// Wo die zusaetzlichen Verzeichnispunkte anfangen. Gemeinsam nutzbar, weil
    /// auch das Fenster sie beim Aufbau der Werte braucht — und zwar bevor es
    /// diese Eingabeseite ueberhaupt gibt.
    /// </summary>
    public static DossierTocAttachmentStart VerzeichnisStart(
        DossierPreviewDocument document,
        DossierDefinition dossier)
        => DossierTocAttachments.StartAfter(
            document.Pages
                .SelectMany(page => page.Blocks)
                .OfType<DossierPreviewParagraph>()
                .Select(paragraph => paragraph.TocEntry),
            dossier.HiddenChapters);

}
