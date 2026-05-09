# Slice 8a Punkt 3 — Mini-ADR Frame-Readiness / Live-AI-Coding-Loop

Datum: 2026-05-09
Status: **Entschieden** (User-Review 2026-05-09 22:00 lokal)
- Q1 = C (VM-Owner)
- Q2 = C (Hybrid)
- Q3 = A (Loop-CTS)
- Q5-Verteilung: zugestimmt
- Schritt 1 in 1a/1b/(1c optional) verfeinert (siehe Migrations-Schnitt)
- Smoke-Test-Gates: nach Schritt 4 + vor Schritt 5
Vorgeschichte:
- Konsolidierungs-ADR: `2026-05-09-slice-8a-coding-mode-konsolidierung.md` (Option B.1)
- Audit-Diff: `2026-05-09-slice-8a-1-audit-diff.md`
- Stop-Liste: `2026-05-09-slice-8a-2-stop-list-adr.md` (Punkt 3)
- Slices 8a.2.1 – 8a.2.11 abgeschlossen (3/4 Stop-Punkte erledigt)

## Was diese ADR NICHT macht

Sie schreibt keinen Code, keine Service-Skelette, keine Migration. Sie
beantwortet die **fünf Designfragen** vom 2026-05-09 und legt Grenzen
fest, an denen der eigentliche Slice ansetzen kann.

Die Migration selbst kommt **erst nach User-Freigabe** dieser ADR und
dann als eigener Slice mit eigenem Step-Plan.

## Bestandsaufnahme (Stand HEAD `78e39eb`)

Zwei parallele Implementierungen leben heute nebeneinander:

### A) PlayerWindow (vollständig)

Live-Coding-Loop seit längerem produktiv, alles in
`PlayerWindow.CodingMode.cs`:

- **Frame-Readiness-State-Maschine:** 5 Felder
  (`_codingFrameState : FrameReadiness`, `_codingOsdSkippedFrames`,
  `_codingMeterConfirmCount`, `_codingLastOsdMeter`,
  `_pendingWarmupResult`) + 3 Methoden (`IsFrameReady`,
  `UpdateFrameReadiness`, `ResetFrameReadiness`).
- **OSD-Reader:** `CodingReadOsdMeterAsync` — VLC-Snapshot + Ollama-LLM
  liest die Meterzahl unten rechts aus dem Video, schreibt in
  `_codingLastOsdMeter`, `OsdMeterBadge.Visibility`, `TxtOsdMeter.Text`.
- **Loop-Orchestrator:** `RunCodingAnalysisAsync(activityText, ...)`
  pro Frame KI fragen, Findings durchschleusen, Dedup, Auto-Accept-
  Versus-Pause-Confirm-Entscheidung. Auch
  `AnalyzeWithOverlayHintAsync` für User-markierte Hints.
- **Init:** `InitCodingAi` setzt EnhancedVisionAnalysisService +
  LiveDetectionService auf, registriert PipelineFailure-Handler.
- **Pause-Confirm-Workflow:** `PauseAndAskConfirmation`,
  `ConfirmAccept_Click` etc. (siehe Audit-Diff Sektion „Pause-Confirm").

### B) CodingModeWindow (Teilbau)

Schon implementiert in `CodingModeWindow.xaml.cs` + Partials:

- **AI-Init:** `InitAiOverlay` mit Few-Shot-Loading, identischer
  Aufbau wie PlayerWindow.
- **Single-Frame-Analyse:** `BtnAnalyzeFrame_Click` →
  `AnalyzeCurrentFrameAsync` — User klickt einen Button, ein Frame
  wird analysiert. Keine Live-Loop, kein Frame-Gate.
- **Cancel-Pattern:** `_analysisCts` (CancellationTokenSource),
  Reset-Notbremse nach 1s wenn `_isAnalyzing` hängt.
- **Status-Anzeige:** seit Slice 8a.2.4 in `AiStatus.cs`-Partial:
  `SetAiStatus`, `StartAiStatusPulse` etc.
- **Snapshot:** seit Slice 8a.2.3 in `FrameCapture.cs`-Partial:
  `CaptureCurrentFrameAsync` mit VLC-`TakeSnapshot` +
  `RenderTargetBitmap`-Fallback.
- **Pipeline-Failure:** `OnPipelineFailure` Event-Handler vorhanden.

**Was im CodingModeWindow fehlt:** Frame-Readiness-Gate, OSD-Reader,
Live-Loop, Pause-Confirm-Workflow, Auto-BCD/BCE.

## Die fünf Designfragen

### Q1 — Wer entscheidet „Frame bereit"?

**Optionen:**

- **A) State-Maschine wandert als Partial ins CodingModeWindow.**
  5 Felder + 3 Methoden 1:1 verschieben. *Risiko:* Window wächst um
  Coding-Geschäftslogik; gleicher Pattern wie heute in PlayerWindow.
