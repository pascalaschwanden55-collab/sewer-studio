# SewerStudio — Verbesserungsplan Richtung Note A (fuer Codex)

**Erstellt:** 2026-07-09 von Fable (Claude)
**Branch/Stand:** `feature/gis-karte`, HEAD `76561e6f5`, Working Tree mit kleinen uncommitteten Aenderungen
**Baseline verifiziert am 2026-07-09:** `dotnet build AuswertungPro.sln` = **0 Fehler, 0 Warnungen**; `dotnet test` = **8075 Tests gruen, 0 rot, 1 uebersprungen** (Pipeline 1638, Infrastructure 2305, UI 4069, ProjectModernizer 62)

## Methode (transparent)

Multi-Agenten-Audit ueber 11 Dimensionen gestartet; 3 Dimensionen (Architektur, God-Classes/MVVM, Python-Sidecar) liefen vollstaendig durch, die uebrigen 8 wurden wegen Session-Limit abgebrochen und stattdessen mit gezielten Direkt-Checks abgedeckt (Build, Testlauf, atomares Speichern, async-Muster, leere catches, VisualTree-Crashmuster, HttpClient-Streuung). Zusaetzlich wurden die frueheren Plaene abgeglichen (ARCHITEKTUR-CODE-AUDIT-2026-07-01, KORREKTURPLAN-OPTIMIERUNG-2026-07, SANIERUNGSMATRIX-KONSISTENZ-PLAN), damit nichts doppelt vorgeschlagen wird.

**Verifikations-Status pro Befund:**
- ✅ = am 09.07. direkt am Code verifiziert (Datei:Zeile gelesen)
- 🔎 = Befund eines Audit-Agenten mit Datei:Zeile-Beleg, aber ohne Zweit-Verifikation — **Codex: vor der Umsetzung die genannte Stelle lesen und Befund bestaetigen; wenn er nicht stimmt, Punkt ueberspringen und im Commit-Text vermerken**

## Gesamturteil

**Note heute: B+.** Fundament ist stark: saubere csproj-Schichtung (Domain ← Application ← Infrastructure ← UI, keine Rueckwaertskanten), 8075 gruene Tests, Build ohne Warnungen, atomares Speichern (`JsonProjectRepository` mit tmp+`File.Replace`, ✅), Architektur-Guard-Tests, Test-Isolation, gepinnter Sidecar-Lock-File. Die Sanierungsmatrix-Pakete K1–K7 aus dem Plan vom 04.07. sind umgesetzt (ChfFormat, UnitKinds, Dirty-Guard, Template-Store, LV-Summen-Tests — ✅ stichprobenhaft geprueft).

**Was Note A verhindert (die 3 grossen Blöcke):**
1. **KI-Vertrauens-Luecken:** QualityGate-Bypass im UI, Sidecar-Health luegt, Trainings-Export kann Trainingsdaten kontaminieren — Fehler hier erzeugen *falsche Inspektionsergebnisse*, das wiegt schwerer als jede Struktursache.
2. **UI-Projekt als Sammelbecken:** ~121k von ~178k Zeilen liegen im WPF-Projekt, davon ~493 Dateien reine Geschaeftslogik ohne WPF-Bezug (UI/Ai). PlayerWindow = 98 partial-Dateien.
3. **DI-Disziplin:** ~99 verstreute `new *Service(...)` in 42 UI-Dateien, teils Doppel-Konstruktion registrierter Services.

---

## STUFE 1 — HOTFIXES (sofort, in dieser Reihenfolge)

### H1 — Branch pushen (Datenverlust-Schutz) ✅ — Aufwand: Minuten
`feature/gis-karte` ist **16 Commits vor `origin`** und nicht gepusht. Ein Platten-/PC-Ausfall kostet mehrere Tage Arbeit (Dashboard, Schacht-Tool, Kosten-Sync).
**Fix:** `git push origin feature/gis-karte`. Vorher nichts umbauen. Danach als Gewohnheit: nach jedem Arbeitstag pushen.

