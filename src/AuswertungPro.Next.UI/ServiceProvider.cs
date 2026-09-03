using System;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Text.Json;
using AuswertungPro.Next.UI.Ai.Coding;
using Microsoft.Extensions.Logging;

using AuswertungPro.Next.Application.Diagnostics;
using AuswertungPro.Next.Application.Export;
using AuswertungPro.Next.Application.Import;
using AuswertungPro.Next.Application.Media;
using AuswertungPro.Next.Application.Maintenance;
using AuswertungPro.Next.Application.Projects;
using AuswertungPro.Next.Application.Protocol;
using AuswertungPro.Next.Application.Backup;
using AuswertungPro.Next.Application.Common;
using AuswertungPro.Next.Application.Costs;
using AuswertungPro.Next.Application.DataPage;
using AuswertungPro.Next.Application.Map;
// using AuswertungPro.Next.Application.Reports; // entfernt, da bereits oben vorhanden
using AuswertungPro.Next.Application.Vsa;

using AuswertungPro.Next.Infrastructure.Backup;
using AuswertungPro.Next.Infrastructure.Ai.Backup;
using AuswertungPro.Next.Infrastructure.Common;
using AuswertungPro.Next.Infrastructure.Costs;
using AuswertungPro.Next.Infrastructure.Diagnostics;
using AuswertungPro.Next.Infrastructure.DataPage;
using AuswertungPro.Next.Infrastructure.Export;
using AuswertungPro.Next.Infrastructure.Export.Excel;
using AuswertungPro.Next.Infrastructure.Import.Pdf;
using AuswertungPro.Next.Infrastructure.Import.Xtf;
using AuswertungPro.Next.Infrastructure.Import.WinCan;
using AuswertungPro.Next.Infrastructure.Import.Ibak;
using AuswertungPro.Next.Infrastructure.Import.Kins;
using AuswertungPro.Next.Infrastructure.Import.SchachtPro;
using AuswertungPro.Next.Infrastructure.Import;
using AuswertungPro.Next.Infrastructure.HoldingDistribution;
using AuswertungPro.Next.Infrastructure.Maintenance;
using AuswertungPro.Next.Infrastructure.Map;
using AuswertungPro.Next.Infrastructure.Media;
using AuswertungPro.Next.Infrastructure.Projects;
using AuswertungPro.Next.Infrastructure.Protocol;
using AuswertungPro.Next.Infrastructure.Settings;
using AuswertungPro.Next.Infrastructure.Telemetry;
using AuswertungPro.Next.Infrastructure.Vsa;
using AuswertungPro.Next.Infrastructure.Ai;
using AuswertungPro.Next.Infrastructure.Ai.BendSuggestions;
using AuswertungPro.Next.Infrastructure.Ai.Configuration;
using AuswertungPro.Next.Infrastructure.Ai.KnowledgeBase;
using AuswertungPro.Next.Infrastructure.Ai.Ollama;
using AuswertungPro.Next.Infrastructure.Ai.Pipeline;
using AuswertungPro.Next.Infrastructure.Ai.Sanierung;
using AuswertungPro.Next.Infrastructure.Ai.SelfImproving;
using AuswertungPro.Next.Infrastructure.Ai.Shared;
using AuswertungPro.Next.Infrastructure.Ai.Startup;
using AuswertungPro.Next.Infrastructure.Ai.Training;
using AuswertungPro.Next.Infrastructure.Ai.Training.ClassMaps;
using AuswertungPro.Next.Infrastructure.Ai.Training.PdfReview;
using AuswertungPro.Next.Infrastructure.Ai.Teacher;
using AuswertungPro.Next.Infrastructure.Reports;

using AuswertungPro.Next.UI.Ai.Pipeline;
using AuswertungPro.Next.UI.Ai;
using AuswertungPro.Next.UI.Ai.Training;
using AuswertungPro.Next.UI.Services;
using AuswertungPro.Next.UI.Settings;
using AuswertungPro.Next.Application.Ai;
using AuswertungPro.Next.Application.Ai.KnowledgeBase;
using AuswertungPro.Next.Application.Ai.Sanierung;
using AuswertungPro.Next.Application.Ai.Startup;
using AuswertungPro.Next.Application.Ai.Training;
using AuswertungPro.Next.Application.Ai.Training.ClassMaps;
using AuswertungPro.Next.Application.Ai.Training.ExportPlans;
using AuswertungPro.Next.Application.Ai.Training.Inventory;
using AuswertungPro.Next.Application.Ai.Teacher;
using AuswertungPro.Next.Application.Reports;
using AuswertungPro.Next.Application.UseCases.PdfTrainingReview;
using AuswertungPro.Next.Application.UseCases.BendSuggestions;

namespace AuswertungPro.Next.UI
{
    /// <summary>
    /// Minimaler DI-Container (damit kein extra Hosting-Paket nötig ist).
    /// </summary>
    public sealed partial class ServiceProvider : IServiceProvider
    {
        // Ein langlebiger Client fuer den optionalen Import-Schiedsrichter. HttpClient ist
        // thread-sicher und soll nicht bei jedem Import neu erzeugt werden.
        private readonly HttpClient _importAiHttp = new() { Timeout = TimeSpan.FromSeconds(60) };
        private readonly Lazy<ReviewQueueService> _trainingReviewQueue;
        // Langlebiger Sidecar-Client fuer den Bogen-Vorschlagsdurchlauf (je Bild ein Aufruf).
        // Wird erst beim ersten Durchgang gebaut und lebt den ganzen Programmlauf.
        private readonly Lazy<VisionPipelineClient> _bendSuggestionClient;

