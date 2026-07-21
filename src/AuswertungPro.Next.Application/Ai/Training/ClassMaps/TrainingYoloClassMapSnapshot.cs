using System.Collections.ObjectModel;

namespace AuswertungPro.Next.Application.Ai.Training.ClassMaps;

public enum TrainingYoloClassAction
{
    Map,
    Discard,
    Review
}

public enum TrainingYoloClassApprovalStatus
{
    Pending,
    Approved,
    Rejected
}

public sealed record TrainingYoloClassMapping(
    string SourceKind,
    string SourceKey,
    string? SourceId,
    TrainingYoloClassAction Action,
    string? TargetKey,
    TrainingYoloClassApprovalStatus ApprovalStatus);

public sealed record TrainingYoloClassResolution(
    string SourceKey,
    bool ShouldExport,
    string? TargetKey,
    int? ClassId);

/// <summary>
/// Unveraenderlicher Klassenstand inklusive der menschlich geprueften Alt-Zuordnungen.
/// Unbekannte oder noch offene Zuordnungen werden nie geraten.
/// </summary>
public sealed class TrainingYoloClassMapSnapshot
{
    private readonly IReadOnlyDictionary<string, int> _classes;
    private readonly IReadOnlyDictionary<string, IReadOnlyList<TrainingYoloClassMapping>> _bySourceKey;
    private readonly IReadOnlyDictionary<string, TrainingYoloClassMapping> _bySourceId;

    public TrainingYoloClassMapSnapshot(
        int version,
        string vsaManifestHash,
        IReadOnlyDictionary<string, int> classes,
        IReadOnlyList<TrainingYoloClassMapping> mappings,
        IReadOnlyDictionary<string, string>? migrationSourceHashes = null,
        IReadOnlyList<string>? resolutionOrder = null)
    {
        if (version <= 0)
            throw new ArgumentOutOfRangeException(nameof(version));
        ArgumentException.ThrowIfNullOrWhiteSpace(vsaManifestHash);
        ArgumentNullException.ThrowIfNull(classes);
        ArgumentNullException.ThrowIfNull(mappings);

        Version = version;
        VsaManifestHash = vsaManifestHash.Trim().ToLowerInvariant();
        _classes = new ReadOnlyDictionary<string, int>(
            classes.ToDictionary(
                item => item.Key,
                item => item.Value,
                StringComparer.OrdinalIgnoreCase));

        foreach (var mapping in mappings.Where(item => !string.IsNullOrWhiteSpace(item.TargetKey)))
        {
            if (!_classes.ContainsKey(mapping.TargetKey!.Trim()))
            {
                throw new TrainingYoloClassMapException(
                    $"Migrationsziel '{mapping.TargetKey}' fuer '{mapping.SourceKey}' fehlt in der Klassenkarte.");
            }
        }

        _bySourceKey = mappings
            .Where(mapping => string.IsNullOrWhiteSpace(mapping.SourceId))
            .GroupBy(mapping => Normalize(mapping.SourceKey), StringComparer.OrdinalIgnoreCase)
            .ToDictionary(
                group => group.Key,
                group => (IReadOnlyList<TrainingYoloClassMapping>)group.ToArray(),
                StringComparer.OrdinalIgnoreCase);

        var sourceIds = new Dictionary<string, TrainingYoloClassMapping>(StringComparer.OrdinalIgnoreCase);
        foreach (var mapping in mappings.Where(item => !string.IsNullOrWhiteSpace(item.SourceId)))
        {
            var sourceId = mapping.SourceId!.Trim();
            if (!sourceIds.TryAdd(sourceId, mapping))
                throw new TrainingYoloClassMapException(
                    $"Die Migrationskarte enthaelt die Quell-ID '{sourceId}' mehrfach.");
        }

        _bySourceId = new ReadOnlyDictionary<string, TrainingYoloClassMapping>(sourceIds);
        MigrationSourceHashes = new ReadOnlyDictionary<string, string>(
            (migrationSourceHashes ?? new Dictionary<string, string>())
            .ToDictionary(
                item => item.Key,
                item => item.Value,
                StringComparer.OrdinalIgnoreCase));

        var orderedKinds = (resolutionOrder ?? [])
            .Select(kind => kind?.Trim())
            .Where(kind => !string.IsNullOrWhiteSpace(kind))
            .Cast<string>()
            .ToArray();
        if (orderedKinds.Distinct(StringComparer.OrdinalIgnoreCase).Count() != orderedKinds.Length)
            throw new TrainingYoloClassMapException("Die Quellenreihenfolge enthaelt Duplikate.");
        ResolutionOrder = Array.AsReadOnly(orderedKinds);
    }

    public int Version { get; }

    public string VsaManifestHash { get; }

    public IReadOnlyDictionary<string, int> Classes => _classes;

    /// <summary>SHA-256-Auditwerte der Quellen, aus denen die Migration erzeugt wurde.</summary>
    public IReadOnlyDictionary<string, string> MigrationSourceHashes { get; }

