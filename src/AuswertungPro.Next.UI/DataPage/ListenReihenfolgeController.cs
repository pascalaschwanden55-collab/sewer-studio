using System.Collections;
using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Threading;
using AuswertungPro.Next.UI.Behaviors;
using AuswertungPro.Next.UI.Views.Controls;

namespace AuswertungPro.Next.UI.DataPage;

/// <summary>
/// Gemeinsame Mausbedienung beider Aufklapplisten. Verschiebt ausschliesslich ueber
/// den vorhandenen ViewModel-Weg; Nummerierung und Speichern bleiben dessen Aufgabe.
/// </summary>
public sealed class ListenReihenfolgeController
{
    private readonly ListBox _liste;
    private readonly ListenReihenfolgeLeiste _leiste;
    private readonly Action _klappeZu;
    private readonly DispatcherTimer _scrollTimer;
    private Point _startpunkt;
    private Point _ziehpunkt;
    private object? _kandidat;
    private object? _gezogen;
    private object[]? _ausgangsfolge;
    private ListenEinfuegelinie? _linie;
    private AdornerLayer? _linienEbene;

    public Func<IList?>? Datensaetze { get; set; }
    public Func<bool>? DarfVerschieben { get; set; }
    public Func<object, int, bool>? Verschiebe { get; set; }

    public ListenReihenfolgeController(ListBox liste, ListenReihenfolgeLeiste leiste, Action klappeZu)
    {
        _liste = liste;
        _leiste = leiste;
        _klappeZu = klappeZu;
        _scrollTimer = new DispatcherTimer(TimeSpan.FromMilliseconds(60), DispatcherPriority.Input,
            (_, _) => ScrolleAmRand(), liste.Dispatcher);
        _scrollTimer.Stop();
        liste.AllowDrop = true;
        liste.PreviewMouseLeftButtonDown += MerkeStart;
        liste.PreviewMouseLeftButtonUp += (_, _) => _kandidat = null;
        liste.PreviewMouseMove += StarteZiehen;
        liste.DragOver += ZeigeZiel;
        liste.DragLeave += (_, _) => { EntferneLinie(); _scrollTimer.Stop(); };
        liste.Drop += Ablegen;
        liste.SelectionChanged += (_, _) => ZeigeAuswahl();
        liste.Unloaded += (_, _) => Beende();
        leiste.AktivGeaendert += (_, _) =>
        {
            if (!leiste.IstAktiv) return;
            liste.Focus();
            klappeZu();
            ZeigeAuswahl();
            leiste.Hinweis = "An der Nr. ziehen. Die blaue Linie zeigt die neue Stelle. Escape bricht das Ziehen ab.";
        };
        leiste.PositionGewuenscht += VerschiebeAuswahl;
    }

    public void Beende()
    {
        BeendeZiehen();
        _leiste.IstAktiv = false;
        _leiste.Hinweis = string.Empty;
    }

    internal void BeendeZiehen()
    {
        _kandidat = _gezogen = null;
        _ausgangsfolge = null;
        _scrollTimer.Stop();
        EntferneLinie();
    }

    private void ZeigeAuswahl()
    {
        var index = Datensaetze?.Invoke()?.IndexOf(_liste.SelectedItem) ?? -1;
        _leiste.AuswahlText = index < 0 ? "Zeile auswählen" : $"Auswahl: Nr. {index + 1}";
    }

    private bool PruefeFreigabe(out IList records)
    {
        records = Datensaetze?.Invoke() ?? Array.Empty<object>();
        if (!_liste.IsEnabled || DarfVerschieben?.Invoke() != true || Verschiebe is null)
        {
            _leiste.Hinweis = "Die Reihenfolge kann gerade nicht geändert werden.";
            return false;
        }
        var eigeneSortierung = _liste.ItemsSource is not null
            && CollectionViewSource.GetDefaultView(_liste.ItemsSource) is ListCollectionView { CustomSort: not null };
        if (_liste.Items.Filter is not null || _liste.Items.SortDescriptions.Count > 0 || eigeneSortierung
            || _liste.Items.GroupDescriptions.Count > 0 || !GleicheFolge(records, _liste.Items))
        {
            _leiste.Hinweis = "Zum Umordnen zuerst Suche, Filter und Sortierung aufheben.";
            return false;
        }
        return true;
    }

