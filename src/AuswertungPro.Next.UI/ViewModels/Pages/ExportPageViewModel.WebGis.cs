using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using AuswertungPro.Next.Application.WebGis;
using AuswertungPro.Next.Infrastructure.WebGis;
using CommunityToolkit.Mvvm.Input;

namespace AuswertungPro.Next.UI.ViewModels.Pages;

/// <summary>
/// WebGIS direkt: Anmelden im sichtbaren Browser, dann Plan bauen, Vorschau bestaetigen,
/// schreiben, Bericht in die Projektablage. Der Ablauf liegt in
/// <see cref="WebGisExportUseCase"/>; hier wird nur verdrahtet, was die Oberflaeche leiht.
/// Nur im Vollkonstruktor (mit ServiceProvider) aktiv; sonst bleiben die Knoepfe aus.
/// </summary>
public sealed partial class ExportPageViewModel
{
    private ServiceProvider? _webGisSp;
    private string _webGisStatus = "Nicht angemeldet.";
    private bool _webGisLaeuft;

    public IAsyncRelayCommand WebGisAnmeldenCommand { get; private set; } = new AsyncRelayCommand(() => Task.CompletedTask, () => false);
    public IAsyncRelayCommand WebGisUebertragenCommand { get; private set; } = new AsyncRelayCommand(() => Task.CompletedTask, () => false);
    public IAsyncRelayCommand WebGisAbmeldenCommand { get; private set; } = new AsyncRelayCommand(() => Task.CompletedTask, () => false);
    public IAsyncRelayCommand WebGisHolenCommand { get; private set; } = new AsyncRelayCommand(() => Task.CompletedTask, () => false);

    /// <summary>Angemeldet als …, Fortschritt oder letztes Ergebnis.</summary>
    public string WebGisStatus
    {
        get => _webGisStatus;
        private set => SetProperty(ref _webGisStatus, value);
    }

    public bool WebGisAngemeldet => _webGisSp?.WebGisZugang is not null;

    private void InitialisiereWebGis(ServiceProvider sp)
    {
        _webGisSp = sp;
        WebGisAnmeldenCommand = new AsyncRelayCommand(WebGisAnmeldenAsync, () => !_webGisLaeuft && !WebGisAngemeldet);
        WebGisUebertragenCommand = new AsyncRelayCommand(
            WebGisUebertragenAsync,
            () => !_webGisLaeuft && WebGisAngemeldet && _shell.Project is not null);
        WebGisAbmeldenCommand = new AsyncRelayCommand(WebGisAbmeldenAsync, () => !_webGisLaeuft && WebGisAngemeldet);
        WebGisHolenCommand = new AsyncRelayCommand(
            WebGisHolenAsync,
            () => !_webGisLaeuft && WebGisAngemeldet && _shell.Project is not null);
        var kontext = sp.Settings.WebGisSynLogin;
        WebGisStatus = string.IsNullOrWhiteSpace(kontext)
            ? "Nicht angemeldet. Beim ersten Mal nach der Anmeldung einmal eine Attributmaske öffnen."
            : $"Nicht angemeldet (zuletzt {kontext}).";
    }

    private void WebGisAktualisiere()
    {
        OnPropertyChanged(nameof(WebGisAngemeldet));
        WebGisAnmeldenCommand.NotifyCanExecuteChanged();
        WebGisUebertragenCommand.NotifyCanExecuteChanged();
        WebGisAbmeldenCommand.NotifyCanExecuteChanged();
        WebGisHolenCommand.NotifyCanExecuteChanged();
    }

    /// <summary>
    /// Vom WebGIS holen: Plan bauen (liest nur), Vorschau im bestehenden Fenster, erst nach
    /// «Uebernehmen» in die Datensaetze. Das Projekt wird beim Start gebunden — nach einem
    /// Projektwechsel waehrend des Lesens wird nichts uebernommen.
    /// </summary>
    private async Task WebGisHolenAsync()
    {
        if (_webGisSp is null || _shell.Project is null || !WebGisAngemeldet) return;
        await _webGisSp.WebGisHolen.OeffneAsync(_shell, () => _settings.LastProjectPath, () => { });
    }

