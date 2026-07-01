using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using AuswertungPro.Next.Application.Reports;
using AuswertungPro.Next.Domain.Models;
using AuswertungPro.Next.Domain.Protocol;
using AuswertungPro.Next.Infrastructure.Costs;
using AuswertungPro.Next.Infrastructure.Media;
using AuswertungPro.Next.UI.Views.Windows;

namespace AuswertungPro.Next.UI.DataPage;

public sealed record DataPageDossierPrintAvailability(
    bool HasSchachtVon,
    string? SchachtVonNr,
    bool HasSchachtBis,
    string? SchachtBisNr,
    bool HydraulikAvailable,
    bool KostenAvailable,
    int OriginalPdfCount);

public sealed class DataPagePrintController
{
    private readonly IDialogService _dialogs;
    private readonly Func<string?> _getProjectFolder;
    private readonly Func<string?> _getLastProjectPath;
    private readonly Func<Project, HaltungRecord, ProtocolDocument, string, HaltungsprotokollPdfOptions, byte[]> _buildAwuPdf;
    private readonly Func<HaltungRecord, HydraulikCalcResult?> _buildHydraulikCalculation;
    private readonly Func<HydraulikPrintOptions?> _selectHydraulikPrintOptions;
    private readonly Func<HaltungRecord, HydraulikCalcResult, HydraulikPrintOptions, Task<byte[]>> _buildHydraulikPdfAsync;
    private readonly Func<string, (string? VonNr, string? BisNr)> _splitHoldingNodes;
    private readonly Func<string?, SchachtRecord?> _findSchachtByNummer;
    private readonly Func<HaltungRecord, DataPageHydraulikAvailability> _readDossierHydraulikAvailability;
    private readonly Func<HaltungRecord, double?, HydraulikCalcResult?> _buildDossierHydraulikCalculation;
    private readonly Func<string, HoldingCost?> _findHoldingCost;
    private readonly Func<HaltungRecord, string, SchachtRecord?, SchachtRecord?, List<string>> _resolveDossierOriginalPdfPaths;
    private readonly Func<DataPageDossierPrintAvailability, DossierPrintOptions?> _selectDossierPrintOptions;
    private readonly Func<Project, HaltungRecord, SchachtRecord?, SchachtRecord?, HydraulikCalcResult?, string, DossierPrintOptions, Task<byte[]>> _buildDossierPdfAsync;
    private readonly Func<IReadOnlyList<string>, byte[]> _mergeOriginals;
    private readonly Func<byte[], IReadOnlyList<string>, byte[]> _mergeWithOriginals;
    private readonly string _baseDirectory;
    private readonly Func<string, bool> _fileExists;
    private readonly Action<string, byte[]> _writeAllBytes;
    private readonly Func<string, byte[], Task> _writeAllBytesAsync;
    private readonly Func<DateTime> _now;

    public DataPagePrintController(
        IDialogService dialogs,
        ProtocolPdfExporter protocolPdfExporter,
        Func<string?> getProjectFolder,
        Func<HaltungRecord, HydraulikCalcResult?>? buildHydraulikCalculation = null,
        Func<string?>? getLastProjectPath = null,
        Func<string?, SchachtRecord?>? findSchachtByNummer = null,
        Func<HaltungRecord, double?, HydraulikCalcResult?>? buildDossierHydraulikCalculation = null)
        : this(
            dialogs,
            getProjectFolder,
            CreateBuildAwuPdf(protocolPdfExporter),
            AppContext.BaseDirectory,
            buildHydraulikCalculation: buildHydraulikCalculation,
            getLastProjectPath: getLastProjectPath,
            findSchachtByNummer: findSchachtByNummer,
            buildDossierHydraulikCalculation: buildDossierHydraulikCalculation)
    {
    }