        #region Infrastruktur / Querschnitt
        // Basis-Einstellungen, Logging und Fehlercode-Generator
        public AppSettings Settings { get; }
        public ISettingsRestorePointStore SettingsRestorePoints { get; }
        public ISettingsFileStore SettingsFiles { get; }
        public ISettingsQuarantineStore SettingsQuarantine { get; }
        public ISettingsMigrationService SettingsMigration { get; }
        public IExplorerRevealService ExplorerReveal { get; }
        public ISafeShellOpenService ShellOpen { get; }
        public IFolderOpenService FolderOpen { get; }
        public IProgramRootLocator ProgramRootLocator { get; }
        public IRepositoryRootLocator RepositoryRootLocator { get; }
        public IFfmpegExecutableLocator FfmpegExecutables { get; }
        public IProcessOutputReader ProcessOutputs { get; }
        public IVideoFrameExtractor VideoFrameExtraction { get; }
        public ITrainingFfmpegPathResolver TrainingFfmpegPaths { get; }
        public ISidecarScriptLocator SidecarScripts { get; }
        public ISidecarTokenResolver SidecarTokens { get; }
        public IAiStartedProcessLifetime AiStartedProcesses { get; }
        public IVsaYoloClassMapStore VsaYoloClasses { get; }
        public ITrainingYoloClassMapStore TrainingYoloClasses { get; }
        public IGitCommitResolver GitCommit { get; }
        public IProgramSnapshotService ProgramSnapshot { get; }
        public DiagnosticsOptions Diagnostics { get; }
        public ILogger Logger { get; }
        public ILoggerFactory LoggerFactory { get; }
        public ErrorCodeGenerator ErrorCodes { get; } = new();
        // Setter nur fuer Tests (InternalsVisibleTo): echte Dialoge durch Fakes ersetzen.
        public IDialogService Dialogs { get; internal set; } = new DialogService();
        public ToastService Toasts { get; } = new ToastService();
        // AP-06: Startwarnung zur Wissensdatenbank (anderer/leerer KB-Ordner). Gesetzt im Konstruktor,
        // angezeigt vom MainWindow, sobald der Toast-Host bereit ist. Null = keine Warnung.
        public string? KnowledgeRootStartupWarning { get; private set; }
        // Einmal beim Start aufgeloest. Alle vom ServiceProvider erzeugten KB-Dienste
        // erhalten genau diesen Ordner und koennen nicht versehentlich auseinanderlaufen.
        public IKnowledgeBasePathService KnowledgePaths { get; }
        public string KnowledgeRoot { get; }
        public string KnowledgeDbPath { get; }
        // Zentrale Statusfarben (Ampel/Severity/Konfidenz) — eine Farbsprache fuer alle Fenster.
        public IStatusColorService StatusColors { get; } = new StatusColorService();
        // Nutzungszaehler fuer VSA-Codes (Favoriten-Chips im Code-Explorer).
        public ICodeUsageTracker CodeUsage { get; } = new CodeUsageTracker();
        // ETA fuer lange Laeufe: pro Lauf eine frische Instanz (gleitende Rate ist lauf-spezifisch).
        public AuswertungPro.Next.Application.Common.IEtaCalculator CreateEtaCalculator() => new AuswertungPro.Next.Application.Common.EtaCalculator();
        public DashboardRefreshNotifier DashboardRefresh { get; } = new();
        public IDropdownOptionsStore DropdownOptions { get; }
        public ICostStoreFactory CostStores { get; }
        public IKatasterXtfPathResolver KatasterXtfPaths { get; }
        public IHaltungCadastreTableStore HaltungCadastreTables { get; }
        public IHaltungCadastreIndexProvider HaltungCadastreIndexes { get; }
        public IVsaCatalogPathResolver VsaCatalogPaths { get; }
        public IPlaywrightInstallService PlaywrightInstaller { get; }
        public ILogTailReader LogTailReader { get; }
        public IDiagnosticsPackageService DiagnosticsPackages { get; }
        public IVideoStartErrorLogWriter VideoStartErrorLogs { get; }
        public IKnowledgeBaseHealthInspector KnowledgeBaseHealth { get; }
        public IKnowledgeBackupService KnowledgeBackup { get; }
        public IKnowledgeRealtimeMirrorService KnowledgeRealtimeMirror { get; }
        public FullBackupOperationState FullBackupOperation { get; } = new();
        public ProgramCleanupService ProgramCleanup { get; } = new();
        public ICodexArtifactCleanupService CodexArtifactCleanup { get; } = new CodexArtifactCleanupService();
        #endregion

        #region Persistenz
        // Projektverwaltung und lokale Datenspeicherung
        public IProjectRepository Projects { get; }

        /// <summary>Erzeugt revidierte XTF-Dateien aus dem aktuellen Projektstand.</summary>
        public AuswertungPro.Next.Application.Xtf.IXtfRevisionExportService XtfRevisionExport { get; }

        /// <summary>Der vollstaendige Neu-Export mit eigenen, stabilen XTF-Kennungen.</summary>
        public AuswertungPro.Next.Application.Xtf.IXtfNeuExportService XtfNeuExport { get; }
        public IProjectContentSignature ProjectContentSignature { get; }
        public IImportTransactionJournal ImportTransactionJournal { get; }
        public IImportTransactionRecoveryService ImportTransactionRecovery { get; }
        public IProjectPhotoReferenceNormalizer ProjectPhotoReferences { get; }
        public IProjectFileDiscovery ProjectFileDiscovery { get; }
        public IProjectOverviewCatalog ProjectOverviewCatalog { get; }
        public IProjectDropPathResolver ProjectDropPaths { get; }
        #endregion

        #region Import
        // Alle Import-Adapter für externe Datenformate
        public IPdfFileSafetyChecker PdfFileSafety { get; }
        public IAtomicPdfFileReplacer PdfFileReplacement { get; }
        public IPdfTextExtractor PdfTextExtraction { get; }
        public IPdfOcrExtractor PdfOcrExtraction { get; }
        public ISchachtProtocolOcrReader SchachtProtocolOcr { get; }
        public IPdfFormFieldReader PdfFormFields { get; }
        public IPdfTextPrefixReader PdfTextPrefixes { get; }
        public IPdfImportService PdfImport { get; }
        public IPdfTextLayerRewriter PdfTextLayerRewrite { get; }
        public IDistributionPdfPageReader DistributionPdfPages { get; }
        public IDistributionFileTransfer DistributionFileTransfers { get; }
        public IVideoConflictCandidateCopier VideoConflictCandidates { get; }
        public IShaftPdfSelectionExpander ShaftPdfSelectionExpansion { get; }
        // Ordnet Herstellernamen wie "Section_8_892037-74091.pdf" einer bereits
        // vorhandenen Haltung/einem Schacht zu (fail-closed, legt selbst nichts an).
        public IImportPdfReferenceResolver ImportPdfReferences { get; }
        // Liest das Protokolldatum mit derselben Textquelle und Regel wie die Verteilung.
        public IProtocolPdfDateReader ProtocolPdfDates { get; }
        // "Abgleichen": raeumt aus den Verteilordnern, was im Projekt kein Gegenstueck hat.
        public IDistributionReconciliationService DistributionReconciliation { get; }
        // Name-basierte Protokoll-Verteilung (Haltungen + Schaechte) aus einem Quellordner.
        public INameBasedProtocolDistributor NameBasedProtocolDistributor { get; }
        public IVsaMediaPathResolver VsaMediaPaths { get; }
        public IXtfHoldingFileReader XtfHoldingFiles { get; }
        public IM150SourceFileReader M150SourceFiles { get; }
        public IM150MdbRowReader M150MdbRows { get; }
        public IXtfImportService XtfImport { get; }
        public IWinCanDbImportService WinCanImport { get; }
        public IXtfStammdatenSourceReader XtfStammdatenSources { get; }
        public IIbakPdfStammdatenSourceReader IbakPdfStammdatenSources { get; }
        public IIbakFdbConnectionOptions IbakConnections { get; }
        public IIbakImportService IbakImport { get; }
        public IKinsImportService KinsImport { get; }
        public ISchachtProImportService SchachtProImport { get; }
        public IKinsDvdTextEnricher KinsDvdTextEnrichment { get; }
        public IKinsDbfWhitelistEnricher KinsDbfWhitelistEnrichment { get; }
        public IKinsGesamtprotokollLocator KinsGesamtprotokolle { get; }
        public IPhotoImportService PhotoImport { get; }
        public AuswertungPro.Next.Infrastructure.Media.BatchMediaSearchService BatchMediaSearch { get; } = new();
        public AuswertungPro.Next.Infrastructure.Media.MediaConflictCenterService MediaConflictCenter { get; } = new();
        public IProjectPortabilityService ProjectPortability { get; }
        public IProjectPhotoAssignmentService ProjectPhotoAssignment { get; }
        public IHoldingRenameService HoldingRename { get; }
        public IShaftRenameService ShaftRename { get; }
        public IPlanPdfImporter PlanPdfImport { get; }
        public IProtocolRegenerationService ProtocolRegeneration { get; }
        public IProtocolSingleRegenerationService ProtocolSingleRegeneration { get; }
        public IOneClickImportReportWriter OneClickImportReports { get; }
        public IImportRunReportExporter ImportRunReports { get; }
        public IImportSummaryExporter ImportSummaryExporter { get; }
        public IStoredImportFileService StoredImportFiles { get; }
        public IStoredImportFilePathResolver StoredImportFilePaths { get; }
        public IImportFileStagingService ImportFileStaging { get; }
        public IShaftDistributionService ShaftDistribution { get; }

