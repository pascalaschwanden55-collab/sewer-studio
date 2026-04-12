# SewerStudio KI 4.1 — All-In Upgrade Prompt

## Kontext
v4.0 Code-Audit hat 25 Findings identifiziert. NVIDIA Grounding DINO wird am 15.04.2026 deprecated. Dieses Upgrade fixt alle Audit-Findings, ersetzt DINO+SAM3 durch Florence-2+SAM2 (Clean Cut), integriert NVFP4, und fuegt ChangeNet + Nemotron-Parse hinzu. NV-CLIP wird evaluiert.

## Regeln
- Arbeite auf einer KOPIE des Repos (`Sewer-Studio_KI_4.1/`)
- Jeder Fix einzeln committen mit Verweis auf Phase+ID (z.B. "P1-A01: remove hardcoded credentials")
- Kommentare auf Deutsch
- Nach jeder Phase: `dotnet build AuswertungPro.sln` pruefen
- Tests NUR fuer Recommendation- und QualityGate-Logik
- Keine NuGet-Pakete ohne Rueckfrage
- Bestehenden Code nur aendern wenn im Plan beschrieben

## Schluessel-Erkenntnis
Der Sidecar hat ein sauberes Slot-System (`ModelSlot.DINO` / `ModelSlot.SAM`). Florence-2 und SAM 2 nutzen dieselben Slots. Die C#-Seite (`VisionPipelineClient`) braucht KEINE Aenderung — die API-Schemas (`DinoRequest`/`DinoResponse`, `SamRequest`/`SamResponse`) bleiben kompatibel.

---

## Phase 1 — Audit Quick Wins (6 Fixes, alle Aufwand S)

### P1-A01: Hardcodierte Firebird-Credentials entfernen
**Datei:** `src/AuswertungPro.Next.Infrastructure/Import/Ibak/IbakExportImportService.cs`  
**Zeile:** 462-463  
**Aenderung:** Fallback-Werte `"SYSDBA"` und `"masterkey"` entfernen. Wenn Env-Vars `IBAK_FDB_USER` / `IBAK_FDB_PASSWORD` fehlen: `InvalidOperationException` werfen mit Meldung "IBAK_FDB_USER und IBAK_FDB_PASSWORD muessen als Umgebungsvariablen gesetzt sein."

### P1-A03: BAA aus IsRissLabel entfernen
**Datei:** `src/AuswertungPro.Next.UI/Ai/AiOverlayConverter.cs`  
**Zeile:** 284  
**Aenderung:** `"baa"` aus `IsRissLabel()` entfernen. BAA = Deformation (Verformung), NICHT Riss. Ergebnis:
```csharp
return lower.Contains("riss") || lower.Contains("crack") || lower.Contains("bab");
```

### P1-A06: Microsoft.Data.Sqlite Version vereinheitlichen
**Dateien:**
- `src/AuswertungPro.Next.Infrastructure/AuswertungPro.Next.Infrastructure.csproj:15` — Version `8.0.2`
- `src/AuswertungPro.Next.UI/AuswertungPro.Next.UI.csproj:29` — Version `10.0.3`
**Aenderung:** Beide auf `10.0.3` setzen.

### P1-A07: Circuit Breaker entschaerfen
**Datei:** `src/AuswertungPro.Next.UI/Ai/OllamaClient.cs`  
**Zeile:** 74-76  
**Aenderung:**
```csharp
FailureRatio = 0.5,          // War: 1.0 (oeffnet nach JEDEM Fehler)
MinimumThroughput = 10,      // War: 5
```

### P1-A09: .gitignore erweitern
**Datei:** `.gitignore`  
**Aenderung:** Am Ende hinzufuegen:
```
# KnowledgeBase SQLite (mehrere GB, nicht ins Repo)
**/Knowledge/*.db
**/KnowledgeBase.db
```

### P1-A19: AppSettings.Load Silent Catch durch Logging ersetzen
**Datei:** `src/AuswertungPro.Next.UI/Ai/AiPlatformConfig.cs`  
**Zeile:** 82-84  
**Aenderung:**
```csharp
try { settings = AppSettings.Load(); }
catch (Exception ex)
{
    System.Diagnostics.Debug.WriteLine(
        $"[AiPlatformConfig] AppSettings.Load fehlgeschlagen: {ex.Message} — verwende Defaults");
}
```

