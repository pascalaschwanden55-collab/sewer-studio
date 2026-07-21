namespace AuswertungPro.Next.Application.Ai.Training.ExportPlans;

public interface ITrainingExportSidecarRequestBuilder
{
    Task<TrainingExportPlanRequestDto> BuildAsync(
        TrainingExportPlanBundle bundle,
        CancellationToken cancellationToken = default);
}
