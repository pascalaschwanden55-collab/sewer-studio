namespace AuswertungPro.Next.Application.Ai.Training.ExportPlans;

public interface ITrainingExportCompletionService
{
    TrainingExportCompletionResult Apply(
        TrainingExportPlan plan,
        TrainingExportExecutionResult execution,
        IReadOnlyList<TrainingSample> samples,
        DateTime exportedUtc);
}
