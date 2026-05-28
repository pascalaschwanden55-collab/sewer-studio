using AuswertungPro.Next.Application.Ai.Evaluation;

var evalSetRoot = args.Length > 0
    ? args[0]
    : @"C:\KI_BRAIN\eval_set";

Console.WriteLine($"Eval-Set: {evalSetRoot}");

try
{
    var result = EvalSetManifestHasher.ComputeAndStoreHashes(evalSetRoot);
    Console.WriteLine($"Hash-Algorithmus: {result.Algorithm}");
    Console.WriteLine($"Hash-Eintraege:   {result.HashesCount}");
    Console.WriteLine("Manifest aktualisiert: _manifest.json");
    return 0;
}
catch (Exception ex)
{
    Console.Error.WriteLine($"FEHLER: {ex.Message}");
    return 2;
}