---

## Phase 2 — Audit Medium Fixes (5 Fixes, Aufwand M)

### P2-A02: MaskQuantificationService Kalibrierung durchreichen
**Datei:** `src/AuswertungPro.Next.UI/Ai/Pipeline/MaskQuantificationService.cs`  
**Zeile:** 158-168  
**Aenderung:** `QuantifyAll()` Signature erweitern:
```csharp
public static IReadOnlyList<QuantifiedMask> QuantifyAll(
    SamResponse samResponse, int pipeDiameterMm, PipeCalibration? calibration = null)
{
    var results = new List<QuantifiedMask>(samResponse.Masks.Count);
    foreach (var mask in samResponse.Masks)
    {
        results.Add(Quantify(mask, samResponse.ImageWidth, samResponse.ImageHeight,
            pipeDiameterMm, calibration));
    }
    return results;
}
```
Dann ALLE Aufrufer von `QuantifyAll()` suchen und `calibration` durchreichen wo verfuegbar.

### P2-A04: Severity-Algorithmen vereinheitlichen
**Datei:** `src/AuswertungPro.Next.UI/Ai/AiOverlayConverter.cs`  
**Zeile:** 288-305  
**Aenderung:** Neue unified Methode:
```csharp
private static int ComputeSeverity(
    string? label, double confidence,
    MaskQuantificationService.QuantifiedMask? quantified = null)
{
    // Wenn SAM-Quantifizierung vorhanden: physische Groesse priorisieren
    if (quantified != null)
    {
        if (quantified.CrossSectionReductionPercent is >= 50) return 5;
        if (quantified.CrossSectionReductionPercent is >= 30) return 4;
        if (quantified.CrossSectionReductionPercent is >= 15) return 3;
        if (quantified.IntrusionPercent is >= 30) return 4;
    }
    // Sonst: Label-gewichtete Confidence
    var labelLower = label?.ToLowerInvariant() ?? "";
    bool isCriticalLabel = labelLower.Contains("bac") || labelLower.Contains("bruch")
        || labelLower.Contains("einsturz");
    double adjustedConf = isCriticalLabel ? confidence + 0.15 : confidence;
    return adjustedConf switch
    {
        >= 0.85 => 5,
        >= 0.65 => 4,
        >= 0.45 => 3,
        >= 0.25 => 2,
        _ => 1
    };
}
```
Alle Aufrufe von `ConfidenceToSeverity()` und `EstimateSeverity()` durch `ComputeSeverity()` ersetzen.

### P2-A05: VSA minLength fuer Schaechte differenzieren
**Datei:** `src/AuswertungPro.Next.Infrastructure/Vsa/VsaEvaluationService.cs`  
**Zeile:** 59, 99, 133  
**Aenderung:** An allen 3 Stellen:
```csharp
// VORHER:
const double minLength = 3.0; // Kanaele; Schaechte: 0.5
// NACHHER:
double minLength = isManhole ? 0.5 : 3.0;
```
Die Methoden-Signaturen muessen `bool isManhole` Parameter bekommen. Aufrufer pruefen ob `record.GetFieldValue("Objektart")` ein Schacht ist.

### P2-A08: Sidecar VRAM-Monitor mit Request-Ablehnung
**Datei:** `sidecar/sidecar/main.py`  
**Zeile:** 67-85 und neue Middleware  
**Aenderung:**
```python
# Globales Flag
_vram_critical: bool = False

async def _vram_monitor_loop() -> None:
    global _vram_critical
    while True:
        try:
            await asyncio.sleep(_VRAM_MONITOR_INTERVAL_SEC)
            status = gpu_manager.check_vram_health()
            _vram_critical = (status == "critical")
            if status != "ok":
                pct = gpu_manager.get_vram_utilization_percent()
                logger.warning("VRAM-Monitor: Status=%s (%.1f%%)", status, pct)
        except asyncio.CancelledError:
            break

# Middleware in main.py registrieren:
@app.middleware("http")
async def vram_guard(request: Request, call_next):
    if _vram_critical and request.url.path not in ("/health", "/status"):
        return JSONResponse(status_code=503,
            content={"detail": "VRAM critical — Request abgelehnt"})
    return await call_next(request)
```