        /// <summary>Ruecknahme der Dateien eines verworfenen Ein-Knopf-Imports.</summary>
        public IImportedFileLedger ImportedFiles { get; }
        public IImportMediaDistributionService ImportMediaDistribution { get; }
        public IProjectRestorePointService ProjectRestorePoints { get; }
        public IProjectStructureInitializer ProjectStructure { get; }
        public IKiasExportPatternDetector KiasExportPatterns { get; }
        public IKanalExportDetectionService KanalExportDetection { get; }
        public ISchaechteTemplateColumnReader SchaechteTemplateColumns { get; }
        public IProjectRecoveryService ProjectRecovery { get; }
        public IImportSourceArchiver ImportSourceArchiver { get; }
        public IDichtheitImportDistributor DichtheitImportDistributor { get; }
        public IKanalImportDistributor KanalImportDistributor { get; }
        // Einzel-Import eines Schacht-Protokolls (Aktualisieren + Protokoll importieren, Schachtseite).
        public ISchachtProtocolImportService SchachtProtocolImport { get; }
        // Nachlauf fuer bestehende Projekte: nur fehlende Schacht-Stammdaten aus vorhandenen PDFs.
        public ISchachtStammdatenErgaenzungsService SchachtStammdatenErgaenzung { get; }
        // Sucht die Protokoll-PDF genau eines Schachts (Verknuepfung, sonst dessen Schachtordner).
        public ISchachtProtocolFileLocator SchachtProtocolFiles { get; }
        #endregion

        #region Export / Protokoll
        // Export-Dienste und Protokollerzeugung
        public IExcelExportService ExcelExport { get; }
        public INpkLeistungsverzeichnisExcelExporter NpkExcelExport { get; }
        public IDistributionPatternResolver DistributionPatterns { get; }
        public IDistributionDirectoryTreeResolver DistributionDirectoryTree { get; }
        public IProtocolService Protocols { get; }
        public IProtocolPdfLayoutSettings ProtocolPdfLayoutSettings { get; }
        public ProtocolPdfExporter ProtocolPdfExporter { get; }
        public IProtocolPdfExporter ProtocolPdfExports => ProtocolPdfExporter;
        public IPdfMergeService PdfMerge { get; }
        public AuswertungPro.Next.Application.Output.IOfferPdfExportService OfferPdfExport { get; }
        public AuswertungPro.Next.Application.Output.INpkOfferPdfExportService NpkOfferPdfExport { get; }
        public AuswertungPro.Next.Application.Output.IPdfPrintService PdfPrint { get; }
        public IDossierPhotoAvailabilityService DossierPhotoAvailability { get; }
        public IInspectionProtocolFileLocator InspectionProtocolFiles { get; }
        public IDichtheitProtocolFileLocator DichtheitProtocolFiles { get; }
        public ISchachtFileTargetResolver SchachtFileTargets { get; }
        // Zieht abgeleitete Kostenfelder nach der Sanieren-Regel nach (nur Sanieren=Ja zaehlt).
        public AuswertungPro.Next.Application.DataPage.IDerivedCostFieldSynchronizer CostFieldSync { get; }
        #endregion

        #region VSA-Bewertung
        // Zustandsklassifizierung nach VSA/EN 13508-2
        public IVsaEvaluationService Vsa { get; }
        public IVsaShadowTelemetryWriter VsaShadowTelemetry { get; }
        #endregion

        #region KI / Vision
        // KI-Pipeline: CodeCatalog, Retrieval, KnowledgeBase, KI-Protokoll, Sanierungsempfehlung
        public IProtocolAiService ProtocolAi { get; }
        public IGpuModelSelector GpuModels { get; }
        public IAiPlatformSettingsResolver AiSettings { get; }
        public IPipelineEnvironmentOptions PipelineEnvironment { get; }
        public ICodingFramePhotoStore CodingFramePhotos { get; }
        public ICodingDefectPreviewRenderer CodingDefectPreviews { get; }
        public IBendSuggestionScanService BendSuggestionScan { get; }
        // Sitzungsgedaechtnis der angesehenen Vorschlagslisten — bewusst Singleton: Das
        // Gedaechtnis muss den ganzen Programmlauf leben, ein Neustart setzt es zurueck.
        public ICodingSuggestionExposure CodingSuggestionExposure { get; }
        public IVideoClipExtractor VideoClipExtraction { get; }
        public ITelemetryPathResolver TelemetryPaths { get; }
        public ISidecarTelemetryWriter SidecarTelemetry { get; }
        public IPipelineTraceWriter PipelineTrace { get; }
        public IProtocolTrainingStore ProtocolTraining { get; }
        public ITrainingCenterSettingsStore TrainingSettings { get; }
        public ISelfTrainingHistoryStore SelfTrainingHistory { get; }
        public ITeacherAnnotationStore TeacherAnnotations { get; }
        public IAiOptimizationSessionStore AiOptimizationSessions { get; }
        public ITrainingSampleStore TrainingSamples { get; }
        public IPersonalGoldAlbumService PersonalGoldAlbum { get; }
        public IPersonalGoldInboxService PersonalGoldInbox { get; }
        public ITrainingPdfReviewImportService TrainingPdfReviews { get; }
        internal ITrainingPdfReviewImportService TrainingPdfReviewReader { get; }
        public ITrainingFrameStore TrainingFrames { get; }
        public ITrainingPreviewFrameExtractor TrainingPreviewFrames { get; }
        public AuswertungPro.Next.Application.Protocol.ICodeCatalogProvider CodeCatalog { get; }
        public AuswertungPro.Next.Application.Protocol.IVsaCodeSelectionCatalog CodeSelectionCatalog { get; }
        public string? VsaCatalogResolvedPath { get; }

        /// <summary>Gibt bei jedem Zugriff eine frische PipelineConfig zurück (1x laden, projizieren).</summary>
        public PipelineConfig PipelineCfg => AiSettings
            .Load(AppSettingsAiSettingsProvider.ToSource(Settings))
            .ToPipelineConfig();

        public IRetrievalService? Retrieval { get; }
        public IKnowledgeBaseDiagnosticsRunner KnowledgeBaseDiagnostics { get; }
        public IMeasureRecommendationService MeasureRecommendation { get; }
        public IVideoAnalysisPipelineFactory VideoAnalysisPipelines { get; }
        public IAiSanierungOptimizationFactory SanierungOptimizations { get; }
        internal DataPage.IDataPageSanierungViewModelFactory DataPageSanierungViewModels { get; }
        internal DataPage.IDataPageWindowLauncher DataPageWindows { get; }
        public ITrainingCenterDocumentStore TrainingCenterDocuments { get; }
        public AuswertungPro.Next.UI.Ai.Training.TrainingCenterStore TrainingCenterStore { get; }
        public ITrainingCaseIdSource TrainingCases { get; }
        public TrainingCenterImportService TrainingCenterImport { get; } = new();
        public ReviewQueueService TrainingReviewQueue => _trainingReviewQueue.Value;

        public TrainingReviewSamSegmentationService CreateTrainingReviewSam()
            => new(new VisionPipelineTrainingReviewSamClient(PipelineCfg, SidecarTelemetry));

        // Globale, selbst gepflegte Schacht-Massnahmen-Liste (einfacher Weg ohne NPK):
        // Name + manueller Preis, projektuebergreifend unter %AppData%.
        public AuswertungPro.Next.Application.Schacht.ISchachtMassnahmenKatalogStore SchachtMassnahmenKatalog { get; }
            = new AuswertungPro.Next.Infrastructure.Schacht.SchachtMassnahmenKatalogStore();
        #endregion

