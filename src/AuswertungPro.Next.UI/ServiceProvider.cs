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
            var aiPlatform = AiPlatformConfig.Load(settings);
            PipelineCfg = aiPlatform.ToPipelineConfig();

            // Sidecar (YOLO/DINO/SAM) — wird in App.xaml.cs async gestartet
            var sidecarDir = Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "..", "sidecar");
            if (!Directory.Exists(sidecarDir))
                sidecarDir = Path.Combine(AppContext.BaseDirectory, "sidecar");
            Sidecar = new PythonSidecarService(
                loggerFactory.CreateLogger<PythonSidecarService>(),
                Path.GetFullPath(sidecarDir),
                PipelineCfg.SidecarUrl.Host,
                PipelineCfg.SidecarUrl.Port);

            // AI/CodeCatalog Init (AiLocalPack)
            var cfg = aiPlatform.ToRuntimeConfig();

            // Startup-Warmup: 8B-Q8 + nomic + 32B (RAM) permanent vorladen
            // V4.1 Final: 8B-Q8 (11.7 GB GPU) + nomic (0.6 GB GPU) + 32B (22.8 GB RAM, num_gpu=0)
            // Sidecar (YOLO+DINO+SAM): ~6 GB GPU → Total GPU ~20 GB, ~12 GB frei
            if (cfg.Enabled)
            {
                _ = Task.Run(async () =>
                {
                    try
                    {
                        // 0. Auf Sidecar warten (max 60s) — verhindert parallelen VRAM-Kampf,
                        // bei dem Ollama bei knappen VRAM-Budgets auf CPU zurueckfaellt.
                        // Sidecar laedt YOLO+SAM (~3-4 GB) zuerst, erst dann kommt Qwen.
                        for (int i = 0; i < 60 && !Sidecar.IsAvailable; i++)
                            await System.Threading.Tasks.Task.Delay(1000);
                        if (!Sidecar.IsAvailable)
                            Logger.LogWarning(
                                "[Startup] Sidecar nach 60s nicht verfuegbar — Qwen-Warmup laeuft trotzdem");

                        using var warmupClient = cfg.CreateOllamaClient();

                        // 1. VisionModel (8B) permanent vorladen — num_gpu=-1 zwingt GPU
                        // (default -1 wird in OllamaClient zu num_gpu=999 uebersetzt =
                        // "so viele Layer wie moeglich auf GPU", verhindert CPU-Fallback).
                        await warmupClient.WarmupModelAsync(cfg.VisionModel, cfg.OllamaNumCtx, numGpu: -1);
                        Logger.LogInformation(
                            "[Startup] VisionModel {Model} vorgeladen (num_gpu=all, NUM_PARALLEL={Parallel}, ctx={Ctx})",
                            cfg.VisionModel,
                            Environment.GetEnvironmentVariable("OLLAMA_NUM_PARALLEL") ?? "?",
                            cfg.OllamaNumCtx);

                        // 2. EmbedModel (nomic-embed-text) vorladen — klein, immer auf GPU
                        if (!string.IsNullOrEmpty(cfg.EmbedModel))
                        {
                            await warmupClient.WarmupModelAsync(cfg.EmbedModel, 0, numGpu: -1);
                            Logger.LogInformation(
                                "[Startup] EmbedModel {Model} vorgeladen (num_gpu=all)", cfg.EmbedModel);
                        }

                        // 3. ReferenceModel (32B) permanent in RAM vorladen — num_gpu=0, kein VRAM
                        if (!string.IsNullOrEmpty(cfg.ReferenceVisionModel)
                            && !string.Equals(cfg.ReferenceVisionModel, cfg.VisionModel, StringComparison.OrdinalIgnoreCase))
                        {
                            try
                            {
                                using var warmupHttp = new HttpClient { BaseAddress = cfg.OllamaBaseUri, Timeout = TimeSpan.FromMinutes(10) };
                                var payload = new Dictionary<string, object?>
                                {
                                    ["model"] = cfg.ReferenceVisionModel,
                                    ["prompt"] = "",
                                    ["stream"] = false,
                                    ["keep_alive"] = "8760h",
                                    ["options"] = new Dictionary<string, object>
                                    {
                                        ["num_gpu"] = 10,  // hybrid: 10 Layers GPU + Rest RAM (~9s statt 28s)
                                        ["num_ctx"] = cfg.OllamaNumCtx > 0 ? Math.Min(cfg.OllamaNumCtx, 4096) : 4096
                                    }
                                };
                                var json = System.Text.Json.JsonSerializer.Serialize(payload);
                                using var req = new HttpRequestMessage(HttpMethod.Post, "/api/generate")
                                {
                                    Content = new StringContent(json, System.Text.Encoding.UTF8, "application/json")
                                };
                                using var resp = await warmupHttp.SendAsync(req).ConfigureAwait(false);
                                Logger.LogInformation(
                                    "[Startup] ReferenceModel {Model} vorgeladen (num_gpu=10 hybrid, komplett RAM)",
                                    cfg.ReferenceVisionModel);
                            }
                            catch (Exception exRef)
                            {
                                Logger.LogWarning(exRef,
                                    "[Startup] ReferenceModel {Model} Warmup fehlgeschlagen",
                                    cfg.ReferenceVisionModel);
                            }
                        }

                        // 4. Verifikation: pruefen ob Qwen-8B auch wirklich im VRAM ist.
                        // Wenn size_vram==0 → Ollama hat trotz num_gpu auf CPU gewechselt
                        // (passiert bei VRAM-OOM oder wenn das Modell groesser ist als frei).
                        await VerifyModelInVramAsync(cfg.OllamaBaseUri, cfg.VisionModel);
                    }
                    catch (Exception ex)
                    {
                        Logger.LogWarning(ex, "[Startup] Modell-Warmup fehlgeschlagen");
                    }
                });
            }
            var codeCatalogPath = Path.Combine(AppContext.BaseDirectory, "Data", "vsa_codes.json");
            EnsureEmbeddedCatalogFile(codeCatalogPath);
            var nodCatalogPath = ResolveVsaCatalogNodPath(settings);
            var xmlCatalogPath = nodCatalogPath ?? ResolveVsaCatalogPath(settings);
            VsaCatalogResolvedPath = xmlCatalogPath;
            var fallbackTextXmlPath = ResolveVsaCatalogTextPath(settings, xmlCatalogPath);
            CodeCatalog = !string.IsNullOrWhiteSpace(xmlCatalogPath)
                ? new AuswertungPro.Next.Application.Protocol.XmlCodeCatalogProvider(xmlCatalogPath, codeCatalogPath, fallbackTextXmlPath)
                : new AuswertungPro.Next.Application.Protocol.JsonCodeCatalogProvider(codeCatalogPath);
            RetrievalService? retrieval = null;
            try
            {
                var ollamaConfig = aiPlatform.ToOllamaConfig();
                _kbHttp = new HttpClient { Timeout = ollamaConfig.RequestTimeout };
                var kbCtx = new KnowledgeBaseContext();
                var embedder = new EmbeddingService(_kbHttp, ollamaConfig);
                retrieval = new RetrievalService(kbCtx, embedder, Settings);
                retrieval.CheckModelConsistency();
                if (retrieval.HasModelMismatch)
                    Logger.LogWarning(
                        "KB-Embedding-Modell '{StoredModel}' stimmt nicht mit aktuellem Modell '{CurrentModel}' überein. KB-Rebuild empfohlen.",
                        retrieval.StoredEmbedModel, ollamaConfig.EmbedModel);
            }
            catch (Exception ex)
            {
                // Phase 0.2: HttpClient bei Init-Fehler explizit freigeben.
                _kbHttp?.Dispose();
                _kbHttp = null;
                Logger.LogWarning(ex, "KnowledgeBase-Retrieval konnte nicht initialisiert werden. KI läuft ohne KB-Kontext.");
            }

            Retrieval = retrieval;

            var allowedCodeSet = new HashSet<string>(CodeCatalog.AllowedCodes(), StringComparer.OrdinalIgnoreCase);
            IAiSuggestionPlausibilityService plausibility = new RuleBasedAiSuggestionPlausibilityService(allowedCodeSet);

            ProtocolAi = cfg.Enabled
                ? new OllamaProtocolAiService(cfg, retrieval, plausibility)
                : new NoopProtocolAiService();

            LogCodeCatalogWarnings(CodeCatalog, xmlCatalogPath ?? codeCatalogPath);

            var channelsTable = Path.Combine(AppContext.BaseDirectory, "Data", "classification_channels.json");
            var manholesTable = Path.Combine(AppContext.BaseDirectory, "Data", "classification_manholes.json");
            Vsa = new VsaEvaluationService(channelsTable, manholesTable);

            // Brain-Mirror: Alle KI-Lerndaten nach E:\Brain spiegeln
            var brainPath = settings.BrainMirrorPath ?? Ai.KnowledgeRoot.ResolveBrainMirrorPath();
            var brainDrive = Path.GetPathRoot(brainPath);
            if (brainDrive is not null && Directory.Exists(brainDrive))
            {
                _ = new KnowledgeMirrorService(
                    Ai.KnowledgeRoot.GetRoot(),
                    brainPath,
                    logger);
                // Initialer Sync beim Start (async, blockiert nicht)
                _ = Task.Run(async () =>
                {
                    try { await KnowledgeMirrorService.Current!.SyncNowAsync(); }
                    catch (Exception ex) { Debug.WriteLine($"[BrainMirror] Initialer Sync fehlgeschlagen: {ex.Message}"); }
                });
            }
            else
            {
                logger.LogWarning("Brain-Mirror deaktiviert: Laufwerk {Drive} nicht verfuegbar", brainDrive);
            }

            MeasureRecommendation = new Infrastructure.Ai.MeasureRecommendationService(
                Ai.KnowledgeRoot.GetMeasuresLearningPath(),
                Ai.KnowledgeRoot.GetMeasuresModelPath());

            // Eigendevis - mit Submissions-Positionskatalog (Markt-Referenzpreise aus Buerglen 2026)
            var devisMappingPath = Path.Combine(AppContext.BaseDirectory, "Config", "devis_mappings.json");
            var devisMappingService = new DevisMappingService(devisMappingPath);
            var submissionsCatalogPath = Path.Combine(AppContext.BaseDirectory, "Config", "submission_positionen.json");
            SubmissionsPositions = new Infrastructure.Devis.SubmissionsPositionService(submissionsCatalogPath);

            // Historische Sanierungs-Referenzen (Buerglen 2024-2026, ~217 Haltungen)
            var histPath = Path.Combine(AppContext.BaseDirectory, "Config", "historische_sanierungen.json");
            HistorischeSanierungen = new Infrastructure.Devis.HistorischeSanierungenService(histPath);

            // Marktdaten-Import-Service (User kann neue JSONs aus Knowledge/sanierung/ einlesen)
            var configDir = Path.Combine(AppContext.BaseDirectory, "Config");
            MarktdatenImport = new Infrastructure.Devis.MarktdatenImportService(
                configDir, SubmissionsPositions, HistorischeSanierungen);

            // Hard-Constraint-RulesEngine fuer Sanierungsverfahren (vor KI-Anfrage)
            // Quelle: Knowledge/sanierung/rehabilitation_methods.yaml + products_and_manufacturers.yaml
            // + User-Regeln aus Config/sanierung_user_rules.json (im UI editierbar)
            var userRulesPath = Path.Combine(AppContext.BaseDirectory, "Config", "sanierung_user_rules.json");
            SanierungUserRules = new Infrastructure.Sanierung.SanierungUserRulesService(userRulesPath);
            var rehabMethodsPath = Path.Combine(AppContext.BaseDirectory, "Config", "rehabilitation_methods.json");
            RehabRulesEngine = new Infrastructure.Sanierung.RehabilitationRulesEngine(SanierungUserRules, rehabMethodsPath);

            DevisGenerator = new Infrastructure.Devis.DevisGenerator(devisMappingService);
            DevisExcelExporter = new DevisExcelExporter();
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

        private static string? ResolveVsaCatalogPath(AppSettings settings)
        {
            if (!string.IsNullOrWhiteSpace(settings.VsaCatalogSecXmlPath))
            {
                if (File.Exists(settings.VsaCatalogSecXmlPath))
                    return settings.VsaCatalogSecXmlPath;

                if (Directory.Exists(settings.VsaCatalogSecXmlPath))
                {
                    var fromDir = FindCatalogInRoot(settings.VsaCatalogSecXmlPath);
                    if (!string.IsNullOrWhiteSpace(fromDir))
                        return fromDir;
                }
            }

            var env = Environment.GetEnvironmentVariable("VSA_CATALOG_SEC_XML");
            if (!string.IsNullOrWhiteSpace(env) && File.Exists(env))
                return env;

            var envRoot = Environment.GetEnvironmentVariable("VSA_CATALOG_ROOT");
            if (!string.IsNullOrWhiteSpace(envRoot) && Directory.Exists(envRoot))
            {
                var fromRoot = FindCatalogInRoot(envRoot);
                if (!string.IsNullOrWhiteSpace(fromRoot))
                    return fromRoot;
            }

            if (!string.IsNullOrWhiteSpace(settings.LastProjectPath))
            {
                var candidate = Path.Combine(
                    settings.LastProjectPath,
                    "DISK1",
                    "System",
                    "ProgramData",
                    "CDLAB",
                    "Common",
                    "Catalogs",
                    "Version4",
                    "EN13508_VSA_CH_DEU_SEC.xml");
                if (File.Exists(candidate))
                    return candidate;

                var fromProject = FindCatalogInRoot(Path.Combine(
                    settings.LastProjectPath,
                    "DISK1",
                    "System",
                    "ProgramData",
                    "CDLAB",
                    "Common",
                    "Catalogs"));
                if (!string.IsNullOrWhiteSpace(fromProject))
                    return fromProject;
            }

            // WinCan catalog directory (user-configured via Katalog-Auswahl)
            if (!string.IsNullOrWhiteSpace(settings.WinCanCatalogDirectory))
            {
                var fromWinCan = FindCatalogInRoot(settings.WinCanCatalogDirectory);
                if (!string.IsNullOrWhiteSpace(fromWinCan))
                    return fromWinCan;
            }

            // Auto-detect common WinCanVX installation paths
            var commonPaths = new[]
            {
                @"C:\CDLAB\WinCanVX\WinCanMerger\App_Data\Catalogs",
                @"C:\Program Files\CDLAB\WinCanVX\WinCanMerger\App_Data\Catalogs",
                @"C:\Program Files (x86)\CDLAB\WinCanVX\WinCanMerger\App_Data\Catalogs"
            };
            foreach (var commonPath in commonPaths)
            {
                var fromCommon = FindCatalogInRoot(commonPath);
                if (!string.IsNullOrWhiteSpace(fromCommon))
                    return fromCommon;
            }

            return null;
        }

        private static string? ResolveVsaCatalogNodPath(AppSettings settings)
        {
            if (!string.IsNullOrWhiteSpace(settings.VsaCatalogNodXmlPath))
            {
                if (File.Exists(settings.VsaCatalogNodXmlPath))
                    return settings.VsaCatalogNodXmlPath;

                if (Directory.Exists(settings.VsaCatalogNodXmlPath))
                {
                    var fromDir = FindCatalogInRootNod(settings.VsaCatalogNodXmlPath);
                    if (!string.IsNullOrWhiteSpace(fromDir))
                        return fromDir;
                }
            }

            var env = Environment.GetEnvironmentVariable("VSA_CATALOG_NOD_XML");
            if (!string.IsNullOrWhiteSpace(env) && File.Exists(env))
                return env;

            var envRoot = Environment.GetEnvironmentVariable("VSA_CATALOG_NOD_ROOT");
            if (!string.IsNullOrWhiteSpace(envRoot) && Directory.Exists(envRoot))
            {
                var fromRoot = FindCatalogInRootNod(envRoot);
                if (!string.IsNullOrWhiteSpace(fromRoot))
                    return fromRoot;
            }

            return null;
        }

        private static string? FindCatalogInRoot(string root)
        {
            var v4 = Path.Combine(root, "Version4");
            var candidates = new[]
            {
                Path.Combine(root, "EN13508_VSA-2019_CH_DEU_SEC.xml"),
                Path.Combine(root, "EN13508_VSA_CH_DEU_SEC.xml"),
                Path.Combine(v4, "EN13508_VSA-2019_CH_DEU_SEC.xml"),
                Path.Combine(v4, "EN13508_VSA_CH_DEU_SEC.xml"),
            };

            foreach (var c in candidates)
            {
                if (File.Exists(c))
                    return c;
            }

            return null;
        }

        private static string? FindCatalogInRootNod(string root)
        {
            var v4 = Path.Combine(root, "Version4");
            var candidates = new[]
            {
                Path.Combine(v4, "EN13508_VSA-2019_CH_DEU_NOD.xml"),
                Path.Combine(v4, "EN13508_VSA_CH_DEU_NOD.xml"),
                Path.Combine(root, "EN13508_VSA-2019_CH_DEU_NOD.xml"),
                Path.Combine(root, "EN13508_VSA_CH_DEU_NOD.xml")
            };

            foreach (var c in candidates)
            {
                if (File.Exists(c))
                    return c;
            }

            return null;
        }

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

            var candidates = new[]
            {
                Path.Combine(root, "EN13508_VSA_CH_DEU_SEC.xml"),
                Path.Combine(root, "EN13508_VSA-2019_CH_DEU_SEC.xml")
            };

            foreach (var c in candidates)
            {
                if (File.Exists(c))
                    return c;
            }

            return null;
        }

        private static void EnsureEmbeddedCatalogFile(string targetPath)
        {
            if (File.Exists(targetPath))
                return;

            try
            {
                var dir = Path.GetDirectoryName(targetPath);
                if (!string.IsNullOrWhiteSpace(dir))
                    Directory.CreateDirectory(dir);

                var asm = Assembly.GetExecutingAssembly();
                var resourceName = "AuswertungPro.Next.UI.Data.vsa_codes.json";
                using var stream = asm.GetManifestResourceStream(resourceName);
                if (stream is null)
                    return;

                using var fs = File.Create(targetPath);
                stream.CopyTo(fs);
            }
            catch
            {
                // ignore, fallback handled by JsonCodeCatalogProvider
            }
        }

        /// <summary>
        /// Prueft nach dem Warmup via /api/ps ob das Modell im VRAM liegt.
        /// Falls size_vram == 0 → Ollama hat auf CPU zurueckgefallen (VRAM zu knapp).
        /// Loggt Warnung, wirft nicht — der Warmup hat trotzdem "geklappt".
        /// </summary>
        private async System.Threading.Tasks.Task VerifyModelInVramAsync(Uri ollamaBaseUri, string model)
        {
            try
            {
                using var http = new HttpClient { BaseAddress = ollamaBaseUri, Timeout = TimeSpan.FromSeconds(5) };
                using var resp = await http.GetAsync("/api/ps").ConfigureAwait(false);
                if (!resp.IsSuccessStatusCode) return;

                var body = await resp.Content.ReadAsStringAsync().ConfigureAwait(false);
                using var doc = System.Text.Json.JsonDocument.Parse(body);
                if (!doc.RootElement.TryGetProperty("models", out var models)) return;

                var modelPrefix = model.Split(':')[0];
                foreach (var m in models.EnumerateArray())
                {
                    var name = m.TryGetProperty("name", out var n) ? n.GetString() ?? "" : "";
                    if (!name.Equals(model, StringComparison.OrdinalIgnoreCase)
                        && !name.StartsWith(modelPrefix, StringComparison.OrdinalIgnoreCase))
                        continue;

                    long sizeVram = m.TryGetProperty("size_vram", out var v) ? v.GetInt64() : 0;
                    long sizeTotal = m.TryGetProperty("size", out var s) ? s.GetInt64() : 0;
                    double vramGb = sizeVram / 1_073_741_824.0;
                    double totalGb = sizeTotal / 1_073_741_824.0;

                    if (sizeVram == 0)
                    {
                        Logger.LogWarning(
                            "[Startup] Modell {Model} laeuft auf CPU (size_vram=0, total={Total:F1}GB) — "
                            + "VRAM zu knapp oder Ollama konnte nicht auf GPU laden. Prueffe nvidia-smi und andere VRAM-Nutzer.",
                            name, totalGb);
                    }
                    else
                    {
                        Logger.LogInformation(
                            "[Startup] Modell {Model} im VRAM: {Vram:F1}GB von {Total:F1}GB",
                            name, vramGb, totalGb);
                    }
                    return;
                }

                Logger.LogWarning("[Startup] Modell {Model} nicht in /api/ps gefunden nach Warmup", model);
            }
            catch (Exception ex)
            {
                Logger.LogDebug(ex, "[Startup] VRAM-Verifikation fehlgeschlagen (nicht kritisch)");
            }
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
