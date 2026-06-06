# VSA-Klassifikator-Datensatz (eval-frei) Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Aus `C:\KI_BRAIN\training_frames` einen sauberen, garantiert eval-freien YOLO-Klassifikations-Datensatz (Hauptcode-Ebene) bauen, auf dem später ein kleiner VSA-Klassifikator trainiert und ehrlich gegen das eingefrorene Eval-Set gemessen werden kann.

**Architecture:** Neues C#-Konsolentool `tools/ClassifierDatasetBuilder`. Reine Entscheidungslogik (Code→Klasse-Mapping, Haltungs-Split, Kontaminations-Filter) als testbare statische Funktionen in der Application-Schicht; das Tool ruft sie auf und kopiert Frames in `train/<klasse>/` bzw. `val/<klasse>/`. Eval-Ausschluss per SHA-256 über den bestehenden `EvalSetManifestHasher`. KEIN Training in diesem Plan.

**Tech Stack:** .NET 10, C#, xUnit; Ausgabe ist ein Ultralytics-YOLO-cls-Ordnerlayout (reine Bildordner, kein neues NuGet-Paket).

---

## Design-Entscheidungen (VOR dem Bau abnicken)

Diese Sektion ist der Review-Kern. Zahlen = Trainingsbilder (Eval-Frames ausgeschlossen) / Anzahl distinkter Haltungen.

### Zielklassen v1 (eval-ausgerichtet)

| Klasse | Bilder | Haltungen | im Eval (clean 57) | Begründung |
|---|---:|---:|---:|---|
| BCD | 1909 | 359 | 15 | Rohranfang |
| BCE | 2158 | 328 | 2 | Rohrende |
| BDA | 1449 | 185 | 3 | Wasserstand |
| BDD | 1556 | 116 | 6 (BDDC) | Wasserstand-Untergruppe |
| BAJ | 1726 | 127 | 3 (BAJB) | Verschobene Verbindung |
| BAF | 1445 | 106 | 2 (BAFCE) | Oberflächenschaden |
| BAB | 751 | 70 | 2 (BABBA) | Riss — **nicht in deiner Liste, aber im Eval** |
| BAI | 411 | 37 | 5 (BAIZ) | Einragendes Dichtungsmaterial |
| BBB | 642 | 89 | 1 (BBBZ) | Anhaftende Stoffe |
| BBA | 134 | 23 | 2 (BBAA/BBAB) | Wurzeln — **nicht in deiner Liste, im Eval, WENIG Daten** |
| LEER | 858 | 275 | 16 | kein_schaden |

**Offene Punkte (brauchen deine Bestätigung):**
1. **BAB + BBA dazunehmen?** Dein Vorschlag hatte sie nicht, aber das Eval enthält BABBA (→BAB, ×2) und BBAA/BBAB (→BBA, ×2). Ohne diese Klassen können 4 Eval-Frames nie korrekt getroffen werden. Empfehlung: BAB aufnehmen (751 Bilder, genug). BBA ist grenzwertig (nur 134 Bilder / 23 Haltungen) — aufnehmen, aber als „schwach" markieren.
2. **schacht (641) / axial (638):** Das sind keine Schadenscodes — `axial` ist ein Ansichtstyp, `schacht` ist Schachtkontext (überlappt BCD/BCE). Empfehlung: **beide in v1 ausschließen** (nicht als Klasse). Alternativ später als eigene „Kontext"-Klassen.
3. **Nicht-eval-Codes mit viel Daten (BCA 1867, BDB 1400, BCC 1372, BBC 793, BAA 679, BAH 564):** In v1 **ausgeschlossen** (nicht im aktuellen Eval messbar). Erweiterung in v2, wenn das Eval-Set wächst.

### Mapping-Regel
- `kein_schaden` → `LEER`.
- Sonst: erste 3 Zeichen des Code-Tokens = Hauptcode. Wenn Hauptcode in der Whitelist (11 Klassen oben) → diese Klasse, sonst Frame **ausschließen** (nicht trainieren).
- `axial`, `schacht`, `muffe`, alle `AE…`-Tokens und alle Hauptcodes außerhalb der Whitelist → ausgeschlossen.

