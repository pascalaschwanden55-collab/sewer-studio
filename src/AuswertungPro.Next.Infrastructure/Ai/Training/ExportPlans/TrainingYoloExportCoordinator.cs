using AuswertungPro.Next.Application.Ai.Training;
using AuswertungPro.Next.Application.Ai.Training.ClassMaps;
using AuswertungPro.Next.Application.Ai.Training.ExportPlans;
using AuswertungPro.Next.Application.Ai.Training.Inventory;
using AuswertungPro.Next.Application.Protocol;

namespace AuswertungPro.Next.Infrastructure.Ai.Training.ExportPlans;

/// <summary>
/// Zentrale Ablaufsteuerung des plan-gesteuerten YOLO-Exports. Die UI liefert nur
/// den Befehl und stellt Fortschritt dar.
/// </summary>
public sealed class TrainingYoloExportCoordinator : ITrainingYoloExportCoordinator
{
    private readonly string _knowledgeRoot;
    private readonly string _evalSetRoot;
    private readonly ITrainingSampleStore _sampleStore;
    private readonly ICodeCatalogProvider _codeCatalog;
    private readonly ITrainingExportRegistryStore _registryStore;
    private readonly ITrainingDataInventoryService _inventoryService;
    private readonly ITrainingYoloClassMapStore _classMapStore;
    private readonly ITrainingExportPlanInputBuilder _planInputBuilder;
    private readonly ITrainingExportPlanService _planService;
    private readonly ITrainingExportExecutionService _executionService;
    private readonly ITrainingExportCompletionService _completionService;
    private readonly TimeProvider _timeProvider;