        public ServiceProvider(AppSettings settings, DiagnosticsOptions diagnostics, ILogger logger, ILoggerFactory loggerFactory)
            : this(
                settings,
                diagnostics,
                logger,
                loggerFactory,
                new SettingsQuarantineStore(),
                new SettingsMigrationService())
        {
        }

        internal ServiceProvider(
            AppSettings settings,
            DiagnosticsOptions diagnostics,
            ILogger logger,
            ILoggerFactory loggerFactory,
            ISettingsQuarantineStore settingsQuarantine,
            ISettingsMigrationService settingsMigration,
            IKatasterXtfPathResolver? katasterXtfPaths = null,
            IKnowledgeBaseHealthInspector? knowledgeBaseHealth = null)
        {
            Settings = settings;
            SettingsQuarantine = settingsQuarantine
                ?? throw new ArgumentNullException(nameof(settingsQuarantine));
            SettingsMigration = settingsMigration
                ?? throw new ArgumentNullException(nameof(settingsMigration));
            KatasterXtfPaths = katasterXtfPaths ?? new KatasterXtfFilePathResolver();
            HaltungCadastreTables = new HaltungCadastreTableFileStore();
            HaltungCadastreIndexes = new HaltungCadastreIndexProvider(HaltungCadastreTables);
            VsaCatalogPaths = new VsaCatalogFilePathResolver();
            SettingsRestorePoints = new SettingsRestorePointStore();
            SettingsFiles = SettingsStore.CreateDefault(SettingsRestorePoints);
            ExplorerReveal = new ExplorerRevealLauncher();
            ShellOpen = new SafeShellOpenService();
            FolderOpen = new FolderOpenService(ShellOpen);
            ProgramRootLocator = new ProgramRootFileLocator();
            RepositoryRootLocator = new RepositoryRootFileLocator();
            FfmpegExecutables = new FfmpegFileLocator();
            ProcessOutputs = new ProcessOutputReaderService();
            VideoFrameExtraction = new VideoFrameExtractionService(ProcessOutputs);
            TrainingFfmpegPaths = new TrainingFfmpegFilePathResolver();
            SidecarScripts = new SidecarScriptFileLocator();
            SidecarTokens = new SidecarTokenFileResolver();
            AiStartedProcesses = new AiStartedProcessLifetimeService();
            GpuModels = new GpuModelSelectionService();
            AiSettings = new AiPlatformSettingsResolver(GpuModels);
            PipelineEnvironment = new PipelineEnvironmentOptionsService();
            settings.UseSettingsFileStore(SettingsFiles);
            Diagnostics = diagnostics;
            Logger = logger;
            LoggerFactory = loggerFactory;
            TelemetryPaths = new TelemetryFilePathResolver();
            SidecarTelemetry = new SidecarTelemetryFileWriter(TelemetryPaths);
            PipelineTrace = new PipelineTraceFileWriter(TelemetryPaths);
            VideoStartErrorLogs = new VideoStartErrorLogFileWriter();
            var logDirectory = Path.Combine(AppSettings.AppDataDir, "logs");
            LogTailReader = new DailyLogTailReader(logDirectory);
            DiagnosticsPackages = new DiagnosticsPackageService(logDirectory, AppIdentity.Version);
            FullBackupOperation.SetLastBackupInfo(
                SettingsFullBackupPresentationBuilder.BuildLastBackupInfo(
                    settings.LastFullBackupUtc,
                    settings.LastFullBackupPath,
                    settings.LastFullBackupSizeBytes));

            KnowledgePaths = KnowledgeBasePaths.Current;
            settings.MigrateLegacyKnowledgeRootPath();

            // Env-Var hat Vorrang. Fehlt sie, bleibt der zuletzt bestaetigte KB-Pfad
            // aus settings.json aktiv und die App startet nicht unbemerkt mit leerem Wissen.
            KnowledgePaths.ConfigureSettingsRoot(settings.KnowledgeRootPath);
            KnowledgeRoot = KnowledgePaths.GetRoot();
            VsaYoloClasses = new VsaYoloClassMapFileStore(
                Path.Combine(KnowledgeRoot, "yolo_class_map.json"));
            TrainingSettings = new TrainingCenterSettingsFileStore(
                KnowledgePaths.GetTrainingSettingsPath());
            SelfTrainingHistory = new SelfTrainingHistoryFileStore(
                Path.Combine(KnowledgeRoot, "selftraining_history.json"));
            TeacherAnnotations = new TeacherAnnotationFileStore(KnowledgeRoot);
            AiOptimizationSessions = new AiOptimizationSessionFileStore(
                Path.Combine(AppSettings.AppDataDir, "ai_sanierung_sessions.json"));
            var trainingSamples = new TrainingSampleFileStore(
                KnowledgePaths.GetTrainingSamplesPath());
            trainingSamples.ConfigureEvalProtection(settings.EvalSetRoot);
            // Auch die unveraenderbare Kompatibilitaets-Fassade auf denselben Eval-Schutz-Root
            // setzen, damit Fallback-Pfade ueber TrainingSamplesStore.Current nicht gegen einen
            // abweichend konfigurierten Eval-Root filtern (Schutz vor Eval-Kontamination).
            TrainingSamplesStore.ConfigureEvalProtection(settings.EvalSetRoot);
            TrainingSamples = trainingSamples;
            PersonalGoldAlbum = new PersonalGoldAlbumService(TrainingSamples);
            PersonalGoldInbox = new PersonalGoldInboxFileService(
                KnowledgeRoot,
                VsaCodeResolver.LookupLabel);
            TrainingPdfReviewReader = new TrainingPdfReviewImportService(
                KnowledgeRoot,
                new TrainingPdfJpegColorNormalizer());
            TrainingPdfReviews = new TrainingPdfReviewProtectedImportService(
                TrainingPdfReviewReader,
                () => EvalContaminationSetProvider.LoadPdfProtectionSnapshot(
                    settings.EvalSetRoot));
            TrainingFrames = new TrainingFrameFileStore();
            TrainingPreviewFrames = new TrainingPreviewFrameExtractionService(TrainingFrames);
            CodingFramePhotos = new CodingFramePhotoFileStore();
            CodingDefectPreviews = new CodingDefectPreviewRenderer();
            _trainingReviewQueue = new Lazy<ReviewQueueService>(
                ReviewQueueService.CreatePersistent,
                System.Threading.LazyThreadSafetyMode.ExecutionAndPublication);
            var knowledgeResolution = KnowledgePaths.GetResolution();
            KnowledgeDbPath = Path.Combine(KnowledgeRoot, "KnowledgeBase.db");
            if (knowledgeResolution.Source == KnowledgeBasePaths.RootSource.EnvironmentOverride)
            {
                Logger.LogInformation(
                    "Wissensdatenbank-Override {EnvironmentVariable} ist fuer diesen Start aktiv: {KnowledgeRoot}",
                    KnowledgeBasePaths.EnvironmentVariableName,
                    KnowledgeRoot);
            }
            var knowledgeConfigurationWarning = knowledgeResolution.HasEnvironmentSettingsMismatch
                ? "Fuer diesen Start ist ein anderer Wissensordner ueber die Umgebungsvariable " +
                  $"{KnowledgeBasePaths.EnvironmentVariableName} aktiv.\n" +
                  $"Gespeichert: {knowledgeResolution.PersistedSettingsRoot}\n" +
                  $"Jetzt aktiv: {KnowledgeRoot}\n" +
                  "Der gespeicherte Pfad wird nicht ueberschrieben. Pruefe bitte, ob diese Abweichung gewollt ist."
                : null;

            // Statische Fassaden auf dieselben Instanzen zeigen lassen (Konsumenten ohne DI).

            DropdownOptions = new FileDropdownOptionsStore();
            CostStores = new CostStoreFactory();
            TrainingCenterDocuments = new TrainingCenterDocumentFileStore();
            TrainingCenterStore = new TrainingCenterStore(TrainingCenterDocuments);
            TrainingCases = new TrainingCaseIdSource(TrainingCenterStore);
            ProtocolTraining = new ProtocolTrainingFileStore();

            ProjectPhotoReferences = new ProjectPhotoReferenceNormalizationService();
            Projects = new JsonProjectRepository(ProjectPhotoReferences);
            XtfRevisionExport = new AuswertungPro.Next.Infrastructure.Import.Xtf.XtfRevisionExportService();
            XtfNeuExport = new AuswertungPro.Next.Infrastructure.Import.Xtf.XtfNeuExportService(
                new AuswertungPro.Next.Infrastructure.Lookup.QgisGpkgVerlaufLeser(
                    () => Settings.QgisHaltungenGpkgPath));
            ProjectContentSignature = new JsonProjectContentSignature();
            ImportTransactionJournal = new FileImportTransactionJournal();
            ImportTransactionRecovery = new ImportTransactionRecoveryService(ImportTransactionJournal);
            ProjectFileDiscovery = new ProjectFileDiscoveryService();
            ProjectOverviewCatalog = new ProjectOverviewCatalogService(ProjectFileDiscovery);
            ProjectDropPaths = new ProjectDropFilePathResolver();
            ProjectStructure = new ProjectStructureInitializer();
            KiasExportPatterns = new KiasExportPatternDetectionService();
            KanalExportDetection = new KanalExportDetectionService(KiasExportPatterns);
            SchaechteTemplateColumns = new SchaechteTemplateColumnFileReader();
            PdfFileSafety = new PdfFileSafetyService();
            PdfFileReplacement = new AtomicPdfFileReplacementService(PdfFileSafety);
            PdfTextExtraction = new PdfTextExtractionService(PdfFileSafety);
            PdfOcrExtraction = new PdfOcrExtractionService(PdfTextExtraction, PdfFileSafety);
            SchachtProtocolOcr = new SchachtProtocolOcrReaderService(PdfFileSafety, PdfOcrExtraction);
            PdfFormFields = new PdfFormFieldReaderService();
            PdfTextPrefixes = new PdfTextPrefixReaderService();
            PdfDokumentTypErkennung.UseTextPrefixReader(PdfTextPrefixes);
            PdfImport = new PdfImportServiceAdapter(PdfTextExtraction, PdfOcrExtraction);
            PdfTextLayerRewrite = new PdfTextLayerRewriteService(
                PdfFileReplacement,
                loggerFactory.CreateLogger<PdfTextLayerRewriteService>());
            DistributionPdfPages = new DistributionPdfPageReadingService(PdfTextExtraction, PdfFileSafety);
            DistributionFileTransfers = new DistributionFileTransferService();
            VideoConflictCandidates = new VideoConflictCandidateCopyService(DistributionFileTransfers);
            ShaftPdfSelectionExpansion = new ShaftPdfSelectionExpansionService();
            ImportPdfReferences = new AuswertungPro.Next.Infrastructure.Import.Protocols.ImportPdfReferenceResolver();
            ProtocolPdfDates = new AuswertungPro.Next.Infrastructure.Import.Protocols.ProtocolPdfDateReader();
            DistributionReconciliation =
                new AuswertungPro.Next.Infrastructure.Export.DistributionReconciliationService();
            NameBasedProtocolDistributor = new AuswertungPro.Next.Infrastructure.Import.Protocols.NameBasedProtocolDistributor(
                ImportPdfReferences,
                ProtocolPdfDates);
            VsaMediaPaths = new VsaMediaPathFileResolver();
            XtfHoldingFiles = new XtfHoldingFileReader();
            XtfHelper.UseHoldingReader(XtfHoldingFiles);
            M150SourceFiles = new M150XmlTextFileReader();
            M150MdbRows = new PowerShellM150MdbRowReader();
            XtfImport = new XtfImportServiceAdapter(
                VsaMediaPaths,
                M150SourceFiles,
                M150MdbRows);
            // Protocols wird vor den Import-Diensten gebaut, damit alle dieselbe Instanz erhalten
            // (kein verstreutes new ProtocolService() mehr in den einzelnen Import-Diensten).
            Protocols = new ProtocolService();
            WinCanImport = new WinCanDbImportService(M150MdbRows, XtfImport, Protocols);
            XtfStammdatenSources = new XtfStammdatenSourceReader();
            XtfStammdatenExtractor.UseSourceReader(XtfStammdatenSources);
            IbakPdfStammdatenSources = new IbakPdfStammdatenSourceReader(PdfTextExtraction);
            IbakPdfStammdatenExtractor.UseSourceReader(IbakPdfStammdatenSources);
            IbakConnections = new IbakFdbConnectionOptionsService();
            IbakImport = new IbakExportImportService(IbakConnections, Protocols);
            KinsImport = new KinsImportService(WinCanImport, IbakImport, Protocols);
            SchachtProImport = new SchachtProImportService();
            KinsDvdTextEnrichment = new KinsDvdTextEnrichmentService();
            KinsDbfWhitelistEnrichment = new KinsDbfWhitelistEnrichmentService();
            KinsGesamtprotokolle = new KinsGesamtprotokollFileLocator();
            ProjectPortability = new ProjectPortabilityService();
            ProjectPhotoAssignment = new ProjectPhotoAssignmentService();
            HoldingRename = new HoldingRenameFileService();
            ShaftRename = new ShaftRenameFileService();
            PlanPdfImport = new PlanPdfImportService();
            var catalogPaths = VsaCatalogPaths.Resolve(
                Services.VsaCatalogPathResolver.ToRequest(settings));
            VsaCatalogResolvedPath = catalogPaths.DisplayPath;
            var vsaManifestPath = !string.IsNullOrWhiteSpace(catalogPaths.KekManifestPath)
                ? catalogPaths.KekManifestPath
                : Path.Combine(AppContext.BaseDirectory, "Data", "vsa_kek_2020_catalog_manifest.json");
            CodeCatalog = CreateCodeCatalog(
                settings,
                VsaCatalogPaths,
                catalogPaths.KekManifestPath,
                catalogPaths.XmlCatalogPaths);
            ProtocolPdfLayoutSettings = new AppSettingsProtocolPdfLayoutSettings(Settings);
            ProtocolPdfExporter = new ProtocolPdfExporter(new ProtocolPdfAssetFileResolver(), ProtocolPdfLayoutSettings, CodeCatalog);
            PdfMerge = new PdfMergeService();
            OfferPdfExport = new AuswertungPro.Next.Infrastructure.Output.Offers.OfferPdfExportService();
            NpkOfferPdfExport = new AuswertungPro.Next.Infrastructure.Output.Offers.NpkOfferPdfExportService();
            PdfPrint = new AuswertungPro.Next.Infrastructure.Output.Offers.PdfPrintService();
            DossierPhotoAvailability = new DossierPhotoFileAvailabilityService();
            StoredImportFiles = new StoredImportFileService();
            StoredImportFilePaths = new StoredImportFilePathResolver();
            ImportFileStaging = new ImportFileStagingService();
            ShaftDistribution = new ShaftDistributionService();
            ImportedFiles = new ImportedFileLedgerService();
            ImportMediaDistribution = new MediaDistributionService();
            InspectionProtocolFiles = new InspectionProtocolFileLocator(StoredImportFilePaths);
            _dossierComposition = new AuswertungPro.Next.Infrastructure.Dossiers.DossierComposition(
                InspectionProtocolFiles,
                ProtocolPdfExporter,
                PdfMerge,
                // Erst beim Aufruf gelesen, damit ein nachtraeglich eingetragener
                // Schluessel ohne Programmneustart wirkt.
                () => Settings.SearchChApiKey);
            DichtheitProtocolFiles = new DichtheitProtocolFileLocator();
            SchachtFileTargets = new SchachtFileTargetPathResolver();
            var protocolRegeneration = new ProtocolRegenerationAdapter(ProtocolPdfExporter);
            ProtocolRegeneration = protocolRegeneration;
            ProtocolSingleRegeneration = protocolRegeneration;
            OneClickImportReports = new OneClickImportReportWriter(Logger);
            ImportRunReports = new ImportRunReportFileExporter();
            ImportSummaryExporter = new ImportSummaryExporter();
            ProjectRestorePoints = new ProjectRestorePointStore();
            ProjectRecovery = new ProjectRecoveryService();
            ImportSourceArchiver = new ImportSourceArchiveService();
            DichtheitImportDistributor = new DichtheitImportDistributionService();
            KanalImportDistributor = new KanalImportDistributionService();
            DistributionPatterns = new DistributionPatternResolver();
            DistributionDirectoryTree = new DistributionDirectoryTreeResolver(DistributionPatterns);
            ExcelExport = new ExcelTemplateExportService();
            NpkExcelExport = new NpkLeistungsverzeichnisExcelExportService();
            CostFieldSync = new AuswertungPro.Next.Application.DataPage.DerivedCostFieldSynchronizer();

            // Register protocol/photo/pdf services (Protocols oben schon gebaut und injiziert)
            PhotoImport = new PhotoImportService();
            SchachtProtocolImport = new AuswertungPro.Next.Infrastructure.Import.Protocols.SchachtProtocolImportService(
                PdfTextExtraction,
                SchachtProtocolOcr);
            SchachtStammdatenErgaenzung = new AuswertungPro.Next.Infrastructure.Import.Protocols.SchachtStammdatenErgaenzungsService(
                SchachtProtocolImport);
            SchachtProtocolFiles = new AuswertungPro.Next.Infrastructure.Import.Protocols.SchachtProtocolFileLocator();

            PlaywrightInstaller = new PlaywrightInstallService(loggerFactory.CreateLogger<PlaywrightInstallService>());
            KnowledgeWalCheckpoint = new KnowledgeWalCheckpointService(KnowledgeDbPath);
            KnowledgeBaseHealth = knowledgeBaseHealth ?? new KnowledgeBaseHealthInspectionService();
            GitCommit = new GitCommitFileResolver();
            BackupSources = new FullBackupSourcesProvider(RepositoryRootLocator);
            _fullBackupComposition = FullBackupComposition.Create(
                () => BackupSources.Resolve(settings),
                KnowledgeWalCheckpoint,
                OllamaListAsync,
                GitCommit);
            KnowledgeBackup = new KnowledgeBackupTransferService(
                KnowledgeBackupLocationFactory.FromCurrentSystem(),
                AppSettings.FlushPendingSave,
                KnowledgeBackupEngine.FlushSqliteWal,
                SqliteSnapshots);
            ProgramSnapshot = new ProgramSnapshotService(GitCommit);
            KnowledgeRealtimeMirror = new KnowledgeRealtimeMirrorService(
                KnowledgeRoot,
                loggerFactory.CreateLogger<KnowledgeRealtimeMirrorService>());



            // Einheitliche KI-Konfiguration (1x laden, 3x projizieren)
            var aiPlatform = AiSettings.Load(AppSettingsAiSettingsProvider.ToSource(settings));

            // AI/CodeCatalog Init (AiLocalPack)
            var cfg = aiPlatform.ToRuntimeSettings();
            TrainingYoloClasses = new TrainingYoloClassMapFileStore(
                Path.Combine(AppContext.BaseDirectory, "Data", "Training", "detect_class_map_v3.json"),
                Path.Combine(AppContext.BaseDirectory, "Data", "Training", "detect_class_migration_v3.candidate.json"),
                vsaManifestPath);
            _trainingYoloExportComposition = TrainingYoloExportComposition.Create(
                KnowledgeRoot,
                settings.EvalSetRoot,
                TrainingSamples,
                CodeCatalog,
                TrainingYoloClasses,
                PipelineCfg,
                SidecarTelemetry,
                TimeProvider.System);
            // Kontrollierter Sidecar-Neustart (Paket 3/A2): delegiert auf den bestehenden
            // Startweg (Skript + Launcher + Prozess-Tracking). Der Dienst selbst prueft pro
            // Versuch, ob die App den Sidecar gestartet hat; sonst bleibt es bei Degraded.
            var sidecarRestart = new SidecarRestartService(
                AiStartedProcesses,
                new DefaultAiStartupLauncher(AiStartedProcesses),
                getTarget: () =>
                {
                    var restartPlatform = AiSettings.Load(AppSettingsAiSettingsProvider.ToSource(settings));
                    return new Application.Ai.Startup.SidecarRestartTarget(
                        SidecarUrl: restartPlatform.SidecarUrl,
                        Headers: AiStartupService.BuildSidecarHeaders(
                            restartPlatform.SidecarUrl,
                            restartPlatform.SidecarToken,
                            SidecarTokens),
                        ScriptPath: SidecarScripts.FindDefaultSidecarScript(),
                        PowerShellExe: SidecarScripts.ResolvePowerShellExe(),
                        EnvironmentVariables: new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
                        {
                            ["SEWER_SIDECAR_TRAINING_EXPORT_ROOT"] = Path.Combine(
                                KnowledgeRoot, "training", "datasets"),
                            ["SEWER_SIDECAR_TRAINING_MODEL_CANDIDATES_ROOT"] = Path.Combine(
                                KnowledgeRoot, "training", "models", "candidates")
                        });
                },
                logger: LoggerFactory.CreateLogger<SidecarRestartService>());
            VideoAnalysisPipelines = new Infrastructure.Ai.VideoAnalysisPipelineFactory(
                PipelineTrace,
                ProcessOutputs,
                () => AiSettings.Load(AppSettingsAiSettingsProvider.ToSource(settings)).ToPipelineConfig(),
                CodeCatalog,
                LoggerFactory,
                PipelineEnvironment,
                SidecarTelemetry,
                sidecarRestart);
            SanierungOptimizations = new Infrastructure.Ai.Sanierung.AiSanierungOptimizationFactory();
            // Bogen-Vorschlaege (Auftrag Paket 4): derselbe Sidecar-Weg wie die uebrigen
            // Pipeline-Clients — URL, Token und Telemetrie kommen aus derselben Konfiguration.
            _bendSuggestionClient = new Lazy<VisionPipelineClient>(() =>
            {
                var bendCfg = PipelineCfg;
                return new VisionPipelineClient(
                    bendCfg.SidecarUrl,
                    httpClient: null,
                    bendCfg.SidecarToken,
                    SidecarTelemetry,
                    ownedTimeout: TimeSpan.FromSeconds(Math.Max(30, bendCfg.SidecarTimeoutSec)));
            });
            BendSuggestionScan = new BendSuggestionScanService(
                new BendSuggestionCalibrationFileStore(),
                new VideoFrameSequenceExtractor(),
                (anfrage, abbruch) => _bendSuggestionClient.Value.DetectBccTestYoloAsync(anfrage, abbruch),
                FfmpegExecutables.ResolveFfmpeg,
                () => Path.Combine(Path.GetTempPath(), "auswertungpro-bogen-scan"));
            CodingSuggestionExposure = new CodingSuggestionExposure();
            VideoClipExtraction = new VideoClipExtractionService(ProcessOutputs);
            // Picker-Anordnung wie ISYBAU/WinCan (kuratierter VsaCodeTree), aber Mengen-/Uhrlage-
            // Regeln aus dem aktuellen VSA-Katalog – Codes sind EN-13508-/VSA-konform (geprueft).
            CodeSelectionCatalog = new AuswertungPro.Next.Application.Protocol.VsaCodeTreeSelectionCatalog(
                new CodeCatalogSelectionCatalog(CodeCatalog));
            VsaCodeResolver.ConfigureCatalog(CodeCatalog);

            // AP-06: Zustand der Wissensdatenbank VOR der Init erfassen (existiert die DB-Datei,
            // bevor der Context sie ggf. neu/leer anlegt?). Schuetzt gegen stillen Split-Brain,
            // wenn die Umgebungsvariable SEWERSTUDIO_KNOWLEDGE_ROOT verloren geht.
            var knowledgeHealth = KnowledgeBaseHealth.Inspect(KnowledgeDbPath);
            var knowledgeDbExisted = knowledgeHealth.DatabaseExists;
            var knowledgeSampleCount = 0;
            var knowledgeSampleCountRead = false;

            RetrievalService? retrieval = null;
            try
            {
                if (!knowledgeHealth.IsHealthy)
                    throw new InvalidDataException(knowledgeHealth.Error ?? "SQLite quick_check fehlgeschlagen.");

                var ollamaConfig = aiPlatform.ToOllamaConfig();
                var kbHttp = new HttpClient { Timeout = ollamaConfig.RequestTimeout };
                var kbCtx = new KnowledgeBaseContext(KnowledgeDbPath);
                var embedder = new EmbeddingService(kbHttp, ollamaConfig);
                // Audit Fix #6a: Eval-Haltungs-Sperrliste auch leseseitig anwenden (Defense-in-Depth,
                // gleiche Quelle wie der Schreib-Guard) -> kontaminierte Samples kommen nie als Few-Shot.
                var evalHaltungKeys = AuswertungPro.Next.Application.Ai.Training.EvalContaminationGuard
                    .LoadEvalHaltungKeys(settings.EvalSetRoot);
                retrieval = new RetrievalService(kbCtx, embedder, evalHaltungKeys);
                retrieval.CheckModelConsistency();
                if (retrieval.HasModelMismatch)
                    Logger.LogWarning(
                        "KB-Embedding-Modell '{StoredModel}' stimmt nicht mit aktuellem Modell '{CurrentModel}' überein. KB-Rebuild empfohlen.",
                        retrieval.StoredEmbedModel, ollamaConfig.EmbedModel);

                // AP-06: aktuelle Sample-Zahl fuer die Abweichungs-Warnung (best-effort).
                try
                {
                    knowledgeSampleCount = new KnowledgeBaseDiagnosticsService(kbCtx).ReadSummary(topCodes: 1).SampleCount;
                    knowledgeSampleCountRead = true;
                }
                catch { /* Sample-Zahl ist optional; 0 bleibt gueltig fuer die Pruefung. */ }
            }
            catch (Exception ex)
            {
                Logger.LogWarning(ex, "KnowledgeBase-Retrieval konnte nicht initialisiert werden. KI läuft ohne KB-Kontext.");
            }

            // AP-06: Warnen, wenn die App unbemerkt mit einer anderen oder leeren Wissensdatenbank laeuft.
            var knowledgeRootGuard = KnowledgeRootGuard.Evaluate(
                KnowledgeRoot,
                settings.LastKnownKnowledgeRoot,
                knowledgeDbExisted,
                knowledgeSampleCount,
                settings.LastKnownKnowledgeSampleCount);
            if (!knowledgeHealth.IsHealthy)
            {
                KnowledgeRootStartupWarning =
                    "Die Wissensdatenbank ist beschaedigt oder nicht lesbar. Die App arbeitet vorerst ohne KB-Kontext.\n" +
                    $"Datei: {KnowledgeDbPath}\n" +
                    $"Fehler: {knowledgeHealth.Error}\n" +
                    "Bitte stelle die Datei aus einer Datensicherung wieder her.";
                Logger.LogError("Wissensdatenbank-Integritaetspruefung fehlgeschlagen: {Error}", knowledgeHealth.Error);
            }
            else if (knowledgeRootGuard.HatWarnung)
            {
                KnowledgeRootStartupWarning = knowledgeRootGuard.Meldung;
                Logger.LogWarning("Wissensdatenbank-Startwarnung ({Art}): {Meldung}",
                    knowledgeRootGuard.Art, knowledgeRootGuard.Meldung);
            }
            else if (knowledgeConfigurationWarning is not null)
            {
                KnowledgeRootStartupWarning = knowledgeConfigurationWarning;
                Logger.LogWarning("Wissensdatenbank-Pfadabweichung: {Meldung}", knowledgeConfigurationWarning);
            }
            settings.RecordKnowledgeRootStart(
                KnowledgeRoot,
                knowledgeSampleCountRead ? knowledgeSampleCount : null,
                knowledgeResolution.Source);
            settings.SaveImmediate();

            Retrieval = retrieval;
            KnowledgeBaseDiagnostics = new KnowledgeBaseDiagnosticsRunner(
                KnowledgeDbPath,
                TrainingSamples);

            var allowedCodeSet = new HashSet<string>(CodeCatalog.AllowedCodes(), StringComparer.OrdinalIgnoreCase);
            IAiSuggestionPlausibilityService plausibility = new RuleBasedAiSuggestionPlausibilityService(allowedCodeSet);
            var protocolTrainingSamples = new ProtocolTrainingSampleProvider(ProtocolTraining);

            ProtocolAi = cfg.Enabled
                ? new OllamaProtocolAiService(
                    cfg.Enabled,
                    aiPlatform.ToOllamaConfig(),
                    cfg.FfmpegPath,
                    protocolTrainingSamples,
                    retrieval,
                    plausibility)
                : new NoopProtocolAiService();

            LogCodeCatalogWarnings(CodeCatalog, VsaCatalogResolvedPath);

            var channelsTable = Path.Combine(AppContext.BaseDirectory, "Data", "classification_channels.json");
            var manholesTable = Path.Combine(AppContext.BaseDirectory, "Data", "classification_manholes.json");
            var v2ChannelsTable = Path.Combine(AppContext.BaseDirectory, "Data", "vsa_zustandsklassifizierung_2023_channels.json");
            var v2ManholesTable = Path.Combine(AppContext.BaseDirectory, "Data", "vsa_zustandsklassifizierung_2023_manholes.json");
            VsaShadowTelemetry = new VsaShadowTelemetryFileWriter(TelemetryPaths);
            Vsa = new VsaEvaluationService(
                channelsTable,
                manholesTable,
                VsaShadowTelemetry,
                shadowModeEnabled: settings.VsaClassificationShadowEnabled ?? true,
                useV2Engine: settings.VsaUseV2Engine ?? true,
                v2ChannelsTablePath: v2ChannelsTable,
                v2ManholesTablePath: v2ManholesTable);

            MeasureRecommendation = new Infrastructure.Ai.MeasureRecommendationService(
                Path.Combine(KnowledgeRoot, "measures_learning.json"),
                Path.Combine(KnowledgeRoot, "measures-model.zip"));
            DataPageSanierungViewModels = new DataPage.DataPageSanierungViewModelFactory(
                Settings,
                CostStores,
                CostStores.CreateProjectCostStore(),
                CostFieldSync,
                DashboardRefresh,
                SanierungOptimizations,
                AiOptimizationSessions,
                OfferPdfExport);
            DataPageWindows = new DataPage.DataPageWindowLauncher(this);
            _services = ServiceProviderRegistrationMap.Create(this);
        }

