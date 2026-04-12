Perfekt! Jetzt habe ich alle Details. Lass mich einen sehr ausführlichen Plan erstellen:

---

# DETAILLIERTE ABLAUF-DOKUMENTATION DES SELBSTTRAINING-TOOLS

## Übersicht
Das Self-Training-Tool ist eine vollautomatisierte Pipeline, die PDF-Protokolle von Kanalinspektion ausliest, Fotos extrahiert, die KI blind analysiert und dann deterministisch mit der Ground-Truth (Protokoll) vergleicht. Die Ergebnisse werden als Trainingssamples in der Knowledge Base gespeichert.

**Kern-Dateien:**
1. `TrainingCenterViewModel.cs` - UI-Orchestrierung und Haupteinstieg
2. `SelfTrainingOrchestrator.cs` - Verarbeitungs-Logik (Multi-Modell-Pipeline)
3. `SelfTrainingComparisonService.cs` - Deterministischer Vergleich KI vs. Protokoll
4. `PdfProtocolExtractor.cs` - PDF-Parsing und Foto-Extraktion
5. `TrainingSamplesStore.cs` - Persistierung mit Atomare Writes
6. `SampleQualityGateService.cs` - Validierung der Trainingssamples
7. `EnhancedVisionAnalysisService.cs` - Qwen-Vision-Wrapper

---

## SCHRITT-FÜR-SCHRITT ABLAUF

### **SCHRITT 0: Konfiguration laden + Services instanziieren**

**Datei:** `TrainingCenterViewModel.cs` (Zeilen 2019-2050)

```csharp
var cfg = AiRuntimeConfig.Load();  // Ollama-Config (BaseUri, VisionModel)
Log($"Ollama: {cfg.OllamaBaseUri}, Modell: {cfg.VisionModel}");

var visionModel = cfg.VisionModel ?? "Qwen2.5-VL";
_activeVisionModel = visionModel;
var ollamaClient = cfg.CreateOllamaClient();

// Services instanziieren
var vision = new EnhancedVisionAnalysisService(ollamaClient, visionModel);
var comparison = new SelfTrainingComparisonService();
var technique = new TechniqueAssessmentService(ollamaClient, visionModel);
var pdfExtractor = new PdfProtocolExtractor();

// Multi-Modell-Pipeline (OPTIONAL: YOLO/DINO/SAM wenn Sidecar verfügbar)
Ai.Pipeline.SingleFrameMultiModelService? multiModel = null;
try
{
    var pipeCfg = Ai.PipelineConfig.Load();
    if (pipeCfg.MultiModelEnabled)
    {
        var sidecarHttp = new System.Net.Http.HttpClient
        {
            BaseAddress = pipeCfg.SidecarUrl,
            Timeout = TimeSpan.FromSeconds(pipeCfg.SidecarTimeoutSec)
        };
        var pipelineClient = new Ai.Pipeline.VisionPipelineClient(pipeCfg.SidecarUrl, sidecarHttp);
        multiModel = new Ai.Pipeline.SingleFrameMultiModelService(
            pipelineClient, pipeCfg.YoloConfidence, pipeCfg.DinoBoxThreshold, pipeCfg.DinoTextThreshold);
    }
}
catch { /* Sidecar nicht konfiguriert — nur Qwen */ }
```

**Input:** 
- `AiRuntimeConfig` (JSON-Datei mit Ollama-URL, Modell-Name)
- `PipelineConfig` (Optional: Sidecar-URL für YOLO/DINO/SAM)

**Output:**
- `vision` (EnhancedVisionAnalysisService) - Qwen-Analysen
- `multiModel` (Optional) - YOLO+DINO+SAM, nur wenn Sidecar läuft
- `comparison` (SelfTrainingComparisonService) - Vergleichslogik
- `technique` (TechniqueAssessmentService) - Qualitätsbewertung
- `pdfExtractor` (PdfProtocolExtractor) - PDF-Text + Foto-Extraktion

**Fehlerbehandlung:**
- Sidecar-Fehler: Fallback auf Qwen-only (try-catch ignoriert Fehler)
- Ollama nicht erreichbar: RuntimeException wird später abgefangen

---

### **SCHRITT 1: Fälle laden + Auto-Scan**

**Datei:** `TrainingCenterViewModel.cs` (Zeilen 1962-1995)

```csharp
// Auto-Scan: Wenn keine Fälle geladen, Ordner automatisch scannen
if (Cases.Count == 0 && _rootFolders.Count > 0)
{
    StatusText = "Scanne Ordner automatisch...";
    foreach (var folder in _rootFolders)
    {
        if (!Directory.Exists(folder)) continue;
        var found = await _import.ScanAsync(folder);  // TrainingCenterImportService
        foreach (var c in found)
            Cases.Add(c);
    }
}

// Auto-Auswahl: Erste unverarbeitete Haltung
if (SelectedCase is null)
{
    var existingSamples = await TrainingSamplesStore.LoadAsync();
    var processedIds = existingSamples.Select(s => s.CaseId)
        .ToHashSet(StringComparer.OrdinalIgnoreCase);

    var firstUnprocessed = Cases.FirstOrDefault(c =>
        !string.IsNullOrEmpty(c.ProtocolPath) && !processedIds.Contains(c.CaseId));

    if (firstUnprocessed is null)
    {
        StatusText = "Alle Fälle bereits verarbeitet oder keine mit Protokoll vorhanden.";
        return;
    }
    SelectedCase = firstUnprocessed;
}
```

**Input:**
- `_rootFolders` (Liste von Ordnerpfaden)
- `TrainingSamplesStore.LoadAsync()` - Bereits verarbeitete Fälle

**Output:**
- `Cases` (ObservableCollection<TrainingCase>)
- `SelectedCase` (TrainingCase mit ProtocolPath)

**Fehlerbehandlung:**
- Folder nicht existent: `!Directory.Exists()` prüft vor Scan
- Keine unverarbeiteten Fälle: Status-Nachricht, Return

---

### **SCHRITT 2: Batch-Loop - PDF-Fotos vorab extrahieren**

**Datei:** `TrainingCenterViewModel.cs` (Zeilen 2074-2091)

```csharp
// PDF-Fotos für ALLE Fälle vorab extrahieren (CPU-parallel, blockiert GPU nicht)
Log("PDF-Fotos vorab extrahieren...");
await Parallel.ForEachAsync(casesToTrain,
    new ParallelOptions { MaxDegreeOfParallelism = 4, CancellationToken = ct },
    async (c, token) =>
{
    if (string.IsNullOrEmpty(c.ProtocolPath)) return;
    var framesDir = Path.Combine(c.FolderPath, "self_training_frames");
    if (Directory.Exists(framesDir) && Directory.GetFiles(framesDir, "*.png").Length > 0) return;
    
    try
    {
        var extractor = new PdfProtocolExtractor();
        await extractor.ExtractAsync(c.ProtocolPath, framesDir, token);
    }
    catch { /* Fehler beim Vorextrahieren ignorieren — RunAsync versucht es nochmal */ }
});
```

**Input:**
- `casesToTrain` (List<TrainingCase>)
- `c.ProtocolPath` (Pfad zur PDF-Datei)

**Output:**
- `self_training_frames/` Ordner mit PNG-Dateien pro Fall

**Parallelisierung:**
- `MaxDegreeOfParallelism = 4` (CPU-Arbeit, nicht GPU-limitiert)
- Gibt sofort zurück wenn PNG-Dateien bereits vorhanden sind (Cache-Hit)

**Fehlerbehandlung:**
- Fehler ignoriert - wird später in `RunAsync` erneut versucht

---

### **SCHRITT 3: Für jeden Fall - RunAsync() starten**

**Datei:** `TrainingCenterViewModel.cs` (Zeilen 2097-2170)

```csharp
for (int ci = 0; ci < casesToTrain.Count; ci++)
{
    ct.ThrowIfCancellationRequested();
    var currentCase = casesToTrain[ci];
    SelectedCase = currentCase;
    ProgressValue = ci + 1;
    StatusText = $"[{ci + 1}/{casesToTrain.Count}] {currentCase.CaseId}...";
    
    SelfTrainingResult result;
    try
    {
        var progress = new Progress<SelfTrainingStep>(OnSelfTrainingStep);
        result = await _selfTrainingOrchestrator.RunAsync(currentCase, progress, ct);
    }
    catch (OperationCanceledException)
    {
        Log("Selbsttraining abgebrochen.");
        break;
    }
    catch (Exception ex)
    {
        Log($"FEHLER bei Fall {currentCase.CaseId}: {ex.Message}");
        caseErrors++;
        continue;
    }
    
    // Statistiken sammeln
    totalExact += result.ExactMatches;
    totalPartial += result.PartialMatches;
    totalMismatch += result.Mismatches;
    totalNoFindings += result.NoFindings;
    totalSamples += result.SamplesGenerated;
}
```

**Input:**
- `currentCase` (TrainingCase)
- `progress` Callback (für UI-Updates)

**Output:**
- `SelfTrainingResult` mit Statistiken

**Fehlerbehandlung:**
- `OperationCanceledException`: break (Benutzer hat gestoppt)
- Andere Exceptions: continue mit Fehler-Log (nicht abbrechen)

---

### **SCHRITT 4: Orchestrator.RunAsync() - Protokoll parsen und Einträge filtern**

**Datei:** `SelfTrainingOrchestrator.cs` (Zeilen 90-140)

