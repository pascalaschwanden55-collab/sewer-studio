using System;
using AuswertungPro.Next.Application.UseCases.Xtf;
using AuswertungPro.Next.UI.Services;
using CommunityToolkit.Mvvm.Input;

namespace AuswertungPro.Next.UI.ViewModels.Pages;

/// <summary>
/// XTF an den Kataster: zwei Wege in der Sprache des Nutzers. Aktualisieren = Revision an den
/// Original-Kennungen, Neu = Erstexport mit eigenen Kennungen. Welcher Weg empfohlen wird,
/// entscheidet <see cref="XtfExportAuswahl"/> aus den Importkopien des Projekts. Der Ablauf
/// selbst (pruefen, Quelle erfragen, Vorschau, schreiben) liegt in
/// <see cref="XtfAktualisierenUseCase"/> und <see cref="XtfNeuErstellenUseCase"/>; hier wird
/// nur verdrahtet, was die Oberflaeche dazu leiht.
/// </summary>
public sealed partial class ExportPageViewModel
{
    /// <summary>Oeffnet den Ausgabeordner der zuletzt geschriebenen XTF im Explorer.</summary>
    public IRelayCommand OeffneXtfOrdnerCommand { get; }

    /// <summary>Welcher XTF-Weg zum Projekt passt — entschieden von <see cref="XtfExportAuswahl"/>.</summary>
    public bool XtfAktualisierenEmpfohlen => _xtfAuswahl.Empfohlen == XtfExportWeg.Aktualisieren;
    public bool XtfNeuEmpfohlen => _xtfAuswahl.Empfohlen == XtfExportWeg.Neu;
    public string XtfOriginalZeile => _xtfAuswahl.OriginalZeile;
    public string XtfNeuHinweis => _xtfAuswahl.NeuHinweis;

    /// <summary>Ordner der zuletzt erzeugten XTF; null, solange in dieser Sitzung nichts geschrieben wurde.</summary>
    public string? LetzterXtfOrdner
    {
        get => _letzterXtfOrdner;
        private set
        {
            if (SetProperty(ref _letzterXtfOrdner, value))
            {
                OnPropertyChanged(nameof(HatXtfOrdner));
                OeffneXtfOrdnerCommand.NotifyCanExecuteChanged();
            }
        }
    }

    public bool HatXtfOrdner => !string.IsNullOrWhiteSpace(_letzterXtfOrdner);

    /// <summary>Bestehende Katasterdaten aktualisieren — Vorschau, dann schreiben.</summary>
    private void ErzeugeXtfRevision()
    {
        var ziel = Zielordner("Zielordner für die aktualisierte XTF wählen");
        if (ziel is null)
            return;

        var ergebnis = XtfAktualisierenUseCase.Execute(
            _xtfRevisionExport,
            new XtfAktualisierenRequest(_shell.Project, _settings.LastProjectPath ?? "", ziel),
            XtfAktionen());
        Uebernimm(ergebnis);
    }

    /// <summary>Neue eigenstaendige XTF erstellen — Bericht als Vorschau, dann schreiben.</summary>
    private void ErzeugeXtfNeu()
    {
        var ziel = Zielordner("Zielordner für die neue XTF wählen");
        if (ziel is null)
            return;

        var ergebnis = XtfNeuErstellenUseCase.Execute(
            _xtfNeuExport,
            new AuswertungPro.Next.Application.Xtf.XtfNeuExportRequest(_shell.Project, ziel),
            XtfAktionen());
        Uebernimm(ergebnis);
    }

    /// <summary>Was der Ablauf von der Oberflaeche braucht: Dateiwahl, Vorschaufenster, Fehlerfenster.</summary>
    private XtfExportActions XtfAktionen() => new(
        () => _dialogs.OpenFiles("Original-XTF für die Aktualisierung wählen", "XTF-Dateien (*.xtf)|*.xtf"),
        _xtfVorschau.Bestaetige,
        _xtfVorschau.ZeigeFehler);

    private string? Zielordner(string frage)
    {
        var ziel = ExcelExportRoot;
        if (string.IsNullOrWhiteSpace(ziel))
            ziel = _dialogs.SelectFolder(frage);
        return string.IsNullOrWhiteSpace(ziel) ? null : ziel;
    }

    private void Uebernimm(XtfExportErgebnis ergebnis)
    {
        LastResult = ergebnis.Meldung;
        if (!ergebnis.Geschrieben)
            return;

        LetzterXtfOrdner = ergebnis.Ordner;
        _toasts.Success(ergebnis.Meldung);
    }

    /// <summary>
    /// Liest die Importkopien des Projekts und leitet daraus Empfehlung, Original-Zeile und
    /// Hinweis ab. Laeuft beim Aufbau und nach jedem Projektwechsel; scheitert nie sichtbar.
    /// </summary>
    private void AktualisiereXtfAuswahl()
    {
        var kopien = _shell.Project is null
            ? []
            : _xtfRevisionExport.FindeProjektkopien(_settings.LastProjectPath);
        _xtfAuswahl = XtfExportAuswahl.Aus(kopien);
        OnPropertyChanged(nameof(XtfAktualisierenEmpfohlen));
        OnPropertyChanged(nameof(XtfNeuEmpfohlen));
        OnPropertyChanged(nameof(XtfOriginalZeile));
        OnPropertyChanged(nameof(XtfNeuHinweis));
    }

    private void OeffneXtfOrdner()
    {
        if (!_explorerReveal.TryReveal(LetzterXtfOrdner, out var fehler))
            _dialogs.Warn(fehler ?? "Der Ordner konnte nicht geöffnet werden.", "Ordner öffnen");
    }
}