        public IVideoAnalysisPipelineService CreateVideoAnalysisPipeline(
            AiRuntimeSettings cfg,
            IAiSuggestionPlausibilityService plausibility,
            HttpClient http)
        {
            return VideoAnalysisPipelines.Create(cfg, plausibility, http);
        }

        public IAiSanierungOptimizationService CreateSanierungOptimization(
            AiRuntimeSettings cfg,
            HttpClient? http = null)
        {
            return SanierungOptimizations.Create(cfg, http);
        }

        // Baut eine kurzlebige Schnellscan-Sitzung (eigener Ollama-Client) fuer den Player,
        // damit der UI-Controller die Infrastruktur-Pipeline nicht mehr selbst zusammensetzt.
        public IQuickScanSession CreateQuickScanSession(AiRuntimeSettings cfg)
            => QuickScanSession.Create(cfg, ProcessOutputs);

        // Baut den regelbasierten Plausibilitaetsdienst aus einer laufzeit-spezifischen
        // Codemenge; haelt das new der Infrastruktur-Regelklasse im Composition-Root statt in der UI.
        public IAiSuggestionPlausibilityService CreatePlausibility(IReadOnlySet<string> allowedCodes)
            => new RuleBasedAiSuggestionPlausibilityService(allowedCodes);

        // ── Schattenauswertung: eigenstaendige Parallel-Auswertung (nur lesend) ──
        // Ergebnis-Ablage als eigene Datei im Projektordner, nie in projekt.json.
        public AuswertungPro.Next.Application.Schatten.ISchattenAuswertungStore SchattenStore { get; }
            = new AuswertungPro.Next.Infrastructure.Schatten.SchattenAuswertungStoreRepository();

