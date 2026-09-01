using System;
using System.IO;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using AuswertungPro.Next.Application.Ai;                 // IVisionPipelineClient, PipelineConfig, ISidecarTelemetryWriter
using AuswertungPro.Next.Application.Ai.Startup;
using AuswertungPro.Next.Application.Ai.KnowledgeBase;   // IRetrievalService
using AuswertungPro.Next.Application.Ai.Teacher;         // ITeacherAnnotationStore, IVsaYoloClassMapStore
using AuswertungPro.Next.Application.Ai.Training;        // ITrainingSampleStore, IKnowledgeBaseIndexer
using AuswertungPro.Next.Application.Ai.Training.Preview;
using AuswertungPro.Next.Application.Ai.Workbench;       // IAnnotationWorkbenchService
using AuswertungPro.Next.Application.Common;
using AuswertungPro.Next.Application.Media;              // IVideoFrameExtractor, IVideoClipExtractor
using AuswertungPro.Next.Application.UseCases.BendSuggestions;
using AuswertungPro.Next.Application.UseCases.PdfTrainingReview;
using AuswertungPro.Next.Application.UseCases.GoldQualityReview;
using AuswertungPro.Next.Infrastructure.Ai;              // OllamaClient, BcaFineCodeClassifier
using AuswertungPro.Next.Infrastructure.Ai.BendSuggestions;
using AuswertungPro.Next.Infrastructure.Ai.KnowledgeBase;
using AuswertungPro.Next.Infrastructure.Ai.Ollama;       // ToOllamaConfig
using AuswertungPro.Next.Infrastructure.Ai.Pipeline;     // VisionPipelineClient, SidecarTelemetryWriter
using AuswertungPro.Next.Infrastructure.Ai.Shared;       // FfmpegLocator
using AuswertungPro.Next.Infrastructure.Ai.Teacher;      // TeacherAnnotationStore, VsaYoloClassMap
using AuswertungPro.Next.Infrastructure.Ai.Training;     // TrainingSamplesStore, DelegatingKnowledgeBaseIndexer
using AuswertungPro.Next.Infrastructure.Ai.Training.Preview;
using AuswertungPro.Next.Infrastructure.Ai.Training.PdfReview;
using AuswertungPro.Next.Infrastructure.Ai.Training.ExportPlans;
using AuswertungPro.Next.Infrastructure.Ai.Training.GoldQualityReview;
using AuswertungPro.Next.Infrastructure.Ai.Training.Inventory;
using AuswertungPro.Next.Infrastructure.Media;           // VideoFrameSequenceExtractor
using AuswertungPro.Next.UI.Ai.Training;                 // TrainingKnowledgeBaseIndexWorkflow
using AuswertungPro.Next.UI.ViewModels.BendSuggestions;

namespace AuswertungPro.Next.UI.Services;

/// <summary>
/// Zentraler Aufbau des Pruefplatz-Workbench-Service aus dem <see cref="ServiceProvider"/>.
/// Normalweg zieht die Dienste aus dem Provider; der parameterlose WPF-/Designer-Rueckfall
/// nutzt die stabilen statischen .Current-Fassaden. Analog zu
/// <see cref="TrainingCenterWindowDependencyFactory"/>.
/// </summary>
internal static class TrainingStudioWindowDependencyFactory
{
    internal sealed record Dependencies(
        IAnnotationWorkbenchService Workbench,
        ITrainingPreviewDetectionService PreviewDetection,
        WorkbenchQueueService QueueService,
        ITrainingPdfReviewImportService PdfReviewImport,
        ITrainingPdfReviewBatchImportUseCase PdfReviewBatchImport,
        IPersonalGoldAlbumService GoldAlbum,
        IPersonalGoldInboxService GoldInbox,
        IGoldQualityReviewQueueUseCase GoldQualityReview,
        IFolderOpenService? FolderOpen,
        Func<CancellationToken, Task<IReadOnlyList<PersonalGoldMainCodeStatus>>> LoadGoldProgress,
        Func<IProgress<string>, CancellationToken, Task<(bool Ready, string StatusText)>>? EnsureAiReady,
        BendSuggestionListViewModel BendSuggestions);

