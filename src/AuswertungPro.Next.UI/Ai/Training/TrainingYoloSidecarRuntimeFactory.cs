using AuswertungPro.Next.Application.Ai;
using AuswertungPro.Next.Infrastructure.Ai.Pipeline;
using AuswertungPro.Next.UI.Services;
using System;

namespace AuswertungPro.Next.UI.Ai.Training;

public sealed record TrainingYoloSidecarRuntime(
    PipelineConfig PipelineConfig,
    IVisionPipelineClient Client);

public static class TrainingYoloSidecarRuntimeFactory
{
    public static TrainingYoloSidecarRuntime CreateWithDefaults(
        ISidecarTelemetryWriter? sidecarTelemetry = null)
        => Create(
            loadPipelineConfig: () => new AppSettingsAiSettingsProvider().Load().ToPipelineConfig(),
            createClient: config => new VisionPipelineClient(
                config.SidecarUrl,
                null,
                config.SidecarToken,
                sidecarTelemetry ?? SidecarTelemetryWriter.Current));

    public static TrainingYoloSidecarRuntime Create(
        Func<PipelineConfig> loadPipelineConfig,
        Func<PipelineConfig, IVisionPipelineClient> createClient)
    {
        ArgumentNullException.ThrowIfNull(loadPipelineConfig);
        ArgumentNullException.ThrowIfNull(createClient);

        var config = loadPipelineConfig();
        return new TrainingYoloSidecarRuntime(config, createClient(config));
    }
}