### H2 — QualityGate-Bypass entfernen ✅ — Aufwand: S
**Datei:** `src/AuswertungPro.Next.UI/Ai/CodingLiveFindingQualityGatePolicy.cs:15-20`
**Befund (verifiziert):** Ist `qualityGate == null`, wird ein Fallback-Resultat fabriziert: Befunde mit Severity ≥ 4 werden **Green** markiert — ohne dass das Gate je gelaufen ist. Verstoss gegen das CLAUDE.md-Prinzip „QualityGate muss immer durchlaufen“. Ein schwerer Befund erscheint so als geprueft-vertrauenswuerdig.
**Fix:**
1. Fallback-Zweig konservativ machen: bei `qualityGate == null` → `TrafficLight.Yellow` (nie Green), Reason `"QualityGate nicht verfuegbar"` — analog zum bereits konservativen Fallback in `CodingMultiModelQualityGatePolicy.cs:22-26`.
2. Besser zusaetzlich: `QualityGate` in `CodingAiRuntime` non-nullable machen (`new QualityGateService()` ist zustandslos und billig, siehe `CodingAiRuntimeFactory.cs`) und den null-Pfad ganz entfernen.
3. Test: Severity-5-Finding + `qualityGate=null` darf **nicht** Green ergeben.

### H3 — Sidecar-VRAM-Bug `total_mem` → `total_memory` ✅ — Aufwand: S
**Datei:** `sidecar/sidecar/gpu_manager.py:137`
**Befund (verifiziert):** `torch.cuda.get_device_properties(0).total_mem` — das Attribut heisst `total_memory`. Der AttributeError wird vom `except Exception: pass` verschluckt → `vram_total_gb` ist in jeder `/health`-Antwort **0.0**. Alles, was den VRAM-Zustand daraus liest, bekommt falsche Werte.
**Fix:** `.total_mem` → `.total_memory`; das `except` mindestens auf `logger.debug(...)` aendern, damit so etwas nie wieder unsichtbar ist. Python-Test mit gemocktem torch ergaenzen.

### H4 — Sidecar-Health luegt: Status immer „ok“ ✅ — Aufwand: S (Teil 1) + M (Teil 2)
**Dateien:** `sidecar/sidecar/routes/health.py:29` (✅), `src/AuswertungPro.Next.Infrastructure/Ai/Pipeline/VisionPipelineDtos.cs` (✅ kennt kein `models_present`), `src/AuswertungPro.Next.Infrastructure/Ai/VideoAnalysisPipelineService.cs` (🔎 prueft laut Agent nur `health.Status != "ok"`, Zeile ~222)
**Befund:** `/health` berechnet zwar `models_present.dino/sam`, liefert aber hart `"status": "ok"`. C# liest `models_present` gar nicht. Fehlen DINO/SAM-Gewichte, startet trotzdem der Multi-Model-Pfad — ein Nachtlauf verbrennt Stunden GPU-Zeit und liefert null Befunde.
**Fix:**
1. *(S)* health.py: `status = "degraded"`, wenn `models_present.dino` oder `.sam` false ist.
2. *(M)* C#: `SidecarHealthResponse` um `[JsonPropertyName("models_present")] Dictionary<string,bool>? ModelsPresent` erweitern; in `ShouldUseMultiModelAsync` bei fehlendem dino/sam den Fallback-Pfad waehlen mit Grund „Sidecar degradiert (fehlende Gewichte)“. Je ein Test (Python: degraded bei fehlendem Gewichtsordner; C#: Fallback-Zweig).

### H5 — Trainings-Export kann Trainingsdaten kontaminieren ✅ — Aufwand: M
**Datei:** `sidecar/sidecar/routes/training.py` (✅ Zeilen 48-66 gelesen: `mkdir(exist_ok=True)` ohne Aufraeumen, `class_map` pro Request neu aus sortierten Namen, `random.shuffle` ungeseedet)
**Befund:** Zweiter Export in denselben Ordner laesst Alt-Dateien liegen; da die Klassen-Indices pro Export neu vergeben werden, werden alte Label-Dateien mit **neuen Klassennamen interpretiert** → systematisch falsch gelabelte YOLO-Trainingsdaten → schlechteres Modell → falsche Codierung downstream.
**Fix:** Vor dem Schreiben `images/` + `labels/` leeren (Default `overwrite=true`, per Request-Feld steuerbar) oder HTTP 409 bei nicht-leerem Ordner; `random.Random(seed)` fuer reproduzierbaren Split; Pytest: zweiter Export mit weniger Samples hinterlaesst keine Altdateien.