    public DataPagePrintController(
        IDialogService dialogs,
        Func<string?> getProjectFolder,
        Func<Project, HaltungRecord, ProtocolDocument, string, HaltungsprotokollPdfOptions, byte[]> buildAwuPdf,
        string baseDirectory,
        Func<string, bool>? fileExists = null,
        Action<string, byte[]>? writeAllBytes = null,
        Func<string, byte[], Task>? writeAllBytesAsync = null,
        Func<DateTime>? now = null,
        Func<HaltungRecord, HydraulikCalcResult?>? buildHydraulikCalculation = null,
        Func<HydraulikPrintOptions?>? selectHydraulikPrintOptions = null,
        Func<HaltungRecord, HydraulikCalcResult, HydraulikPrintOptions, Task<byte[]>>? buildHydraulikPdfAsync = null,
        Func<string?>? getLastProjectPath = null,
        Func<string, (string? VonNr, string? BisNr)>? splitHoldingNodes = null,
        Func<string?, SchachtRecord?>? findSchachtByNummer = null,
        Func<HaltungRecord, DataPageHydraulikAvailability>? readDossierHydraulikAvailability = null,
        Func<HaltungRecord, double?, HydraulikCalcResult?>? buildDossierHydraulikCalculation = null,
        Func<string, HoldingCost?>? findHoldingCost = null,
        Func<HaltungRecord, string, SchachtRecord?, SchachtRecord?, List<string>>? resolveDossierOriginalPdfPaths = null,
        Func<DataPageDossierPrintAvailability, DossierPrintOptions?>? selectDossierPrintOptions = null,
        Func<Project, HaltungRecord, SchachtRecord?, SchachtRecord?, HydraulikCalcResult?, string, DossierPrintOptions, Task<byte[]>>? buildDossierPdfAsync = null,
        Func<IReadOnlyList<string>, byte[]>? mergeOriginals = null,
        Func<byte[], IReadOnlyList<string>, byte[]>? mergeWithOriginals = null)
    {
        _dialogs = dialogs ?? throw new ArgumentNullException(nameof(dialogs));
        _getProjectFolder = getProjectFolder ?? throw new ArgumentNullException(nameof(getProjectFolder));
        _getLastProjectPath = getLastProjectPath ?? (() => null);
        _buildAwuPdf = buildAwuPdf ?? throw new ArgumentNullException(nameof(buildAwuPdf));
        _baseDirectory = string.IsNullOrWhiteSpace(baseDirectory) ? AppContext.BaseDirectory : baseDirectory;
        _fileExists = fileExists ?? File.Exists;
        _writeAllBytes = writeAllBytes ?? File.WriteAllBytes;
        _writeAllBytesAsync = writeAllBytesAsync ?? ((path, bytes) => Task.Run(() => _writeAllBytes(path, bytes)));
        _now = now ?? (() => DateTime.Now);
        _buildHydraulikCalculation = buildHydraulikCalculation ?? (_ => null);
        _selectHydraulikPrintOptions = selectHydraulikPrintOptions ?? SelectHydraulikPrintOptionsWithDialog;
        _buildHydraulikPdfAsync = buildHydraulikPdfAsync
            ?? ((record, calc, options) => Task.Run(() => HydraulikPdfBuilder.Build(record, calc, options)));
        _splitHoldingNodes = splitHoldingNodes ?? SplitHoldingNodes;
        _findSchachtByNummer = findSchachtByNummer ?? (_ => null);
        _readDossierHydraulikAvailability = readDossierHydraulikAvailability ?? DataPageHydraulikReportCalculator.ReadAvailability;
        _buildDossierHydraulikCalculation = buildDossierHydraulikCalculation ?? ((_, _) => null);
        _findHoldingCost = findHoldingCost ?? FindHoldingCostFromProjectStore;
        _resolveDossierOriginalPdfPaths = resolveDossierOriginalPdfPaths ?? ResolveDossierOriginalPdfPaths;
        _selectDossierPrintOptions = selectDossierPrintOptions ?? SelectDossierPrintOptionsWithDialog;
        _buildDossierPdfAsync = buildDossierPdfAsync
            ?? ((project, record, schachtVon, schachtBis, calc, projectRoot, options) =>
                Task.Run(() => HaltungsDossierPdfBuilder.Build(project, record, schachtVon, schachtBis, calc, projectRoot, options)));
        _mergeOriginals = mergeOriginals ?? PdfMergeHelper.MergeOriginals;
        _mergeWithOriginals = mergeWithOriginals ?? PdfMergeHelper.MergeWithOriginals;
    }

