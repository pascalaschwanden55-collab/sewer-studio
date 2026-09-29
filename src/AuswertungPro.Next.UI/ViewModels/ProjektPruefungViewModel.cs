using AuswertungPro.Next.Application.Common;
using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using AuswertungPro.Next.Application.UseCases.ProjektPruefung;
using AuswertungPro.Next.Domain.Models;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace AuswertungPro.Next.UI.ViewModels;

/// <summary>Besitzt nur den sichtbaren Prueflauf. Snapshot und Signatur schuetzen Projektwechsel und Zwischenkorrekturen.</summary>
public sealed partial class ProjektPruefungViewModel : ObservableObject, IDisposable
{
    private readonly IProjektPruefung _dienst;
    private readonly Func<(Project Projekt, string? Pfad)> _aktuell;
    private readonly Func<Project, Project> _kopie;
    private readonly Func<Project, string> _signatur;
    private readonly Func<bool> _bereit;
    private readonly Action<ProjektPruefpunkt> _oeffnen;
    private Project? _geprueft;
    private string? _stand, _pfad;
    private bool _disposed;
    [ObservableProperty] private IReadOnlyList<ProjektPruefpunkt> _punkte = [];
    [ObservableProperty] private string _meldung = "Noch nicht geprüft. Die Prüfung verändert keine Daten.";
    [ObservableProperty] private bool _istAktuell;

    public ProjektPruefungViewModel(IProjektPruefung dienst, Func<(Project, string?)> aktuell,
        Func<Project, Project> kopie, Func<Project, string> signatur, Func<bool> bereit, Action<ProjektPruefpunkt> oeffnen)
    {
        _dienst = dienst; _aktuell = aktuell; _kopie = kopie; _signatur = signatur; _bereit = bereit; _oeffnen = oeffnen;
        AbbrechenCommand = PruefenCommand.CreateCancelCommand();
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
            // Kopie auf dem UI-Thread erstellen, dann nur die Kopie im Hintergrund lesen.
            var snapshot = _kopie(projekt);
            var stand = _signatur(snapshot);
            var ergebnis = await Task.Run(() => _dienst.Pruefe(snapshot, pfad, ct), ct);
            if (_disposed) return;
            ct.ThrowIfCancellationRequested();
            if (!_bereit() || !ReferenceEquals(projekt, _aktuell().Projekt) || pfad != _aktuell().Pfad || stand != _signatur(projekt))
            { Verwerfe(); return; }
            _geprueft = projekt; _pfad = pfad; _stand = stand;
            Punkte = ergebnis.Punkte; IstAktuell = true;
            Meldung = ergebnis.Haltungen + ergebnis.Schaechte == 0 ? "Das Projekt enthält keine Haltungen oder Schächte."
                : $"{Punkte.Count} Hinweise · {ergebnis.Haltungen} Haltungen und {ergebnis.Schaechte} Schächte geprüft · Stand {DateTime.Now:HH:mm}.";
        }
        catch (OperationCanceledException) { if (!_disposed) Meldung = "Prüfung abgebrochen. Kein vollständiges Ergebnis."; }
        catch (Exception ex) { if (!_disposed) Meldung = $"Prüfung fehlgeschlagen: {UserError.DescribeAndReport(ex, "Projektprüfung")}"; }
    }

    [RelayCommand]
    private void Oeffnen(ProjektPruefpunkt? punkt)
    {
        if (_disposed || !IstAktuell || punkt is null || !System.Linq.Enumerable.Contains(Punkte, punkt)) return;
        try
        {
            var (p, pfad) = _aktuell();
            if (!_bereit() || !ReferenceEquals(p, _geprueft) || pfad != _pfad || _signatur(p) != _stand)
            { Verwerfe(); return; }
            _oeffnen(punkt);
            // Auch Aenderungen an Protokolleintraegen ohne PropertyChanged erkennen.
            if (!_disposed && (!ReferenceEquals(p, _aktuell().Projekt) || pfad != _aktuell().Pfad || _signatur(p) != _stand)) Verwerfe();
        }
        catch (Exception ex)
        {
            if (_disposed) return;
            Verwerfe(); Meldung = $"Stelle konnte nicht geöffnet werden: {UserError.DescribeAndReport(ex, "Prüfstelle öffnen")}";
        }
    }

    public void Verwerfe()
    {
        PruefenCommand.Cancel();
        IstAktuell = false; Punkte = []; _geprueft = null;
        Meldung = "Projektstand geändert. Bitte erneut prüfen.";
        PruefenCommand.NotifyCanExecuteChanged();
    }

    public void Dispose() { _disposed = true; PruefenCommand.Cancel(); }
}
