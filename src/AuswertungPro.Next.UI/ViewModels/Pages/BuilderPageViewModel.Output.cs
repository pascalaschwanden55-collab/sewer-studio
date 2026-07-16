using System.Diagnostics;
using System.IO;
using System.Text;
using CommunityToolkit.Mvvm.Input;
using AuswertungPro.Next.Application.Common;
using AuswertungPro.Next.Application.Costs;
using AuswertungPro.Next.Application.DataPage;
using AuswertungPro.Next.Domain.Models;
using AuswertungPro.Next.Infrastructure.Costs;
using AuswertungPro.Next.Infrastructure.Output.Offers;
using AuswertungPro.Next.UI.DataPage;
using AuswertungPro.Next.UI.Services;

namespace AuswertungPro.Next.UI.ViewModels.Pages;

/// <summary>Ausgabe-, Druck- und NPK-Arbeitsablaeufe des Druckcenters.</summary>
public sealed partial class BuilderPageViewModel
{
    private DataPagePrintController? _printController;

    [RelayCommand]
    private async Task ExportPdfAsync()
    {
        if (IsPdfExportInProgress)
            return;

        RefreshData();
        var filteredRows = Rows.ToList();
        if (filteredRows.Count == 0)
        {
            _dialogs.Info(
                "Keine Daten fuer den aktuellen Filter gefunden.",
                "Druckcenter");
            return;
        }

        if (!OfferRecomputeCostsForCurrentCatalog(filteredRows))
            return;

        filteredRows = Rows.ToList();

        var safeProjectName = SanitizeFilePart(_shell.Project.Name);
        var defaultName = $"Druckcenter_{safeProjectName}_{DateTime.Now:yyyyMMdd}.pdf";
        var output = _dialogs.SaveFile(
            "Druckcenter PDF speichern",
            "PDF (*.pdf)|*.pdf",
            defaultExt: "pdf",
            defaultFileName: defaultName);
        if (string.IsNullOrWhiteSpace(output))
            return;

        var selection = BuilderPageExportScope.All(filteredRows);
        var qualityHint = RowsWithDetailedCosts == FilteredRowsCount
            ? "Alle gefilterten Haltungen haben Positionsdetails."
            : $"{FilteredRowsCount - RowsWithDetailedCosts} Haltung(en) ohne Positionsdetails (Pauschalwerte aus Tabelle).";

        await RenderCostSummaryPdfAsync(selection, BuildFilterSummaryText(), qualityHint, output);
    }

    /// <summary>Kostenblatt fuer genau die gewaehlte Haltung: gleicher Ausdruck, auf eine Haltung begrenzt.</summary>
    [RelayCommand]
    private async Task PrintSingleKostenblattAsync()
    {
        if (IsPdfExportInProgress)
            return;

        var row = SelectedRow;
        if (row is null)
        {
            _dialogs.Info("Bitte zuerst eine Haltung in der Tabelle waehlen.", "Druckcenter");
            return;
        }

        var safeHolding = SanitizeFilePart(row.Holding);
        var defaultName = $"Kostenblatt_{safeHolding}_{DateTime.Now:yyyyMMdd}.pdf";
        var output = _dialogs.SaveFile(
            "Kostenblatt (Haltung) speichern",
            "PDF (*.pdf)|*.pdf",
            defaultExt: "pdf",
            defaultFileName: defaultName);
        if (string.IsNullOrWhiteSpace(output))
            return;

        var selection = BuilderPageExportScope.Single(row);
        var qualityHint = row.HasDetailedCost
            ? "Diese Haltung hat Positionsdetails."
            : "Diese Haltung hat keine Positionsdetails (Pauschalwert aus Tabelle).";
        var filterSummary = $"Einzelne Haltung: {row.Holding}";

        await RenderCostSummaryPdfAsync(selection, filterSummary, qualityHint, output);
    }