    public async Task PrintDossierPdfAsync(Project project, HaltungRecord? record)
    {
        ArgumentNullException.ThrowIfNull(project);

        if (record is null)
        {
            _dialogs.Info("Bitte zuerst eine Haltung auswaehlen.", "Dossier");
            return;
        }

        var holdingLabel = record.GetFieldValue("Haltungsname") ?? "";
        var (vonNr, bisNr) = _splitHoldingNodes(holdingLabel);
        var schachtVon = _findSchachtByNummer(vonNr);
        var schachtBis = _findSchachtByNummer(bisNr);

        var hydraulikAvailability = _readDossierHydraulikAvailability(record);
        var hydraulikAvailable = hydraulikAvailability.IsAvailable;

        var projectFolder = _getProjectFolder() ?? "";
        var holdingCost = _findHoldingCost(holdingLabel.Trim());
        var kostenField = record.GetFieldValue("Kosten");
        var kostenAvailable = holdingCost?.Measures is { Count: > 0 }
            || !string.IsNullOrWhiteSpace(kostenField)
            || !string.IsNullOrWhiteSpace(record.GetFieldValue("Empfohlene_Sanierungsmassnahmen"));

        var originalPdfPaths = _resolveDossierOriginalPdfPaths(record, projectFolder, schachtVon, schachtBis);
        var availability = new DataPageDossierPrintAvailability(
            schachtVon is not null,
            vonNr,
            schachtBis is not null,
            bisNr,
            hydraulikAvailable,
            kostenAvailable,
            originalPdfPaths.Count);

        var selectedOptions = _selectDossierPrintOptions(availability);
        if (selectedOptions is null)
            return;

        var defaultName = $"Dossier_{SanitizeFilenamePart(holdingLabel)}_{_now():yyyyMMdd}.pdf";
        var output = _dialogs.SaveFile(
            "Haltungsdossier als PDF speichern",
            "PDF (*.pdf)|*.pdf",
            defaultExt: "pdf",
            defaultFileName: defaultName);
        if (string.IsNullOrWhiteSpace(output))
            return;

        try
        {
            HydraulikCalcResult? calcResult = null;
            if (selectedOptions.IncludeHydraulik && hydraulikAvailable)
                calcResult = _buildDossierHydraulikCalculation(record, hydraulikAvailability.DnMm);

            var logoPath = Path.Combine(_baseDirectory, "Assets", "Brand", "abwasser-uri-logo.png");
            var options = selectedOptions with
            {
                LogoPathAbs = _fileExists(logoPath) ? logoPath : null,
                HoldingCost = selectedOptions.IncludeKostenschaetzung ? holdingCost : null,
                OriginalPdfPaths = selectedOptions.IncludeOriginalProtokolle ? originalPdfPaths : null,
            };

            var printableSections = DataPageDossierAvailability.EvaluatePrintableSections(
                options,
                record,
                projectFolder,
                hasSchachtVon: schachtVon is not null,
                hasSchachtBis: schachtBis is not null,
                hasHydraulikResult: calcResult is not null,
                kostenAvailable,
                originalPdfPaths.Count);
            if (!printableSections.HasAnySection)
            {
                _dialogs.Info(
                    "Die ausgewaehlte Kombination enthaelt keine druckbaren Inhalte.",
                    "Dossier");
                return;
            }

            var originalsAlreadyMerged = false;
            byte[] pdf;
            if (printableSections.HasDossierBaseSection)
            {
                pdf = await _buildDossierPdfAsync(project, record, schachtVon, schachtBis, calcResult, projectFolder, options);
            }
            else
            {
                pdf = await Task.Run(() => _mergeOriginals(originalPdfPaths));
                if (pdf.Length == 0)
                    throw new InvalidOperationException("Die Original-Protokolle konnten nicht zusammengefuehrt werden.");

                originalsAlreadyMerged = true;
            }

            if (!originalsAlreadyMerged && options.IncludeOriginalProtokolle && originalPdfPaths.Count > 0)
                pdf = await Task.Run(() => _mergeWithOriginals(pdf, originalPdfPaths));

            await _writeAllBytesAsync(output, pdf);
            _dialogs.Info($"Dossier wurde erstellt:\n{output}", "Dossier");
        }
        catch (Exception ex)
        {
            _dialogs.Error($"Dossier konnte nicht erstellt werden:\n{ex.Message}", "Dossier");
        }
    }