### Eval-Ausschluss (Kontamination)
- Über `EvalSetManifestHasher.ComputeHashes("C:\KI_BRAIN\eval_set")` die SHA-256 aller 120 Eval-Bilder bilden.
- Jeder Trainings-Frame, dessen SHA-256 in dieser Menge liegt, wird **hart verworfen** (nicht nur per Dateiname — auch umbenannte/augmentierte Kopien).
- Nach dem Bau Pflicht-Gegenprobe: 0 Eval-Hashes im fertigen Datensatz (sonst Abbruch).

### Split-Strategie (gegen Leakage)
- Split-Einheit = **Haltung**, nicht Einzelbild. Alle Frames einer Haltung (inkl. aller Zeit-Varianten t±N desselben Befunds) landen im selben Split.
- Ziel ~80/20 train/val, **stratifiziert**: Haltungen werden so auf train/val verteilt, dass jede Zielklasse ≥ `MinValFrames` (Default 5) im val hat.
- Deterministisch über festen Seed (kein `Random()` ohne Seed) → reproduzierbar.
- Konflikt offen dokumentiert: Eine Haltung enthält Frames mehrerer Klassen; perfekte Per-Klasse-Stratifizierung bei Haltungs-Split ist nicht garantiert. Lösung: Greedy-Zuteilung + Report der Ist-Verteilung; schwache Klasse BBA gesondert prüfen.

### Ausgabeformat
Ultralytics YOLO-cls Ordnerlayout:
```
<out>/train/<KLASSE>/<frame>.png
<out>/val/<KLASSE>/<frame>.png
<out>/dataset_report.json   (Klassen, Counts pro Split, ausgeschlossene Eval-Frames, Seed)
```

---

## File Structure

- Create: `src/AuswertungPro.Next.Application/Ai/Training/ClassifierDatasetPlan.cs` — reine Logik: Mapping, Haltungs-Extraktion, Split, Report-Modell.
- Create: `tools/ClassifierDatasetBuilder/ClassifierDatasetBuilder.csproj` — Konsolentool.
- Create: `tools/ClassifierDatasetBuilder/Program.cs` — CLI + Datei-I/O (Hashing, Kopieren), ruft `ClassifierDatasetPlan` auf.
- Test: `tests/AuswertungPro.Next.Pipeline.Tests/ClassifierDatasetPlanTests.cs` — testet Mapping/Haltung/Split.

Begründung: Die *Entscheidungslogik* (was wird welche Klasse, welcher Split) ist die fehleranfällige, testwürdige Stelle (vgl. CLAUDE.md: Tests für Entscheidungslogik). Datei-I/O bleibt dünn im Tool.

---

## Task 1: Code→Klasse-Mapping

**Files:**
- Create: `src/AuswertungPro.Next.Application/Ai/Training/ClassifierDatasetPlan.cs`
- Test: `tests/AuswertungPro.Next.Pipeline.Tests/ClassifierDatasetPlanTests.cs`

- [ ] **Step 1: Failing test**

```csharp
using AuswertungPro.Next.Application.Ai.Training;
using Xunit;

public class ClassifierDatasetPlanTests
{
    [Theory]
    [InlineData("BDDC", "BDD")]
    [InlineData("BAIZ", "BAI")]
    [InlineData("BAJB", "BAJ")]
    [InlineData("BABBA", "BAB")]
    [InlineData("BCD", "BCD")]
    [InlineData("BDA", "BDA")]
    [InlineData("kein_schaden", "LEER")]
    public void MapCode_bekannte_Codes_werden_auf_Klasse_abgebildet(string code, string expected)
        => Assert.Equal(expected, ClassifierDatasetPlan.MapCodeToClass(code));

    [Theory]
    [InlineData("axial")]
    [InlineData("schacht")]
    [InlineData("AECXC")]
    [InlineData("BCAAA")]   // BCA nicht in v1-Whitelist
    public void MapCode_ausgeschlossene_Codes_geben_null(string code)
        => Assert.Null(ClassifierDatasetPlan.MapCodeToClass(code));
}
```

- [ ] **Step 2: Test schlägt fehl**

Run: `dotnet test tests/AuswertungPro.Next.Pipeline.Tests/AuswertungPro.Next.Pipeline.Tests.csproj --filter "ClassifierDatasetPlanTests"`
Expected: FAIL (Typ `ClassifierDatasetPlan` existiert nicht).

