namespace AuswertungPro.Next.Application.Ai.Training.ExportPlans;

public enum TrainingExportExecutionStatus
{
    Created,
    AlreadyComplete
}

public sealed record TrainingExportExecutionResult(
    string PlanId,
    string PlanSha256,
    TrainingExportExecutionStatus Status,
    int TotalImages,
    int TrainImages,
    int ValidationImages,
    int ClassCount,
    string DatasetPath,
    string DataYamlPath,
    string ManifestPath,
    IReadOnlyList<string> WrittenImageSha256);

public interface ITrainingExportPlanLocalExecutor
{
    Task<TrainingExportExecutionResult> ExecuteAsync(
        TrainingExportPlanBundle bundle,
        string datasetRoot,
        CancellationToken cancellationToken = default);
}
