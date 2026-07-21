using AuswertungPro.Next.Application.Ai.Training.ExportPlans;

namespace AuswertungPro.Next.Infrastructure.Ai.Training.ExportPlans;

/// <summary>
/// Fuehrt einen verbindlichen Exportplan bewusst lokal aus. Dieser Weg ist fuer
/// Werkzeuge ohne laufenden Sidecar gedacht und trifft keine eigenen Exportentscheidungen.
/// </summary>
public sealed class TrainingExportLocalExecutionService : ITrainingExportExecutionService
{
    private readonly ITrainingExportPlanLocalExecutor _localExecutor;
    private readonly string _datasetRoot;

    public TrainingExportLocalExecutionService(
        ITrainingExportPlanLocalExecutor localExecutor,
        string datasetRoot)
    {
        _localExecutor = localExecutor ?? throw new ArgumentNullException(nameof(localExecutor));
        ArgumentException.ThrowIfNullOrWhiteSpace(datasetRoot);
        _datasetRoot = Path.GetFullPath(datasetRoot);
    }

    public async Task<TrainingExportExecutionOutcome> ExecuteAsync(
        TrainingExportPlanBundle bundle,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(bundle);
        TrainingExportPlanValidator.Validate(bundle.Plan);
        if (bundle.Plan.Images.Count == 0)
            throw new TrainingExportPlanException("Der Exportplan enthaelt keine auszugebenden Bilder.");

        var result = await _localExecutor
            .ExecuteAsync(bundle, _datasetRoot, cancellationToken)
            .ConfigureAwait(false);
        return new TrainingExportExecutionOutcome(
            TrainingExportExecutionRoute.LocalRequested,
            result);
    }
}
