using System.Threading;
using AuswertungPro.Next.Application.Import;
using AuswertungPro.Next.Domain.Models;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace AuswertungPro.Next.UI.ViewModels.Pages;

/// <summary>
/// Hintergrund-Nachlauf fuer PDF-Stammdaten. Bewusst als eigener Teil gehalten,
/// damit das allgemeine Schacht-ViewModel nicht zur Grossklasse wird.
/// </summary>
public sealed partial class SchaechtePageViewModel
{
    private CancellationTokenSource? _stammdatenErgaenzungCts;

    [ObservableProperty] private bool _isStammdatenErgaenzungInProgress;
    [ObservableProperty] private double _stammdatenErgaenzungProgress;
    [ObservableProperty] private string _stammdatenErgaenzungText = string.Empty;

    public IAsyncRelayCommand ErgaenzeStammdatenAusPdfsCommand { get; }
    public IRelayCommand CancelStammdatenErgaenzungCommand { get; }

    partial void OnIsStammdatenErgaenzungInProgressChanged(bool value)
    {
        _ = value;
        ErgaenzeStammdatenAusPdfsCommand.NotifyCanExecuteChanged();
        (CancelStammdatenErgaenzungCommand as RelayCommand)?.NotifyCanExecuteChanged();
    }

    private bool CanErgaenzeStammdatenAusPdfs()
        => !IsStammdatenErgaenzungInProgress && Records.Count > 0;

    private void CancelStammdatenErgaenzung()
    {
        _stammdatenErgaenzungCts?.Cancel();
        StammdatenErgaenzungText = "Abbruch angefordert ...";
    }

    private async Task ErgaenzeStammdatenAusPdfsAsync()
    {
        var projektOrdner = _shell.GetProjectFolder();
        if (string.IsNullOrWhiteSpace(projektOrdner))
        {
            _dialogs.Info("Kein Projekt geoeffnet.", "PDF-Stammdaten ergaenzen");
            return;
        }

        if (!_dialogs.ConfirmWarn(
                "Fehlende Schachtform, Dimension und Schachttiefe werden aus den bereits vorhandenen PDFs ergaenzt.\n\n" +
                "Vorhandene Eintraege bleiben unveraendert. Der Vorgang kann bei vielen PDFs einige Minuten dauern.",
                "PDF-Stammdaten ergaenzen"))
            return;

        List<SchachtStammdatenQuelle> quellen;
        lock (_shell.CollectionLock)
        {
            quellen = Records
                .Select(record => new SchachtStammdatenQuelle(
                    record.Id,
                    ResolveSchachtNummer(record),
                    record.GetFieldValue("PDF_Path"),
                    record.GetFieldValue("Link"),
                    record.GetFieldValue("Schachtform"),
                    record.GetFieldValue("Dimension"),
                    record.GetFieldValue("Schachttiefe")))
                .ToList();
        }

        _stammdatenErgaenzungCts?.Dispose();
        _stammdatenErgaenzungCts = new CancellationTokenSource();
        var cancellationToken = _stammdatenErgaenzungCts.Token;
        IsStammdatenErgaenzungInProgress = true;
        StammdatenErgaenzungProgress = 0;
        StammdatenErgaenzungText = "Vorhandene Schacht-PDFs werden geprueft ...";

        var progress = new Progress<SchachtStammdatenErgaenzungsFortschritt>(p =>
        {
            StammdatenErgaenzungProgress = p.Gesamt <= 0
                ? 0
                : Math.Clamp(p.Aktuell * 100d / p.Gesamt, 0d, 100d);
            StammdatenErgaenzungText = $"{p.Meldung} ({p.Aktuell}/{p.Gesamt})";
        });

        try
        {
            var result = await Task.Run(
                () => _schachtStammdatenErgaenzung.Ermitteln(
                    projektOrdner,
                    quellen,
                    progress,
                    cancellationToken),
                cancellationToken);

            cancellationToken.ThrowIfCancellationRequested();
            var recordsById = Records.ToDictionary(record => record.Id);
            var geaenderteSchaechte = 0;
            var ergaenzteFelder = 0;

            if (result.Ergaenzungen.Count > 0)
                _shell.TryCreateImportRestorePoint("Schacht-PDF-Stammdaten");

            foreach (var ergaenzung in result.Ergaenzungen)
            {
                if (!recordsById.TryGetValue(ergaenzung.RecordId, out var record))
                    continue;

                var recordChanged = false;
                recordChanged |= SetIfMissing(record, "Schachtform", ergaenzung.Schachtform, ref ergaenzteFelder);
                recordChanged |= SetIfMissing(record, "Dimension", ergaenzung.Dimension, ref ergaenzteFelder);
                recordChanged |= SetIfMissing(record, "Schachttiefe", ergaenzung.Schachttiefe, ref ergaenzteFelder);
                if (recordChanged)
                    geaenderteSchaechte++;
            }

            if (geaenderteSchaechte > 0)
            {
                _shell.MarkProjectDirty();
                if (!_shell.TrySaveProject())
                {
                    _dialogs.Warn(
                        "Die Werte wurden in der geoeffneten Ansicht ergaenzt, konnten aber noch nicht gespeichert werden. Bitte erneut speichern.",
                        "PDF-Stammdaten ergaenzen");
                }
            }

            StammdatenErgaenzungProgress = 100;
            var summary = $"Ergaenzt: {geaenderteSchaechte} Schaechte / {ergaenzteFelder} Felder. " +
                          $"PDF gefunden: {result.PdfGefunden}, ohne PDF: {result.PdfNichtGefunden}, " +
                          $"kein passendes Schachtprotokoll: {result.NichtLesbar}, " +
                          $"bereits vollstaendig: {result.BereitsVollstaendig}.";
            LastResult = summary;
            StammdatenErgaenzungText = summary;

            var details = result.Meldungen.Count == 0
                ? string.Empty
                : "\n\nHinweise:\n" + string.Join("\n", result.Meldungen.Take(12));
            if (result.Meldungen.Count > 12)
                details += $"\n... und {result.Meldungen.Count - 12} weitere Hinweise.";

            _dialogs.Info(summary + details, "PDF-Stammdaten ergaenzen");
        }
        catch (OperationCanceledException)
        {
            LastResult = "PDF-Stammdaten: Vorgang abgebrochen. Es wurden keine Werte uebernommen.";
            StammdatenErgaenzungText = LastResult;
        }
        catch (Exception ex)
        {
            LastResult = $"PDF-Stammdaten konnten nicht ergaenzt werden: {ex.Message}";
            StammdatenErgaenzungText = LastResult;
            _dialogs.Warn(LastResult, "PDF-Stammdaten ergaenzen");
        }
        finally
        {
            IsStammdatenErgaenzungInProgress = false;
            _stammdatenErgaenzungCts?.Dispose();
            _stammdatenErgaenzungCts = null;
        }
    }

    private static bool SetIfMissing(
        SchachtRecord record,
        string fieldName,
        string? value,
        ref int ergaenzteFelder)
    {
        if (!string.IsNullOrWhiteSpace(record.GetFieldValue(fieldName))
            || string.IsNullOrWhiteSpace(value))
            return false;

        record.SetFieldValue(fieldName, value.Trim());
        ergaenzteFelder++;
        return true;
    }
}