```csharp
public async Task<SelfTrainingResult> RunAsync(
    TrainingCase tc,
    IProgress<SelfTrainingStep> progress,
    CancellationToken ct)
{
    var sw = Stopwatch.StartNew();

    // 0. Sidecar-Verfügbarkeit prüfen (YOLO/DINO/SAM)
    _sidecarAvailable = false;
    if (_multiModel is not null)
    {
        try
        {
            var cfg = PipelineConfig.Load();
            using var http = new System.Net.Http.HttpClient { Timeout = TimeSpan.FromSeconds(5) };
            var resp = await http.GetAsync(new Uri(cfg.SidecarUrl, "/health"), ct);
            _sidecarAvailable = resp.IsSuccessStatusCode;
        }
        catch { /* Sidecar nicht erreichbar */ }

        progress.Report(new SelfTrainingStep(
            0, 1, "", 0, SelfTrainingStage.BuildingTimeline, null, null, null,
            _sidecarAvailable
                ? "Multi-Modell aktiv: YOLO + DINO + SAM + Qwen"
                : "Sidecar nicht erreichbar — Fallback: nur Qwen"));
    }

    // 1. Protokoll-Einträge MIT Fotos extrahieren
    string framesDir = Path.Combine(tc.FolderPath, "self_training_frames");
    Directory.CreateDirectory(framesDir);

    progress.Report(new SelfTrainingStep(
        0, 1, "", 0, SelfTrainingStage.BuildingTimeline, null, null, null,
        "PDF-Protokoll wird gelesen..."));

    var allEntries = await _pdfExtractor.ExtractAsync(tc.ProtocolPath, framesDir, ct);

    progress.Report(new SelfTrainingStep(
        0, allEntries.Count, "", 0, SelfTrainingStage.BuildingTimeline, null, null, null,
        $"{allEntries.Count} Protokoll-Einträge gefunden"));

    // Nur Einträge mit Foto behalten — das sind unsere Trainingsbilder
    var entries = allEntries
        .Where(e => !string.IsNullOrEmpty(e.ExtractedFramePath)
                    && File.Exists(e.ExtractedFramePath))
        .ToList();

    if (entries.Count == 0)
    {
        return new SelfTrainingResult(tc.CaseId, allEntries.Count, 0, 0, 0, 0, null, sw.Elapsed, 0);
    }
```

**Input:**
- `tc.ProtocolPath` (PDF-Datei)
- `framesDir` (Zielordner für Fotos)

**Output:**
- `entries` (List<GroundTruthEntry>) - nur Einträge mit ExtractedFramePath

**Sidecar-Check:**
- HTTP GET `/health` zum Sidecar
- Falls erfolgreich: `_sidecarAvailable = true` (Multi-Modell aktiviert)
- Falls Fehler: `_sidecarAvailable = false` (nur Qwen)

**Fehlerbehandlung:**
- Keine Einträge mit Fotos: Return mit 0 Samples
- Alle Fehler werden geloggt, nicht re-thrown

---

### **SCHRITT 5: PDF-Extraktion (PdfProtocolExtractor.ExtractAsync)**

**Datei:** `PdfProtocolExtractor.cs` (Zeilen 88-207)

#### 5a: Text-Extraktion aus PDF

```csharp
public Task<IReadOnlyList<GroundTruthEntry>> ExtractAsync(
    string filePath, string? framesDir = null, CancellationToken ct = default)
{
    if (!File.Exists(filePath))
        return Task.FromResult<IReadOnlyList<GroundTruthEntry>>(Array.Empty<GroundTruthEntry>());

    var ext = Path.GetExtension(filePath).ToLowerInvariant();

    return ext switch
    {
        ".json" => Task.FromResult(ExtractFromJson(filePath)),
        ".pdf"  => Task.FromResult(ExtractFromPdf(filePath, framesDir)),
        _       => Task.FromResult<IReadOnlyList<GroundTruthEntry>>(Array.Empty<GroundTruthEntry>())
    };
}

private static IReadOnlyList<GroundTruthEntry> ExtractFromPdf(string path, string? framesDir)
{
    // Filter: Rechnungen, Offerten, Lieferscheine etc. überspringen
    var fileName = Path.GetFileNameWithoutExtension(path).ToLowerInvariant();
    if (NonProtocolKeywords.Any(kw => fileName.Contains(kw)))
        return Array.Empty<GroundTruthEntry>();

    using var doc = UglyToad.PdfPig.PdfDocument.Open(path);

    var text = ExtractTextFromPdfDoc(doc);
    if (string.IsNullOrWhiteSpace(text))
        return Array.Empty<GroundTruthEntry>();

    var entries = ParseEntriesFromText(text);

    // Fotos aus PDF-Bildbericht extrahieren und Einträgen zuordnen
    if (entries.Count > 0 && !string.IsNullOrWhiteSpace(framesDir))
    {
        entries = ExtractAndAssignPdfImages(doc, entries, path, framesDir);
    }

    return entries;
}
```

**Input:**
- `filePath` (PDF- oder JSON-Datei)
- `framesDir` (Ordner für extrahierte Fotos)

**Output:**
- `List<GroundTruthEntry>` mit Einträgen + `ExtractedFramePath`

**Text-Extraktion:**
- PdfPig: Letter-by-Letter-Extraktion mit Spacing (Zeilen 215-272)
- Font-Encoding-Korrektur: Custom-Font-Shift-Dekodierung (Zeilen 282-316)

#### 5b: Text-Parsing - Mehrere Regex-Strategien

```csharp
private static IReadOnlyList<GroundTruthEntry> ParseEntriesFromText(string text)
{
    // Strategie 1: IKAS Leitungsgrafik (Zeit VOR Beschreibung)
    // Format: "0.00  BCD  [1777]  00:00:09  Rohranfang"
    foreach (Match m in IkasTablePattern.Matches(text))
    {
        var entry = BuildEntry(...);
        if (entry is not null && seen.Add(Sig(entry)))
            results.Add(entry);
    }

    // Strategie 2: Fretz-Format (Foto + Zeit VOR Meter)
    // Format: "040  00:00:16  0.00  BCD  Rohranfang"
    foreach (Match m in FretzTablePattern.Matches(text))
    {
        var entry = BuildEntry(...);
        if (entry is not null && seen.Add(Sig(entry)))
            results.Add(entry);
    }

    // Strategie 3: Standard-Tabelle (Zeit NACH Text)
    // Format: "0.00  BCD  Rohranfang  00:00:09"
    foreach (Match m in TableRowPattern.Matches(text))
    {
        var entry = BuildEntry(...);
        if (entry is not null && seen.Add(Sig(entry)))
            results.Add(entry);
    }

    // Strategie 4: IKAS Bildbericht (Label-Value Blöcke)
    // Zustand / Entf. / Video
    foreach (Match codeMatch in BildberichtCodePattern.Matches(text))
    {
        var code = codeMatch.Groups["code"].Value;
        var meterMatch = FindFollowingMeterMatch(...);
        // ...
    }

    // Strategie 5: Fallback (Bereichsmuster / Einzelmeter)
    // "@12.45 BAB Riss..." oder "12.45m-13.50m BAB Riss..."
    foreach (Match m in EntryPattern.Matches(text))
    {
        var entry = BuildEntry(...);
        // ...
    }

    return results;
}
```

**Regex-Muster:**
| Muster | Format | Priorät |
|--------|--------|---------|
| `IkasTablePattern` | `meter code foto time text` | 1 |
| `FretzTablePattern` | `foto time meter code text` | 2 |
| `TableRowPattern` | `meter code text time` | 3 |
| `BildberichtCodePattern` | `Zustand CODE\nEntf. METER\nVideo TIME` | 4 |
| `EntryPattern` | `@meter1-meter2 CODE text` oder `@meter CODE text` | 5 |

**Code-Normalisierung:**

```csharp
private static string? NormalizeVsaCode(string code)
{
    var upper = code.ToUpperInvariant();
    return upper switch
    {
        // Bestandsaufnahme
        "BEGINN" or "ROHRANFANG" or "ANFANG"      => "BCD",  // Rohranfang
        "ENDE" or "ROHRENDE"                       => "BCE",  // Rohrende
        "BOGEN" or "KURVE" or "RICHTUNGSWECHSEL"   => "BCC",  // Bogen
        "ANSCHLUSS" or "ABZWEIG" or "STUTZEN"      => "BCA",  // Anschluss

        // Nicht trainingsrelevant (skip = null)
        "LAGE" or "ORT" or "IN" or "INSPEKTION"    => null,
        "NEUE" or "NEUEROHR" or "TEXT" or "FOTO"   => null,
        "ROHR" or "MATERIAL" or "PROFIL" or "DN"   => null,
        "SCHACHT" or "WETTER" or "DATUM" or "ZEIT" => null,
        // ... weitere Metadaten-Codes ...

        // Bereits VSA-Code (beginnt mit B + A-D)
        _ when upper.Length >= 2 && upper[0] == 'B' && upper[1] is >= 'A' and <= 'D'
            => upper,

        // AE-Codes (Profilwechsel) durchlassen
        _ when upper.StartsWith("AE", StringComparison.Ordinal) => upper,

        // Unbekannter Code → Reverse-Lookup
        _ => VsaCodeTree.ReverseLookup(upper) ?? VsaCodeTree.ReverseLookup(code)
    };
}
```

**Deduplication:**
- `HashSet<string> seen` mit Signatur (Code|Meter|MeterEnd)
- Duplikate werden übersprungen

#### 5c: Foto-Extraktion und Zuordnung

**Datei:** `PdfProtocolExtractor.cs` (Zeilen 394-467)