- **B) Neuer `IFrameReadinessService` in Application-Layer.** Window
  meldet eingehende `LiveDetection`-Ergebnisse, Service liefert
  „Ready"-Event. *Risiko:* abstrakter Service nur für 5 Felder, sehr
  state-lastig — der Service ist quasi reine Datenstruktur.
- **C) Property + Methode auf `CodingSessionViewModel`.** Analog
  Punkt 4 (Session-State-Besitz). VM hat `IsFrameReady` als
  ObservableProperty + `RecordFrame(LiveDetection result)` als
  State-Mutator. *Risiko:* VM bekommt eine neue Verantwortung
  („Frame-Lifecycle"), die nicht direkt zu „Coding-Session" gehört.

**Empfehlung: C.** Frame-Readiness ist *de facto* Session-Phase
(Warm-up → Bereit → laufend). Das gehört zum VM, nicht zum Window
(zu UI-nah) und nicht zu einem isolierten Service (zu wenig
fachlich begründet). Konsequent zum Punkt-4-Pattern (`SortByMeter`,
`AddEventInOrder`): VM ist der Single-Source-of-Truth für
Session-Zustand.

**Konsequenz:** `FrameReadiness`-Enum + 5 Felder ziehen ins VM.
Die Window-Methoden lesen über `_vm.IsFrameReady` und melden Frames
mit `_vm.RecordFrame(result)`.

### Q2 — Wer besitzt Capture / Buffer / AI-Request?

**Aktuell:** Window startet alles (CaptureCurrentFrameAsync,
EnhancedVisionAnalysisService.AnalyzeAsync, Result-Verarbeitung).

**Optionen:**

- **A) Status quo.** Window orchestriert. *Risiko:* Window-Code
  bleibt fett und hart-testbar.
- **B) `IFrameAnalysisOrchestrator`-Service.** Service kennt die
  Pipeline (Snapshot → Vision → Quality-Gate → Findings), Window
  liefert nur den Snapshot via `OrchestrateAsync(byte[] png, ct)`
  und konsumiert ein `AnalysisResult`-Record. *Risiko:* neuer
  Service-Layer, mehr Indirektion.
- **C) Hybrid.** Service kapselt nur den AI-Call (heute schon
  `EnhancedVisionAnalysisService`); Loop-Orchestrierung bleibt im
  Window/VM, weil sie an UI-Timing und CancellationToken hängt.

**Empfehlung: C.** Service-Schicht ist heute schon richtig
geschnitten (`EnhancedVisionAnalysisService` + `LiveDetectionService`
+ `MultiModelAnalysisService`). Was fehlt, ist *kein neuer Service*,
sondern eine kleine Schicht im Window, die die Phasen koordiniert —
und das ist Window-Zuständigkeit, weil sie an UI-Timer + Player
hängt.

**Konsequenz:** Capture bleibt im Window-Partial (existiert schon).
AI-Call bleibt im bestehenden Service. Was wandert: nur die
Loop-Methode `RunCodingAnalysisAsync` von PlayerWindow ins
CodingModeWindow als Partial, und sie nutzt
`_vm.IsFrameReady` / `_vm.RecordFrame` aus Q1.

### Q3 — Cancel, Timeout, UI-Status

**Aktuell im CodingModeWindow:** `_analysisCts` (single CTS),
1s-Reset-Timeout, `SetAiStatus` für Anzeige. **Fehlt:** Loop-weite
CancellationToken-Verwaltung über mehrere Frames.