    private async Task WebGisAnmeldenAsync()
    {
        if (_webGisSp is null) return;
        var sp = _webGisSp;
        var s = sp.Settings;
        var bekannt = string.IsNullOrWhiteSpace(s.WebGisSynLogin) || string.IsNullOrWhiteSpace(s.WebGisSynGroups)
            ? null
            : new WebGisSynKontext(s.WebGisSynLogin, string.IsNullOrWhiteSpace(s.WebGisSynRoles) ? "WebOffice+-+Editing" : s.WebGisSynRoles, s.WebGisSynGroups);

        _webGisLaeuft = true;
        WebGisAktualisiere();
        try
        {
            var fortschritt = new Progress<string>(t => WebGisStatus = t);
            var zugang = await sp.WebGisAnmeldung.AnmeldenAsync(
                s.WebGisBasisUrl, s.WebGisProjekt, s.WebGisDatenquelle, bekannt, fortschritt);
            if (zugang is null)
            {
                WebGisStatus = "Anmeldung abgebrochen (Browser geschlossen oder Zeit abgelaufen).";
                return;
            }
            sp.WebGisZugang = zugang;
            if (sp.WebGisAnmeldung.SynKontext is { } k)
            {
                s.WebGisSynLogin = k.Login; s.WebGisSynRoles = k.Roles; s.WebGisSynGroups = k.Groups;
            }
            WebGisStatus = $"Angemeldet als {zugang.SynLogin}. Der Browser bleibt offen, solange übertragen wird.";
        }
        catch (Exception ex)
        {
            WebGisStatus = "Anmeldung fehlgeschlagen: "
                + AuswertungPro.Next.Application.Common.UserError.DescribeAndReport(ex, "WebGIS anmelden");
            _dialogs.Error(WebGisStatus, "WebGIS");
        }
        finally
        {
            _webGisLaeuft = false;
            WebGisAktualisiere();
        }
    }

    private async Task WebGisAbmeldenAsync()
    {
        if (_webGisSp is null) return;
        if (_webGisFenster is { IsLoaded: true }) _webGisFenster.Close();
        await _webGisSp.WebGisAnmeldung.SchliessenAsync();
        _webGisSp.WebGisZugang = null;
        WebGisStatus = "Abgemeldet.";
        WebGisAktualisiere();
    }

    private Views.Windows.WebGisVorschauWindow? _webGisFenster;

    /// <summary>Der Plan, den das Prueffenster zuletzt gezeigt hat — nur genau der darf geschrieben werden.</summary>
    private WebGisExportPlan? _webGisBestaetigterPlan;

    /// <summary>
    /// Oeffnet das nicht-modale Pruef-/Schreibfenster (oder holt es nach vorn) und prueft.
    /// Das Fenster bleibt offen, waehrend in Haltungen/Schaechten korrigiert wird; "Neu pruefen"
    /// baut den Plan aus dem aktuellen Projektstand, "Jetzt schreiben" prueft erst frisch und
    /// schreibt dann. Berichte gehen in die Projektablage.
    /// </summary>
    private async Task WebGisUebertragenAsync()
    {
        if (_webGisSp is null || _shell.Project is null) return;
        if (_webGisFenster is { IsLoaded: true })
        {
            _webGisFenster.Activate();
            await _webGisFenster.PruefeAsync();
            return;
        }

        var fenster = new Views.Windows.WebGisVorschauWindow(WebGisPruefenAsync, WebGisSchreibenAsync, WebGisOeffne);
        var besitzer = System.Windows.Application.Current?.MainWindow;
        if (besitzer is not null && besitzer.IsLoaded) fenster.Owner = besitzer;
        fenster.Closed += (_, _) => { _webGisFenster = null; WebGisAktualisiere(); };
        _webGisFenster = fenster;
        fenster.Show();
        await fenster.PruefeAsync();
    }

