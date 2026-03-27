# KI Video-Schadenserkennung + Trainingsmodul (Funktionsanleitung)

## 1) Kurzfazit

Ja, die KI-Pipeline fuer Schadenserkennung aus Videos ist im Projekt implementiert und lauffaehig.

Verifiziert am Code-Stand:
- `VideoAnalysisPipelineService` (Video -> Detections -> Code-Mapping)
- `VideoFullAnalysisService` + `EnhancedVisionAnalysisService`
- `TrainingCenter` (Sample-Generierung + Knowledge-Base Index)

## 2) Voraussetzungen

### 2.1 Umgebungsvariablen

```powershell
setx SEWERSTUDIO_AI_ENABLED 1
setx SEWERSTUDIO_OLLAMA_URL "http://localhost:11434"
setx SEWERSTUDIO_AI_VISION_MODEL "qwen3.5:27b"
setx SEWERSTUDIO_AI_TEXT_MODEL "qwen3.5:27b"
setx SEWERSTUDIO_AI_EMBED_MODEL "nomic-embed-text"
setx SEWERSTUDIO_FFMPEG "C:\\Pfad\\zu\\ffmpeg.exe"
```

Wichtig: Nach `setx` App neu starten.

Hinweis: Die Anwendung akzeptiert weiterhin Legacy-Variablen mit dem Praefix `AUSWERTUNGPRO_`, die dokumentierten Standardnamen sind jedoch `SEWERSTUDIO_*`.

### 2.2 Runtime-Checks

```powershell
ollama list
ffmpeg -version
ffprobe -version
```

## 3) Videoanalyse (pro Haltung)

### 3.1 Start in der UI

1. In `Haltungen` eine Zeile auswaehlen.
2. Rechtsklick auf die Zeile.
3. `Videoanalyse (KI Pipeline)...` starten.

Alternativ:
- `KI-Tools` -> `Videoanalyse (KI Pipeline)`.

### 3.2 Ablauf

1. Video wird (falls noetig) gesucht/neu verlinkt.
2. Phase `Videoanalyse`: Frames werden extrahiert und mit Vision-Modell ausgewertet.
3. Phase `Code-Mapping`: erkannte Befunde werden auf erlaubte VSA-Codes gemappt.
4. `Protokoll uebernehmen` speichert das erzeugte Protokoll in der Haltung.

### 3.3 Typische Fehler

- `KI ist deaktiviert`: `SEWERSTUDIO_AI_ENABLED=1` setzen.
- `Videodauer konnte nicht ermittelt werden`: ffmpeg/ffprobe Pfad pruefen.
- `Kein Code-Katalog vorhanden`: VSA-Katalog laden/pruefen.

## 4) Trainingsmodul (Training Center)

### 4.1 Start in der UI

- `Werkzeuge` -> `KI Videoanalyse - Training Center...`

### 4.2 Workflow A (manuell)

1. `Ordner waehlen...`
2. `Scannen`
3. Fall auswaehlen
4. `Samples generieren...`
5. Samples in Tab `Samples` auf `Approve` setzen
6. `Approved exportieren...`

### 4.3 Workflow B (Batch)

1. Mehrere Ordner waehlen
2. `Batch-Import + KB`
3. Der Lauf macht:
   - Scan
   - Sample-Generierung
   - Auto-Approve
   - KB-Rebuild (Embeddings)

### 4.4 Speicherorte

- `%APPDATA%\\AuswertungPro\\training_center.json`
- `%APPDATA%\\AuswertungPro\\training_center_samples.json`
- `%APPDATA%\\AuswertungPro\\frames\\...`
- `%APPDATA%\\AuswertungPro\\KiVideoanalyse\\KnowledgeBase.db`
- `%LOCALAPPDATA%\\SewerStudio\\data\\protocol_training.json`

## 5) Wichtige Hinweise

- Training-Sample-Generator unterstuetzt fuer Protokolle aktuell JSON und PDF.
- Falls in einem Ordner sowohl PDF als auch XML liegt, wird jetzt PDF bevorzugt.
- Reine XML-Protokolle ohne PDF/JSON liefern derzeit keine Samples.
- OSD-Meter-Timeline fuer Trainingssamples ist aktiv, wenn KI eingeschaltet ist und Ollama erreichbar ist.

## 6) Schnelltest (empfohlen)

1. Eine Haltung mit gueltigem Video + PDF waehlen.
2. `Videoanalyse (KI Pipeline)` durchlaufen und Protokoll uebernehmen.
3. Training Center oeffnen, denselben Fall scannen, Samples generieren.
4. `Batch-Import + KB` auf einem kleinen Testordner laufen lassen.
5. `KB pruefen` ausfuehren.