        /// <summary>
        /// Baut den Schatten-Rechendienst. KI-Teil nur, wenn die KI-Plattform aktiviert ist —
        /// sonst laeuft die Seite rein regelbasiert (Hybrid-Prinzip mit Regel-Rueckfall).
        /// </summary>
        public AuswertungPro.Next.Application.Schatten.ISchattenAuswertungService CreateSchattenAuswertung()
        {
            var cfg = new Services.AppSettingsAiSettingsProvider(AiSettings).Load().ToRuntimeSettings();
            var ki = cfg.Enabled ? CreateSanierungOptimization(cfg) : null;
            return new AuswertungPro.Next.Infrastructure.Schatten.SchattenAuswertungService(
                Vsa, MeasureRecommendation, ki, cfg.Enabled ? cfg.TextModel : null);
        }

        public ProjectImportOrchestrator CreateProjectImportOrchestrator()
            => new(
                XtfImport,
                WinCanImport,
                KinsImport,
                IbakImport,
                CreateImportAiArbitrator(),
                NameBasedProtocolDistributor,
                PlanPdfImport,
                ProjectRestorePoints,
                ImportSourceArchiver,
                DichtheitImportDistributor,
                KanalImportDistributor,
                ProjectStructure,
                KanalExportDetection,
                KinsDvdTextEnrichment,
                KinsDbfWhitelistEnrichment,
                KinsGesamtprotokolle,
                ImportMediaDistribution);