---

## STUFE 2 — QUICK WINS (je unter ~2 Std, hoher Nutzen)

| # | Was | Dateien / Beleg | Status | Fix kurz |
|---|-----|-----------------|--------|----------|
| Q1 | `/classify/yolo` schluckt Bend-Veto-Fehler und fehlendes cls-Modell still — BCE-Fehlcodierung moeglich (Bogen als Rohrende) | `sidecar/sidecar/routes/yolo.py:66-67`, `yolo_wrapper.py:669-671`, `MultiModelAnalysisService.cs:473/636` | 🔎 | `YoloClassifyResponse` um `classifier_loaded` + `bend_veto_failed` erweitern (analog DinoResponse-degraded); C# markiert Frame bei `bend_veto_failed` als Review statt `IsBend=false` zu vertrauen |
| Q2 | C# etikettiert **jeden** HTTP-Fehler (400/413/422/500) als „Sidecar nicht verfuegbar“ — Fehldiagnosen | `VisionPipelineClient.cs:178-181, 198-202` | 🔎 | `IsSidecarUnavailableError` auf Transport/503 einschraenken; 4xx als eigene `SidecarBadRequestException` mit Body |
| Q3 | `requirements.txt` installiert sam2 von git-HEAD ohne Commit-Pin | `sidecar/requirements.txt:23` vs. `requirements-lock.txt` (dort korrekt `@2b90b9f...`) | 🔎 | Commit-Pin nachziehen + Kopf-Kommentar „immer requirements-lock.txt verwenden“ |
| Q4 | nvidia-smi-Subprozess bei **jedem** Detect-Frame (30-150 ms Overhead) + doppeltes Bild-Decode in `/classify/yolo` | `yolo_wrapper.py:289-311, 388, 439`, `routes/yolo.py:58-63` | 🔎 | GPU-Auslastung mit 2s-TTL-Cache; dekodiertes Bild an `analyze_bend` weiterreichen |
| Q5 | `export_yolo` ist `async def` mit blockierendem Datei-I/O — blockiert waehrend grosser Exporte den ganzen Sidecar inkl. `/health`; kein Mengenlimit im Schema | `routes/training.py:23`, `schemas/segmentation.py:66` | 🔎 | zu `def` machen (FastAPI-Threadpool); `samples`-Feld `max_length=500`; C#-`TrainingExportService` chunked senden (erst NACH H5!) |
| Q6 | Rename+PDF-Rewrite-Logik doppelt in zwei Code-Behinds, **bereits auseinandergelaufen** (DataPage sammelt PDF_Path+PDF_All, SchaechtePage zusaetzlich PdfEigen+Link) | `DataPage.xaml.cs:583-625`, `SchaechtePage.xaml.cs:972-1021` | 🔎 | gemeinsamer `IRecordRenameCoordinator` (Application-Interface, DI); beide Code-Behinds rufen nur noch den Service; Test fuer die PDF-Sammel-Logik |
| Q7 | `CostCalculatorViewModel.cs` enthaelt 4 Top-Level-Klassen (u.a. 626-Zeilen-`MeasureBlockVm`) | `CostCalculatorViewModel.cs:23/552/568/1194` | 🔎 | reiner Datei-Split, keine Logik-Aenderung, Build als Verifikation |
| Q8 | PhotoMeasurement: Marker-Zeichnung doppelt (Copy-Paste) + PNG-Datei-I/O im Window | `PhotoMeasurementWindow.xaml.cs:714-723 vs. 1331-1341, 1198-1203` | 🔎 | gemeinsame `DrawDeformationMarker`/`DrawPolygonMarker`-Methoden; `SaveOverlayPng` in OverlayToolService |
| Q9 | Sync-over-async im Import: `KlassifiziereAsync(...).GetAwaiter().GetResult()` | `DichtheitImportDistributor.cs:118` ✅ | ✅ | Aufrufkette pruefen: laeuft das je auf dem UI-Thread? Wenn ja → async durchreichen; wenn sicher Hintergrund → Kommentar „bewusst synchron, laeuft auf Worker“ |
| Q10 | Schacht-Feld-Heuristiken als private statics im Code-Behind untestbar | `SchaechtePage.xaml.cs:489-524, 674-694, 1035-1079` | 🔎 | reine Funktionen (`ResolveOptionField`, `Normalize`, `ResolveSchachtDetailGroup`, …) in statische Klasse `SchachtFeldHeuristik` extrahieren + Unit-Tests |

