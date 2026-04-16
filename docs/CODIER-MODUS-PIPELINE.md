# SewerStudio Codier-Modus — KI-Pipeline Dokumentation

**Stand:** 16. April 2026
**Zweck:** Komplette Dokumentation des KI-Analyse-Pfads im Codier-Modus fuer Review und Fehlersuche.

---

## 1. Architektur-Ueberblick

```
┌──────────────────────────────────────────────────────────────┐
│  VIDEO (VLC Player)                                          │
│  _player.Time, _player.IsPlaying                             │
└──────────────┬───────────────────────────────────────────────┘
               │
   ┌───────────┼───────────┬──────────────┐
   │           │           │              │
   │  Auto-Timer (5s)  "Analysieren"  Manuelle Markierung
   │  Zeile 8433       Zeile 6780     Zeile 2036
   │           │           │              │
   └───────────┼───────────┴──────────────┘
               │
               ▼
      RunCodingAnalysisAsync()         ← Zeile 6788
               │
               ├─ CaptureSnapshotAsync()        → PNG-Bytes
               │
               ├─ Qwen VLM Analyse              → Findings + Meter + view_type
               │  EnhancedVisionAnalysisService.AnalyzeAsync()
               │  Zeile 6829
               │
               ├─ LiveDetectionMapper            → LiveDetection
               │  FromEnhancedAnalysis()
               │  Zeile 6831
               │
               ├─ ShowCodingAiResults()          → UI + Events
               │  Zeile 7765
               │
               └─ SAM Nachsegmentierung          → Masken auf Canvas
                  (wenn Sidecar erreichbar)
                  Zeile 6842-6898
```

**WICHTIG:** Der Codier-Modus nutzt **NICHT** die YOLO→DINO→SAM Pipeline.
DINO (Grounding DINO 1.5) versagt konsistent bei dunklen Kanalbildern (0 Detektionen).
Die Multi-Model Pipeline laeuft nur im **Nachtbatch** (Video-Pipeline).

---

## 2. Initialisierung: InitCodingAi()

**Datei:** `PlayerWindow.xaml.cs`, Zeile 6439-6508

```
AiRuntimeConfig.Load()
  │
  ├─ _codingEnhancedVision = new EnhancedVisionAnalysisService(
  │      client, config.VisionModel, config.ReferenceVisionModel)
  │  → Qwen 8B fuer Frame-Analyse
  │
  ├─ Sidecar-Verbindung (fuer SAM):
  │  ├─ _codingVisionClient = VisionPipelineClient("http://localhost:8100")
  │  ├─ HealthCheckAsync() → Sidecar erreichbar?
  │  └─ Status: "Qwen + SAM-Segmentierung" oder "Qwen (ohne SAM)"
  │
  └─ Few-Shot-Beispiele (async):
     └─ EnableFewShotAsync(store) → 238 Beispielbilder laden
```

---

## 3. Haupt-Analyse: RunCodingAnalysisAsync()

**Datei:** `PlayerWindow.xaml.cs`, Zeile 6788-6912

### 3.1 Guard-Checks (Zeile 6791-6796)

```csharp
if ((_codingEnhancedVision == null && _codingLiveDetection == null)
    || _codingIsAnalyzing) return;

_codingIsAnalyzing = true;  // Nur eine Analyse gleichzeitig
```

### 3.2 Frame Capture (Zeile 6813)

```csharp
var pngBytes = await CaptureSnapshotAsync();
// VLC extrahiert aktuellen Frame als PNG
// Kann bis 1s dauern wenn Video laeuft
```

### 3.3 Qwen-Analyse (Zeile 6825-6831)

```csharp
if (_codingEnhancedVision != null)
{
    var b64 = Convert.ToBase64String(pngBytes);
    var enhanced = await _codingEnhancedVision.AnalyzeAsync(
        b64, _codingAnalysisCts.Token);
    result = LiveDetectionMapper.FromEnhancedAnalysis(enhanced, captureTimestampSec);
}
```

**Was Qwen bekommt:**
- Prompt: DamageClassesPrompt (~300 Woerter, kurz und fokussiert)
- Bild: Base64-kodiertes PNG
- Few-Shot: Bis zu 5 aehnliche Beispielbilder mit korrekten Labels
- Schema: JSON-Zwang (meter, findings[], view_type, image_quality)

**Was Qwen zurueckgibt:**
```json
{
  "meter": 12.5,
  "findings": [
    {"label": "BCC", "severity": 1, "position_clock": "3"}
  ],
  "image_quality": "gut",
  "is_empty_frame": false,
  "view_type": "axial"
}
```

### 3.4 Ergebnis-Anzeige: ShowCodingAiResults() (Zeile 7765)

```
1. Error-Check
2. Frame-Readiness-Gate (Dateneinblendung erkennen, 3 Frames warten)
3. OSD-Meterstand uebernehmen (wenn vorhanden und < 500m)
4. FilterValidFindings():
   ├─ VSA-Code-Validierung
   ├─ Duplikat-Pruefung (IsAlreadyCovered)
   └─ Ergebnis: validFindings[]
5. UI Update:
   ├─ Befundliste rechts
   ├─ Status-Badge
   └─ Detection-Overlay auf Video
6. CodingEvent erstellen (Datenbank)
7. Pausenmodus: Video pausieren wenn Befunde erkannt
```