    internal static Dependencies CreateDependencies(
        ServiceProvider? services,
        Action<Action>? marshalToUi = null)
    {
        var pipeline = CreatePipelineClient(services);
        var workbench = Create(services, pipeline);
        var previewDetection = new TrainingPreviewDetectionService(pipeline);
        var queue = CreateQueueService(services);
        var rawPdfReviewImport = services?.TrainingPdfReviewReader
            ?? new TrainingPdfReviewImportService(
                services?.KnowledgeRoot ?? KnowledgeBasePaths.GetRoot(),
                new TrainingPdfJpegColorNormalizer());
        var loadPdfProtection = CreatePdfProtectionLoader(services);
        var pdfReviewImport = services?.TrainingPdfReviews
            ?? new TrainingPdfReviewProtectedImportService(
                rawPdfReviewImport,
                loadPdfProtection);
        var pdfReviewBatchImport = new TrainingPdfReviewBatchImportUseCase(
            new TrainingPdfFolderDiscoveryService(),
            rawPdfReviewImport,
            loadPdfProtection);
        var goldAlbum = services?.PersonalGoldAlbum
            ?? new PersonalGoldAlbumService(TrainingSamplesStore.Current);
        var goldInbox = services?.PersonalGoldInbox
            ?? new PersonalGoldInboxFileService(KnowledgeBasePaths.GetRoot());
        var goldQualityReview = CreateGoldQualityReview(services);
        var goldProgress = CreateGoldProgressLoader(services);
        var bendSuggestions = CreateBendSuggestionListViewModel(services, pipeline, marshalToUi);
        if (services is null)
        {
            return new Dependencies(
                workbench,
                previewDetection,
                queue,
                pdfReviewImport,
                pdfReviewBatchImport,
                goldAlbum,
                goldInbox,
                goldQualityReview,
                FolderOpen: null,
                goldProgress,
                EnsureAiReady: null,
                BendSuggestions: bendSuggestions);
        }

        var readiness = new TrainingStudioAiReadinessWorkflow(
            pipeline.CheckHealthDetailedAsync,
            async (progress, ct) =>
            {
                var result = await AiStartupService.StartAsync(
                    services.Settings,
                    services.AiStartedProcesses,
                    services.AiSettings,
                    services.SidecarScripts,
                    services.SidecarTokens,
                    progress,
                    ct);
                services.Settings.SaveImmediate();
                return result;
            });

        return new Dependencies(
            workbench,
            previewDetection,
            queue,
            pdfReviewImport,
            pdfReviewBatchImport,
            goldAlbum,
            goldInbox,
            goldQualityReview,
            services.FolderOpen,
            goldProgress,
            async (progress, ct) =>
            {
                var result = await readiness.EnsureReadyAsync(progress, ct);
                return (result.Ready, result.StatusText);
            },
            BendSuggestions: bendSuggestions);
    }

    /// <summary>
    /// Baut das Bogen-Vorschlags-ViewModel (Auftrag Paket 3/4). Mit Provider kommen die
    /// registrierten Singletons zum Zug — das Exposure-Gedaechtnis muss denselben
    /// Programmlauf ueberdauern. Der Designer-Rueckfall (services = null) komponiert lokal
    /// aus dem Fenster-eigenen Pipeline-Client, wie die uebrigen Dienste hier.
    /// </summary>
    internal static BendSuggestionListViewModel CreateBendSuggestionListViewModel(
        ServiceProvider? services,
        IVisionPipelineClient pipeline,
        Action<Action>? marshalToUi)
    {
        ArgumentNullException.ThrowIfNull(pipeline);
        Func<string> resolveFfmpeg = () => services?.FfmpegExecutables.ResolveFfmpeg()
            ?? FfmpegLocator.ResolveFfmpeg();

        IBendSuggestionScanService scan = services?.BendSuggestionScan
            ?? new BendSuggestionScanService(
                new BendSuggestionCalibrationFileStore(),
                new VideoFrameSequenceExtractor(),
                pipeline.DetectBccTestYoloAsync,
                resolveFfmpeg,
                () => Path.Combine(Path.GetTempPath(), "auswertungpro-bogen-scan"));
        ICodingSuggestionExposure exposure = services?.CodingSuggestionExposure
            ?? new CodingSuggestionExposure();
        IVideoFrameExtractor frames = services?.VideoFrameExtraction
            ?? new VideoFrameExtractionService(ProcessOutputReader.Current);
        IVideoClipExtractor clips = services?.VideoClipExtraction
            ?? new VideoClipExtractionService(ProcessOutputReader.Current);

        return new BendSuggestionListViewModel(
            scan,
            exposure,
            frames,
            clips,
            resolveFfmpeg,
            marshalToUi,
            log: text => services?.Logger.LogInformation("{Meldung}", text));
    }

