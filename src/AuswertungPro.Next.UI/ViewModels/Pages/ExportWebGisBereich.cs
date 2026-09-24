using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using AuswertungPro.Next.Application.WebGis;
using AuswertungPro.Next.Infrastructure.WebGis;
using AuswertungPro.Next.UI.Services;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace AuswertungPro.Next.UI.ViewModels.Pages;

/// <summary>
/// WebGIS direkt auf der Export-Seite: Anmelden im sichtbaren Browser, dann Plan bauen, Vorschau bestaetigen,
/// schreiben, Bericht in die Projektablage. Der Ablauf liegt in <see cref="WebGisExportUseCase"/>; hier wird nur
/// verdrahtet, was die Oberflaeche leiht. Ohne <see cref="Dienste"/> (<see cref="Inaktiv"/>) bleiben die Knoepfe aus.
/// Eigene Klasse seit 24.09.2026: Sie bekommt nur die Dienste, die sie wirklich braucht, statt des ganzen
/// ServiceProviders, und die Export-Seite bleibt unter der Zeilengrenze (Waechter
/// ExportPageViewModelDependencyTests, ArchitectureDriftRatchetTests, MaintainabilityFitnessTests).
/// </summary>
public sealed class ExportWebGisBereich : ObservableObject
{
    /// <summary>Was der Bereich von der Export-Seite und vom Dienstcontainer leiht.</summary>
    public sealed record Dienste(
        ShellViewModel Shell,
        AppSettings Settings,
        IDialogService Dialogs,
        IToastService Toasts,
        PlaywrightWebGisAnmeldung Anmeldung,
        Func<WebGisZugang?> Zugang,
        Action<WebGisZugang?> SetzeZugang,
        Func<WebGisExportUseCase> Export,
        WebGisHolenAblauf Holen,
        Func<bool> BeginneProjektvorgang,
        Action BeendeProjektvorgang,
        Action<string> MeldeErgebnis);

    private readonly Dienste? _d;
    private string _status = "Nicht angemeldet.";
    private bool _laeuft;
    private Views.Windows.WebGisVorschauWindow? _fenster;

    /// <summary>Der Plan, den das Prueffenster zuletzt gezeigt hat — nur genau der darf geschrieben werden.</summary>
    private WebGisExportPlan? _bestaetigterPlan;

    public IAsyncRelayCommand AnmeldenCommand { get; }
    public IAsyncRelayCommand UebertragenCommand { get; }
    public IAsyncRelayCommand AbmeldenCommand { get; }
    public IAsyncRelayCommand HolenCommand { get; }

    /// <summary>Angemeldet als …, Fortschritt oder letztes Ergebnis.</summary>
    public string Status
    {
        get => _status;
        private set => SetProperty(ref _status, value);
    }

    public bool Angemeldet => _d?.Zugang() is not null;

    /// <summary>Ohne Dienste: alle Knoepfe aus (Export-Seite ohne ServiceProvider, z. B. in Tests).</summary>
    public static ExportWebGisBereich Inaktiv() => new(null, aktiv: false);

    public ExportWebGisBereich(Dienste dienste) : this(dienste ?? throw new ArgumentNullException(nameof(dienste)), aktiv: true)
    {
    }

    private ExportWebGisBereich(Dienste? dienste, bool aktiv)
    {
        if (!aktiv) dienste = null;
        _d = dienste;
        if (dienste is null)
        {
            AnmeldenCommand = new AsyncRelayCommand(() => Task.CompletedTask, () => false);
            UebertragenCommand = new AsyncRelayCommand(() => Task.CompletedTask, () => false);
            AbmeldenCommand = new AsyncRelayCommand(() => Task.CompletedTask, () => false);
            HolenCommand = new AsyncRelayCommand(() => Task.CompletedTask, () => false);
            return;
        }

        AnmeldenCommand = new AsyncRelayCommand(AnmeldenAsync, () => !_laeuft && !Angemeldet);
        UebertragenCommand = new AsyncRelayCommand(
            UebertragenAsync,
            () => !_laeuft && Angemeldet && dienste.Shell.Project is not null);
        AbmeldenCommand = new AsyncRelayCommand(AbmeldenAsync, () => !_laeuft && Angemeldet);
        HolenCommand = new AsyncRelayCommand(
            HolenAsync,
            () => !_laeuft && Angemeldet && dienste.Shell.Project is not null);
        var kontext = dienste.Settings.WebGisSynLogin;
        Status = string.IsNullOrWhiteSpace(kontext)
            ? "Nicht angemeldet. Beim ersten Mal nach der Anmeldung einmal eine Attributmaske öffnen."
            : $"Nicht angemeldet (zuletzt {kontext}).";
    }

