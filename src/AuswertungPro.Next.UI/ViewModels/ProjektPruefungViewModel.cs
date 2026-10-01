using AuswertungPro.Next.Application.Common;
using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Threading;
using AuswertungPro.Next.Application.UseCases.ProjektPruefung;
using AuswertungPro.Next.Domain.Models;
using AuswertungPro.Next.UI.ViewModels.Pages;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace AuswertungPro.Next.UI.ViewModels;

/// <summary>Besitzt nur den sichtbaren Prueflauf. Snapshot und Signatur schuetzen Projektwechsel und Zwischenkorrekturen.</summary>
public sealed partial class ProjektPruefungViewModel : ObservableObject, IDisposable
{
    private readonly IProjektPruefung _dienst;
    private readonly Func<(Project Projekt, string? Pfad)> _aktuell;
    private readonly Func<bool> _bereit;
    private readonly Action<ProjektPruefpunkt> _oeffnen;
    private readonly Func<ProjektPruefpunkt, Func<bool>, Action<ProjektPruefungViewModel>, CancellationToken, Task>? _oeffnenAsync;
    private readonly Func<Project, CancellationToken, Task<Project>> _erfassen;
    private Project? _geprueft;
    private Project? _stand;
    private string? _pfad;
    private bool _disposed;
    [ObservableProperty] private IReadOnlyList<ProjektPruefpunkt> _punkte = [];
    [ObservableProperty] private string _meldung = "Noch nicht geprüft. Die Prüfung verändert keine Daten.";
    [ObservableProperty] private bool _istAktuell;
    [ObservableProperty] private ProjektPruefpunkt? _fokusPunkt;

    public ProjektPruefungViewModel(IProjektPruefung dienst, Func<(Project, string?)> aktuell,
        Func<Project, Project> kopie, Func<Project, string> signatur, Func<bool> bereit, Action<ProjektPruefpunkt> oeffnen,
        Func<ProjektPruefpunkt, Func<bool>, Action<ProjektPruefungViewModel>, CancellationToken, Task>? oeffnenAsync = null,
        Func<Project, CancellationToken, Task<Project>>? erfassen = null)
    {
        _dienst = dienst; _aktuell = aktuell; _bereit = bereit; _oeffnen = oeffnen;
        _ = kopie; _ = signatur; // bestehender Konstruktionsvertrag bleibt erhalten
        _oeffnenAsync = oeffnenAsync;
        _erfassen = erfassen ?? ((p, ct) => ProjektPruefdatenKopie.ErfasseAsync(p, PauseAsync, ct));
        AbbrechenCommand = PruefenCommand.CreateCancelCommand();
    }

    private static async Task PauseAsync()
    {
        var dispatcher = System.Windows.Application.Current?.Dispatcher;
        if (dispatcher is null) await Task.Yield();
        else await Dispatcher.Yield(DispatcherPriority.Background);
    }

    public System.Windows.Input.ICommand AbbrechenCommand { get; }
    private bool KannPruefen() => !_disposed && _bereit();

    [RelayCommand(CanExecute = nameof(KannPruefen))]
    private async Task PruefenAsync(CancellationToken ct)
    {
        if (!KannPruefen()) return;
        IstAktuell = false; Punkte = []; Meldung = "Fünf Bereiche werden geprüft …";
        var (projekt, pfad) = _aktuell();
        try
        {
            var snapshot = await _erfassen(projekt, ct);
            if (!GleicherStand(projekt, pfad, snapshot)) { Verwerfe(); return; }
            var ergebnis = await Task.Run(() => _dienst.Pruefe(snapshot, pfad, ct), ct);
            if (_disposed) return;
            ct.ThrowIfCancellationRequested();
            if (!GleicherStand(projekt, pfad, snapshot))
            { Verwerfe(); return; }
            _geprueft = projekt; _pfad = pfad; _stand = snapshot;
            Punkte = ergebnis.Punkte; IstAktuell = true;
            Meldung = ergebnis.Haltungen + ergebnis.Schaechte == 0 ? "Das Projekt enthält keine Haltungen oder Schächte."
                : $"{Punkte.Count} Hinweise · {ergebnis.Haltungen} Haltungen und {ergebnis.Schaechte} Schächte geprüft · Stand {DateTime.Now:HH:mm}.";
        }
        catch (OperationCanceledException) { if (!_disposed) Meldung = "Prüfung abgebrochen. Kein vollständiges Ergebnis."; }
        catch (Exception ex) { if (!_disposed) Meldung = $"Prüfung fehlgeschlagen: {UserError.DescribeAndReport(ex, "Projektprüfung")}"; }
    }