---

## STUFE 3 — MITTELFRISTIG (je 0,5–2 Tage, Struktur & Vertrauen)

### M1 — DI-Disziplin wiederherstellen (groesster Einzelhebel fuer Testbarkeit) 🔎
~99 verstreute `new *Service(...)` in 42 UI-Dateien. Schlimmste Faelle zuerst:
1. `ImportPageViewModel.cs:665-671`: baut `WinCanDbImportService`/`XtfImportServiceAdapter` **neu**, obwohl `_sp.WinCanImport`/`_sp.XtfImport` existieren → auf ServiceProvider-Properties umstellen.
2. `ImportPageViewModel.cs:718-724`: `HttpClient` + `OllamaClient` pro Import-Lauf, HttpClient wird nie disposed → Factory-Methode `ErzeugeKiSchiedsrichter` in den ServiceProvider verschieben (Muster: `CreateVideoAnalysisPipeline`, `ServiceProvider.cs:222`), HttpClient dort als Feld halten.
3. Kosten-Stores (`CostCatalogStore`, `MeasureTemplateStore`, `ProjectCostStoreRepository`) werden an 8+ Stellen per `new` erzeugt (`CostCalculatorViewModel.cs:25-27`, `BuilderPageViewModel.cs:40-41`, `SanierungsMatrixPageViewModel.cs:488-490`, `SchachtSanierungsMatrixPageViewModel.cs:42-44`, `ExportPageViewModel.cs:95`, `DataPagePrintController.cs:374`, `SchaechtePage.xaml.cs:1243`) → Interfaces `ICostCatalogStore`/`IMeasureTemplateStore`/`IProjectCostStoreRepository` (+Factory fuer alternative Dateinamen wie `schacht_empfehlungen.json`) in Application, Registrierung im ServiceProvider, Stellen schrittweise umstellen. **Wichtig:** Die heikle Save-Disziplin (frisch laden + nur eigene Haltung mergen) gehoert damit an EINE Stelle.
> **NICHT** auf Microsoft.Extensions.DependencyInjection umbauen — das wurde am 2026-06-20 bewusst verworfen (siehe „Bewusst nicht anfassen“). Der bestehende ServiceProvider bleibt, er wird nur konsequent benutzt.

### M2 — Interfaces an die richtige Schicht 🔎
`IVisionPipelineClient` (`Infrastructure/Ai/Pipeline/IVisionPipelineClient.cs:10`) und `INameBasedProtocolDistributor` (`Infrastructure/Import/Protocols/INameBasedProtocolDistributor.cs:20`) liegen in Infrastructure statt Application; `ServiceProvider.cs:75` exponiert den Infrastructure-Typ vollqualifiziert. → Reiner Namespace-Move nach Application (+ zugehoerige reine DTOs), Implementierungen bleiben in Infrastructure. Build + Tests als Absicherung.