    /// <summary>
    /// Springt aus dem Prueffenster in die Bearbeitung des Objekts — denselben Weg, den auch
    /// Dossier und Karte nehmen. Der Datensatz wird im AKTUELLEN Projekt gesucht: Nach einem
    /// Projektwechsel gehoert die Id nicht mehr hierher, dann geschieht nichts.
    /// </summary>
    private void WebGisOeffne(WebGisObjektart art, Guid recordId)
    {
        var projekt = _shell.Project;
        if (projekt is null || recordId == Guid.Empty) return;
        if (art == WebGisObjektart.Haltung)
            _shell.NavigateToHolding(projekt.Data.FirstOrDefault(h => h.Id == recordId));
        else
            _shell.NavigateToShaft(projekt.SchaechteData.FirstOrDefault(s => s.Id == recordId));
    }

    /// <summary>Plan aus dem aktuellen Projektstand + frischem WebGIS-Stand; schreibt nichts.</summary>
    private async Task<WebGisUebersicht?> WebGisPruefenAsync()
    {
        if (_webGisSp is null || _shell.Project is null || !WebGisAngemeldet) return null;
        _webGisLaeuft = true; WebGisAktualisiere();
        try
        {
            WebGisStatus = "Lese den WebGIS-Stand je Objekt …";
            var plan = await _webGisSp.WebGisExport.BauePlanAsync(_shell.Project);
            _webGisBestaetigterPlan = plan;
            SchreibeBericht(WebGisAblageordner(), WebGisExportBericht.Details(plan, mitErgebnis: false), "Vorschau");
            var uebersicht = WebGisUebersicht.Aus(plan);
            WebGisStatus = uebersicht.Kopfzeile;
            return uebersicht;
        }
        catch (WebGisSitzungException ex)
        {
            WebGisStatus = "WebGIS-Sitzung abgelaufen — bitte abmelden und neu anmelden. (" + ex.Message + ")";
            throw;
        }
        finally { _webGisLaeuft = false; WebGisAktualisiere(); }
    }

