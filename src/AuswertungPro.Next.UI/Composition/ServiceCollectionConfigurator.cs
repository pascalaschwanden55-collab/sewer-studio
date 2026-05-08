using System;
using System.Collections.Generic;
using System.IO;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using AuswertungPro.Next.Application.Ai;
using AuswertungPro.Next.Application.Ai.Annotation;
using AuswertungPro.Next.Application.Ai.KnowledgeBase;
using AuswertungPro.Next.Application.Devis;
using AuswertungPro.Next.Application.Diagnostics;
using AuswertungPro.Next.Application.Export;
using AuswertungPro.Next.Application.Import;
using AuswertungPro.Next.Application.Media;
using AuswertungPro.Next.Application.Projects;
using AuswertungPro.Next.Application.Protocol;
using AuswertungPro.Next.Application.Reports;
using AuswertungPro.Next.Application.Vsa;
using AuswertungPro.Next.Infrastructure.Devis;
using AuswertungPro.Next.Infrastructure.Sanierung;
using AuswertungPro.Next.Infrastructure.Vsa;
using AuswertungPro.Next.UI.Ai;
using AuswertungPro.Next.UI.Ai.Pipeline;
using AuswertungPro.Next.UI.Modules;
using AuswertungPro.Next.UI.Services;
using AuswertungPro.Next.UI.ViewModels.Protocol;
using AuswertungPro.Next.Infrastructure.Ai.KnowledgeBase;

namespace AuswertungPro.Next.UI.Composition;

/// <summary>
/// Phase 5.1.A: Erstes Skelett des DI-Containers (Microsoft.Extensions.DependencyInjection).
///
/// Parallel zu ServiceProvider — beide existieren gleichzeitig waehrend der Migration:
///   - ServiceProvider liefert weiter alle Aufrufer ueber App.Services (legacy)
///   - Dieser DI-Container ist initial verfuegbar, aber von keinem Aufrufer benutzt
///
/// In Etappe 2 werden Aufrufer schrittweise auf Constructor-Injection umgestellt;
/// in Etappe 4 wird ServiceProvider als Compatibility-Shim reduziert oder geloescht.
///
/// Aktuell registriert nur die bereits migrierten Modul-Services (5.2.A-C):
///   - ImportExportModule (Projects, PdfImport, XtfImport, WinCanImport, IbakImport,
///     KinsImport, ExcelExport)
///   - ProtocolReportsModule (Protocols, PhotoImport, ProtocolPdfExporter)
///   - DevisSanierungModule (DevisGenerator, DevisExcelExporter, SubmissionsPositions,
///     HistorischeSanierungen, MarktdatenImport, RehabRulesEngine, SanierungUserRules)
///
/// AiPipeline + KnowledgeBase + VsaCatalogResolver bleiben fuer Etappe 2 offen,
/// weil sie Lifecycle-Tasks (Warmup, BrainMirror) und Disposable-State haben.
/// </summary>
public static class ServiceCollectionConfigurator
{
    /// <summary>
    /// Registriert die heute migrierten Domain-IO-, Reports- und Devis/Sanierungs-
    /// Services im IServiceCollection. Erfordert AppSettings + Logger als Singleton
    /// vorab registriert.
    /// </summary>
    public static IServiceCollection AddSewerStudioCoreServices(this IServiceCollection services)
    {
        // Phase 5.2.A — ImportExport
        var importExport = ImportExportModule.Configure();
        services.AddSingleton(importExport.Projects);
        services.AddSingleton(importExport.PdfImport);
        services.AddSingleton(importExport.XtfImport);
        services.AddSingleton(importExport.WinCanImport);
        services.AddSingleton(importExport.IbakImport);
        services.AddSingleton(importExport.KinsImport);
        services.AddSingleton(importExport.ExcelExport);

        // Phase 5.2.B — Protocol/Reports
        var protocolReports = ProtocolReportsModule.Configure();
        services.AddSingleton(protocolReports.Protocols);
        services.AddSingleton(protocolReports.PhotoImport);
        services.AddSingleton(protocolReports.ProtocolPdfExporter);

        // Phase 5.2.C — Devis/Sanierung
        var devisSanierung = DevisSanierungModule.Configure();
        services.AddSingleton(devisSanierung.DevisGenerator);
        services.AddSingleton(devisSanierung.DevisExcelExporter);
        services.AddSingleton(devisSanierung.SubmissionsPositions);
        services.AddSingleton(devisSanierung.HistorischeSanierungen);
        services.AddSingleton(devisSanierung.MarktdatenImport);
        services.AddSingleton(devisSanierung.RehabRulesEngine);
        services.AddSingleton(devisSanierung.SanierungUserRules);

        // Cross-cutting UI services
        services.AddSingleton<IDialogService, DialogService>();
        services.AddSingleton<ErrorCodeGenerator>();

        return services;
    }