### P2-A10: Protokoll-Revision Dedup
**Datei:** `src/AuswertungPro.Next.Infrastructure/Import/Kins/KinsImportService.cs`  
**Zeile:** 569-592  
**Aenderung:** Vor `History.Add()` Hash-Vergleich:
```csharp
var newHash = ComputeProtocolHash(cloned);
var currentHash = ComputeProtocolHash(record.Protocol.Current.Entries);
if (newHash == currentHash)
    return; // Identisch, kein Update noetig

record.Protocol.History.Add(record.Protocol.Current);
// ... rest wie bisher

// Neue Hilfsmethode:
private static string ComputeProtocolHash(IReadOnlyList<ProtocolEntry> entries)
{
    using var sha = System.Security.Cryptography.SHA256.Create();
    var sb = new System.Text.StringBuilder();
    foreach (var e in entries.OrderBy(x => x.MeterStart))
        sb.Append($"{e.Code}|{e.MeterStart}|{e.MeterEnd}|{e.Remark};");
    var bytes = sha.ComputeHash(System.Text.Encoding.UTF8.GetBytes(sb.ToString()));
    return Convert.ToHexString(bytes);
}
```

---

## Phase 3 — Audit Remaining Fixes (11 Fixes, Aufwand S-M)

### P3-A11: AutoCalibration ScanLines erweitern
**Datei:** `src/AuswertungPro.Next.UI/Ai/AutoCalibrationService.cs:23`
```csharp
// VORHER:
private static readonly double[] ScanLines = { 0.45, 0.475, 0.50, 0.525, 0.55 };
// NACHHER:
private static readonly double[] ScanLines = { 0.30, 0.35, 0.40, 0.45, 0.50, 0.55, 0.60, 0.65, 0.70 };
```
Minimum-Threshold anpassen: `if (measurements.Count < 5) return null;` (war 3)

### P3-A12: NormToMm Fallback dokumentieren
**Datei:** `src/AuswertungPro.Next.Domain/Models/CodingSession.cs:172`
```csharp
// VORHER:
if (NormalizedDiameter <= 0) return normalizedLength * 500; // Fallback
// NACHHER:
if (NormalizedDiameter <= 0)
{
    // Fallback: Annahme DN300 bei ~60% Bildbreite (300mm / 0.6 = 500)
    const double fallbackMmPerNorm = 500.0;
    return normalizedLength * fallbackMmPerNorm;
}
```

### P3-A13: PixelToMm Aspect-Ratio einbauen
**Datei:** `src/AuswertungPro.Next.Domain/Models/CodingSession.cs:177-185`
```csharp
public double PixelToMm(double normalizedPixels, double frameWidthPx, double imageAspect = 1.0)
{
    // Aspect-Korrektur anwenden falls nicht-quadratisch
    double corrected = normalizedPixels * (imageAspect > 0 ? imageAspect : 1.0);
    if (NormalizedDiameter > 0)
        return NormToMm(corrected);
    if (PipePixelDiameter <= 0) return 0;
    double pipePixelNormalized = PipePixelDiameter / frameWidthPx;
    double mmPerNormPixel = NominalDiameterMm / pipePixelNormalized;
    return corrected * mmPerNormPixel;
}
```
ACHTUNG: Alle Aufrufer pruefen und `imageAspect` Parameter durchreichen wo verfuegbar.

### P3-A15: SAM Batch-Size dynamisch
**Datei:** `sidecar/sidecar/models/sam_wrapper.py:22`
```python
# VORHER:
_SAM_MAX_BATCH = 100
# NACHHER:
def _get_sam_max_batch() -> int:
    avail_gb = gpu_manager.get_available_vram_gb()
    return max(10, min(100, int(avail_gb * 15)))
```

### P3-A16: PerFrameTimeout groesser als QwenFrameTimeout
**Datei:** `src/AuswertungPro.Next.UI/Ai/Pipeline/MultiModelAnalysisService.cs:42,45`
```csharp
public TimeSpan QwenFrameTimeout { get; set; } = TimeSpan.FromSeconds(45);
public TimeSpan PerFrameTimeout { get; set; } = TimeSpan.FromSeconds(90); // War: 45
```