### M3 — ServiceProvider-Konstruktor entlasten 🔎
`ServiceProvider.cs:162-184`: oeffnet SQLite-KB, baut HttpClient/EmbeddingService und ruft `retrieval.CheckModelConsistency()` **synchron im Konstruktor** — App-Start haengt an Ollama-Erreichbarkeit. Zusaetzlich wird `VsaCodeResolver` als static-mutable Singleton konfiguriert (`VsaCodeResolver.cs:19-29`): jeder Nutzer vor `ConfigureCatalog` (Tools, Tests) rechnet stillschweigend ohne Katalog.
→ Retrieval-Init in `Lazy<>`/`InitializeAsync` nach dem Splash; VsaCodeResolver zu Instanzklasse mit Katalog im Konstruktor (statische Fassade darf uebergangsweise delegieren).

### M4 — Sidecar: Eviction-Race + echtes VRAM-Budget 🔎
1. `gpu_manager.py:71-74/109-114/198-206` + `main.py:82`: `evict_lru` kann ein Modell **mitten in laufender Inferenz** entladen (Fastpath liest Slot ohne Lock; `del state.model` → AttributeError/500; VRAM wird wegen lebender Referenz gar nicht frei). → Slots mit frischem `last_used` (<30 s) ueberspringen; `state.model = None` statt `del`; Wrapper kopieren Model-Referenz lokal. Test ergaenzen.
2. `gpu_manager.py:180-196`: 29-GB-Budget wird nur gegen `torch.cuda.memory_allocated` geprueft — TensorRT-Engines und Ollama sind unsichtbar, die Warnung kann nie richtig feuern. → Gesamt-VRAM via nvidia-smi (mit TTL-Cache aus Q4) messen und in `/health` als `vram_used_total_gb` exponieren.

### M5 — Bend-Entscheidung zurueck nach C# (Thin-AI) ✅
`bend_geometry.py` ist ein 1:1-Port des C# `VanishingPointBendDetector`. Die Konstanten sind aktuell noch synchron (✅ beide `0.12`/`0.15`, geprueft 09.07 — eine Agent-Behauptung ueber Divergenz war falsch), aber genau das ist das Risiko: Wer kuenftig nur in C# tuned, aendert das Sidecar-Verhalten nicht (und umgekehrt). Die fachliche `is_bend`-Entscheidung faellt im Sidecar und C# uebernimmt sie ungeprueft — Verstoss gegen Thin-AI.
→ C# leitet `is_bend` selbst aus dem gelieferten Rohsignal `bend_shift` + eigener Schwelle ab; Sidecar-Feld bleibt nur informativ. Hinweis: `bend_geometry_enabled` ist im HEAD per Default aus — Prioritaet entsprechend nachrangig, aber vor jeder Re-Aktivierung zwingend.

### M6 — DataPage-Zell-Edit-Fachregeln aus dem Event-Handler 🔎
`DataPage.xaml.cs:743-823`: Sanieren Ja→Nein (loescht 8 Kostenfelder nach Rueckfrage), Sonderpfade, UserEdited-Stempelung — alles im Event-Handler, untestbar. → dem bestehenden Controller-Muster folgen: `DataPageCellEditCommitController.Resolve(...) -> CommitPlan`, Handler fuehrt nur noch aus; Unit-Test fuer den Plan (Ja→Nein, Nein→Ja, generisches Feld).

### M7 — UI-freie Dienste aus dem UI-Projekt 🔎
`QgisBridgeSnapshotBuilder` (789 Zeilen, Doku sagt selbst „UI-frei“), `KnowledgeBackupService` (oeffnet Infrastructure-SQLite direkt), `SystemMonitorService` (1348 Zeilen, kein Interface, per `new` im ShellViewModel:107). → QgisBridge nach Infrastructure/Map, KnowledgeBackup neben FullBackupService, `ISystemMonitorService`-Interface + Registrierung.

### M8 — yolo_wrapper.py mechanisch splitten 🔎
705 Zeilen mit 4 Verantwortungen (Detect, cls-Governance inkl. active.json/SHA-256, Frame-Quality-Gate, Telemetrie). → `cls_wrapper.py`, `frame_quality.py`, `runtime_telemetry.py`; `yolo_wrapper.py` re-exportiert alte Namen, Routen/Tests bleiben unveraendert.

---

