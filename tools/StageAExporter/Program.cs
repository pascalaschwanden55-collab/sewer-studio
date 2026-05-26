using AuswertungPro.Next.Application.Ai.Training;
using AuswertungPro.Next.Application.Protocol;

var options = StageAExporterCliOptions.Parse(args);
if (options.ShowHelp)
{
    StageAExporterCliOptions.PrintHelp();
    return 0;
}

try
{
    var catalog = new ManifestCodeCatalogProvider(options.CatalogPath);
    var result = await new StageAExporter(catalog).ExportAsync(new StageAExportOptions(
        SourceSamplesPath: options.SourceSamplesPath,
        EvalSetRoot: options.EvalSetRoot,
        OutputRoot: options.OutputRoot,
        DryRun: options.DryRun,
        ValidationRatio: options.ValidationRatio,
        DegreeOfParallelism: options.Workers,
        RequireBoundingBox: options.RequireBoundingBox));

    Console.WriteLine(options.DryRun
        ? "Stage-A Dry-Run:"
        : "Stage-A Export:");
    Console.WriteLine($"  Quelle:              {options.SourceSamplesPath}");
    Console.WriteLine($"  Eval-Set:            {options.EvalSetRoot}");
    Console.WriteLine($"  VSA-KEK-Katalog:     {options.CatalogPath}");
    Console.WriteLine($"  Ziel:                {options.OutputRoot}");
    Console.WriteLine($"  Eingang:             {result.InputSamples}");
    Console.WriteLine($"  Approved:            {result.ApprovedSamples}");
    Console.WriteLine($"  Not approved raus:   {result.SkippedNotApproved}");
    Console.WriteLine($"  Eval-Treffer raus:   {result.SkippedEvalSet}");
    Console.WriteLine($"  Fehlend/kaputt raus: {result.SkippedMissingOrCorrupt}");
    Console.WriteLine($"  Ungueltiger Code:    {result.SkippedInvalidCode}");
    Console.WriteLine($"  Nicht im Katalog:    {result.SkippedInvalidCatalogCode}");
    Console.WriteLine($"  Ohne echte Box raus: {result.SkippedWithoutBoundingBox}");
    Console.WriteLine($"  Doppelte Bilder raus:{result.SkippedDuplicateImage}");
    Console.WriteLine($"  Final:               {result.FinalSamples}");
    Console.WriteLine($"  Train:               {result.TrainSamples}");
    Console.WriteLine($"  Val:                 {result.ValidationSamples}");
    Console.WriteLine($"  Eval-Hashes:         {result.EvalHashesCount}");
    Console.WriteLine($"  Hashlisten-Hash:     {result.EvalHashListSha256}");

    if (!options.DryRun)
    {
        Console.WriteLine($"  Manifest:            {result.ManifestPath}");
        Console.WriteLine($"  Clean Samples:       {result.CleanTrainingSamplesPath}");
        Console.WriteLine($"  data.yaml:           {result.DataYamlPath}");
    }

    Console.WriteLine();
    Console.WriteLine("Klassen:");
    foreach (var c in result.Classes.Take(40))
        Console.WriteLine($"  {c.ClassName,-8} {c.Total,6}  train {c.Train,6}  val {c.Validation,6}");

    return 0;
}
catch (Exception ex)
{
    Console.Error.WriteLine($"FEHLER: {ex.Message}");
    return 2;
}