### 3.5 SAM-Nachsegmentierung (Zeile 6842-6898)

```
Wenn: Qwen hat Findings UND Sidecar erreichbar

Fuer jedes Finding:
  ├─ BBox vorhanden? → Pixel-Koordinaten an SAM
  └─ Nur Uhrlage? → ClockPositionToBox():
     ├─ "12" → Box oben (Scheitel)
     ├─ "3"  → Box rechts
     ├─ "6"  → Box unten (Sohle)
     └─ "9"  → Box links

SAM segmentiert innerhalb der Box → Maske auf Canvas rendern
```

---

## 4. Qwen-Prompt: EnhancedVisionAnalysisService

**Datei:** `EnhancedVisionAnalysisService.cs`

### 4.1 Kurzer Prompt (Codier-Modus): DamageClassesPrompt

~300 Woerter. Fokus auf schnelle Erkennung.

```
PFLICHT (severity=1, IMMER melden):
  BCD = Rohranfang
  BCE = Rohrende
  BCC = Bogen/Kurve
  BCA = Anschluss

SCHAEDEN (severity 2-5):
  BAB = Riss, BAC = Bruch, BAF = Korrosion, BAJ = Rohrverbindung,
  BAI = Dichtung, BAA = Verformung, BBA = Wurzeln, BBB = Inkrustation,
  BBC = Ablagerung, BBF = Infiltration

REGELN:
  - label = VSA-Code (KEIN Freitext)
  - position_clock = Uhrlage
  - BACB ≠ BCD (gezackt vs. glatt)
```

### 4.2 Voller Prompt (Nachtbatch): DamageClassesPromptFull

~1500 Woerter. Zusaetzlich: Aufnahmetechnik, view_type-Klassifikation, Tiefenfilter, Streckenschaeden, Abbruch-Codierung.

### 4.3 JSON-Schema (EnhancedVisionSchema)

```json
{
  "meter": number|null,
  "pipe_material": "beton"|"pvc"|...,
  "pipe_diameter_mm": integer|null,
  "findings": [{
    "label": "BABBA",
    "vsa_code_hint": "BABBA",
    "severity": 1-5,
    "position_clock": "3",
    "extent_percent": 20,
    "height_mm": 15,
    "width_mm": 8
  }],
  "image_quality": "gut"|"mittel"|"schlecht",
  "is_empty_frame": boolean,
  "view_type": "axial"|"nahaufnahme"|"schwenk"|"schacht"
}
```

### 4.4 ViewType-Filter (MapToAnalysis)

```csharp
// Zeile 712-728
if (viewType is "nahaufnahme" or "schwenk")
    findings = [];  // ALLES verwerfen
    
if (viewType is "schacht")
    findings = nur BCD/BCE/BDB;  // Nur Steuercodes
```

---

## 5. Bekannte Probleme

### P1: DINO funktioniert nicht fuer Kanalbilder

**Status:** Bekannt, Workaround aktiv
**Symptom:** DINO (Grounding DINO 1.5) liefert 0 Detektionen bei dunklen Kanalbildern
**Ursache:** Open-Vocabulary-Modell nicht fuer diese Domaene trainiert
**Workaround:** Codier-Modus nutzt direkt Qwen statt YOLO→DINO→SAM
**Langfrist:** YOLO26l-seg mit eigenen Trainingsdaten ersetzen

### P2: Prompt zu lang fuer Qwen 8B

**Status:** Gefixt (16.04.2026)
**Symptom:** BC-Codes (BCD, BCC, BCA) wurden nicht erkannt
**Ursache:** 1500-Woerter-Prompt fuellte den 8192-Token-Kontext komplett
**Fix:** Prompt auf 300 Woerter gekuerzt, voller Prompt nur fuer Nachtbatch

### P3: Auto-Analyse-Timer brach ab

**Status:** Gefixt (16.04.2026)
**Symptom:** Erste Analyse funktioniert, danach keine mehr
**Ursache:** Timer pruefte `_codingLiveDetection` (null) statt `_codingEnhancedVision`
**Fix:** Guard-Check aktualisiert

### P4: SAM-Nachsegmentierung mit Default-Box

**Status:** Teilweise gefixt
**Symptom:** SAM segmentierte das ganze Bild statt nur den Schaden
**Ursache:** Qwen liefert keine BBoxen, nur Uhrlage → Fallback war 50% des Bildes
**Fix:** ClockPositionToBox() erzeugt gezielte Box am Rohrrand basierend auf Uhrlage
**Offenes Problem:** Ohne Uhrlage ist die Box immer noch zu gross

### P5: ViewType-Klassifikator unzuverlaessig

**Status:** Bekannt
**Symptom:** BCD-Frames werden als "schacht" klassifiziert
**Ursache:** Zu wenig Schacht-Trainingsbilder (nur 64)
**Workaround:** ViewType ist nur Info, blockiert nicht mehr
**Langfrist:** Mehr Trainingsbilder sammeln

