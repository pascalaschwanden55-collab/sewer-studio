using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Reflection;
using Microsoft.Extensions.Logging;

using AuswertungPro.Next.Application.Devis;
using AuswertungPro.Next.Application.Diagnostics;
using AuswertungPro.Next.Application.Export;
using AuswertungPro.Next.Application.Import;
using AuswertungPro.Next.Application.Media;
using AuswertungPro.Next.Application.Projects;
using AuswertungPro.Next.Application.Protocol;
// using AuswertungPro.Next.Application.Reports; // entfernt, da bereits oben vorhanden
using AuswertungPro.Next.Application.Vsa;

using AuswertungPro.Next.Infrastructure.Devis;
using AuswertungPro.Next.Infrastructure.Export;
using AuswertungPro.Next.Infrastructure.Export.Excel;
using AuswertungPro.Next.Infrastructure.Import.Pdf;
using AuswertungPro.Next.Infrastructure.Import.Xtf;
using AuswertungPro.Next.Infrastructure.Import.WinCan;
using AuswertungPro.Next.Infrastructure.Import.Ibak;
using AuswertungPro.Next.Infrastructure.Import.Kins;
using AuswertungPro.Next.Infrastructure.Projects;
using AuswertungPro.Next.Infrastructure.Vsa;

// AI/CodeCatalog services are currently defined in this UI namespace:
using AuswertungPro.Next.UI.ViewModels.Protocol;
using AuswertungPro.Next.UI.Ai;
using AuswertungPro.Next.UI.Ai.KnowledgeBase;
using AuswertungPro.Next.UI.Ai.Ollama;
using AuswertungPro.Next.UI.Ai.Pipeline;
using AuswertungPro.Next.UI.Ai.Sanierung;
using AuswertungPro.Next.UI.Modules;
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
    public sealed class ServiceProvider : IServiceProvider, IDisposable
    {
        // Phase 0.2: KB-HttpClient als Feld halten — verhindert Socket-Leak im
        // catch-Pfad (vorher local var ohne Dispose) und ermoeglicht Cleanup
        // bei App-Shutdown via IDisposable. Audit B9 / Claude-CRITICAL.
        private HttpClient? _kbHttp;
        private bool _disposed;

        public AppSettings Settings { get; }
        public DiagnosticsOptions Diagnostics { get; }
        public ILogger Logger { get; }
        public ILoggerFactory LoggerFactory { get; }
        public ErrorCodeGenerator ErrorCodes { get; } = new();


        public IProjectRepository Projects { get; }
        public IPdfImportService PdfImport { get; }
        public IXtfImportService XtfImport { get; }
        public IWinCanDbImportService WinCanImport { get; }
        public IIbakImportService IbakImport { get; }
        public IKinsImportService KinsImport { get; }
        public IExcelExportService ExcelExport { get; }
        public IVsaEvaluationService Vsa { get; }

        // Protocol/Photo/PDF services

        public IProtocolService Protocols { get; }
        public IPhotoImportService PhotoImport { get; }
        public ProtocolPdfExporter ProtocolPdfExporter { get; }

        // AI/CodeCatalog Services
        public IProtocolAiService ProtocolAi { get; }
        public AuswertungPro.Next.Application.Protocol.ICodeCatalogProvider CodeCatalog { get; }
        public string? VsaCatalogResolvedPath { get; }

        public IDialogService Dialogs { get; } = new DialogService();
        public IPlaywrightInstallService PlaywrightInstaller { get; }
        public PipelineConfig PipelineCfg { get; }
        public PythonSidecarService Sidecar { get; }

        public IMeasureRecommendationService MeasureRecommendation { get; }
        public IRetrievalService? Retrieval { get; }

        // Eigendevis
        public IDevisGenerator DevisGenerator { get; }
        public DevisExcelExporter DevisExcelExporter { get; }

        public ServiceProvider(AppSettings settings, DiagnosticsOptions diagnostics, ILogger logger, ILoggerFactory loggerFactory)
                // Removed misplaced property initialization
        {
            Settings = settings;
            Diagnostics = diagnostics;
            Logger = logger;
            LoggerFactory = loggerFactory;

            // Phase 5.2.A: Domain-IO-Services aus ImportExportModule.
            var importExport = ImportExportModule.Configure();
            Projects = importExport.Projects;
            PdfImport = importExport.PdfImport;
            XtfImport = importExport.XtfImport;
            WinCanImport = importExport.WinCanImport;
            IbakImport = importExport.IbakImport;
            KinsImport = importExport.KinsImport;
            ExcelExport = importExport.ExcelExport;

            // Phase 5.2.B: Protokoll-/Foto-/PDF-Services aus ProtocolReportsModule.
            var protocolReports = ProtocolReportsModule.Configure();
            Protocols = protocolReports.Protocols;
            PhotoImport = protocolReports.PhotoImport;
            ProtocolPdfExporter = protocolReports.ProtocolPdfExporter;

            PlaywrightInstaller = new PlaywrightInstallService(loggerFactory.CreateLogger<PlaywrightInstallService>());



            // Phase 5.2.E: KI-Pipeline (Sidecar + Warmup) aus AiPipelineModule.
            var aiPlatform = AiPlatformConfig.Load(settings);
            PipelineCfg = aiPlatform.ToPipelineConfig();
            Sidecar = AiPipelineModule.CreateSidecar(PipelineCfg, loggerFactory);

            var cfg = aiPlatform.ToRuntimeConfig();
            if (cfg.Enabled)
                AiPipelineModule.RunWarmupInBackground(cfg, Sidecar, Logger);
            // Phase 5.2.D: VSA-Katalog-Resolution aus VsaCatalogResolver-Helper.
            var codeCatalogPath = Path.Combine(AppContext.BaseDirectory, "Data", "vsa_codes.json");
            VsaCatalogResolver.EnsureEmbeddedCatalogFile(codeCatalogPath);
            var nodCatalogPath = VsaCatalogResolver.ResolveNodPath(settings);
            var xmlCatalogPath = nodCatalogPath ?? VsaCatalogResolver.ResolveSecPath(settings);
            VsaCatalogResolvedPath = xmlCatalogPath;
            var fallbackTextXmlPath = VsaCatalogResolver.ResolveTextPath(settings, xmlCatalogPath);
            CodeCatalog = !string.IsNullOrWhiteSpace(xmlCatalogPath)
                ? new AuswertungPro.Next.Application.Protocol.XmlCodeCatalogProvider(xmlCatalogPath, codeCatalogPath, fallbackTextXmlPath)
                : new AuswertungPro.Next.Application.Protocol.JsonCodeCatalogProvider(codeCatalogPath);
            // Phase 5.2.F: KnowledgeBase-Retrieval + BrainMirror aus KnowledgeBaseModule.
            var kb = KnowledgeBaseModule.ConfigureRetrieval(aiPlatform, settings, Logger);
            _kbHttp = kb.KbHttp;
            Retrieval = kb.Retrieval;

            var allowedCodeSet = new HashSet<string>(CodeCatalog.AllowedCodes(), StringComparer.OrdinalIgnoreCase);
            IAiSuggestionPlausibilityService plausibility = new RuleBasedAiSuggestionPlausibilityService(allowedCodeSet);

            ProtocolAi = cfg.Enabled
                ? new OllamaProtocolAiService(cfg, kb.Retrieval as RetrievalService, plausibility)
                : new NoopProtocolAiService();

            LogCodeCatalogWarnings(CodeCatalog, xmlCatalogPath ?? codeCatalogPath);

            var channelsTable = Path.Combine(AppContext.BaseDirectory, "Data", "classification_channels.json");
            var manholesTable = Path.Combine(AppContext.BaseDirectory, "Data", "classification_manholes.json");
            Vsa = new VsaEvaluationService(channelsTable, manholesTable);

            KnowledgeBaseModule.StartBrainMirror(settings, logger);

            MeasureRecommendation = new Infrastructure.Ai.MeasureRecommendationService(
                Ai.KnowledgeRoot.GetMeasuresLearningPath(),
                Ai.KnowledgeRoot.GetMeasuresModelPath());

            // Phase 5.2.C: Devis-/Sanierungs-Services aus DevisSanierungModule.
            // Konfiguriert ueber 5 JSON-Files in Config/, in korrekter Konstruktor-Reihenfolge.
            var devisSanierung = DevisSanierungModule.Configure();
            DevisGenerator = devisSanierung.DevisGenerator;
            DevisExcelExporter = devisSanierung.DevisExcelExporter;
            SubmissionsPositions = devisSanierung.SubmissionsPositions;
            HistorischeSanierungen = devisSanierung.HistorischeSanierungen;
            MarktdatenImport = devisSanierung.MarktdatenImport;
            RehabRulesEngine = devisSanierung.RehabRulesEngine;
            SanierungUserRules = devisSanierung.SanierungUserRules;
        }

        public Infrastructure.Devis.SubmissionsPositionService SubmissionsPositions { get; private set; } = null!;
        public Infrastructure.Devis.HistorischeSanierungenService HistorischeSanierungen { get; private set; } = null!;
        public Infrastructure.Devis.MarktdatenImportService MarktdatenImport { get; private set; } = null!;
        public Infrastructure.Sanierung.RehabilitationRulesEngine RehabRulesEngine { get; private set; } = null!;
        public Infrastructure.Sanierung.SanierungUserRulesService SanierungUserRules { get; private set; } = null!;

        public IVideoAnalysisPipelineService CreateVideoAnalysisPipeline(
            AiRuntimeConfig cfg,
            IAiSuggestionPlausibilityService plausibility,
            HttpClient http)
        {
            return new VideoAnalysisPipelineService(cfg, plausibility, http);
        }

        public IAiSanierungOptimizationService CreateSanierungOptimization(
            AiRuntimeConfig cfg,
            HttpClient? http = null)
        {
            return new AiSanierungOptimizationService(cfg, http);
        }

        // Phase 5.2.D: ResolveVsaCatalogPath / ResolveVsaCatalogNodPath /
        // FindCatalogInRoot / FindCatalogInRootNod sind nach VsaCatalogResolver migriert.

        private void LogCodeCatalogWarnings(AuswertungPro.Next.Application.Protocol.ICodeCatalogProvider provider, string? sourcePath)
        {
            IReadOnlyList<string>? warnings = provider switch
            {
                AuswertungPro.Next.Application.Protocol.XmlCodeCatalogProvider xml => xml.LastLoadWarnings,
                AuswertungPro.Next.Application.Protocol.JsonCodeCatalogProvider json => json.LastLoadWarnings,
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

        // Phase 5.2.D: ResolveVsaCatalogTextPath / FindTextCatalogInRoot /
        // EnsureEmbeddedCatalogFile sind nach VsaCatalogResolver migriert.
        // Phase 5.2.E: VerifyModelInVramAsync und Warmup-Task sind nach AiPipelineModule migriert.

        public object? GetService(Type serviceType)
        {
            if (serviceType == typeof(IProjectRepository)) return Projects;
            if (serviceType == typeof(IPdfImportService)) return PdfImport;
            if (serviceType == typeof(IXtfImportService)) return XtfImport;
            if (serviceType == typeof(IWinCanDbImportService)) return WinCanImport;
            if (serviceType == typeof(IIbakImportService)) return IbakImport;
            if (serviceType == typeof(IKinsImportService)) return KinsImport;
            if (serviceType == typeof(IExcelExportService)) return ExcelExport;
            if (serviceType == typeof(IVsaEvaluationService)) return Vsa;
            if (serviceType == typeof(IProtocolService)) return Protocols;
            if (serviceType == typeof(ILogger)) return Logger;
            if (serviceType == typeof(ILoggerFactory)) return LoggerFactory;
            return null;
        }

        // Phase 0.2: Cleanup beim App-Shutdown (Audit B9 / kbHttp-Leak).
        // Caller (App.OnExit) sollte Dispose aufrufen — HttpClient bleibt
        // sonst bis Prozessende, was bei normalem Exit ok ist, aber bei
        // mehrfacher ServiceProvider-Erzeugung Sockets leakt.
        public void Dispose()
        {
            if (_disposed) return;
            _disposed = true;
            _kbHttp?.Dispose();
            _kbHttp = null;
        }
    }
}
