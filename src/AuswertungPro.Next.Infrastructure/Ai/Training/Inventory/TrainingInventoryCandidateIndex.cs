namespace AuswertungPro.Next.Infrastructure.Ai.Training.Inventory;

internal sealed class TrainingInventoryCandidateIndex
{
    private readonly Dictionary<string, List<string>> _allowed = new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string, List<string>> _protected = new(StringComparer.OrdinalIgnoreCase);

    public static TrainingInventoryCandidateIndex Build(
        IReadOnlyList<string> searchRoots,
        IReadOnlyList<string> protectedRoots,
        ICollection<string> skippedDirectories,
        CancellationToken cancellationToken)
    {
        var index = new TrainingInventoryCandidateIndex();
        var visited = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach (var root in searchRoots.Concat(protectedRoots).Distinct(StringComparer.OrdinalIgnoreCase))
        {
            cancellationToken.ThrowIfCancellationRequested();
            foreach (var file in TrainingInventoryFileEnumerator.EnumerateFiles(
                         root,
                         skippedDirectories,
                         cancellationToken))
            {
                cancellationToken.ThrowIfCancellationRequested();
                var fullPath = Path.GetFullPath(file);
                if (!visited.Add(fullPath))
                    continue;

                if (TrainingInventoryPaths.IsWithinAny(fullPath, protectedRoots))
                    index.Add(index._protected, fullPath);
                else
                    index.Add(index._allowed, fullPath);
            }
        }

        foreach (var paths in index._allowed.Values.Concat(index._protected.Values))
            paths.Sort(StringComparer.OrdinalIgnoreCase);
        return index;
    }

    public IReadOnlyList<string> FindAllowed(string fileName)
        => Find(_allowed, fileName);

    public IReadOnlyList<string> FindProtected(string fileName)
        => Find(_protected, fileName);

    private void Add(IDictionary<string, List<string>> index, string path)
    {
        var fileName = Path.GetFileName(path);
        if (string.IsNullOrWhiteSpace(fileName))
            return;
        if (!index.TryGetValue(fileName, out var paths))
        {
            paths = [];
            index[fileName] = paths;
        }

        paths.Add(path);
    }

    private static IReadOnlyList<string> Find(
        IReadOnlyDictionary<string, List<string>> index,
        string fileName)
        => !string.IsNullOrWhiteSpace(fileName) && index.TryGetValue(fileName, out var paths)
            ? paths
            : Array.Empty<string>();
}