    private void Aktualisiere()
    {
        OnPropertyChanged(nameof(Angemeldet));
        AnmeldenCommand.NotifyCanExecuteChanged();
        UebertragenCommand.NotifyCanExecuteChanged();
        AbmeldenCommand.NotifyCanExecuteChanged();
        HolenCommand.NotifyCanExecuteChanged();
    }

    /// <summary>
    /// Vom WebGIS holen: Plan bauen (liest nur), Vorschau im bestehenden Fenster, erst nach
    /// «Uebernehmen» in die Datensaetze. Das Projekt wird beim Start gebunden — nach einem
    /// Projektwechsel waehrend des Lesens wird nichts uebernommen.
    /// </summary>
    private async Task HolenAsync()
    {
        if (_d is null || _d.Shell.Project is null || !Angemeldet) return;
        await _d.Holen.OeffneAsync(_d.Shell, () => _d.Settings.LastProjectPath, () => { });
    }

    private async Task AnmeldenAsync()
    {
        if (_d is null) return;
        var s = _d.Settings;
        var bekannt = WebGisSynKontext.AusEinstellungen(s.WebGisSynLogin, s.WebGisSynRoles, s.WebGisSynGroups); // geprueft (D2)

        _laeuft = true;
        Aktualisiere();
        try
        {
            var fortschritt = new Progress<string>(t => Status = t);
            var zugang = await _d.Anmeldung.AnmeldenAsync(
                s.WebGisBasisUrl, s.WebGisProjekt, s.WebGisDatenquelle, bekannt, fortschritt);
            if (zugang is null)
            {
                Status = "Anmeldung abgebrochen (Browser geschlossen oder Zeit abgelaufen).";
                return;
            }
            _d.SetzeZugang(zugang);
            if (_d.Anmeldung.SynKontext is { } k)
            {
                s.WebGisSynLogin = k.Login; s.WebGisSynRoles = k.Roles; s.WebGisSynGroups = k.Groups;
            }
            Status = $"Angemeldet als {zugang.SynLogin}. Der Browser bleibt offen, solange übertragen wird.";
        }
        catch (Exception ex)
        {
            Status = "Anmeldung fehlgeschlagen: "
                + AuswertungPro.Next.Application.Common.UserError.DescribeAndReport(ex, "WebGIS anmelden");
            _d.Dialogs.Error(Status, "WebGIS");
        }
        finally
        {
            _laeuft = false;
            Aktualisiere();
        }
    }

    private async Task AbmeldenAsync()
    {
        if (_d is null) return;
        if (_fenster is { IsLoaded: true }) _fenster.Close();
        await _d.Anmeldung.SchliessenAsync();
        _d.SetzeZugang(null);
        Status = "Abgemeldet.";
        Aktualisiere();
    }