    /// <summary>
    /// Convenience: Settings + Logger registrieren.
    /// Wird von App.OnStartup vor AddSewerStudioCoreServices aufgerufen.
    /// </summary>
    public static IServiceCollection AddSewerStudioInfrastructure(
        this IServiceCollection services,
        AppSettings settings,
        DiagnosticsOptions diagnostics,
        ILogger logger,
        ILoggerFactory loggerFactory)
    {
        services.AddSingleton(settings);
        services.AddSingleton(diagnostics);
        services.AddSingleton(logger);
        services.AddSingleton(loggerFactory);
        return services;
    }

    /// <summary>
    /// Phase 5.1.B Etappe 2: Erweiterung um KI-Plattform, Code-Catalog,
    /// VSA-Evaluation, KnowledgeBase, ProtocolAi, Plausibility,
    /// MeasureRecommendation, Sidecar, Playwright.
    ///
    /// Erfordert AddSewerStudioInfrastructure (Settings + Logger) vorab.
    /// Background-Tasks (Warmup + BrainMirror) werden NICHT aus dem Container
    /// gestartet — separater Aufruf von <see cref="StartBackgroundServices"/>
    /// nach BuildServiceProvider() noetig.
    /// </summary>
    public static IServiceCollection AddSewerStudioAiServices(this IServiceCollection services)
    {
        // KI-Plattform-Konfiguration (1x laden, mehrfach projizieren)
        services.AddSingleton(sp => AiPlatformConfig.Load(sp.GetRequiredService<AppSettings>()));
        services.AddSingleton(sp => sp.GetRequiredService<AiPlatformConfig>().ToPipelineConfig());
        services.AddSingleton(sp => sp.GetRequiredService<AiPlatformConfig>().ToRuntimeConfig());

        // Sidecar (YOLO/DINO/SAM) — wird in App.OnStartup async gestartet
        services.AddSingleton(sp => AiPipelineModule.CreateSidecar(
            sp.GetRequiredService<PipelineConfig>(),
            sp.GetRequiredService<ILoggerFactory>()));

        // VSA-Code-Katalog (XML aus VsaCatalogResolver oder JSON-Fallback)
        services.AddSingleton<ICodeCatalogProvider>(sp =>
        {
            var settings = sp.GetRequiredService<AppSettings>();
            var codeCatalogPath = Path.Combine(AppContext.BaseDirectory, "Data", "vsa_codes.json");
            VsaCatalogResolver.EnsureEmbeddedCatalogFile(codeCatalogPath);
            var nodPath = VsaCatalogResolver.ResolveNodPath(settings);
            var xmlPath = nodPath ?? VsaCatalogResolver.ResolveSecPath(settings);
            var fallback = VsaCatalogResolver.ResolveTextPath(settings, xmlPath);
            return !string.IsNullOrWhiteSpace(xmlPath)
                ? new XmlCodeCatalogProvider(xmlPath, codeCatalogPath, fallback)
                : new JsonCodeCatalogProvider(codeCatalogPath);
        });

        // VSA-Evaluation (Channels + Manholes Tables aus AppContext.BaseDirectory/Data)
        services.AddSingleton<IVsaEvaluationService>(_ =>
        {
            var channels = Path.Combine(AppContext.BaseDirectory, "Data", "classification_channels.json");
            var manholes = Path.Combine(AppContext.BaseDirectory, "Data", "classification_manholes.json");
            return new VsaEvaluationService(channels, manholes);
        });

        // KnowledgeBase-Retrieval (record mit nullable KbHttp + Retrieval)
        // KbHttp ist IDisposable — DI-Container disposed das Singleton beim Shutdown.
        services.AddSingleton(sp => KnowledgeBaseModule.ConfigureRetrieval(
            sp.GetRequiredService<AiPlatformConfig>(),
            sp.GetRequiredService<AppSettings>(),
            sp.GetRequiredService<ILogger>()));

        // Plausibility (CodeCatalog → AllowedCodes → HashSet)
        services.AddSingleton<IAiSuggestionPlausibilityService>(sp =>
        {
            var catalog = sp.GetRequiredService<ICodeCatalogProvider>();
            var allowed = new HashSet<string>(catalog.AllowedCodes(), StringComparer.OrdinalIgnoreCase);
            return new RuleBasedAiSuggestionPlausibilityService(allowed);
        });

        // ProtocolAi: Ollama wenn aktiv, sonst Noop
        services.AddSingleton<IProtocolAiService>(sp =>
        {
            var cfg = sp.GetRequiredService<AiRuntimeConfig>();
            if (!cfg.Enabled)
                return new NoopProtocolAiService();
            var kb = sp.GetRequiredService<KnowledgeBaseModule.Services>();
            var plausibility = sp.GetRequiredService<IAiSuggestionPlausibilityService>();
            return new OllamaProtocolAiService(cfg, kb.Retrieval as RetrievalService, plausibility);
        });

        // MeasureRecommendation (KnowledgeRoot-Pfade)
        services.AddSingleton<IMeasureRecommendationService>(_ =>
            new Infrastructure.Ai.MeasureRecommendationService(
                KnowledgeRoot.GetMeasuresLearningPath(),
                KnowledgeRoot.GetMeasuresModelPath()));

        // Playwright-Installer (Logger-Factory-Anbindung)
        services.AddSingleton<IPlaywrightInstallService>(sp =>
            new PlaywrightInstallService(
                sp.GetRequiredService<ILoggerFactory>().CreateLogger<PlaywrightInstallService>()));

        // Slice 1 (Operateur-Annotation): zwei Adapter mit einfachem
        // Lifetime registrieren. Der Service-Vollzusammenbau (mit
        // KnowledgeBaseManager + VisionPipelineClient) passiert im
        // App-Bootstrapping, weil diese beiden heute nicht aus dem
        // Container kommen — der Service-Accessor wird dort befuellt.
        services.AddSingleton<ITrainingSamplesWriter,
            Infrastructure.Ai.Annotation.TrainingSamplesWriterAdapter>();
        services.AddSingleton<IYoloDatasetWriter>(_ =>
            new Infrastructure.Ai.Training.YoloDatasetExportService(
                ResolveYoloDatasetRoot()));

        return services;
    }

