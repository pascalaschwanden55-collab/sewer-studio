using System;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Text.Json;
using Microsoft.Extensions.Logging;

using AuswertungPro.Next.Application.Diagnostics;
using AuswertungPro.Next.Application.Export;
using AuswertungPro.Next.Application.Import;
using AuswertungPro.Next.Application.Media;
using AuswertungPro.Next.Application.Projects;
using AuswertungPro.Next.Application.Protocol;
using AuswertungPro.Next.Application.Backup;
using AuswertungPro.Next.Application.Common;
// using AuswertungPro.Next.Application.Reports; // entfernt, da bereits oben vorhanden
using AuswertungPro.Next.Application.Vsa;

using AuswertungPro.Next.Infrastructure.Backup;
using AuswertungPro.Next.Infrastructure.Diagnostics;
using AuswertungPro.Next.Infrastructure.Export;
using AuswertungPro.Next.Infrastructure.Export.Excel;
using AuswertungPro.Next.Infrastructure.Import.Pdf;
using AuswertungPro.Next.Infrastructure.Import.Xtf;
using AuswertungPro.Next.Infrastructure.Import.WinCan;
using AuswertungPro.Next.Infrastructure.Import.Ibak;
using AuswertungPro.Next.Infrastructure.Import.Kins;
using AuswertungPro.Next.Infrastructure.Import;
using AuswertungPro.Next.Infrastructure.Maintenance;
using AuswertungPro.Next.Infrastructure.Projects;
using AuswertungPro.Next.Infrastructure.Vsa;
using AuswertungPro.Next.Infrastructure.Ai;
using AuswertungPro.Next.Infrastructure.Ai.Configuration;
using AuswertungPro.Next.Infrastructure.Ai.KnowledgeBase;
using AuswertungPro.Next.Infrastructure.Ai.Ollama;
using AuswertungPro.Next.Infrastructure.Ai.Sanierung;
using AuswertungPro.Next.Infrastructure.Ai.SelfImproving;
using AuswertungPro.Next.Infrastructure.Ai.Training;

using AuswertungPro.Next.UI.Ai.Pipeline;
using AuswertungPro.Next.UI.Services;
using AuswertungPro.Next.UI.Settings;
using AuswertungPro.Next.Application.Ai;
using AuswertungPro.Next.Application.Ai.KnowledgeBase;
using AuswertungPro.Next.Application.Ai.Sanierung;
using AuswertungPro.Next.Application.Reports;

namespace AuswertungPro.Next.UI
{
    /// <summary>
    /// Minimaler DI-Container (damit kein extra Hosting-Paket nötig ist).
    /// </summary>
    public sealed class ServiceProvider : IServiceProvider
    {
        // Ein langlebiger Client fuer den optionalen Import-Schiedsrichter. HttpClient ist
        // thread-sicher und soll nicht bei jedem Import neu erzeugt werden.
        private readonly HttpClient _importAiHttp = new() { Timeout = TimeSpan.FromSeconds(60) };
        private readonly Lazy<ReviewQueueService> _trainingReviewQueue;

        #region Infrastruktur / Querschnitt
        // Basis-Einstellungen, Logging und Fehlercode-Generator
        public AppSettings Settings { get; }
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
        // Kartennetz-Cache (Netzlinien + raeumlicher Index): einmal gebaut, ueber alle
        // Kartenoeffnungen wiederverwendet, beim Start vorladbar. Singleton.
        public AuswertungPro.Next.UI.Mapping.NetworkFeatureCache NetworkFeatures { get; } = new();
        public IPlaywrightInstallService PlaywrightInstaller { get; }
        public ILogTailReader LogTailReader { get; }
        public IDiagnosticsPackageService DiagnosticsPackages { get; }
        public IFullBackupService FullBackup { get; }
        public FullBackupOperationState FullBackupOperation { get; } = new();
        public ProgramCleanupService ProgramCleanup { get; } = new();
        #endregion

        #region Persistenz
        // Projektverwaltung und lokale Datenspeicherung
        public IProjectRepository Projects { get; }
        #endregion

        #region Import
        // Alle Import-Adapter für externe Datenformate
        public IPdfImportService PdfImport { get; }
        // Name-basierte Protokoll-Verteilung (Haltungen + Schaechte) aus einem Quellordner.
        public INameBasedProtocolDistributor NameBasedProtocolDistributor { get; }
        public IXtfImportService XtfImport { get; }
        public IWinCanDbImportService WinCanImport { get; }
        public IIbakImportService IbakImport { get; }
        public IKinsImportService KinsImport { get; }
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
        public IImportSummaryExporter ImportSummaryExporter { get; }
        public IStoredImportFileService StoredImportFiles { get; }
        public IProjectRestorePointService ProjectRestorePoints { get; }
        public IProjectRecoveryService ProjectRecovery { get; }
        public IImportSourceArchiver ImportSourceArchiver { get; }
        // Einzel-Import eines Schacht-Protokolls (Aktualisieren + Protokoll importieren, Schachtseite).
        public ISchachtProtocolImportService SchachtProtocolImport { get; }
        // Nachlauf fuer bestehende Projekte: nur fehlende Schacht-Stammdaten aus vorhandenen PDFs.
        public ISchachtStammdatenErgaenzungsService SchachtStammdatenErgaenzung { get; }
        #endregion