        public IOneClickProjectImportService CreateOneClickProjectImportService()
            => CreateProjectImportOrchestrator();

        private PdfKiSchiedsrichter? CreateImportAiArbitrator()
        {
            try
            {
                var platform = AiSettings.Load(
                    AppSettingsAiSettingsProvider.ToSource(Settings));
                if (!platform.Enabled)
                    return null;

                var ollama = new OllamaClient(
                    platform.OllamaBaseUri,
                    _importAiHttp,
                    TimeSpan.FromSeconds(45),
                    keepAlive: platform.OllamaKeepAlive,
                    numCtx: platform.OllamaNumCtx);
                var schema = JsonDocument
                    .Parse(PdfKiSchiedsrichter.JsonSchema)
                    .RootElement.Clone();

                return new PdfKiSchiedsrichter(async (prompt, ct) =>
                {
                    var answer = await ollama.ChatStructuredAsync<JsonElement>(
                        platform.TextModel,
                        [new OllamaClient.ChatMessage("user", prompt)],
                        schema,
                        ct).ConfigureAwait(false);
                    return answer.GetRawText();
                });
            }
            catch (Exception ex)
            {
                Logger.LogInformation(ex, "KI-Schiedsrichter fuer Import ist nicht verfuegbar.");
                return null;
            }
        }

