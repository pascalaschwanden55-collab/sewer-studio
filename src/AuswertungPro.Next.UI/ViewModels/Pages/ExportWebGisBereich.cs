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

    /// <summary>Für welches Projekt <see cref="_bestaetigterPlan"/> gilt (WG04).</summary>
    private WebGisProjektBindung? _bestaetigteBindung;

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

    /// <summary>
    /// Plan aus dem aktuellen Projektstand + frischem WebGIS-Stand; schreibt nichts. Das Projekt wird VOR dem
    /// Lesen gebunden: Ist nach der Netzwartezeit ein anderes Projekt offen, wird die Vorschau verworfen (WG04).
    /// </summary>
    private async Task<WebGisUebersicht?> PruefenAsync()
    {
        if (_d is null || _d.Shell.Project is null || !Angemeldet) return null;
        var bindung = new WebGisProjektBindung(_d.Shell.Project, _d.Settings.LastProjectPath);
        _laeuft = true; Aktualisiere();
        try
        {
            Status = "Lese den WebGIS-Stand je Objekt …";
            var plan = await _d.Export().BauePlanAsync(bindung.Projekt);
            if (!GiltNoch(bindung))
            {
                _bestaetigterPlan = null; _bestaetigteBindung = null;
                Status = "Das Projekt wurde während der Prüfung gewechselt — Vorschau verworfen.";
                return null;
            }
            _bestaetigterPlan = plan; _bestaetigteBindung = bindung;
            SchreibeBericht(Ablageordner(bindung), WebGisExportBericht.Details(plan, mitErgebnis: false), "Vorschau");
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
    /// WebGIS), wird nichts geschrieben und die neue Vorschau gezeigt.
    /// Seit 28.09.2026 (WG03/WG04): Projekt und Berichtsordner werden VOR der frischen Pruefung gebunden und die
    /// Projektsperre schon dann erworben; ohne geschriebene Startzeile im Log geht kein Aufruf ans WebGIS, und
    /// faellt das Log waehrend des Laufs aus, stoppt er vor dem naechsten Schreiben (<see cref="WebGisSendenAblauf"/>).
    /// </summary>
    private async Task<WebGisUebersicht?> SchreibenAsync()
    {
        if (_d is null || _d.Shell.Project is null || !Angemeldet) return null;
        var bindung = new WebGisProjektBindung(_d.Shell.Project, _d.Settings.LastProjectPath);
        // Ordner VOR dem Lauf binden: Ein Projektwechsel darf das Log nicht in ein anderes Projekt verlegen.
        var ordner = Ablageordner(bindung);
        // Ohne Ablage kein Beleg: Ins WebGIS wird nur geschrieben, wenn das Log entstehen kann.
        if (ordner is null)
        {
            Status = "Kein sicherer Berichtsordner beim Projekt (__WebGIS_Export) — nichts geschrieben. "
                + "Projekt speichern und erneut versuchen.";
            return null;
        }
        // Pruefung 22.09.2026, C3 / WG04: Die Sperre gilt schon waehrend der frischen Pruefung — sonst koennte das
        // Projekt zwischen Pruefen und Schreiben gewechselt werden.
        if (!_d.BeginneProjektvorgang())
        {
            Status = "Es läuft bereits ein anderer Projektvorgang — nichts geschrieben.";
            return null;
        }

        _laeuft = true; Aktualisiere();
        try
        {
            Status = "Prüfe nochmals frisch …";
            var useCase = _d.Export();
            var bestaetigt = _bestaetigteBindung is not null && ReferenceEquals(_bestaetigteBindung.Projekt, bindung.Projekt)
                ? _bestaetigterPlan
                : null;
            var plan = await useCase.BaueFrischenPlanAsync(bindung.Projekt, bestaetigt);
            if (!GiltNoch(bindung))
            {
                _bestaetigterPlan = null; _bestaetigteBindung = null;
                Status = "Das Projekt wurde gewechselt — nichts geschrieben.";
                return null;
            }
            if (plan.NichtsZuSchreiben)
            {
                Status = "Nichts zu übertragen.";
                _bestaetigterPlan = plan; _bestaetigteBindung = bindung;
                return WebGisUebersicht.Aus(plan);
            }

            if (bestaetigt is null || !WebGisPlanVergleich.Gleich(bestaetigt, plan))
            {
                _bestaetigterPlan = plan; _bestaetigteBindung = bindung;
                SchreibeBericht(ordner, WebGisExportBericht.Details(plan, mitErgebnis: false), "Vorschau");
                var neu = WebGisUebersicht.Aus(plan);
                neu.Sammelmeldungen.Insert(0,
                    "Der Stand hat sich seit der letzten Prüfung geändert — nichts geschrieben. "
                    + "Bitte diese Vorschau prüfen und dann erneut «Jetzt schreiben».");
                Status = "Stand geändert — nichts geschrieben, bitte Vorschau prüfen.";
                return neu;
            }

            var log = new WebGisLaufLog(ordner);
            Status = "Schreibe ins WebGIS …";
            WebGisSendenAblauf.Ausgang ausgang;
            try
            {
                ausgang = await WebGisSendenAblauf.FuehreAusAsync(
                    useCase, plan, log, _d.Zugang()?.SynLogin ?? "", () => GiltNoch(bindung));
            }
            catch (Exception)
            {
                // Auch nach einem Abbruch: Was bis dahin geschrieben ist, muss belegt sein.
                SchreibeBericht(ordner, WebGisExportBericht.Details(plan, mitErgebnis: true), "Ergebnis-abgebrochen");
                _bestaetigterPlan = null; _bestaetigteBindung = null;
                throw;
            }
            _bestaetigterPlan = null; _bestaetigteBindung = null; // nach dem Schreiben braucht es einen neuen Plan

            var logPfad = Path.Combine(ordner, WebGisBerichtAblage.LogDatei);
            if (ausgang == WebGisSendenAblauf.Ausgang.KeinStartbeleg)
            {
                Status = "Das Änderungslog ist nicht schreibbar (" + logPfad + ") — nichts ins WebGIS geschrieben.";
                _d.Toasts.Warning(Status);
                return WebGisUebersicht.Aus(plan);
            }

            var ergebnis = WebGisExportBericht.Ergebnis(plan);
            var zusatz = ausgang switch
            {
                WebGisSendenAblauf.Ausgang.LogAusgefallen =>
                    "\nACHTUNG: Das Änderungslog fiel während des Laufs aus — Lauf gestoppt. Bereits bestätigte "
                    + "Änderungen stehen im Bericht; vor einem neuen Versuch im WebGIS nachsehen.",
                WebGisSendenAblauf.Ausgang.ProjektGewechselt =>
                    "\nACHTUNG: Das Projekt war nicht mehr offen — Lauf vor dem nächsten Schreiben gestoppt.",
                _ => string.Empty,
            };
            var pfad = SchreibeBericht(ordner, WebGisExportBericht.Details(plan, mitErgebnis: true),
                ausgang == WebGisSendenAblauf.Ausgang.Abgeschlossen ? "Ergebnis" : "Ergebnis-abgebrochen");
            _d.MeldeErgebnis(ergebnis + zusatz + $"\nLog: {logPfad} (Lauf {log.LaufId})"
                + (pfad is null ? "" : $"\nBericht: {pfad}"));
            Status = ergebnis + zusatz;
            var fehlgeschlagen = ausgang != WebGisSendenAblauf.Ausgang.Abgeschlossen
                || plan.Positionen.Exists(p => p.SchreibFehler is not null)
                || plan.Sanierungen.Exists(s => s.SchreibFehler is not null);
            if (fehlgeschlagen) _d.Toasts.Warning(ergebnis + zusatz); else _d.Toasts.Success(ergebnis);

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
        finally
        {
            _d.BeendeProjektvorgang();
            _laeuft = false; Aktualisiere();
        }
    }

    /// <summary>Ist noch genau das gebundene Projekt offen (Instanz und Speicherpfad)?</summary>
    private bool GiltNoch(WebGisProjektBindung bindung)
        => _d is not null && bindung.Gilt(_d.Shell.Project, _d.Settings.LastProjectPath);

    // Berichte und Log in <Projektordner>\__WebGIS_Export, hinter der Schreibgrenze des Projekts (Pruefung 22.09.2026,
    // C3). Eine Stelle fuer Senden und Holen: WebGisBerichtAblage.
    private static string? Ablageordner(WebGisProjektBindung bindung) => WebGisBerichtAblage.Ordner(bindung.Pfad);
    private static string? SchreibeBericht(string? ordner, string text, string art) => WebGisBerichtAblage.SchreibeBericht(ordner, art, text);
}
