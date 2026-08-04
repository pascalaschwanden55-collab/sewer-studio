using AuswertungPro.Next.Application.Ai.Training.Inventory;
using AuswertungPro.Next.Application.UseCases.GoldQualityReview;

namespace AuswertungPro.Next.Infrastructure.Ai.Training.GoldQualityReview;

/// <summary>
/// Liefert Goldbestand und Eval-Schutz aus genau einem strikten, rein lesenden
/// Inventarlauf. Ein unvollstaendiger Schutzstand ergibt niemals eine Pruefliste.
/// </summary>
public sealed class GoldQualityReviewSnapshotProvider : IGoldQualityReviewSnapshotProvider
{
    private readonly ITrainingDataInventoryService _inventory;
    private readonly string _knowledgeRoot;
    private readonly Func<string?> _resolveEvalSetRoot;

    public GoldQualityReviewSnapshotProvider(
        ITrainingDataInventoryService inventory,
        string knowledgeRoot,
        Func<string?> resolveEvalSetRoot)
    {
        _inventory = inventory ?? throw new ArgumentNullException(nameof(inventory));
        ArgumentException.ThrowIfNullOrWhiteSpace(knowledgeRoot);
        _knowledgeRoot = Path.GetFullPath(knowledgeRoot);
        _resolveEvalSetRoot = resolveEvalSetRoot ?? throw new ArgumentNullException(nameof(resolveEvalSetRoot));
    }

    public async Task<GoldQualityReviewDataSnapshot> LoadAsync(
        IReadOnlyDictionary<string, string> protectedSetRootPaths,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(protectedSetRootPaths);
        var evalSetRoot = _resolveEvalSetRoot();
        if (string.IsNullOrWhiteSpace(evalSetRoot))
        {
            throw new InvalidOperationException(
                "Goldpruefung gesperrt: Der Eval-Schutzordner ist nicht konfiguriert.");
        }

        var request = TrainingDataInventoryRequestFactory.CreateStrictCurrentSnapshot(
            _knowledgeRoot,
            evalSetRoot,
            protectedSetRootPaths);
        var snapshot = await _inventory
            .InspectRuntimeSnapshotAsync(request, progress: null, cancellationToken)
            .ConfigureAwait(false);
        var protection = snapshot.Protection;
        if (!TrainingInventoryExitPolicy.IsSuccessful(snapshot.Report)
            || !protection.Status.Complete
            || !protection.Status.ImageHashCheckEnabled
            || protection.ImageHashes.Count == 0
            || protection.HoldingKeys.Count == 0
            || protection.Sets.Count == 0
            || string.IsNullOrWhiteSpace(protection.Fingerprint))
        {
            throw new InvalidOperationException(
                "Goldpruefung gesperrt: Der aktuelle Trainings-/Eval-Schutzscan ist unvollstaendig.");
        }

        return new GoldQualityReviewDataSnapshot(
            snapshot.TrainingSamples,
            protection.ImageHashes,
            protection.HoldingKeys,
            protection.Fingerprint);
    }
}