        #region Export / Protokoll
        // Export-Dienste und Protokollerzeugung
        public IExcelExportService ExcelExport { get; }
        public IProtocolService Protocols { get; }
        public ProtocolPdfExporter ProtocolPdfExporter { get; }
        public IProtocolPdfExporter ProtocolPdfExports => ProtocolPdfExporter;
        // Zieht abgeleitete Kostenfelder nach der Sanieren-Regel nach (nur Sanieren=Ja zaehlt).
        public AuswertungPro.Next.Application.DataPage.IDerivedCostFieldSynchronizer CostFieldSync { get; }
        #endregion

        #region VSA-Bewertung
        // Zustandsklassifizierung nach VSA/EN 13508-2
        public IVsaEvaluationService Vsa { get; }
        #endregion

        #region KI / Vision
        // KI-Pipeline: CodeCatalog, Retrieval, KnowledgeBase, KI-Protokoll, Sanierungsempfehlung
        public IProtocolAiService ProtocolAi { get; }
        public AuswertungPro.Next.Application.Protocol.ICodeCatalogProvider CodeCatalog { get; }
        public AuswertungPro.Next.Application.Protocol.IVsaCodeSelectionCatalog CodeSelectionCatalog { get; }
        public string? VsaCatalogResolvedPath { get; }

        /// <summary>Gibt bei jedem Zugriff eine frische PipelineConfig zurück (1x laden, projizieren).</summary>
        public PipelineConfig PipelineCfg => AiSettingsFactory
            .Load(AppSettingsAiSettingsProvider.ToSource(Settings))
            .ToPipelineConfig();

        public IRetrievalService? Retrieval { get; }
        public IKnowledgeBaseDiagnosticsRunner KnowledgeBaseDiagnostics { get; }
        public IMeasureRecommendationService MeasureRecommendation { get; }
        public IVideoAnalysisPipelineFactory VideoAnalysisPipelines { get; }
        public IAiSanierungOptimizationFactory SanierungOptimizations { get; }
        internal DataPage.IDataPageWindowLauncher DataPageWindows { get; }
        public AuswertungPro.Next.UI.Ai.Training.TrainingCenterStore TrainingCenterStore { get; } = new();
        public TrainingCenterImportService TrainingCenterImport { get; } = new();
        public ReviewQueueService TrainingReviewQueue => _trainingReviewQueue.Value;

        public TrainingReviewSamSegmentationService CreateTrainingReviewSam()
            => new(new VisionPipelineTrainingReviewSamClient(PipelineCfg));

        public FewShotExampleStore CreateFewShotStore()
            => new(message => Logger.LogWarning("{Message}", message));

        // Globale, selbst gepflegte Schacht-Massnahmen-Liste (einfacher Weg ohne NPK):
        // Name + manueller Preis, projektuebergreifend unter %AppData%.
        public AuswertungPro.Next.Application.Schacht.ISchachtMassnahmenKatalogStore SchachtMassnahmenKatalog { get; }
            = new AuswertungPro.Next.Infrastructure.Schacht.SchachtMassnahmenKatalogStore();
        #endregion

