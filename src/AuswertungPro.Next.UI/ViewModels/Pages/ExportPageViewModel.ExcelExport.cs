using System;
using System.IO;
using System.Threading;
using AuswertungPro.Next.Application.Common;
using AuswertungPro.Next.Application.Export;
using AuswertungPro.Next.Domain.Models;

namespace AuswertungPro.Next.UI.ViewModels.Pages;

public sealed partial class ExportPageViewModel
{
    private async Task ExportAsync()
    {
        var templatePath = Path.Combine(AppContext.BaseDirectory, "Export_Vorlage", "Haltungen.xlsx");
        CancellationTokenSource? cancellation = null;
        try
        {
            if (!TryValidateExcelTemplate(templatePath, "Haltungs"))
                return;

            // Ein gemeinsamer Zielordner, fester Dateiname; ohne Zielordner den Dialog wie bisher.
            var outPath = ResolveConfiguredExcelPath("Haltungen")
                ?? _dialogs.SaveFile("Export (Haltungen.xlsx)", "Excel (*.xlsx)|*.xlsx", ".xlsx");
            if (outPath is null)
                return;

            if (!TryLoadCostsForHoldingExport(out var costStore))
                return;

            using var busy = Busy.Enter("Haltungen werden exportiert …");
            cancellation = BeginExcelExport();

            // Vor dem Export die abgeleiteten Kostenfelder auf den aktuellen Stand ziehen
            // (Sanieren=Nein/leer -> in der Arbeitskopie geleert). Das geoeffnete Projekt
            // bleibt dabei unveraendert; ein Export ist keine Bearbeitung.
            var exportProject = HoldingExcelExportSnapshotFactory.Create(_shell.Project);
            var projectFilePath = _settings.LastProjectPath;
            if (!string.IsNullOrWhiteSpace(projectFilePath))
                _costFieldSync.Sync(exportProject, costStore);

            var res = await Task.Run(() =>
                _excelExport.ExportToTemplate(
                    exportProject, templatePath, outPath,
                    headerRow: ExcelVorlagenLayout.KopfZeile,
                    startRow: ExcelVorlagenLayout.ErsteDatenZeile,
                    projectFilePath: projectFilePath,
                    cancellationToken: cancellation.Token), cancellation.Token);
            LastResult = res.Ok ? $"Exportiert: {outPath}" : $"Fehler: {res.ErrorMessage}";
            _shell.SetStatus(res.Ok ? "Exportiert" : "Export fehlgeschlagen");
            if (res.Ok)
                _toasts.Success(
                    $"Haltungen exportiert: {Path.GetFileName(outPath)}",
                    "Ordner öffnen",
                    () => _explorerReveal.TryReveal(outPath, out _));
            else
                _toasts.Error(res.ErrorMessage ?? "Haltungs-Export fehlgeschlagen.");
        }
        catch (OperationCanceledException)
        {
            LastResult = "Excel-Export abgebrochen. Es wurde keine neue Datei veröffentlicht.";
            _shell.SetStatus("Export abgebrochen");
            _toasts.Info("Haltungs-Export abgebrochen.");
        }
        catch (Exception ex)
        {
            var userMessage = UserError.DescribeAndReport(ex, "Haltungs-Excel-Export");
            LastResult = $"Fehler: {userMessage}";
            _shell.SetStatus("Export fehlgeschlagen");
            _toasts.Error($"Haltungs-Export fehlgeschlagen: {userMessage}");
        }
        finally
        {
            if (cancellation is not null)
            {
                EndExcelExport(cancellation);
                cancellation.Dispose();
            }
        }
    }

    private bool TryLoadCostsForHoldingExport(out ProjectCostStore store)
    {
        store = new ProjectCostStore();
        var projectPath = _settings.LastProjectPath ?? "";
        if (string.IsNullOrWhiteSpace(projectPath))
            return true;

        store = _projectCosts.Load(projectPath, out var loadError);
        if (string.IsNullOrWhiteSpace(loadError))
            return true;

        LastResult = $"Kostendaten konnten nicht geladen werden: {loadError}";
        _shell.SetStatus("Haltungs-Export gesperrt: Kostendaten nicht lesbar");
        _dialogs.Error(
            $"Der Haltungs-Export wurde abgebrochen, weil die Kostendaten nicht lesbar sind:\n{loadError}\n\n" +
            "Bitte costs.json pruefen und den Export danach erneut starten.",
            "Haltungs-Export");
        return false;
    }