    /// <summary>
    /// Frisch pruefen und NUR den bestaetigten Plan schreiben: Weicht der frische Plan von dem
    /// ab, den das Fenster zuletzt gezeigt hat (Korrektur in Haltungen/Schaechten, Aenderung im
    /// WebGIS), wird nichts geschrieben und die neue Vorschau gezeigt. Das Log entsteht je
    /// Objekt, Bericht und Log auch nach einem Abbruch.
    /// </summary>
    private async Task<WebGisUebersicht?> WebGisSchreibenAsync()
    {
        if (_webGisSp is null || _shell.Project is null || !WebGisAngemeldet) return null;
        _webGisLaeuft = true; WebGisAktualisiere();
        try
        {
            WebGisStatus = "Prüfe nochmals frisch …";
            var useCase = _webGisSp.WebGisExport;
            var plan = await useCase.BaueFrischenPlanAsync(_shell.Project, _webGisBestaetigterPlan);
            if (plan.NichtsZuSchreiben)
            {
                WebGisStatus = "Nichts zu übertragen.";
                _webGisBestaetigterPlan = plan;
                return WebGisUebersicht.Aus(plan);
            }

            // Ordner und Benutzer VOR dem Lauf binden: Ein Projektwechsel waehrenddessen darf
            // das Log nicht in ein anderes Projekt verlegen.
            var ordner = WebGisAblageordner();
            if (_webGisBestaetigterPlan is null || !WebGisPlanVergleich.Gleich(_webGisBestaetigterPlan, plan))
            {
                _webGisBestaetigterPlan = plan;
                SchreibeBericht(ordner, WebGisExportBericht.Details(plan, mitErgebnis: false), "Vorschau");
                var neu = WebGisUebersicht.Aus(plan);
                neu.Sammelmeldungen.Insert(0,
                    "Der Stand hat sich seit der letzten Prüfung geändert — nichts geschrieben. "
                    + "Bitte diese Vorschau prüfen und dann erneut «Jetzt schreiben».");
                WebGisStatus = "Stand geändert — nichts geschrieben, bitte Vorschau prüfen.";
                return neu;
            }

            // Ohne Ablage kein Beleg: Ins WebGIS wird nur geschrieben, wenn das Log je Objekt entstehen kann.
            if (ordner is null)
            {
                WebGisStatus = "Kein sicherer Berichtsordner beim Projekt (__WebGIS_Export) — nichts geschrieben. "
                    + "Projekt speichern und erneut versuchen.";
                return WebGisUebersicht.Aus(plan);
            }
            // Pruefung 22.09.2026, C3: Waehrend ins WebGIS geschrieben wird, sind Projektwechsel und Schliessen gesperrt.
            if (!TryBeginProjectOperation(allowsInternalProjectSave: false))
            {
                WebGisStatus = "Es läuft bereits ein anderer Projektvorgang — nichts geschrieben.";
                return WebGisUebersicht.Aus(plan);
            }

            var benutzer = _webGisSp.WebGisZugang?.SynLogin ?? "";
            void Log(string zeile) { if (zeile.Length > 0) HaengeAnLog(ordner, zeile); }

            WebGisStatus = "Schreibe ins WebGIS …";
            Log(WebGisExportBericht.LogStart(DateTime.Now, benutzer, plan));
            try
            {
                await useCase.FuehreAusAsync(plan, probelauf: false,
                    nachObjekt: p => Log(WebGisExportBericht.LogZeile(p, DateTime.Now)),
                    nachMassnahme: s => Log(WebGisExportBericht.LogZeile(s, DateTime.Now)));
            }
            catch (Exception ex)
            {
                // Auch nach einem Abbruch: Was bis dahin geschrieben ist, muss belegt sein.
                Log(WebGisExportBericht.LogAbbruch(DateTime.Now, ex.Message, plan));
                SchreibeBericht(ordner, WebGisExportBericht.Details(plan, mitErgebnis: true), "Ergebnis-abgebrochen");
                _webGisBestaetigterPlan = null;
                throw;
            }
            finally
            {
                EndProjectOperation();
            }
            Log(WebGisExportBericht.LogAbschluss(DateTime.Now, plan));
            _webGisBestaetigterPlan = null; // nach dem Schreiben braucht es einen neuen, gepruefeten Plan

            var ergebnis = WebGisExportBericht.Ergebnis(plan);
            var pfad = SchreibeBericht(ordner, WebGisExportBericht.Details(plan, mitErgebnis: true), "Ergebnis");
            var logPfad = ordner is null ? null : Path.Combine(ordner, "WebGIS_Log.txt");
            LastResult = ergebnis + (logPfad is null ? "" : $"\nLog: {logPfad}") + (pfad is null ? "" : $"\nBericht: {pfad}");
            WebGisStatus = ergebnis;
            var fehlgeschlagen = plan.Positionen.Exists(p => p.SchreibFehler is not null)
                || plan.Sanierungen.Exists(s => s.SchreibFehler is not null);
            if (fehlgeschlagen) _toasts.Warning(ergebnis); else _toasts.Success(ergebnis);

            // Ergebnis im selben Fenster: dieselben Karten, aber als "geschrieben" markiert.
            return WebGisUebersicht.Aus(plan, ergebnis: true);
        }
        catch (WebGisSitzungException ex)
        {
            WebGisStatus = "WebGIS-Sitzung abgelaufen — bitte abmelden und neu anmelden. (" + ex.Message + ")";
            throw;
        }
        catch (Exception ex)
        {
            WebGisStatus = "Übertragung fehlgeschlagen: "
                + AuswertungPro.Next.Application.Common.UserError.DescribeAndReport(ex, "WebGIS übertragen");
            throw;
        }
        finally { _webGisLaeuft = false; WebGisAktualisiere(); }
    }

    // Berichte und Log in <Projektordner>\__WebGIS_Export, hinter der Schreibgrenze des Projekts (Pruefung 22.09.2026,
    // C3). Eine Stelle fuer Senden und Holen: WebGisBerichtAblage.
    private static void HaengeAnLog(string? ordner, string text) => WebGisBerichtAblage.HaengeAnLog(ordner, text);
    private string? WebGisAblageordner() => WebGisBerichtAblage.Ordner(_settings.LastProjectPath);
    private static string? SchreibeBericht(string? ordner, string text, string art) => WebGisBerichtAblage.SchreibeBericht(ordner, art, text);
}