    public async Task PrintHydraulikPdfAsync(HaltungRecord? record)
    {
        if (record is null)
        {
            _dialogs.Info("Bitte zuerst eine Haltung auswaehlen.", "Hydraulik PDF");
            return;
        }

        var calc = _buildHydraulikCalculation(record);
        if (calc is null)
        {
            _dialogs.Warn("Hydraulik-Berechnung konnte nicht durchgefuehrt werden.\nBitte DN und Gefaelle pruefen.", "Hydraulik PDF");
            return;
        }

        var selectedOptions = _selectHydraulikPrintOptions();
        if (selectedOptions is null)
            return;

        var holding = record.GetFieldValue("Haltungsname") ?? "Haltung";
        var defaultName = $"Hydraulik_{SanitizeFilenamePart(holding)}_{_now():yyyyMMdd}.pdf";
        var output = _dialogs.SaveFile(
            "Hydraulik-Bericht als PDF speichern",
            "PDF (*.pdf)|*.pdf",
            defaultExt: "pdf",
            defaultFileName: defaultName);
        if (string.IsNullOrWhiteSpace(output))
            return;

        try
        {
            var logoPath = Path.Combine(_baseDirectory, "Assets", "Brand", "abwasser-uri-logo.png");
            var options = selectedOptions with
            {
                LogoPathAbs = _fileExists(logoPath) ? logoPath : null
            };

            var pdf = await _buildHydraulikPdfAsync(record, calc, options);
            await _writeAllBytesAsync(output, pdf);

            _dialogs.Info($"PDF wurde erstellt:\n{output}", "Hydraulik PDF");
        }
        catch (Exception ex)
        {
            _dialogs.Error($"PDF konnte nicht erstellt werden:\n{ex.Message}", "Hydraulik PDF");
        }
    }

    public void PrintAwuHaltungsprotokollPdf(
        Project project,
        HaltungRecord? record,
        Func<HaltungRecord, ProtocolDocument> ensureProtocolDocument)
    {
        ArgumentNullException.ThrowIfNull(project);
        ArgumentNullException.ThrowIfNull(ensureProtocolDocument);

        if (record is null)
        {
            _dialogs.Info("Bitte zuerst eine Haltung auswaehlen.", "Haltungsprotokoll AWU");
            return;
        }

        var projectFolder = _getProjectFolder() ?? string.Empty;
        if (string.IsNullOrWhiteSpace(projectFolder))
        {
            _dialogs.Info(
                "Projekt bitte zuerst speichern — dann wird das Protokoll direkt in den Haltungsordner erzeugt.",
                "Haltungsprotokoll AWU");
            return;
        }

        try
        {
            // Direkt in den Haltungsordner (Haltungen_Verteilt\<H>\...._E.pdf), kein Speichern-Dialog.
            // Gleiche Logik wie "Protokoll neu generieren", nur fuer diese eine Haltung.
            var doc = ensureProtocolDocument(record);
            var dest = AuswertungPro.Next.Infrastructure.Import.ProtocolRegenerationService.RegenerateOne(
                project, projectFolder, record, doc);
            if (string.IsNullOrWhiteSpace(dest))
            {
                _dialogs.Info(
                    "Fuer diese Haltung liegt kein Haltungsname vor — der Zielordner kann nicht bestimmt werden.",
                    "Haltungsprotokoll AWU");
                return;
            }

            project.ModifiedAtUtc = DateTime.UtcNow;
            project.Dirty = true;

            // PDF direkt anzeigen; nur wenn das nicht klappt, den Pfad melden.
            var (opened, _) = DataPageOriginalPdfController.TryShellOpen(dest);
            if (!opened)
                _dialogs.Info($"AWU-Haltungsprotokoll wurde erstellt:\n{dest}", "Haltungsprotokoll AWU");
        }
        catch (Exception ex)
        {
            _dialogs.Error($"AWU-Haltungsprotokoll konnte nicht erstellt werden:\n{ex.Message}", "Haltungsprotokoll AWU");
        }
    }

