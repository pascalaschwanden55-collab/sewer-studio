using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Win32;

using AuswertungPro.Next.Application.Common;
using AuswertungPro.Next.Domain.Models;
using AuswertungPro.Next.Infrastructure.Costs;
using AuswertungPro.Next.Infrastructure.Output.Offers;

namespace AuswertungPro.Next.UI.ViewModels.Pages;

// BuilderPageViewModel PDF-Export: ExportPdfAsync (PDF erzeugen + Save-Dialog),
// PrintPdf (Standard-Drucker via ProcessRunner.TryOpenWithVerb), OpenLast
// ExportedPdf (Letzte PDF im Standard-Viewer) + Stale-/Clear-/Can-Helpers.
// Aus dem Hauptdatei extrahiert (Slice 14b).
public sealed partial class BuilderPageViewModel
{
    [RelayCommand]
    private async Task ExportPdfAsync()
    {
        if (IsPdfExportInProgress)
            return;

        RefreshData();
        var filteredRows = Rows.ToList();
        if (filteredRows.Count == 0)
        {
            _dialogs.ShowMessage(
                "Keine Daten fuer den aktuellen Filter gefunden.",
                "Druckcenter",
                MessageBoxButton.OK,
                MessageBoxImage.Information);
            return;
        }

        var safeProjectName = SanitizeFilePart(_shell.Project.Name);
        var defaultName = $"Druckcenter_{safeProjectName}_{DateTime.Now:yyyyMMdd}.pdf";
        var output = App.Resolve<IDialogService>().SaveFile(
            "Druckcenter PDF speichern",
            "PDF (*.pdf)|*.pdf",
            defaultExt: "pdf",
            defaultFileName: defaultName);
        if (string.IsNullOrWhiteSpace(output))
            return;

        IsPdfExportInProgress = true;
        PdfExportProgress = "PDF wird vorbereitet...";

        try
        {
            await Task.Yield();
            var entries = BuildSummaryEntries(filteredRows);
            var dataLines = IncludeDataSection ? BuildHoldingDataLines(filteredRows) : null;

            var projectMeta = _shell.Project.Metadata;
            var projectCustomer = BuildProjectCustomerBlock(projectMeta);
            var objectBlock = BuildObjectBlock(projectMeta, filteredRows);
            var filterSummary = BuildFilterSummaryText();
            var qualityHint = RowsWithDetailedCosts == FilteredRowsCount
                ? "Alle gefilterten Haltungen haben Positionsdetails."
                : $"{FilteredRowsCount - RowsWithDetailedCosts} Haltung(en) ohne Positionsdetails (Pauschalwerte aus Tabelle).";

            var ctx = new OfferPdfContext
            {
                ProjectTitle = "Abwasser Uri - Druckcenter",
                VariantTitle = $"Gefilterte Kostenzusammenstellung ({filteredRows.Count} Haltungen)",
                CustomerBlock = projectCustomer,
                ObjectBlock = objectBlock,
                FilterSummaryText = filterSummary,
                Currency = "CHF",
                OfferNo = "",
                TextBlocks = new List<string>
                {
                    qualityHint,
                    "Die Statistik fuer Inliner/Manschetten basiert auf vorhandenen Positionsdetails.",
                    "Kostenzusammenstellung nach Eigentuemer und Gesamtpositionen ist im Ausdruck enthalten."
                }
            };

            var model = OfferPdfModelFactory.CreateCostSummary(
                entries,
                ctx,
                DateTimeOffset.Now,
                includeOwnerSummary: IncludeOwnerSummarySection,
                includePositionSummary: IncludePositionSummarySection,
                holdingDataLines: dataLines);

            var templatePath = Path.Combine(AppContext.BaseDirectory, "Templates", "cost_summary.sbnhtml");
            var logoPath = Path.Combine(AppContext.BaseDirectory, "Assets", "Brand", "abwasser-uri-logo.png");

            var renderer = new OfferHtmlToPdfRenderer();
            PdfExportProgress = "PDF wird gerendert...";
            await renderer.RenderAsync(model, templatePath, output, logoPath);

            LastExportedPdfPath = output;
            LastExportedAt = DateTimeOffset.Now;
            LastExportScopeSummary = BuildExportScopeSummary(filteredRows);
            IsLastExportCurrent = true;
            _lastExportProjectPath = App.Resolve<AppSettings>().LastProjectPath ?? "";
            LastResult = $"PDF erstellt: {Path.GetFileName(output)}";
            _shell.SetStatus("Druckcenter PDF erstellt");
            PdfExportProgress = "PDF fertig.";
            _dialogs.ShowMessage(
                $"Druckcenter-PDF wurde erstellt:\n{output}",
                "Druckcenter",
                MessageBoxButton.OK,
                MessageBoxImage.Information);
        }
        catch (Exception ex)
        {
            LastResult = $"Fehler: {ex.Message}";
            PdfExportProgress = "PDF-Erstellung fehlgeschlagen.";
            _dialogs.ShowMessage(
                $"PDF konnte nicht erstellt werden:\n{ex.Message}",
                "Druckcenter",
                MessageBoxButton.OK,
                MessageBoxImage.Error);
        }
        finally
        {
            IsPdfExportInProgress = false;
        }
    }

