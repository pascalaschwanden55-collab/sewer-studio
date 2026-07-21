using AuswertungPro.Next.Application.Ai.Training.Inventory;

namespace AuswertungPro.Next.Infrastructure.Ai.Training.Inventory;

internal sealed class TrainingInventoryPathResolver
{
    private static readonly HashSet<string> HashableExtensions = new(
        [".png", ".jpg", ".jpeg", ".bmp", ".webp", ".txt"],
        StringComparer.OrdinalIgnoreCase);

    private readonly TrainingInventoryCandidateIndex _candidateIndex;
    private readonly string _knowledgeRoot;
    private readonly IReadOnlyList<string> _protectedRoots;
    private readonly bool _computeAssetHashes;
    private readonly Dictionary<string, TrainingInventoryHashResult> _hashCache =
        new(StringComparer.OrdinalIgnoreCase);

    private TrainingInventoryPathResolver(
        TrainingInventoryCandidateIndex candidateIndex,
        string knowledgeRoot,
        IReadOnlyList<string> protectedRoots,
        bool computeAssetHashes)
    {
        _candidateIndex = candidateIndex;
        _knowledgeRoot = knowledgeRoot;
        _protectedRoots = protectedRoots;
        _computeAssetHashes = computeAssetHashes;
    }

    public static TrainingInventoryPathResolver Create(
        string knowledgeRoot,
        IReadOnlyList<string> searchRoots,
        IReadOnlyList<string> protectedRoots,
        bool computeAssetHashes,
        ICollection<string> skippedDirectories,
        CancellationToken cancellationToken)
        => new(
            TrainingInventoryCandidateIndex.Build(
                searchRoots,
                protectedRoots,
                skippedDirectories,
                cancellationToken),
            knowledgeRoot,
            protectedRoots,
            computeAssetHashes);

    public async Task<TrainingInventoryPathReference> ResolveAsync(
        string? storedPath,
        bool hashContent,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(storedPath))
            return CreateEmpty(storedPath);

        string fullStoredPath;
        try
        {
            fullStoredPath = TrainingInventoryPaths.ResolveAgainstRoot(storedPath, _knowledgeRoot);
        }
        catch (Exception ex) when (ex is ArgumentException or NotSupportedException or PathTooLongException)
        {
            return CreateInvalid(storedPath, ex.Message);
        }

        try
        {
            var reparsePoint = TrainingInventoryPaths.FindReparsePoint(fullStoredPath);
            if (reparsePoint is not null)
                return CreateInvalid(storedPath, $"Pfad enthaelt eine Verknuepfung oder Junction: {reparsePoint}");
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            return CreateInvalid(storedPath, ex.Message);
        }

        var probe = ProbeFile(fullStoredPath);
        if (probe.Error is not null)
            return CreateInvalid(storedPath, probe.Error);
        if (probe.Exists)
            return await ResolveExistingAsync(storedPath, fullStoredPath, hashContent, cancellationToken)
                .ConfigureAwait(false);

