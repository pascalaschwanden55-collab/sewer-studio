using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Data;
using AuswertungPro.Next.Application.WebGis;
using CommunityToolkit.Mvvm.ComponentModel;

namespace AuswertungPro.Next.UI.Views.Windows;

/// <summary>Was ein Ablauf dem Fenster zurückgibt: der gezeigte Plan, eine Meldung und — nach dem Schreiben — der geschriebene Teil.</summary>
public sealed record WebGisFensterStand(WebGisExportPlan Plan, string Meldung, WebGisExportPlan? Geschrieben = null);

/// <summary>
/// Vergleichsliste SewerStudio ↔ WebGIS (Wunsch Pascal 28.09.2026, Entwurf freigegeben): links die Objekte, rechts
/// jedes Feld mit SewerStudio-Wert, WebGIS-Wert und was beim Schreiben passiert. Ein Objekt lässt sich einzeln neu
/// prüfen und einzeln schreiben, ohne die ganze Liste neu zu suchen. Vor dem Schreiben zeigt ein Fenster genau die
/// Änderungen «vorher → nachher», danach das Ergebnis je Feld. Nicht modal: bleibt offen, während in Haltungen/Schächten
/// korrigiert wird. Das Fenster orchestriert nur; geprüft und geschrieben wird im UseCase.
/// </summary>
public partial class WebGisVorschauWindow : Window
{
    /// <summary>Die Abläufe, die die Export-Seite dem Fenster leiht.</summary>
    public sealed record Ablaeufe(
        Func<Task<WebGisFensterStand?>> PruefeAlle,
        Func<WebGisObjektart, Guid, Task<WebGisFensterStand?>> PruefeEinzeln,
        Func<WebGisObjektart, Guid, Func<WebGisExportPlan, bool>, Task<WebGisFensterStand?>> SchreibeEinzeln,
        Func<Func<WebGisExportPlan, bool>, Task<WebGisFensterStand?>> SchreibeAlle,
        Action<WebGisObjektart, Guid>? Oeffne = null);

    private readonly Ablaeufe _ablaeufe;
    private readonly Anzeige _anzeige = new();

    /// <summary>Für Tests: Bestätigung ersetzen (Standard: Fenster «vorher → nachher»).</summary>
    internal Func<IReadOnlyList<WebGisSchreibZeile>, string, bool>? BestaetigungFuerTests { get; set; }

    public WebGisVorschauWindow(Ablaeufe ablaeufe)
    {
        _ablaeufe = ablaeufe ?? throw new ArgumentNullException(nameof(ablaeufe));
        InitializeComponent();
        DataContext = _anzeige;
    }

    internal Anzeige Stand => _anzeige;

    /// <summary>Erstes Prüfen nach dem Öffnen und «Alle neu prüfen».</summary>
    public Task PruefeAsync() => LaufAsync(() => _ablaeufe.PruefeAlle(), "Lese den WebGIS-Stand je Objekt …");

    private async void OnAlleNeuPruefen(object sender, RoutedEventArgs e) => await PruefeAsync();

    private async void OnEinzelnPruefen(object sender, RoutedEventArgs e)
    {
        if (_anzeige.Ausgewaehlt is not { } o) return;
        await LaufAsync(() => _ablaeufe.PruefeEinzeln(o.Objektart, o.RecordId), "Lese " + o.ArtText + " " + o.Name + " frisch …");
    }

    private async void OnEinzelnSchreiben(object sender, RoutedEventArgs e)
    {
        if (_anzeige.Ausgewaehlt is not { } o) return;
        await LaufAsync(() => _ablaeufe.SchreibeEinzeln(o.Objektart, o.RecordId, p => Bestaetige(p, o.ArtText + " " + o.Name)),
            "Prüfe " + o.ArtText + " " + o.Name + " nochmals frisch …");
    }

    private async void OnAlleSchreiben(object sender, RoutedEventArgs e)
        => await LaufAsync(() => _ablaeufe.SchreibeAlle(p => Bestaetige(p, "Alle Änderungen")),
            "Prüfe alle Objekte nochmals frisch …");

