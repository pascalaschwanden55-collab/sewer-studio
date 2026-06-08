using System.Globalization;
using System.Text.RegularExpressions;
using AuswertungPro.Next.Application.Ai.Training;
using AuswertungPro.Next.Infrastructure.Ai.Training;

// Lokales Diagnose-Tool: faehrt den ECHTEN Scan-/Datums-Aufloesungspfad ueber D:\Haltungen
// und beweist deterministisch, wie viele Faelle der yyyyMMdd-Fix entsperrt.

var root = args.Length > 0 ? args[0] : @"D:\Haltungen";
Console.WriteLine($"=== InspectionDate-Audit: {root} ===");

var svc = new TrainingCenterImportService();
var cases = await svc.ScanAsync(root);

if (cases.Count == 0)
{
    Console.WriteLine("Keine Faelle gefunden (Pfad leer/unerreichbar).");
    return;
}

// --- Autoritative Zahlen aus ScanAsync (neuer Parser, inkl. .json/.xml-Textdaten) ---
var withDate = cases.Where(c => c.InspectionDate is not null).ToList();
var eligibleByDate = withDate.Where(c => TrainingSampleEligibility.Evaluate(c.InspectionDate).IsEligible).ToList();
var before2022 = withDate.Where(c => !TrainingSampleEligibility.Evaluate(c.InspectionDate).IsEligible).ToList();
var noDate = cases.Where(c => c.InspectionDate is null).ToList();

// Der rekursive Scan zaehlt auch Output-Unterordner (self_training_frames/manifest.json) als "Faelle".
// Die sind keine echten Inspektionen -> getrennt ausweisen, sonst wird "ohne Datum" kuenstlich aufgeblaeht.
bool IsFramesOutput(TrainingCaseInput c) => c.FolderPath.Contains("self_training_frames", StringComparison.OrdinalIgnoreCase);
var noDateFrames = noDate.Where(IsFramesOutput).ToList();
var noDateReal = noDate.Where(c => !IsFramesOutput(c)).ToList();

double Pct(int n) => cases.Count == 0 ? 0 : 100.0 * n / cases.Count;

Console.WriteLine();
Console.WriteLine($"Ordner gescannt (Faelle, rekursiv): {cases.Count}");
Console.WriteLine($"Mit aufgeloestem Datum:          {withDate.Count,5}  ({Pct(withDate.Count):F1}%)");
Console.WriteLine($"  davon >=2022 (export/KB-faehig): {eligibleByDate.Count,5}  ({Pct(eligibleByDate.Count):F1}%)");
Console.WriteLine($"  davon  <2022 (korrekt gesperrt): {before2022.Count,5}");
Console.WriteLine($"Ohne Datum gesamt:               {noDate.Count,5}  ({Pct(noDate.Count):F1}%)");
Console.WriteLine($"  davon self_training_frames-Output (keine echten Faelle): {noDateFrames.Count,5}");
Console.WriteLine($"  davon ECHTE Faelle ohne Datum:                           {noDateReal.Count,5}");

// --- A/B: alter vs. neuer Parser ueber dieselben Datei-/Ordner-Kandidaten ---
int oldHit = 0, newHit = 0, unlocked = 0, scanMismatch = 0;
var unlockedExamples = new List<string>();
foreach (var c in cases)
{
    var oldDate = ResolveWith(OldParse, c);
    var newDate = ResolveWith(TrainingSampleEligibility.TryParseInspectionDate, c);
    if (oldDate is not null) oldHit++;
    if (newDate is not null) newHit++;
    if (oldDate is null && newDate is not null)
    {
        unlocked++;
        if (unlockedExamples.Count < 10)
            unlockedExamples.Add($"   {c.CaseId,-28} | {Path.GetFileName(c.ProtocolPath)}{(string.IsNullOrEmpty(c.ProtocolPath) ? Path.GetFileName(c.VideoPath) : "")} -> {newDate:yyyy-MM-dd}");
    }
    // Plausibilitaet: deckt sich der Kandidaten-basierte neue Parser mit ScanAsync?
    if ((newDate is null) != (c.InspectionDate is null)) scanMismatch++;
}

Console.WriteLine();
Console.WriteLine("A/B ueber Datei-/Ordner-Kandidaten (alter vs. neuer Parser):");
Console.WriteLine($"  Alter Parser erkannt:                 {oldHit,5}");
Console.WriteLine($"  Neuer Parser erkannt:                 {newHit,5}");
Console.WriteLine($"  >>> NEU entsperrt durch yyyyMMdd-Fix: {unlocked,5} <<<");
Console.WriteLine($"  (Kandidaten-Parser vs. ScanAsync, Abweichungen bei Datum-vorhanden: {scanMismatch} — i.d.R. .json/.xml-Textdaten)");

