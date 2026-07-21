using AuswertungPro.Next.Application.Ai;
using AuswertungPro.Next.Application.Ai.Training;
using AuswertungPro.Next.Application.Ai.Training.ClassMaps;
using AuswertungPro.Next.Application.Ai.Training.ExportPlans;
using AuswertungPro.Next.Application.Ai.Training.Inventory;
using AuswertungPro.Next.Application.Protocol;
using AuswertungPro.Next.Infrastructure.Ai.Pipeline;
using AuswertungPro.Next.Infrastructure.Ai.Training.ExportPlans;

namespace AuswertungPro.Next.UI.Ai.Training;

/// <summary>
/// Baut das vollstaendige YOLO-Training-Export-Subsystem genau einmal zusammen.
/// Der zentrale ServiceProvider muss dadurch nur noch die fertige Komposition halten.
/// </summary>
public sealed class TrainingYoloExportComposition
{
    private TrainingYoloExportComposition(
        TrainingYoloExportRuntime runtime,
        TrainingYoloExportDependencies dependencies)
    {
        Runtime = runtime;
        Dependencies = dependencies;
    }

    public TrainingYoloExportRuntime Runtime { get; }
    public ITrainingDataInventoryService Inventory => Runtime.Inventory;
    public ITrainingExportRegistryStore Registry => Runtime.Registry;
    public ITrainingExportPlanInputBuilder PlanInput => Runtime.PlanInput;
    public ITrainingExportPlanService Plans => Runtime.Plans;
    public ITrainingExportSidecarRequestBuilder SidecarRequests => Runtime.SidecarRequests;
    public ITrainingExportPlanLocalExecutor LocalExecutor => Runtime.LocalExecutor;
    public ITrainingExportCompletionService Completion => Runtime.Completion;
    public ITrainingExportExecutionService Execution => Runtime.Execution;
    public ITrainingYoloExportCoordinator Coordinator => Runtime.Coordinator;
    public TrainingYoloExportDependencies Dependencies { get; }

    public static TrainingYoloExportComposition Create(
        string knowledgeRoot,
        string evalSetRoot,
        ITrainingSampleStore samples,
        ICodeCatalogProvider codeCatalog,
        ITrainingYoloClassMapStore classMap,
        PipelineConfig pipeline,
        ISidecarTelemetryWriter telemetry,
        TimeProvider timeProvider)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(knowledgeRoot);
        ArgumentException.ThrowIfNullOrWhiteSpace(evalSetRoot);
        ArgumentNullException.ThrowIfNull(samples);
        ArgumentNullException.ThrowIfNull(codeCatalog);
        ArgumentNullException.ThrowIfNull(classMap);
        ArgumentNullException.ThrowIfNull(pipeline);
        ArgumentNullException.ThrowIfNull(telemetry);
        ArgumentNullException.ThrowIfNull(timeProvider);

        var sidecarClient = new VisionPipelineClient(
            baseUri: pipeline.SidecarUrl,
            httpClient: null,
            sidecarToken: pipeline.SidecarToken,
            telemetry: telemetry);
        var runtime = TrainingYoloExportRuntime.CreateHybrid(
            new TrainingYoloExportRuntimeOptions(knowledgeRoot, evalSetRoot),
            samples,
            codeCatalog,
            classMap,
            sidecarClient,
            timeProvider);
        var dependencies = new TrainingYoloExportDependencies(runtime.Coordinator);
        return new TrainingYoloExportComposition(runtime, dependencies);
    }
}