## STUFE 4 — LANGFRISTIG Richtung Note A (nur nach Ruecksprache mit Pascal, etappenweise)

### L1 — Die 493 UI-freien Ai-Dateien aus dem WPF-Projekt verschieben 🔎 (groesster Architektur-Hebel)
Unter `src/AuswertungPro.Next.UI/Ai/` liegen 598 Dateien, davon ~493 ohne jede WPF-Abhaengigkeit — darunter die **fachlich kritischste Logik der App**: VSA-Code-Aufloesung (`CodingFindingCodeResolver.cs`), Meterstand-Bestimmung (`CodingMeterResolver.cs`), Dedupe-Keys, Streckenschaden-Policies.
→ Etappenweise nach `Infrastructure/Ai/Coding/` verschieben (reiner Move + Namespace, UI referenziert Infrastructure bereits): pro Etappe ein Thema (Meter → Code-Resolution → Dedupe → Streckenschaden), pro Etappe Build + Tests + Commit. Danach sind Kern-Fachlogik-Tests ohne WPF-Testhost moeglich.
**Vorbedingung:** L2 (Zirkularitaet), sonst blockieren Player-Referenzen den Move.

### L2 — Zirkulaere Abhaengigkeit UI.Ai ↔ UI.Player aufloesen 🔎
37 Ai-Dateien importieren `UI.Player`, 23 Player-Dateien importieren `UI.Ai`. Regel festlegen: **Player darf Ai referenzieren, Ai nie Player.** Die von Ai konsumierten Player-Typen (PlayerTrace, Timeline-/Host-Typen) in `UI/Common/` verschieben oder durch ILogger/Interfaces ersetzen; danach per Grep verifizieren.

### L3 — Codier-Session als echter Service (PlayerWindow entlasten) 🔎
PlayerWindow: 98 partial-Dateien (~5500 Zeilen), Codier-Pipeline verdrahtet ueber 152 statische Workflow-Klassen mit 134 Delegate-Bag-Records, **1 einziges Interface** unter UI/Ai. → `ICodingSessionService` (Analyse, Accept/Reject) + schmales `ICodingViewSurface` (Overlays, Button-State), das PlayerWindow implementiert; Delegate-Bags schrittweise durch die zwei Interfaces ersetzen. Reihenfolge: Analysepfad → AiEvents → Overlay/Health. (Deckt sich mit dem laufenden PlayerWindow-Vorhaben aus dem Fahrplan.)

### L4 — SchaechtePage & ProtocolEntryEditorDialog auf MVVM heben 🔎
- `SchaechtePage.xaml.cs:1243` schreibt `schacht_empfehlungen.json` direkt aus dem View, ruft `HoldingFolderDistributor` direkt (`:1014`), holt das Projekt via `App.Current.MainWindow.DataContext` (`:643`) und Commands per Reflection (`:909`). → `ISchachtMassnahmenCoordinator` + Commands im ViewModel.
- `ProtocolEntryEditorDialog.xaml.cs` (943 Zeilen): zentrale VSA-Codier-Maske ohne bindendes ViewModel; `ApplyAndClose` (669-794) mutiert das Domain-Modell direkt, Streckenschaden-Regel doppelt (296-308 vs. 735-747). → `ProtocolEntryEditorDialogViewModel` mit testbarem `TryApply(out errors)`; nur EINE Streckenschaden-Validierung (via `ProtocolEntryValidator`).

### L5 — HoldingFolderDistributor hinter Fassade 🔎
Statische God-Class, ~4230 Zeilen ueber 6 partial-Dateien, direkt von Views aufgerufen. → Fassaden-Interface `IHoldingDistributionService` (Application) mit den von der UI genutzten Einstiegen, duenne delegierende Implementierung; zweiter Schritt: PdfParsing-Teil als instanzierbare `HoldingPdfParser`-Klasse mit fokussierten Format-Tests. Nicht blind zerlegen — Vorgehen aus ARCHITEKTUR-CODE-AUDIT-2026-07-01 (Plan/Executor/Source) gilt weiter.

