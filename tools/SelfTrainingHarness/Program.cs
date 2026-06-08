using AuswertungPro.Next.Application.Ai;
using AuswertungPro.Next.Application.Ai.Training;
using AuswertungPro.Next.Infrastructure.Ai;
using AuswertungPro.Next.Infrastructure.Ai.KnowledgeBase;
using AuswertungPro.Next.Infrastructure.Ai.Training;
using AuswertungPro.Next.Infrastructure.Ai.Training.Services;

// Lokaler, geboundeter Headless-Beweis: faehrt den ECHTEN Self-Training-Orchestrator
// (mit echtem Qwen) ueber ein paar eligible Faelle und beweist:
//   - alle erzeugten Samples landen als Review-Kandidaten (kein Auto-Approve)
//   - nichts wird in die KB indexiert (KbIndexState=None)
//   - TrainingEligible spiegelt das (jetzt aufgeloeste) Datum
//   - keine YOLO-Dummy-Box (Orchestrator exportiert nie)
// Non-destruktiv: der training_samples.json-Store wird vorher gesichert und nachher wiederhergestellt.

var root = args.Length > 0 ? args[0] : @"D:\Haltungen";
var maxCases = args.Length > 1 && int.TryParse(args[1], out var m) ? m : 5;
var ollamaUri = new Uri(args.Length > 2 ? args[2] : "http://localhost:11434");
var model = args.Length > 3 ? args[3] : "qwen3-vl:8b-q8";

Console.WriteLine("=== Self-Training Headless-Harness (gebounded, non-destruktiv) ===");
Console.WriteLine($"Root: {root} | maxCases: {maxCases} | Ollama: {ollamaUri} | Modell: {model}");

// 1. Scan + eligible filtern (Datum>=2022 UND PDF-Protokoll vorhanden)
var import = new TrainingCenterImportService();
var cases = await import.ScanAsync(root);
var eligibleAll = cases.Where(c =>
    !string.IsNullOrEmpty(c.ProtocolPath)
    && c.ProtocolPath.EndsWith(".pdf", StringComparison.OrdinalIgnoreCase)
    && c.InspectionDate is not null
    && TrainingSampleEligibility.Evaluate(c.InspectionDate).IsEligible).ToList();
var eligible = eligibleAll.Take(maxCases).ToList();

Console.WriteLine($"Gescannt: {cases.Count} | eligible (Datum>=2022 + PDF): {eligibleAll.Count} | verwende: {eligible.Count}");
if (eligible.Count == 0) { Console.WriteLine("Keine eligible Faelle gefunden."); return; }
foreach (var c in eligible)
    Console.WriteLine($"  - {c.CaseId,-28} | {Path.GetFileName(c.ProtocolPath)} | {c.InspectionDate:yyyy-MM-dd}");

// 2. Store-Snapshot + KB-Zustand vorher
var storePath = KnowledgeBasePaths.GetTrainingSamplesPath();
var dbPath = KnowledgeBasePaths.GetKnowledgeDbPath();
var storeExisted = File.Exists(storePath);
string? backup = null;
if (storeExisted)
{
    backup = storePath + ".harness-bak";
    File.Copy(storePath, backup, overwrite: true);
}
var idsBefore = (await TrainingSamplesStore.LoadAsync()).Select(s => s.SampleId).ToHashSet();
var dbBefore = DbState(dbPath);