internal sealed record StageAExporterCliOptions(
    string SourceSamplesPath,
    string EvalSetRoot,
    string OutputRoot,
    string CatalogPath,
    bool DryRun,
    double ValidationRatio,
    int Workers,
    bool RequireBoundingBox,
    bool ShowHelp)
{
    public static StageAExporterCliOptions Parse(string[] args)
    {
        var source = @"C:\KI_BRAIN\training_samples.json";
        var evalSet = @"C:\KI_BRAIN\eval_set";
        var output = @"D:\stage_a_clean";
        var catalog = ResolveDefaultCatalogPath();
        var dryRun = false;
        var validationRatio = 0.2;
        var workers = 0;
        var requireBoundingBox = false;
        var help = false;

        for (var i = 0; i < args.Length; i++)
        {
            switch (args[i])
            {
                case "--help" or "-h" or "/?":
                    help = true;
                    break;
                case "--source":
                    source = RequireValue(args, ref i, "--source");
                    break;
                case "--eval-set":
                    evalSet = RequireValue(args, ref i, "--eval-set");
                    break;
                case "--out":
                    output = RequireValue(args, ref i, "--out");
                    break;
                case "--catalog":
                    catalog = RequireValue(args, ref i, "--catalog");
                    break;
                case "--dry-run":
                    dryRun = true;
                    break;
                case "--val-ratio":
                    validationRatio = double.Parse(
                        RequireValue(args, ref i, "--val-ratio"),
                        System.Globalization.CultureInfo.InvariantCulture);
                    break;
                case "--workers":
                    workers = int.Parse(RequireValue(args, ref i, "--workers"));
                    break;
                case "--require-bbox":
                    requireBoundingBox = true;
                    break;
                default:
                    throw new ArgumentException($"Unbekannte Option: {args[i]}");
            }
        }

        if (validationRatio is < 0 or > 1)
            throw new ArgumentException("--val-ratio muss zwischen 0 und 1 liegen.");
        if (workers < 0)
            throw new ArgumentException("--workers darf nicht negativ sein.");
        if (string.IsNullOrWhiteSpace(catalog) || !File.Exists(catalog))
            throw new FileNotFoundException("VSA-KEK-Katalogmanifest nicht gefunden.", catalog);

        return new StageAExporterCliOptions(
            source,
            evalSet,
            output,
            catalog,
            dryRun,
            validationRatio,
            workers,
            requireBoundingBox,
            help);
    }

    public static void PrintHelp()
    {
        Console.WriteLine("""
        StageAExporter

        Baut einen sauberen Stage-A-Trainingsdatensatz aus training_samples.json.
        Eval-Bilder werden per SHA-256 ausgeschlossen.

        Beispiele:

          dotnet run --project tools\StageAExporter -- --dry-run

          dotnet run --project tools\StageAExporter -- `
            --source C:\KI_BRAIN\training_samples.json `
            --eval-set C:\KI_BRAIN\eval_set `
            --out D:\stage_a_clean `
            --val-ratio 0.2

          dotnet run --project tools\StageAExporter -- `
            --dry-run `
            --require-bbox `
            --out D:\stage_a_bbox_clean

        Optionen:
          --source <pfad>     Standard: C:\KI_BRAIN\training_samples.json
          --eval-set <pfad>   Standard: C:\KI_BRAIN\eval_set
          --out <pfad>        Standard: D:\stage_a_clean
          --catalog <pfad>    Standard: VSA_KEK_2020_CATALOG_MANIFEST oder src\AuswertungPro.Next.UI\Data\vsa_kek_2020_catalog_manifest.json
          --dry-run           Nur zaehlen, nichts kopieren
          --val-ratio <zahl>  Standard: 0.2
          --workers <zahl>    0 = automatisch
          --require-bbox      Nur Samples mit echter Bounding-Box exportieren
        """);
    }

    private static string RequireValue(string[] args, ref int index, string name)
    {
        if (index + 1 >= args.Length)
            throw new ArgumentException($"{name} braucht einen Wert.");

        index++;
        return args[index];
    }

    private static string ResolveDefaultCatalogPath()
    {
        var env = Environment.GetEnvironmentVariable("VSA_KEK_2020_CATALOG_MANIFEST");
        if (!string.IsNullOrWhiteSpace(env) && File.Exists(env))
            return env;

        var repoPath = Path.GetFullPath(Path.Combine(
            Environment.CurrentDirectory,
            "src",
            "AuswertungPro.Next.UI",
            "Data",
            "vsa_kek_2020_catalog_manifest.json"));
        if (File.Exists(repoPath))
            return repoPath;

        var appDataPath = Path.Combine(AppContext.BaseDirectory, "Data", "vsa_kek_2020_catalog_manifest.json");
        return appDataPath;
    }
}
