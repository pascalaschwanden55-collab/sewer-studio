# Code-Deepscan & Verbesserungsplan — 21.07.2026

**Für:** Opus (Abarbeitung in kleinen, getesteten Paketen)
**Basis:** Arbeitskopie `feature/gis-karte` vom 20./21.07.2026 (inkl. unkommittierter Änderungen)
**Methode:** 8 parallele Analyse-Dimensionen (Architektur/DI, WPF-UI, Import/Export/Backup,
KI-Pipeline C#, Sidecar Python, Fehlerbehandlung/Nebenläufigkeit, Test-Suite, Duplikate/toter
Code), jeder ernste Befund adversarial gegengeprüft. Zwei Agentenläufe wurden vom
Sitzungslimit unterbrochen; die sechs schwersten offenen Befunde wurden danach von Hand
direkt am Code verifiziert. Der Architektur-Scan ist zweimal am Limit gescheitert —
Architektur-Aussagen stammen aus den Nachbar-Scans und sind entsprechend markiert.

---

## 1. Gesamtbewertung

Die Codebase ist für ein Solo-Projekt dieser Grösse (ca. 230'000 Zeilen C#, 1'900
Testdateien, 57 Python-Dateien) **aussergewöhnlich diszipliniert**. Alle unabhängigen
Prüfer kamen zum selben Bild:

**Stärken (nicht anfassen, weiter so):**
- Zentrale, getestete Robustheits-Bausteine: `ExternalProcessRunner` (Timeout + Kill-Baum),
  `SafeFireAndForget`, `BestEffort` statt stiller catches, `BoundedBackgroundTaskRunner`,
  atomarer Projekt-Save mit Lock und `.bak`.
- Guard-Netz greift: 1000-Zeilen-Ratchet, eingefrorene Fassaden, gepinnter
  ServiceProvider-Zähler (126), `PageViewModelLifecycleTests`, AppData-Isolation.
- WPF-Hygiene: alle geprüften Timer/Events sauber paarig, `async void` nur in Handlern
  mit Fangnetz, VisualTree-Crashmuster flächendeckend durch `VisualTreeSafe` ersetzt.
- Sidecar-Trainingsroute praktisch wasserdicht (SHA-gebundene Namen, Symlink-Abwehr,
  atomares Staging, 23 Security-Tests); keine bare excepts, saubere sync-Route-Disziplin.
- Null TODO/FIXME/HACK im eigenen Code, praktisch kein auskommentierter Code.
- DTO-Verträge C# ↔ Pydantic bis auf einen Default deckungsgleich (Versions-Gate 1.2.0).

**Note: B+ bis A-.** Was fehlt, sind nicht Grundlagen, sondern punktuelle Randpfade, die
an den eigenen guten Standards vorbeigebaut wurden — plus **ein echter P1**, der durch
einen früheren Fix entstanden ist.

---

## 2. Verifizierte Top-Befunde (von Hand am Code bestätigt)

### befund-1 (P1) — Batch-Videoanalyse bricht deterministisch: `OllamaClient` setzt `BaseAddress` auf geteiltem HttpClient

**Dateien:** `src/AuswertungPro.Next.Infrastructure/Ai/OllamaClient.cs:28`,
`src/AuswertungPro.Next.Infrastructure/Ai/VideoAnalysisPipelineService.cs:157-218`,
`src/AuswertungPro.Next.UI/DataPage/DataPageVideoAnalysisController.cs:155-163`

**Mechanik (verifiziert):**
1. `DataPageVideoAnalysisController` **cacht HttpClients je Timeout über Läufe hinweg**
   (`_httpClients`-Dictionary; eingeführt als Deepscan-Fix A2-04 am 13.07.).
2. `VideoAnalysisPipelineService.RunAsync` schickt zuerst den detaillierten
   Sidecar-Health-Check über genau diesen Client (`ShouldUseMultiModelAsync`, Z. 287) —
   damit gilt der HttpClient für .NET als „gestartet".
