using AuswertungPro.Next.Application.Ai;
using AuswertungPro.Next.Application.Ai.Training;
using AuswertungPro.Next.Application.Ai.Training.ClassMaps;
using AuswertungPro.Next.Application.Ai.Training.ExportPlans;
using AuswertungPro.Next.Application.Ai.Training.Inventory;
using AuswertungPro.Next.Application.Protocol;
using AuswertungPro.Next.Infrastructure.Ai.Training.Inventory;

namespace AuswertungPro.Next.Infrastructure.Ai.Training.ExportPlans;

public sealed record TrainingYoloExportRuntimeOptions(
    string KnowledgeRoot,
    string EvalSetRoot);

/// <summary>
/// Gemeinsame Zusammensetzung des plan-gesteuerten YOLO-Exports. WPF und
/// Kommandozeilenwerkzeuge erhalten dadurch dieselben Stores, Pruefungen,
/// Planer und lokalen Dateischreiber.
/// </summary>
public sealed class TrainingYoloExportRuntime
{
    private TrainingYoloExportRuntime(
        string knowledgeRoot,
        string evalSetRoot,
        string datasetRoot,
        ITrainingDataInventoryService inventory,
        ITrainingExportRegistryStore registry,
        ITrainingExportPlanInputBuilder planInput,
        ITrainingExportPlanService plans,
        ITrainingExportSidecarRequestBuilder sidecarRequests,
        ITrainingExportPlanLocalExecutor localExecutor,
        ITrainingExportCompletionService completion,
        ITrainingExportExecutionService execution,
        ITrainingYoloExportCoordinator coordinator)
    {
        KnowledgeRoot = knowledgeRoot;
        EvalSetRoot = evalSetRoot;
        DatasetRoot = datasetRoot;
        Inventory = inventory;
        Registry = registry;
        PlanInput = planInput;
        Plans = plans;
        SidecarRequests = sidecarRequests;
        LocalExecutor = localExecutor;
        Completion = completion;
        Execution = execution;
        Coordinator = coordinator;
    }

    public string KnowledgeRoot { get; }
    public string EvalSetRoot { get; }
    public string DatasetRoot { get; }
    public ITrainingDataInventoryService Inventory { get; }
    public ITrainingExportRegistryStore Registry { get; }
    public ITrainingExportPlanInputBuilder PlanInput { get; }
    public ITrainingExportPlanService Plans { get; }
    public ITrainingExportSidecarRequestBuilder SidecarRequests { get; }
    public ITrainingExportPlanLocalExecutor LocalExecutor { get; }
    public ITrainingExportCompletionService Completion { get; }
    public ITrainingExportExecutionService Execution { get; }
    public ITrainingYoloExportCoordinator Coordinator { get; }

    public static TrainingYoloExportRuntime CreateHybrid(
        TrainingYoloExportRuntimeOptions options,
        ITrainingSampleStore samples,
        ICodeCatalogProvider codeCatalog,
        ITrainingYoloClassMapStore classMap,
        IVisionPipelineClient sidecarClient,
        TimeProvider timeProvider)
    {
        ArgumentNullException.ThrowIfNull(sidecarClient);
        return Create(
            options,
            samples,
            codeCatalog,
            classMap,
            timeProvider,
            (sidecarRequests, localExecutor, datasetRoot) =>
                new TrainingExportExecutionService(
                    sidecarClient,
                    sidecarRequests,
                    localExecutor,
                    datasetRoot));
    }

    public static TrainingYoloExportRuntime CreateLocal(
        TrainingYoloExportRuntimeOptions options,
        ITrainingSampleStore samples,
        ICodeCatalogProvider codeCatalog,
        ITrainingYoloClassMapStore classMap,
        TimeProvider timeProvider)
        => Create(
            options,
            samples,
            codeCatalog,
            classMap,
            timeProvider,
            (_, localExecutor, datasetRoot) =>
                new TrainingExportLocalExecutionService(localExecutor, datasetRoot));

    private static TrainingYoloExportRuntime Create(
        TrainingYoloExportRuntimeOptions options,
        ITrainingSampleStore samples,
        ICodeCatalogProvider codeCatalog,
        ITrainingYoloClassMapStore classMap,
        TimeProvider timeProvider,
        Func<
            ITrainingExportSidecarRequestBuilder,
            ITrainingExportPlanLocalExecutor,
            string,
            ITrainingExportExecutionService> createExecution)
    {
        ArgumentNullException.ThrowIfNull(options);
        ArgumentException.ThrowIfNullOrWhiteSpace(options.KnowledgeRoot);
        ArgumentException.ThrowIfNullOrWhiteSpace(options.EvalSetRoot);
        ArgumentNullException.ThrowIfNull(samples);
        ArgumentNullException.ThrowIfNull(codeCatalog);
        ArgumentNullException.ThrowIfNull(classMap);
        ArgumentNullException.ThrowIfNull(timeProvider);
        ArgumentNullException.ThrowIfNull(createExecution);

        var root = Path.GetFullPath(options.KnowledgeRoot);
        var evalSetRoot = Path.GetFullPath(options.EvalSetRoot);
        var datasetRoot = Path.Combine(root, "training", "datasets");
        var inventory = new TrainingDataInventoryService(timeProvider);
        var registry = new TrainingExportRegistryFileStore(
            Path.Combine(root, "training", "export_registry_v1.json"),
            root);
        var planInput = new TrainingExportPlanInputBuilder();
        var plans = new TrainingExportPlanService();
        var sidecarRequests = new TrainingExportSidecarRequestBuilder();
        var localExecutor = new TrainingExportPlanLocalExecutor();
        var completion = new TrainingExportCompletionService();
        var execution = createExecution(sidecarRequests, localExecutor, datasetRoot);
        var coordinator = new TrainingYoloExportCoordinator(
            root,
            evalSetRoot,
            samples,
            codeCatalog,
            registry,
            inventory,
            classMap,
            planInput,
            plans,
            execution,
            completion,
            timeProvider);

        return new TrainingYoloExportRuntime(
            root,
            evalSetRoot,
            datasetRoot,
            inventory,
            registry,
            planInput,
            plans,
            sidecarRequests,
            localExecutor,
            completion,
            execution,
            coordinator);
    }
}
