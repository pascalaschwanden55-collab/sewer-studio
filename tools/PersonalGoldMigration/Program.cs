using AuswertungPro.Next.Application.Ai.Training;
using AuswertungPro.Next.Infrastructure.Ai.Training;

var knowledgeRoot = @"C:\KI_BRAIN";
var confirmedBy = Environment.UserName;
var dryRun = false;
for (var index = 0; index < args.Length; index++)
{
    switch (args[index])
    {
        case "--knowledge-root" when index + 1 < args.Length:
            knowledgeRoot = args[++index];
            break;
        case "--confirmed-by" when index + 1 < args.Length:
            confirmedBy = args[++index];
            break;
        case "--dry-run":
            dryRun = true;
            break;
        default:
            Console.Error.WriteLine($"Unbekannte oder unvollstaendige Option: {args[index]}");
            return 2;
    }
}

var request = new PersonalGoldFrameMigrationRequest(
    knowledgeRoot,
    confirmedBy,
    PersonalGoldMainCodeCatalog.RequiredCodes,
    DateTimeOffset.UtcNow,
    TargetMinimumPerMainCode: 30,
    TargetMaximumPerMainCode: 50,
    DryRun: dryRun);
var result = await new PersonalGoldFrameMigrationService().MigrateAsync(request);

Console.WriteLine($"Modus: {(result.DryRun ? "PRUEFLAUF" : "MIGRATION")}");
Console.WriteLine($"Persoenliche Samples: {result.SelectedSamples}");
Console.WriteLine($"Uebernommen: {result.MigratedSamples}");
Console.WriteLine($"Eindeutige Goldbilder: {result.UniqueGoldFrames}");
Console.WriteLine($"Vollstaendig mit Box und Segmentierung: {result.FullGoldSamples}");
foreach (var code in result.MainCodes)
{
    Console.WriteLine(
        $"{code.MainCode}: voll={code.FullGoldSamples}, Bilder={code.UniqueGoldFrames}, " +
        $"fehlen bis {code.TargetMinimum}={code.NeededForMinimum}, Status={code.Status}");
}
if (!string.IsNullOrWhiteSpace(result.InventoryPath))
    Console.WriteLine($"Inventar: {result.InventoryPath}");
if (!string.IsNullOrWhiteSpace(result.AuditDirectory))
    Console.WriteLine($"Pruefspur: {result.AuditDirectory}");
if (!result.Success)
{
    Console.Error.WriteLine(result.Error);
    return 1;
}

return 0;