    private static Func<Project, HaltungRecord, ProtocolDocument, string, HaltungsprotokollPdfOptions, byte[]> CreateBuildAwuPdf(
        ProtocolPdfExporter protocolPdfExporter)
    {
        ArgumentNullException.ThrowIfNull(protocolPdfExporter);

        return (project, record, document, projectRoot, options) =>
            protocolPdfExporter.BuildHaltungsprotokollPdf(project, record, document, projectRoot, options);
    }

    private static HydraulikPrintOptions? SelectHydraulikPrintOptionsWithDialog()
    {
        var dialog = new HydraulikPrintDialog
        {
            Owner = System.Windows.Application.Current?.MainWindow
        };

        return dialog.ShowDialog() == true ? dialog.SelectedOptions : null;
    }

    private static (string? VonNr, string? BisNr) SplitHoldingNodes(string holdingLabel)
    {
        var (vonNr, bisNr) = ProtocolPdfExporter.SplitHoldingNodes(holdingLabel);
        return (vonNr, bisNr);
    }

    private HoldingCost? FindHoldingCostFromProjectStore(string holdingLabel)
    {
        var projectPath = _getLastProjectPath();
        if (string.IsNullOrWhiteSpace(projectPath))
            return null;

        var store = new ProjectCostStoreRepository().Load(projectPath);
        return store.ByHolding.TryGetValue(holdingLabel.Trim(), out var cost) ? cost : null;
    }

    private static List<string> ResolveDossierOriginalPdfPaths(
        HaltungRecord record,
        string projectFolder,
        SchachtRecord? schachtVon,
        SchachtRecord? schachtBis)
    {
        var paths = DataPageProtocolPathResolver.ResolveOriginalPdfPaths(record, projectFolder);
        if (schachtVon is not null)
            DataPageProtocolPathResolver.ResolveSchachtPdfPaths(schachtVon, projectFolder, paths);
        if (schachtBis is not null)
            DataPageProtocolPathResolver.ResolveSchachtPdfPaths(schachtBis, projectFolder, paths);

        return paths;
    }

    private static DossierPrintOptions? SelectDossierPrintOptionsWithDialog(DataPageDossierPrintAvailability availability)
    {
        var dialog = new DossierPrintDialog
        {
            Owner = System.Windows.Application.Current?.MainWindow
        };
        dialog.SetAvailability(
            availability.HasSchachtVon,
            availability.SchachtVonNr,
            availability.HasSchachtBis,
            availability.SchachtBisNr,
            availability.HydraulikAvailable,
            availability.KostenAvailable,
            availability.OriginalPdfCount);

        return dialog.ShowDialog() == true ? dialog.SelectedOptions : null;
    }

    private static string SanitizeFilenamePart(string? text)
    {
        if (string.IsNullOrWhiteSpace(text))
            return "unknown";

        foreach (var c in Path.GetInvalidFileNameChars())
            text = text.Replace(c, '_');

        return text.Trim();
    }
}