```csharp
private static IReadOnlyList<GroundTruthEntry> ExtractAndAssignPdfImages(
    UglyToad.PdfPig.PdfDocument doc,
    IReadOnlyList<GroundTruthEntry> entries,
    string pdfPath,
    string framesDir)
{
    Directory.CreateDirectory(framesDir);

    // ── PyMuPDF-Extraktion (korrekte CMYK→RGB Konvertierung) ──
    var imagePaths = ExtractImagesViaPyMuPdf(pdfPath, framesDir);

    // Fallback: PdfPig-Extraktion wenn PyMuPDF fehlschlägt
    if (imagePaths.Count == 0)
        imagePaths = ExtractImagesViaPdfPig(doc, pdfPath, framesDir);

    // Logos/Symbole filtern
    imagePaths = imagePaths
        .Where(p => !IsLikelyLogoOrSymbol(File.ReadAllBytes(p), Path.GetExtension(p)))
        .ToList();

    if (imagePaths.Count == 0)
        return entries;

    // Zuordnung: Bilder den Entries zuweisen (1:1 nach Index)
    int assignable = Math.Min(imagePaths.Count, entries.Count);
    double coverageRatio = entries.Count > 0 ? (double)assignable / entries.Count : 0;
    if (coverageRatio < 0.30 && Math.Abs(imagePaths.Count - entries.Count) > 3)
    {
        // Zu viele fehlende Fotos: keine Zuordnung
        return entries;
    }

    var result = new List<GroundTruthEntry>(entries.Count);
    for (int i = 0; i < entries.Count; i++)
    {
        var entry = entries[i];
        string? framePath = null;

        if (i < imagePaths.Count)
        {
            var srcPath = imagePaths[i];
            var targetName = $"{safeName}_{entry.VsaCode}_{entry.MeterStart:F1}m_{i}.png";
            var targetPath = Path.Combine(framesDir, targetName);
            try
            {
                if (srcPath != targetPath)
                {
                    if (File.Exists(targetPath)) File.Delete(targetPath);
                    File.Move(srcPath, targetPath);
                }
                framePath = targetPath;
            }
            catch
            {
                framePath = File.Exists(srcPath) ? srcPath : null;
            }
        }

        result.Add(entry with { ExtractedFramePath = framePath });
    }

    return result;
}
```

**PyMuPDF-Extraktion:**

```csharp
private static IReadOnlyList<string> ExtractImagesViaPyMuPdf(string pdfPath, string framesDir)
{
    try
    {
        var scriptPath = GetPyMuPdfScriptPath();
        if (!File.Exists(scriptPath))
            return Array.Empty<string>();

        var psi = new System.Diagnostics.ProcessStartInfo
        {
            FileName = "python",
            Arguments = $"\"{scriptPath}\" \"{pdfPath}\" \"{framesDir}\" {MinPhotoWidth} {MinPhotoHeight}",
            UseShellExecute = false,
            RedirectStandardOutput = true,
            CreateNoWindow = true
        };

        using var p = System.Diagnostics.Process.Start(psi);
        if (p == null) return Array.Empty<string>();

        var output = p.StandardOutput.ReadToEnd();
        p.WaitForExit(30_000); // Max 30 Sekunden

        if (p.ExitCode != 0 || string.IsNullOrWhiteSpace(output))
            return Array.Empty<string>();

        // JSON parsen: [{"page": 1, "index": 0, "path": "...", "width": 788, "height": 576}, ...]
        using var jsonDoc = System.Text.Json.JsonDocument.Parse(output);
        
        var paths = new List<string>();
        foreach (var item in jsonDoc.RootElement.EnumerateArray())
        {
            var path = item.GetProperty("path").GetString();
            if (path != null && File.Exists(path))
                paths.Add(path);
        }

        return paths;
    }
    catch
    {
        return Array.Empty<string>();
    }
}
```

**Python-Script (extract_pdf_images.py):**

**Datei:** `extract_pdf_images.py` (Zeilen 71-133)

```python
def extract_images(pdf_path: str, output_dir: str, min_w: int = 400, min_h: int = 300) -> list:
    """Extrahiert Kanalfotos aus einem PDF (filtert Haltungsgrafiken/Diagramme)."""
    os.makedirs(output_dir, exist_ok=True)
    doc = fitz.open(pdf_path)
    safe_name = re.sub(r'[^\w\-]', '_', os.path.splitext(os.path.basename(pdf_path))[0])

    results = []
    seen_sizes = set()  # Deduplizierung: Logos wiederholen sich

    for page_num in range(doc.page_count):
        page = doc[page_num]
        images = page.get_images()

        for img_idx, img in enumerate(images):
            xref = img[0]
            try:
                base = doc.extract_image(xref)
            except Exception:
                continue

            w, h = base['width'], base['height']

            # Dimensionsfilter
            if w < min_w or h < min_h: continue
            if w > 5000 or h > 5000: continue

            # Seitenverhältnis: Kanalfotos sind querformat (~4:3 oder 16:9)
            # Haltungsgrafiken sind oft hochformat (aspect < 0.8)
            aspect = w / h
            if aspect < 0.8 or aspect > 3.0: continue

            # Deduplizierung: gleiche Byte-Länge = wahrscheinlich Duplikat
            img_size = len(base['image'])
            if img_size in seen_sizes: continue
            seen_sizes.add(img_size)

            # Korrekte Farbraum-Konvertierung (CMYK → RGB)
            pix = fitz.Pixmap(doc, xref)
            if pix.n - pix.alpha > 3:  # CMYK
                pix = fitz.Pixmap(fitz.csRGB, pix)

            # Foto-Filter: helle Bilder (Grafiken/Diagramme) ausschließen
            if not is_likely_photo(pix):
                continue

            out_name = f"{safe_name}_p{page_num + 1}_{img_idx}.png"
            out_path = os.path.join(output_dir, out_name)
            pix.save(out_path)

            results.append({
                "page": page_num + 1,
                "index": img_idx,
                "path": out_path,
                "width": pix.width,
                "height": pix.height
            })

    doc.close()
    return results

def is_likely_photo(pix) -> bool:
    """Prueft ob ein Bild ein Kanalfoto ist (dunkel, farbig) vs. Grafik (hell, wenig Farben)."""
    # Sample: jeden 10. Pixel
    samples = pix.samples
    n_channels = pix.n
    step = max(1, len(samples) // (n_channels * 500)) * n_channels  # ~500 Samples

    total_r, total_g, total_b = 0, 0, 0
    count = 0

    for i in range(0, len(samples) - n_channels + 1, step):
        if n_channels >= 3:
            r, g, b = samples[i], samples[i + 1], samples[i + 2]
        else:
            r = g = b = samples[i]
        total_r += r
        total_g += g
        total_b += b
        count += 1

    if count == 0:
        return True

    avg_r = total_r / count
    avg_g = total_g / count
    avg_b = total_b / count
    avg_lum = (avg_r * 299 + avg_g * 587 + avg_b * 114) / 1000

    # Haltungsgrafiken: weißer Hintergrund (avgLum > 200)
    if avg_lum > 200:
        return False

    # Sehr helle Bilder mit wenig Farbvarianz = Diagramm
    color_range = max(avg_r, avg_g, avg_b) - min(avg_r, avg_g, avg_b)
    if avg_lum > 180 and color_range < 15:
        return False

    return True
```

**Input:** `pdf_path`, `output_dir`, `min_w=400`, `min_h=300`

**Output:** JSON mit Foto-Pfaden
```json
[
  {"page": 1, "index": 0, "path": "...", "width": 788, "height": 576},
  {"page": 1, "index": 1, "path": "...", "width": 794, "height": 580}
]
```

**Filter-Kriterien:**
| Kriterium | Wert | Zweck |
|-----------|------|--------|
| Min. Größe | 400×300px | PAL-Video-Mindestauflösung |
| Max. Größe | 5000×5000px | PDF-Seitenrender ausschließen |
| Seitenverhältnis | 0.8–3.0 | Kanalfotos: 4:3 (1.33) oder 16:9 (1.78) |
| Farbraum | CMYK→RGB | WinCan/IKAS PDFs haben CMYK-JPEGs |
| Helligkeit (is_likely_photo) | avgLum < 200 | Haltungsgrafiken haben weißen Hintergrund |
| Farbvarianz | color_range > 15 | Echte Fotos: viele Farben; Logos: wenig |
| Deduplizierung | Byte-Länge | Logos wiederholen sich auf jeder Seite |

**Zuordnung (1:1-Match):**
- Wenn `imagePaths.Count` == `entries.Count`: 1:1-Zuordnung
- Wenn Coverage-Ratio < 30%: keine Zuordnung (zu unsicher)
- Wenn Differenz > 3: keine Zuordnung

---

### **SCHRITT 6: Aufnahmetechnik bewerten (einmalig)**

**Datei:** `SelfTrainingOrchestrator.cs` (Zeilen 142-166)

```csharp
// 2. Aufnahmetechnik EINMAL mit dem ersten Frame bewerten (Qwen-basiert)
TechniqueAssessment? overallTechnique = null;
if (entries.Count > 0)
{
    var firstEntry = entries[0];
    var firstPath = firstEntry.ExtractedFramePath!;
    try
    {
        var firstBytes = await File.ReadAllBytesAsync(firstPath, ct);
        var firstB64 = Convert.ToBase64String(firstBytes);
        var firstAnalysis = await _vision.AnalyzeAsync(firstB64, ct);
        overallTechnique = await _technique.AssessFrameWithVisionAsync(
            firstBytes, firstAnalysis.Meter, firstEntry.MeterStart, ct);
    }
    catch
    {
        // Fallback: deterministisch ohne Qwen
        try
        {
            var firstBytes = await File.ReadAllBytesAsync(firstPath, ct);
            overallTechnique = _technique.AssessFrame(firstBytes, 0, firstEntry.MeterStart);
        }
        catch { /* Technik-Bewertung nicht möglich */ }
    }
}
```

**Input:**
- `entries[0]` (First GroundTruthEntry)
- Frame-Bytes

**Output:**
- `TechniqueAssessment` (OsdReadable, OsdDeltaMeters, LightingQuality, SharpnessQuality, OverallGrade)

