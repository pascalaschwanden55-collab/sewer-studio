namespace SewerStudio.Tools.StageAExporter;

public sealed record StageAExporterCliOptions(
    string KnowledgeRoot,
    string SourceSamplesPath,
    string EvalSetRoot,
    string DatasetRoot,
    string CatalogPath,
    string ClassMapPath,
    string ClassMigrationPath,
    bool PlanOnly,
    int Workers,
    bool WorkersSpecified,
    bool ShowHelp)
{
    private const string DefaultKnowledgeRoot = @"C:\KI_BRAIN";
    private const string DefaultEvalSetRoot = @"C:\KI_BRAIN\eval_set";

    public static StageAExporterCliOptions Parse(string[] args)
    {
        ArgumentNullException.ThrowIfNull(args);
        if (args.Any(IsHelp))
            return CreateHelpOptions();

        string? explicitRoot = null;
        string? source = null;
        string? evalSet = null;
        string? output = null;
        string? catalog = null;
        var planOnly = false;
        var workers = 0;
        var workersSpecified = false;
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        for (var index = 0; index < args.Length; index++)
        {
            var option = args[index];
            switch (option)
            {
                case "--knowledge-root":
                    EnsureSingle(seen, option);
                    explicitRoot = RequireValue(args, ref index, option);
                    break;
                case "--source":
                    EnsureSingle(seen, option);
                    source = RequireValue(args, ref index, option);
                    break;
                case "--eval-set":
                    EnsureSingle(seen, option);
                    evalSet = RequireValue(args, ref index, option);
                    break;
                case "--out":
                    EnsureSingle(seen, option);
                    output = RequireValue(args, ref index, option);
                    break;
                case "--catalog":
                    EnsureSingle(seen, option);
                    catalog = RequireValue(args, ref index, option);
                    break;
                case "--dry-run" or "--plan-only":
                    EnsureSingle(seen, "--plan-only");
                    planOnly = true;
                    break;
                case "--workers":
                    EnsureSingle(seen, option);
                    workersSpecified = true;
                    if (!int.TryParse(RequireValue(args, ref index, option), out workers) || workers < 0)
                        throw new ArgumentException("--workers muss eine ganze Zahl ab 0 sein.");
                    break;
                case "--require-bbox":
                    EnsureSingle(seen, option);
                    break;
                case "--val-ratio":
                    throw new ArgumentException(
                        "--val-ratio ist nicht mehr erlaubt. Train/Val kommt ausschliesslich " +
                        "aus dem menschlich freigegebenen Haltungsregister.");
                case "--allow-dummy-bbox":
                    throw new ArgumentException(
                        "--allow-dummy-bbox ist nicht mehr erlaubt. Ersatzboxen sind im AP-0.3-Export verboten.");
                default:
                    throw new ArgumentException($"Unbekannte Option: {option}");
            }
        }

        var sourceRoot = string.IsNullOrWhiteSpace(source)
            ? null
            : Path.GetDirectoryName(NormalizeAbsolute(source, "--source"));
        var environmentRoot = Environment.GetEnvironmentVariable("SEWERSTUDIO_KNOWLEDGE_ROOT");
        var root = NormalizeAbsolute(
            explicitRoot
            ?? environmentRoot
            ?? sourceRoot
            ?? DefaultKnowledgeRoot,
            "Wissensordner");
        var canonicalSource = Path.Combine(root, "training_samples.json");
        var canonicalDatasetRoot = Path.Combine(root, "training", "datasets");

        if (!string.IsNullOrWhiteSpace(source)
            && !PathsEqual(source, canonicalSource))
        {
            throw new ArgumentException(
                $"--source darf nur die aktive Datei sein: {canonicalSource}");
        }
        if (!string.IsNullOrWhiteSpace(output)
            && !PathsEqual(output, canonicalDatasetRoot))
        {
            throw new ArgumentException(
                $"--out darf nur der zentrale Datensatzordner sein: {canonicalDatasetRoot}");
        }

        return new StageAExporterCliOptions(
            root,
            canonicalSource,
            NormalizeAbsolute(
                evalSet
                ?? Environment.GetEnvironmentVariable("SEWERSTUDIO_EVAL_SET_ROOT")
                ?? DefaultEvalSetRoot,
                "Eval-Set"),
            canonicalDatasetRoot,
            NormalizeAbsolute(catalog ?? ResolvePackagedCatalogPath(), "VSA-Katalog"),
            NormalizeAbsolute(ResolvePackagedClassMapPath(), "Detect-Klassenkarte"),
            NormalizeAbsolute(ResolvePackagedClassMigrationPath(), "Klassenmigration"),
            planOnly,
            workers,
            workersSpecified,
            ShowHelp: false);
    }

    public static void PrintHelp(TextWriter output)
    {
        ArgumentNullException.ThrowIfNull(output);
        output.WriteLine("""
        StageAExporter - sichere Kommandozeile fuer den zentralen AP-0.3-YOLO-Export

        Der Export verwendet exakt denselben Inventar-, Register-, Klassenkarten- und
        Planweg wie SewerStudio. Ausgaben liegen immer unter:
          <Wissensordner>\training\datasets\<plan-id>

        Beispiele:
          dotnet run --project tools\StageAExporter -- --dry-run
          dotnet run --project tools\StageAExporter -- --knowledge-root C:\KI_BRAIN

        Optionen:
          --knowledge-root <pfad>  Aktiver Wissensordner; Standard: Umgebungsvariable
                                   SEWERSTUDIO_KNOWLEDGE_ROOT, sonst C:\KI_BRAIN
          --source <pfad>          Kompatibilitaet: nur <Wissensordner>\training_samples.json
          --eval-set <pfad>        Standard: SEWERSTUDIO_EVAL_SET_ROOT oder C:\KI_BRAIN\eval_set
          --out <pfad>             Kompatibilitaet: nur <Wissensordner>\training\datasets
          --catalog <pfad>         VSA-KEK-Katalogmanifest; Klassen-IDs kommen nie daraus
          --dry-run, --plan-only   Alles pruefen und planen, aber nichts schreiben
          --require-bbox           Kompatibilitaet; echte gueltige Boxen sind immer Pflicht
          --workers <zahl>         Kompatibilitaet; der zentrale Ablauf steuert die Arbeit
          --help, -h, /?           Diese Hilfe

        Nicht mehr erlaubt:
          --val-ratio              Split kommt aus dem freigegebenen Haltungsregister
          --allow-dummy-bbox       Ersatzboxen sind verboten
        """);
    }

    private static StageAExporterCliOptions CreateHelpOptions()
    {
        var root = Path.GetFullPath(DefaultKnowledgeRoot);
        return new StageAExporterCliOptions(
            root,
            Path.Combine(root, "training_samples.json"),
            Path.GetFullPath(DefaultEvalSetRoot),
            Path.Combine(root, "training", "datasets"),
            ResolvePackagedCatalogPath(),
            ResolvePackagedClassMapPath(),
            ResolvePackagedClassMigrationPath(),
            PlanOnly: false,
            Workers: 0,
            WorkersSpecified: false,
            ShowHelp: true);
    }

    private static bool IsHelp(string value)
        => value is "--help" or "-h" or "/?";

    private static void EnsureSingle(ISet<string> seen, string option)
    {
        if (!seen.Add(option))
            throw new ArgumentException($"Option doppelt angegeben: {option}");
    }

    private static string RequireValue(string[] args, ref int index, string option)
    {
        if (index + 1 >= args.Length || args[index + 1].StartsWith("--", StringComparison.Ordinal))
            throw new ArgumentException($"{option} braucht einen Wert.");
        return args[++index];
    }

    private static string NormalizeAbsolute(string path, string label)
    {
        if (string.IsNullOrWhiteSpace(path) || !Path.IsPathFullyQualified(path.Trim()))
            throw new ArgumentException($"{label} muss ein vollstaendiger Pfad sein.");
        return Path.TrimEndingDirectorySeparator(Path.GetFullPath(path.Trim()));
    }

    private static bool PathsEqual(string left, string right)
        => NormalizeAbsolute(left, "Pfad").Equals(
            NormalizeAbsolute(right, "Pfad"),
            StringComparison.OrdinalIgnoreCase);

    private static string ResolvePackagedCatalogPath()
        => Path.Combine(AppContext.BaseDirectory, "Data", "vsa_kek_2020_catalog_manifest.json");

    private static string ResolvePackagedClassMapPath()
        => Path.Combine(AppContext.BaseDirectory, "Data", "Training", "detect_class_map_v3.json");

    private static string ResolvePackagedClassMigrationPath()
        => Path.Combine(
            AppContext.BaseDirectory,
            "Data",
            "Training",
            "detect_class_migration_v3.candidate.json");
}
