using System.IO;
using AuswertungPro.Next.Application.Common;
using AuswertungPro.Next.Application.Import;
using AuswertungPro.Next.Domain.Models;
using AuswertungPro.Next.UI.Services;

namespace AuswertungPro.Next.UI.ViewModels.Pages;

public sealed partial class SchaechtePageViewModel
{
    private bool CanRefreshProtocol()
        => CanStartProtocolPdfOperation()
           && SchachtProtocolRefreshController.CanExecute(Selected);

    private async Task RefreshProtocolAsync()
    {
        if (!TryBeginProtocolPdfOperation("Protokollaktualisierung"))
            return;

        try
        {
            _ = await _schachtProtocolRefreshController.ExecuteAsync(Selected);
        }
        finally
        {
            EndProtocolPdfOperation();
        }
    }

    /// <summary>
    /// Sucht die Protokoll-PDF genau dieses einen Schachts: zuerst die gespeicherte
    /// Verknuepfung (relativ oder absolut), danach nur dessen eigenen Schachtordner.
    /// </summary>
    private SchachtProtocolFileMatch? LocateProtocolFile(SchachtRecord record, string projektOrdner)
        => _protocolFileLocator.Locate(
            projektOrdner,
            record.GetFieldValue(FieldKeys.PdfPath),
            record.GetFieldValue(FieldKeys.Link),
            record.GetFieldValue("Schachtnummer"));

    /// <summary>
    /// Uebernimmt das frisch gelesene Protokoll auf genau diesen einen Schacht und
    /// baut ihn dabei vollstaendig neu auf. Nur so verschwindet auch ein Wert, den
    /// der Benutzer inzwischen aus der verknuepften PDF entfernt hat. Ein Dienst
    /// ohne diese Faehigkeit ergaenzt weiterhin nur.
    /// </summary>
    private void RebuildFromProtocol(
        SchachtRecord schacht,
        SchachtProtocolParseResult protokoll,
        string pdfPfadFuerFeld)
    {
        if (_schachtProtocolImport is ISchachtProtocolRebuildService rebuild)
            rebuild.Rebuild(schacht, protokoll, pdfPfadFuerFeld);
        else
            _schachtProtocolImport.Apply(schacht, protokoll, pdfPfadFuerFeld);
    }

    private async Task ImportProtocolAsync()
    {
        if (!TryBeginProtocolPdfOperation("Protokollimport"))
            return;

        try
        {
            await ImportProtocolCoreAsync();
        }
        finally
        {
            EndProtocolPdfOperation();
        }
    }

    private async Task ImportProtocolCoreAsync()
    {
        var projectContext = new ProjectOperationContext(
            _shell.Project,
            _settings.LastProjectPath);
        var projektOrdner = _shell.GetProjectFolder();
        if (string.IsNullOrWhiteSpace(projektOrdner))
        {
            _dialogs.Info("Kein Projekt geöffnet.", "Protokoll importieren");
            return;
        }

        var quelle = _dialogs.ConfirmCancel(
            "Quelle auswählen:\n\n" +
            "Ja = einzelne PDF-Datei\n" +
            "Nein = ganzen Ordner einschliesslich Unterordner\n" +
            "Abbrechen = nichts importieren",
            "Protokoll importieren");
        if (quelle == DialogConfirm.Cancel)
            return;

        if (quelle == DialogConfirm.No)
        {
            var ordner = _dialogs.SelectFolder("Ordner mit Schachtprotokollen auswählen");
            if (!string.IsNullOrWhiteSpace(ordner))
                await ImportProtocolFolderAsync(projectContext, projektOrdner, ordner);
            return;
        }

        var pdfPfad = _dialogs.OpenFile("Schachtprotokoll auswählen", "PDF (*.pdf)|*.pdf");
        if (!string.IsNullOrWhiteSpace(pdfPfad))
            await ImportSingleProtocolAsync(projectContext, projektOrdner, pdfPfad);
    }

    private Task ImportSingleProtocolAsync(
        ProjectOperationContext projectContext,
        string projektOrdner,
        string pdfPfad)
        => _schachtProtocolSingleImportController.ExecuteAsync(
            projectContext,
            projektOrdner,
            pdfPfad,
            Selected);

    private async Task<SchachtProtocolParseResult?> ReadProtocolAsync(string pdfPath, string dialogTitle)
    {
        try
        {
            LastResult = $"PDF wird gelesen: {Path.GetFileName(pdfPath)} ...";
            return await Task.Run(() => _schachtProtocolImport.Parse(pdfPath));
        }
        catch (Exception ex)
        {
            LastResult = "PDF konnte nicht gelesen werden.";
            var userMessage = UserError.DescribeAndReport(ex, "Schachtprotokoll lesen");
            _dialogs.Warn($"Das PDF konnte nicht gelesen werden:\n{userMessage}", dialogTitle);
            return null;
        }
    }

    private bool ProjectIsStillOpen(
        ProjectOperationContext expectedProject,
        string dialogTitle,
        ProjectOperationImpact impact)
    {
        if (ActiveProjectGuard.IsCurrent(
                expectedProject,
                _shell.Project,
                _settings.LastProjectPath))
            return true;

        var filesWritten = (impact & ProjectOperationImpact.ProjectFilesWritten) != 0;
        var dataChanged = (impact & ProjectOperationImpact.ProjectDataChanged) != 0;
        if (filesWritten && dataChanged)
        {
            LastResult =
                "Projekt wurde gewechselt: PDF-Verteilung abgeschlossen; Projektdaten übernommen, aber nicht gespeichert.";
            _dialogs.Warn(
                "Das Projekt wurde während der Übernahme gewechselt. " +
                "Mindestens eine PDF-Datei wurde bereits in das zuvor gestartete Projekt kopiert. " +
                "Die zugehörigen Projektdaten wurden übernommen, aber nicht gespeichert. " +
                "Bitte prüfen Sie die kopierten Dateien; die ungespeicherten Projektdaten " +
                "können nach dem Wechsel nicht automatisch übernommen werden.",
                dialogTitle);
            return false;
        }

        if (dataChanged)
        {
            LastResult =
                "Projekt wurde gewechselt: Änderungen wurden übernommen, aber nicht gespeichert.";
            _dialogs.Warn(
                "Das Projekt wurde während der Übernahme gewechselt. " +
                "Die Änderungen im zuvor gestarteten Projekt wurden nicht gespeichert.",
                dialogTitle);
            return false;
        }

        if (filesWritten)
        {
            LastResult =
                "Projekt wurde gewechselt: PDF-Verteilung abgeschlossen; Projektdaten wurden nicht übernommen.";
            _dialogs.Warn(
                "Das Projekt wurde während des Imports gewechselt. " +
                "Mindestens eine PDF-Datei wurde bereits in das zuvor gestartete Projekt kopiert, " +
                "aber nicht in dessen Projektdaten übernommen. Bitte prüfen Sie die kopierten Dateien.",
                dialogTitle);
            return false;
        }

        LastResult = "Vorgang abgebrochen: Projekt wurde gewechselt.";
        _dialogs.Warn(
            "Das Projekt wurde während des Einlesens gewechselt. " +
            "Es wurden keine Daten übernommen.",
            dialogTitle);
        return false;
    }

    private void ClearSelectedIfSame(SchachtRecord? expectedSelection)
    {
        if (ReferenceEquals(Selected, expectedSelection))
            Selected = null;
    }

    private static string ResolveReadFailure(SchachtProtocolParseResult ergebnis, string fallback)
        => string.IsNullOrWhiteSpace(ergebnis.Lesehinweis) ? fallback : ergebnis.Lesehinweis;
}
