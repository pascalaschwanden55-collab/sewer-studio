namespace AuswertungPro.Next.Application.Ai.Training.Inventory;

/// <summary>Eine gemeinsame Pfadregel fuer CLI-Inventar und produktiven Export.</summary>
public static class TrainingDataInventoryRequestFactory
{
    public static TrainingDataInventoryRequest CreateStrictCurrentSnapshot(
        string knowledgeRoot,
        string evalSetRoot,
        IReadOnlyDictionary<string, string>? protectedSetRoots = null)
    {
        var root = Path.GetFullPath(knowledgeRoot);
        var evalRoot = Path.GetFullPath(evalSetRoot);
        var setRoots = protectedSetRoots ?? new Dictionary<string, string>();
        return new TrainingDataInventoryRequest
        {
            KnowledgeRoot = root,
            EvalSetRoot = evalRoot,
            SearchRoots = CreateDefaultSearchRoots(root),
            ProtectedRoots = CreateDefaultProtectedRoots(root, evalRoot)
                .Concat(setRoots.Values)
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToArray(),
            ProtectedSetRoots = new Dictionary<string, string>(
                setRoots,
                StringComparer.OrdinalIgnoreCase),
            IncludeBackups = false,
            ComputeAssetHashes = true
        };
    }

    public static IReadOnlyList<string> CreateDefaultSearchRoots(string knowledgeRoot)
    {
        var root = Path.GetFullPath(knowledgeRoot);
        return
        [
            Path.Combine(root, "images"),
            Path.Combine(root, "teacher_images"),
            Path.Combine(root, "teacher_labels"),
            Path.Combine(root, "frames"),
            Path.Combine(root, "training_frames"),
            Path.Combine(root, "training", "frames")
        ];
    }

    public static IReadOnlyList<string> CreateDefaultProtectedRoots(
        string knowledgeRoot,
        string evalSetRoot)
    {
        var root = Path.GetFullPath(knowledgeRoot);
        var evalRoot = Path.GetFullPath(evalSetRoot);
        return
        [
            evalRoot,
            Path.Combine(root, "gold_frames"),
            Path.Combine(root, "gold_frames_annotated"),
            Path.Combine(root, "training", "testset_gold")
        ];
    }
}
