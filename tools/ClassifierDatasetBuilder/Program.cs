using System.Globalization;
using System.Security.Cryptography;
using System.Text.Json;
using AuswertungPro.Next.Application.Ai.Evaluation;
using AuswertungPro.Next.Application.Ai.Training;

// Baut aus training_frames einen eval-freien YOLO-cls-Datensatz (v1, Hauptcode-Ebene).
// Eval-Schutz doppelt: per Dateiname UND per SHA-256 (umbenannte Kopien). KEIN Training.

string? Arg(string name) { var i = Array.IndexOf(args, name); return (i >= 0 && i + 1 < args.Length) ? args[i + 1] : null; }
bool Flag(string name) => Array.IndexOf(args, name) >= 0;

string framesRoot = Arg("--frames") ?? @"C:\KI_BRAIN\training_frames";
string evalSet    = Arg("--eval-set") ?? @"C:\KI_BRAIN\eval_set";
string outDir     = Arg("--out") ?? @"C:\KI_BRAIN\yolo_vsa_cls_dataset";
double valFraction = double.TryParse(Arg("--val-fraction"), NumberStyles.Float, CultureInfo.InvariantCulture, out var vf) ? vf : 0.2;
int seed = int.TryParse(Arg("--seed"), out var sd) ? sd : 42;
bool dryRun = Flag("--dry-run");
int leerOversample = int.TryParse(Arg("--leer-oversample"), out var lo) ? Math.Max(1, lo) : 1;

Console.WriteLine($"Frames:   {framesRoot}");
Console.WriteLine($"Eval-Set: {evalSet}");
Console.WriteLine($"Ausgabe:  {outDir}{(dryRun ? "   (DRY-RUN, schreibt nichts)" : "")}");
Console.WriteLine($"Split:    val={valFraction:P0}, seed={seed}");
Console.WriteLine($"LEER-Oversample (nur train): {leerOversample}x");
Console.WriteLine();

// Schutz (User-Bedingung): bestehenden, nicht-leeren Zielordner NICHT ueberschreiben.
if (!dryRun && Directory.Exists(outDir) && Directory.EnumerateFileSystemEntries(outDir).Any())
{
    Console.Error.WriteLine($"ABBRUCH: Zielordner existiert bereits und ist nicht leer:\n  {outDir}");
    Console.Error.WriteLine("Bitte --out auf einen neuen, nicht existierenden Ordner setzen oder den Ordner entfernen.");
    return 1;
}

static string Sha256Hex(string path)
{
    using var fs = File.OpenRead(path);
    return Convert.ToHexString(SHA256.HashData(fs)).ToLowerInvariant();
}

// 1) Eval-Schutz: Bild-Hashes + Bild-Dateinamen des eingefrorenen Eval-Sets
var evalHashes = EvalSetManifestHasher.ComputeHashes(evalSet).Hashes
    .Where(h => h.RelativePath.StartsWith("images/", StringComparison.OrdinalIgnoreCase))
    .Select(h => h.Sha256Hex.ToLowerInvariant())
    .ToHashSet(StringComparer.OrdinalIgnoreCase);
var evalImagesDir = Path.Combine(evalSet, "images");
var evalNames = Directory.Exists(evalImagesDir)
    ? Directory.EnumerateFiles(evalImagesDir).Select(f => Path.GetFileName(f)!).ToHashSet(StringComparer.OrdinalIgnoreCase)
    : new HashSet<string>(StringComparer.OrdinalIgnoreCase);

// 2) Frames lesen, parsen, mappen, Eval hart ausschliessen
var kept = new List<(FrameInfo Info, string Path)>();
int total = 0, exclEvalName = 0, exclEvalHash = 0, exclCode = 0, exclUnparsed = 0;
foreach (var path in Directory.EnumerateFiles(framesRoot, "*.png", SearchOption.AllDirectories))
{
    total++;
    var name = Path.GetFileName(path);
    if (evalNames.Contains(name)) { exclEvalName++; continue; }
    if (!ClassifierDatasetPlan.TryParseFrame(name, out var info)) { exclUnparsed++; continue; }
    if (info.TrainingClass is null) { exclCode++; continue; }
    if (evalHashes.Contains(Sha256Hex(path))) { exclEvalHash++; continue; }
    kept.Add((info, path));
}