    [RelayCommand]
    private async Task OeffnenAsync(ProjektPruefpunkt? punkt, CancellationToken ct)
    {
        if (_disposed || !IstAktuell || punkt is null || !System.Linq.Enumerable.Contains(Punkte, punkt)) return;
        try
        {
            var (p, pfad) = _aktuell();
            var stand = _stand;
            if (!ReferenceEquals(p, _geprueft) || pfad != _pfad || stand is null
                || !GleicherStand(p, pfad, stand))
            { Verwerfe(); return; }
            if (_oeffnenAsync is { } asyncOeffnen)
            {
                var punkte = Punkte;
                var meldung = Meldung;
                await asyncOeffnen(punkt,
                    () => StandPasst(p, pfad, stand),
                    ziel => ziel.UebernehmeRueckkehr(p, pfad, stand, punkte, meldung, punkt), ct);
            }
            else _oeffnen(punkt);
            // Auch stille Aenderungen im Dialog oder waehrend der Pfadaufloesung erkennen.
            if (!_disposed && !GleicherStand(p, pfad, stand)) Verwerfe();
            else if (!_disposed) FokusPunkt = punkt;
        }
        catch (OperationCanceledException) { /* Ein neuer Lauf oder Projektwechsel hat den Sprung abgebrochen. */ }
        catch (Exception ex)
        {
            if (_disposed) return;
            Verwerfe(); Meldung = $"Stelle konnte nicht geöffnet werden: {UserError.DescribeAndReport(ex, "Prüfstelle öffnen")}";
        }
    }

    private bool GleicherStand(Project projekt, string? pfad, Project stand)
    {
        return !_disposed && StandPasst(projekt, pfad, stand);
    }

    private bool StandPasst(Project projekt, string? pfad, Project stand)
    {
        if (!_bereit() || !ReferenceEquals(projekt, _aktuell().Projekt) || pfad != _aktuell().Pfad) return false;
        // Kein await: UI-Eingaben koennen zwischen erster und letzter Zeile nicht
        // eingreifen. Hintergrundschreiber muessen ihre eigenen Projektguards nutzen.
        return ProjektPruefdatenKopie.Gleich(stand, projekt)
            && _bereit() && ReferenceEquals(projekt, _aktuell().Projekt)
            && pfad == _aktuell().Pfad;
    }

    internal void UebernehmeRueckkehr(Project projekt, string? pfad, Project stand,
        IReadOnlyList<ProjektPruefpunkt> punkte, string meldung, ProjektPruefpunkt punkt)
    {
        if (!GleicherStand(projekt, pfad, stand)) return;
        _geprueft = projekt; _pfad = pfad; _stand = stand;
        Punkte = punkte; Meldung = meldung; IstAktuell = true; FokusPunkt = punkt;
    }

    public void Verwerfe()
    {
        PruefenCommand.Cancel();
        IstAktuell = false; Punkte = []; FokusPunkt = null; _geprueft = null; _stand = null;
        Meldung = "Projektstand geändert. Bitte erneut prüfen.";
        PruefenCommand.NotifyCanExecuteChanged();
    }

    public void Dispose() { _disposed = true; PruefenCommand.Cancel(); }
}
