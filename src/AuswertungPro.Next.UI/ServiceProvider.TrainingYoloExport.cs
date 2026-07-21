using AuswertungPro.Next.Application.Ai.Training.ExportPlans;
using AuswertungPro.Next.Application.Ai.Training.Inventory;
using AuswertungPro.Next.UI.Ai.Training;

namespace AuswertungPro.Next.UI;

public sealed partial class ServiceProvider
{
    private readonly TrainingYoloExportComposition _trainingYoloExportComposition;

    public ITrainingDataInventoryService TrainingDataInventory => _trainingYoloExportComposition.Inventory;
    public ITrainingExportRegistryStore TrainingExportRegistry => _trainingYoloExportComposition.Registry;
    public ITrainingExportPlanInputBuilder TrainingExportPlanInput => _trainingYoloExportComposition.PlanInput;
    public ITrainingExportPlanService TrainingExportPlans => _trainingYoloExportComposition.Plans;
    public ITrainingExportSidecarRequestBuilder TrainingExportSidecarRequests => _trainingYoloExportComposition.SidecarRequests;
    public ITrainingExportPlanLocalExecutor TrainingExportLocalExecutor => _trainingYoloExportComposition.LocalExecutor;
    public ITrainingExportCompletionService TrainingExportCompletion => _trainingYoloExportComposition.Completion;
    public ITrainingExportExecutionService TrainingExportExecution => _trainingYoloExportComposition.Execution;
    public ITrainingYoloExportCoordinator TrainingYoloExportCoordinator => _trainingYoloExportComposition.Coordinator;
    public TrainingYoloExportDependencies TrainingYoloExport => _trainingYoloExportComposition.Dependencies;
}