    private static IGoldQualityReviewQueueUseCase CreateGoldQualityReview(
        ServiceProvider? services)
    {
        var knowledgeRoot = services?.KnowledgeRoot ?? KnowledgeBasePaths.GetRoot();
        var inventory = services?.TrainingDataInventory ?? new TrainingDataInventoryService();
        var registry = services?.TrainingExportRegistry
                       ?? new TrainingExportRegistryFileStore(
                           Path.Combine(knowledgeRoot, "training", "export_registry_v1.json"),
                           knowledgeRoot);
        var snapshotProvider = new GoldQualityReviewSnapshotProvider(
            inventory,
            knowledgeRoot,
            () => services?.Settings.EvalSetRoot ?? TrainingSamplesStore.EffectiveEvalSetRoot);
        var sessionStore = new GoldQualityReviewSessionFileStore(knowledgeRoot);

        return new GoldQualityReviewQueueUseCase(
            snapshotProvider,
            registry,
            sessionStore,
            TrainingImageFileProbe.CanDecode,
            TrainingImageFileProbe.ReadDimensions,
            EvalContaminationGuard.ComputeFileHash);
    }

    private static Func<TrainingPdfReviewProtectionSnapshot> CreatePdfProtectionLoader(
        ServiceProvider? services)
        => () =>
        {
            var root = services?.Settings.EvalSetRoot
                       ?? TrainingSamplesStore.EffectiveEvalSetRoot;
            return LoadPdfProtectionSnapshot(root);
        };

    internal static TrainingPdfReviewProtectionSnapshot LoadPdfProtectionSnapshot(
        string? evalSetRoot)
        => EvalContaminationSetProvider.LoadPdfProtectionSnapshot(evalSetRoot);

    internal static IAnnotationWorkbenchService Create(ServiceProvider? services)
        => Create(services, CreatePipelineClient(services));

    private static IAnnotationWorkbenchService Create(
        ServiceProvider? services,
        IVisionPipelineClient pipeline)
    {
        // 1) SAM-Segmentierung (bestehender Review-Weg).
        ITrainingReviewSamSegmentationService sam =
            services?.CreateTrainingReviewSam() ?? CreateDefaultReviewSam();

        // 2) KB-Retrieval (nullable; Fallback null).
        IRetrievalService? retrieval = services?.Retrieval;

        // 3) Sample-Store.
        ITrainingSampleStore sampleStore = services?.TrainingSamples ?? TrainingSamplesStore.Current;
        ITrainingFrameStore frameStore = services?.TrainingFrames ?? FrameStore.Current;

        // 4) KB-Indexer: Adapter um den zentralen Index-Workflow (inkl. Eval-Schutz) mit
        //    echtem Deindex (Codekorrektur-Ersetzen entfernt den alten KB-Eintrag).
        IKnowledgeBaseIndexer kbIndexer = CreateKbIndexer(services);

        // 5) Teacher-Store.
        ITeacherAnnotationStore teacherStore = services?.TeacherAnnotations ?? TeacherAnnotationStore.Current;

        // 6) VSA->YOLO-Klassenkarte (darf per GetOrAddClassId wachsen).
        IVsaYoloClassMapStore teacherClassMap = services?.VsaYoloClasses ?? VsaYoloClassMap.Current;

        // 7) Feiner Anschluss-Code (BCA-Bauart) via eigenem Qwen-Client. Nur bei aktiven KI-Settings;
        //    sonst null -> der Pruefplatz-Knopf bleibt wirkungslos. Der Client gehoert dem Classifier
        //    (ownsClient) und wird ueber die Dispose-Kette des Workbench-Service freigegeben.
        IBcaFineCodeClassifier? bcaClassifier = null;
        var aiCfg = services?.AiSettings.Load();
        if (aiCfg is { Enabled: true } && !string.IsNullOrWhiteSpace(aiCfg.VisionModel))
        {
            var ollama = new OllamaClient(
                aiCfg.OllamaBaseUri,
                http: null,
                ownedTimeout: TimeSpan.FromSeconds(90),
                keepAlive: aiCfg.OllamaKeepAlive,
                numCtx: aiCfg.OllamaNumCtx);
            bcaClassifier = new BcaFineCodeClassifier(ollama, aiCfg.VisionModel, ownsClient: true);
        }

        return new AnnotationWorkbenchService(
            sam,
            pipeline,
            retrieval,
            sampleStore,
            frameStore,
            () => Path.Combine(
                services?.KnowledgeRoot ?? KnowledgeBasePaths.GetRoot(),
                "gold_frames"),
            kbIndexer,
            teacherStore,
            teacherClassMap,
            File.ReadAllBytes,
            () => TrainingSamplesStore.EffectiveEvalSetRoot,
            bcaClassifier: bcaClassifier,
            protocolAi: services?.ProtocolAi,
            resolveAllowedCodes: services is null
                ? null
                : () => services.CodeCatalog.AllowedCodes());
    }