**Methoden:**
1. **Qwen-basiert** (mit Qwen-Vision): AssessFrameWithVisionAsync
   - Qwen analysiert Beleuchtung, Schärfe, Zentrierung
   - OSD-Meter wird gelesen (falls sichtbar)
   
2. **Deterministisch** (ohne Qwen): AssessFrame
   - Luminance und Laplace-Varianz (Schärfe-Maß)
   - Keine OSD-Extraktion

---

### **SCHRITT 7: Parallele Verarbeitung - Frame analysieren und vergleichen**

**Datei:** `SelfTrainingOrchestrator.cs` (Zeilen 168-307)

```csharp
// 3. Alle Entries PARALLEL verarbeiten (GPU-Concurrency konfigurierbar)
int exactMatches = 0, partialMatches = 0, mismatches = 0, noFindings = 0;
var generatedSamples = new System.Collections.Concurrent.ConcurrentBag<TrainingSample>();
int gpuConcurrency = _gpuConcurrency;
int completedCount = 0;

progress.Report(new SelfTrainingStep(
    0, entries.Count, "", 0, SelfTrainingStage.Analyzing, null, null, null,
    $"Parallele KI-Analyse: {gpuConcurrency} gleichzeitige Requests..."));

await Parallel.ForEachAsync(
    entries.Select((e, i) => (Entry: e, Index: i)),
    new ParallelOptions { MaxDegreeOfParallelism = gpuConcurrency, CancellationToken = ct },
    async (item, token) =>
{
    _pauseGate.Wait(token);  // Pause-Handling
    var (entry, i) = item;
    string framePath = entry.ExtractedFramePath!;

    // ── Foto laden ──
    progress.Report(new SelfTrainingStep(
        i, entries.Count, entry.VsaCode, entry.MeterStart,
        SelfTrainingStage.ExtractingFrame, null, null, framePath));

    byte[] pngBytes;
    try
    {
        pngBytes = await File.ReadAllBytesAsync(framePath, token);
    }
    catch
    {
        return; // Skip bei Fehler
    }

    // ── Blinde KI-Analyse (weiss NICHTS vom Protokoll) ──
    progress.Report(new SelfTrainingStep(
        i, entries.Count, entry.VsaCode, entry.MeterStart,
        SelfTrainingStage.Analyzing, null, null, framePath));

    string b64 = Convert.ToBase64String(pngBytes);
    bool isPdfPhoto = framePath.Contains("self_training_frames", StringComparison.OrdinalIgnoreCase);

    EnhancedFrameAnalysis analysis;
    try
    {
        analysis = await AnalyzeFrameAsync(pngBytes, b64, token);
    }
    catch (Exception ex) when (ex is not OperationCanceledException)
    {
        var errMsg = $"[SelfTraining] EXCEPTION bei {entry.VsaCode}@{entry.MeterStart:F1}m: {ex.GetType().Name}: {ex.Message}";
        _logger?.LogWarning(ex, "Selbsttraining KI-Analyse fehlgeschlagen");
        LogToFile(errMsg);
        progress.Report(new SelfTrainingStep(
            i, entries.Count, entry.VsaCode, entry.MeterStart,
            SelfTrainingStage.Analyzing, null, null, framePath,
            ErrorMessage: errMsg));
        analysis = EnhancedFrameAnalysis.Empty(ex.Message);
    }

    if (analysis.Error is not null)
    {
        _logger?.LogWarning("Selbsttraining KI-Fehler: {Error}", analysis.Error);
        LogToFile($"[SelfTraining] {entry.VsaCode}@{entry.MeterStart:F1}m: {analysis.Error}");
    }

    // ── Deterministischer Vergleich ──
    var comparison = _comparison.Compare(entry, analysis, isPdfPhoto: isPdfPhoto);

    // ── Aufnahmetechnik (deterministisch) ──
    var technique = _technique.AssessFrame(pngBytes, analysis.Meter, entry.MeterStart);

    // ── Thread-safe Zähler ──
    switch (comparison.Level)
    {
        case MatchLevel.ExactMatch: Interlocked.Increment(ref exactMatches); break;
        case MatchLevel.PartialMatch: Interlocked.Increment(ref partialMatches); break;
        case MatchLevel.Mismatch: Interlocked.Increment(ref mismatches); break;
        case MatchLevel.NoFindings: Interlocked.Increment(ref noFindings); break;
    }

    // ── TrainingSample erzeugen ──
    var meterCenter = (entry.MeterStart + entry.MeterEnd) / 2.0;
    var sample = new TrainingSample
    {
        SampleId = $"{tc.CaseId}_st_{i:D3}",
        CaseId = tc.CaseId,
        Code = entry.VsaCode,
        Beschreibung = entry.Text,
        MeterStart = entry.MeterStart,
        MeterEnd = entry.MeterEnd,
        IsStreckenschaden = entry.IsStreckenschaden,
        TimeSeconds = 0,
        DetectedMeter = analysis.Meter,
        MeterSource = "Protokoll",
        FramePath = framePath,
        // ExactMatch + PartialMatch → Approved (Protokoll-Code ist Ground Truth,
        // auch wenn Meter/Clock daneben liegen ist das Sample wertvoll für KB)
        Status = comparison.Level is MatchLevel.ExactMatch or MatchLevel.PartialMatch
            ? TrainingSampleStatus.Approved
            : TrainingSampleStatus.New,
        KbIndexState = comparison.Level is MatchLevel.ExactMatch or MatchLevel.PartialMatch
            ? KbIndexState.Pending
            : KbIndexState.None,
        TruthMeterCenter = meterCenter,
        OdsDeltaMeters = technique?.OsdDeltaMeters,
        HasOsdMismatch = technique?.OsdDeltaMeters > 5.0,
        Signature = TrainingSample.BuildCanonicalSignature(tc.CaseId, entry.VsaCode, meterCenter, entry.MeterEnd),
        MatchLevel = comparison.Level.ToString(),
        KiCode = comparison.BestMatchCode,
        SourceType = SourceTypeNames.PdfPhoto,
        TechniqueGrade = technique?.OverallGrade
    };
    generatedSamples.Add(sample);

    // ── Few-Shot: ExactMatch-Samples als Trainingsbeispiele speichern ──
    if (comparison.Level == MatchLevel.ExactMatch
        && pngBytes.Length > 10_000
        && !_basicStructureCodes.Contains(entry.VsaCode.Replace(".", "").ToUpperInvariant()[..Math.Min(3, entry.VsaCode.Length)]))
    {
        try
        {
            var clock = entry.ClockPosition;
            await _fewShotStore.AddExampleAsync(
                pngBytes, ".png", entry.VsaCode, entry.Text,
                clock, entry.MeterStart, null, null,
                $"selftraining:{tc.CaseId}", 0.85, token);
        }
        catch { /* Few-Shot ist optional */ }
    }

    // ── Fortschritt melden ──
    var done = Interlocked.Increment(ref completedCount);
    progress.Report(new SelfTrainingStep(
        done - 1, entries.Count, entry.VsaCode, entry.MeterStart,
        SelfTrainingStage.Completed, comparison, technique, framePath));
});
```

**Parallelisierung:**
- `MaxDegreeOfParallelism = gpuConcurrency` (z.B. 2 oder 4)
- `Interlocked.Increment()` für thread-safe Zähler
- `ConcurrentBag<TrainingSample>` für Lock-freie Sammlung

**Input pro Frame:**
- `pngBytes` (Bilddaten)
- `b64` (Base64-kodiert für Qwen)
- `isPdfPhoto` (Flag für tolerantere Toleranzen)

**Output pro Frame:**
- `TrainingSample` mit Code, Beschreibung, Frame, Match-Level
- Zähler (exactMatches, partialMatches, etc.)

---

### **SCHRITT 8: KI-Analyse (AnalyzeFrameAsync)**

**Datei:** `SelfTrainingOrchestrator.cs` (Zeilen 344-383)

```csharp
private async Task<EnhancedFrameAnalysis> AnalyzeFrameAsync(
    byte[] pngBytes, string b64, CancellationToken ct)
{
    // Multi-Modell: YOLO → DINO → SAM → Qwen (mit Kontext)
    if (_sidecarAvailable && _multiModel is not null)
    {
        try
        {
            var result = await _multiModel.AnalyzeFrameAsync(pngBytes, _pipeDiameterMm, null, ct);
            if (result.Error is null && result.IsRelevant)
            {
                // Konvertiere SingleFrameResult → MultiModelFrameResult für Qwen
                var context = new MultiModelFrameResult(
                    TimestampSec: 0,
                    Meter: null,
                    IsRelevant: true,
                    DinoDetections: result.DinoDetections ?? Array.Empty<DinoDetectionDto>(),
                    SamMasks: result.SamResponse?.Masks ?? Array.Empty<SamMaskResult>(),
                    ImageWidth: result.SamResponse?.ImageWidth ?? 0,
                    ImageHeight: result.SamResponse?.ImageHeight ?? 0,
                    YoloTimeMs: result.YoloTimeMs,
                    DinoTimeMs: result.DinoTimeMs,
                    SamTimeMs: result.SamTimeMs);

                return await _vision.AnalyzeWithContextAsync(b64, context, _pipeDiameterMm, ct);
            }

            // YOLO sagt "nicht relevant" → Qwen-only Fallback
            if (result.Error is null && !result.IsRelevant)
                return await _vision.AnalyzeAsync(b64, ct);
        }
        catch (Exception ex)
        {
            _logger?.LogWarning(ex, "Multi-Modell-Pipeline fehlgeschlagen, Fallback auf Qwen-only");
        }
    }

    // Fallback: nur Qwen
    return await _vision.AnalyzeAsync(b64, ct);
}
```

#### Pipeline-Flows:

**Flow A: Multi-Modell (wenn Sidecar erreichbar)**
```
Frame-Bytes
  ↓
YOLO (Object Detection)
  ├─ Findet relevante Objekte
  └─ IsRelevant = true/false
  ↓
DINO (Ground-Level Detection) [wenn IsRelevant=true]
  ├─ Detektiert Schadens-Bounding-Boxes
  └─ Liefert Beschreibungen
  ↓
SAM (Segment Anything Model) [wenn IsRelevant=true]
  ├─ Segmentiert Schaden-Regionen
  └─ Liefert Pixel-Masken
  ↓
Qwen-Vision (mit MultiModelContext)
  ├─ Erhält: Bild + DINO-Detections + SAM-Masks
  ├─ Ausgabe: VSA-Codes, Severity, Clock, etc.
  └─ Rückgabe: EnhancedFrameAnalysis
```

**Flow B: Qwen-Only (Fallback)**
```
Frame-Bytes
  ↓
Qwen-Vision (blind, ohne Kontext)
  ├─ Analysiert Bild direkt
  └─ Rückgabe: EnhancedFrameAnalysis
```

---

### **SCHRITT 9: Qwen-Vision-Analyse (EnhancedVisionAnalysisService)**

**Datei:** `EnhancedVisionAnalysisService.cs` (Zeilen 71-260)

#### 9a: Prompt für Damage-Klassen

```csharp
private static readonly string DamageClassesPrompt = """
VSA/EN 13508-2 CODES für Kanalinspektion (Haltungen).
Melde ALLES was du siehst. Jeder Befund braucht vsa_code_hint, severity, position_clock.

=== BA: BAULICHE SCHÄDEN (severity 2-5) ===
BAA  Deformation (BAAA=vertikal, BAAB=horizontal) — Uhrlage + Querschnittsverringerung %
BAB  Riss (BABA/BABBA=längs, BABB/BABBB=radial, BABC=klaffend) — Uhrlage von-bis
BAC  Bruch/Scherbe (BACA=verschoben, BACB=Loch, BACC=Einsturz) — Uhrlage von-bis
... [weitere BA-Codes] ...

=== BB: BETRIEBLICHE STÖRUNGEN (severity 2-5) ===
BBA  Wurzeleinwuchs (BBAA=Pfahlwurzel, BBAB=fein, BBAC=komplex) — Uhrlage + Ausmaß %
BBB  Anhaftungen (BBBA=Inkrustation/Kalk, BBBB=Fett, BBBC=Fäulnis) — Uhrlage + Ausmaß %
... [weitere BB-Codes] ...

=== BC: BESTANDSAUFNAHME (severity=1) ===
BCA  Anschluss — Uhrlage + Durchmesser mm
BCB  Reparatur
BCC  Bogen/Kurve — Richtung
BCD  Rohranfang — immer bei Meter 0.0
BCE  Rohrende — am Ende der Haltung

=== REGELN ===
- vsa_code_hint MUSS bei JEDEM Finding gesetzt werden
- Verwende den SPEZIFISCHSTENCode den du bestimmen kannst
- severity: 1=Beobachtung, 2=leicht, 3=mittel, 4=schwer, 5=kritisch
- position_clock: Uhrlage als "HH" (z.B. "12"=Scheitel, "6"=Sohle, "3"=rechts, "9"=links)
- BC-Codes sind severity=1, MÜSSEN gemeldet werden
- Wenn NICHTS sichtbar: findings=[] und is_empty_frame=true
""";
```

#### 9b: Standard-Analyse (AnalyzeAsync)

```csharp
public async Task<EnhancedFrameAnalysis> AnalyzeAsync(
    string framePngBase64,
    CancellationToken ct = default)
{
    var messages = BuildMessages(framePngBase64);

    EnhancedVisionDto dto;
    try
    {
        dto = await _client.ChatStructuredAsync<EnhancedVisionDto>(
            model: _model,
            messages: messages,
            formatSchema: EnhancedVisionSchema,
            ct: ct).ConfigureAwait(false);
    }
    catch (OperationCanceledException) { throw; }
    catch (Exception ex)
    {
        System.Diagnostics.Debug.WriteLine(
            $"[EnhancedVision] KI-Fehler ({_model}): {ex.GetType().Name}: {ex.Message}");
        return EnhancedFrameAnalysis.Empty(ex.Message);
    }

    return MapToAnalysis(dto);
}

private List<OllamaClient.ChatMessage> BuildMessages(string framePngBase64)
{
    var messages = new List<OllamaClient.ChatMessage>();

    // Few-Shot Beispiele injizieren (optional)
    if (_cachedFewShot is { Count: > 0 })
    {
        foreach (var (example, b64) in _cachedFewShot)
        {
            // User-Nachricht mit Beispiel-Bild
            messages.Add(new OllamaClient.ChatMessage(
                Role: "user",
                Content: $"Analysiere dieses Kanalbild (Beispiel: {example.VsaCode} @ {example.ClockPosition})",
                ImagesBase64: [b64]));

            // Assistant-Antwort (vorgegeben)
            messages.Add(new OllamaClient.ChatMessage(
                Role: "assistant",
                Content: BuildFewShotResponse(example)));
        }
    }

    // Aktuelle Frame-Analyse
    messages.Add(new OllamaClient.ChatMessage(
        Role: "user",
        Content: $"{DamageClassesPrompt}\n\nAnalysiere dieses Kanalbild:",
        ImagesBase64: [framePngBase64]));

    return messages;
}
```

**Input:**
- `framePngBase64` (PNG-Bild in Base64)
- `_cachedFewShot` (optionale Few-Shot-Beispiele)

**Output:**
- `EnhancedVisionDto` mit:
  - `is_empty_frame: bool`
  - `meter: double?` (OSD-Meter falls lesbar)
  - `findings: List<EnhancedFinding>` mit VSA-Code-Hints, Severity, Clock, etc.

**Few-Shot Learning:**
- Bis zu 4 Beispiele (bei 2048-Token-Context)
- Bilder vorab als Base64 gecacht
- Jedes Beispiel zeigt: Bild → Assistant-Response

---

### **SCHRITT 10: Deterministischer Vergleich KI vs. Protokoll**

**Datei:** `SelfTrainingComparisonService.cs` (Zeilen 40-165)

```csharp
public ComparisonResult Compare(GroundTruthEntry truth, EnhancedFrameAnalysis analysis, bool isPdfPhoto = false)
{
    // ── Fall 1: KI hat NICHTS erkannt (findings=[]) ──
    if (!analysis.HasFindings)
    {
        // Grundgerust-Codes sind KEINE Schäden
        // Wenn Qwen nichts findet UND der Protokolleintrag ein Grundgerust-Code ist → ExactMatch
        var truthNorm = truth.VsaCode.Replace(".", "", StringComparison.Ordinal).ToUpperInvariant();
        bool isStructure = _basicStructureCodes.Contains(truthNorm);
        
        // Präfix-Match: AEDXO → AED ist in der Liste
        if (!isStructure && truthNorm.Length > 3)
            isStructure = _basicStructureCodes.Contains(truthNorm[..3]);
        if (!isStructure && truthNorm.Length > 2)
            isStructure = _basicStructureCodes.Contains(truthNorm[..2]);

        if (isStructure)
        {
            bool meterOk = (isPdfPhoto && !analysis.Meter.HasValue)
                || MeterMatches(truth.MeterStart, analysis.Meter);
            return new ComparisonResult(
                Level: meterOk ? MatchLevel.ExactMatch : MatchLevel.PartialMatch,
                ConfidenceScore: 0.70,
                Explanation: $"Grundgerust {truth.VsaCode} @ {truth.MeterStart:F1}m — KI korrekt: keine Schäden erkannt.",
                CodeMatched: true,
                MeterMatched: meterOk,
                SeverityPlausible: true,
                ClockMatched: true,
                BestMatchCode: truth.VsaCode,
                BestMatchMeter: analysis.Meter);
        }

        return new ComparisonResult(
            Level: MatchLevel.NoFindings,
            ConfidenceScore: 0.0,
            Explanation: $"KI hat keine Befunde bei {truth.MeterStart:F1}m erkannt.",
            CodeMatched: false,
            MeterMatched: false,
            SeverityPlausible: false,
            ClockMatched: false,
            BestMatchCode: null,
            BestMatchMeter: null);
    }

    // ── Fall 2: KI hat Findings (findings ist nicht leer) ──
    EnhancedFinding? bestMatch = null;
    double bestScore = -1;
    bool bestCodeMatch = false;
    bool bestMeterMatch = false;
    bool bestSeverityOk = false;
    bool bestClockMatch = false;

    foreach (var finding in analysis.Findings)
    {
        // Code-Matching: 3-stufiger Fallback
        // 1. vsa_code_hint direkt aus Qwen (z.B. "BABBA")
        // 2. InferCodeFromLabel: Label-Text → Code (z.B. "Riss" → "BAB")
        // 3. ReverseLookup: Langtext → Code (z.B. "Anschluss mit Formstück" → "BCAAA")
        string? resolvedCode = finding.VsaCodeHint;
        if (string.IsNullOrEmpty(resolvedCode) && !string.IsNullOrEmpty(finding.Label))
        {
            resolvedCode = VsaCodeResolver.InferCodeFromLabel(finding.Label)
                ?? VsaCodeTree.ReverseLookup(finding.Label);
        }
        bool codeMatch = CodesMatch(truth.VsaCode, resolvedCode);

        // Bei PDF-Fotos: Meter ist implizit korrekt (Foto gehört zum Protokolleintrag)
        bool meterMatch = isPdfPhoto && !analysis.Meter.HasValue
            ? true
            : MeterMatches(truth.MeterStart, analysis.Meter);

        bool severityOk = SeverityPlausible(truth.VsaCode, finding.Severity);

        // Bei PDF-Fotos: Clock nicht bestrafen wenn KI keine Uhrlage liefert
        bool clockMatch = isPdfPhoto
            ? ClockMatchesPdfTolerant(truth.ClockPosition, finding.PositionClock)
            : ClockMatches(truth.ClockPosition, finding.PositionClock);

        // Gewichtete Punktzahl
        double score = 0;
        if (codeMatch) score += 0.40;
        if (meterMatch) score += 0.25;
        if (severityOk) score += 0.15;
        if (clockMatch) score += 0.20;

        if (score > bestScore)
        {
            bestScore = score;
            bestMatch = finding;
            bestCodeMatch = codeMatch;
            bestMeterMatch = meterMatch;
            bestSeverityOk = severityOk;
            bestClockMatch = clockMatch;
        }
    }

    // Match-Level bestimmen
    MatchLevel level;
    if (bestCodeMatch && bestMeterMatch && bestClockMatch)
        level = MatchLevel.ExactMatch;
    else if (bestCodeMatch)
        level = MatchLevel.PartialMatch;
    else
        level = MatchLevel.Mismatch;

    return new ComparisonResult(
        Level: level,
        ConfidenceScore: Math.Round(bestScore, 2),
        Explanation: BuildExplanation(truth, bestMatch!, level, bestCodeMatch, bestMeterMatch, bestClockMatch),
        CodeMatched: bestCodeMatch,
        MeterMatched: bestMeterMatch,
        SeverityPlausible: bestSeverityOk,
        ClockMatched: bestClockMatch,
        BestMatchCode: bestResolvedCode,
        BestMatchMeter: analysis.Meter);
}
```