        public ServiceProvider(AppSettings settings, DiagnosticsOptions diagnostics, ILogger logger, ILoggerFactory loggerFactory)
                // Removed misplaced property initialization
        {
            Settings = settings;
            Diagnostics = diagnostics;
            Logger = logger;
            LoggerFactory = loggerFactory;
            var logDirectory = Path.Combine(AppSettings.AppDataDir, "logs");
            LogTailReader = new DailyLogTailReader(logDirectory);
            DiagnosticsPackages = new DiagnosticsPackageService(logDirectory, AppIdentity.Version);
            FullBackupOperation.SetLastBackupInfo(
                SettingsFullBackupPresentationBuilder.BuildLastBackupInfo(
                    settings.LastFullBackupUtc,
                    settings.LastFullBackupPath,
                    settings.LastFullBackupSizeBytes));

            settings.MigrateLegacyKnowledgeRootPath();

            // Env-Var hat Vorrang. Fehlt sie, bleibt der zuletzt bestaetigte KB-Pfad
            // aus settings.json aktiv und die App startet nicht unbemerkt mit leerem Wissen.
            KnowledgeBasePaths.ConfigureSettingsRoot(settings.KnowledgeRootPath);
            KnowledgeRoot = KnowledgeBasePaths.GetRoot();
            _trainingReviewQueue = new Lazy<ReviewQueueService>(
                ReviewQueueService.CreatePersistent,
                System.Threading.LazyThreadSafetyMode.ExecutionAndPublication);
            var knowledgeResolution = KnowledgeBasePaths.GetResolution();
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
            Theme.StatusColors.Current = StatusColors;
            CodeUsageTrackers.Current = CodeUsage;

            DropdownOptions = new FileDropdownOptionsStore();

            Projects = new JsonProjectRepository();
            PdfImport = new PdfImportServiceAdapter();
            NameBasedProtocolDistributor = new AuswertungPro.Next.Infrastructure.Import.Protocols.NameBasedProtocolDistributor();
            XtfImport = new XtfImportServiceAdapter();
            WinCanImport = new WinCanDbImportService();
            IbakImport = new IbakExportImportService();
            KinsImport = new KinsImportService(WinCanImport, IbakImport);
            ProjectPortability = new ProjectPortabilityService();
            ProjectPhotoAssignment = new ProjectPhotoAssignmentService();
            HoldingRename = new HoldingRenameFileService();
            ShaftRename = new ShaftRenameFileService();
            PlanPdfImport = new PlanPdfImportService();
            ProtocolPdfExporter = new ProtocolPdfExporter();
            var protocolRegeneration = new ProtocolRegenerationAdapter(ProtocolPdfExporter);
            ProtocolRegeneration = protocolRegeneration;
            ProtocolSingleRegeneration = protocolRegeneration;
            OneClickImportReports = new OneClickImportReportWriter(Logger);
            ImportSummaryExporter = new ImportSummaryExporter();
            StoredImportFiles = new StoredImportFileService();
            ProjectRestorePoints = new ProjectRestorePointStore();
            ProjectRecovery = new ProjectRecoveryService();
            ImportSourceArchiver = new ImportSourceArchiveService();
            ExcelExport = new ExcelTemplateExportService();
            CostFieldSync = new AuswertungPro.Next.Application.DataPage.DerivedCostFieldSynchronizer();

            // Register protocol/photo/pdf services
            Protocols = new ProtocolService();
            PhotoImport = new PhotoImportService();
            SchachtProtocolImport = new AuswertungPro.Next.Infrastructure.Import.Protocols.SchachtProtocolImportService();
            SchachtStammdatenErgaenzung = new AuswertungPro.Next.Infrastructure.Import.Protocols.SchachtStammdatenErgaenzungsService(
                SchachtProtocolImport);

            PlaywrightInstaller = new PlaywrightInstallService(loggerFactory.CreateLogger<PlaywrightInstallService>());
            FullBackup = new FullBackupService(
                () => FullBackupSourcesFactory.ErmittleAktuelleQuellen(settings),
                KnowledgeWalCheckpoint.TryCheckpoint,
                ct => OllamaListAsync(ct));



            // Einheitliche KI-Konfiguration (1x laden, 3x projizieren)
            var aiPlatform = AiSettingsFactory.Load(AppSettingsAiSettingsProvider.ToSource(settings));
            TrainingSamplesStore.ConfigureEvalProtection(settings.EvalSetRoot);

            // AI/CodeCatalog Init (AiLocalPack)
            var cfg = aiPlatform.ToRuntimeSettings();
            var catalogPaths = VsaCatalogPathResolver.Resolve(settings);
            VsaCatalogResolvedPath = catalogPaths.DisplayPath;
            CodeCatalog = CreateCodeCatalog(settings, catalogPaths.KekManifestPath, catalogPaths.XmlCatalogPaths);
            VideoAnalysisPipelines = new Infrastructure.Ai.VideoAnalysisPipelineFactory(
                () => AiSettingsFactory.Load(AppSettingsAiSettingsProvider.ToSource(settings)).ToPipelineConfig(),
                CodeCatalog,
                LoggerFactory);
            SanierungOptimizations = new Infrastructure.Ai.Sanierung.AiSanierungOptimizationFactory();
            // Picker-Anordnung wie ISYBAU/WinCan (kuratierter VsaCodeTree), aber Mengen-/Uhrlage-
            // Regeln aus dem aktuellen VSA-Katalog – Codes sind EN-13508-/VSA-konform (geprueft).
            CodeSelectionCatalog = new AuswertungPro.Next.Application.Protocol.VsaCodeTreeSelectionCatalog(
                new CodeCatalogSelectionCatalog(CodeCatalog));
            VsaCodeResolver.ConfigureCatalog(CodeCatalog);

            // AP-06: Zustand der Wissensdatenbank VOR der Init erfassen (existiert die DB-Datei,
            // bevor der Context sie ggf. neu/leer anlegt?). Schuetzt gegen stillen Split-Brain,
            // wenn die Umgebungsvariable SEWERSTUDIO_KNOWLEDGE_ROOT verloren geht.
            var knowledgeDbExisted = File.Exists(KnowledgeDbPath);
            var knowledgeHealth = knowledgeDbExisted
                ? KnowledgeBaseHealthChecker.Check(KnowledgeDbPath)
                : KnowledgeBaseHealthResult.Ok;
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
            KnowledgeBaseDiagnostics = new KnowledgeBaseDiagnosticsRunner(KnowledgeDbPath);

            var allowedCodeSet = new HashSet<string>(CodeCatalog.AllowedCodes(), StringComparer.OrdinalIgnoreCase);
            IAiSuggestionPlausibilityService plausibility = new RuleBasedAiSuggestionPlausibilityService(allowedCodeSet);
            var protocolTrainingSamples = new ProtocolTrainingSampleProvider();

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
            Vsa = new VsaEvaluationService(
                channelsTable,
                manholesTable,
                shadowModeEnabled: settings.VsaClassificationShadowEnabled ?? true,
                useV2Engine: settings.VsaUseV2Engine ?? true,
                v2ChannelsTablePath: v2ChannelsTable,
                v2ManholesTablePath: v2ManholesTable);

            MeasureRecommendation = new Infrastructure.Ai.MeasureRecommendationService(
                Path.Combine(KnowledgeRoot, "measures_learning.json"),
                Path.Combine(KnowledgeRoot, "measures-model.zip"));
            DataPageWindows = new DataPage.DataPageWindowLauncher(this);
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
            var cfg = new Services.AppSettingsAiSettingsProvider().Load().ToRuntimeSettings();
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
                ImportSourceArchiver);