**Optionen:**

- **A) Eine Loop-CTS pro Coding-Session.** Wird beim Session-Start
  erzeugt, beim Stop/Close cancelled, kein Re-Use über Pausen.
  Per-Frame-Aufrufe übernehmen `cts.Token`.
- **B) Per-Frame-CTS.** Jeder Frame bekommt eigene CTS (heute
  Single-Frame-Pfad). *Risiko:* Pausierter Frame blockiert nichts,
  aber die User-Pause = Loop-Pause kann nicht mehr global gesetzt
  werden.

**Empfehlung: A.** Loop-CTS ist näher am tatsächlichen Lifecycle
(eine Codiersession = ein zusammenhängender Lauf). Pause-Resume
bleibt eine VM-Property, kein neues CTS.

**Timeout:** harter Timeout pro Frame im AI-Call (bereits in
`AiRuntimeConfig.OllamaRequestTimeout = 5min`); kein zusätzliches
Window-Timeout.

**UI-Status:** bleibt wie heute via `SetAiStatus` (UI-Sorge).

### Q4 — VLC / VideoView-Fallback

**Status:** schon erledigt. `CaptureCurrentFrameAsync` in
`CodingModeWindow.FrameCapture.cs` hat:

1. VLC `TakeSnapshot` (bevorzugt, echter Source-Frame).
2. WPF `RenderTargetBitmap`-Fallback wenn Snapshot leer/0-Byte.

Kein zusätzlicher Fix nötig. Der OSD-Reader (Q1/Q2) nutzt denselben
Capture-Pfad — kein Duplikat.

### Q5 — Wer macht was: Window vs. CodingModeWindow vs. Service

Da PlayerWindow laut Konsolidierungs-ADR (Option B.1) verschwindet,
gibt es nur **Window** (= CodingModeWindow) und **Service** (=
EnhancedVisionAnalysisService etc.). Verteilung:

| Aufgabe                              | Lokation                              |
|--------------------------------------|---------------------------------------|
| Frame-Readiness-State                | `CodingSessionViewModel` (Q1 Option C) |
| OSD-Reader (Pixel → Meterzahl)       | `IOsdMeterReader`-Service (neu, klein) |
| Frame-Capture (VLC/WPF)              | `CodingModeWindow.FrameCapture.cs` (existiert) |
| AI-Request (Vision+Quantification)   | `EnhancedVisionAnalysisService` (existiert) |
| Loop-Orchestrierung                  | `CodingModeWindow.LiveLoop.cs` (neu) |
| Status-Anzeige                       | `CodingModeWindow.AiStatus.cs` (existiert) |
| Pause-Confirm-Workflow               | bleibt VM + Window (eigener Sub-Slice) |

**Begründung für `IOsdMeterReader`:** der heutige
`CodingReadOsdMeterAsync` mischt VLC-Snapshot, Ollama-Call und
XAML-Status-Updates. Das ist der einzige Block, der wirklich einen
neuen Service rechtfertigt — kapselt eine pure Pixel-zu-Zahl-
Pipeline, die testbar wird.

## Resultierender Migrations-Schnitt (kein Code, nur Liste)

Slice 8a.3 läuft in dieser Reihenfolge:

### Schritt 1 — OSD-Parsing extrahieren (in drei Mini-Schritten)

- **1a. Pure Parsing-/Mapping-Logik extrahieren.** Neuer `OsdMeterParser`
  in der Application-Schicht: `TryParse(string rawText)` → `double?`.
  Kapselt nur die heutige inline-Regex (`(\d{1,3}(?:\.\d{1,2})?)`) +
  Komma→Punkt-Normalisierung + Plausibilitäts-Range (0–500m).
  KEINE Ollama-, KEINE VLC-, KEINE XAML-Abhängigkeit. Stateless,
  rein synchron, voll Unit-test-bar.
- **1b. Caller umstellen.** `CodingReadOsdMeterAsync` in
  `PlayerWindow.CodingMode.cs` ersetzt die inline-Regex-Logik durch
  einen Aufruf von `OsdMeterParser.TryParse(...)`. Verhalten
  unverändert; einziges Resultat: die deterministische Schicht ist
  testbar separiert.