    /// <summary>
    /// Slice 1: liefert den Default-Pfad fuer den YOLO-seg-Datensatz.
    /// Reihenfolge: Env-Var <c>SEWERSTUDIO_YOLO_DATASET</c> &gt; <c>D:\yolo_sewer_v1</c>
    /// (Workstation-Konvention laut CLAUDE.md). Existenz wird hier nicht
    /// erzwungen — AppendSampleAsync legt die Unterordner an.
    /// </summary>
    private static string ResolveYoloDatasetRoot()
    {
        var envRoot = System.Environment.GetEnvironmentVariable("SEWERSTUDIO_YOLO_DATASET");
        return string.IsNullOrWhiteSpace(envRoot)
            ? @"D:\yolo_sewer_v1"
            : envRoot;
    }

    /// <summary>
    /// Startet die Background-Tasks (Modell-Warmup + BrainMirror-Sync) nach
    /// dem BuildServiceProvider()-Aufruf. Bewusst NICHT in AddSewerStudioAiServices,
    /// damit Service-Resolution selbst keine Side-Effects ausloest.
    /// </summary>
    public static void StartBackgroundServices(IServiceProvider provider)
    {
        var cfg = provider.GetRequiredService<AiRuntimeConfig>();
        var sidecar = provider.GetRequiredService<PythonSidecarService>();
        var logger = provider.GetRequiredService<ILogger>();
        var settings = provider.GetRequiredService<AppSettings>();

        if (cfg.Enabled)
            AiPipelineModule.RunWarmupInBackground(cfg, sidecar, logger);

        KnowledgeBaseModule.StartBrainMirror(settings, logger);
    }