#### 10a: Code-Matching (3-stufig)

```csharp
private static bool CodesMatch(string truthCode, string? kiCode)
{
    if (string.IsNullOrEmpty(kiCode)) return false;

    // Punkt-Notation entfernen: "BDC.A" → "BDC", "BAB.B" → "BAB"
    string t = truthCode.ToUpperInvariant().Trim().Split('.')[0];
    string k = kiCode.ToUpperInvariant().Trim().Split('.')[0];

    // 1. Exakt
    if (t == k) return true;

    // 2. Präfix: Protokoll "BAB" matcht KI "BABA"
    if (k.StartsWith(t, StringComparison.Ordinal)) return true;
    if (t.StartsWith(k, StringComparison.Ordinal)) return true;

    // 3. Gleiche Schadensgruppe (erste 3 Zeichen)
    if (t.Length >= 3 && k.Length >= 3 && t[..3] == k[..3]) return true;

    return false;
}
```

**Fallback-Hierarchie:**
1. `finding.VsaCodeHint` (direkt von Qwen)
2. `VsaCodeResolver.InferCodeFromLabel()` (Label-Text → Code)
3. `VsaCodeTree.ReverseLookup()` (Langtext → Code)

#### 10b: Toleranzen

```csharp
private const double MeterTolerance = 1.0;      // ± 1.0m
private const int ClockTolerance = 1;            // ± 1 Stunde (zirkulär 12h)
private const int SeverityTolerance = 1;         // ± 1 Stufe (1-5)

private static bool MeterMatches(double truthMeter, double? kiMeter)
{
    if (!kiMeter.HasValue) return false;
    return Math.Abs(truthMeter - kiMeter.Value) <= MeterTolerance;
}

// PDF-Fotos: toleranter (Meter implizit OK, Clock optional)
private static bool ClockMatchesPdfTolerant(string? truthClock, string? kiClock)
{
    if (string.IsNullOrEmpty(truthClock) && string.IsNullOrEmpty(kiClock)) return true;
    if (string.IsNullOrEmpty(kiClock)) return true;  // KI hat keine Uhrlage → nicht bestrafen
    if (string.IsNullOrEmpty(truthClock)) return true; // Protokoll hat keine → auch OK
    if (!TryParseClock(truthClock, out int tHour)) return true;
    if (!TryParseClock(kiClock, out int kHour)) return true;
    int diff = Math.Abs(tHour - kHour);
    if (diff > 6) diff = 12 - diff;  // Zirkulär
    return diff <= ClockTolerance;
}

// Video-Frames: strenger (beide Uhrlage nötig)
private static bool ClockMatches(string? truthClock, string? kiClock)
{
    if (string.IsNullOrEmpty(truthClock) && string.IsNullOrEmpty(kiClock)) return true;
    if (string.IsNullOrEmpty(truthClock) || string.IsNullOrEmpty(kiClock)) return false;
    
    if (!TryParseClock(truthClock, out int tHour)) return false;
    if (!TryParseClock(kiClock, out int kHour)) return false;

    int diff = Math.Abs(tHour - kHour);
    if (diff > 6) diff = 12 - diff;
    return diff <= ClockTolerance;
}
```

#### 10c: Match-Level-Bestimmung

| Bedingung | Level | Bedeutung |
|-----------|-------|-----------|
| Code + Meter + Clock alle OK | **ExactMatch** | Volltreffer (Score 0.80–1.0) |
| Code OK, aber Meter oder Clock nicht | **PartialMatch** | Teiltreffer (Score 0.40–0.79) |
| Code nicht OK | **Mismatch** | Abweichung (Score 0.0–0.39) |
| KI findings=[] und nicht Grundgerust | **NoFindings** | Keine Erkennung (Score 0.0) |

---

### **SCHRITT 11: QualityGate - Samples filtern**

**Datei:** `SelfTrainingOrchestrator.cs` (Zeilen 309-325)

```csharp
// QualityGate: nur akzeptierte Samples speichern
var samplesList = generatedSamples.ToList();
var samplesAccepted = 0;
if (samplesList.Count > 0)
{
    var qgBatch = _qualityGate.EvaluateBatch(samplesList);
    if (qgBatch.Red > 0)
    {
        _logger?.LogWarning(
            "QualityGate: {Count} Samples abgelehnt (Red) für {CaseId}",
            qgBatch.Red, tc.CaseId);
    }
    var accepted = qgBatch.Accepted.ToList();
    samplesAccepted = accepted.Count;
    if (accepted.Count > 0)
        await TrainingSamplesStore.MergeAndSaveAsync(accepted);
}
```

**Datei:** `SampleQualityGateService.cs` (Zeilen 47-127)

```csharp
public SampleQualityResult Evaluate(TrainingSample sample)
{
    var issues = new List<QualityIssue>();

    // ── Hard-Red: sofortiger Ausschluss (eines reicht) ────────────
    
    if (string.IsNullOrWhiteSpace(sample.Code) || sample.Code.Length < 2)
        return HardRed("Code fehlt oder zu kurz (min. 2 Zeichen)");

    if (!KnowledgeBase.KnowledgeBaseManager.IsValidVsaLeitungscode(sample.Code))
        return HardRed($"Code '{sample.Code}' ist kein gültiger VSA-Leitungscode");

    if (string.IsNullOrWhiteSpace(sample.SampleId))
        return HardRed("SampleId fehlt");

    if (string.IsNullOrWhiteSpace(sample.CaseId))
        return HardRed("CaseId fehlt");

    if (string.IsNullOrWhiteSpace(sample.Beschreibung))
        return HardRed("Beschreibung fehlt komplett");

    // ── Gewichtete Mängel ────────────────────────────────────────

    if (string.IsNullOrWhiteSpace(sample.Signature))
        issues.Add(new("Signatur fehlt (Dedup nicht möglich)", 2));

    // Frame-Prüfung: SourceType-bewusst
    var isBatchOrPdf = sample.SourceType is SourceTypeNames.BatchImport
                       or SourceTypeNames.PdfPhoto;
    if (!isBatchOrPdf && string.IsNullOrWhiteSpace(sample.FramePath))
        issues.Add(new("Kein Frame-Pfad (erwartet bei Selbsttraining)", 2));
    else if (!string.IsNullOrWhiteSpace(sample.FramePath) && !File.Exists(sample.FramePath))
        issues.Add(new("Frame-Datei existiert nicht", 2));

    if (sample.Beschreibung.Trim().Equals(sample.Code.Trim(), StringComparison.OrdinalIgnoreCase))
        issues.Add(new("Beschreibung ist nur Code-Echo", 1));

    if (sample.MeterStart <= 0 && !IsZeroMeterCode(sample.Code))
        issues.Add(new("MeterStart ist 0 (unüblich für diesen Code)", 1));

    if (sample.IsStreckenschaden && sample.MeterEnd <= sample.MeterStart)
        issues.Add(new("Streckenschaden ohne Ausdehnung", 1));

    // ── Ergebnis berechnen ────────────────────────────────────────
    
    if (issues.Count == 0)
        return new SampleQualityResult(SampleQualityGrade.Green, []);

    var totalWeight = issues.Sum(i => i.Weight);
    var grade = totalWeight >= RedThreshold  // RedThreshold = 4
        ? SampleQualityGrade.Red
        : SampleQualityGrade.Yellow;

    return new SampleQualityResult(grade, issues.Select(i => i.Text).ToList());
}

public SampleQualityBatchResult EvaluateBatch(IReadOnlyList<TrainingSample> samples)
{
    var results = new List<(TrainingSample Sample, SampleQualityResult Result)>();
    foreach (var s in samples)
        results.Add((s, Evaluate(s)));

    return new SampleQualityBatchResult(results);
}
```

**QualityGrade:**
| Grade | Gewicht | Bedeutung |
|-------|---------|-----------|
| **Green** | 0 | Vollständig, Auto-Approve + KB-Index |
| **Yellow** | 1–3 | Brauchbar, gespeichert aber Review |
| **Red** | 4+ oder Hard-Red | Unbrauchbar, Reject (nicht speichern) |

