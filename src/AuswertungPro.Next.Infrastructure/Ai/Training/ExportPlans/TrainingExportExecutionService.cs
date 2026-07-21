using AuswertungPro.Next.Application.Ai;
using AuswertungPro.Next.Application.Ai.Training.ExportPlans;

namespace AuswertungPro.Next.Infrastructure.Ai.Training.ExportPlans;

/// <summary>
/// Fuehrt einen fertigen Exportplan ueber Sidecar oder den gleichwertigen lokalen
/// Ausfuehrer aus. Alle Rueckfall- und Antwortpruefungen liegen zentral hier.
/// </summary>
public sealed class TrainingExportExecutionService : ITrainingExportExecutionService
{
    private readonly IVisionPipelineClient _sidecarClient;
    private readonly ITrainingExportSidecarRequestBuilder _sidecarRequestBuilder;
    private readonly ITrainingExportPlanLocalExecutor _localExecutor;
    private readonly string _datasetRoot;

    public TrainingExportExecutionService(
        IVisionPipelineClient sidecarClient,
        ITrainingExportSidecarRequestBuilder sidecarRequestBuilder,
        ITrainingExportPlanLocalExecutor localExecutor,
        string datasetRoot)
    {
        _sidecarClient = sidecarClient ?? throw new ArgumentNullException(nameof(sidecarClient));
        _sidecarRequestBuilder = sidecarRequestBuilder
            ?? throw new ArgumentNullException(nameof(sidecarRequestBuilder));
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

        var health = await _sidecarClient
            .CheckHealthDetailedAsync(cancellationToken)
            .ConfigureAwait(false);
        if (!health.IsReachable)
        {
            var local = await RunLocalAsync(bundle, cancellationToken).ConfigureAwait(false);
            return new TrainingExportExecutionOutcome(
                TrainingExportExecutionRoute.LocalSidecarOffline,
                local,
                Detail: health.Error);
        }

        if (!health.IsAuthorized)
        {
            throw new TrainingExportPlanException(
                "Sidecar ist erreichbar, aber die Anmeldung ist ungueltig. Kein automatischer Sicherheits-Bypass.");
        }

        if (health.Health is null)
        {
            throw new TrainingExportPlanException(
                $"Sidecar ist erreichbar, aber nicht betriebsbereit: {health.Error ?? "keine Health-Antwort"}");
        }

        if (bundle.Plan.Images.Count > TrainingExportSidecarRequestBuilder.MaximumImagesPerRequest)
        {
            var local = await RunLocalAsync(bundle, cancellationToken).ConfigureAwait(false);
            return new TrainingExportExecutionOutcome(
                TrainingExportExecutionRoute.LocalRequestTooLarge,
                local,
                health.Health.Version,
                $"Plan hat {bundle.Plan.Images.Count} Bilder; hoechstens " +
                $"{TrainingExportSidecarRequestBuilder.MaximumImagesPerRequest} sind pro Sidecar-Request erlaubt.");
        }

        var request = await _sidecarRequestBuilder
            .BuildAsync(bundle, cancellationToken)
            .ConfigureAwait(false);
        try
        {
            var response = await _sidecarClient
                .ExportPlannedTrainingAsync(request, cancellationToken)
                .ConfigureAwait(false);
            return new TrainingExportExecutionOutcome(
                TrainingExportExecutionRoute.Sidecar,
                MapResponse(response, bundle.Plan),
                health.Health.Version);
        }
        catch (SidecarUnavailableException ex)
        {
            var local = await RunLocalAsync(bundle, cancellationToken).ConfigureAwait(false);
            return new TrainingExportExecutionOutcome(
                TrainingExportExecutionRoute.LocalAfterTransportFailure,
                local,
                health.Health.Version,
                ex.Message);
        }
    }

    private Task<TrainingExportExecutionResult> RunLocalAsync(
        TrainingExportPlanBundle bundle,
        CancellationToken cancellationToken)
        => _localExecutor.ExecuteAsync(bundle, _datasetRoot, cancellationToken);

    private TrainingExportExecutionResult MapResponse(
        TrainingExportPlanResponseDto response,
        TrainingExportPlan plan)
    {
        ArgumentNullException.ThrowIfNull(response);
        if (!string.Equals(
                response.SchemaVersion,
                TrainingExportPlan.CurrentSchemaVersion,
                StringComparison.Ordinal))
        {
            throw new TrainingExportPlanException(
                $"Sidecar meldet unbekannte Exportversion '{response.SchemaVersion}'.");
        }

        if (!string.Equals(response.PlanId, plan.PlanId, StringComparison.OrdinalIgnoreCase)
            || !string.Equals(response.PlanSha256, plan.PlanId, StringComparison.OrdinalIgnoreCase))
        {
            throw new TrainingExportPlanException(
                "Sidecar-Bestaetigung gehoert nicht zum aktuellen Exportplan.");
        }

        var status = response.Status switch
        {
            "created" => TrainingExportExecutionStatus.Created,
            "already_complete" => TrainingExportExecutionStatus.AlreadyComplete,
            _ => throw new TrainingExportPlanException(
                $"Sidecar meldet unbekannten Exportstatus '{response.Status}'.")
        };
        ValidateSidecarOutputPaths(response);
        return new TrainingExportExecutionResult(
            response.PlanId,
            response.PlanSha256,
            status,
            response.TotalSamples,
            response.TrainCount,
            response.ValidationCount,
            response.ClassCount,
            response.DatasetPath,
            response.DataYamlPath,
            response.ManifestPath,
            response.WrittenImageSha256);
    }

    private void ValidateSidecarOutputPaths(TrainingExportPlanResponseDto response)
    {
        try
        {
            var expectedDataset = Path.Combine(_datasetRoot, response.PlanId);
            var expectedYaml = Path.Combine(expectedDataset, "data.yaml");
            var expectedManifest = Path.Combine(expectedDataset, "manifest.json");
            if (!Path.GetFullPath(response.DatasetPath)
                    .Equals(expectedDataset, StringComparison.OrdinalIgnoreCase)
                || !Path.GetFullPath(response.DataYamlPath)
                    .Equals(expectedYaml, StringComparison.OrdinalIgnoreCase)
                || !Path.GetFullPath(response.ManifestPath)
                    .Equals(expectedManifest, StringComparison.OrdinalIgnoreCase))
            {
                throw new TrainingExportPlanException(
                    "Sidecar und lokaler Export verwenden unterschiedliche Zielordner. " +
                    "Pruefe SEWER_SIDECAR_TRAINING_EXPORT_ROOT.");
            }
        }
        catch (Exception ex) when (ex is ArgumentException or NotSupportedException or PathTooLongException)
        {
            throw new TrainingExportPlanException("Sidecar meldet ungueltige Exportpfade.", ex);
        }
    }
}