    private async Task ExportSchaechteAsync()
    {
        var templatePath = Path.Combine(AppContext.BaseDirectory, "Export_Vorlage", "Schächte.xlsx");
        CancellationTokenSource? cancellation = null;
        try
        {
            if (!TryValidateExcelTemplate(templatePath, "Schacht"))
                return;

            var outPath = ResolveConfiguredExcelPath("Schaechte")
                ?? _dialogs.SaveFile("Export (Schaechte.xlsx)", "Excel (*.xlsx)|*.xlsx", ".xlsx");
            if (outPath is null)
                return;

            using var busy = Busy.Enter("Schächte werden exportiert …");
            cancellation = BeginExcelExport();
            var projectFilePath = _settings.LastProjectPath;
            var res = await Task.Run(() =>
                _excelExport.ExportSchaechteToTemplate(
                    _shell.Project, templatePath, outPath,
                    headerRow: ExcelVorlagenLayout.KopfZeile,
                    startRow: ExcelVorlagenLayout.ErsteDatenZeile,
                    projectFilePath: projectFilePath,
                    cancellationToken: cancellation.Token), cancellation.Token);
            LastResult = res.Ok ? $"Exportiert: {outPath}" : $"Fehler: {res.ErrorMessage}";
            _shell.SetStatus(res.Ok ? "Exportiert" : "Export fehlgeschlagen");
            if (res.Ok)
                _toasts.Success(
                    $"Schächte exportiert: {Path.GetFileName(outPath)}",
                    "Ordner öffnen",
                    () => _explorerReveal.TryReveal(outPath, out _));
            else
                _toasts.Error(res.ErrorMessage ?? "Schacht-Export fehlgeschlagen.");
        }
        catch (OperationCanceledException)
        {
            LastResult = "Excel-Export abgebrochen. Es wurde keine neue Datei veröffentlicht.";
            _shell.SetStatus("Export abgebrochen");
            _toasts.Info("Schacht-Export abgebrochen.");
        }
        catch (Exception ex)
        {
            var userMessage = UserError.DescribeAndReport(ex, "Schacht-Excel-Export");
            LastResult = $"Fehler: {userMessage}";
            _shell.SetStatus("Export fehlgeschlagen");
            _toasts.Error($"Schacht-Export fehlgeschlagen: {userMessage}");
        }
        finally
        {
            if (cancellation is not null)
            {
                EndExcelExport(cancellation);
                cancellation.Dispose();
            }
        }
    }

    internal bool TryValidateExcelTemplate(string templatePath, string exportName)
    {
        if (File.Exists(templatePath))
            return true;

        var message = $"Excel-Vorlage nicht gefunden: {templatePath}";
        LastResult = $"Fehler: {message}";
        _shell.SetStatus("Export fehlgeschlagen");
        _dialogs.Error(
            $"Der {exportName}-Export wurde abgebrochen, weil die Excel-Vorlage fehlt:\n{templatePath}\n\n" +
            "Bitte die SewerStudio-Installation pruefen und den Export danach erneut starten.",
            $"{exportName}-Export");
        return false;
    }

    private CancellationTokenSource BeginExcelExport()
    {
        var cancellation = new CancellationTokenSource();
        _excelExportCancellation = cancellation;
        CancelExcelExportCommand.NotifyCanExecuteChanged();
        return cancellation;
    }

    private void CancelExcelExport()
    {
        _excelExportCancellation?.Cancel();
        CancelExcelExportCommand.NotifyCanExecuteChanged();
    }

    private void EndExcelExport(CancellationTokenSource cancellation)
    {
        if (ReferenceEquals(_excelExportCancellation, cancellation))
            _excelExportCancellation = null;
        CancelExcelExportCommand.NotifyCanExecuteChanged();
    }
}