### P6: Few-Shot-Beispiele ohne BCC-Boegen

**Status:** Pruefen
**Symptom:** Boegen werden trotz 65 BCC-Beispielen nicht erkannt
**Moegliche Ursache:** Few-Shots werden nicht geladen, oder Kontext-Fenster zu klein
**Naechster Schritt:** Debug-Logging in EnableFewShotAsync einbauen

---

## 6. Architektur-Entscheidungen

### Warum Qwen statt YOLO im Codier-Modus?

| Kriterium | YOLO→DINO→SAM | Qwen direkt |
|-----------|---------------|-------------|
| Kanalbilder | DINO versagt (0 Detektionen) | Qwen erkennt Schaeden |
| BC-Codes | Nicht trainiert | Im Prompt definiert |
| Latenz | ~100ms (wenn DINO funktioniert) | ~500ms |
| Segmentierung | SAM direkt aus DINO-Box | SAM nachtraeglich aus Uhrlage |
| Few-Shot | Nicht moeglich | Beispielbilder mitschicken |

### Warum nicht YOLO26l-seg im Codier-Modus?

YOLO26l-seg ist trainiert (mAP50=25.5%) aber:
- Nur 828 Trainingsbilder (braucht 5000+)
- Labels sind Box-Polygone, keine echten Masken
- Noch nicht zuverlaessig genug fuer Live-Codierung
- Wird im Nachtbatch getestet und verbessert

---

## 7. Sidecar-Konfiguration

**Datei:** `sidecar/sidecar/config.py`

| Setting | Wert | Funktion |
|---------|------|----------|
| yolo_model_name | yolo26l-seg.pt | YOLO Detection+Segmentation |
| yolo_confidence | 0.25 | Schwellenwert |
| yolo_use_tensorrt | true | TensorRT FP16 Beschleunigung |
| dino_box_threshold | 0.25 | DINO Confidence (nicht im Codier-Modus) |
| sam_model_type | sam2.1_hiera_l.yaml | SAM 2.1 Large |
| Port | 8100 | localhost |

**Modell-Verzeichnis:**
```
sidecar/models/
├─ yolo26l-seg/          ← NEU: Detection + Segmentation
│  └─ yolo26l-seg.pt     (63 MB, 28.0M Parameter)
├─ yolo26m/              ← ALT: Nur Detection (Backup)
│  ├─ yolo26m.pt
│  └─ yolo26m.engine
├─ grounding_dino_1.5/   ← DINO (funktioniert nicht fuer Kanal)
├─ sam2/                 ← SAM 2.1 Large
└─ florence-2-ft/        ← Florence-2 (Shadow-Training)
```

---

## 8. Code-Referenzen

| Datei | Zeilen | Funktion |
|-------|--------|----------|
| PlayerWindow.xaml.cs | 6439-6508 | InitCodingAi |
| PlayerWindow.xaml.cs | 6788-6912 | RunCodingAnalysisAsync |
| PlayerWindow.xaml.cs | 7765-7857 | ShowCodingAiResults |
| PlayerWindow.xaml.cs | 8433-8451 | CodingLiveAiTimer_Tick |
| PlayerWindow.xaml.cs | 2095-2152 | ShowSamPreviewAtMarkAsync |
| PlayerWindow.xaml.cs | 7376-7406 | ClockPositionToBox |
| EnhancedVisionAnalysisService.cs | 78-106 | DamageClassesPrompt (kurz) |
| EnhancedVisionAnalysisService.cs | 109-583 | DamageClassesPromptFull |
| EnhancedVisionAnalysisService.cs | 586-648 | BuildPrompt |
| EnhancedVisionAnalysisService.cs | 650-739 | MapToAnalysis (ViewType-Filter) |
| EnhancedVisionAnalysisService.cs | 28-74 | EnhancedVisionSchema |
| LiveDetectionMapper.cs | 14-68 | FromEnhancedAnalysis |
| SingleFrameMultiModelService.cs | 49-157 | AnalyzeFrameAsync (nur Nachtbatch) |
| DetectionAggregator.cs | 7-173 | Temporal Voting (nur Nachtbatch) |
| sidecar/config.py | 43-55 | YOLO-Konfiguration |
| sidecar/yolo_wrapper.py | 49-55 | _yolo_dir (Modell-Ordner) |
| sidecar/dino_wrapper.py | 148-237 | DINO detect (funktioniert nicht) |

---

## 9. Naechste Schritte

1. **Prompt-Optimierung:** Qwen muss BC-Codes (Bogen, Anschluss) zuverlaessig erkennen
2. **YOLO26l-seg verbessern:** Mehr Trainingsdaten (echte Masken statt Box-Polygone)
3. **Few-Shot-Debug:** Pruefen ob BCC-Beispiele tatsaechlich an Qwen geschickt werden
4. **SAM3 als Annotation-Tool:** Exemplar-Prompts fuer schnelleres KB-Labeling
5. **Stammdaten im Prompt:** DN/Material aus DB3 als Kontext fuer Qwen