**Hard-Red (sofort):**
- Code zu kurz oder fehlend
- Ungültiger VSA-Leitungscode
- SampleId fehlend
- CaseId fehlend
- Beschreibung komplett leer

**Gewichtete Mängel:**
- Signatur fehlt: -2 (Dedup nicht möglich)
- Frame-Pfad fehlt/existiert nicht: -2 (Datenverlust)
- Beschreibung ist Code-Echo: -1
- MeterStart=0 (ungewöhnlich): -1
- Streckenschaden ohne Ausdehnung: -1

---

### **SCHRITT 12: Speichern mit atomare Writes**

**Datei:** `TrainingSamplesStore.cs` (Zeilen 50-76, 213-267)

```csharp
public static async Task MergeAndSaveAsync(List<TrainingSample> newSamples)
{
    await _fileLock.WaitAsync();  // Lock für Race-Condition-Prävention
    try
    {
        // Lade existierende Samples
        var existing = await LoadInternalAsync();
        var existingSigs = existing
            .Where(s => !string.IsNullOrEmpty(s.Signature))
            .Select(s => s.Signature)
            .ToHashSet(StringComparer.Ordinal);

        // Füge neue Samples hinzu (Dedup via Signature)
        foreach (var s in newSamples)
        {
            if (!string.IsNullOrEmpty(s.Signature) && existingSigs.Contains(s.Signature))
                continue;  // Skip Duplikat
            existing.Add(s);
            if (!string.IsNullOrEmpty(s.Signature))
                existingSigs.Add(s.Signature);
        }

        // Speichern mit atomarem Rename
        await SaveInternalAsync(existing);
    }
    finally
    {
        _fileLock.Release();
    }
}

private static async Task SaveInternalAsync(List<TrainingSample> samples)
{
    var path = GetStorePath();  // z.B. KnowledgeBase/training_samples.json
    var dir = Path.GetDirectoryName(path)!;
    Directory.CreateDirectory(dir);

    // ── Rotierende Sicherungs-Backups vor dem Schreiben ──
    if (File.Exists(path))
    {
        try
        {
            var bak1 = path + ".bak";
            var bak2 = path + ".bak.2";
            var bak3 = path + ".bak.3";
            // Rotation: .bak.2 → .bak.3, .bak → .bak.2, aktuell → .bak
            if (File.Exists(bak2)) { try { File.Copy(bak2, bak3, true); } catch { } }
            if (File.Exists(bak1)) { try { File.Copy(bak1, bak2, true); } catch { } }
            File.Copy(path, bak1, overwrite: true);
        }
        catch { /* best-effort */ }
    }

    // ── In temp-Datei schreiben (gleicher Ordner für atomares Rename) ──
    var tempPath = path + ".tmp";
    try
    {
        using (var stream = File.Create(tempPath))
        {
            await JsonSerializer.SerializeAsync(stream, samples, Application.Common.JsonDefaults.Indented);
            await stream.FlushAsync();
        }

        // Validierung: temp-Datei muss lesbar sein
        using (var checkStream = File.OpenRead(tempPath))
        {
            var check = await JsonSerializer.DeserializeAsync<List<TrainingSample>>(checkStream);
            if (check is null || check.Count != samples.Count)
                throw new InvalidOperationException(
                    $"Validierung fehlgeschlagen: erwartet {samples.Count}, gelesen {check?.Count ?? 0}");
        }

        // ── ATOMARES RENAME: temp → Zieldatei ──
        File.Move(tempPath, path, overwrite: true);

        // Alte .bad_* Dateien aufräumen
        CleanupBadFiles(path);
    }
    catch
    {
        // temp-Datei aufräumen bei Fehler, Originaldatei bleibt unberührt
        try { if (File.Exists(tempPath)) File.Delete(tempPath); }
        catch { /* best-effort */ }
        throw;
    }
}

// Fehlerbehandlung: Korruptete Datei → Backup laden
private static async Task<List<TrainingSample>> LoadInternalAsync()
{
    var path = GetStorePath();
    if (!File.Exists(path))
        return new List<TrainingSample>();

    try
    {
        using var stream = File.OpenRead(path);
        var samples = await JsonSerializer.DeserializeAsync<List<TrainingSample>>(stream);
        return samples ?? new List<TrainingSample>();
    }
    catch (Exception ex)
    {
        // Backup-Dateien als Fallback
        var timestamp = DateTime.UtcNow.ToString("yyyyMMdd_HHmmss");
        var backup = path + $".bad_{timestamp}";
        try { File.Copy(path, backup); }
        catch { }

        var dir = Path.GetDirectoryName(path)!;
        var name = Path.GetFileName(path);
        var backups = new List<string>();
        
        // .bak zuerst (jüngstes erfolgreiche Save-Backup)
        var bakFile = path + ".bak";
        if (File.Exists(bakFile))
            backups.Add(bakFile);
        
        // Dann .bad_* (Korruptions-Backups, jüngstes zuerst)
        backups.AddRange(Directory.GetFiles(dir, name + ".bad_*")
            .OrderByDescending(f => f));

        foreach (var bak in backups)
        {
            try
            {
                using var bakStream = File.OpenRead(bak);
                var bakSamples = await JsonSerializer.DeserializeAsync<List<TrainingSample>>(bakStream);
                if (bakSamples is { Count: > 0 })
                {
                    return bakSamples;
                }
            }
            catch { /* nächstes Backup versuchen */ }
        }

        return new List<TrainingSample>();
    }
}
```

**Atomare Write-Strategie:**
1. Schreibe in `.tmp`
2. Validiere JSON
3. Rename `.tmp` → Final (atomar)
4. Falls Fehler: `.tmp` gelöscht, Originaldatei unberührt

**Backup-Rotation:**
```
training_samples.json      (aktuell)
training_samples.json.bak  (letztes erfolgreiches Save)
training_samples.json.bak.2
training_samples.json.bak.3
training_samples.json.bad_20260331_120000
training_samples.json.bad_20260331_113000
... (max. 3 behalten)
```

**Fehlerbehandlung:**
- JSON-Parsing-Fehler: Backup `.bak` laden
- `.bak` auch beschädigt: Alte `.bad_*` versuchen
- Keine Backups: Leere Liste, kein Datenverlust

**Signatur (Deduplication):**
```csharp
public static string BuildCanonicalSignature(string caseId, string code, double meterCenter, double? meterEnd)
{
    return $"{caseId}|{code}|{meterCenter:F1}|{(meterEnd ?? meterCenter):F1}";
}
```

---

### **SCHRITT 13: KB-Indexierung (inkrementell)**

**Datei:** `TrainingCenterViewModel.cs` (Zeilen 2250-2295)

```csharp
private async Task<List<string>> IncrementalKbUpdateAsync(List<TrainingSample> samples, CancellationToken ct)
{
    var indexedIds = new List<string>();
    try
    {
        var ollamaConfig = OllamaConfig.Load();
        var ollamaReachable = await CheckOllamaReachableAsync(ollamaConfig, ct);
        if (!ollamaReachable)
        {
            Log($"KB-Update übersprungen: Ollama nicht erreichbar auf {ollamaConfig.BaseUri}");
            return indexedIds;
        }

        _kbHttpClient ??= new System.Net.Http.HttpClient { Timeout = ollamaConfig.RequestTimeout };
        using var kbCtx = new KnowledgeBaseContext();
        var embedder = new EmbeddingService(_kbHttpClient, ollamaConfig);
        var kbManager = new KnowledgeBaseManager(kbCtx, embedder);

        // Embeddings parallel generieren (CPU-Arbeit, blockiert GPU nicht)
        var indexedBag = new System.Collections.Concurrent.ConcurrentBag<string>();
        await Parallel.ForEachAsync(samples,
            new ParallelOptions { MaxDegreeOfParallelism = 4, CancellationToken = ct },
            async (sample, token) =>
            {
                if (kbManager.IsIndexed(sample.SampleId)) return;
                if (await kbManager.IndexSampleAsync(sample, token))
                    indexedBag.Add(sample.SampleId);
            });
        indexedIds.AddRange(indexedBag);

        if (indexedIds.Count > 0)
        {
            kbManager.CreateVersion($"Self-Training inkrementell {DateTime.Now:yyyy-MM-dd HH:mm}");
            Log($"KB-Update: {indexedIds.Count} Samples inkrementell indexiert");
        }
        else
        {
            Log("KB-Update: Alle Samples bereits indexiert");
        }
    }
    catch (Exception ex) when (ex is not OperationCanceledException)
    {
        Log($"KB-Update Fehler: {ex.Message}");
    }
    return indexedIds;
}
```

**Input:**
- `samples` (List<TrainingSample> mit Status=Approved oder PartialMatch)

**Output:**
- `indexedIds` (List<string> mit erfolgreich indizierten SampleIds)

**Parallelisierung:**
- `MaxDegreeOfParallelism = 4` (Embedding-CPU-Arbeit)
- `ConcurrentBag` für Lock-freie Sammlung

**KB-Prozess:**
1. Prüfe ob Sample bereits indexiert: `kbManager.IsIndexed(sample.SampleId)`
2. Indexiere Sample: `IndexSampleAsync()` (Embedding generieren + speichern)
3. Erstelle KB-Version: `CreateVersion()` mit Timestamp

---

### **SCHRITT 14: Progress-Callback und UI-Updates**

**Datei:** `TrainingCenterViewModel.cs` (Zeilen 165-222)

