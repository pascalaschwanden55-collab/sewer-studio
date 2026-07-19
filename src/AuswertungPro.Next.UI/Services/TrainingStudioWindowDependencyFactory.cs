using System;
using System.IO;
using System.Net.Http;
using AuswertungPro.Next.Application.Ai;                 // IVisionPipelineClient, PipelineConfig, ISidecarTelemetryWriter
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
    internal static IAnnotationWorkbenchService Create(ServiceProvider? services)
    {
        // 1) SAM-Segmentierung (bestehender Review-Weg).
        ITrainingReviewSamSegmentationService sam =
            services?.CreateTrainingReviewSam() ?? CreateDefaultReviewSam();

        // 2) Pipeline-Client fuer den cls-Klassifikator (keine ServiceProvider-Instanz -> selbst bauen).
        PipelineConfig cfg = services?.PipelineCfg
            ?? new AppSettingsAiSettingsProvider().Load().ToPipelineConfig();
        ISidecarTelemetryWriter telemetry = services?.SidecarTelemetry ?? SidecarTelemetryWriter.Current;
        var timeout = TimeSpan.FromSeconds(Math.Max(30, cfg.SidecarTimeoutSec));
        IVisionPipelineClient pipeline = new VisionPipelineClient(
            cfg.SidecarUrl, new HttpClient { Timeout = timeout }, cfg.SidecarToken, telemetry);

        // 3) KB-Retrieval (nullable; Fallback null).
        IRetrievalService? retrieval = services?.Retrieval;

        // 4) Sample-Store.
        ITrainingSampleStore sampleStore = services?.TrainingSamples ?? TrainingSamplesStore.Current;

        // 5) KB-Indexer: Adapter um den zentralen Index-Workflow (inkl. Eval-Schutz).
        //    Deindex ist ein No-op — der Pruefplatz ruft nur IndexAsync.
        HttpClient? kbHttp = null;
        IKnowledgeBaseIndexer kbIndexer = new DelegatingKnowledgeBaseIndexer(
            (samples, ct) => TrainingKnowledgeBaseIndexWorkflow.RunWithDefaultsAsync(
                samples, ct, () => kbHttp, v => kbHttp = v, services?.Settings, _ => { }),
            _ => { });

        // 6) Teacher-Store.
        ITeacherAnnotationStore teacherStore = services?.TeacherAnnotations ?? TeacherAnnotationStore.Current;

        // 7) VSA->YOLO-Klassenkarte (darf per GetOrAddClassId wachsen).
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

    private static TrainingReviewSamSegmentationService CreateDefaultReviewSam()
    {
        var cfg = new AppSettingsAiSettingsProvider().Load().ToPipelineConfig();
        return new TrainingReviewSamSegmentationService(new VisionPipelineTrainingReviewSamClient(cfg));
    }
}