- **1c (optional, wahrscheinlich nicht nötig).** Falls sich später
  zeigt, dass die volle I/O-Pipeline (Snapshot → Vision → Parse) als
  Service gebraucht wird, kommt dann ein `IOsdMeterReader`. Heute
  reicht der Parser plus eine Window-Methode, die Snapshot + Vision
  inline orchestriert.

### Schritte 2–6

2. **VM-API für Frame-Readiness:** `IsFrameReady`-Property,
   `RecordFrame(LiveDetection)`-Methode, `ResetFrameReadiness`-
   Methode auf `CodingSessionViewModel`. Felder + Enum mitnehmen.
3. **`CodingModeWindow.LiveLoop.cs`**-Partial mit
   `RunLiveAnalysisAsync` (Loop-Methode aus PlayerWindow portiert
   und auf VM-API umgebogen). Loop-CTS aus Q3.
4. **Single-Frame-Pfad anpassen:** existierender
   `BtnAnalyzeFrame_Click` ruft die Loop-Methode mit `oneShot=true`-
   Parameter — gleicher Pfad, eine Iteration.
   → **UI-Smoke-Test fällig** (siehe unten).
5. **PlayerWindow-Pendant löschen:** `RunCodingAnalysisAsync`,
   Frame-Readiness-Methoden, OSD-Reader, alle 5 Felder. Bridge-
   Methoden umbiegen (`TrySeekTo` etc.).
   → **UI-Smoke-Test fällig vor diesem Schritt** (siehe unten).
6. **Pause-Confirm-Workflow** als separater Sub-Slice nach
   Stabilisierung — eigenes Mini-ADR.

### Verifikation pro Schritt

- **1a, 1b, 2, 3:** Build + Tests reichen (deterministisches Refactor,
  keine User-sichtbare UI-Änderung).
- **4:** **UI-Smoke-Test fällig** — Single-Frame-Pfad läuft jetzt
  über die neue Loop-Methode. User klickt einmal auf "Analyse" und
  prüft: Status-Anzeige, Result-Panel, AI-Overlay erscheinen wie
  vorher.
- **5:** **UI-Smoke-Test fällig BEVOR PlayerWindow gelöscht wird** —
  Test der vollständigen Live-Coding-Session: Session starten, durchs
  Video navigieren, KI feuert mehrere Frames, Akzept/Edit/Reject
  funktioniert, Session abschließen erzeugt Protokoll.
- **6:** Eigene ADR + eigener Smoke-Test, weil Pause-Confirm ein
  separater Workflow ist.

## Was nicht in diese ADR gehört

- **Konkrete Methoden-Signaturen.** Die kommen im jeweiligen Step.
- **Pause-Confirm-Workflow-Migration.** Eigenes Mini-ADR.
- **Auto-BCD/BCE-Logik.** Eigene Iteration nach Live-Loop steht.
- **PlayerWindow.Coding\*-Löschung.** Slice 8a.7 laut Konsolidierungs-
  ADR.

## Entscheidungs-Protokoll

User-Review am 2026-05-09 ~22:00 lokal:

1. **Q1=C, Q2=C, Q3=A, Q5-Verteilung:** zugestimmt. Begründung des
   Users: "bester Kompromiss: Session-/Readiness-State ins ViewModel,
   Capture nah am Window, AI-Aufruf im bestehenden Service,
   Orchestrierung erstmal als Window-Partial. Hält den Slice machbar,
   ohne sofort alles neu zu erfinden."
2. **Schritt 1 wird in 1a/1b/(1c optional) verfeinert.** 1a kapselt
   nur die deterministische Pixel-zu-Meter-Parsing-Logik ohne neue
   Ollama-/AI-Abhängigkeit. 1c bleibt offen für den Fall, dass eine
   echte I/O-Service-Schicht später nötig wird.
3. **Smoke-Test-Gates:** nach Schritt 4 (Single-Frame über neue Loop)
   und vor Schritt 5 (PlayerWindow-Löschung). Alle anderen Schritte
   nur Build + Tests.

Slice 8a.3 ist freigegeben und startet mit Schritt 1a.