// 3) Haltungs-Split (eine Haltung komplett in genau einem Split)
var valSet = ClassifierDatasetPlan.SelectValHaltungen(kept.Select(k => k.Info.Haltung), valFraction, seed);

// 4) Kopieren (ausser dry-run) + Zaehlung
var trainTally = new Dictionary<string, int>(StringComparer.Ordinal);
var valTally = new Dictionary<string, int>(StringComparer.Ordinal);
var haltungenPerClass = new Dictionary<string, HashSet<string>>(StringComparer.Ordinal);
foreach (var (info, path) in kept)
{
    var cls = info.TrainingClass!;
    var isVal = valSet.Contains(info.Haltung);
    // LEER wird NUR im train-Split oversampled (val bleibt repraesentativ/ehrlich).
    var copies = (!isVal && cls == "LEER") ? leerOversample : 1;
    var tally = isVal ? valTally : trainTally;
    tally[cls] = tally.GetValueOrDefault(cls) + copies;
    if (!haltungenPerClass.TryGetValue(cls, out var hs)) { hs = new HashSet<string>(StringComparer.Ordinal); haltungenPerClass[cls] = hs; }
    hs.Add(info.Haltung);
    if (!dryRun)
    {
        var dstDir = Path.Combine(outDir, isVal ? "val" : "train", cls);
        Directory.CreateDirectory(dstDir);
        var fn = Path.GetFileName(path);
        File.Copy(path, Path.Combine(dstDir, fn), overwrite: true);
        for (int k = 1; k < copies; k++)
        {
            var stem = Path.GetFileNameWithoutExtension(fn);
            var ext = Path.GetExtension(fn);
            File.Copy(path, Path.Combine(dstDir, $"{stem}_os{k}{ext}"), overwrite: true);
        }
    }
}

// 5) Schwache Klassen (zu wenige Haltungen -> Generalisierungs-Risiko), z.B. BBA
const int WeakHaltungThreshold = 30;
var weak = haltungenPerClass.Where(kv => kv.Value.Count < WeakHaltungThreshold)
    .ToDictionary(kv => kv.Key, kv => kv.Value.Count, StringComparer.Ordinal);

// 6) Report
var classes = ClassifierDatasetPlan.TargetClasses.OrderBy(c => c, StringComparer.Ordinal).Select(c => new
{
    klasse = c,
    train = trainTally.GetValueOrDefault(c),
    val = valTally.GetValueOrDefault(c),
    haltungen = haltungenPerClass.TryGetValue(c, out var hs) ? hs.Count : 0,
    schwach = weak.ContainsKey(c)
}).ToList();

var report = new
{
    created_utc = DateTimeOffset.UtcNow.ToString("O"),
    frames_root = framesRoot, eval_set = evalSet, out_dir = outDir,
    seed, val_fraction = valFraction, dry_run = dryRun, leer_oversample = leerOversample,
    frames_total = total,
    excluded_eval_by_name = exclEvalName,
    excluded_eval_by_hash = exclEvalHash,
    excluded_non_target = exclCode,
    excluded_unparsed = exclUnparsed,
    kept_total = kept.Count,
    weak_classes = weak,
    classes
};
if (!dryRun)
{
    Directory.CreateDirectory(outDir);
    File.WriteAllText(Path.Combine(outDir, "dataset_report.json"),
        JsonSerializer.Serialize(report, new JsonSerializerOptions { WriteIndented = true }));
}

// 7) Konsolen-Zusammenfassung
Console.WriteLine($"Frames gesamt:           {total}");
Console.WriteLine($"Eval ausgeschlossen:     {exclEvalName} per Name + {exclEvalHash} per Hash  (zusammen sollte ~120 sein)");
Console.WriteLine($"Nicht-Zielcode raus:     {exclCode}");
Console.WriteLine($"Unparsebar raus:         {exclUnparsed}");
Console.WriteLine($"Behalten (trainierbar):  {kept.Count}");
Console.WriteLine();
Console.WriteLine($"{"Klasse",-8} {"train",7} {"val",6} {"Haltungen",10}  Hinweis");
foreach (var c in classes)
    Console.WriteLine($"{c.klasse,-8} {c.train,7} {c.val,6} {c.haltungen,10}  {(c.schwach ? "SCHWACH (<30 Haltungen)" : "")}");
if (!dryRun) Console.WriteLine($"\nReport: {Path.Combine(outDir, "dataset_report.json")}");

return 0;