    /// <summary>
    /// Oeffnet das nicht-modale Pruef-/Schreibfenster (oder holt es nach vorn) und prueft.
    /// Das Fenster bleibt offen, waehrend in Haltungen/Schaechten korrigiert wird; "Neu pruefen"
    /// baut den Plan aus dem aktuellen Projektstand, "Jetzt schreiben" prueft erst frisch und
    /// schreibt dann. Berichte gehen in die Projektablage.
    /// </summary>
    private async Task UebertragenAsync()
    {
        if (_d is null || _d.Shell.Project is null) return;
        if (_fenster is { IsLoaded: true })
        {
            _fenster.Activate();
            await _fenster.PruefeAsync();
            return;
        }

        var fenster = new Views.Windows.WebGisVorschauWindow(PruefenAsync, SchreibenAsync, Oeffne);
        var besitzer = System.Windows.Application.Current?.MainWindow;
        if (besitzer is not null && besitzer.IsLoaded) fenster.Owner = besitzer;
        fenster.Closed += (_, _) => { _fenster = null; Aktualisiere(); };
        _fenster = fenster;
        fenster.Show();
        await fenster.PruefeAsync();
    }

    /// <summary>
    /// Springt aus dem Prueffenster in die Bearbeitung des Objekts — denselben Weg, den auch
    /// Dossier und Karte nehmen. Der Datensatz wird im AKTUELLEN Projekt gesucht: Nach einem
    /// Projektwechsel gehoert die Id nicht mehr hierher, dann geschieht nichts.
    /// </summary>
    private void Oeffne(WebGisObjektart art, Guid recordId)
    {
        var shell = _d?.Shell;
        var projekt = shell?.Project;
        if (shell is null || projekt is null || recordId == Guid.Empty) return;
        if (art == WebGisObjektart.Haltung)
            shell.NavigateToHolding(projekt.Data.FirstOrDefault(h => h.Id == recordId));
        else
            shell.NavigateToShaft(projekt.SchaechteData.FirstOrDefault(s => s.Id == recordId));
    }

    /// <summary>Plan aus dem aktuellen Projektstand + frischem WebGIS-Stand; schreibt nichts.</summary>
    private async Task<WebGisUebersicht?> PruefenAsync()
    {
        if (_d is null || _d.Shell.Project is null || !Angemeldet) return null;
        _laeuft = true; Aktualisiere();
        try
        {
            Status = "Lese den WebGIS-Stand je Objekt …";
            var plan = await _d.Export().BauePlanAsync(_d.Shell.Project);
            _bestaetigterPlan = plan;
            SchreibeBericht(Ablageordner(), WebGisExportBericht.Details(plan, mitErgebnis: false), "Vorschau");
            var uebersicht = WebGisUebersicht.Aus(plan);
            Status = uebersicht.Kopfzeile;
            return uebersicht;
        }
        catch (WebGisSitzungException ex)
        {
            Status = "WebGIS-Sitzung abgelaufen — bitte abmelden und neu anmelden. (" + ex.Message + ")";
            throw;
        }
        finally { _laeuft = false; Aktualisiere(); }
    }

