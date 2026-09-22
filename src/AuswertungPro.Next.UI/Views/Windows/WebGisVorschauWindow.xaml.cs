using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using System.Windows;
using AuswertungPro.Next.Application.WebGis;
using CommunityToolkit.Mvvm.ComponentModel;

namespace AuswertungPro.Next.UI.Views.Windows;

/// <summary>
/// Nicht-modales Pruef- und Schreibfenster fuer die WebGIS-Uebertragung. Es bleibt offen,
/// waehrend der Bearbeiter in Haltungen/Schaechten korrigiert. Das Fenster orchestriert nur:
/// "Neu pruefen" und "Jetzt schreiben" rufen die vom ViewModel geliehenen Ablaeufe auf und
/// zeigen deren <see cref="WebGisUebersicht"/>; entschieden wird im UseCase.
/// </summary>
public partial class WebGisVorschauWindow : Window
{
    private readonly Func<Task<WebGisUebersicht?>> _pruefen;
    private readonly Func<Task<WebGisUebersicht?>> _schreiben;
    private readonly Anzeige _anzeige = new();

    public WebGisVorschauWindow(Func<Task<WebGisUebersicht?>> pruefen, Func<Task<WebGisUebersicht?>> schreiben)
    {
        _pruefen = pruefen ?? throw new ArgumentNullException(nameof(pruefen));
        _schreiben = schreiben ?? throw new ArgumentNullException(nameof(schreiben));
        InitializeComponent();
        DataContext = _anzeige;
    }

    /// <summary>Erstes Pruefen nach dem Oeffnen.</summary>
    public Task PruefeAsync() => LaufAsync(_pruefen, "Lese den WebGIS-Stand je Objekt …");

    private async void OnNeuPruefen(object sender, RoutedEventArgs e) => await PruefeAsync();

    private async void OnSchreiben(object sender, RoutedEventArgs e)
        => await LaufAsync(_schreiben, "Prüfe nochmals frisch und schreibe ins WebGIS …");

    private void OnSchliessen(object sender, RoutedEventArgs e) => Close();

    private async Task LaufAsync(Func<Task<WebGisUebersicht?>> ablauf, string status)
    {
        if (!_anzeige.Bereit) return;
        _anzeige.Bereit = false;
        _anzeige.Status = status;
        try
        {
            var u = await ablauf();
            if (u is not null) _anzeige.Zeige(u);
            else _anzeige.Status = "Abgebrochen oder nicht angemeldet — nichts geschrieben.";
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

    private void OnClosing(object? sender, System.ComponentModel.CancelEventArgs e)
    {
        if (!_anzeige.Bereit) e.Cancel = true; // waehrend Lesen/Schreiben nicht schliessen
    }

    protected override void OnSourceInitialized(EventArgs e)
    {
        base.OnSourceInitialized(e);
        Closing += OnClosing;
    }

    /// <summary>Eine Zahl mit Beschriftung im Kopf des Fensters.</summary>
    public sealed record Kachel(string Zahl, string Beschriftung);

    public sealed partial class Anzeige : ObservableObject
    {
        [ObservableProperty] private string _titel = "WebGIS-Übertragung prüfen";
        [ObservableProperty] private IReadOnlyList<Kachel> _kacheln = Array.Empty<Kachel>();
        [ObservableProperty] private IReadOnlyList<WebGisUebersichtObjekt> _objekte = Array.Empty<WebGisUebersichtObjekt>();
        [ObservableProperty] private IReadOnlyList<string> _sammelmeldungen = Array.Empty<string>();
        [ObservableProperty] private bool _hatSammelmeldungen;
        [ObservableProperty] private bool _leer;
        [ObservableProperty] private string _bericht = "";
        [ObservableProperty] private bool _berichtOffen;
        [ObservableProperty] private bool _bereit = true;
        [ObservableProperty] private string _status = "";

        public void Zeige(WebGisUebersicht u)
        {
            Titel = u.Titel;
            Kacheln = u.IstErgebnis
                ? new[]
                {
                    new Kachel(u.Geschrieben.ToString(), "Objekte geschrieben"),
                    new Kachel(u.NeueMassnahmen.ToString(), "Sanierungsmassnahmen angelegt"),
                    new Kachel(u.Gesperrt.ToString(), "nicht geschrieben"),
                    new Kachel(u.MitHinweis.ToString(), "nur Hinweis"),
                }
                : new[]
                {
                    new Kachel(u.ObjekteMitAenderung.ToString(), "Objekte mit Änderung"),
                    new Kachel(u.NeueMassnahmen.ToString(), "neue Sanierungsmassnahmen"),
                    new Kachel(u.Gesperrt.ToString(), "gesperrt"),
                    new Kachel(u.MitHinweis.ToString(), "nur Hinweis"),
                };
            Objekte = u.Objekte;
            Sammelmeldungen = u.Sammelmeldungen;
            HatSammelmeldungen = u.HatSammelmeldungen;
            Leer = u.Objekte.Count == 0;
            Bericht = u.Bericht;
            Status = u.NichtsZuTun ? "Nichts zu übertragen." : u.Kopfzeile;
        }
    }
}
