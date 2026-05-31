# Schlussbericht: Codier-Qualität & KB-Pipeline (READ-ONLY-Audit)

> Erzeugt durch Multi-Agent-Workflow (5 Tracer → Kausalkette → adversariale Doppel-Verifikation → Bericht).
> 29 Agenten, ~2,1 Mio Tokens. Read-only, keine Codeänderung.
> Ergebnis der Verifikation: 10 Behauptungen überlebt, 0 unklar, 1 widerlegt; 17 Phantome.

Stand: Checkout HEAD `0bd6667d`, Branch `feature/gis-karte`. Alle Aussagen mit Code-Beleg. `.claude/worktrees/*` ignoriert. Hinweis zu Pfaden: Mehrere Belege zitieren `...UI/Ai/...` — der real aktive Produktivcode liegt in `...Infrastructure/Ai/...` (die UI-Variante existiert nur in den ignorierten Worktrees). Inhalt/Zeilen stimmen mit den Infrastructure-Dateien überein.

---

## A. Korrigierte Architektur-Landkarte

| Thema | Wahrheit im Code | Behauptung in Doku | Abweichung | Datei:Zeile |
|---|---|---|---|---|
| Detection (YOLO) | Läuft ausschliesslich im Python-Sidecar (`model.predict`); bei fehlenden Custom-Gewichten Fallback auf COCO `yolo11m.pt` (nur Bildqualitäts-Vorscreening). C# ruft nur via HTTP. | „YOLO26m-seg permanent GPU" | Doku nennt -seg/permanent; real Detection-Default + Fallback-Risiko | `sidecar/.../yolo_wrapper.py:362-434,52,116-123`; `MultiModelAnalysisService.cs:153-258` |
| Dedup-Ort | In C#, nicht im Sidecar. Zwei nahezu identische Implementierungen (`MultiModelAnalysisService.UpdateActive` und `VideoFullAnalysisService.UpdateActive`). | — | Doppelte Logik mit einer Divergenz (siehe k6) | `MultiModelAnalysisService.cs:667-714`; `VideoFullAnalysisService.cs:195-247` |
| Dedup-Mechanik | Frame-Zähler `DedupWindowFrames` (Default 3) + Key aus Code/Label+Uhrlage. Kein Meter-Radius. | — | — | `MultiModelAnalysisService.cs:34,693-698,782-789` |
| Tracking (ByteTrack/OC-SORT) | Existiert nicht. Kein `track_id`, kein Multi-Object-Tracking — weder C# noch Sidecar. | „ByteTrack/OC-SORT → CPU, immer aktiv" | Widerspricht Doku | `sidecar/sidecar/` (0 Treffer); CLAUDE.md:15 |
| DetectionAggregator / Temporal Voting | Existiert nicht in HEAD. Nur auf nicht-gemergten Branches (`342ca566`) + als veraltetes DLL-Symbol. | „DetectionAggregator → Temporal Voting" | Widerspricht Doku | `git grep HEAD` → nur CLAUDE.md:37 |
| InferenceOrchestratorService | Kein C#-Typ dieses Namens. GPU-Slot-Steuerung liegt im Sidecar (`gpu_manager.py`). | „InferenceOrchestratorService → Zustandssteuerung GPU" | Widerspricht Doku | Grep src: NONE; `sidecar/.../gpu_manager.py:16-101` |
| QualityGate-Signale | 8 Signale existieren als Felder; je nach Pfad nur 2–6 tatsächlich befüllt. | „8 Signale" | Teilweise (Befüllung lückenhaft) | `EvidenceVector.cs:9-19`; `QualityGateService.cs:41-49` |
| CategoryWeights adaptiv? | Statisch. `SetWeights`/`LoadWeights` haben keinen produktiven Aufrufer; immer `CategoryWeights.Default()`. | „Learned weights persisted/used" (Klassen-Doku) | Widerspricht Doku (Lernkreis offen) | `QualityGateService.cs:34-37`; `WeightLearningService.cs:51,61,209` |
| KB-Schreibpfad | Schreibt `Samples`+`Embeddings` (11 Spalten). Keine Status-/Ampel-/Red-Spalte. Gate = `IsIndexWorthy` (Textlänge≥10 + Code-Existenz) + Aufrufer-Filter `Status==Approved`. | Implikation „QualityGate entscheidet KB-Eintrag" | Widerspricht Doku/Annahme | `KnowledgeBaseContext.cs:45-67`; `KnowledgeBaseManager.cs:34-38,339-348` |
| KB-Dedup (Cosine) | Kein Dedup beim Schreiben, keine Schwelle. Cosine nur für Top-K-Retrieval. | „KbDeduplicationService / Cosine-Dedup" | Widerspricht Annahme | `RetrievalService.cs:159-171,45-74` |
| Sidecar-Lifecycle | Kein managed Start, keine DI-Registrierung, keine `PythonSidecarService`. Externer HTTP-Dienst, Fallback auf Qwen bei `health==null`. | Annahme „App startet/verwaltet Sidecar" | Widerspricht Annahme | `PlayerWindow.Coding.cs:2451-2474`; App.xaml.cs (0 Treffer) |
| Sidecar-HTTP-Vertrag | Sauber: snake_case beidseitig, Confidence 0..1, BBox xyxy-Pixel, alle 6 Endpoints existieren. Einziger Verlust: C# verwirft Health-Extra-Felder (harmlos, einseitig). | — | Nur dokumentierter Mapping-Verlust | `VisionPipelineDtos.cs:8-18`; `health.py:14-26` |
| SAM-Version | `sam_model_type='vit_h'` (SAM1) im Ordner `sam3`; YOLO-Default `yolo26m.pt` (Detection, kein -seg). | „SAM 3", „YOLO26m-seg" | Widerspricht Doku (Begriffe) | `sidecar/sidecar/config.py:44,74`; `sam_wrapper.py:23` |
| Live-Akzeptanz | Asymmetrisch: Qwen-Pfad setzt alle KI-Events auf `Ignored` („KI darf nicht selbst akzeptieren"); Multi-Model-Pfad setzt grün auf `Accepted` ohne Bestätigung. | — | Zwei widersprüchliche Regeln | `PlayerWindow.Coding.cs:3550-3558` vs `:2999-3002` |

---

## B. Phantom-Liste

| Name | Wer behauptet | Warum nicht echt | Beleg |
|---|---|---|---|
| DetectionAggregator (Temporal Voting) | CLAUDE.md:37 | Klasse nur auf nicht-gemergten Branches (`342ca566`); kein Vorfahr von HEAD/master. Im Checkout nur als veraltetes DLL-Symbol. | `git grep DetectionAggregator HEAD` → nur CLAUDE.md; `merge-base --is-ancestor` = NO |
| meterMergeRadius=1.5 | Frageannahme | Gehört zu DetectionAggregator (nur Branch). Im HEAD kein meter-basierter Merge. | `git show 342ca566:.../DetectionAggregator.cs:11,23,46-47`; src-Grep 0 Treffer |
| ByteTrack / OC-SORT | CLAUDE.md:15 | Kein echtes Tracking, kein `track_id`. Nur namensähnliche UI/Monitoring-Klassen. | `sidecar/sidecar/` 0 Treffer |
| InferenceOrchestratorService | CLAUDE.md:36 | Kein C#-Typ. GPU-Slots im Sidecar. | Grep src: NONE; `gpu_manager.py:16-101` |
| Adaptive/gelernte CategoryWeights zur Laufzeit | WeightLearningService-/CategoryWeights-Doku | Gewichte werden geschrieben, nie zurückgeladen. Beide Gates `new QualityGateService()` ohne Gewichte → immer Default. | `QualityGateService.cs:34-37`; `WeightLearningService.cs:51,61` |
| SamMaskStability/QwenVisionConf im Batch-Pfad | Inline-Kommentare `MultiModelAnalysisService.cs:391-392` | `frameEvidence` wird nie reassigned; Felder bleiben null. Kommentare = nicht umgesetzte Absicht. | `MultiModelAnalysisService.cs:388-394`; `PipelineConfig.cs:21` |
| Echte YoloConf im Multi-Model-Live-Pfad | Feldname suggeriert echten Score | Live-Pfad: Festwert 0.8; Batch-Pfad: binär 1.0/0.0 (IsRelevant). Kein Modell-Score. | `PlayerWindow.Coding.cs:2972`; `MultiModelAnalysisService.cs:389` |
| KbDeduplicationService | Architektur-Skill (Vorgabe) | Klasse existiert nicht. Kein Dedup beim KB-Schreiben. | Glob `**/*Dedup*.cs` → keine Datei |
| QualityGate-Ampel-Gate vor KB-Index | Implikation CLAUDE.md / Frage | KB-Manager referenziert kein QualityGate. Nur `IsIndexWorthy` + `Status==Approved`. | `KnowledgeBaseManager.cs:339-348` |
| Status-/Ampel-Spalte in KB-DB („96,2% Red/15703") | Tracer-Behauptung | 11 Spalten, keine Red-Spalte. Live-DB leer (Samples=0). Zahl nirgends belegbar. | `PRAGMA table_info(Samples)`; Negativ-Grep |
| PythonSidecarService | Tracer-D-Annahme | Existiert nicht. Sidecar extern, nur HTTP-Client. | Volltext-Grep `*.cs` |
| App.xaml.cs Sidecar-Registrierung | Tracer-D-Annahme | Kein DI, kein Startup-Hook. Clients ad-hoc `new VisionPipelineClient(...)`. | App.xaml.cs (0 Treffer) |
| BatchSelfTrainingOrchestrator | Tracer-E-Annahme | Existiert nicht. Nur String-Konstante `BatchImport`. | Grep 0 Treffer |
| VideoSelfTrainingOrchestrator | Tracer-E-Annahme | Existiert nicht. Video-Fallback in `SelfTrainingOrchestrator` integriert. | `SelfTrainingOrchestrator.cs:78-142` |
| AutoApprovalService | Code-Existenz / Anspruch | Toter Code, nie instanziiert. Auto-Akzeptanz steckt inline im Multi-Model-Pfad. | `AutoApprovalService.cs:5-31`; Grep `new AutoApprovalService` 0 |
| Florence-Shadow / Florence-Modell | Tracer-E-Annahme | Kein Florence. „Shadow" = nur VSA-Telemetrie. | Grep `Florence` 0 Treffer |
| Teacher (autonomes Lehrer-System) | Doku/Namespace `Ai.Teacher` | Nur Export-/Annotations-Mechanismus für manuelle Overlays, kein Lern-/Akzeptanz-Agent. | `CodingEventToSampleMapper.cs:35` |

---

## C. Root-Cause-Kette der Codier-Qualität

### Belegte Kette (Status „ueberlebt", doppelt verifiziert)

**Vorbemerkung (k0, k1 — Prämissen):** Die Annahme „KB flutet mit Red-Samples" ist falsch. Die KB-SQLite hat **keine Red-/Ampel-Spalte** und ist aktuell **leer** (Samples=0). Die Zahl „96,2% Red/15703" hat keine Quelle. Die QualityGate-Ampel ist **kein Gate** für den KB-Schreibpfad — einziges Gate ist das formale `IsIndexWorthy` (Textlänge≥10 + Code existiert im Katalog) plus Aufrufer-Filter `Status==Approved`. Ein fachlich falscher, aber syntaktisch gültiger Code (z.B. BAB statt BBA) passiert ungehindert.
*Beleg: `KnowledgeBaseContext.cs:45-67`; `KnowledgeBaseManager.cs:34-38,339-348`.*

Die eigentliche Qualitätskette betrifft also **was und mit welchem (Fehl-)Code/Meter-Bereich** überhaupt akzeptiert und später indiziert wird:

1. **(k2) Auto-Akzeptanz grüner Multi-Model-Befunde.** Im Primärpfad (gesunder Sidecar) gilt `Decision = gateResult.IsGreen ? Accepted : Ignored` — **ohne** menschliche Bestätigung. Der parallele Qwen-Pfad setzt dagegen jeden Befund hart auf `Ignored`. Zwei widersprüchliche Akzeptanz-Regeln je nach Pfad.
   *`PlayerWindow.Coding.cs:2999-3002` vs `:3550-3558`; `:2454-2458`.*

2. **(k3) Das „Grün" beruht teils auf Festwerten.** Im Multi-Model-Live-Pfad ist `YoloConf` hart auf 0.8 gesetzt (keine echte YOLO-Confidence — sie ist im `SingleFrameResult` gar nicht verfügbar), `PlausibilityScore` 0.8/0.4. Der feste 0.8-Beitrag hebt das Composite Richtung Green. Mit echtem niedrigem YOLO-Wert würde das Ergebnis kippen.
   *`PlayerWindow.Coding.cs:2970-2976` + `QualityGateService.cs:62-71` + `CategoryWeights.cs:14-21`.*

3. **(k10) Gewichte sind statisch, Ampel pfadabhängig.** Beide Gates laufen immer mit `CategoryWeights.Default()`. Folge: Qwen-Pfad `composite=0.12·Severity+0.24` → nur Severity 5 wird grün (Sev4=0.72 < 0.75). Multi-Model-Pfad wird durch Festwert-Evidenz (k3) leicht grün. **Dieselbe Realität → gegensätzliche Ampeln je nach Pfad → inkonsistente Auto-Akzeptanz.** Eine Fehlkalibrierung lässt sich nicht durch gelernte Gewichte korrigieren (Lernkreis offen).
   *`QualityGateService.cs:34-37`; `PlayerWindow.Coding.cs:2446`; `WeightLearningService.cs:51,61`.*

4. **(k8) Akzeptanz → KB-Eintrag.** `Accepted` wird via `CodingEventToSampleMapper.MapDecision` auf `Approved` gemappt; `CodingSessionService.IndexApprovedSamplesToKbAsync` filtert `Status==Approved` → `IndexSampleAsync`. Direkter Kausal-Link grün → Approved → KB (sofern Ollama online und `IsIndexWorthy` ok). Weder ein Mensch noch eine Red-Ampel hält das auf.
   *`CodingEventToSampleMapper.cs:13-20`; `CodingSessionService.cs:203-222`.*

5. **(k9) Wichtige Entschärfung.** Aus dem **PlayerWindow** fliesst nichts direkt in die SQLite-KB — es persistiert nur in den JSON-`TrainingSamplesStore` und ruft `CompleteSession` **nicht** auf. Der KB-Index erfolgt erst später über `CodingModeWindow.CompleteSession` bzw. TrainingCenter. Das erklärt auch die aktuell leere Live-DB (k0): die Index-Pfade wurden kaum durchlaufen. **Die Akutheit ist begrenzt — die Schwachstelle materialisiert sich erst beim späteren Indexieren.**
   *`PlayerWindow.Coding.cs:431-434,466-521`; kein `IndexSample` im PlayerWindow.*

**Merge-/Meter-Achse (verstärkt Fehlcodierung der Bereiche):**

6. **(k4) Über-Mergen ist rein frame-zähler-basiert.** Solange derselbe Key (Code+Uhrlage) innerhalb <3 Frame-Aussetzern wieder auftaucht, wird derselbe Befund verlängert (`MeterEnd=aktueller Meter`) — **unabhängig von der Meter-Distanz**. Kein räumlicher Trenn-Guard im aktiven Code.
   *`MultiModelAnalysisService.cs:34,693-698,924-928`.*

7. **(k6) Pfadabhängiger Punkt/Strecken-Kollaps.** `ResolveMeterEnd` (Punkt → `MeterEnd=MeterStart`) existiert **nur** im Multi-Model-Pfad. Im Ollama-Only-Fallback gibt `ToDetection` `MeterEnd` ungefiltert aus — ein Punktschaden wird fälschlich als Strecke gespeichert. Downstream kippt das sogar das `IsStreckenschaden`-Flag (`FullProtocolGenerationService.cs:390-391` leitet es rein aus der Meter-Spanne ab). Inkonsistente Trainingsdaten je nach aktivem Pfad.
   *`MultiModelAnalysisService.cs:888-891,947-950`; `VideoFullAnalysisService.cs:531,551-553`.*

8. **(k7) Meterstand ohne OSD rein heuristisch.** Initial `EstimateMeter = t/duration · 50m` (Default, nirgends überschrieben), erst danach ggf. Qwen-OSD-Override. VideoFull inkrementell ab `_lastKnownMeter`. **Keine geometrisch verlässliche Meter-Quelle ohne OSD.** Fehlt/falsch die OSD-Lesung, sind alle Meterstände systematisch verschoben; Befunde rutschen bei verzerrter Skala näher zusammen (verstärkt k4).
   *`MultiModelAnalysisService.cs:649-657,432-437`; `VideoFullAnalysisService.cs:407-418`.*

### Geklärt — widerlegt (kein Handlungsbedarf)

- **(k5) „Dedup-Key gröber als VSA-Codierung, Char1/Char2 geht verloren, Riss=Bruch verschmilzt"** → **widerlegt.** `BuildFindingKey` ist eine Prioritätskette: `NormalizeFindingCode(VsaCodeHint) ?? InferCodeFromLabel(Label) ?? NormalizeFindingLabel(...)`. Der volle VSA-Code (inkl. Char1/Char2) wird bewahrt; die grobe Label-Gruppierung greift nur als letzter Fallback. crack/break sind getrennt (`crack` vs `break`), riss→BAB vs bruch→BAC werden nie verschmolzen. Tests (`VideoFullAnalysisServiceTests.cs:16-47`) bestätigen Key=„BBB"/„BBA" statt grober Gruppe. Das „3:00 vs 3:30"-Beispiel ist hypothetisch — die Pipeline erzeugt nur ganze Stunden.
  *`MultiModelAnalysisService.cs:784-786`; `VsaCodeResolver.cs:32-57,136-138`.*

---

## D. Entscheidungsliste für Fixes (keine Implementierung)

| # | Entscheidung | Wirkung | Aufwand | Risiko | Messbarkeit |
|---|---|---|---|---|---|
| D1 | **Auto-Akzeptanz im Multi-Model-Pfad (k2) angleichen** an Qwen-Regel („KI akzeptiert nicht selbst", Bestätigung erzwingen) — oder bewusst beibehalten? Architektur-Entscheid nötig. | Hoch — schliesst die Haupttür für ungeprüfte Codes | Niedrig (eine Decision-Zeile + Confirmation-Routing) | Mittel (ändert Live-UX-Verhalten) | Hoch — Decision-Verteilung in CodingEvents vorher/nachher zählbar |
| D2 | **k3 Festwert-Evidenz ehrlich machen:** echte YOLO-Confidence durch den `SingleFrameResult` propagieren statt 0.8, ODER `YoloConf` aus dem Live-Composite herausnehmen. Entscheid, welcher Weg. | Hoch — verhindert künstlich angehobenes „Grün" | Mittel (DTO/Result um Confidence erweitern, Sidecar liefert sie bereits) | Mittel (verschiebt Ampel-Charakteristik) | Hoch — Composite-Werte vor/nach am gleichen Frame vergleichbar |
| D3 | **k10 Pfad-Asymmetrie der Ampel auflösen:** entweder Lernkreis schliessen (`LoadWeights` in Gate verdrahten) oder Gewichte/Schwellen bewusst pro Pfad dokumentieren+vereinheitlichen. | Hoch — eine Realität soll eine Ampel ergeben | Mittel | Mittel (Schwellen-Verschiebung betrifft alle Läufe) | Hoch — Severity-Sweep deterministisch nachrechenbar |
| D4 | **k6 Punkt/Strecken-Kollaps im VideoFull-Pfad nachziehen** (`ResolveMeterEnd` auch dort), damit beide Pfade konsistente Meter-Bereiche liefern. | Mittel-Hoch — verhindert inkonsistente Trainingsdaten | Niedrig (vorhandene Methode wiederverwenden) | Niedrig | Hoch — selber Punktschaden über beide Pfade, `MeterEnd` vergleichen |
| D5 | **k4 räumliche Trennung erwägen:** optionaler Meter-Distanz-Guard beim Mergen (Wiederbelebung des Branch-Konzepts `meterMergeRadius`), Entscheid ob nötig. | Mittel — trennt weit entfernte gleiche Codes | Mittel | Mittel (kann legitime Streckenschäden zerschneiden) | Mittel — Event-Anzahl/Bereiche an Testvideo vergleichbar |
| D6 | **Doppelte Dedup-Implementierung (MultiModel vs VideoFull) konsolidieren** — Architektur-Entscheid (Thin-AI/Layer-Disziplin beachten). | Mittel — beseitigt Divergenz-Quelle dauerhaft | Hoch (Refactoring, laut CLAUDE.md diskussionspflichtig) | Hoch | Niedrig direkt — indirekt über Konsistenz beider Pfade |
| D7 | **k1/IsIndexWorthy:** Entscheid, ob ein fachliches Plausibilitäts-Gate (über reine Code-Existenz hinaus) vor KB-Index gehört. | Mittel — verhindert syntaktisch-gültige Fehlcodes in KB | Mittel | Niedrig-Mittel | Hoch — KB-Code-Verteilung nach Aktivierung messbar |
| D8 | **CLAUDE.md korrigieren** (Phantome: DetectionAggregator, ByteTrack, InferenceOrchestratorService, adaptive Gewichte, „SAM 3"/„YOLO26m-seg"). | Niedrig technisch, hoch für Vertrauen | Niedrig (Doku) | Niedrig | N/A — reine Doku-Konsistenz |
| D9 | **Toten Code/irreführende Kommentare** entfernen oder umsetzen: `AutoApprovalService`, Inline-Kommentare zu Sam/Qwen-Befüllung. | Niedrig — reduziert Verwirrung | Niedrig | Niedrig | N/A |

Priorisierung: D1–D4 zuerst (höchste Wirkung, niedriger/mittlerer Aufwand, gut messbar). D6 ist das einzige grosse Refactoring — laut CLAUDE.md nicht ohne explizite Diskussion. D8/D9 sind risikoarme Aufräumarbeiten.

---

## Schluss: Was blieb „unklar" und warum

1. **Custom YOLO26m-seg-Gewichte physisch vorhanden?** Im Quellcode nicht belegbar — der Code regelt nur das Verhalten (Fallback + Warnung), nicht die Existenz der Datei auf der Platte. Klärung nur durch Dateisystem-Inspektion der `models/yolo26m/`-Ablage.
2. **Herkunft „833 Rohdetektionen → 55 Events, GT 49".** Im aktiven HEAD nicht belegbar. Das einzige Eval-Tool (`SewerStudio.AiTestRunner`) arbeitet pro Frame, aggregiert nicht zu Events. Vermutlich Artefakt eines anderen Branch-Standes (`DetectionAggregator`).
3. **`meterMergeRadius=1.5`.** Im aktiven Code nicht vorhanden, nur als kompiliertes Symbol in einer veralteten `.tmp`-DLL und im Branch `342ca566`.

Diese drei Punkte konnten READ-ONLY am Code nicht abschliessend geklärt werden (liegen ausserhalb des Codes bzw. auf nicht-gemergten Branches). Keine akuten Risiken im aktiven System — die Live-KB ist leer (k0), und der PlayerWindow-Pfad schreibt nicht direkt in die KB (k9).