    public TrainingYoloExportCoordinator(
        string knowledgeRoot,
        string evalSetRoot,
        ITrainingSampleStore sampleStore,
        ICodeCatalogProvider codeCatalog,
        ITrainingExportRegistryStore registryStore,
        ITrainingDataInventoryService inventoryService,
        ITrainingYoloClassMapStore classMapStore,
        ITrainingExportPlanInputBuilder planInputBuilder,
        ITrainingExportPlanService planService,
        ITrainingExportExecutionService executionService,
        ITrainingExportCompletionService completionService,
        TimeProvider timeProvider)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(knowledgeRoot);
        ArgumentException.ThrowIfNullOrWhiteSpace(evalSetRoot);
        _knowledgeRoot = Path.GetFullPath(knowledgeRoot);
        _evalSetRoot = Path.GetFullPath(evalSetRoot);
        _sampleStore = sampleStore ?? throw new ArgumentNullException(nameof(sampleStore));
        _codeCatalog = codeCatalog ?? throw new ArgumentNullException(nameof(codeCatalog));
        _registryStore = registryStore ?? throw new ArgumentNullException(nameof(registryStore));
        _inventoryService = inventoryService ?? throw new ArgumentNullException(nameof(inventoryService));
        _classMapStore = classMapStore ?? throw new ArgumentNullException(nameof(classMapStore));
        _planInputBuilder = planInputBuilder ?? throw new ArgumentNullException(nameof(planInputBuilder));
        _planService = planService ?? throw new ArgumentNullException(nameof(planService));
        _executionService = executionService ?? throw new ArgumentNullException(nameof(executionService));
        _completionService = completionService ?? throw new ArgumentNullException(nameof(completionService));
        _timeProvider = timeProvider ?? throw new ArgumentNullException(nameof(timeProvider));
    }

    public async Task<TrainingYoloExportResult> RunAsync(
        TrainingYoloExportCommand command,
        IProgress<TrainingYoloExportProgress>? progress = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(command);
        cancellationToken.ThrowIfCancellationRequested();

        Report(
            progress,
            TrainingYoloExportProgressStage.PreparingSamples,
            "YOLO-Export: Trainingsfreigaben werden geprueft.");
        var registry = _registryStore.ReadBundle();
        Report(
            progress,
            TrainingYoloExportProgressStage.InspectingInventory,
            "YOLO-Export: Datenbestand und Schutz-Sets werden geprueft.");
        var inventoryRequest = TrainingDataInventoryRequestFactory.CreateStrictCurrentSnapshot(
            _knowledgeRoot,
            _evalSetRoot,
            registry.ProtectedSetRootPaths);
        var inventory = await _inventoryService
            .InspectRuntimeSnapshotAsync(
                inventoryRequest,
                cancellationToken: cancellationToken)
            .ConfigureAwait(false);

        var samples = inventory.TrainingSamples.ToList();
        var selection = SelectTrainingSamples(
            samples,
            registry.Snapshot.ApprovedBy,
            registry.Snapshot.ApprovedSampleIds);

        if (selection.RegistryGateSkippedSampleIds.Count > 0)
        {
            Report(
                progress,
                TrainingYoloExportProgressStage.RegistryGateNotice,
                $"YOLO-Export: {selection.RegistryGateSkippedSampleIds.Count} vollstaendige " +
                "Goldsamples nicht im Freigaberegister - nicht exportiert.");
        }

        cancellationToken.ThrowIfCancellationRequested();
        Report(
            progress,
            TrainingYoloExportProgressStage.CreatingPlan,
            "YOLO-Export: Ein verbindlicher Exportplan wird erstellt.");
        var classMap = _classMapStore.ReadSnapshot();
        var planInput = await _planInputBuilder
            .BuildAsync(
                inventory,
                registry.Snapshot,
                selection.ApprovedSampleIds,
                classMap,
                command.GeneratedUtc,
                cancellationToken)
            .ConfigureAwait(false);
        var bundle = _planService.CreatePlan(planInput);
        if (bundle.Plan.Images.Count == 0)
        {
            const string message = "YOLO-Export: Der gepruefte Plan enthaelt keine exportierbaren Bilder.";
            Report(progress, TrainingYoloExportProgressStage.NoImages, message, total: 0);
            return new TrainingYoloExportResult(
                TrainingYoloExportResultStatus.NoImages,
                bundle.Plan,
                null,
                new TrainingExportCompletionResult(0, []),
                RegistryGateSkippedOrNull(selection));
        }

        if (command.Mode == TrainingYoloExportMode.PlanOnly)
        {
            var message =
                $"YOLO-Exportplan geprueft: {bundle.Plan.Images.Count} Bilder, " +
                $"{bundle.Plan.Classes.Count} feste Klassen. Es wurde nichts geschrieben.";
            Report(
                progress,
                TrainingYoloExportProgressStage.Planned,
                message,
                bundle.Plan.Images.Count,
                bundle.Plan.Images.Count);
            return new TrainingYoloExportResult(
                TrainingYoloExportResultStatus.Planned,
                bundle.Plan,
                null,
                new TrainingExportCompletionResult(0, []),
                RegistryGateSkippedOrNull(selection));
        }

        Report(
            progress,
            TrainingYoloExportProgressStage.ExecutingPlan,
            $"YOLO-Export: {bundle.Plan.Images.Count} geplante Bilder werden geschrieben.",
            total: bundle.Plan.Images.Count);
        var execution = await _executionService
            .ExecuteAsync(bundle, cancellationToken)
            .ConfigureAwait(false);

        Report(
            progress,
            TrainingYoloExportProgressStage.Completing,
            CreateCompletionMessage(execution),
            total: bundle.Plan.Images.Count);
        var completion = _completionService.Apply(
            bundle.Plan,
            execution.Result,
            samples,
            _timeProvider.GetUtcNow().UtcDateTime);
        ApplyEligibilityUpdates(selection.EligibilityUpdates);
        if (completion.MarkedTrainingSamples > 0 || selection.RequiresPersistence)
            await _sampleStore.MergeOrUpdateAsync(samples).ConfigureAwait(false);
        MirrorDerivedFields(samples, command.UpdateTargets);

        var doneMessage =
            $"YOLO-Export fertig: {execution.Result.TotalImages} Bilder, " +
            $"{bundle.Plan.Classes.Count} feste Klassen -> {execution.Result.DatasetPath}";
        Report(
            progress,
            TrainingYoloExportProgressStage.Completed,
            doneMessage,
            bundle.Plan.Images.Count,
            bundle.Plan.Images.Count);
        return new TrainingYoloExportResult(
            TrainingYoloExportResultStatus.Completed,
            bundle.Plan,
            execution,
            completion,
            RegistryGateSkippedOrNull(selection));
    }

    private static IReadOnlyList<string>? RegistryGateSkippedOrNull(CandidateSelection selection)
        => selection.RegistryGateSkippedSampleIds.Count > 0
            ? selection.RegistryGateSkippedSampleIds
            : null;

    private CandidateSelection SelectTrainingSamples(
        IReadOnlyList<TrainingSample> samples,
        string? approvedBy,
        IReadOnlySet<string> registrySampleIds)
    {
        ArgumentNullException.ThrowIfNull(registrySampleIds);
        var approvedIds = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var requiresPersistence = false;
        var updates = new List<EligibilityUpdate>();
        var foundRegistryIds = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var rejectedRegistryIds = new List<string>();
        var registryGateSkippedIds = new List<string>();
        foreach (var sample in samples)
        {
            var sampleId = sample.SampleId?.Trim();
            var inRegistry = !string.IsNullOrWhiteSpace(sampleId)
                             && registrySampleIds.Contains(sampleId);
            if (registrySampleIds.Count > 0 && !inRegistry)
            {
                // Dokumentiertes Pilot-Gate: Nicht registrierte Samples bleiben
                // ausgeschlossen. Exportfaehige Goldsamples werden dabei sichtbar
                // gesammelt, damit der Gate-Effekt nicht still bleibt.
                if (!string.IsNullOrWhiteSpace(sampleId)
                    && EvaluateExportEligibility(sample, approvedBy) is { IsEligible: true })
                {
                    registryGateSkippedIds.Add(sampleId);
                }

                continue;
            }

            if (inRegistry)
                foundRegistryIds.Add(sampleId!);

            var eligibility = EvaluateExportEligibility(sample, approvedBy);
            if (eligibility is null)
            {
                if (inRegistry)
                    rejectedRegistryIds.Add(sampleId!);
                continue;
            }

            var changed = sample.TrainingEligible != eligibility.Value.IsEligible
                          || !string.Equals(
                              sample.TrainingEligibilityReason,
                              eligibility.Value.Reason,
                              StringComparison.Ordinal);
            updates.Add(new EligibilityUpdate(sample, eligibility.Value));
            requiresPersistence |= changed;
            if (eligibility.Value.IsEligible && !string.IsNullOrWhiteSpace(sampleId))
                approvedIds.Add(sampleId);
            else if (inRegistry)
                rejectedRegistryIds.Add(sampleId!);
        }

        if (registrySampleIds.Count > 0)
        {
            var missing = registrySampleIds
                .Where(sampleId => !foundRegistryIds.Contains(sampleId))
                .OrderBy(sampleId => sampleId, StringComparer.OrdinalIgnoreCase)
                .ToArray();
            if (missing.Length > 0)
            {
                throw new TrainingExportPlanException(
                    $"Freigegebene Pilot-Samples fehlen im aktuellen Bestand: {string.Join(", ", missing)}");
            }

            var rejected = rejectedRegistryIds
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .OrderBy(sampleId => sampleId, StringComparer.OrdinalIgnoreCase)
                .ToArray();
            if (rejected.Length > 0)
            {
                throw new TrainingExportPlanException(
                    $"Freigegebene Pilot-Samples sind nicht mehr exportbereit: {string.Join(", ", rejected)}");
            }
        }

        return new CandidateSelection(
            approvedIds,
            updates,
            requiresPersistence,
            registryGateSkippedIds);
    }

    /// <summary>
    /// Fuehrt dieselben Exportpruefungen wie im Auswahlweg aus, ohne etwas zu
    /// veraendern. Null bedeutet: Status oder Goldbild verhindern den Export.
    /// </summary>
    private TrainingEligibilityResult? EvaluateExportEligibility(
        TrainingSample sample,
        string? approvedBy)
    {
        if (sample.Status != TrainingSampleStatus.Approved
            || string.IsNullOrWhiteSpace(sample.FramePath)
            || !File.Exists(sample.FramePath))
        {
            return null;
        }

        var manualApproval = ManualGoldTrainingPolicy.EvaluateForExport(sample, approvedBy);
        return manualApproval.IsEligible
            ? TrainingSampleEligibility.Evaluate(sample, _codeCatalog)
            : manualApproval;
    }

    private static void ApplyEligibilityUpdates(IReadOnlyList<EligibilityUpdate> updates)
    {
        foreach (var update in updates)
        {
            update.Sample.TrainingEligible = update.Result.IsEligible;
            update.Sample.TrainingEligibilityReason = update.Result.Reason;
        }
    }

    private static void MirrorDerivedFields(
        IReadOnlyList<TrainingSample> source,
        IReadOnlyList<TrainingSample>? targets)
    {
        if (targets is null || targets.Count == 0)
            return;

        var byId = source
            .Where(sample => !string.IsNullOrWhiteSpace(sample.SampleId))
            .GroupBy(sample => sample.SampleId.Trim(), StringComparer.OrdinalIgnoreCase)
            .Where(group => group.Count() == 1)
            .ToDictionary(group => group.Key, group => group.Single(), StringComparer.OrdinalIgnoreCase);
        foreach (var target in targets)
        {
            if (string.IsNullOrWhiteSpace(target.SampleId)
                || !byId.TryGetValue(target.SampleId.Trim(), out var saved))
            {
                continue;
            }

            target.TrainingEligible = saved.TrainingEligible;
            target.TrainingEligibilityReason = saved.TrainingEligibilityReason;
            target.ExportedUtc = saved.ExportedUtc;
        }
    }

    private static string CreateCompletionMessage(TrainingExportExecutionOutcome execution)
        => execution.Route switch
        {
            TrainingExportExecutionRoute.Sidecar =>
                $"Sidecar v{execution.SidecarVersion ?? "?"} hat den Export bestaetigt.",
            TrainingExportExecutionRoute.LocalRequested =>
                "Der verbindliche Plan wurde wie angefordert lokal ausgefuehrt.",
            TrainingExportExecutionRoute.LocalSidecarOffline =>
                "Sidecar ist offline. Derselbe Plan wurde lokal ausgefuehrt.",
            TrainingExportExecutionRoute.LocalRequestTooLarge =>
                "Der Plan ist fuer einen Sidecar-Request zu gross. Derselbe Plan wurde lokal ausgefuehrt.",
            TrainingExportExecutionRoute.LocalAfterTransportFailure =>
                "Die Sidecar-Verbindung ist ausgefallen. Derselbe Plan wurde lokal ausgefuehrt.",
            _ => throw new ArgumentOutOfRangeException(nameof(execution))
        };

    private static void Report(
        IProgress<TrainingYoloExportProgress>? progress,
        TrainingYoloExportProgressStage stage,
        string message,
        int processed = 0,
        int? total = null)
        => progress?.Report(new TrainingYoloExportProgress(stage, message, processed, total));

    private sealed record CandidateSelection(
        IReadOnlySet<string> ApprovedSampleIds,
        IReadOnlyList<EligibilityUpdate> EligibilityUpdates,
        bool RequiresPersistence,
        IReadOnlyList<string> RegistryGateSkippedSampleIds);

    private sealed record EligibilityUpdate(
        TrainingSample Sample,
        TrainingEligibilityResult Result);
}
