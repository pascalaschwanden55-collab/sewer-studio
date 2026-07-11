using AuswertungPro.Next.Application.Ai.Evaluation;

if (args.Contains("--help", StringComparer.OrdinalIgnoreCase) || args.Length == 0)
{
    PrintHelp();
    return args.Length == 0 ? 2 : 0;
}

try
{
    var candidates = RequireOption(args, "--candidates");
    var v1Root = Option(args, "--v1-root") ?? @"C:\KI_BRAIN\eval_set";
    var output = Option(args, "--output") ?? Path.Combine(v1Root, "v2");
    var dryRun = args.Contains("--dry-run", StringComparer.OrdinalIgnoreCase);

    var result = EvalSetV2Builder.Build(new EvalSetV2BuildOptions(
        candidates,
        output,
        v1Root,
        dryRun));

    Console.WriteLine(dryRun ? "Eval-Set V2: Pruefung erfolgreich" : "Eval-Set V2: gebaut und eingefroren");
    Console.WriteLine($"Ziel:       {result.OutputRoot}");
    Console.WriteLine($"Faelle:     {result.CandidateCount}");
    Console.WriteLine($"Haltungen:  {result.HoldingCount}");
    Console.WriteLine($"Hash-Eintraege: {result.HashesCount}");
    Console.WriteLine("Gruppen:");
    foreach (var group in result.Groups)
        Console.WriteLine($"  {group.Key,-22} {group.Value,5}");
    Console.WriteLine("DN-Bereiche:");
    foreach (var band in result.DnBands)
        Console.WriteLine($"  {band.Key,-22} {band.Value,5}");
    Console.WriteLine("Materialien:");
    foreach (var material in result.Materials)
        Console.WriteLine($"  {material.Key,-22} {material.Value,5}");
    Console.WriteLine("Bildqualitaet:");
    foreach (var quality in result.ImageQualities)
        Console.WriteLine($"  {quality.Key,-22} {quality.Value,5}");
    return 0;
}
catch (Exception ex)
{
    Console.Error.WriteLine("FEHLER: " + ex.Message);
    return 1;
}

static string RequireOption(string[] values, string name)
    => Option(values, name)
       ?? throw new ArgumentException($"Pflichtoption fehlt: {name}");

static string? Option(string[] values, string name)
{
    for (var i = 0; i < values.Length - 1; i++)
    {
        if (values[i].Equals(name, StringComparison.OrdinalIgnoreCase))
            return values[i + 1];
    }

    return null;
}

static void PrintHelp()
{
    Console.WriteLine("""
        EvalSetV2Builder

        Baut ein neues, hash-geschuetztes Eval-Set V2. V1 wird nur gelesen.

        Verwendung:
          dotnet run --project tools/EvalSetV2Builder -- \
            --candidates C:\Pfad\eval_v2_candidates.json \
            --v1-root C:\KI_BRAIN\eval_set \
            --output C:\KI_BRAIN\eval_set\v2 \
            --dry-run

        Ohne --output wird <v1-root>\v2 verwendet.
        Vor dem echten Lauf zuerst --dry-run ausfuehren.
        """);
}
