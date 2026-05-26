using System;
using System.IO;
using System.Linq;
using System.Net.Http;
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
        public AppSettings Settings { get; }
        public DiagnosticsOptions Diagnostics { get; }
        public ILogger Logger { get; }
        public ILoggerFactory LoggerFactory { get; }
        public ErrorCodeGenerator ErrorCodes { get; } = new();
        private const string IkasManifestFileName = "ikas_vsa_catalog_manifest.json";


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

            Projects = new JsonProjectRepository();
            PdfImport = new PdfImportServiceAdapter();
            XtfImport = new XtfImportServiceAdapter();
            WinCanImport = new WinCanDbImportService();
            IbakImport = new IbakExportImportService();
            KinsImport = new KinsImportService(WinCanImport, IbakImport);
            ExcelExport = new ExcelTemplateExportService();

            // Register protocol/photo/pdf services
            Protocols = new ProtocolService();
            PhotoImport = new PhotoImportService();
            ProtocolPdfExporter = new ProtocolPdfExporter();

            PlaywrightInstaller = new PlaywrightInstallService(loggerFactory.CreateLogger<PlaywrightInstallService>());



            // Einheitliche KI-Konfiguration (1x laden, 3x projizieren)
            var aiPlatform = AiSettingsFactory.Load(AppSettingsAiSettingsProvider.ToSource(settings));
            PipelineCfg = aiPlatform.ToPipelineConfig();

            // AI/CodeCatalog Init (AiLocalPack)
            var cfg = aiPlatform.ToRuntimeSettings();
            var secCatalogPath = ResolveVsaCatalogPath(settings);
            var nodCatalogPath = ResolveVsaCatalogNodPath(settings);
            var ikasManifestPath = ResolveIkasCatalogManifestPath();
            var xmlCatalogPaths = new[] { secCatalogPath, nodCatalogPath }
                .Where(p => !string.IsNullOrWhiteSpace(p))
                .Select(p => p!)
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToList();
            var catalogSourcePaths = new[] { ikasManifestPath }
                .Where(p => !string.IsNullOrWhiteSpace(p))
                .Select(p => p!)
                .Concat(xmlCatalogPaths)
                .ToList();
            VsaCatalogResolvedPath = catalogSourcePaths.Count > 0
                ? string.Join(" | ", catalogSourcePaths)
                : null;
            CodeCatalog = CreateCodeCatalog(settings, ikasManifestPath, xmlCatalogPaths);
            VsaCodeTreeCatalogAdapter.Apply(CodeCatalog);
            VsaCodeResolver.ConfigureCatalog(CodeCatalog);
            RetrievalService? retrieval = null;
            try
            {
                var ollamaConfig = aiPlatform.ToOllamaConfig();
                var kbHttp = new HttpClient { Timeout = ollamaConfig.RequestTimeout };
                var kbCtx = new KnowledgeBaseContext();
                var embedder = new EmbeddingService(kbHttp, ollamaConfig);
                retrieval = new RetrievalService(kbCtx, embedder);
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
            Vsa = new VsaEvaluationService(channelsTable, manholesTable);

            MeasureRecommendation = new Infrastructure.Ai.MeasureRecommendationService(
                KnowledgeBasePaths.GetMeasuresLearningPath(),
                KnowledgeBasePaths.GetMeasuresModelPath());

            // Eigendevis
            var devisMappingPath = Path.Combine(AppContext.BaseDirectory, "Config", "devis_mappings.json");
            var devisMappingService = new DevisMappingService(devisMappingPath);
            DevisGenerator = new Infrastructure.Devis.DevisGenerator(devisMappingService);
            DevisExcelExporter = new DevisExcelExporter();
        }

        public IVideoAnalysisPipelineService CreateVideoAnalysisPipeline(
            AiRuntimeSettings cfg,
            IAiSuggestionPlausibilityService plausibility,
            HttpClient http)
        {
            return new VideoAnalysisPipelineService(cfg, PipelineCfg, plausibility, http);
        }

        public IAiSanierungOptimizationService CreateSanierungOptimization(
            AiRuntimeSettings cfg,
            HttpClient? http = null)
        {
            return new AiSanierungOptimizationService(cfg, http);
        }

        private static string? ResolveVsaCatalogPath(AppSettings settings)
        {
            if (!string.IsNullOrWhiteSpace(settings.VsaCatalogSecXmlPath))
            {
                if (IsCanonicalVsa2019Catalog(settings.VsaCatalogSecXmlPath, Vsa2019CatalogResolver.SectionCatalogFileName))
                    return settings.VsaCatalogSecXmlPath;

                if (Directory.Exists(settings.VsaCatalogSecXmlPath))
                {
                    var fromDir = Vsa2019CatalogResolver.FindSectionCatalog(settings.VsaCatalogSecXmlPath);
                    if (!string.IsNullOrWhiteSpace(fromDir))
                        return fromDir;
                }
            }

            var env = Environment.GetEnvironmentVariable("VSA_CATALOG_SEC_XML");
            if (IsCanonicalVsa2019Catalog(env, Vsa2019CatalogResolver.SectionCatalogFileName))
                return env;

            var envRoot = Environment.GetEnvironmentVariable("VSA_CATALOG_ROOT");
            if (!string.IsNullOrWhiteSpace(envRoot) && Directory.Exists(envRoot))
            {
                var fromRoot = Vsa2019CatalogResolver.FindSectionCatalog(envRoot);
                if (!string.IsNullOrWhiteSpace(fromRoot))
                    return fromRoot;
            }

            // WinCan catalog directory (user-configured via Katalog-Auswahl)
            if (!string.IsNullOrWhiteSpace(settings.WinCanCatalogDirectory))
            {
                var fromWinCan = Vsa2019CatalogResolver.FindSectionCatalog(settings.WinCanCatalogDirectory);
                if (!string.IsNullOrWhiteSpace(fromWinCan))
                    return fromWinCan;
            }

            foreach (var root in Vsa2019CatalogResolver.GetDefaultCatalogRoots(lastProjectPath: settings.LastProjectPath))
            {
                var fromCommon = Vsa2019CatalogResolver.FindSectionCatalog(root);
                if (!string.IsNullOrWhiteSpace(fromCommon))
                    return fromCommon;
            }

            return null;
        }

        private static string? ResolveVsaCatalogNodPath(AppSettings settings)
        {
            if (!string.IsNullOrWhiteSpace(settings.VsaCatalogNodXmlPath))
            {
                if (IsCanonicalVsa2019Catalog(settings.VsaCatalogNodXmlPath, Vsa2019CatalogResolver.NodeCatalogFileName))
                    return settings.VsaCatalogNodXmlPath;

                if (Directory.Exists(settings.VsaCatalogNodXmlPath))
                {
                    var fromDir = Vsa2019CatalogResolver.FindNodeCatalog(settings.VsaCatalogNodXmlPath);
                    if (!string.IsNullOrWhiteSpace(fromDir))
                        return fromDir;
                }
            }

            var env = Environment.GetEnvironmentVariable("VSA_CATALOG_NOD_XML");
            if (IsCanonicalVsa2019Catalog(env, Vsa2019CatalogResolver.NodeCatalogFileName))
                return env;

            var envRoot = Environment.GetEnvironmentVariable("VSA_CATALOG_NOD_ROOT");
            if (!string.IsNullOrWhiteSpace(envRoot) && Directory.Exists(envRoot))
            {
                var fromRoot = Vsa2019CatalogResolver.FindNodeCatalog(envRoot);
                if (!string.IsNullOrWhiteSpace(fromRoot))
                    return fromRoot;
            }

            if (!string.IsNullOrWhiteSpace(settings.WinCanCatalogDirectory))
            {
                var fromWinCan = Vsa2019CatalogResolver.FindNodeCatalog(settings.WinCanCatalogDirectory);
                if (!string.IsNullOrWhiteSpace(fromWinCan))
                    return fromWinCan;
            }

            foreach (var root in Vsa2019CatalogResolver.GetDefaultCatalogRoots(lastProjectPath: settings.LastProjectPath))
            {
                var fromCommon = Vsa2019CatalogResolver.FindNodeCatalog(root);
                if (!string.IsNullOrWhiteSpace(fromCommon))
                    return fromCommon;
            }

            return null;
        }

        private static string? ResolveIkasCatalogManifestPath()
        {
            var env = Environment.GetEnvironmentVariable("IKAS_VSA_CATALOG_MANIFEST");
            if (!string.IsNullOrWhiteSpace(env) && File.Exists(env))
                return env;

            var fromData = Path.Combine(AppContext.BaseDirectory, "Data", IkasManifestFileName);
            if (File.Exists(fromData))
                return fromData;

            return null;
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

        private static string? ResolveVsaCatalogTextPath(AppSettings settings, string? resolvedXmlPath)
        {
            var configured = settings.VsaCatalogSecXmlPath;
            if (!string.IsNullOrWhiteSpace(configured))
            {
                if (File.Exists(configured))
                {
                    var dir = Path.GetDirectoryName(configured);
                    var fromDir = FindTextCatalogInRoot(dir);
                    if (!string.IsNullOrWhiteSpace(fromDir))
                        return fromDir;
                }
                else if (Directory.Exists(configured))
                {
                    var fromRoot = FindTextCatalogInRoot(configured);
                    if (!string.IsNullOrWhiteSpace(fromRoot))
                        return fromRoot;
                }
            }

            if (!string.IsNullOrWhiteSpace(resolvedXmlPath))
            {
                var dir = Path.GetDirectoryName(resolvedXmlPath);
                if (!string.IsNullOrWhiteSpace(dir))
                {
                    var parent = Directory.GetParent(dir);
                    if (parent is not null && string.Equals(Path.GetFileName(dir), "Version4", StringComparison.OrdinalIgnoreCase))
                    {
                        var fromRoot = FindTextCatalogInRoot(parent.FullName);
                        if (!string.IsNullOrWhiteSpace(fromRoot))
                            return fromRoot;
                    }
                }
            }

            return null;
        }

        private static string? FindTextCatalogInRoot(string? root)
        {
            if (string.IsNullOrWhiteSpace(root) || !Directory.Exists(root))
                return null;

            return Vsa2019CatalogResolver.FindSectionCatalog(root);
        }

        private static bool IsCanonicalVsa2019Catalog(string? path, string fileName)
            => !string.IsNullOrWhiteSpace(path)
               && File.Exists(path)
               && string.Equals(Path.GetFileName(path), fileName, StringComparison.OrdinalIgnoreCase);

        private static AuswertungPro.Next.Application.Protocol.ICodeCatalogProvider CreateCodeCatalog(
            AppSettings settings,
            string? ikasManifestPath,
            IReadOnlyList<string> xmlCatalogPaths)
        {
            var providers = new List<AuswertungPro.Next.Application.Protocol.ICodeCatalogProvider>();

            if (!string.IsNullOrWhiteSpace(ikasManifestPath) && File.Exists(ikasManifestPath))
            {
                providers.Add(new AuswertungPro.Next.Application.Protocol.ManifestCodeCatalogProvider(ikasManifestPath));
            }

            providers.AddRange(xmlCatalogPaths
                .Select(path => new AuswertungPro.Next.Application.Protocol.SourceDecoratingCodeCatalogProvider(
                    new AuswertungPro.Next.Application.Protocol.XmlCodeCatalogProvider(
                    path,
                    fallbackJsonPath: null,
                    fallbackTextXmlPath: ResolveVsaCatalogTextPath(settings, path)),
                    AuswertungPro.Next.Application.Protocol.IkasCatalogSources.WinCanFallback))
                .Cast<AuswertungPro.Next.Application.Protocol.ICodeCatalogProvider>());

            return new AuswertungPro.Next.Application.Protocol.CompositeCodeCatalogProvider(providers);
        }

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
    }
}