if (unlockedExamples.Count > 0)
{
    Console.WriteLine();
    Console.WriteLine("Beispiele neu entsperrt (CaseId | Datei -> Datum):");
    foreach (var ex in unlockedExamples) Console.WriteLine(ex);
}

if (noDateReal.Count > 0)
{
    Console.WriteLine();
    Console.WriteLine("Beispiele ECHTE Faelle weiterhin OHNE Datum (CaseId | Protokoll | Video):");
    foreach (var c in noDateReal.Take(12))
        Console.WriteLine($"   {c.CaseId,-32} | {Path.GetFileName(c.ProtocolPath)} | {Path.GetFileName(c.VideoPath)}");
}

if (before2022.Count > 0)
{
    Console.WriteLine();
    Console.WriteLine("Beispiele <2022 (korrekt gesperrt | Datum):");
    foreach (var c in before2022.Take(8))
        Console.WriteLine($"   {c.CaseId,-28} | {c.InspectionDate:yyyy-MM-dd}");
}

return;

// ── Kandidaten-Enumeration analog EnumerateDateCandidates (ohne .json/.xml-Textzeilen) ──
static IEnumerable<string> Candidates(TrainingCaseInput c)
{
    if (!string.IsNullOrWhiteSpace(c.FolderPath))
    {
        yield return Path.GetFileName(c.FolderPath);
        yield return c.FolderPath;
    }
    if (!string.IsNullOrWhiteSpace(c.ProtocolPath))
        yield return Path.GetFileName(c.ProtocolPath);
    if (!string.IsNullOrWhiteSpace(c.VideoPath))
        yield return Path.GetFileName(c.VideoPath);
}

static DateTime? ResolveWith(Func<string?, DateTime?> parse, TrainingCaseInput c)
{
    foreach (var cand in Candidates(c))
    {
        var p = parse(cand);
        if (p is not null) return p;
    }
    return null;
}

// ── Exakte Kopie des Parsers VOR dem Fix (ohne den eingebetteten yyyyMMdd-Block) ──
static DateTime? OldParse(string? raw)
{
    if (string.IsNullOrWhiteSpace(raw)) return null;
    var text = raw.Trim();
    var formats = new[]
    {
        "dd.MM.yyyy", "d.M.yyyy", "dd.MM.yy", "d.M.yy",
        "dd/MM/yyyy", "d/M/yyyy", "dd/MM/yy", "d/M/yy",
        "yyyy-MM-dd", "yyyy/MM/dd", "yyyyMMdd"
    };
    if (DateTime.TryParseExact(text, formats, CultureInfo.InvariantCulture, DateTimeStyles.AssumeLocal, out var exact))
        return exact.Date;

    var dateMatch = Regex.Match(text, @"\b(?<d>\d{1,2})[./-](?<m>\d{1,2})[./-](?<y>\d{2,4})\b");
    if (dateMatch.Success)
    {
        var day = int.Parse(dateMatch.Groups["d"].Value, CultureInfo.InvariantCulture);
        var month = int.Parse(dateMatch.Groups["m"].Value, CultureInfo.InvariantCulture);
        var year = int.Parse(dateMatch.Groups["y"].Value, CultureInfo.InvariantCulture);
        if (year < 100) year += year >= 70 ? 1900 : 2000;
        if (OldTryCreate(year, month, day, out var parsed)) return parsed;
    }

    var isoMatch = Regex.Match(text, @"\b(?<y>\d{4})[-/](?<m>\d{1,2})[-/](?<d>\d{1,2})\b");
    if (isoMatch.Success)
    {
        var year = int.Parse(isoMatch.Groups["y"].Value, CultureInfo.InvariantCulture);
        var month = int.Parse(isoMatch.Groups["m"].Value, CultureInfo.InvariantCulture);
        var day = int.Parse(isoMatch.Groups["d"].Value, CultureInfo.InvariantCulture);
        if (OldTryCreate(year, month, day, out var parsed)) return parsed;
    }

    var yearMatch = Regex.Match(text, @"\b(?<y>19\d{2}|20\d{2})\b");
    if (yearMatch.Success)
    {
        var year = int.Parse(yearMatch.Groups["y"].Value, CultureInfo.InvariantCulture);
        return new DateTime(year, 1, 1);
    }

    return null;
}

static bool OldTryCreate(int year, int month, int day, out DateTime date)
{
    try { date = new DateTime(year, month, day); return true; }
    catch (ArgumentOutOfRangeException) { date = default; return false; }
}
