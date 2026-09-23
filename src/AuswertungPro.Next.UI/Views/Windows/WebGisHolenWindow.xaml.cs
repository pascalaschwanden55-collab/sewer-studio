using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using AuswertungPro.Next.Application.WebGis;
using CommunityToolkit.Mvvm.ComponentModel;

namespace AuswertungPro.Next.UI.Views.Windows;

/// <summary>
/// Nicht-modales Fenster fuer «Vom WebGIS holen» (Wunsch Pascal 23.09.2026). Es orchestriert nur:
/// «Neu pruefen» und «Uebernehmen» rufen die geliehenen Ablaeufe auf; Doppelklick auf eine Zeile
/// oeffnet Haltung oder Schacht, das Fenster bleibt offen.
/// </summary>
public partial class WebGisHolenWindow : Window
{
    private readonly Func<Task<WebGisImportPlan?>> _pruefen;
    private readonly Func<WebGisImportPlan, string> _uebernehmen;
    private readonly Action<WebGisObjektart, Guid> _oeffnen;
    private readonly Anzeige _anzeige = new();
    private WebGisImportPlan? _plan;

    public WebGisHolenWindow(Func<Task<WebGisImportPlan?>> pruefen, Func<WebGisImportPlan, string> uebernehmen,
        Action<WebGisObjektart, Guid> oeffnen)
    {
        _pruefen = pruefen ?? throw new ArgumentNullException(nameof(pruefen));
        _uebernehmen = uebernehmen ?? throw new ArgumentNullException(nameof(uebernehmen));
        _oeffnen = oeffnen ?? throw new ArgumentNullException(nameof(oeffnen));
        InitializeComponent();
        DataContext = _anzeige;
        Closing += (_, e) => { if (!_anzeige.Bereit) e.Cancel = true; }; // waehrend des Lesens nicht schliessen
    }

    /// <summary>Liest den WebGIS-Stand und zeigt den Plan.</summary>
    public async Task PruefeAsync()
    {
        if (!_anzeige.Bereit) return;
        _anzeige.Bereit = false;
        _anzeige.Status = "Lese aus dem WebGIS … (je Objekt einzeln, das dauert einige Minuten)";
        try
        {
            var plan = await _pruefen();
            if (plan is null) { _anzeige.Status = "Nicht angemeldet oder kein Projekt offen."; return; }
            _plan = plan;
            _anzeige.Zeige(plan);
        }
        catch (WebGisSitzungException ex)
        {
            _anzeige.Status = "WebGIS-Sitzung abgelaufen — auf der Seite «Export» abmelden und neu anmelden. (" + ex.Message + ")";
        }
        catch (Exception ex)
        {
            _anzeige.Status = "Fehler beim Lesen: " + ex.Message;
        }
        finally
        {
            _anzeige.Bereit = true;
        }
    }

    private async void OnNeuPruefen(object sender, RoutedEventArgs e) => await PruefeAsync();

    private async void OnUebernehmen(object sender, RoutedEventArgs e)
    {
        if (_plan is null || !_anzeige.Bereit) return;
        var meldung = _uebernehmen(_plan);
        _plan = null;
        _anzeige.LeereZeilen();
        await PruefeAsync();
        _anzeige.Status = meldung + " Der Stand oben ist frisch geprüft.";
    }

    private void OnSchliessen(object sender, RoutedEventArgs e) => Close();

    private void OnZeileDoppelklick(object sender, System.Windows.Input.MouseButtonEventArgs e)
    {
        if (sender is not DataGrid { SelectedItem: WebGisHolenZeile z } || z.RecordId == Guid.Empty) return;
        _oeffnen(z.Objektart, z.RecordId);
        _anzeige.Status = $"{z.Objekt} geöffnet — nach einer Korrektur «Neu prüfen».";
    }

    public sealed partial class Anzeige : ObservableObject
    {
        [ObservableProperty] private string _zusammenfassung = "Noch nicht gelesen.";
        [ObservableProperty] [NotifyPropertyChangedFor(nameof(AllesZugeordnet))] private bool _warnung;
        [ObservableProperty] private string _warntext = "";
        [ObservableProperty] private IReadOnlyList<WebGisHolenZeile> _zeilen = Array.Empty<WebGisHolenZeile>();
        [ObservableProperty] private IReadOnlyList<WebGisHolenZeile> _nichtZugeordnet = Array.Empty<WebGisHolenZeile>();
        [ObservableProperty] private string _nichtZugeordnetTitel = "";
        [ObservableProperty] private bool _hatNichtZugeordnet;
        [ObservableProperty] private string _bericht = "";
        [ObservableProperty] private string _status = "";
        [ObservableProperty] [NotifyPropertyChangedFor(nameof(KannUebernehmen))] private bool _bereit = true;
        [ObservableProperty] [NotifyPropertyChangedFor(nameof(KannUebernehmen))] private bool _hatZeilen;

        public bool KannUebernehmen => Bereit && HatZeilen;

        /// <summary>Gruenes Band nur, wenn nichts gesperrt und nichts ohne Zuordnung ist.</summary>
        public bool AllesZugeordnet => !Warnung;

        public void Zeige(WebGisImportPlan plan)
        {
            var kopf = WebGisImportBericht.Kopf(plan);
            Warnung = kopf.Warnung;
            Warntext = kopf.Warntext;
            Zusammenfassung = kopf.Zusammenfassung;
            Zeilen = WebGisImportBericht.Zeilen(plan);
            HatZeilen = Zeilen.Count > 0;
            NichtZugeordnet = WebGisImportBericht.NichtZugeordnet(plan);
            HatNichtZugeordnet = NichtZugeordnet.Count > 0;
            NichtZugeordnetTitel = $"Nicht zugeordnet — wird nicht übernommen ({NichtZugeordnet.Count})";
            Bericht = WebGisImportBericht.Details(plan);
            Status = HatZeilen ? "Doppelklick auf eine Zeile öffnet das Objekt." : "Nichts zu übernehmen — SewerStudio ist auf dem Stand des WebGIS.";
        }

        public void LeereZeilen()
        {
            Zeilen = Array.Empty<WebGisHolenZeile>();
            HatZeilen = false;
            NichtZugeordnet = Array.Empty<WebGisHolenZeile>();
            HatNichtZugeordnet = false;
        }
    }
}