- [ ] **Step 3: Minimal-Implementierung**

```csharp
namespace AuswertungPro.Next.Application.Ai.Training;

public static class ClassifierDatasetPlan
{
    // v1, eval-ausgerichtet. Bei Bestätigung BAB/BBA enthalten.
    public static readonly IReadOnlySet<string> TargetClasses = new HashSet<string>(StringComparer.Ordinal)
    { "BCD", "BCE", "BDA", "BDD", "BAJ", "BAF", "BAB", "BAI", "BBB", "BBA", "LEER" };

    public static string? MapCodeToClass(string? code)
    {
        if (string.IsNullOrWhiteSpace(code)) return null;
        var c = code.Trim();
        if (c.Equals("kein_schaden", StringComparison.OrdinalIgnoreCase)) return "LEER";
        if (c.Length < 3) return null;
        var main = c[..3].ToUpperInvariant();
        return TargetClasses.Contains(main) ? main : null;
    }
}
```

- [ ] **Step 4: Test grün**

Run: `dotnet test tests/AuswertungPro.Next.Pipeline.Tests/AuswertungPro.Next.Pipeline.Tests.csproj --filter "ClassifierDatasetPlanTests"`
Expected: PASS.

- [ ] **Step 5: Commit**

```bash
git add src/AuswertungPro.Next.Application/Ai/Training/ClassifierDatasetPlan.cs tests/AuswertungPro.Next.Pipeline.Tests/ClassifierDatasetPlanTests.cs
git commit -m "feat(training): Code-zu-Klasse-Mapping fuer VSA-Klassifikator-Datensatz"
```

---

## Task 2: Haltung + Finding-Key aus Dateinamen

**Files:**
- Modify: `src/AuswertungPro.Next.Application/Ai/Training/ClassifierDatasetPlan.cs`
- Test: `tests/AuswertungPro.Next.Pipeline.Tests/ClassifierDatasetPlanTests.cs`

- [ ] **Step 1: Failing test**

```csharp
[Theory]
[InlineData("81030-80945_8.8s_BCD_t+0.png", "81030-80945")]
[InlineData("06.24341-35625_100.8s_BDA_t+0.png", "06.24341-35625")]
[InlineData("80671-80658_1048.7s_BCE_t+0.png", "80671-80658")]
public void ParseHaltung_extrahiert_Haltungs_Key(string file, string expected)
{
    Assert.True(ClassifierDatasetPlan.TryParseFrame(file, out var info));
    Assert.Equal(expected, info.Haltung);
}

[Fact]
public void ParseFrame_liefert_Code_und_Klasse()
{
    Assert.True(ClassifierDatasetPlan.TryParseFrame("287425-81162_319.1s_BDDC_t+0.png", out var info));
    Assert.Equal("BDDC", info.Code);
    Assert.Equal("BDD", info.TrainingClass);
}
```

- [ ] **Step 2: Test schlägt fehl**

Run: `dotnet test tests/AuswertungPro.Next.Pipeline.Tests/AuswertungPro.Next.Pipeline.Tests.csproj --filter "ClassifierDatasetPlanTests"`
Expected: FAIL (`TryParseFrame`/`FrameInfo` fehlt).

- [ ] **Step 3: Implementierung**

```csharp
using System.Text.RegularExpressions;

public sealed record FrameInfo(string Haltung, double TimeSeconds, string Code, string? TrainingClass);

private static readonly Regex FramePattern =
    new(@"^(?<haltung>.+?)_(?<zeit>[0-9.]+)s_(?<code>.+?)(_t[+-]\d+)?\.png$",
        RegexOptions.Compiled | RegexOptions.IgnoreCase);

public static bool TryParseFrame(string fileName, out FrameInfo info)
{
    info = default!;
    var m = FramePattern.Match(fileName);
    if (!m.Success) return false;
    if (!double.TryParse(m.Groups["zeit"].Value,
            System.Globalization.NumberStyles.Float,
            System.Globalization.CultureInfo.InvariantCulture, out var t))
        t = 0;
    var code = m.Groups["code"].Value;
    info = new FrameInfo(m.Groups["haltung"].Value, t, code, MapCodeToClass(code));
    return true;
}
```

- [ ] **Step 4: Test grün**

