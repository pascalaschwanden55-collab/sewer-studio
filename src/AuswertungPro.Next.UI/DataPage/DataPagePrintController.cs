using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using AuswertungPro.Next.Application.Common;
using AuswertungPro.Next.Application.Costs;
using AuswertungPro.Next.Application.DataPage;
using AuswertungPro.Next.Application.Import;
using AuswertungPro.Next.Application.Reports;
using AuswertungPro.Next.Domain.Models;
using AuswertungPro.Next.Domain.Protocol;
using AuswertungPro.Next.Infrastructure.Media;
using AuswertungPro.Next.UI.Services;
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
    private readonly IPdfMergeService _pdfMerge;
    private readonly string _baseDirectory;
    private readonly Func<string, bool> _fileExists;
    private readonly Action<string, byte[]> _writeAllBytes;
    private readonly Func<string, byte[], Task> _writeAllBytesAsync;
    private readonly Func<DateTime> _now;
    // AWU-Einzeldruck: erzeugt das _E-Protokoll in den Haltungsordner (Rueckgabe = Zielpfad, null wenn kein Haltungsname)
    private readonly Func<Project, string, HaltungRecord, ProtocolDocument, string?> _regenerateOne;
    private readonly Func<string, bool> _openPdf;
    private readonly IDossierPhotoAvailabilityService _dossierPhotoAvailability;
    private readonly IInspectionProtocolFileLocator _inspectionProtocolFiles;
    private readonly IProjectCostStoreRepository _projectCosts;
    private readonly IProtocolPdfLayoutSettings? _protocolPdfLayoutSettings;

    [Obsolete("Kompatibilitaetskonstruktor. Neue Aufrufer muessen einen sicheren PDF-Oeffner injizieren.")]
    public DataPagePrintController(
        IDialogService dialogs,
        ProtocolPdfExporter protocolPdfExporter,
        Func<string?> getProjectFolder,
        Func<HaltungRecord, HydraulikCalcResult?>? buildHydraulikCalculation = null,
        Func<string?>? getLastProjectPath = null,
        Func<string?, SchachtRecord?>? findSchachtByNummer = null,
        Func<HaltungRecord, double?, HydraulikCalcResult?>? buildDossierHydraulikCalculation = null,
        IProtocolSingleRegenerationService? protocolRegeneration = null,
        IDossierPhotoAvailabilityService? dossierPhotoAvailability = null,
        IInspectionProtocolFileLocator? inspectionProtocolFiles = null)
        : this(
            dialogs,
            (IProtocolPdfExporter)protocolPdfExporter,
            getProjectFolder,
            CreateCompatibilityProjectCosts(),
            path => DataPageOriginalPdfController.TryShellOpen(path).Success,
            buildHydraulikCalculation,
            getLastProjectPath,
            findSchachtByNummer,
            buildDossierHydraulikCalculation,
            protocolRegeneration,
            dossierPhotoAvailability,
            inspectionProtocolFiles)
    {
    }

    [Obsolete("Kompatibilitaetskonstruktor. Neue Aufrufer muessen einen sicheren PDF-Oeffner injizieren.")]
    public DataPagePrintController(
        IDialogService dialogs,
        ProtocolPdfExporter protocolPdfExporter,
        Func<string?> getProjectFolder,
        IPdfMergeService pdfMerge,
        Func<HaltungRecord, HydraulikCalcResult?>? buildHydraulikCalculation = null,
        Func<string?>? getLastProjectPath = null,
        Func<string?, SchachtRecord?>? findSchachtByNummer = null,
        Func<HaltungRecord, double?, HydraulikCalcResult?>? buildDossierHydraulikCalculation = null,
        IProtocolSingleRegenerationService? protocolRegeneration = null,
        IDossierPhotoAvailabilityService? dossierPhotoAvailability = null,
        IInspectionProtocolFileLocator? inspectionProtocolFiles = null)
        : this(
            dialogs,
            (IProtocolPdfExporter)protocolPdfExporter,
            getProjectFolder,
            pdfMerge,
            CreateCompatibilityProjectCosts(),
            path => DataPageOriginalPdfController.TryShellOpen(path).Success,
            buildHydraulikCalculation,
            getLastProjectPath,
            findSchachtByNummer,
            buildDossierHydraulikCalculation,
            protocolRegeneration,
            dossierPhotoAvailability,
            inspectionProtocolFiles)
    {
    }

    internal DataPagePrintController(
        IDialogService dialogs,
        IProtocolPdfExporter protocolPdfExporter,
        Func<string?> getProjectFolder,
        IProjectCostStoreRepository projectCosts,
        Func<string, bool> openPdf,
        Func<HaltungRecord, HydraulikCalcResult?>? buildHydraulikCalculation = null,
        Func<string?>? getLastProjectPath = null,
        Func<string?, SchachtRecord?>? findSchachtByNummer = null,
        Func<HaltungRecord, double?, HydraulikCalcResult?>? buildDossierHydraulikCalculation = null,
        IProtocolSingleRegenerationService? protocolRegeneration = null,
        IDossierPhotoAvailabilityService? dossierPhotoAvailability = null,
        IInspectionProtocolFileLocator? inspectionProtocolFiles = null,
        IProtocolPdfLayoutSettings? protocolPdfLayoutSettings = null)
        : this(
            dialogs,
            getProjectFolder,
            CreateBuildAwuPdf(protocolPdfExporter),
            AppContext.BaseDirectory,
            projectCosts,
            buildHydraulikCalculation: buildHydraulikCalculation,
            getLastProjectPath: getLastProjectPath,
            findSchachtByNummer: findSchachtByNummer,
            buildDossierHydraulikCalculation: buildDossierHydraulikCalculation,
            // Immer ueber den eingebauten Erzeuger: nur so gilt die Einstellung
            // "Fotos pro Seite" auch fuer das neu erzeugte _E-Protokoll.
            regenerateOne: CreateRegenerateOne(protocolRegeneration, protocolPdfExporter),
            openPdf: openPdf,
            dossierPhotoAvailability: dossierPhotoAvailability,
            inspectionProtocolFiles: inspectionProtocolFiles,
            protocolPdfLayoutSettings: protocolPdfLayoutSettings)
    {
    }

    internal DataPagePrintController(
        IDialogService dialogs,
        IProtocolPdfExporter protocolPdfExporter,
        Func<string?> getProjectFolder,
        IPdfMergeService pdfMerge,
        IProjectCostStoreRepository projectCosts,
        Func<string, bool> openPdf,
        Func<HaltungRecord, HydraulikCalcResult?>? buildHydraulikCalculation = null,
        Func<string?>? getLastProjectPath = null,
        Func<string?, SchachtRecord?>? findSchachtByNummer = null,
        Func<HaltungRecord, double?, HydraulikCalcResult?>? buildDossierHydraulikCalculation = null,
        IProtocolSingleRegenerationService? protocolRegeneration = null,
        IDossierPhotoAvailabilityService? dossierPhotoAvailability = null,
        IInspectionProtocolFileLocator? inspectionProtocolFiles = null,
        IProtocolPdfLayoutSettings? protocolPdfLayoutSettings = null)
        : this(
            dialogs,
            protocolPdfExporter,
            getProjectFolder,
            projectCosts,
            openPdf,
            buildHydraulikCalculation,
            getLastProjectPath,
            findSchachtByNummer,
            buildDossierHydraulikCalculation,
            protocolRegeneration,
            dossierPhotoAvailability,
            inspectionProtocolFiles,
            protocolPdfLayoutSettings)
    {
        _pdfMerge = pdfMerge ?? throw new ArgumentNullException(nameof(pdfMerge));
    }

    [Obsolete("Kompatibilitaetskonstruktor. Neue Aufrufer muessen einen sicheren PDF-Oeffner injizieren.")]
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
        Func<byte[], IReadOnlyList<string>, byte[]>? mergeWithOriginals = null,
        Func<Project, string, HaltungRecord, ProtocolDocument, string?>? regenerateOne = null,
        Func<string, bool>? openPdf = null,
        IDossierPhotoAvailabilityService? dossierPhotoAvailability = null,
        IInspectionProtocolFileLocator? inspectionProtocolFiles = null)
        : this(
            dialogs,
            getProjectFolder,
            buildAwuPdf,
            baseDirectory,
            CreateCompatibilityProjectCosts(),
            openPdf ?? (path => DataPageOriginalPdfController.TryShellOpen(path).Success),
            fileExists,
            writeAllBytes,
            writeAllBytesAsync,
            now,
            buildHydraulikCalculation,
            selectHydraulikPrintOptions,
            buildHydraulikPdfAsync,
            getLastProjectPath,
            splitHoldingNodes,
            findSchachtByNummer,
            readDossierHydraulikAvailability,
            buildDossierHydraulikCalculation,
            findHoldingCost,
            resolveDossierOriginalPdfPaths,
            selectDossierPrintOptions,
            buildDossierPdfAsync,
            mergeOriginals,
            mergeWithOriginals,
            regenerateOne,
            dossierPhotoAvailability,
            inspectionProtocolFiles)
    {
    }

    internal DataPagePrintController(
        IDialogService dialogs,
        Func<string?> getProjectFolder,
        Func<Project, HaltungRecord, ProtocolDocument, string, HaltungsprotokollPdfOptions, byte[]> buildAwuPdf,
        string baseDirectory,
        IProjectCostStoreRepository projectCosts,
        Func<string, bool> openPdf,
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
        Func<byte[], IReadOnlyList<string>, byte[]>? mergeWithOriginals = null,
        Func<Project, string, HaltungRecord, ProtocolDocument, string?>? regenerateOne = null,
        IDossierPhotoAvailabilityService? dossierPhotoAvailability = null,
        IInspectionProtocolFileLocator? inspectionProtocolFiles = null,
        IProtocolPdfLayoutSettings? protocolPdfLayoutSettings = null)
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
        var mergeFallback = PdfMergeHelper.Current;
        _pdfMerge = mergeOriginals is null && mergeWithOriginals is null
            ? mergeFallback
            : new DelegatePdfMergeService(
                mergeOriginals ?? mergeFallback.MergeOriginals,
                mergeWithOriginals ?? mergeFallback.MergeWithOriginals);
        // Kein stiller statischer Rueckfall: der wuerde sich einen zweiten PDF-Erzeuger
        // ohne die Benutzereinstellung bauen. Die produktiven Aufrufwege reichen den
        // eingebauten Dienst immer durch.
        _regenerateOne = regenerateOne
            ?? ((_, _, _, _) => throw new InvalidOperationException(
                "Protokoll-Neuerzeugung ist nicht verdrahtet."));
        _openPdf = openPdf ?? throw new ArgumentNullException(nameof(openPdf));
        _dossierPhotoAvailability = dossierPhotoAvailability
            ?? DataPageDossierAvailability.CompatibilityService;
        _inspectionProtocolFiles = inspectionProtocolFiles
            ?? DataPageProtocolPathResolver.CompatibilityService;
        _projectCosts = projectCosts ?? throw new ArgumentNullException(nameof(projectCosts));
        _protocolPdfLayoutSettings = protocolPdfLayoutSettings;
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
        HoldingCost? holdingCost;
        try
        {
            holdingCost = _findHoldingCost(holdingLabel.Trim());
        }
        catch (Exception ex)
        {
            // Beschaedigte Kostendaten: Bericht sichtbar abbrechen statt still ein
            // plausibel aussehendes Dossier ohne Kosten zu erzeugen (Audit K3-Muster).
            var userMessage = UserError.DescribeAndReport(ex, "Dossier-Kostendaten laden");
            _dialogs.Error($"Dossier konnte nicht erstellt werden:\n{userMessage}", "Dossier");
            return;
        }
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

        if (project.Dirty && !ConfirmDirtyDossierPrint())
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
                originalPdfPaths.Count,
                _dossierPhotoAvailability);
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
                pdf = await Task.Run(() => _pdfMerge.MergeOriginals(originalPdfPaths));
                if (pdf.Length == 0)
                    throw new UserFacingException("Die Original-Protokolle konnten nicht zusammengefuehrt werden.");

                originalsAlreadyMerged = true;
            }

            if (!originalsAlreadyMerged && options.IncludeOriginalProtokolle && originalPdfPaths.Count > 0)
                pdf = await Task.Run(() => _pdfMerge.MergeWithOriginals(pdf, originalPdfPaths));

            await _writeAllBytesAsync(output, pdf);
            _dialogs.Info($"Dossier wurde erstellt:\n{output}", "Dossier");
        }
        catch (Exception ex)
        {
            var userMessage = UserError.DescribeAndReport(ex, "Dossier erstellen");
            _dialogs.Error($"Dossier konnte nicht erstellt werden:\n{userMessage}", "Dossier");
        }
    }

    private bool ConfirmDirtyDossierPrint()
        => _dialogs.ConfirmWarn(
            "ACHTUNG: Es gibt ungespeicherte Aenderungen im Projekt.\n\n" +
            "Das Dossier verwendet den zuletzt gespeicherten Stand der Sanierungs-Matrix. Trotzdem drucken?",
            "Dossier",
            defaultNo: true);

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
            var userMessage = UserError.DescribeAndReport(ex, "Hydraulik-PDF erstellen");
            _dialogs.Error($"PDF konnte nicht erstellt werden:\n{userMessage}", "Hydraulik PDF");
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
            var dest = _regenerateOne(project, projectFolder, record, doc);
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
            if (!_openPdf(dest!))
                _dialogs.Info($"AWU-Haltungsprotokoll wurde erstellt:\n{dest}", "Haltungsprotokoll AWU");
        }
        catch (Exception ex)
        {
            var userMessage = UserError.DescribeAndReport(ex, "AWU-Haltungsprotokoll erstellen");
            _dialogs.Error($"AWU-Haltungsprotokoll konnte nicht erstellt werden:\n{userMessage}", "Haltungsprotokoll AWU");
        }
    }

    private static Func<Project, string, HaltungRecord, ProtocolDocument, string?> CreateRegenerateOne(
        IProtocolSingleRegenerationService? protocolRegeneration,
        IProtocolPdfExporter protocolPdfExporter)
    {
        ArgumentNullException.ThrowIfNull(protocolPdfExporter);

        var service = protocolRegeneration
            ?? new AuswertungPro.Next.Infrastructure.Import.ProtocolRegenerationAdapter(protocolPdfExporter);

        return (project, folder, record, document) =>
            service.RegenerateOne(project, folder, record, document);
    }

    private static Func<Project, HaltungRecord, ProtocolDocument, string, HaltungsprotokollPdfOptions, byte[]> CreateBuildAwuPdf(
        IProtocolPdfExporter protocolPdfExporter)
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

        // Beschaedigte/unlesbare costs.json nicht still als "keine Kosten" behandeln:
        // der Druckweg bricht mit diesem Fehler sichtbar ab (Audit K3-Muster).
        var store = _projectCosts.Load(projectPath, out var loadError);
        if (!string.IsNullOrWhiteSpace(loadError))
            throw new UserFacingException($"Kostendaten konnten nicht geladen werden:\n{loadError}");
        return store.ByHolding.TryGetValue(holdingLabel.Trim(), out var cost) ? cost : null;
    }

    private static IProjectCostStoreRepository CreateCompatibilityProjectCosts()
        => CostStoreCompatibility.Factory.CreateProjectCostStore();

    private List<string> ResolveDossierOriginalPdfPaths(
        HaltungRecord record,
        string projectFolder,
        SchachtRecord? schachtVon,
        SchachtRecord? schachtBis)
    {
        var paths = _inspectionProtocolFiles.ResolveOriginalPdfPaths(record, projectFolder);
        if (schachtVon is not null)
            _inspectionProtocolFiles.ResolveSchachtPdfPaths(schachtVon, projectFolder, paths);
        if (schachtBis is not null)
            _inspectionProtocolFiles.ResolveSchachtPdfPaths(schachtBis, projectFolder, paths);

        return paths;
    }

    private DossierPrintOptions? SelectDossierPrintOptionsWithDialog(DataPageDossierPrintAvailability availability)
    {
        var dialog = _protocolPdfLayoutSettings is null
            ? new DossierPrintDialog()
            : new DossierPrintDialog(_protocolPdfLayoutSettings);
        dialog.Owner = System.Windows.Application.Current?.MainWindow;
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

    private sealed class DelegatePdfMergeService(
        Func<IReadOnlyList<string>, byte[]> mergeOriginals,
        Func<byte[], IReadOnlyList<string>, byte[]> mergeWithOriginals) : IPdfMergeService
    {
        public byte[] MergeWithOriginals(
            byte[] generatedPdf,
            IReadOnlyList<string> originalPdfPaths)
            => mergeWithOriginals(generatedPdf, originalPdfPaths);

        public byte[] MergeOriginals(IReadOnlyList<string> originalPdfPaths)
            => mergeOriginals(originalPdfPaths);
    }
}