    /// <summary>
    /// KB-Indexer fuer den Pruefplatz: Index ueber den zentralen Workflow (Eval-Schutz,
    /// IsIndexWorthy), Deindex ueber denselben KB-Weg (loescht Samples + Embeddings
    /// transaktional via <see cref="TrainingKnowledgeBaseSampleDeindexer"/>). Beide Wege
    /// teilen einen gecachten HttpClient. Deindex-Fehler fliessen zum Aufrufer und werden
    /// dort als sichtbare Warnung gemeldet (nie still).
    /// </summary>
    internal static IKnowledgeBaseIndexer CreateKbIndexer(ServiceProvider? services)
    {
        HttpClient? kbHttp = null;
        return new DelegatingKnowledgeBaseIndexer(
            (samples, ct) => TrainingKnowledgeBaseIndexWorkflow.RunWithDefaultsAsync(
                samples, ct, () => kbHttp, v => kbHttp = v, services?.Settings, _ => { }),
            sampleId =>
            {
                var ollamaConfig = new AppSettingsAiSettingsProvider().Load().ToOllamaConfig();
                kbHttp ??= new HttpClient { Timeout = ollamaConfig.RequestTimeout };
                TrainingKnowledgeBaseSampleDeindexer.DeindexWithDefaultInfrastructure(
                    kbHttp, ollamaConfig, sampleId);
            },
            dispose: () =>
            {
                kbHttp?.Dispose();
                kbHttp = null;
            });
    }

    /// <summary>Baut die Quellen (Fotos + Review-Warteschlange) fuer den Pruefplatz.</summary>
    internal static WorkbenchQueueService CreateQueueService(ServiceProvider? services)
        => new(services?.TrainingSamples ?? TrainingSamplesStore.Current);

    private static Func<CancellationToken, Task<IReadOnlyList<PersonalGoldMainCodeStatus>>> CreateGoldProgressLoader(
        ServiceProvider? services)
    {
        var sampleStore = services?.TrainingSamples ?? TrainingSamplesStore.Current;
        return async cancellationToken =>
        {
            var samples = await sampleStore.LoadAsync().ConfigureAwait(false);
            cancellationToken.ThrowIfCancellationRequested();
            return PersonalGoldProgressCalculator.Calculate(
                samples,
                Environment.UserName,
                PersonalGoldMainCodeCatalog.RequiredCodes);
        };
    }

    private static IVisionPipelineClient CreatePipelineClient(ServiceProvider? services)
    {
        PipelineConfig cfg = services?.PipelineCfg
            ?? new AppSettingsAiSettingsProvider().Load().ToPipelineConfig();
        ISidecarTelemetryWriter telemetry = services?.SidecarTelemetry ?? SidecarTelemetryWriter.Current;
        var timeout = TimeSpan.FromSeconds(Math.Max(30, cfg.SidecarTimeoutSec));
        // Eigener HttpClient (ownedTimeout) statt manuellem new HttpClient — nur so besitzt der
        // VisionPipelineClient ihn (_ownsHttp) und gibt ihn beim Dispose des Workbench frei.
        return new VisionPipelineClient(
            cfg.SidecarUrl,
            httpClient: null,
            cfg.SidecarToken,
            telemetry,
            ownedTimeout: timeout);
    }

    private static TrainingReviewSamSegmentationService CreateDefaultReviewSam()
    {
        var cfg = new AppSettingsAiSettingsProvider().Load().ToPipelineConfig();
        return new TrainingReviewSamSegmentationService(new VisionPipelineTrainingReviewSamClient(cfg));
    }
}