var results = new List<SelfTrainingResult>();
try
{
    // 3. Services (catalog=null und retrieval=null halten den Harness UI-/KB-frei;
    //    beides aendert die Auto-Accept-Entscheidung unter RequireHumanReview nicht.)
    var settings = await TrainingCenterSettingsStore.LoadAsync();
    Console.WriteLine($"RequireHumanReview: {settings.RequireHumanReview}");

    using var ollama = new OllamaClient(ollamaUri, ownedTimeout: TimeSpan.FromSeconds(180), keepAlive: "24h", numCtx: 12288);
    var vision = new EnhancedVisionAnalysisService(ollama, model, null);
    var comparison = new SelfTrainingComparisonService();
    ITechniqueAssessmentService technique = new StubTechnique();
    var pdf = new PdfProtocolExtractor();
    var orch = new SelfTrainingOrchestrator(vision, comparison, technique, pdf, settings, "ffmpeg", retrieval: null);

    var progress = new Progress<SelfTrainingStep>(s =>
    {
        if (s.Stage == SelfTrainingStage.Completed && s.Comparison is not null)
            Console.WriteLine($"    [{s.EntryIndex + 1}/{s.TotalEntries}] {s.VsaCode,-8} -> {s.Comparison.Level}");
        else if (!string.IsNullOrEmpty(s.ErrorMessage))
            Console.WriteLine($"    ! {s.ErrorMessage}");
    });

    using var cts = new CancellationTokenSource();
    foreach (var c in eligible)
    {
        Console.WriteLine($"\n--- Lauf: {c.CaseId} ({c.InspectionDate:yyyy-MM-dd}) ---");
        try
        {
            var r = await orch.RunAsync(c, progress, cts.Token);
            results.Add(r);
            Console.WriteLine($"  Ergebnis: {r.SamplesGenerated} Samples | Exact {r.ExactMatches}, Partial {r.PartialMatches}, Mismatch {r.Mismatches}, NoFindings {r.NoFindings} | {r.Duration.TotalSeconds:F0}s");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"  FEHLER: {ex.GetType().Name}: {ex.Message}");
        }
    }

    // 4. Neu erzeugte Samples aus dem Store herausfiltern und pruefen
    var after = await TrainingSamplesStore.LoadAsync();
    var caseIds = eligible.Select(c => c.CaseId).ToHashSet();
    var newSamples = after.Where(s => !idsBefore.Contains(s.SampleId) && caseIds.Contains(s.CaseId)).ToList();

    Console.WriteLine($"\n=== BEWEIS (ueber {newSamples.Count} neu erzeugte Samples) ===");
    int approved = newSamples.Count(s => s.Status == TrainingSampleStatus.Approved);
    int indexed = newSamples.Count(s => s.KbIndexState == KbIndexState.Indexed);
    int held = newSamples.Count(s => s.Status == TrainingSampleStatus.New);
    int eligibleTrue = newSamples.Count(s => s.TrainingEligible);

    Console.WriteLine($"Status=New (Review-Kandidaten):  {held}");
    Console.WriteLine($"Status=Approved (Auto-Gold):     {approved}   <- MUSS 0 sein (RequireHumanReview)");
    Console.WriteLine($"KbIndexState=Indexed (Auto-KB):  {indexed}   <- MUSS 0 sein");
    Console.WriteLine($"TrainingEligible=true:           {eligibleTrue}/{newSamples.Count}");

    Console.WriteLine("MatchLevel-Verteilung:");
    foreach (var g in newSamples.GroupBy(s => s.MatchLevel).OrderByDescending(g => g.Count()))
        Console.WriteLine($"  {g.Key,-14}: {g.Count()}");

    Console.WriteLine("Beispiele (CaseId | Code @ Meter | Status/Kb/Match | Grund):");
    foreach (var s in newSamples.Take(10))
        Console.WriteLine($"   {s.CaseId,-24} | {s.Code,-7} @ {s.MeterStart,5:F1}m | {s.Status}/{s.KbIndexState}/{s.MatchLevel} | {s.Notes}");

    var dbAfter = DbState(dbPath);
    Console.WriteLine();
    Console.WriteLine($"KB-DB vorher : {dbBefore}");
    Console.WriteLine($"KB-DB nachher: {dbAfter}");
    Console.WriteLine($"  -> KB unangetastet (keine Auto-KB): {(dbBefore == dbAfter ? "JA" : "NEIN!")}");
    Console.WriteLine("  -> YOLO-Export ausgeloest: NEIN (Orchestrator exportiert nie -> keine Dummy-Box moeglich)");

    var verdictOk = approved == 0 && indexed == 0 && dbBefore == dbAfter;
    Console.WriteLine();
    Console.WriteLine(verdictOk
        ? ">>> GESAMT: Phase-1-Garantien halten im echten Lauf (kein Auto-Approve, keine Auto-KB)."
        : ">>> GESAMT: ABWEICHUNG! Bitte Ausgabe pruefen.");
}
finally
{
    // 5. Store wiederherstellen (non-destruktiv)
    if (storeExisted && backup is not null)
    {
        File.Copy(backup, storePath, overwrite: true);
        File.Delete(backup);
        Console.WriteLine("\nStore wiederhergestellt (kein Sample dauerhaft persistiert).");
    }
    else if (!storeExisted && File.Exists(storePath))
    {
        File.Delete(storePath);
        Console.WriteLine("\nWaehrend des Laufs angelegter Store wieder entfernt (non-destruktiv).");
    }
}

return;

static string DbState(string p)
    => File.Exists(p) ? $"{new FileInfo(p).Length} B @ {File.GetLastWriteTimeUtc(p):yyyy-MM-dd HH:mm:ss}" : "(keine Datei)";

// Neutraler Technik-Stub: Aufnahmetechnik fliesst NICHT in die Auto-Accept-Entscheidung ein
// (die haengt nur an MatchLevel + RequireHumanReview + KbCheck). So bleibt der Harness UI-frei.
sealed class StubTechnique : ITechniqueAssessmentService
{
    private static readonly TechniqueAssessment Neutral = new(
        OsdReadable: false, OsdDeltaMeters: null, LightingQuality: "n/a",
        SharpnessQuality: "n/a", CenteringQuality: null, OverallGrade: "n/a",
        MeanLuminance: 0, LaplacianVariance: 0);

    public TechniqueAssessment AssessFrame(byte[] pngBytes, double? osdMeterReading, double protocolMeter) => Neutral;

    public Task<TechniqueAssessment> AssessFrameWithVisionAsync(byte[] pngBytes, double? osdMeterReading, double protocolMeter, CancellationToken ct)
        => Task.FromResult(Neutral);
}