    /// <summary>
    /// Frisch pruefen und NUR den bestaetigten Plan schreiben: Weicht der frische Plan von dem
    /// ab, den das Fenster zuletzt gezeigt hat (Korrektur in Haltungen/Schaechten, Aenderung im
    /// WebGIS), wird nichts geschrieben und die neue Vorschau gezeigt. Das Log entsteht je
    /// Objekt, Bericht und Log auch nach einem Abbruch.
    /// </summary>
    private async Task<WebGisUebersicht?> SchreibenAsync()
    {
        if (_d is null || _d.Shell.Project is null || !Angemeldet) return null;
        _laeuft = true; Aktualisiere();
        try
        {
            Status = "Prüfe nochmals frisch …";
            var useCase = _d.Export();
            var plan = await useCase.BaueFrischenPlanAsync(_d.Shell.Project, _bestaetigterPlan);
            if (plan.NichtsZuSchreiben)
            {
                Status = "Nichts zu übertragen.";
                _bestaetigterPlan = plan;
                return WebGisUebersicht.Aus(plan);
            }

            // Ordner und Benutzer VOR dem Lauf binden: Ein Projektwechsel waehrenddessen darf
            // das Log nicht in ein anderes Projekt verlegen.
            var ordner = Ablageordner();
            if (_bestaetigterPlan is null || !WebGisPlanVergleich.Gleich(_bestaetigterPlan, plan))
            {
                _bestaetigterPlan = plan;
                SchreibeBericht(ordner, WebGisExportBericht.Details(plan, mitErgebnis: false), "Vorschau");
                var neu = WebGisUebersicht.Aus(plan);
                neu.Sammelmeldungen.Insert(0,
                    "Der Stand hat sich seit der letzten Prüfung geändert — nichts geschrieben. "
                    + "Bitte diese Vorschau prüfen und dann erneut «Jetzt schreiben».");
                Status = "Stand geändert — nichts geschrieben, bitte Vorschau prüfen.";
                return neu;
            }

            // Ohne Ablage kein Beleg: Ins WebGIS wird nur geschrieben, wenn das Log je Objekt entstehen kann.
            if (ordner is null)
            {
                Status = "Kein sicherer Berichtsordner beim Projekt (__WebGIS_Export) — nichts geschrieben. "
                    + "Projekt speichern und erneut versuchen.";
                return WebGisUebersicht.Aus(plan);
            }
            // Pruefung 22.09.2026, C3: Waehrend ins WebGIS geschrieben wird, sind Projektwechsel und Schliessen gesperrt.
            if (!_d.BeginneProjektvorgang())
            {
                Status = "Es läuft bereits ein anderer Projektvorgang — nichts geschrieben.";
                return WebGisUebersicht.Aus(plan);
            }

            var benutzer = _d.Zugang()?.SynLogin ?? "";
            void Log(string zeile) { if (zeile.Length > 0) WebGisBerichtAblage.HaengeAnLog(ordner, zeile); }

            Status = "Schreibe ins WebGIS …";
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
                _bestaetigterPlan = null;
                throw;
            }
            finally
            {
                _d.BeendeProjektvorgang();
            }
            Log(WebGisExportBericht.LogAbschluss(DateTime.Now, plan));
            _bestaetigterPlan = null; // nach dem Schreiben braucht es einen neuen, gepruefeten Plan

            var ergebnis = WebGisExportBericht.Ergebnis(plan);
            var pfad = SchreibeBericht(ordner, WebGisExportBericht.Details(plan, mitErgebnis: true), "Ergebnis");
            var logPfad = Path.Combine(ordner, WebGisBerichtAblage.LogDatei);
            _d.MeldeErgebnis(ergebnis + $"\nLog: {logPfad}" + (pfad is null ? "" : $"\nBericht: {pfad}"));
            Status = ergebnis;
            var fehlgeschlagen = plan.Positionen.Exists(p => p.SchreibFehler is not null)
                || plan.Sanierungen.Exists(s => s.SchreibFehler is not null);
            if (fehlgeschlagen) _d.Toasts.Warning(ergebnis); else _d.Toasts.Success(ergebnis);

            // Ergebnis im selben Fenster: dieselben Karten, aber als "geschrieben" markiert.
            return WebGisUebersicht.Aus(plan, ergebnis: true);
        }
        catch (WebGisSitzungException ex)
        {
            Status = "WebGIS-Sitzung abgelaufen — bitte abmelden und neu anmelden. (" + ex.Message + ")";
            throw;
        }
        catch (Exception ex)
        {
            Status = "Übertragung fehlgeschlagen: "
                + AuswertungPro.Next.Application.Common.UserError.DescribeAndReport(ex, "WebGIS übertragen");
            throw;
        }
        finally { _laeuft = false; Aktualisiere(); }
    }

    // Berichte und Log in <Projektordner>\__WebGIS_Export, hinter der Schreibgrenze des Projekts (Pruefung 22.09.2026,
    // C3). Eine Stelle fuer Senden und Holen: WebGisBerichtAblage.
    private string? Ablageordner() => WebGisBerichtAblage.Ordner(_d?.Settings.LastProjectPath);
    private static string? SchreibeBericht(string? ordner, string text, string art) => WebGisBerichtAblage.SchreibeBericht(ordner, art, text);
}