### P3-A17: Qwen-Timeout Fallback zu FastModel
**Datei:** `src/AuswertungPro.Next.UI/Ai/Pipeline/MultiModelAnalysisService.cs:649-653`
```csharp
catch (OperationCanceledException) when (!ct.IsCancellationRequested)
{
    _logger.LogWarning("Frame {Frame}: Qwen timeout ({Timeout}s) — versuche FastModel",
        frameIndex, QwenFrameTimeout.TotalSeconds);
    // Fallback: FastModel mit kuerzem Timeout
    try
    {
        using var fallbackCts = CancellationTokenSource.CreateLinkedTokenSource(ct);
        fallbackCts.CancelAfter(TimeSpan.FromSeconds(20));
        // FastModel Aufruf hier...
    }
    catch { /* Wenn auch FastModel fehlschlaegt: Frame ueberspringen */ }
}
```

### P3-A18: ParseClockHour Fallback null statt 12.0
**Datei:** `src/AuswertungPro.Next.UI/Ai/AiOverlayConverter.cs:307-314`
```csharp
private static double? ParseClockHour(string? clockStr)
{
    var normalized = VsaCodeResolver.NormalizeClock(clockStr);
    if (string.IsNullOrWhiteSpace(normalized)) return null;  // War: 12.0
    var match = Regex.Match(normalized, @"(\d{1,2})");
    if (match.Success && int.TryParse(match.Groups[1].Value, out var h))
        return h == 0 ? 12.0 : Math.Clamp(h, 1, 12);
    return null;  // War: 12.0
}
```
ACHTUNG: Return-Typ aendert sich von `double` zu `double?`. Alle Aufrufer muessen auf `null` pruefen.

### P3-A20: KinsImportService Encoding-Fallback loggen
**Datei:** `src/AuswertungPro.Next.Infrastructure/Import/Kins/KinsImportService.cs:347-358`
```csharp
try {
    return File.ReadLines(path, Encoding.GetEncoding(1252)).ToList();
}
catch (Exception ex1) {
    System.Diagnostics.Debug.WriteLine(
        $"[KinsImport] Windows-1252 fehlgeschlagen fuer {Path.GetFileName(path)}: {ex1.Message} — versuche UTF-8");
    try {
        return File.ReadLines(path, Encoding.UTF8).ToList();
    }
    catch (Exception ex2) {
        throw new InvalidOperationException(
            $"Datei {path} konnte weder als Windows-1252 noch als UTF-8 gelesen werden.", ex2);
    }
}
```

### P3-A22: ClockToNormalized Kommentar
**Datei:** `src/AuswertungPro.Next.UI/Ai/AiOverlayConverter.cs:323`
```csharp
// 0.8-Faktor: Uhrzeiger-Indikatoren 20% innerhalb des Rohrrands
// fuer bessere Sichtbarkeit bei ueberlappendem Overlay
double x = cx + Math.Sin(angleRad) * radius * 0.8;
```

### P3-A23: Qwen-Logging nur bei Diagnostics-Flag
**Datei:** `src/AuswertungPro.Next.UI/Ai/OllamaClient.cs:460-470`
```csharp
if (System.Diagnostics.Debugger.IsAttached || _diagnosticsEnabled)
{
    // Bestehendes Logging...
}
```

### P3-A24: Aggregator Magic Numbers in PipelineConfig
**Datei:** `src/AuswertungPro.Next.UI/Ai/Pipeline/MultiModelAnalysisService.cs:34-38`
```csharp
private DetectionAggregator CreateAggregator() => new(
    minConsecutiveFrames: _cfg.AggregatorMinFrames,   // Default: 3
    minConfidence: _cfg.AggregatorMinConfidence,       // Default: 0.4
    meterMergeRadius: _cfg.AggregatorMergeRadius,      // Default: 1.5
    maxGapFrames: _cfg.AggregatorMaxGap                // Default: 5
);
```
Neue Properties in `PipelineConfig` Record hinzufuegen mit Defaults.

---

## Phase 4 — Florence-2 + SAM 2 Clean Cut (Aufwand L)

### Architektur
Das Sidecar Slot-System bleibt unveraendert:
- `ModelSlot.DINO` → Florence-2 (selber Slot)
- `ModelSlot.SAM` → SAM 2 (selber Slot)
- API-Schemas bleiben identisch → C# braucht KEINE Aenderung

