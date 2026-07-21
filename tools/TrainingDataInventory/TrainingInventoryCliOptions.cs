using AuswertungPro.Next.Application.Ai.Training.Inventory;

namespace TrainingDataInventory;

internal sealed record TrainingInventoryCliOptions
{
    private const string DefaultKnowledgeRoot = @"C:\KI_BRAIN";

    public bool ShowHelp { get; private init; }
    public required string KnowledgeRoot { get; init; }
    public required string EvalSetRoot { get; init; }
    public required string OutputPath { get; init; }
    public required IReadOnlyList<string> SearchRoots { get; init; }
    public required IReadOnlyList<string> ProtectedRoots { get; init; }
    public bool IncludeBackups { get; init; }
    public bool ComputeAssetHashes { get; init; }

    public TrainingDataInventoryRequest CreateRequest()
        => new()
        {
            KnowledgeRoot = KnowledgeRoot,
            EvalSetRoot = EvalSetRoot,
            SearchRoots = SearchRoots,
            ProtectedRoots = ProtectedRoots,
            IncludeBackups = IncludeBackups,
            ComputeAssetHashes = ComputeAssetHashes
        };

    public static TrainingInventoryCliOptions Parse(
        IReadOnlyList<string> arguments,
        DateTimeOffset generatedAtUtc)
    {
        ArgumentNullException.ThrowIfNull(arguments);

        string? rootValue = null;
        string? evalRootValue = null;
        string? outputValue = null;
        var searchRootValues = new List<string>();
        var protectedRootValues = new List<string>();
        var flags = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        for (var index = 0; index < arguments.Count; index++)
        {
            var argument = arguments[index];
            switch (argument.ToLowerInvariant())
            {
                case "--help":
                case "--current-only":
                case "--no-hashes":
                    AddFlag(flags, argument);
                    break;
                case "--root":
                    rootValue = ReadSingleValue(arguments, ref index, argument, rootValue);
                    break;
                case "--eval-root":
                    evalRootValue = ReadSingleValue(arguments, ref index, argument, evalRootValue);
                    break;
                case "--out":
                    outputValue = ReadSingleValue(arguments, ref index, argument, outputValue);
                    break;
                case "--search-root":
                    searchRootValues.Add(ReadValue(arguments, ref index, argument));
                    break;
                case "--protected-root":
                    protectedRootValues.Add(ReadValue(arguments, ref index, argument));
                    break;
                default:
                    throw new ArgumentException($"Unbekannte Option: {argument}");
            }
        }

        var root = Path.GetFullPath(rootValue ?? DefaultKnowledgeRoot);
        var evalRoot = Path.GetFullPath(evalRootValue ?? Path.Combine(root, "eval_set"));
        var output = Path.GetFullPath(
            outputValue
            ?? Path.Combine(
                root,
                "training",
                "reports",
                $"training_inventory_{generatedAtUtc:yyyyMMdd_HHmmss_fff}.json"));

        return new TrainingInventoryCliOptions
        {
            ShowHelp = flags.Contains("--help"),
            KnowledgeRoot = root,
            EvalSetRoot = evalRoot,
            OutputPath = output,
            SearchRoots = ResolveRoots(searchRootValues, CreateDefaultSearchRoots(root)),
            ProtectedRoots = ResolveRoots(protectedRootValues, CreateDefaultProtectedRoots(root, evalRoot))
                .Append(evalRoot)
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToArray(),
            IncludeBackups = !flags.Contains("--current-only"),
            ComputeAssetHashes = !flags.Contains("--no-hashes")
        };
    }

    private static IReadOnlyList<string> CreateDefaultSearchRoots(string root)
        => TrainingDataInventoryRequestFactory.CreateDefaultSearchRoots(root);

    private static IReadOnlyList<string> CreateDefaultProtectedRoots(string root, string evalRoot)
        => TrainingDataInventoryRequestFactory.CreateDefaultProtectedRoots(root, evalRoot);

    private static IReadOnlyList<string> ResolveRoots(
        IReadOnlyList<string> configured,
        IReadOnlyList<string> defaults)
        => (configured.Count > 0 ? configured : defaults)
            .Select(Path.GetFullPath)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();

    private static void AddFlag(ISet<string> flags, string option)
    {
        if (!flags.Add(option))
            throw new ArgumentException($"Option wurde mehrfach angegeben: {option}");
    }

    private static string ReadSingleValue(
        IReadOnlyList<string> arguments,
        ref int index,
        string option,
        string? existingValue)
    {
        if (existingValue is not null)
            throw new ArgumentException($"Option wurde mehrfach angegeben: {option}");
        return ReadValue(arguments, ref index, option);
    }

    private static string ReadValue(
        IReadOnlyList<string> arguments,
        ref int index,
        string option)
    {
        if (index + 1 >= arguments.Count
            || arguments[index + 1].StartsWith("--", StringComparison.Ordinal))
        {
            throw new ArgumentException($"Wert fehlt fuer {option}.");
        }

        index++;
        if (string.IsNullOrWhiteSpace(arguments[index]))
            throw new ArgumentException($"Wert fehlt fuer {option}.");
        return arguments[index];
    }
}