    /// <summary>Volles Haltungsdossier fuer die gewaehlte Haltung — wiederverwendeter DataPage-Ablauf.</summary>
    [RelayCommand]
    private async Task PrintSingleDossierAsync()
    {
        var row = SelectedRow;
        if (row is null)
        {
            _dialogs.Info("Bitte zuerst eine Haltung in der Tabelle waehlen.", "Dossier");
            return;
        }

        await EnsurePrintController().PrintDossierPdfAsync(_shell.Project, row.Record);
    }

    /// <summary>Baut das Kosten-Summary-PDF fuer die uebergebene Auswahl (alle oder eine Haltung).</summary>
    private async Task RenderCostSummaryPdfAsync(
        BuilderPageExportSelection selection,
        string filterSummary,
        string qualityHint,
        string output)
    {
        IsPdfExportInProgress = true;
        PdfExportProgress = "PDF wird vorbereitet...";

        try
        {
            await Task.Yield();
            var rows = selection.Rows;
            var entries = BuilderPageSummaryEntryBuilder.Build(rows, _vatRate);
            var dataLines = IncludeDataSection ? BuilderPageHoldingDataLineBuilder.Build(rows) : null;

            var projectMeta = _shell.Project.Metadata;
            var projectCustomer = BuilderPagePdfBlockBuilder.BuildProjectCustomerBlock(projectMeta);
            var objectBlock = BuilderPagePdfBlockBuilder.BuildObjectBlock(projectMeta, rows.Count);
            var textBlocks = new List<string>
            {
                qualityHint,
                "Die Statistik fuer Inliner/Manschetten basiert auf vorhandenen Positionsdetails.",
                "Kostenzusammenstellung nach Eigentuemer und Gesamtpositionen ist im Ausdruck enthalten."
            };
            var vatMismatchHint = BuildVatMismatchHint(entries, _vatRate);
            if (vatMismatchHint.Length > 0)
                textBlocks.Insert(1, vatMismatchHint);

            var ctx = new OfferPdfContext
            {
                ProjectTitle = "Abwasser Uri - Druckcenter",
                VariantTitle = selection.VariantTitle,
                CustomerBlock = projectCustomer,
                ObjectBlock = objectBlock,
                FilterSummaryText = filterSummary,
                Currency = "CHF",
                OfferNo = "",
                TextBlocks = textBlocks
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
            LastExportScopeSummary = BuildExportScopeSummary(rows);
            IsLastExportCurrent = true;
            _lastExportProjectPath = _settings.LastProjectPath ?? "";
            LastResult = $"PDF erstellt: {Path.GetFileName(output)}";
            _shell.SetStatus("Druckcenter PDF erstellt");
            PdfExportProgress = "PDF fertig.";
            _dialogs.Info(
                $"Druckcenter-PDF wurde erstellt:\n{output}",
                "Druckcenter");
        }
        catch (Exception ex)
        {
            var userMessage = UserError.DescribeAndReport(ex, "Druckcenter PDF erstellen");
            LastResult = $"Fehler: {userMessage}";
            PdfExportProgress = "PDF-Erstellung fehlgeschlagen.";
            _dialogs.Error(
                $"PDF konnte nicht erstellt werden:\n{userMessage}",
                "Druckcenter");
        }
        finally
        {
            IsPdfExportInProgress = false;
        }
    }

    /// <summary>Dossier-Druckcontroller lazy aufbauen (gleiche Provider wie die Datenseite).</summary>
    private DataPagePrintController EnsurePrintController()
        => _printController ??= new DataPagePrintController(
            _dialogs,
            _protocolPdfExporter,
            () => _shell.GetProjectFolder(),
            _pdfMerge,
            _costRepo,
            buildHydraulikCalculation: record => DataPageHydraulikReportCalculator.BuildReportCalculation(
                record,
                _settings.HydraulikPanel,
                saveSettings: _settings.Save),
            getLastProjectPath: () => _settings.LastProjectPath,
            findSchachtByNummer: FindSchachtByNummer,
            dossierPhotoAvailability: _dossierPhotoAvailability,
            inspectionProtocolFiles: _inspectionProtocolFiles,
            buildDossierHydraulikCalculation: (record, dn) => DataPageHydraulikReportCalculator.BuildReportCalculation(
                record,
                _settings.HydraulikPanel,
                dn,
                saveSettings: _settings.Save));

    private SchachtRecord? FindSchachtByNummer(string? nummer)
    {
        if (string.IsNullOrWhiteSpace(nummer))
            return null;

        return _shell.Project.SchaechteData.FirstOrDefault(s =>
            string.Equals(s.GetFieldValue("Schachtnummer"), nummer, StringComparison.OrdinalIgnoreCase));
    }

    /// <summary>
    /// Exportiert EIN NPK-Leistungsverzeichnis ueber alle gefilterten Haltungen:
    /// gleiche NPK-Position wird zusammengezaehlt (ByDN-Positionen je DN getrennt),
    /// als CSV (Semikolon, gruppiert nach NPK-Kapitel, mit Zwischentotalen).
    /// </summary>

    [RelayCommand]
    private async Task ExportNpkLeistungsverzeichnisAsync()
    {
        if (IsPdfExportInProgress)
            return;

        var prep = PrepareLvPositions();
        if (prep is null)
            return;

        var safeProjectName = SanitizeFilePart(_shell.Project.Name);
        var defaultName = $"NPK-Leistungsverzeichnis_AWU_{safeProjectName}_{DateTime.Now:yyyyMMdd}.csv";
        var output = _dialogs.SaveFile(
            "NPK-Leistungsverzeichnis speichern",
            "CSV (*.csv)|*.csv",
            defaultExt: "csv",
            defaultFileName: defaultName);
        if (string.IsNullOrWhiteSpace(output))
            return;

        var positions = prep.Positions.ToList();
        var projectName = _shell.Project.Name;
        var standHinweis = BuildLvStandHinweis();
        IsPdfExportInProgress = true;
        PdfExportProgress = "NPK-CSV wird erstellt...";

        try
        {
            await BackgroundFileExportRunner.RunAsync(() =>
            {
                var csv = NpkLeistungsverzeichnisExporter.BuildCsv(
                    positions,
                    "CHF",
                    prep.ExcludedTotal,
                    prep.ExcludedCount,
                    projectName);
                AtomicTextFileWriter.WriteAllText(
                    output,
                    csv,
                    new UTF8Encoding(encoderShouldEmitUTF8Identifier: true));
            });

            LastResult = $"Leistungsverzeichnis erstellt: {Path.GetFileName(output)} ({positions.Count} Positionen)";
            _shell.SetStatus("NPK-Leistungsverzeichnis erstellt");
            PdfExportProgress = "NPK-CSV fertig.";
            _dialogs.Info(
                $"NPK-Leistungsverzeichnis wurde erstellt:\n{output}\n\n{positions.Count} Positionen — " +
                $"nur Eigentum Abwasser Uri (AWU); Private werden separat abgehandelt.{standHinweis}",
                "Druckcenter");
        }
        catch (Exception ex)
        {
            var userMessage = UserError.DescribeAndReport(ex, "Druckcenter Leistungsverzeichnis erstellen");
            LastResult = $"Fehler: {userMessage}";
            PdfExportProgress = "NPK-CSV fehlgeschlagen.";
            _dialogs.Error($"Leistungsverzeichnis konnte nicht erstellt werden:\n{userMessage}", "Druckcenter");
        }
        finally
        {
            IsPdfExportInProgress = false;
        }
    }

    /// <summary>
    /// NPK-Leistungsverzeichnis als formatiertes Excel: zwei Reiter — "Zum Ausfüllen"
    /// (leere Einheitspreise, Totale als Formeln) und "Kalkulation (intern)" mit den
    /// eigenen Schätzpreisen. Nutzt dieselbe Positions-Aggregation wie der CSV-Export.
    /// </summary>
    [RelayCommand]
    private async Task ExportNpkLeistungsverzeichnisExcelAsync()
    {
        if (IsPdfExportInProgress)
            return;

        var prep = PrepareLvPositions();
        if (prep is null)
            return;

        var safeProjectName = SanitizeFilePart(_shell.Project.Name);
        var defaultName = $"NPK-Leistungsverzeichnis_AWU_{safeProjectName}_{DateTime.Now:yyyyMMdd}.xlsx";
        var output = _dialogs.SaveFile(
            "NPK-Leistungsverzeichnis (Excel) speichern",
            "Excel (*.xlsx)|*.xlsx",
            defaultExt: "xlsx",
            defaultFileName: defaultName);
        if (string.IsNullOrWhiteSpace(output))
            return;

        var positions = prep.Positions.ToList();
        var projectName = _shell.Project.Name;
        var vatRate = _vatRate;
        var standHinweis = BuildLvStandHinweis();
        IsPdfExportInProgress = true;
        PdfExportProgress = "NPK-Excel wird erstellt...";

        try
        {
            await BackgroundFileExportRunner.RunAsync(() =>
            {
                var bytes = _npkExcelExporter.BuildWorkbook(
                    positions,
                    "CHF",
                    vatRate,
                    projectName,
                    prep.ExcludedTotal,
                    prep.ExcludedCount);
                File.WriteAllBytes(output, bytes);
            });

            LastResult = $"Leistungsverzeichnis (Excel) erstellt: {Path.GetFileName(output)} ({positions.Count} Positionen)";
            _shell.SetStatus("NPK-Leistungsverzeichnis (Excel) erstellt");
            PdfExportProgress = "NPK-Excel fertig.";
            _dialogs.Info(
                $"Excel-Leistungsverzeichnis wurde erstellt:\n{output}\n\n" +
                $"Reiter 'Zum Ausfüllen' (leere Preise für die Firma) + 'Kalkulation (intern)'.\n" +
                $"{positions.Count} Positionen — nur Eigentum Abwasser Uri (AWU); Private werden separat abgehandelt.{standHinweis}",
                "Druckcenter");
        }
        catch (Exception ex)
        {
            var userMessage = UserError.DescribeAndReport(ex, "Druckcenter Excel-Leistungsverzeichnis erstellen");
            LastResult = $"Fehler: {userMessage}";
            PdfExportProgress = "NPK-Excel fehlgeschlagen.";
            _dialogs.Error($"Excel-Leistungsverzeichnis konnte nicht erstellt werden:\n{userMessage}", "Druckcenter");
        }
        finally
        {
            IsPdfExportInProgress = false;
        }
    }

    /// <summary>Gemeinsame Positions-Aufbereitung fuer CSV- und Excel-LV. null = abgebrochen/leer (Hinweis wurde gezeigt).</summary>
    private LvPrep? PrepareLvPositions()
    {
        RefreshData();
        var filteredRows = Rows.ToList();
        if (filteredRows.Count == 0)
        {
            _dialogs.Info("Keine Daten fuer den aktuellen Filter gefunden.", "Druckcenter");
            return null;
        }

        if (!OfferRecomputeCostsForCurrentCatalog(filteredRows))
            return null;

        filteredRows = Rows.ToList();
        var holdingSelection = BuilderPageLvPreparationService.SelectAwuHoldings(filteredRows, _vatRate);
        var includePauschalen = holdingSelection.FallbackHoldings.Count > 0 && _dialogs.ConfirmWarn(
            "Pauschalen ohne echte NPK-Position im Leistungsverzeichnis ausweisen?\n\n" +
            "Ja = als uebrige Positionen aufnehmen.\nNein = im LV weglassen und unten als nicht enthaltene Pauschalkosten ausweisen.",
            "NPK-Leistungsverzeichnis",
            defaultNo: true);

        var projectPath = _settings.LastProjectPath ?? "";
        var catalog = _catalogStore.LoadMerged(projectPath);
        var catalogDict = BuildCatalogMap(catalog);

        // Schacht-Matrix-Kosten (NPK Kap. 700) mit ins projektweite LV nehmen. Fehlerhafte Datei
        // blockiert das Haltungs-LV nicht, wird aber als Warnung gemeldet (Schaechte fehlen dann).
        var schachtCosts = SchachtLvCostLoader.LoadForLv(projectPath, out var schachtLoadError);
        if (schachtLoadError is not null)
            _dialogs.Warn(
                $"Schacht-Kosten konnten nicht geladen werden und fehlen im Leistungsverzeichnis:\n{schachtLoadError}",
                "Druckcenter");
        // Schacht-Positionen (NPK Kap. 700) nur fuer Schaechte im Eigentum AWU.
        var prep = BuilderPageLvPreparationService.Build(
            holdingSelection,
            includePauschalen,
            schachtCosts,
            BuildAwuSchachtKeys(),
            catalogDict);
        if (prep.Positions.Count == 0 && prep.ExcludedTotal <= 0m)
        {
            _dialogs.Info(
                "Keine AWU-Positionen gefunden. Es gibt keine Haltungen/Schaechte im Eigentum von Abwasser Uri (AWU) " +
                "mit Massnahmen-Positionen. Private werden separat/einzeln abgehandelt.",
                "Druckcenter");
            return null;
        }

        return new LvPrep(prep.Positions, prep.ExcludedTotal, prep.ExcludedCount, prep.HoldingCount);
    }

    // Audit W14: Der Export liest costs.json von Platte — den Daten-Stand ehrlich benennen,
    // sonst druckt man nach Matrix-Aenderungen kommentarlos den alten Stand.
    private string BuildLvStandHinweis()
        => _shell.Project.Dirty
            ? "\n\nACHTUNG: Es gibt ungespeicherte Aenderungen im Projekt — das LV entspricht dem zuletzt GESPEICHERTEN Stand der Sanierungs- und Schacht-Matrix."
            : "\n\nDaten-Stand: zuletzt gespeicherte Sanierungs-Matrix (costs.json) und Schacht-Matrix (schacht_costs.json).";

    private sealed record LvPrep(
        IReadOnlyList<AggregatedPosition> Positions,
        decimal ExcludedTotal,
        int ExcludedCount,
        int HoldingCount);

    /// <summary>
    /// Menge der Schacht-Nummern (normalisiert) im Eigentum AWU — fuer den AWU-Schacht-Filter
    /// im NPK-135-LV. Schaechte ohne AWU-Eigentum werden nicht ins Sammel-LV genommen.
    /// </summary>
    private HashSet<string> BuildAwuSchachtKeys()
    {
        var pairs = _shell.Project.SchaechteData
            .Select(s => ((string?)s.GetFieldValue("Schachtnummer"), SchachtOwner(s)));
        return OwnershipAwuFilter.AwuSchachtKeys(pairs);
    }

    /// <summary>
    /// Schacht-Eigentuemer tolerant lesen: WinCan schreibt "Eigentümer" (mit Umlaut),
    /// das Grid/die Schacht-Seite "Eigentuemer" (ASCII).
    /// </summary>
    private static string? SchachtOwner(SchachtRecord schacht)
    {
        var value = schacht.GetFieldValue("Eigentuemer");
        if (string.IsNullOrWhiteSpace(value))
            value = schacht.GetFieldValue("Eigentümer");
        return value;
    }

    [RelayCommand]
    private async Task ExportNpkOfferPdfAsync()
    {
        if (IsPdfExportInProgress)
            return;

        RefreshData();
        var filteredRows = Rows.ToList();
        if (filteredRows.Count == 0)
        {
            _dialogs.Info("Keine Daten fuer den aktuellen Filter gefunden.", "NPK-Offerte");
            return;
        }

        if (!OfferRecomputeCostsForCurrentCatalog(filteredRows))
            return;

        filteredRows = Rows.ToList();
        var entries = BuilderPageSummaryEntryBuilder.Build(filteredRows, _vatRate);
        // NPK-135-Offerte nur fuer Haltungen im Eigentum AWU (Private separat).
        var holdings = entries
            .Where(e => e.Cost is not null && OwnershipAwuFilter.IsAwu(e.Owner))
            .Select(e => e.Cost)
            .ToList();
        var pauschaleHoldings = holdings
            .Where(TablePauschaleCostHelper.IsFallbackPauschale)
            .ToList();
        var holdingsForOffer = holdings
            .Where(h => !TablePauschaleCostHelper.IsFallbackPauschale(h))
            .ToList();
        var excludedPauschaleTotal = pauschaleHoldings.Sum(h => h.Total);

        var projectPath = _settings.LastProjectPath ?? "";
        var catalog = _catalogStore.LoadMerged(projectPath);
        // Schacht-Kosten (NPK Kap. 700) auch in die NPK-Offerte aufnehmen.
        var schachtCosts = SchachtLvCostLoader.LoadForLv(projectPath, out var schachtLoadError);
        if (schachtLoadError is not null)
            _dialogs.Warn(
                $"Schacht-Kosten konnten nicht geladen werden und fehlen in der Offerte:\n{schachtLoadError}",
                "NPK-Offerte");
        // Schacht-Positionen (NPK Kap. 700) nur fuer Schaechte im Eigentum AWU.
        var awuSchacht = BuildAwuSchachtKeys();
        var awuSchachtCosts = schachtCosts
            .Where(c => awuSchacht.Contains(OwnershipAwuFilter.NormalizeSchacht(c.Holding)))
            .ToList();
        var holdingsWithSchacht = holdingsForOffer.Concat(awuSchachtCosts).ToList();
        var positions = ProjectPositionAggregator.Aggregate(holdingsWithSchacht, BuildCatalogMap(catalog));
        if (positions.Count == 0)
        {
            var pauschaleHint = excludedPauschaleTotal > 0m
                ? "\n\nEs gibt nur Pauschalkosten ohne NPK-Position; diese koennen nicht als echte NPK-135-Offerte ausgegeben werden."
                : "";
            _dialogs.Info(
                "Keine AWU-Positionen gefunden. Es gibt keine Haltungen/Schaechte im Eigentum AWU mit Massnahmen-Positionen " +
                "(Private werden separat abgehandelt)." + pauschaleHint,
                "NPK-Offerte");
            return;
        }

        var safeProjectName = SanitizeFilePart(_shell.Project.Name);
        var defaultName = $"NPK-Offerte_AWU_{safeProjectName}_{DateTime.Now:yyyyMMdd}.pdf";
        var output = _dialogs.SaveFile(
            "NPK-Offerte als PDF speichern",
            "PDF (*.pdf)|*.pdf",
            defaultExt: "pdf",
            defaultFileName: defaultName);
        if (string.IsNullOrWhiteSpace(output))
            return;

        IsPdfExportInProgress = true;
        PdfExportProgress = "NPK-Offerte wird vorbereitet...";

        try
        {
            await Task.Yield();
            var projectMeta = _shell.Project.Metadata;
            var ctx = new NpkOfferPdfContext
            {
                ProjectTitle = "NPK-135-Offerte Kanalsanierung",
                VariantTitle = _shell.Project.Name,
                CustomerBlock = BuilderPagePdfBlockBuilder.BuildProjectCustomerBlock(projectMeta),
                ObjectBlock = BuilderPagePdfBlockBuilder.BuildObjectBlock(projectMeta, filteredRows.Count),
                ReferenceBlock = BuildReferenceBlock(projectMeta),
                FilterSummaryText = BuildFilterSummaryText(),
                Currency = "CHF",
                VatRate = catalog.VatRate > 0m ? catalog.VatRate : CostCalculatorLogicService.DefaultVatRate,
                DiscountPercent = 0m,
                SkontoPercent = 0m
            };

            var model = NpkOfferPdfModelFactory.Create(
                positions,
                ctx,
                DateTimeOffset.Now,
                excludedPauschaleTotal,
                pauschaleHoldings.Count);

            var templatePath = Path.Combine(AppContext.BaseDirectory, "Templates", "npk_offer.sbnhtml");
            var logoPath = Path.Combine(AppContext.BaseDirectory, "Assets", "Brand", "abwasser-uri-logo.png");
            var renderer = new OfferHtmlToPdfRenderer();
            PdfExportProgress = "NPK-Offerte wird gerendert...";
            await renderer.RenderAsync(model, templatePath, output, logoPath);

            LastExportedPdfPath = output;
            LastExportedAt = DateTimeOffset.Now;
            LastExportScopeSummary = BuildExportScopeSummary(filteredRows);
            IsLastExportCurrent = true;
            _lastExportProjectPath = _settings.LastProjectPath ?? "";
            LastResult = $"NPK-Offerte erstellt: {Path.GetFileName(output)}";
            _shell.SetStatus("NPK-Offerte erstellt");
            PdfExportProgress = "NPK-Offerte fertig.";
            _dialogs.Info($"NPK-Offerte wurde erstellt:\n{output}", "NPK-Offerte");
        }
        catch (Exception ex)
        {
            var userMessage = UserError.DescribeAndReport(ex, "Druckcenter NPK-Offerte erstellen");
            LastResult = $"Fehler: {userMessage}";
            PdfExportProgress = "NPK-Offerte fehlgeschlagen.";
            _dialogs.Error($"NPK-Offerte konnte nicht erstellt werden:\n{userMessage}", "NPK-Offerte");
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
                var decision = _dialogs.ConfirmCancel(
                    "Der Druckstand hat sich seit dem letzten Export geaendert.\n\nJa = letztes PDF drucken\nNein = anderes PDF auswaehlen\nAbbrechen = nichts tun",
                    "Druckcenter");

                if (decision == DialogConfirm.Cancel)
                    return;

                if (decision == DialogConfirm.Yes)
                    pdfPath = LastExportedPdfPath;
            }
        }

        pdfPath ??= _dialogs.OpenFile("PDF zum Drucken waehlen", "PDF (*.pdf)|*.pdf");

        if (string.IsNullOrWhiteSpace(pdfPath))
            return;

        try
        {
            var psi = new ProcessStartInfo
            {
                FileName = pdfPath,
                Verb = "print",
                UseShellExecute = true
            };
            Process.Start(psi);
            LastResult = $"Druckauftrag gestartet: {pdfPath}";
            _shell.SetStatus("PDF-Druckauftrag gestartet");
        }
        catch (Exception ex)
        {
            var userMessage = UserError.DescribeAndReport(ex, "Druckcenter PDF drucken");
            LastResult = $"Fehler beim Drucken: {userMessage}";
            _dialogs.Error(
                $"PDF konnte nicht gedruckt werden:\n{userMessage}",
                "Druckcenter");
        }
    }

    [RelayCommand(CanExecute = nameof(CanOpenLastExportedPdf))]
    private void OpenLastExportedPdf()
    {
        if (!HasLastExportedPdf())
        {
            ClearLastExport("Die zuletzt exportierte PDF-Datei wurde nicht gefunden.");
            _dialogs.Info(
                "Die zuletzt exportierte PDF-Datei wurde nicht gefunden.",
                "Druckcenter");
            return;
        }

        if (AuswertungPro.Next.UI.Services.SafeShellOpen.TryOpen(LastExportedPdfPath, out var error))
        {
            LastResult = $"PDF geoeffnet: {Path.GetFileName(LastExportedPdfPath)}";
            return;
        }

        LastResult = $"Fehler beim Oeffnen: {error}";
        _dialogs.Error(
            $"PDF konnte nicht geoeffnet werden:\n{error}",
            "Druckcenter");
    }

}
