using AuswertungPro.Next.Application.Ai.Training;
using AuswertungPro.Next.Infrastructure.Ai.Training;
using AuswertungPro.Next.Infrastructure.Backup;
using Microsoft.Extensions.Logging.Abstractions;

var startedUtc = DateTimeOffset.UtcNow;
var knowledgeRoot = @"C:\KI_BRAIN";
var confirmedBy = Environment.UserName;
var legacyProtocolTraining = Path.Combine(
    Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
    "SewerStudio",
    "data",
    "protocol_training.json");
var execute = false;
string? recoveryArchive = null;

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
        case "--legacy-protocol-training" when index + 1 < args.Length:
            legacyProtocolTraining = args[++index];
            break;
        case "--execute":
            execute = true;
            break;
        case "--dry-run":
            execute = false;
            break;
        case "--recover-from" when index + 1 < args.Length:
            recoveryArchive = args[++index];
            break;
        default:
            Console.Error.WriteLine($"Unbekannte oder unvollstaendige Option: {args[index]}");
            return 2;
    }
}

var elementsRoot = ResolveElementsRoot();
if (elementsRoot is null)
{
    Console.Error.WriteLine("Der Datentraeger \"Elements\" ist nicht angeschlossen.");
    return 1;
}

knowledgeRoot = Path.TrimEndingDirectorySeparator(Path.GetFullPath(knowledgeRoot));
var suffix = startedUtc.ToString("yyyyMMdd_HHmmss");
var localArchive = knowledgeRoot + "_ALT_" + suffix;
var externalMirror = Path.Combine(elementsRoot, "Brain");
var externalArchive = Path.Combine(
    elementsRoot,
    "Brain_Archiv",
    "KI_BRAIN_ALT_" + suffix);

if (!string.IsNullOrWhiteSpace(recoveryArchive))
{
    var recoveryOnly = await RecoverArchiveOnlyAsync(
        knowledgeRoot,
        recoveryArchive,
        confirmedBy,
        startedUtc,
        execute,
        externalMirror);
    return recoveryOnly;
}

var request = new PersonalGoldBrainSeparationRequest(
    knowledgeRoot,
    localArchive,
    externalMirror,
    externalArchive,
    legacyProtocolTraining,
    confirmedBy,
    startedUtc,
    PersonalGoldMainCodeCatalog.RequiredCodes,
    DryRun: !execute);
var result = await new PersonalGoldBrainSeparationService().SeparateAsync(request);

Console.WriteLine($"Modus: {(result.DryRun ? "PRUEFLAUF" : "ECHTE TRENNUNG")}");
Console.WriteLine($"Trainingssamples vorher: {result.SourceSamples}");
Console.WriteLine($"Persoenliche Goldsamples: {result.PersonalGoldSamples}");
Console.WriteLine($"Vollstaendig mit Box und Segmentierung: {result.FullGoldSamples}");
Console.WriteLine($"Wissensfaelle vorher: {result.SourceKnowledgeSamples}");
Console.WriteLine($"Wissensfaelle im neuen Gehirn: {result.ActiveKnowledgeSamples}");
if (!string.IsNullOrWhiteSpace(result.LocalArchiveRoot))
    Console.WriteLine($"Lokales Altarchiv: {result.LocalArchiveRoot}");
if (!string.IsNullOrWhiteSpace(result.ExternalArchiveRoot))
    Console.WriteLine($"Elements-Altarchiv: {result.ExternalArchiveRoot}");
if (!string.IsNullOrWhiteSpace(result.ReceiptPath))
    Console.WriteLine($"Pruefbeleg: {result.ReceiptPath}");

if (!result.Success)
{
    Console.Error.WriteLine(result.Error);
    return 1;
}

var recoveryRoot = execute
    ? result.LocalArchiveRoot!
    : knowledgeRoot;
var recovery = await new PersonalGoldArchiveRecoveryService().RecoverAsync(
    new PersonalGoldArchiveRecoveryRequest(
        knowledgeRoot,
        recoveryRoot,
        confirmedBy,
        startedUtc,
        PersonalGoldMainCodeCatalog.RequiredCodes,
        DryRun: !execute));
Console.WriteLine($"Nur in alter Datenbank gefundene Handlabels: {recovery.DatabaseOnlyCandidates}");
Console.WriteLine($"Davon nachgeholt: {recovery.RecoveredSamples}");
Console.WriteLine($"Persoenliche Goldsamples nach Nachholung: {recovery.ActivePersonalGoldSamples}");
if (!string.IsNullOrWhiteSpace(recovery.ReceiptPath))
    Console.WriteLine($"Nachholbeleg: {recovery.ReceiptPath}");

if (execute)
{
    var mirror = new KnowledgeRealtimeMirrorService(
        knowledgeRoot,
        NullLogger<KnowledgeRealtimeMirrorService>.Instance);
    await mirror.SynchronizeNowAsync();
    mirror.Dispose();
    Console.WriteLine($"Neuer Gold-Spiegel: {externalMirror}");
}

if (!recovery.Success)
{
    Console.Error.WriteLine(recovery.Error);
    return 1;
}

return 0;

static async Task<int> RecoverArchiveOnlyAsync(
    string knowledgeRoot,
    string recoveryArchive,
    string confirmedBy,
    DateTimeOffset startedUtc,
    bool execute,
    string externalMirror)
{
    var result = await new PersonalGoldArchiveRecoveryService().RecoverAsync(
        new PersonalGoldArchiveRecoveryRequest(
            knowledgeRoot,
            Path.GetFullPath(recoveryArchive),
            confirmedBy,
            startedUtc,
            PersonalGoldMainCodeCatalog.RequiredCodes,
            DryRun: !execute));
    Console.WriteLine($"Modus: {(result.DryRun ? "NACHHOL-PRUEFLAUF" : "ECHTE NACHHOLUNG")}");
    Console.WriteLine($"Persoenliche Goldsamples vorher: {result.ExistingPersonalGoldSamples}");
    Console.WriteLine($"Nur in alter Datenbank gefundene Handlabels: {result.DatabaseOnlyCandidates}");
    Console.WriteLine($"Davon nachgeholt: {result.RecoveredSamples}");
    Console.WriteLine($"Persoenliche Goldsamples danach: {result.ActivePersonalGoldSamples}");
    if (!string.IsNullOrWhiteSpace(result.ReceiptPath))
        Console.WriteLine($"Nachholbeleg: {result.ReceiptPath}");

    if (execute)
    {
        var mirror = new KnowledgeRealtimeMirrorService(
            knowledgeRoot,
            NullLogger<KnowledgeRealtimeMirrorService>.Instance);
        await mirror.SynchronizeNowAsync();
        mirror.Dispose();
        Console.WriteLine($"Aktualisierter Gold-Spiegel: {externalMirror}");
    }

    if (result.Success)
        return 0;
    Console.Error.WriteLine(result.Error);
    return 1;
}

static string? ResolveElementsRoot()
{
    foreach (var drive in DriveInfo.GetDrives().OrderBy(drive => drive.Name, StringComparer.OrdinalIgnoreCase))
    {
        try
        {
            if (drive.IsReady
                && string.Equals(drive.VolumeLabel, "Elements", StringComparison.OrdinalIgnoreCase))
            {
                return drive.RootDirectory.FullName;
            }
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            // Ein gerade abgezogenes Laufwerk wird wie "nicht angeschlossen" behandelt.
        }
    }

    return null;
}