    /// <summary>
    /// Slice 1 (Operateur-Annotation): voll zusammengesetzten Service in
    /// <see cref="OperateurAnnotationServiceAccessor"/> ablegen. Wird vom
    /// PlayerWindow.OperateurAnnotation lazy aus dem Accessor gezogen
    /// (dort hat das Submodus-Bedienpanel Zugriff darauf).
    ///
    /// Bewusst nach <see cref="StartBackgroundServices"/> aufgerufen — Sidecar
    /// muss nicht laufen, damit der Service registriert ist; Sidecar-Calls
    /// failen erst bei tatsaechlicher Nutzung (gewuenscht: KI-Pipeline kann
    /// lazy starten).
    ///
    /// Bei Init-Fehlern bleibt der Accessor null und der Submodus wirft eine
    /// klare Meldung beim Enter — die Haupt-App laeuft weiter.
    /// </summary>
    public static void WireOperateurAnnotationService(IServiceProvider provider)
    {
        var logger = provider.GetRequiredService<ILogger>();
        try
        {
            var writer = provider.GetRequiredService<ITrainingSamplesWriter>();
            var yolo = provider.GetRequiredService<IYoloDatasetWriter>();
            var pipelineCfg = provider.GetRequiredService<PipelineConfig>();
            var aiPlatform = provider.GetRequiredService<AiPlatformConfig>();

            // Sidecar-HTTP fuer SAM-Calls: lebt fuer die Lebenszeit der App.
            // Wird beim Shutdown ueber den DI-Container nicht mehr disposed
            // (wir bauen ihn ausserhalb), aber WPF-Process-Exit raeumt eh alles ab.
            var sidecarHttp = new System.Net.Http.HttpClient();
            var sidecarClient = new Infrastructure.Ai.Pipeline.VisionPipelineClient(
                pipelineCfg.SidecarUrl, sidecarHttp);

            // KnowledgeBaseManager — gleiches Pattern wie der Maintenance-Pruner
            // in App.xaml.cs (db + http + embedder + manager).
            var ollamaCfg = aiPlatform.ToOllamaConfig();
            var kbHttp = new System.Net.Http.HttpClient { Timeout = ollamaCfg.RequestTimeout };
            var kbDb = new KnowledgeBaseContext();
            var kbEmbedder = new EmbeddingService(kbHttp, ollamaCfg);
            var kbManager = new KnowledgeBaseManager(kbDb, kbEmbedder);
            var indexer = new Infrastructure.Ai.Annotation.KnowledgeBaseIndexerAdapter(kbManager);

            var service = new Infrastructure.Ai.Annotation.OperateurAnnotationService(
                samDelegate: (req, ct) => sidecarClient.SegmentSamAsync(req, ct),
                writer: writer,
                indexer: indexer,
                yolo: yolo);

            OperateurAnnotationServiceAccessor.Current = service;
            logger.LogInformation("[OperateurAnnotation] Service registriert.");
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex,
                "[OperateurAnnotation] Service-Setup fehlgeschlagen — Submodus deaktiviert");
            OperateurAnnotationServiceAccessor.Current = null;
        }
    }
}