    private void OnBearbeiten(object sender, RoutedEventArgs e)
    {
        if (!_anzeige.Bereit || _ablaeufe.Oeffne is null || _anzeige.Ausgewaehlt is not { } o) return;
        _ablaeufe.Oeffne(o.Objektart, o.RecordId);
        _anzeige.Status = o.ArtText + " " + o.Name + " geöffnet — nach der Korrektur «Nur dieses Objekt neu prüfen».";
    }

    /// <summary>
    /// Doppelklick auf ein Objekt öffnet es in SewerStudio (Wunsch Pascal 28.09.2026); das Prüffenster bleibt offen.
    /// Nur ein Klick auf einen Eintrag zählt, nicht auf Bildlauf oder Leerraum.
    /// </summary>
    private void OnObjektDoppelklick(object sender, System.Windows.Input.MouseButtonEventArgs e)
    {
        var daten = e.OriginalSource switch
        {
            FrameworkElement f => f.DataContext,
            FrameworkContentElement c => c.DataContext,
            _ => null,
        };
        if (daten is not WebGisVergleichsObjekt o) return;
        _anzeige.Ausgewaehlt = o;
        OnBearbeiten(sender, e);
        e.Handled = true;
    }

    private void OnSchliessen(object sender, RoutedEventArgs e) => Close();

    /// <summary>Genau die Änderungen «vorher → nachher» zeigen; geschrieben wird nur nach «Ja».</summary>
    private bool Bestaetige(WebGisExportPlan plan, string was)
    {
        var zeilen = WebGisVergleichsanzeige.Schreibliste(plan);
        if (zeilen.Count == 0) return false;
        if (BestaetigungFuerTests is { } test) return test(zeilen, was);
        return WebGisSchreibBestaetigungWindow.Frage(this, zeilen, was);
    }

    private async Task LaufAsync(Func<Task<WebGisFensterStand?>> ablauf, string status)
    {
        if (!_anzeige.Bereit) return;
        _anzeige.Bereit = false;
        _anzeige.Status = status;
        try
        {
            var stand = await ablauf();
            if (stand is null)
            {
                _anzeige.Status = "Abgebrochen oder nicht angemeldet — nichts geschrieben.";
                return;
            }
            _anzeige.Zeige(stand.Plan, stand.Meldung);
            if (stand.Geschrieben is { } geschrieben && BestaetigungFuerTests is null)
            {
                var zeilen = WebGisVergleichsanzeige.Schreibliste(geschrieben, nachDemSchreiben: true);
                if (zeilen.Count > 0) WebGisSchreibBestaetigungWindow.ZeigeErgebnis(this, zeilen, stand.Meldung);
            }
        }
        catch (Exception ex)
        {
            _anzeige.Status = "Fehler: " + ex.Message;
        }
        finally
        {
            _anzeige.Bereit = true;
        }
    }

    private void OnClosing(object? sender, CancelEventArgs e)
    {
        if (!_anzeige.Bereit) e.Cancel = true; // während Lesen/Schreiben nicht schliessen
    }

    protected override void OnSourceInitialized(EventArgs e)
    {
        base.OnSourceInitialized(e);
        Closing += OnClosing;
    }

    /// <summary>Anzeigezustand des Fensters (Objektliste, Auswahl, Filter, Knöpfe).</summary>
    public sealed partial class Anzeige : ObservableObject
    {
        private IReadOnlyList<WebGisVergleichsObjekt> _alle = Array.Empty<WebGisVergleichsObjekt>();

        [ObservableProperty] private ICollectionView? _objekte;
        [ObservableProperty] private ICollectionView? _zeilen;
        [ObservableProperty] private WebGisVergleichsObjekt? _ausgewaehlt;
        [ObservableProperty] private string _suche = string.Empty;
        [ObservableProperty] private bool _nurUnterschiede;
        [ObservableProperty] private string _zaehler = string.Empty;
        [ObservableProperty] private string _geprueftUm = string.Empty;
        [ObservableProperty] private string _titel = "Noch nicht geprüft";
        [ObservableProperty] private string _untertitel = string.Empty;
        [ObservableProperty] private string _schreibKnopf = "Nichts zu schreiben";
        [ObservableProperty, NotifyPropertyChangedFor(nameof(SchreibenAktiv))] private bool _kannSchreiben;
        [ObservableProperty, NotifyPropertyChangedFor(nameof(AlleSchreibenAktiv))] private bool _kannAlleSchreiben;
        [ObservableProperty, NotifyPropertyChangedFor(nameof(EinzelnPruefenAktiv))] private bool _hatAuswahl;
        [ObservableProperty, NotifyPropertyChangedFor(nameof(SchreibenAktiv), nameof(AlleSchreibenAktiv), nameof(EinzelnPruefenAktiv))]
        private bool _bereit = true;