        private void LogCodeCatalogWarnings(AuswertungPro.Next.Application.Protocol.ICodeCatalogProvider provider, string? sourcePath)
        {
            IReadOnlyList<string>? warnings = provider switch
            {
                AuswertungPro.Next.Application.Protocol.XmlCodeCatalogProvider xml => xml.LastLoadWarnings,
                AuswertungPro.Next.Application.Protocol.JsonCodeCatalogProvider json => json.LastLoadWarnings,
                AuswertungPro.Next.Application.Protocol.CompositeCodeCatalogProvider composite => composite.GetWarnings(),
                _ => null
            };

            if (warnings is null || warnings.Count == 0)
                return;

            const int maxItems = 12;
            var sample = string.Join(", ", warnings.Take(maxItems));
            var suffix = warnings.Count > maxItems ? $" (+{warnings.Count - maxItems} weitere)" : string.Empty;
            var sourceLabel = string.IsNullOrWhiteSpace(sourcePath) ? "unbekannt" : sourcePath;

            Logger.LogWarning("Code-Katalog Duplikate ({Count}) in {Source}: {Sample}{Suffix}",
                warnings.Count, sourceLabel, sample, suffix);
        }

        private static AuswertungPro.Next.Application.Protocol.ICodeCatalogProvider CreateCodeCatalog(
            AppSettings settings,
            IVsaCatalogPathResolver catalogPathResolver,
            string? vsaKekManifestPath,
            IReadOnlyList<string> xmlCatalogPaths)
        {
            var providers = new List<AuswertungPro.Next.Application.Protocol.ICodeCatalogProvider>();

            if (!string.IsNullOrWhiteSpace(vsaKekManifestPath) && File.Exists(vsaKekManifestPath))
            {
                providers.Add(new AuswertungPro.Next.Application.Protocol.ManifestCodeCatalogProvider(vsaKekManifestPath));
            }

            providers.AddRange(xmlCatalogPaths
                .Select(path => new AuswertungPro.Next.Application.Protocol.SourceDecoratingCodeCatalogProvider(
                    new AuswertungPro.Next.Application.Protocol.XmlCodeCatalogProvider(
                        path,
                        fallbackJsonPath: null,
                        fallbackTextXmlPath: catalogPathResolver.ResolveTextFallbackPath(
                            settings.VsaCatalogSecXmlPath,
                            path)),
                    AuswertungPro.Next.Application.Protocol.VsaKekCatalogSources.WinCanFallback))
                .Cast<AuswertungPro.Next.Application.Protocol.ICodeCatalogProvider>());

            return new AuswertungPro.Next.Application.Protocol.CompositeCodeCatalogProvider(providers);
        }

        private static async Task<string?> OllamaListAsync(CancellationToken ct)
        {
            try
            {
                var result = await ExternalProcessRunner.RunAsync(
                    "ollama",
                    new[] { "list" },
                    TimeSpan.FromSeconds(10),
                    cancellationToken: ct).ConfigureAwait(false);

                return result.Success ? result.StdOut : null;
            }
            catch
            {
                return null;
            }
        }
    }
}
