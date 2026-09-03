using System;
using System.IO;
using AuswertungPro.Next.Application.UseCases.Xtf;
using AuswertungPro.Next.UI.Services;
using CommunityToolkit.Mvvm.Input;

namespace AuswertungPro.Next.UI.ViewModels.Pages;

/// <summary>
/// XTF an den Kataster: zwei Wege in der Sprache des Nutzers. Aktualisieren = Revision an den
/// Original-Kennungen, Neu = Erstexport mit eigenen Kennungen. Welcher Weg empfohlen wird,
/// entscheidet <see cref="XtfExportAuswahl"/> aus den Importkopien des Projekts.
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

    /// <summary>
    /// Erzeugt die revidierten XTF-Dateien. Zuerst laeuft eine reine Pruefung; erst nach
    /// ausdruecklicher Bestaetigung wird geschrieben. Kundenoriginale werden nur gelesen,
    /// die Revisionen landen in einem neuen Ordner mit Zeitstempel.
    /// </summary>
    private void ErzeugeXtfRevision()
    {
        var ziel = ExcelExportRoot;
        if (string.IsNullOrWhiteSpace(ziel))
            ziel = _dialogs.SelectFolder("Zielordner fuer die revidierte XTF waehlen");
        if (string.IsNullOrWhiteSpace(ziel))
            return;

        var projektPfad = _settings.LastProjectPath ?? "";
        IReadOnlyList<string>? quellDateien = null;

        var pruefung = _xtfRevisionExport.Erzeuge(
            new AuswertungPro.Next.Application.Xtf.XtfRevisionExportRequest(
                _shell.Project, projektPfad, ziel!, NurPruefen: true));

        if (pruefung.QuelleFehlt)
        {
            var ausgewaehlt = _dialogs.OpenFiles(
                "XTF-Quelldatei fuer die Revision waehlen",
                "XTF-Dateien (*.xtf)|*.xtf");
            if (ausgewaehlt.Length == 0)
            {
                LastResult = "Revision abgebrochen — keine XTF-Quelle gewaehlt.";
                return;
            }

            quellDateien = ausgewaehlt;
            pruefung = _xtfRevisionExport.Erzeuge(
                new AuswertungPro.Next.Application.Xtf.XtfRevisionExportRequest(
                    _shell.Project,
                    projektPfad,
                    ziel!,
                    NurPruefen: true,
                    Quelldateien: quellDateien));
        }

        if (!pruefung.Ok)
        {
            _dialogs.Error(
                string.IsNullOrWhiteSpace(pruefung.Bericht)
                    ? pruefung.Fehler ?? "Die Pruefung ist fehlgeschlagen."
                    : pruefung.Bericht,
                "Revidierte XTF");
            LastResult = "XTF-Revision konnte nicht geprueft werden.";
            return;
        }

        var weiter = _dialogs.ConfirmCancel(
            $"{pruefung.Bericht}\n\nDie Revision jetzt schreiben?\n" +
            "Die Originaldateien werden dabei nur gelesen.",
            "Revidierte XTF");
        if (weiter != DialogConfirm.Yes)
        {
            LastResult = "Revision abgebrochen.";
            return;
        }

        var ergebnis = _xtfRevisionExport.Erzeuge(
            new AuswertungPro.Next.Application.Xtf.XtfRevisionExportRequest(
                _shell.Project,
                projektPfad,
                ziel!,
                Quelldateien: quellDateien));

        if (!ergebnis.Ok)
        {
            _dialogs.Error($"{ergebnis.Bericht}", "Revidierte XTF");
            LastResult = "Revision nicht vollstaendig erzeugt.";
            return;
        }

        if (ergebnis.Dateien.Count > 0)
            LetzterXtfOrdner = Path.GetDirectoryName(ergebnis.Dateien[0]);

        LastResult = ergebnis.Dateien.Count switch
        {
            0 => "Keine Änderung gegenüber dem Kataster — nichts geschrieben.",
            1 => "Katasterdaten aktualisiert: 1 Datei geschrieben.",
            var n => $"Katasterdaten aktualisiert: {n} Dateien geschrieben."
        };
        _toasts.Success(LastResult);
    }

    /// <summary>
    /// Erzeugt eine eigenstaendige NEUE XTF aus dem ganzen Projektstand. Erst der Bericht,
    /// dann auf Bestaetigung die Datei.
    /// </summary>
    private void ErzeugeXtfNeu()
    {
        var ziel = ExcelExportRoot;
        if (string.IsNullOrWhiteSpace(ziel))
            ziel = _dialogs.SelectFolder("Zielordner fuer die neue XTF waehlen");
        if (string.IsNullOrWhiteSpace(ziel))
            return;

        var pruefung = _xtfNeuExport.Erzeuge(
            new AuswertungPro.Next.Application.Xtf.XtfNeuExportRequest(
                _shell.Project, ziel!, NurPruefen: true));

        if (!pruefung.Ok)
        {
            _dialogs.Error(
                string.IsNullOrWhiteSpace(pruefung.Bericht)
                    ? pruefung.Fehler ?? "Die Pruefung ist fehlgeschlagen."
                    : $"{pruefung.Bericht}\n\n{pruefung.Fehler}",
                "Neue XTF");
            return;
        }

        var weiter = _dialogs.ConfirmCancel(
            $"{pruefung.Bericht}\n\nDie Datei jetzt schreiben?",
            "Neue XTF");
        if (weiter != DialogConfirm.Yes)
        {
            LastResult = "Export abgebrochen.";
            return;
        }

        var ergebnis = _xtfNeuExport.Erzeuge(
            new AuswertungPro.Next.Application.Xtf.XtfNeuExportRequest(_shell.Project, ziel!));

        if (!ergebnis.Ok)
        {
            _dialogs.Error(ergebnis.Fehler ?? ergebnis.Bericht, "Neue XTF");
            LastResult = "Neue XTF nicht erzeugt.";
            return;
        }

        LetzterXtfOrdner = Path.GetDirectoryName(ergebnis.Datei);
        LastResult = $"Neue XTF erstellt: {Path.GetFileName(ergebnis.Datei)}";
        _toasts.Success(LastResult);
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