        public bool SchreibenAktiv => Bereit && KannSchreiben;
        public bool AlleSchreibenAktiv => Bereit && KannAlleSchreiben;
        public bool EinzelnPruefenAktiv => Bereit && HatAuswahl;
        [ObservableProperty] private string _status = string.Empty;

        public void Zeige(WebGisExportPlan plan, string meldung)
        {
            var vorher = Ausgewaehlt;
            if (vorher is not null) vorher.PropertyChanged -= OnObjektGeaendert;
            _alle = WebGisVergleichsanzeige.Objekte(plan);
            foreach (var o in _alle) o.PropertyChanged += OnGesamtGeaendert;

            var ansicht = CollectionViewSource.GetDefaultView(_alle.ToList());
            ansicht.Filter = x => x is WebGisVergleichsObjekt o
                && (string.IsNullOrWhiteSpace(Suche) || o.Name.Contains(Suche.Trim(), StringComparison.OrdinalIgnoreCase));
            Objekte = ansicht;
            GeprueftUm = "Geprüft um " + DateTime.Now.ToString("HH:mm");
            Ausgewaehlt = (vorher is null ? null : _alle.FirstOrDefault(o => o.Objektart == vorher.Objektart && o.RecordId == vorher.RecordId))
                ?? _alle.FirstOrDefault(o => o.AnzahlAenderungen > 0 || o.Gesperrt)
                ?? _alle.FirstOrDefault();
            AktualisiereGesamt();
            Status = meldung;
        }

        partial void OnSucheChanged(string value) => Objekte?.Refresh();

        partial void OnNurUnterschiedeChanged(bool value) => Zeilen?.Refresh();

        partial void OnAusgewaehltChanged(WebGisVergleichsObjekt? oldValue, WebGisVergleichsObjekt? newValue)
        {
            if (oldValue is not null) oldValue.PropertyChanged -= OnObjektGeaendert;
            if (newValue is not null) newValue.PropertyChanged += OnObjektGeaendert;
            HatAuswahl = newValue is not null;
            if (newValue is null)
            {
                Zeilen = null;
                Titel = _alle.Count == 0 ? "Keine Haltungen oder Schächte im Projekt" : "Kein Objekt gewählt";
                Untertitel = string.Empty;
            }
            else
            {
                var ansicht = CollectionViewSource.GetDefaultView(newValue.Zeilen.ToList());
                ansicht.Filter = x => x is WebGisVergleichsZeile z && (!NurUnterschiede || z.IstUnterschied);
                Zeilen = ansicht;
                Titel = newValue.ArtText + " " + newValue.Name;
            }
            AktualisiereAuswahl();
        }

        private void OnObjektGeaendert(object? sender, PropertyChangedEventArgs e) => AktualisiereAuswahl();

        private void OnGesamtGeaendert(object? sender, PropertyChangedEventArgs e) => AktualisiereGesamt();

        private void AktualisiereAuswahl()
        {
            var o = Ausgewaehlt;
            if (o is null)
            {
                KannSchreiben = false;
                SchreibKnopf = "Nichts zu schreiben";
                return;
            }
            Untertitel = o.Untertitel;
            KannSchreiben = o.Schreibbar;
            SchreibKnopf = o.IstGeschrieben ? "Geschrieben"
                : o.Gesperrt ? "Gesperrt"
                : o.AnzahlAenderungen == 0 ? "Nichts zu schreiben"
                : "Nur " + (o.Objektart == WebGisObjektart.Haltung ? "diese Haltung" : "diesen Schacht") + " ins WebGIS schreiben …";
        }

        private void AktualisiereGesamt()
        {
            var mitAenderung = _alle.Count(o => o.Schreibbar);
            Zaehler = _alle.Count + " Objekte · " + mitAenderung + " mit Änderungen · " + _alle.Count(o => o.Gesperrt) + " gesperrt";
            KannAlleSchreiben = mitAenderung > 0;
        }
    }
}