```csharp
public void OnSelfTrainingStep(SelfTrainingStep step)
{
    void Apply()
    {
        PipelineActiveStep = (int)step.Stage;
        CurrentEntryCode = step.VsaCode;
        CurrentEntryMeter = step.MeterPosition;
        ProgressValue = step.EntryIndex + 1;
        ProgressMax = step.TotalEntries;

        // Aktives Modell je Stage anzeigen
        (ActiveModelName, IsModelActive) = step.Stage switch
        {
            SelfTrainingStage.BuildingTimeline    => ("PdfPig (CPU)", true),
            SelfTrainingStage.ExtractingFrame     => ("ffmpeg (CPU)", true),
            SelfTrainingStage.Analyzing           => ($"{_activeVisionModel} (GPU)", true),
            SelfTrainingStage.Comparing           => ("Deterministisch (CPU)", true),
            SelfTrainingStage.AssessingTechnique  => ($"{_activeVisionModel} (GPU)", true),
            SelfTrainingStage.Completed           => ("", false),
            _ => ("", false)
        };

        // Stage-spezifisches Logging
        switch (step.Stage)
        {
            case SelfTrainingStage.BuildingTimeline:
                if (step.ErrorMessage is not null)
                    AddSelfTrainingLog(step.ErrorMessage);
                break;
            case SelfTrainingStage.ExtractingFrame:
                AddSelfTrainingLog($"Frame extrahieren: {step.VsaCode} @ {step.MeterPosition:F1}m");
                if (step.FramePath is not null) SetLiveFrameThrottled(step.FramePath);
                break;
            case SelfTrainingStage.Analyzing:
                AddSelfTrainingLog($"KI-Analyse [{_activeVisionModel}]: {step.VsaCode}");
                break;
            case SelfTrainingStage.Comparing:
                AddSelfTrainingLog($"Vergleich: {step.VsaCode}");
                break;
            case SelfTrainingStage.AssessingTechnique:
                if (step.Technique is { } tech)
                {
                    CurrentTechniqueGrade = tech.OverallGrade;
                    CurrentTechniqueDetails = $"Licht: {tech.LightingQuality} | Schärfe: {tech.SharpnessQuality}";
                    AddSelfTrainingLog($"Technik: {tech.OverallGrade} (Licht={tech.LightingQuality}, Schärfe={tech.SharpnessQuality})");
                }
                break;
            case SelfTrainingStage.Completed:
                if (step.Comparison is { } cmp)
                {
                    // UI-Update mit Vergleichsergebnis
                    var matchSymbol = cmp.Level switch
                    {
                        MatchLevel.ExactMatch => "✓✓",
                        MatchLevel.PartialMatch => "✓",
                        MatchLevel.Mismatch => "✗",
                        MatchLevel.NoFindings => "○",
                        _ => "?"
                    };
                    AddSelfTrainingLog($"{matchSymbol} {cmp.Explanation}");
                }
                break;
        }
    }

    // UI-Updates auf Main-Thread
    Application.Current?.MainWindow?.Dispatcher.InvokeAsync(Apply);
}
```

**Stage-Abläufe:**

| Stage | Modell | Zweck |
|-------|--------|--------|
| **BuildingTimeline** | PdfPig (CPU) | PDF-Text + Bilder extrahieren |
| **ExtractingFrame** | ffmpeg (CPU) | Frame aus PDF/Video laden |
| **Analyzing** | Qwen/YOLO+DINO+SAM (GPU) | KI-Vision-Analyse |
| **Comparing** | Deterministisch (CPU) | KI-Erkennung vs. Protokoll |
| **AssessingTechnique** | Qwen (GPU) | Beleuchtung/Schärfe-Bewertung |
| **Completed** | — | Fortschritt melden |

---

### **SCHRITT 15: Abschluss und Statistiken**

**Datei:** `SelfTrainingOrchestrator.cs` (Zeilen 327-338)

```csharp
sw.Stop();
return new SelfTrainingResult(
    CaseId: tc.CaseId,
    TotalEntries: allEntries.Count,
    ExactMatches: exactMatches,
    PartialMatches: partialMatches,
    Mismatches: mismatches,
    NoFindings: noFindings,
    OverallTechnique: overallTechnique,
    Duration: sw.Elapsed,
    SamplesGenerated: samplesAccepted);
```

**Output (SelfTrainingResult):**
```csharp
public sealed record SelfTrainingResult(
    string CaseId,
    int TotalEntries,           // Alle Protokoll-Einträge
    int ExactMatches,           // Code + Meter + Clock OK
    int PartialMatches,         // Code OK, aber Meter/Clock nicht
    int Mismatches,             // Code nicht OK
    int NoFindings,             // KI erkannte nichts
    TechniqueAssessment? OverallTechnique,
    TimeSpan Duration,
    int SamplesGenerated);      // Samples die QualityGate passiert haben
```

**Statistik-Ausgabe:**

**Datei:** `TrainingCenterViewModel.cs` (Zeilen 2107-2169)

```csharp
Log($"--- [{ci + 1}/{casesToTrain.Count}] Selbsttraining: {currentCase.CaseId} ---");
Log($"  Protokoll: {currentCase.ProtocolPath}");

// ... RunAsync() ...

totalExact += result.ExactMatches;
totalPartial += result.PartialMatches;
totalMismatch += result.Mismatches;
totalNoFindings += result.NoFindings;
totalSamples += result.SamplesGenerated;

Log($"Ergebnis: {result.ExactMatches}x✓✓, {result.PartialMatches}x✓, {result.Mismatches}x✗, {result.NoFindings}x○");
Log($"Samples: {result.SamplesGenerated}/{result.TotalEntries}");
Log($"Dauer: {result.Duration.TotalSeconds:F1}s");

// Nach allen Fällen
Log($"\n=== ZUSAMMENFASSUNG SELBSTTRAINING ===");
Log($"Fälle: {casesToTrain.Count} verarbeitet");
Log($"Protokoll-Einträge: {totalExact + totalPartial + totalMismatch + totalNoFindings}");
Log($"  - ExactMatch: {totalExact}");
Log($"  - PartialMatch: {totalPartial}");
Log($"  - Mismatch: {totalMismatch}");
Log($"  - NoFindings: {totalNoFindings}");
Log($"Samples gespeichert: {totalSamples}");
Log($"Fehler: {caseErrors}");
StatusText = $"Selbsttraining abgeschlossen: {totalSamples} Samples generiert.";
```

---

## FEHLERKETTE UND FEHLERBEHANDLUNG

### Kritische Fehler (Stop):
1. **Datei nicht gefunden:** PDF fehlt → Status-Nachricht, Return
2. **Cancellation:** User klickt Abbrechen → OperationCanceledException → break

### Nicht-kritische Fehler (Continue):
1. **Sidecar nicht erreichbar:** Fallback auf Qwen-only
2. **Qwen-Fehler:** EnhancedFrameAnalysis.Empty(errorMsg)
3. **PyMuPDF fehlgeschlagen:** Fallback auf PdfPig
4. **Frame lesen fehlgeschlagen:** Skip Frame (continue)
5. **KB-Indexierung fehlgeschlagen:** Log + continue
6. **QualityGate Red:** Skip Sample (nicht speichern)

### Datensicherung:
- **Atomare Writes:** `.tmp` + Rename (nie halbwache Dateien)
- **Rotation Backups:** `.bak`, `.bak.2`, `.bak.3`
- **Korruptionsbehandlung:** `.bad_YYYYMMDD_HHMMSS` + Fallback-Laden

---

## ZUSAMMENFASSUNG: DATENFLUSS

```
PDF-Datei
  ↓
[PdfProtocolExtractor.ExtractAsync]
  ├─ Text: PdfPig Letter-by-Letter + Font-Shift-Korrektur
  ├─ Fotos: PyMuPDF (CMYK→RGB) + Filter (is_likely_photo)
  └─ Einträge: 6-Regex-Strategien + Normalisierung
  
GroundTruthEntry[] (mit ExtractedFramePath)
  ↓
[SelfTrainingOrchestrator.RunAsync]
  ├─ Frame laden (PNG-Bytes)
  ├─ KI-Analyse (Qwen oder Multi-Modell: YOLO→DINO→SAM→Qwen)
  ├─ Deterministischer Vergleich (Code/Meter/Clock)
  └─ Aufnahmetechnik-Bewertung
  
TrainingSample (mit Status: Approved/New/etc.)
  ↓
[SampleQualityGateService.Evaluate]
  ├─ Hard-Red Check (5 Kriterien)
  └─ Weighted Issues (3 Stufen: Green/Yellow/Red)
  
Green/Yellow Samples
  ↓
[TrainingSamplesStore.MergeAndSaveAsync]
  ├─ Dedup via Signature
  ├─ Atomare Write (.tmp + Rename)
  ├─ Backup-Rotation
  └─ Fehlerbehandlung (Korruptur → .bak laden)
  
training_samples.json
  ↓
[IncrementalKbUpdateAsync]
  ├─ Embedding generieren (parallel, 4x)
  ├─ KB-Indexierung
  └─ Version-Markierung
  
Knowledge Base (Vector-DB)
```

---

## KONFIGURATIONEN

### AiRuntimeConfig (Ollama)
```json
{
  "OllamaBaseUri": "http://localhost:11434",
  "VisionModel": "Qwen2.5-VL",
  "EmbeddingModel": "nomic-embed-text"
}
```

### PipelineConfig (Sidecar für Multi-Modell)
```json
{
  "MultiModelEnabled": true,
  "SidecarUrl": "http://localhost:8000",
  "SidecarTimeoutSec": 60,
  "YoloConfidence": 0.5,
  "DinoBoxThreshold": 0.3,
  "DinoTextThreshold": 0.25
}
```

### TrainingCenterSettings
```csharp
public int GpuConcurrency { get; set; } = 2;  // Parallele Qwen-Requests
```

---

Diese Dokumentation deckt jeden Schritt des Self-Training-Prozesses mit exakten Datei-Referenzen, Zeilennummern und Code-Auszügen ab.