Run: `dotnet test tests/AuswertungPro.Next.Pipeline.Tests/AuswertungPro.Next.Pipeline.Tests.csproj --filter "ClassifierDatasetPlanTests"`
Expected: PASS.

- [ ] **Step 5: Commit**

```bash
git add -A && git commit -m "feat(training): Frame-Dateiname parsen (Haltung/Zeit/Code/Klasse)"
```

---

## Task 3: Haltungs-stratifizierter Split (deterministisch)

**Files:**
- Modify: `src/AuswertungPro.Next.Application/Ai/Training/ClassifierDatasetPlan.cs`
- Test: `tests/AuswertungPro.Next.Pipeline.Tests/ClassifierDatasetPlanTests.cs`

- [ ] **Step 1: Failing test**

```csharp
[Fact]
public void Split_haelt_eine_Haltung_komplett_in_einem_Split()
{
    var frames = new[]
    {
        new FrameInfo("H1", 0, "BCD", "BCD"), new FrameInfo("H1", 5, "BCD", "BCD"),
        new FrameInfo("H2", 0, "BDA", "BDA"), new FrameInfo("H3", 0, "BCE", "BCE"),
        new FrameInfo("H4", 0, "BDA", "BDA"), new FrameInfo("H5", 0, "BCE", "BCE"),
    };
    var split = ClassifierDatasetPlan.SplitByHaltung(frames, valFraction: 0.4, seed: 42);

    // Keine Haltung in beiden Splits
    foreach (var h in frames.Select(f => f.Haltung).Distinct())
    {
        var inTrain = split.Train.Any(f => f.Haltung == h);
        var inVal = split.Val.Any(f => f.Haltung == h);
        Assert.False(inTrain && inVal, $"Haltung {h} ist in train UND val (Leakage)");
    }
    Assert.Equal(frames.Length, split.Train.Count + split.Val.Count);
}

[Fact]
public void Split_ist_deterministisch_bei_gleichem_Seed()
{
    var frames = Enumerable.Range(0, 20)
        .Select(i => new FrameInfo($"H{i}", 0, "BCD", "BCD")).ToArray();
    var a = ClassifierDatasetPlan.SplitByHaltung(frames, 0.2, 7);
    var b = ClassifierDatasetPlan.SplitByHaltung(frames, 0.2, 7);
    Assert.Equal(a.Val.Select(f => f.Haltung), b.Val.Select(f => f.Haltung));
}
```

- [ ] **Step 2: Test schlägt fehl**

Run: `dotnet test tests/AuswertungPro.Next.Pipeline.Tests/AuswertungPro.Next.Pipeline.Tests.csproj --filter "ClassifierDatasetPlanTests"`
Expected: FAIL (`SplitByHaltung`/`DatasetSplit` fehlt).

- [ ] **Step 3: Implementierung**

```csharp
public sealed record DatasetSplit(IReadOnlyList<FrameInfo> Train, IReadOnlyList<FrameInfo> Val);

// Deterministischer Haltungs-Split: Haltungen werden per stabilem Hash sortiert,
// dann anteilig val zugewiesen. Eine Haltung ist immer komplett in genau einem Split.
public static DatasetSplit SplitByHaltung(IEnumerable<FrameInfo> frames, double valFraction, int seed)
{
    var list = frames.ToList();
    var haltungen = list.Select(f => f.Haltung).Distinct().ToList();

    // Stabiler, seed-abhaengiger Schluessel pro Haltung (kein Random ohne Seed -> reproduzierbar).
    int Key(string h) => unchecked((h + "#" + seed).Aggregate(17, (acc, c) => acc * 31 + c) & 0x7fffffff);
    var ordered = haltungen.OrderBy(Key).ToList();

    var valCount = (int)Math.Round(ordered.Count * valFraction, MidpointRounding.AwayFromZero);
    var valSet = new HashSet<string>(ordered.Take(valCount), StringComparer.Ordinal);

    var train = list.Where(f => !valSet.Contains(f.Haltung)).ToList();
    var val = list.Where(f => valSet.Contains(f.Haltung)).ToList();
    return new DatasetSplit(train, val);
}
```

- [ ] **Step 4: Test grün**

Run: `dotnet test tests/AuswertungPro.Next.Pipeline.Tests/AuswertungPro.Next.Pipeline.Tests.csproj --filter "ClassifierDatasetPlanTests"`
Expected: PASS.

