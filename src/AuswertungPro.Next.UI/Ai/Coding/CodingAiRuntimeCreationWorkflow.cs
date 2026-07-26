using System;
using AuswertungPro.Next.Application.Ai;
using AuswertungPro.Next.Application.Protocol;

namespace AuswertungPro.Next.UI.Ai.Coding;

public sealed record CodingAiRuntimeCreationRequest(
    ICodeCatalogProvider? CodeCatalog,
    PipelineConfig? PipelineConfig);

public sealed record CodingAiRuntimeCreationActions(
    Func<AiPlatformSettings> LoadPlatformSettings,
    Func<AiPlatformSettings, ICodeCatalogProvider?, PipelineConfig?, CodingAiRuntime> CreateRuntime);

public static class CodingAiRuntimeCreationWorkflow
{
    public static CodingAiRuntime Create(
        ICodeCatalogProvider? codeCatalog,
        PipelineConfig? pipelineConfig,
        ISidecarTelemetryWriter? sidecarTelemetry = null)
        => Create(
            new CodingAiRuntimeCreationRequest(codeCatalog, pipelineConfig),
            new CodingAiRuntimeCreationActions(
                () => PlayerAiSettingsLoader.LoadPlatformSettings(),
                (platformSettings, codeCatalog, loadedPipelineConfig) =>
                    CodingAiRuntimeFactory.Create(
                        platformSettings,
                        codeCatalog,
                        loadedPipelineConfig,
                        sidecarTelemetry)));

    public static CodingAiRuntime Create(
        CodingAiRuntimeCreationRequest request,
        CodingAiRuntimeCreationActions actions)
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentNullException.ThrowIfNull(actions);
        ArgumentNullException.ThrowIfNull(actions.LoadPlatformSettings);
        ArgumentNullException.ThrowIfNull(actions.CreateRuntime);

        var platformSettings = actions.LoadPlatformSettings();
        return actions.CreateRuntime(
            platformSettings,
            request.CodeCatalog,
            request.PipelineConfig);
    }
}