        return await ResolveMissingAsync(storedPath, fullStoredPath, hashContent, cancellationToken)
            .ConfigureAwait(false);
    }

    private async Task<TrainingInventoryPathReference> ResolveExistingAsync(
        string storedPath,
        string fullStoredPath,
        bool hashContent,
        CancellationToken cancellationToken)
    {
        var hash = await ComputeHashIfNeededAsync(fullStoredPath, hashContent, cancellationToken)
            .ConfigureAwait(false);
        return new TrainingInventoryPathReference
        {
            StoredPath = storedPath,
            State = TrainingInventoryPathState.Existing,
            IsProtected = TrainingInventoryPaths.IsWithinAny(fullStoredPath, _protectedRoots),
            HashState = hash.State,
            ExistingPath = fullStoredPath,
            Sha256 = hash.Sha256,
            Error = hash.Error
        };
    }

    private async Task<TrainingInventoryPathReference> ResolveMissingAsync(
        string storedPath,
        string fullStoredPath,
        bool hashContent,
        CancellationToken cancellationToken)
    {
        var fileName = Path.GetFileName(fullStoredPath);
        var allowed = _candidateIndex.FindAllowed(fileName);
        var protectedCandidates = _candidateIndex.FindProtected(fileName);
        var all = allowed
            .Concat(protectedCandidates)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderBy(path => path, StringComparer.OrdinalIgnoreCase)
            .ToArray();

        if (allowed.Count == 1 && protectedCandidates.Count == 0)
        {
            var candidate = allowed[0];
            var hash = await ComputeHashIfNeededAsync(candidate, hashContent, cancellationToken)
                .ConfigureAwait(false);
            return new TrainingInventoryPathReference
            {
                StoredPath = storedPath,
                State = TrainingInventoryPathState.SuggestedForManualReview,
                HashState = hash.State,
                SuggestedPath = candidate,
                Candidates = [candidate],
                Sha256 = hash.Sha256,
                Error = hash.Error
            };
        }

        if (all.Length > 1 || (allowed.Count > 0 && protectedCandidates.Count > 0))
        {
            return new TrainingInventoryPathReference
            {
                StoredPath = storedPath,
                State = TrainingInventoryPathState.Ambiguous,
                IsProtected = protectedCandidates.Count > 0,
                HashState = TrainingInventoryHashState.NotApplicable,
                Candidates = all
            };
        }

        if (protectedCandidates.Count > 0)
        {
            return new TrainingInventoryPathReference
            {
                StoredPath = storedPath,
                State = TrainingInventoryPathState.ProtectedCandidate,
                IsProtected = true,
                HashState = TrainingInventoryHashState.NotApplicable,
                Candidates = protectedCandidates
            };
        }

        return new TrainingInventoryPathReference
        {
            StoredPath = storedPath,
            State = TrainingInventoryPathState.Missing,
            HashState = TrainingInventoryHashState.NotApplicable
        };
    }

    private async Task<TrainingInventoryHashResult> ComputeHashIfNeededAsync(
        string path,
        bool hashContent,
        CancellationToken cancellationToken)
    {
        if (!_computeAssetHashes)
            return TrainingInventoryHashResult.NotRequested;
        if (!hashContent || !HashableExtensions.Contains(Path.GetExtension(path)))
            return TrainingInventoryHashResult.NotApplicable;

        if (_hashCache.TryGetValue(path, out var cached))
            return cached;
        var result = await TrainingInventoryFileAccess.ComputeHashAsync(path, cancellationToken)
            .ConfigureAwait(false);
        _hashCache[path] = result;
        return result;
    }

    private static TrainingInventoryPathReference CreateEmpty(string? storedPath)
        => new()
        {
            StoredPath = storedPath,
            State = TrainingInventoryPathState.Empty,
            HashState = TrainingInventoryHashState.NotApplicable
        };

    private static TrainingInventoryPathReference CreateInvalid(
        string storedPath,
        string error)
        => new()
        {
            StoredPath = storedPath,
            State = TrainingInventoryPathState.Invalid,
            HashState = TrainingInventoryHashState.NotApplicable,
            Error = error
        };

    private static FileProbeResult ProbeFile(string path)
    {
        try
        {
            var attributes = File.GetAttributes(path);
            return (attributes & FileAttributes.Directory) != 0
                ? new FileProbeResult(false, "Pfad zeigt auf einen Ordner statt auf eine Datei.")
                : new FileProbeResult(true, null);
        }
        catch (FileNotFoundException)
        {
            return new FileProbeResult(false, null);
        }
        catch (DirectoryNotFoundException)
        {
            return new FileProbeResult(false, null);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            return new FileProbeResult(false, ex.Message);
        }
    }

    private readonly record struct FileProbeResult(bool Exists, string? Error);
}