### 4.1 Florence-2 Wrapper
**Datei:** `sidecar/sidecar/models/dino_wrapper.py` — komplett ersetzen

Kernpunkte:
- Load: `AutoModelForCausalLM.from_pretrained("microsoft/Florence-2-large")` + `AutoProcessor`
- Inference: `model.generate()` mit Task-Prompt `"<OD>"` fuer Object Detection
- Output-Konversion: Florence-2 BBoxes → `DinoDetection(x1, y1, x2, y2, label, confidence)`
- GPU-Slot: weiterhin `ModelSlot.DINO`
- Config: `florence2_confidence: float = 0.25` (DINO hatte `box_threshold=0.25, text_threshold=0.20`)

Das DinoRequest/DinoResponse Schema bleibt IDENTISCH:
```python
class DinoRequest(BaseModel):
    image_base64: str
    text_prompt: str
    box_threshold: float = 0.25   # Wird intern als florence2_confidence interpretiert
    text_threshold: float = 0.20  # Wird ignoriert (Florence-2 braucht das nicht)

class DinoResponse(BaseModel):
    detections: list[DinoDetection]
    inference_time_ms: float
```

### 4.2 SAM 2 Wrapper
**Datei:** `sidecar/sidecar/models/sam_wrapper.py` — komplett ersetzen

Kernpunkte:
- Load: `from sam2.build_sam import build_sam2` + `SAM2ImagePredictor`
- `set_image()` Interface hat sich geaendert → anpassen
- `predict()` gibt weiterhin `masks, scores, logits` zurueck
- Ring-Scan Logik (Annulus-Geometrie, Zeile 152-316) bleibt komplett erhalten, nur Predictor-Calls anpassen
- RLE-Encoding bleibt identisch
- Batch-Handling: dynamisch (Fix A15 bereits implementiert)
- GPU-Slot: weiterhin `ModelSlot.SAM`

### 4.3 Config anpassen
**Datei:** `sidecar/sidecar/config.py`
```python
# DINO-Felder ersetzen:
florence2_model_path: str = "models/florence-2"
florence2_confidence: float = 0.25

# SAM-Felder aktualisieren:
sam_model_path: str = "models/sam2"
sam_model_type: str = "sam2_hiera_large"  # SAM2 Variante
```

### 4.4 Startup anpassen
**Datei:** `sidecar/sidecar/main.py`
- Pre-Warm: Florence-2 statt DINO laden
- Pre-Warm: SAM 2 statt SAM 3 laden
- Version-String → `"2.0.0"`

### 4.5 Modell-Dateien
```
sidecar/models/florence-2/    ← Florence-2-large Weights herunterladen
sidecar/models/sam2/          ← SAM 2 ViT-H Checkpoint herunterladen
```
Alte Modelle (`grounding_dino_1.5/`, `sam3/`) als Backup behalten, nicht loeschen.

### 4.6 Verifikation
1. Sidecar starten: `python -m uvicorn sidecar.main:app --port 8100`
2. Health-Check: `curl http://localhost:8100/health`
3. Test-Frame: Ein bekanntes Schadenbild durch `/detect/dino` und `/segment/sam` senden
4. E2E-Vergleich: 10 Referenz-Haltungen mit v4.0 (DINO+SAM3) vs. v4.1 (Florence-2+SAM2)
   - Detection Count ±20% toleriert
   - QualityGate Score ±0.1 toleriert

---

## Phase 5 — NVFP4 Quantisierung (Aufwand M)

### Was
YOLO26m-seg auf RTX 5090 mit NVFP4 (native 4-bit) quantisieren.

### Wie
**Datei:** `sidecar/sidecar/models/yolo_wrapper.py` (TensorRT Export Zeile 196-234)
- Neuer Config-Parameter: `yolo_precision: str = "fp4"` (war: `"fp16"`)
- TensorRT Export mit `half=False, int8=False` → `precision='fp4'` oder TensorRT Builder API
- Benchmark: 100 Frames mit FP16 vs. FP4 vergleichen (mAP, fps, VRAM)
- NUR wenn mAP-Verlust < 2%: FP4 als Default setzen