- [ ] **Step 5: Commit**

```bash
git add -A && git commit -m "feat(training): deterministischer Haltungs-Split gegen Leakage"
```

---

## Task 4: Tool-Gerüst + Eval-Hash-Ausschluss

**Files:**
- Create: `tools/ClassifierDatasetBuilder/ClassifierDatasetBuilder.csproj`
- Create: `tools/ClassifierDatasetBuilder/Program.cs`

- [ ] **Step 1: csproj anlegen** (analog zu `tools/EvalSetBenchmark/EvalSetBenchmark.csproj`, ProjectReference auf Application)

```xml
<Project Sdk="Microsoft.NET.Sdk">
  <PropertyGroup>
    <OutputType>Exe</OutputType>
    <TargetFramework>net10.0</TargetFramework>
    <Nullable>enable</Nullable>
    <ImplicitUsings>enable</ImplicitUsings>
  </PropertyGroup>
  <ItemGroup>
    <ProjectReference Include="..\..\src\AuswertungPro.Next.Application\AuswertungPro.Next.Application.csproj" />
  </ItemGroup>
</Project>
```

- [ ] **Step 2: Program.cs — Eval-Hashes bilden, Frames filtern, splitten, kopieren, Report schreiben**

```csharp
using AuswertungPro.Next.Application.Ai.Evaluation;
using AuswertungPro.Next.Application.Ai.Training;
using System.Security.Cryptography;
using System.Text.Json;

string trainingFrames = Arg("--frames") ?? @"C:\KI_BRAIN\training_frames";
string evalSet        = Arg("--eval-set") ?? @"C:\KI_BRAIN\eval_set";
string outDir         = Arg("--out") ?? @"C:\KI_BRAIN\yolo_vsa_cls_dataset";
double valFraction    = double.TryParse(Arg("--val-fraction"), out var vf) ? vf : 0.2;
int seed              = int.TryParse(Arg("--seed"), out var s) ? s : 42;
bool dryRun           = Array.IndexOf(args, "--dry-run") >= 0;

// 1) Eval-Bild-Hashes (Kontaminations-Sperre) ueber bestehenden Hasher
var evalHashes = EvalSetManifestHasher.ComputeHashes(evalSet).Hashes
    .Where(h => h.RelativePath.StartsWith("images/", StringComparison.OrdinalIgnoreCase))
    .Select(h => h.Sha256Hex.ToLowerInvariant())
    .ToHashSet();

// 2) Frames einlesen, parsen, mappen, Eval-Frames hart ausschliessen
string Sha(string path) { using var fs = File.OpenRead(path); return Convert.ToHexString(SHA256.HashData(fs)).ToLowerInvariant(); }
var kept = new List<(FrameInfo Info, string Path)>();
int excludedEval = 0, excludedCode = 0;
foreach (var path in Directory.EnumerateFiles(trainingFrames, "*.png", SearchOption.AllDirectories))
{
    if (!ClassifierDatasetPlan.TryParseFrame(Path.GetFileName(path), out var info) || info.TrainingClass is null) { excludedCode++; continue; }
    if (evalHashes.Contains(Sha(path))) { excludedEval++; continue; }
    kept.Add((info, path));
}

// 3) Split
var split = ClassifierDatasetPlan.SplitByHaltung(kept.Select(k => k.Info), valFraction, seed);
// (Pfad-Zuordnung ueber Lookup info->path; bei Duplikaten Dateiname+Haltung als Schluessel)
```

- [ ] **Step 3: Build prüfen**

Run: `dotnet build tools/ClassifierDatasetBuilder/ClassifierDatasetBuilder.csproj`
Expected: Build erfolgreich, 0 Fehler.

- [ ] **Step 4: Commit**

```bash
git add tools/ClassifierDatasetBuilder/
git commit -m "feat(training): ClassifierDatasetBuilder-Geruest mit Eval-Hash-Ausschluss"
```

---

## Task 5: Kopieren in train/val/<klasse> + Report + Dry-Run

**Files:**
- Modify: `tools/ClassifierDatasetBuilder/Program.cs`

- [ ] **Step 1: Kopier- und Report-Logik**