    internal bool VerschiebeAnPosition(object record, int position)
    {
        if (!PruefeFreigabe(out var records) || records.IndexOf(record) < 0) return false;
        if (position < 1 || position > records.Count)
        {
            _leiste.Hinweis = $"Bitte eine Position von 1 bis {records.Count} eingeben.";
            return false;
        }
        if (records.IndexOf(record) == position - 1)
        {
            _leiste.Hinweis = $"Die Zeile steht bereits an Position {position}.";
            return false;
        }
        if (Verschiebe?.Invoke(record, position) != true)
        {
            _leiste.Hinweis = "Die Zeile konnte nicht verschoben werden.";
            return false;
        }
        _liste.SelectedItem = record;
        _liste.ScrollIntoView(record);
        ZeigeAuswahl();
        _leiste.Hinweis = $"Verschoben an Position {position}.";
        return true;
    }

    private void VerschiebeAuswahl(string text)
    {
        text = text.Trim();
        if (_liste.SelectedItem is not { } record)
        {
            _leiste.Hinweis = "Zuerst eine Zeile auswählen.";
            return;
        }
        var position = Datensaetze?.Invoke()?.Count ?? 0;
        if (text != "Ende" && !int.TryParse(text, NumberStyles.None, CultureInfo.InvariantCulture, out position))
        {
            _leiste.Hinweis = "Bitte eine ganze Zahl als Position eingeben.";
            return;
        }
        VerschiebeAnPosition(record, position);
    }

    private void MerkeStart(object sender, MouseButtonEventArgs e)
    {
        _kandidat = null;
        if (e.OriginalSource is not DependencyObject source) return;
        var griff = VisualTreeSafe.FindAncestor<Border>(source);
        if (!Equals(griff?.Tag, "ReihenfolgeGriff")) return;
        _kandidat = VisualTreeSafe.FindAncestor<ListBoxItem>(griff)?.DataContext;
        _startpunkt = e.GetPosition(_liste);
    }

    private void StarteZiehen(object sender, MouseEventArgs e)
    {
        if (_kandidat is null || e.LeftButton != MouseButtonState.Pressed) return;
        var diff = e.GetPosition(_liste) - _startpunkt;
        if (Math.Abs(diff.X) < SystemParameters.MinimumHorizontalDragDistance
            && Math.Abs(diff.Y) < SystemParameters.MinimumVerticalDragDistance) return;
        var record = _kandidat;
        _kandidat = null;
        if (!BereiteZiehenVor(record)) return;
        try
        {
            // Der Absender ist Teil des Payloads: kein Ablegen fremder Zeilen oder Dateien.
            DragDrop.DoDragDrop(_liste, new DataObject(typeof(ListenReihenfolgeController), this), DragDropEffects.Move);
        }
        finally
        {
            BeendeZiehen();
        }
        e.Handled = true;
    }

    internal bool BereiteZiehenVor(object record)
    {
        if (!PruefeFreigabe(out var records) || records.IndexOf(record) < 0) return false;
        _liste.Focus();
        _klappeZu();
        _liste.SelectedItem = record;
        _leiste.IstAktiv = true;
        _gezogen = record;
        _ausgangsfolge = records.Cast<object>().ToArray();
        return true;
    }

    private bool EigenerZiehvorgang(DragEventArgs e)
        => _gezogen is not null && e.Data.GetDataPresent(typeof(ListenReihenfolgeController))
            && ReferenceEquals(e.Data.GetData(typeof(ListenReihenfolgeController)), this);

    private void ZeigeZiel(object sender, DragEventArgs e)
    {
        e.Handled = true;
        e.Effects = DragDropEffects.None;
        if (!EigenerZiehvorgang(e)) return;
        _ziehpunkt = e.GetPosition(_liste);
        if (!PruefeFreigabe(out var records) || !GleicheFolge(records, _ausgangsfolge!))
        {
            EntferneLinie();
            _scrollTimer.Stop();
            return;
        }
        e.Effects = DragDropEffects.Move;
        AktualisiereLinie();
        _scrollTimer.Start();
    }