### Verifikation
```bash
# Benchmark-Script
python -m sidecar.benchmark --model yolo26m --precision fp4 --frames 100
```

---

## Phase 6 — NV-CLIP Evaluation (Aufwand M)

### Was
NV-CLIP als Bild-Embedder evaluieren. Aktuell: `nomic-embed-text` (nur Text). Ziel: Bilder direkt embedden.

### Evaluation (KEIN Production-Code)
1. NV-CLIP lokal auf RTX 5090 installieren (OpenCLIP oder NVIDIA-Variante)
2. 100 KB-Samples: einmal mit nomic-embed-text (Text), einmal mit CLIP (Bild) embedden
3. 20 Goldstandard-Queries: Precision@3 vergleichen
4. Ergebnis in `CLIP_EVALUATION_REPORT.md` dokumentieren

### Falls CLIP gewinnt → Hybrid-Embedding
Dann in separatem Schritt:
- `EmbeddingService.cs` → neuer `EmbedImageAsync(byte[] imageData)` Methode
- `KnowledgeBaseContext.cs` → `ImageVector` Spalte
- `RetrievalService.cs` → Hybrid-Scoring: Text 40% + Image 30% + Code 20% + Material 10%

---

## Phase 7 — Nemotron-Parse PDF-Import (Aufwand L)

### Was
Robusterer PDF-Tabellen-Import fuer alte Inspektionsprotokolle (Fretz, KIT, Uri).

### Neue Dateien
**Python:**
- `sidecar/sidecar/models/nemotron_parse_wrapper.py`
- `sidecar/sidecar/routes/parse.py` → `POST /parse/pdf-table`
- `sidecar/sidecar/schemas/parse.py`
- Neuer `ModelSlot.PARSE` in `gpu_manager.py`

**C#:**
- `src/AuswertungPro.Next.Infrastructure/Import/Pdf/NemotronPdfImportService.cs`
- `src/AuswertungPro.Next.UI/Ai/Pipeline/VisionPipelineClient.cs` → neuer Endpoint `ParsePdfTableAsync()`

### Verifikation
10 bekannte PDFs parsen und mit bestehendem Regex-Ergebnis vergleichen.

---

## Phase 8 — Visual ChangeNet (Aufwand L)

### Was
Pixel-Level Aenderungserkennung: Inspektion 2020 vs. 2026 derselben Haltung.

### Neue Dateien
**Python:**
- `sidecar/sidecar/models/changenet_wrapper.py`
- `sidecar/sidecar/routes/changenet.py` → `POST /analyze/change-detection`
- Neuer `ModelSlot.CHANGENET` in `gpu_manager.py`

**C#:**
- `src/AuswertungPro.Next.UI/Ai/ChangeDetection/ChangeDetectionService.cs`
- `src/AuswertungPro.Next.UI/ViewModels/ChangeDetectionViewModel.cs`
- `src/AuswertungPro.Next.UI/Views/Windows/ChangeDetectionWindow.xaml`

### UI
- Benutzer waehlt 2 Projekte derselben Haltung
- Side-by-Side mit Change-Overlay
- Farben: Rot=Verschlechterung, Gruen=Verbesserung, Gelb=Neu

### Verifikation
3 Haltungspaare mit bekannten Veraenderungen testen.

---

## Reihenfolge und Abhaengigkeiten

```
Phase 0: Repo kopieren
    ↓
Phase 1: Audit Quick Wins (S)        ← Keine Abhaengigkeit
    ↓
Phase 2: Audit Medium Fixes (M)      ← Braucht Phase 1
    ↓
Phase 3: Audit Remaining (S-M)       ← Braucht Phase 2
    ↓
Phase 4: Florence-2 + SAM 2 (L)      ← Braucht Phase 3 (A15 fuer SAM Batch)
    ↓
    ├── Phase 5: NVFP4 (M)           ← Parallel moeglich
    └── Phase 6: NV-CLIP Eval (M)    ← Parallel moeglich
        ↓
Phase 7: Nemotron-Parse (L)          ← Braucht Phase 4 (Sidecar stabil)
    ↓
Phase 8: Visual ChangeNet (L)        ← Braucht Phase 4 (Sidecar stabil)
```

Geschaetzte Gesamtdauer: ~25 Arbeitstage
