using System;
using System.IO;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using AuswertungPro.Next.Application.Ai;                 // IVisionPipelineClient, PipelineConfig, ISidecarTelemetryWriter
using AuswertungPro.Next.Application.Ai.Startup;
using AuswertungPro.Next.Application.Ai.KnowledgeBase;   // IRetrievalService
using AuswertungPro.Next.Application.Ai.Teacher;         // ITeacherAnnotationStore, IVsaYoloClassMapStore
using AuswertungPro.Next.Application.Ai.Training;        // ITrainingSampleStore, IKnowledgeBaseIndexer
using AuswertungPro.Next.Application.Ai.Workbench;       // IAnnotationWorkbenchService
using AuswertungPro.Next.Infrastructure.Ai.Pipeline;     // VisionPipelineClient, SidecarTelemetryWriter
using AuswertungPro.Next.Infrastructure.Ai.Teacher;      // TeacherAnnotationStore, VsaYoloClassMap
using AuswertungPro.Next.Infrastructure.Ai.Training;     // TrainingSamplesStore, DelegatingKnowledgeBaseIndexer
using AuswertungPro.Next.UI.Ai.Training;                 // TrainingKnowledgeBaseIndexWorkflow

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
        WorkbenchQueueService QueueService,
        Func<IProgress<string>, CancellationToken, Task<(bool Ready, string StatusText)>>? EnsureAiReady);

    internal static Dependencies CreateDependencies(ServiceProvider? services)
    {
        var pipeline = CreatePipelineClient(services);
        var workbench = Create(services, pipeline);
        var queue = CreateQueueService(services);
        if (services is null)
            return new Dependencies(workbench, queue, EnsureAiReady: null);

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
            queue,
            async (progress, ct) =>
            {
                var result = await readiness.EnsureReadyAsync(progress, ct);
                return (result.Ready, result.StatusText);
            });
    }

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

        // 4) KB-Indexer: Adapter um den zentralen Index-Workflow (inkl. Eval-Schutz).
        //    Deindex ist ein No-op — der Pruefplatz ruft nur IndexAsync.
        HttpClient? kbHttp = null;
        IKnowledgeBaseIndexer kbIndexer = new DelegatingKnowledgeBaseIndexer(
            (samples, ct) => TrainingKnowledgeBaseIndexWorkflow.RunWithDefaultsAsync(
                samples, ct, () => kbHttp, v => kbHttp = v, services?.Settings, _ => { }),
            _ => { });

        // 5) Teacher-Store.
        ITeacherAnnotationStore teacherStore = services?.TeacherAnnotations ?? TeacherAnnotationStore.Current;

        // 6) VSA->YOLO-Klassenkarte (darf per GetOrAddClassId wachsen).
        IVsaYoloClassMapStore teacherClassMap = services?.VsaYoloClasses ?? VsaYoloClassMap.Current;

        return new AnnotationWorkbenchService(
            sam,
            pipeline,
            retrieval,
            sampleStore,
            kbIndexer,
            teacherStore,
            teacherClassMap,
            File.ReadAllBytes,
            () => TrainingSamplesStore.EffectiveEvalSetRoot);
    }

    /// <summary>Baut die Quellen (Fotos + Review-Warteschlange) fuer den Pruefplatz.</summary>
    internal static WorkbenchQueueService CreateQueueService(ServiceProvider? services)
        => new(services?.TrainingSamples ?? TrainingSamplesStore.Current);

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