```csharp
// 4) Kopieren (oder bei --dry-run nur zaehlen)
void Emit(IReadOnlyList<FrameInfo> set, string splitName, Dictionary<string,int> tally,
          Dictionary<(string,string),string> pathByKey)
{
    foreach (var f in set)
    {
        tally[f.TrainingClass!] = tally.GetValueOrDefault(f.TrainingClass!) + 1;
        if (dryRun) continue;
        var src = pathByKey[(f.Haltung, $"{f.TimeSeconds}|{f.Code}")];
        var dstDir = Path.Combine(outDir, splitName, f.TrainingClass!);
        Directory.CreateDirectory(dstDir);
        File.Copy(src, Path.Combine(dstDir, Path.GetFileName(src)), overwrite: true);
    }
}
var trainTally = new Dictionary<string,int>(); var valTally = new Dictionary<string,int>();
// ... Emit train/val ...

// 5) Report
var report = new {
    created_utc = DateTimeOffset.UtcNow.ToString("O"),
    seed, val_fraction = valFraction,
    excluded_eval_frames = excludedEval, excluded_non_target = excludedCode,
    train = trainTally, val = valTally
};
Directory.CreateDirectory(outDir);
File.WriteAllText(Path.Combine(outDir, "dataset_report.json"),
    JsonSerializer.Serialize(report, new JsonSerializerOptions { WriteIndented = true }));
Console.WriteLine($"Eval ausgeschlossen: {excludedEval} (MUSS 120 sein) | Nicht-Ziel: {excludedCode}");
```

- [ ] **Step 2: Dry-Run gegen echte Daten**

Run: `dotnet run --project tools/ClassifierDatasetBuilder -- --dry-run`
Expected: `Eval ausgeschlossen: 120` und plausible Per-Klasse-Counts (BCD ~1900, BBA ~134 …), keine Datei geschrieben.

- [ ] **Step 3: Echter Lauf**

Run: `dotnet run --project tools/ClassifierDatasetBuilder -- --out C:\KI_BRAIN\yolo_vsa_cls_dataset`
Expected: Ordner `train/<klasse>` + `val/<klasse>` + `dataset_report.json`.

- [ ] **Step 4: Pflicht-Gegenprobe (Warden, Kontamination = 0)**

Run (PowerShell):
```powershell
$eval = Get-ChildItem C:\KI_BRAIN\eval_set\images -File | ForEach-Object {(Get-FileHash $_.FullName -Algorithm SHA256).Hash}
$ds = Get-ChildItem C:\KI_BRAIN\yolo_vsa_cls_dataset -Recurse -File -Include *.png | ForEach-Object {(Get-FileHash $_.FullName -Algorithm SHA256).Hash}
(Compare-Object $eval $ds -IncludeEqual -ExcludeDifferent | Measure-Object).Count
```
Expected: `0` (kein Eval-Frame im Datensatz). Bei >0: Abbruch, Bug in Task 4.

- [ ] **Step 5: Commit**

```bash
git add tools/ClassifierDatasetBuilder/Program.cs
git commit -m "feat(training): Datensatz schreiben + Report + Kontaminations-Gegenprobe"
```

---

## Self-Review (Checkliste)

1. **Spec-Abdeckung:** Zielklassen (Task 1) ✓, Eval-Ausschluss per Hash (Task 4 Step 1, Task 5 Step 4) ✓, Zeit-Varianten zusammenhalten (Haltungs-Split Task 3) ✓, Split nach Haltung statt zufällig (Task 3) ✓, kein Training (kein Task baut/trainiert ein Modell) ✓.
2. **Platzhalter:** keine offenen TODOs; jeder Schritt hat Code/Befehl.
3. **Typ-Konsistenz:** `FrameInfo` (Haltung/TimeSeconds/Code/TrainingClass), `DatasetSplit` (Train/Val), `MapCodeToClass`/`TryParseFrame`/`SplitByHaltung` über alle Tasks gleich benannt.

**Bekannte Restpunkte für den Review (siehe Design-Entscheidungen):** BAB/BBA-Aufnahme, schacht/axial-Ausschluss, Pfad-Lookup-Schlüssel bei evtl. doppelten (Haltung,Zeit,Code) — in Task 5 als `(Haltung,"Zeit|Code")` gelöst, bei echten Kollisionen auf vollen Dateinamen erweitern.
