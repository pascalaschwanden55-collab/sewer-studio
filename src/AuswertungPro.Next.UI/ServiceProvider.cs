using System;
using System.IO;
using System.Linq;
using System.Net.Http;
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
using AuswertungPro.Next.Infrastructure.Export;
using AuswertungPro.Next.Infrastructure.Export.Excel;
using AuswertungPro.Next.Infrastructure.Import.Pdf;
using AuswertungPro.Next.Infrastructure.Import.Xtf;
using AuswertungPro.Next.Infrastructure.Import.WinCan;
using AuswertungPro.Next.Infrastructure.Import.Ibak;
using AuswertungPro.Next.Infrastructure.Import.Kins;
using AuswertungPro.Next.Infrastructure.Projects;
using AuswertungPro.Next.Infrastructure.Vsa;
using AuswertungPro.Next.Infrastructure.Ai;
using AuswertungPro.Next.Infrastructure.Ai.Configuration;
using AuswertungPro.Next.Infrastructure.Ai.KnowledgeBase;
using AuswertungPro.Next.Infrastructure.Ai.Ollama;
using AuswertungPro.Next.Infrastructure.Ai.Sanierung;

using AuswertungPro.Next.UI.Ai.Pipeline;
using AuswertungPro.Next.UI.Services;
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
        public DashboardRefreshNotifier DashboardRefresh { get; } = new();
        // Kartennetz-Cache (Netzlinien + raeumlicher Index): einmal gebaut, ueber alle
        // Kartenoeffnungen wiederverwendet, beim Start vorladbar. Singleton.
        public AuswertungPro.Next.UI.Mapping.NetworkFeatureCache NetworkFeatures { get; } = new();
        public IPlaywrightInstallService PlaywrightInstaller { get; }
        public IFullBackupService FullBackup { get; }
        #endregion

        #region Persistenz
        // Projektverwaltung und lokale Datenspeicherung
        public IProjectRepository Projects { get; }
        #endregion

        #region Import
        // Alle Import-Adapter für externe Datenformate
        public IPdfImportService PdfImport { get; }
        // Name-basierte Protokoll-Verteilung (Haltungen + Schaechte) aus einem Quellordner.
        public AuswertungPro.Next.Infrastructure.Import.Protocols.INameBasedProtocolDistributor NameBasedProtocolDistributor { get; }
        public IXtfImportService XtfImport { get; }
        public IWinCanDbImportService WinCanImport { get; }
        public IIbakImportService IbakImport { get; }
        public IKinsImportService KinsImport { get; }
        public IPhotoImportService PhotoImport { get; }
        // Einzel-Import eines Schacht-Protokolls (Aktualisieren + Protokoll importieren, Schachtseite).
        public ISchachtProtocolImportService SchachtProtocolImport { get; }
        #endregion

        #region Export / Protokoll
        // Export-Dienste und Protokollerzeugung
        public IExcelExportService ExcelExport { get; }
        public IProtocolService Protocols { get; }
        public ProtocolPdfExporter ProtocolPdfExporter { get; }
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

            Projects = new JsonProjectRepository();
            PdfImport = new PdfImportServiceAdapter();
            NameBasedProtocolDistributor = new AuswertungPro.Next.Infrastructure.Import.Protocols.NameBasedProtocolDistributor();
            XtfImport = new XtfImportServiceAdapter();
            WinCanImport = new WinCanDbImportService();
            IbakImport = new IbakExportImportService();
            KinsImport = new KinsImportService(WinCanImport, IbakImport);
            ExcelExport = new ExcelTemplateExportService();
            CostFieldSync = new AuswertungPro.Next.Application.DataPage.DerivedCostFieldSynchronizer();

            // Register protocol/photo/pdf services
            Protocols = new ProtocolService();
            PhotoImport = new PhotoImportService();
            SchachtProtocolImport = new AuswertungPro.Next.Infrastructure.Import.Protocols.SchachtProtocolImportService();
            ProtocolPdfExporter = new ProtocolPdfExporter();

            PlaywrightInstaller = new PlaywrightInstallService(loggerFactory.CreateLogger<PlaywrightInstallService>());
            FullBackup = new FullBackupService(
                FullBackupSourcesFactory.ErmittleAktuelleQuellen,
                KnowledgeWalCheckpoint.TryCheckpoint,
                ct => OllamaListAsync(ct));



            // Einheitliche KI-Konfiguration (1x laden, 3x projizieren)
            var aiPlatform = AiSettingsFactory.Load(AppSettingsAiSettingsProvider.ToSource(settings));

            // AI/CodeCatalog Init (AiLocalPack)
            var cfg = aiPlatform.ToRuntimeSettings();
            var catalogPaths = VsaCatalogPathResolver.Resolve(settings);
            VsaCatalogResolvedPath = catalogPaths.DisplayPath;
            CodeCatalog = CreateCodeCatalog(settings, catalogPaths.KekManifestPath, catalogPaths.XmlCatalogPaths);
            // Picker-Anordnung wie ISYBAU/WinCan (kuratierter VsaCodeTree), aber Mengen-/Uhrlage-
            // Regeln aus dem aktuellen VSA-Katalog – Codes sind EN-13508-/VSA-konform (geprueft).
            CodeSelectionCatalog = new AuswertungPro.Next.Application.Protocol.VsaCodeTreeSelectionCatalog(
                new CodeCatalogSelectionCatalog(CodeCatalog));
            VsaCodeResolver.ConfigureCatalog(CodeCatalog);
            RetrievalService? retrieval = null;
            try
            {
                var ollamaConfig = aiPlatform.ToOllamaConfig();
                var kbHttp = new HttpClient { Timeout = ollamaConfig.RequestTimeout };
                var kbCtx = new KnowledgeBaseContext();
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
            }
            catch (Exception ex)
            {
                Logger.LogWarning(ex, "KnowledgeBase-Retrieval konnte nicht initialisiert werden. KI läuft ohne KB-Kontext.");
            }

            Retrieval = retrieval;
            KnowledgeBaseDiagnostics = new KnowledgeBaseDiagnosticsRunner();

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
                KnowledgeBasePaths.GetMeasuresLearningPath(),
                KnowledgeBasePaths.GetMeasuresModelPath());
        }

        public IVideoAnalysisPipelineService CreateVideoAnalysisPipeline(
            AiRuntimeSettings cfg,
            IAiSuggestionPlausibilityService plausibility,
            HttpClient http)
        {
            return new VideoAnalysisPipelineService(cfg, PipelineCfg, plausibility, http, CodeCatalog, LoggerFactory);
        }

        public IAiSanierungOptimizationService CreateSanierungOptimization(
            AiRuntimeSettings cfg,
            HttpClient? http = null)
        {
            return new AiSanierungOptimizationService(cfg, http);
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
            if (serviceType == typeof(AuswertungPro.Next.Infrastructure.Import.Protocols.INameBasedProtocolDistributor)) return NameBasedProtocolDistributor;
            if (serviceType == typeof(IXtfImportService)) return XtfImport;
            if (serviceType == typeof(IWinCanDbImportService)) return WinCanImport;
            if (serviceType == typeof(IIbakImportService)) return IbakImport;
            if (serviceType == typeof(IKinsImportService)) return KinsImport;
            if (serviceType == typeof(ISchachtProtocolImportService)) return SchachtProtocolImport;
            if (serviceType == typeof(IExcelExportService)) return ExcelExport;
            if (serviceType == typeof(IVsaEvaluationService)) return Vsa;
            if (serviceType == typeof(IProtocolService)) return Protocols;
            if (serviceType == typeof(IKnowledgeBaseDiagnosticsRunner)) return KnowledgeBaseDiagnostics;
            if (serviceType == typeof(AuswertungPro.Next.Application.Protocol.ICodeCatalogProvider)) return CodeCatalog;
            if (serviceType == typeof(AuswertungPro.Next.Application.Protocol.IVsaCodeSelectionCatalog)) return CodeSelectionCatalog;
            if (serviceType == typeof(ILogger)) return Logger;
            if (serviceType == typeof(ILoggerFactory)) return LoggerFactory;
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