3. `CreateOllamaClient()` (Z. 160 Multi-Model bzw. Z. 187 Ollama-Only, außerdem
   `FullProtocolGenerationService` in Z. 218) setzt anschließend
   `_http.BaseAddress = baseUri` (OllamaClient.cs:28, **bedingungslos, auch auf fremden
   Clients**). .NET wirft dann immer `InvalidOperationException`
   („Properties can only be modified before sending the first request").

**Konsequenz:** Multi-Model-Batch scheitert schon im **ersten** Lauf direkt nach dem
Health-Check. Der Ollama-Only-Weg scheitert spätestens **nach** der kompletten
Phase-1-Analyse beim Aufbau des Code-Mappings (Z. 218) bzw. ab dem zweiten Lauf.
Der A2-04-Fix vom 13.07. hat diesen Fehler eingebaut (vorher frischer Client pro Lauf).
Kein Test führt `RunAsync` durch (der bestehende Guard schützt nur `VisionPipelineClient`).

**Fix (klein):** In `OllamaClient` die `BaseAddress` nur bei eigenem Client setzen
(`if (_ownsHttp) _http.BaseAddress = baseUri;`) und bei fremdem Client alle Anfragen mit
absoluten URIs bauen (`new Uri(baseUri, "/api/chat")` statt relativer Pfade).
**Testpflicht:** Durchstich-Test für `RunAsync` mit Fake-Handler, der (a) Health-Check
plus Ollama-Aufruf über DENSELBEN HttpClient fährt und (b) einen zweiten Lauf mit dem
bereits benutzten Client ausführt. Aufwand: S–M.

### befund-2 (P1) — Sidecar-Totalausfall mitten im Video endet als „Erfolg" mit Teilergebnis

**Datei:** `src/AuswertungPro.Next.Infrastructure/Ai/Pipeline/MultiModelAnalysisService.cs:422-433`
(gleiches Muster bei den DINO-/SAM-Schritten)

**Verifiziert:** Der Frame-Loop fängt pro Frame `catch (Exception)`, loggt eine Warnung,
meldet den Fehler nur in der Laufzeit-Fortschrittsanzeige und macht `continue`. Stirbt
der Sidecar bei Frame 50 von 500, laufen 450 Frames als Einzelfehler durch; der Lauf
endet mit `IsSuccess=true` und dem Teilbestand. Es gibt keinen Abbruch nach gehäuften
Folgefehlern und **kein Degraded-Flag im Endergebnis** — ein darauf gebautes Protokoll
sieht aus wie ein sauberes Rohr. Der Health-Check läuft nur einmal vor dem Lauf.

**Fix:** Fehlerzähler im Loop (z. B. ≥ 5 aufeinanderfolgende Sidecar-/Transportfehler →
Lauf als fehlgeschlagen abbrechen) plus `Degraded`-Kennzeichen im `VideoAnalysisResult`
für Läufe mit Einzelfehlern; UI weist es aus. **Achtung:** Datei steht bei exakt
1000 Zeilen (Ratchet-Anschlag) — zuerst AP-7 (Qwen-Block extrahieren), dann diesen Fix.
Aufwand: M.

### befund-3 (P2) — Zwei Store-Instanzen schreiben `training_samples.json` mit getrennten Locks

**Dateien:** `src/AuswertungPro.Next.UI/ServiceProvider.cs:380-387`,
`src/AuswertungPro.Next.Infrastructure/Ai/Training/TrainingSamplesStore.cs:8`

**Verifiziert:** Der ServiceProvider baut eine eigene `TrainingSampleFileStore`-Instanz;
die eingefrorene Fassade `TrainingSamplesStore` hält eine **zweite** (`Default = new()`).
Beide zeigen auf dieselbe JSON; das Schreib-Lock ist aber ein Instanz-Lock. Mehrere
Self-Training-Pfade nutzen hart die Fassade (`SelfTrainingRunCommandRequestFactory.cs:52`,
`SelfTrainingRuntimeSetupController.cs:70`, `SelfTrainingSessionController.cs:79`), Review/
Prüfplatz nutzt die ServiceProvider-Instanz. Laufen beide gleichzeitig (Self-Training-Batch
plus Prüfplatz-Speichern), ist Lost-Update möglich: frisch gemergte Samples eines Weges
verschwinden still beim Save des anderen. Der Eval-Schutz wurde auf beiden konfiguriert
(Z. 382+386) — die Doppel-Instanz selbst wurde übersehen.

**Fix (3 Zeilen):** ServiceProvider verwendet die Fassaden-Instanz statt einer eigenen
(`TrainingSamples = TrainingSamplesStore.Current` + Pfad-/Eval-Konfiguration einmalig),
oder die Fassade delegiert an eine gemeinsam registrierte Instanz. Danach Guard-Test:
genau EINE `TrainingSampleFileStore`-Instanz prozessweit (Instanzgleichheit
`sp.TrainingSamples == TrainingSamplesStore.Current`). Aufwand: S.

### befund-4 (P2) — KINS-Import wischt gefüllte Felder leer und benennt Haltungen um

**Datei:** `src/AuswertungPro.Next.Infrastructure/Import/Kins/KinsImportService.cs:160-162, 471-480`

**Verifiziert mit Abschwächung:** `ApplyImportedField` hat keinen Leer-Schutz —
`Nutzungsart` und `Rohrmaterial` (Z. 161-162) werden bedingungslos gesetzt, auch mit
Leerstring. Handbearbeitete Werte sind durch `HaltungRecord.SetFieldValue`
(HaltungRecord.cs:60-61) geschützt; **nicht** handbearbeitete Werte aus höherwertigen
Quellen (XTF) können bei einem KINS-Nachimport still geleert werden. Zusätzlich wird
`Haltungsname` (Z. 160) entgegen der Merge-Regel („Key wird nie gemerged") überschrieben,
wenn der Record per Grenz-Präfix gefunden wurde. WinCan/IBAK/KINS laufen generell an
der MergeEngine (Prioritäten, Konfliktprotokoll, `fillMissingOnly`) vorbei — XTF/PDF sind
die einzigen Wege mit vollem Schutz.

**Fix kurzfristig:** Leer-Guard in `ApplyImportedField` (leer überschreibt nie gefüllt)
und `Haltungsname` vom Feld-Update ausnehmen. **Mittelfristig (eigenes Paket, mit
Rückfrage):** WinCan/IBAK/KINS auf die MergeEngine umstellen. Aufwand: S (kurzfristig).

### befund-5 (P2) — Vollsicherung: fehlende Quelle wird still aus dem Spiegel rotiert, Lauf meldet Erfolg

**Datei:** `src/AuswertungPro.Next.Infrastructure/Backup/DirectoryMirror.cs:86-87` (+ `RemoveOrphans`)

**Verifiziert:** Existiert ein konfigurierter Quellordner beim Lauf nicht (getrenntes
Laufwerk, umbenannter Ordner, falsch gesetzter KnowledgeRoot), kehrt `MirrorSourceAsync`
still zurück und liefert null erwartete Zielpfade. `RemoveOrphans` verschiebt den
bisherigen Spiegelinhalt dieser Quelle daraufhin nach `_Versionen`; der Lauf endet ohne
Fehler oder Warnung. Kein sofortiger Datenverlust (Versionen bleiben), aber die aktuelle
Sicherung ist unbemerkt unvollständig — und bei begrenzter Versions-Aufbewahrung altert
der Bestand heraus.

**Fix:** Fehlende Quelle als Warnung in `MirrorStats.Errors` aufnehmen und im
Ergebnisdialog/Log ausweisen; optional: bei fehlender Quelle deren bisherigen
Spiegelbestand NICHT als verwaist behandeln. Test: Lauf mit einer fehlenden Quelle →
Warnung sichtbar, Spiegelbestand bleibt. Aufwand: S.

### befund-6 (P2) — Test-Race: `AiSettingsTests` setzt Umgebungsvariablen außerhalb der `EnvironmentVars`-Gruppe

**Datei:** `tests/AuswertungPro.Next.Pipeline.Tests/AiSettingsTests.cs` (Z. 201-211)

**Verifiziert:** Drei Klassen in Pipeline.Tests tragen `[Collection("EnvironmentVars")]`,
`AiSettingsTests` nicht — setzt aber `Environment.SetEnvironmentVariable`. Ohne
Assembly-weites `DisableTestParallelization` laufen Klassen parallel → sporadisch rote
oder falsch grüne Tests. **Fix:** `[Collection("EnvironmentVars")]` ergänzen; kleiner
Meta-Test, der alle Env-Var-setzenden Testklassen auf die Collection prüft. Aufwand: S.

### Gegengeprüfte Sidecar-Befunde (alle P3, in einem Paket bündelbar)

Von den Prüfer-Agenten bestätigt, mit korrigierten, kleinen Lösungen:

1. **OOM-/CUDA-Handler blockiert die Eventloop** (`main.py:108-113`): Aufräumarbeit per
   `run_in_threadpool` ausführen (beide Zweige). Blockiert heute /health für Sekunden im
   seltenen OOM-Pfad; C# stuft den Sidecar dadurch NICHT als tot ein (15-Min-Timeout).
2. **`_slots`-Races im GpuModelManager** (`gpu_manager.py:71-74, 109-114, 142-200`):
   lesende Iterationen auf Kopie unter `_global_lock`; in `unload` Attribute auf `None`
   statt `del`; KEINE Kopplung an die Predict-Locks (Deadlock-Risiko). Ein
   Mehr-Thread-Test auf CPU-Dummies. Deckt sich mit offenem Backlog-Punkt M4.1
   (VERBESSERUNGSPLAN-NOTE-A-2026-07-09).
3. **SAM-Box-Loop verschluckt CUDA-OOM** (`sam_wrapper.py:189-192`): OOM-/CUDA-Fehler
   gezielt re-raisen; Erkennungshelfer in eigenes Modul `cuda_errors.py` (nicht aus
   `main.py` importieren — Importzyklus). Gleiches Muster in `dino_wrapper.py:149-159`
   als Folgeschritt. GPU-freier Test: OOM in predict → 503.

---

## 3. Plausible Befunde ohne abgeschlossene Gegenprüfung

Diese wurden von den Scans (teils zweimal unabhängig) gemeldet, die Gegenprüfung fiel dem
Sitzungslimit zum Opfer. **Vor dem Fix jeweils kurz selbst verifizieren.**

| # | Befund | Datei | Schwere |
|---|---|---|---|
| U1 | Einzelframe-/Codierpfad ignoriert Degraded-Flags von DINO/SAM (Modellfehler wird als grünes „kein Schaden" angezeigt) | `SingleFrameMultiModelService.cs` (~Z. 160) | P2 |
| U2 | ffmpeg-Direktaufrufe ohne Timeout/Kill im Fallback-Pfad; eigener duplizierter ffprobe-Weg statt `VideoProbeService` | `VideoFullAnalysisService.cs` (Z. 265-391) | P2 |
| U3 | ffmpeg-Hänger beendet `VideoFrameStream` still als Normalende | `VideoFrameStream.cs` (~Z. 106) | P2 |
| U4 | Benutzeränderungen während laufendem Import gehen beim Projekt-Swap verloren (Nebenläufigkeitsfenster) | `ImportRunWorkflowController.cs` (~Z. 236) | P2 |
| U5 | Protokoll-Neuerzeugung überschreibt `_E.pdf` nicht-atomar | `ProtocolRegenerationAdapter.cs` (~Z. 89) | P2 |
| U6 | „Nur fehlende Felder ergänzen" wird von 4 von 5 Importwegen ignoriert | `IImportServices.cs` / Importer | P2 |
| U7 | QualityGate-Evidenz: YoloConf binär 1.0 auch für Bypass-Frames | `MultiModelAnalysisService.cs` (~Z. 708) | P2 |
| U8 | Ein fehlgelesener OSD-Meter vergiftet die Meter-Timeline (Max-Sperre, kein Abgleich mit Haltungslänge) | `MultiModelAnalysisService.cs` (~Z. 757) | P2 |
| U9 | Toter KI-Service-Cluster ~550 Zeilen (u. a. `McDropoutService`), nur von Tests am Leben gehalten | `Infrastructure/Ai/QualityGate/` | P2 |
| U10 | VsaFinding→ProtocolEntry-Mapping doppelt (UI vs. Application), bereits divergiert (`ParseDn` ohne InvariantCulture) | `UI/DataPage/VsaFindingToProtocolEntryMapper.cs` | P2 |
| U11 | Vier Uhrlage-Parser mit unterschiedlicher Fehlersemantik (stiller 12-Uhr-Fallback) | `AiOverlayConverter.cs` u. a. | P2 |
| U12 | Sidecar-Default-Testlauf kann echte YOLO-Gewichte laden/aus dem Internet ziehen | `sidecar/tests/test_yolo.py` (~Z. 41) | P2 |
| U13 | Kein laufweites Netz gegen Schreibzugriffe auf echtes `C:\KI_BRAIN` in Infrastructure.Tests | Infrastructure.Tests | P2 |
| U14 | `VideoFullAnalysisService` (produktiver Fallback) komplett ohne Test | `VideoFullAnalysisService.cs` | P2 |
| U15 | TrainingStudioWindow hält wieder den ganzen ServiceProvider als Fensterfeld (Fenster sind nicht guard-geschützt) | `TrainingStudioWindow.xaml.cs` (~Z. 28) | P2 |
| U16 | WinCan/IBAK: Legacy-Quelle überschreibt XTF-Werte still, ohne Konfliktprotokoll | `WinCanDbImportService.cs` (~Z. 906) | P2 |

Dazu ~40 gegengeprüfte oder zweifach gesichtete P3-Punkte (Duplikate, tote Dateien,
kleine Robustheitslücken) — eingearbeitet in die Pakete unten.

---

## 4. Verbesserungsplan für Opus

**Grundregeln für jedes Paket (nicht verhandelbar):**
1. Erst Verhalten mit Test festhalten, dann ändern. Kein Paket ohne fokussierten Test.
2. Kleine Schnitte; kein Komplett-Umbau; öffentliche Verträge stabil halten.
3. `dotnet build AuswertungPro.sln` + volle Testsuite grün vor jedem Commit.
4. 1000-Zeilen-Ratchet beachten: vor Fixes an `MultiModelAnalysisService.cs` (1000 Z.)
   und `TrainingCenterViewModel.cs` (997 Z.) zuerst die vorgesehene Extraktion.
5. Plausible Befunde (Abschnitt 3) vor dem Fix kurz am Code verifizieren.
6. KI-Exportsperren, Eval-Schutz und bewusste Grenzen (CLAUDE.md) nie umgehen.

### Paket AP-1 — KI-Batch wieder lauffähig machen (P1, ZUERST)
- befund-1 fixen (`OllamaClient` BaseAddress nur bei eigenem Client; absolute URIs sonst).
- `RunAsync`-Durchstich-Test mit Fake-Handler inkl. Wiederverwendungs-Fall.
- Dabei U2 kurz mitprüfen: `VideoFullAnalysisService` auf `VideoProbeService` umstellen
  (drei Inline-ffprobe/ffmpeg-Methoden löschen) — beseitigt zugleich den
  Kill-on-Cancel-Mangel. Aufwand gesamt: M.

### Paket AP-2 — Lauf-Ehrlichkeit der Pipeline (P1/P2)
- Vorarbeit (Ratchet): Qwen-Anreicherungsblock (Z. 715-830) als eigenen internen Schritt
  aus `MultiModelAnalysisService` extrahieren (~120 Zeilen Luft, keine API-Änderung).
- befund-2: Folgefehler-Zähler + Abbruch + `Degraded`-Kennzeichen im Ergebnis; UI-Ausweis.
- `_lastFinding = null` in `AnalyzeAsync` neben `_codeVoting.Reset()` (Kontext-Leck).
- U1 verifizieren/fixen: Einzelframe-Pfad übernimmt dieselben Degraded-Regeln wie Batch.
- U7/U8 verifizieren; wenn bestätigt: echte YoloConf in Evidenz, Meter-Plausibilisierung
  gegen Haltungslänge. Aufwand: M–L.

### Paket AP-3 — Trainingsdaten-Sicherheit (P2)
- befund-3: eine einzige `TrainingSampleFileStore`-Instanz + Instanzgleichheits-Guard.
- `AnnotationWorkbenchService.SaveAsync`: Schritte nach dem Sample-Save (KB-Index,
  MergeOrUpdate) wie den Teacher-Schritt absichern — keine falsche
  „Nicht gespeichert"-Meldung nach erfolgreichem Save.
- `CodingSessionService`: Events-Snapshot vor Fire-and-Forget-Persistierung;
  `_ollamaConfigProvider()`-Aufruf in den try-Block (Z. 327). Aufwand: S–M.

### Paket AP-4 — Import-Gerechtigkeit (P2)
- befund-4 kurzfristig: KINS-Leer-Guard + Haltungsname ausnehmen (+ Tests je Feldfall).
- IBAK/`KanalImportDistributionService`: `CreateNewRecord`+`AddRecord` statt
  `project.Data.Add` (NR-Vergabe + Duplikatschutz vereinheitlichen).
- U4/U5/U6/U16 verifizieren; kleine Fixes direkt, die MergeEngine-Vereinheitlichung
  WinCan/IBAK/KINS als eigenes, abgestimmtes Folgepaket vorschlagen (nicht still umbauen).
- MergeEngine: try/catch in die Feldschleife (Feldname ins Log, Rest des Records weiter
  mergen). Aufwand: M.

### Paket AP-5 — Backup-Sichtbarkeit (P2)
- befund-5: Warnung bei fehlender Quelle + Spiegelbestand fehlender Quellen nicht
  ausräumen; Test mit fehlendem Quellordner. Aufwand: S.

### Paket AP-6 — Sidecar-Härtung (P3-Bündel)
- Die drei gegengeprüften Fixes aus Abschnitt 2 (run_in_threadpool, `_slots`-Kopien +
  `None` statt `del`, OOM-Re-Raise via `cuda_errors.py` in SAM und DINO).
- Klein dazu: `SamResponse.error` bei Predict-Fehlern setzen; `queue_wait_ms` echt messen
  (3 Zeilen); `classifier_loaded`-Default in C# auf `false` (fail-closed);
  Auth-Middleware non-ASCII-Header → 401 statt 500; Python-Telemetrie-Rotation (10-MB-Regel
  spiegeln). Aufwand: M.

### Paket AP-7 — Test-Hygiene (P2/P3)
- befund-6: `[Collection("EnvironmentVars")]` für `AiSettingsTests` + Meta-Guard.
- `VisionPipelineClientTests`: echten Connect auf Port 19999 durch Stub ersetzen.
- U12/U13 verifizieren: Sidecar-Tests ohne echte Gewichte/Downloads; Isolations-Netz für
  `C:\KI_BRAIN` in Infrastructure.Tests.
- U14: Charakterisierungstests für `VideoFullAnalysisService` (mind. Erfolgs-, Fehler-,
  Abbruchpfad mit Fake-Client).
- ExcelExportTests auf TempDir-Helfer (Leck), KnowledgeBackup-Zeitfenster-Assert
  deterministisch machen. Aufwand: M.

### Paket AP-8 — Aufräumen: Duplikate & toter Code (P3-Batch, risikoarm)
- Tote UI-Dateien löschen (vorher Referenz-Gegencheck): `PresetCatalogStore`, `GlowPulse`,
  `PropRow`, `UnfrozenDataGrid`, `MeasureTemplateBuilder`; `tools/`-Reste
  (`MdbSchemaReader.cs`, `MdbVideoMapping.cs`, zwei generierte .txt).
- U9 verifizieren → toten QualityGate-/Monitoring-Cluster entfernen (Tests mit).
- `FileContentsEqual` (3 Varianten) und Unique-Path-Strategien (4 Varianten, inkl.
  `return path`-Fallback → Exception) auf je einen Helfer.
- Converter-Doppel (InvertBool ×2, Zustandsklasse-Brush ×3), CategoryBars/DonutChart-Helfer,
  Geometrie-Cache-Zwillinge, LLM-Request-Aufbau (U10-Umfeld) zusammenführen.
- `SchachtSelectionChanged` anschließen oder entfernen; `SchaechtePage`-Suchtimer im
  Unloaded stoppen; SchaechtePage-Dirty-Weg über `Vm`/`_shell.MarkProjectDirty()`
  (Gegenprüfer-Lösung, inkl. kleinem Guard gegen `App.Current.MainWindow` in Views).
  Aufwand: M (mehrere kleine Commits).

### Paket AP-9 — Proaktive Struktur (vor den nächsten Features)
- `TrainingCenterViewModel` (997 Z.): vor Training-Studio-Etappe 2 einen Teilbereich
  (z. B. Teacher-Galerie-Zustand aus dem Fenster-Code-behind als eigenes ViewModel)
  ausgliedern. U15 dabei verifizieren/fixen (kein Container-Feld im Fenster).
- `SystemMonitorService` (920 Z., UI-Schicht, am DI vorbei): Sensorik-Kern hinter
  Interface nach Infrastructure — nur beim nächsten Anfassen, kein Selbstzweck.
- HttpClient-/Dispose-Konzept: `VisionPipelineClient` nach OllamaClient-Muster
  (`_ownsHttp` + IDisposable); Fabriken teilen zentrale Clients. Aufwand: M–L.

**Empfohlene Reihenfolge:** AP-1 → AP-3 → AP-2 → AP-4 → AP-5 → AP-7 → AP-6 → AP-8 → AP-9.
(AP-1 zuerst, weil produktiv kaputt; AP-3 vor AP-2, weil winzig und Datensicherheit.)

---

## 5. Quick Wins (je < 1 Stunde, unabhängig ziehbar)

1. `OllamaClient`-BaseAddress-Guard (Kern von AP-1).
2. Eine `TrainingSampleFileStore`-Instanz (Kern von AP-3).
3. KINS-Leer-Guard (Kern von AP-4).
4. Backup-Warnung fehlende Quelle (AP-5).
5. `[Collection("EnvironmentVars")]` auf `AiSettingsTests`.
6. `_lastFinding`-Reset in `AnalyzeAsync`.
7. Leeres `catch {}` in `SelfTrainingLastMatchRateRefreshWorkflow` → `BestEffort.ReportWarning`
   (+ `ConfigureAwait(false)` dort entfernen).
8. `OnlineXyzTileSource`: HttpClient-Timeout 10 s.
9. `classifier_loaded`-Default in C# auf `false`.
10. `CodingMultiModelQualityGatePolicy`: Null-Gate-Fallback Rot statt Gelb (wie Live-Policy).

---

## 6. Grenzen dieses Scans

- **Architektur/DI-Dimension:** zweimal am Sitzungslimit gescheitert; Aussagen stammen aus
  Nachbar-Scans (ServiceProvider laut UI-Scan nicht rückfällig; Fassaden-Bestand laut
  Duplikat-Scan schrumpfbar). Ein gezielter Architektur-Nachscan lohnt sich, wenn wieder
  Budget frei ist — Startpunkte: Registrierungen ohne Nutzer, Konstruktions-Reihenfolge im
  ServiceProvider, Interface-Hygiene der Trainings-Export-Kette.
- **Abschnitt-3-Befunde** sind plausibel (teils doppelt unabhängig gesichtet), aber nicht
  adversarial bestätigt — Erfahrungswert aus diesem Scan: von 9 gegengeprüften Meldungen
  wurden 3 in der Schwere herabgestuft und Randannahmen korrigiert. Also: erst lesen,
  dann fixen.
- Laufzeitverhalten (echte GPU-Läufe, 8-h-Soak, UI-Flüssigkeit bei 3000 Videos) war nicht
  Teil dieses Scans.