    private void Ablegen(object sender, DragEventArgs e)
    {
        e.Handled = true;
        e.Effects = DragDropEffects.None;
        if (!EigenerZiehvorgang(e)) return;
        _ziehpunkt = e.GetPosition(_liste);
        var zeile = Zielzeile();
        if (zeile is null) return;
        if (LegeAb(zeile.DataContext, MausUnterMitte(zeile))) e.Effects = DragDropEffects.Move;
    }

    internal bool LegeAb(object ziel, bool danach)
    {
        if (_gezogen is null || _ausgangsfolge is null || !PruefeFreigabe(out var records)) return false;
        if (!GleicheFolge(records, _ausgangsfolge))
        {
            _leiste.Hinweis = "Die Liste hat sich geändert. Bitte die Zeile erneut ziehen.";
            return false;
        }
        var position = Zielposition(records.IndexOf(_gezogen), records.IndexOf(ziel), danach, records.Count);
        return VerschiebeAnPosition(_gezogen, position);
    }

    internal static int Zielposition(int quelle, int ziel, bool danach, int anzahl)
    {
        if (quelle < 0 || ziel < 0 || quelle >= anzahl || ziel >= anzahl) return 0;
        var luecke = ziel + (danach ? 1 : 0);
        return luecke - (quelle < luecke ? 1 : 0) + 1;
    }

    internal static bool GleicheFolge(IList links, IList rechts)
    {
        if (links.Count != rechts.Count) return false;
        for (var i = 0; i < links.Count; i++)
            if (!ReferenceEquals(links[i], rechts[i])) return false;
        return true;
    }

    private ListBoxItem? Zielzeile()
    {
        if (_liste.Items.Count == 0 || _ziehpunkt.X < 0 || _ziehpunkt.X >= _liste.ActualWidth - 20
            || _ziehpunkt.Y < 0 || _ziehpunkt.Y > _liste.ActualHeight) return null;
        if (VisualTreeSafe.FindAncestor<ListBoxItem>(_liste.InputHitTest(_ziehpunkt) as DependencyObject) is { } zeile)
            return zeile;
        // Freie Flaeche unter der letzten sichtbaren Zeile bedeutet: ans Listenende.
        var letzte = _liste.ItemContainerGenerator.ContainerFromIndex(_liste.Items.Count - 1) as ListBoxItem;
        return letzte is not null && _liste.TranslatePoint(_ziehpunkt, letzte).Y >= letzte.ActualHeight ? letzte : null;
    }

    private bool MausUnterMitte(ListBoxItem zeile)
        => _liste.TranslatePoint(_ziehpunkt, zeile).Y >= zeile.ActualHeight / 2;

    private void AktualisiereLinie()
    {
        EntferneLinie();
        if (Zielzeile() is not { } zeile) return;
        _linienEbene = AdornerLayer.GetAdornerLayer(zeile);
        if (_linienEbene is null) return;
        _linie = new ListenEinfuegelinie(zeile, MausUnterMitte(zeile),
            (Brush)_liste.FindResource("AccentBrush")) { IsHitTestVisible = false };
        _linienEbene.Add(_linie);
    }

    private void EntferneLinie()
    {
        if (_linie is not null) _linienEbene?.Remove(_linie);
        _linie = null;
        _linienEbene = null;
    }

    private void ScrolleAmRand()
    {
        if (_gezogen is null || !_liste.IsVisible) { _scrollTimer.Stop(); return; }
        if (_ausgangsfolge is null || !PruefeFreigabe(out var records) || !GleicheFolge(records, _ausgangsfolge))
        {
            _scrollTimer.Stop();
            EntferneLinie();
            return;
        }
        var richtung = _ziehpunkt.Y < 35 ? -1 : _ziehpunkt.Y > _liste.ActualHeight - 35 ? 1 : 0;
        if (richtung == 0) return;
        if (VisualTreeSafe.FindDescendants<ScrollViewer>(_liste).FirstOrDefault() is not { } scroll) return;
        scroll.ScrollToVerticalOffset(scroll.VerticalOffset + richtung * 24);
        _liste.UpdateLayout();
        AktualisiereLinie();
    }

}