        public IOneClickProjectImportService CreateOneClickProjectImportService()
            => CreateProjectImportOrchestrator();

        private PdfKiSchiedsrichter? CreateImportAiArbitrator()
        {
            try
            {
                var platform = AiSettingsFactory.Load(
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
                    fallbackTextXmlPath: VsaCatalogPathResolver.ResolveTextFallbackPath(settings, path)),
                    AuswertungPro.Next.Application.Protocol.VsaKekCatalogSources.WinCanFallback))
                .Cast<AuswertungPro.Next.Application.Protocol.ICodeCatalogProvider>());

            return new AuswertungPro.Next.Application.Protocol.CompositeCodeCatalogProvider(providers);
        }

        public object? GetService(Type serviceType)
        {
            if (serviceType == typeof(IFullBackupService)) return FullBackup;
            if (serviceType == typeof(IProjectRepository)) return Projects;
            if (serviceType == typeof(IPdfImportService)) return PdfImport;
            if (serviceType == typeof(INameBasedProtocolDistributor)) return NameBasedProtocolDistributor;
            if (serviceType == typeof(IXtfImportService)) return XtfImport;
            if (serviceType == typeof(IWinCanDbImportService)) return WinCanImport;
            if (serviceType == typeof(IIbakImportService)) return IbakImport;
            if (serviceType == typeof(IKinsImportService)) return KinsImport;
            if (serviceType == typeof(ISchachtProtocolImportService)) return SchachtProtocolImport;
            if (serviceType == typeof(ISchachtStammdatenErgaenzungsService)) return SchachtStammdatenErgaenzung;
            if (serviceType == typeof(IExcelExportService)) return ExcelExport;
            if (serviceType == typeof(IVsaEvaluationService)) return Vsa;
            if (serviceType == typeof(IProtocolService)) return Protocols;
            if (serviceType == typeof(IKnowledgeBaseDiagnosticsRunner)) return KnowledgeBaseDiagnostics;
            if (serviceType == typeof(IDiagnosticsPackageService)) return DiagnosticsPackages;
            if (serviceType == typeof(AuswertungPro.Next.Application.Protocol.ICodeCatalogProvider)) return CodeCatalog;
            if (serviceType == typeof(AuswertungPro.Next.Application.Protocol.IVsaCodeSelectionCatalog)) return CodeSelectionCatalog;
            if (serviceType == typeof(ILogger)) return Logger;
            if (serviceType == typeof(ILoggerFactory)) return LoggerFactory;
            if (serviceType == typeof(IStatusColorService)) return StatusColors;
            if (serviceType == typeof(ICodeUsageTracker)) return CodeUsage;
            return null;
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
