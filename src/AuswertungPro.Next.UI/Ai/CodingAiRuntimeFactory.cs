using System;
using AuswertungPro.Next.Application.Ai;
using AuswertungPro.Next.Application.Protocol;
using AuswertungPro.Next.Infrastructure.Ai;
using AuswertungPro.Next.Infrastructure.Ai.Pipeline;
using AuswertungPro.Next.Infrastructure.Ai.QualityGate;
using AuswertungPro.Next.Infrastructure.Ai.Training;

namespace AuswertungPro.Next.UI.Ai;

public sealed record CodingAiRuntime(
    AiRuntimeSettings RuntimeSettings,
    PipelineConfig PipelineConfig,
    string ModelName,
    LiveDetectionService? LiveDetection,
    EnhancedVisionAnalysisService? EnhancedVision,
    QualityGateService? QualityGate,
    GuidedVerificationService? ProtocolVerifier,
    IVisionPipelineClient? VisionClient,
    SingleFrameMultiModelService? MultiModel,
    MarkBoxSegmentationService? BoxSegmentation,
    string? MultiModelError)
{
    public bool QwenAvailable => LiveDetection is not null || EnhancedVision is not null;

    public bool MultiModelAvailable
        => VisionClient is not null && MultiModel is not null && BoxSegmentation is not null;
}

public static class CodingAiRuntimeFactory
{
    public static CodingAiRuntime Create(
        AiPlatformSettings platformSettings,
        ICodeCatalogProvider? codeCatalog,
        PipelineConfig? overridePipelineConfig = null,
        ISidecarTelemetryWriter? sidecarTelemetry = null)
    {
        ArgumentNullException.ThrowIfNull(platformSettings);

        var runtimeSettings = platformSettings.ToRuntimeSettings();
        var pipelineConfig = overridePipelineConfig ?? platformSettings.ToPipelineConfig();
        if (!runtimeSettings.Enabled)
            return Disabled(runtimeSettings, pipelineConfig);

        var ollamaClient = new OllamaClient(
            runtimeSettings.OllamaBaseUri,
            ownedTimeout: runtimeSettings.OllamaRequestTimeout,
            keepAlive: runtimeSettings.OllamaKeepAlive,
            numCtx: runtimeSettings.OllamaNumCtx);

        var liveDetection = new LiveDetectionService(ollamaClient, runtimeSettings.VisionModel);
        var enhancedVision = new EnhancedVisionAnalysisService(ollamaClient, runtimeSettings.VisionModel, codeCatalog);
        var protocolVerifier = new GuidedVerificationService(ollamaClient, runtimeSettings.VisionModel, codeCatalog);
        // Produktiv nur validierte Default-Gewichte. Gelernte Gewichte bleiben bis
        // zu einem getrennten Eval im Schattenbetrieb.
        var qualityGate = LearnedWeightsGateFactory.Create();

        try
        {
            var visionClient = new VisionPipelineClient(
                pipelineConfig.SidecarUrl,
                null,
                pipelineConfig.SidecarToken,
                sidecarTelemetry ?? SidecarTelemetryWriter.Current);
            var multiModel = new SingleFrameMultiModelService(visionClient, pipelineConfig);
            var boxSegmentation = new MarkBoxSegmentationService(visionClient.SegmentSamAsync);

            return new CodingAiRuntime(
                runtimeSettings,
                pipelineConfig,
                runtimeSettings.VisionModel,
                liveDetection,
                enhancedVision,
                qualityGate,
                protocolVerifier,
                visionClient,
                multiModel,
                boxSegmentation,
                MultiModelError: null);
        }
        catch (Exception ex)
        {
            return new CodingAiRuntime(
                runtimeSettings,
                pipelineConfig,
                runtimeSettings.VisionModel,
                liveDetection,
                enhancedVision,
                qualityGate,
                protocolVerifier,
                VisionClient: null,
                MultiModel: null,
                BoxSegmentation: null,
                MultiModelError: ex.Message);
        }
    }

    public static IPipelineHealthMonitor CreateHealthMonitor(
        IVisionPipelineClient visionClient,
        Func<bool> aiEnabled,
        Func<bool> qwenAvailable)
        => new PipelineHealthMonitor(visionClient, aiEnabled, qwenAvailable);

    public static SingleFrameMultiModelService CreateMultiModelService(
        IVisionPipelineClient visionClient,
        PipelineConfig? pipelineConfig)
        => pipelineConfig is null
            ? new SingleFrameMultiModelService(visionClient)
            : new SingleFrameMultiModelService(visionClient, pipelineConfig);

    private static CodingAiRuntime Disabled(AiRuntimeSettings runtimeSettings, PipelineConfig pipelineConfig)
        => new(
            runtimeSettings,
            pipelineConfig,
            runtimeSettings.VisionModel,
            LiveDetection: null,
            EnhancedVision: null,
            QualityGate: null,
            ProtocolVerifier: null,
            VisionClient: null,
            MultiModel: null,
            BoxSegmentation: null,
            MultiModelError: null);
}