    [RelayCommand(CanExecute = nameof(CanPrintPdf))]
    private void PrintPdf()
    {
        string? pdfPath = null;

        if (HasLastExportedPdf())
        {
            if (IsLastExportCurrent)
            {
                pdfPath = LastExportedPdfPath;
            }
            else
            {
                var decision = _dialogs.ShowMessage(
                    "Der Druckstand hat sich seit dem letzten Export geaendert.\n\nJa = letztes PDF drucken\nNein = anderes PDF auswaehlen\nAbbrechen = nichts tun",
                    "Druckcenter",
                    MessageBoxButton.YesNoCancel,
                    MessageBoxImage.Question);

                if (decision == MessageBoxResult.Cancel)
                    return;

                if (decision == MessageBoxResult.Yes)
                    pdfPath = LastExportedPdfPath;
            }
        }

        pdfPath ??= App.Resolve<IDialogService>().OpenFile("PDF zum Drucken waehlen", "PDF (*.pdf)|*.pdf");

        if (string.IsNullOrWhiteSpace(pdfPath))
            return;

        if (AuswertungPro.Next.Application.Common.ProcessRunner.TryOpenWithVerb(pdfPath, "print", out var printErr))
        {
            LastResult = $"Druckauftrag gestartet: {pdfPath}";
            _shell.SetStatus("PDF-Druckauftrag gestartet");
        }
        else
        {
            LastResult = $"Fehler beim Drucken: {printErr}";
            _dialogs.ShowMessage(
                $"PDF konnte nicht gedruckt werden:\n{printErr}",
                "Druckcenter",
                MessageBoxButton.OK,
                MessageBoxImage.Error);
        }
    }

    [RelayCommand(CanExecute = nameof(CanOpenLastExportedPdf))]
    private void OpenLastExportedPdf()
    {
        if (!HasLastExportedPdf())
        {
            ClearLastExport("Die zuletzt exportierte PDF-Datei wurde nicht gefunden.");
            _dialogs.ShowMessage(
                "Die zuletzt exportierte PDF-Datei wurde nicht gefunden.",
                "Druckcenter",
                MessageBoxButton.OK,
                MessageBoxImage.Information);
            return;
        }

        if (AuswertungPro.Next.Application.Common.ProcessRunner.TryOpenWithDefaultProgram(LastExportedPdfPath, out var openErr))
        {
            LastResult = $"PDF geoeffnet: {Path.GetFileName(LastExportedPdfPath)}";
        }
        else
        {
            LastResult = $"Fehler beim Oeffnen: {openErr}";
            _dialogs.ShowMessage(
                $"PDF konnte nicht geoeffnet werden:\n{openErr}",
                "Druckcenter",
                MessageBoxButton.OK,
                MessageBoxImage.Error);
        }
    }

    private void MarkExportAsStale()
    {
        if (IsPdfExportInProgress || !HasAnyExportPath())
            return;

        if (!HasLastExportedPdf())
        {
            ClearLastExport("Die zuletzt exportierte PDF-Datei wurde nicht gefunden.");
            return;
        }

        IsLastExportCurrent = false;
    }

    private void ClearLastExport(string? resultText = null)
    {
        LastExportedPdfPath = "";
        LastExportScopeSummary = "";
        LastExportedAt = null;
        IsLastExportCurrent = false;
        _lastExportProjectPath = "";

        if (!string.IsNullOrWhiteSpace(resultText))
            LastResult = resultText;
    }

    private bool HasAnyExportPath()
        => !string.IsNullOrWhiteSpace(LastExportedPdfPath);

    private bool HasLastExportedPdf()
        => HasAnyExportPath() && File.Exists(LastExportedPdfPath);

    private bool CanOpenLastExportedPdf()
        => !IsPdfExportInProgress && HasLastExportedPdf();

    private bool CanPrintPdf()
        => !IsPdfExportInProgress;

}