### L6 — Sidecar-API-Vertrag testbar machen 🔎
JSON-Vertraege C# ↔ Sidecar sind implizit (H4/Q1 sind Symptome). → Contract-Tests: gespeicherte Beispiel-Antworten des Sidecars gegen die C#-DTOs deserialisieren (beide Richtungen); bei jeder Schema-Aenderung schlagen sie an. (Empfehlung aus Audit 01.07, weiterhin offen.)

**Ziel-Zustand Note A:** kritische Fachlogik liegt testbar ausserhalb des UI-Projekts, kein stiller KI-Vertrauensbruch mehr moeglich (Gate/Health/Contract), God-Classes in Kernpfaden aufgeloest, DI konsequent — bei weiterhin 100 % gruener Testsuite und 0 Build-Warnungen.

---

## Bewusst NICHT anfassen (Verdikte bleiben gueltig)

- **Microsoft-DI-Umbau, Docker/K8s, adaptives QualityGate, VsaCodeTree-Merge, async-Sidecar-Umbau** — verworfen am 2026-06-20, nicht re-litigieren.
- **Rundungsarchitektur / 5-Rappen-Rundung** — nur per Guard-Test sichtbar machen, kein Umbau ohne Pascals Entscheid.
- **WPF → WinUI/MAUI, AvalonDock, Custom-Chrome** — Overkill + NuGet-Regel.
- **KI-Modell-Wechsel, qwen2.5-Rueckfall, SAM-3-Aktivierung** — Hebel ist Daten/Codierung, nicht Modelle.
- **Legacy-Dateien unter %APPDATA% loeschen** — nie; nur toten Code entfernen.
- **Thin-AI, Laptop/Workstation-Abstraktion, VRAM-Budget 29 GB** — Prinzipien, keine Baustellen.
- **Medienverteilungs-Kopierverhalten beim Import** — gewollt.

## Arbeitsregeln fuer Codex (zwingend)

1. **Pro Punkt ein Commit** mit deutschem Commit-Text; Logik-Aenderungen immer mit fokussiertem Test im selben Commit.
2. Nach jedem Punkt: `dotnet build AuswertungPro.sln` (muss 0 Warnungen bleiben!) + betroffene Testprojekte; Baseline ist 8075 gruen.
3. 🔎-Befunde **vor** Umsetzung an der genannten Stelle verifizieren; stimmt der Befund nicht, Punkt ueberspringen + im Commit/Report vermerken.
4. Python-Aenderungen: bestehende Pytests im Sidecar mitlaufen lassen; keine neuen pip-Pakete.
5. Keine NuGet-Pakete ohne Rueckfrage; Kommentare auf Deutsch; keine grossen Umbauten aus Stufe 4 ohne explizites Go von Pascal.
6. Reihenfolge: Stufe 1 komplett → Stufe 2 → Stufe 3; Stufe 4 nur einzeln nach Freigabe.

## Offene Rest-Audit-Punkte (spaeter nachholen)

Diese Dimensionen wurden am 09.07. nur per Stichprobe geprueft (Session-Limit); ein spaeterer Durchgang lohnt sich:
- **WPF-Memory-Leaks** (BitmapImage ohne Freeze/OnLoad, nicht abgemeldete Event-Handler, DispatcherTimer) — Stichprobe unauffaellig, aber nicht flaechig geprueft.
- **XAML-Theme-Konsistenz** (hartkodierte Farben vs. Theme-Brushes, Light/Dark-Key-Paritaet) — nicht geprueft.
- **Testluecken-Landkarte** (welche Kern-Services haben wie viele Tests) — QualityGate/LV/Backup nachweislich getestet, Rest nicht kartiert.
- Positiv bereits verifiziert: atomares Speichern (`JsonProjectRepository` tmp+Replace), VisualTree-Crashmuster bereinigt (verbleibende `VisualTreeHelper.GetParent`-Stellen laufen auf HitTest-Visuals), nur noch ~2 wirklich leere catch-Bloecke, `async void` nur im bewussten `SafeFireAndForget`-Helper.