    public IReadOnlyList<string> ResolutionOrder { get; }

    public IReadOnlyList<string> OrderedClassNames => _classes
        .OrderBy(item => item.Value)
        .Select(item => item.Key)
        .ToArray();

    public TrainingYoloClassResolution ResolveRequired(
        string sourceKey,
        string? sourceId = null,
        string? sourceKind = null)
    {
        if (string.IsNullOrWhiteSpace(sourceKey))
            throw new TrainingYoloClassMapException("Ein leerer Klassenwert darf nicht exportiert werden.");

        var normalizedSource = Normalize(sourceKey);

        if (!string.IsNullOrWhiteSpace(sourceId)
            && _bySourceId.TryGetValue(sourceId.Trim(), out var overrideMapping))
        {
            if (!string.Equals(
                    Normalize(overrideMapping.SourceKey),
                    normalizedSource,
                    StringComparison.OrdinalIgnoreCase))
            {
                throw new TrainingYoloClassMapException(
                    $"Die Einzelpruefung '{sourceId.Trim()}' gehoert zu Klasse " +
                    $"'{overrideMapping.SourceKey}', nicht zu '{sourceKey.Trim()}'.");
            }

            return ResolveMapping(normalizedSource, [overrideMapping]);
        }

        if (_classes.TryGetValue(sourceKey.Trim(), out var canonicalId))
        {
            return new TrainingYoloClassResolution(
                sourceKey.Trim(),
                ShouldExport: true,
                TargetKey: _classes.First(item => item.Value == canonicalId).Key,
                ClassId: canonicalId);
        }

        if (!_bySourceKey.TryGetValue(normalizedSource, out var mappings))
        {
            throw new TrainingYoloClassMapException(
                $"Klasse '{sourceKey.Trim()}' ist in der freigegebenen Migration nicht enthalten.");
        }

        if (!string.IsNullOrWhiteSpace(sourceKind))
        {
            var matchingKind = mappings
                .Where(mapping => string.Equals(
                    mapping.SourceKind,
                    sourceKind.Trim(),
                    StringComparison.OrdinalIgnoreCase))
                .ToArray();
            if (matchingKind.Length == 0)
            {
                throw new TrainingYoloClassMapException(
                    $"Klasse '{sourceKey.Trim()}' ist fuer Quelle '{sourceKind.Trim()}' nicht freigegeben.");
            }

            return ResolveMapping(normalizedSource, matchingKind);
        }

        foreach (var kind in ResolutionOrder)
        {
            var matchingKind = mappings
                .Where(mapping => string.Equals(
                    mapping.SourceKind,
                    kind,
                    StringComparison.OrdinalIgnoreCase))
                .ToArray();
            if (matchingKind.Length > 0)
                return ResolveMapping(normalizedSource, matchingKind);
        }

        return ResolveMapping(normalizedSource, mappings);
    }

    private TrainingYoloClassResolution ResolveMapping(
        string normalizedSource,
        IReadOnlyList<TrainingYoloClassMapping> mappings)
    {
        if (mappings.Any(mapping => mapping.ApprovalStatus != TrainingYoloClassApprovalStatus.Approved)
            || mappings.Any(mapping => mapping.Action == TrainingYoloClassAction.Review))
        {
            throw new TrainingYoloClassMapException(
                $"Klasse '{normalizedSource}' ist noch nicht menschlich freigegeben.");
        }

        var decisions = mappings
            .Select(mapping => new
            {
                mapping.Action,
                Target = string.IsNullOrWhiteSpace(mapping.TargetKey)
                    ? null
                    : mapping.TargetKey.Trim().ToUpperInvariant()
            })
            .Distinct()
            .ToArray();

        if (decisions.Length != 1)
        {
            throw new TrainingYoloClassMapException(
                $"Klasse '{normalizedSource}' hat widerspruechliche freigegebene Zuordnungen.");
        }

        var decision = decisions[0];
        if (decision.Action == TrainingYoloClassAction.Discard)
        {
            return new TrainingYoloClassResolution(
                normalizedSource,
                ShouldExport: false,
                TargetKey: null,
                ClassId: null);
        }

        if (decision.Action != TrainingYoloClassAction.Map
            || string.IsNullOrWhiteSpace(decision.Target))
        {
            throw new TrainingYoloClassMapException(
                $"Klasse '{normalizedSource}' hat keine gueltige Exportentscheidung.");
        }

        if (!_classes.TryGetValue(decision.Target, out var classId))
        {
            throw new TrainingYoloClassMapException(
                $"Zielklasse '{decision.Target}' fuer '{normalizedSource}' fehlt in class_map v{Version}.");
        }

        var canonicalKey = _classes.First(item => item.Value == classId).Key;
        return new TrainingYoloClassResolution(
            normalizedSource,
            ShouldExport: true,
            TargetKey: canonicalKey,
            ClassId: classId);
    }

    private static string Normalize(string value)
        => value.Replace(".", string.Empty, StringComparison.Ordinal)
            .Trim()
            .ToUpperInvariant();
}
