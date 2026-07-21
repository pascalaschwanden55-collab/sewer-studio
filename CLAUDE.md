# SewerStudio — AI Sewer Inspection System

## Projekt-Kontext
- **App:** WPF / .NET 10, MVVM, Windows 11
- **Zweck:** Automatisierte Kanalinspektion, ~3000 Videos aus Kanal-TV-Exporten
- **Standards:** EN 13508-2, VSA-KEK; aktive Quelle: `vsa_kek_2020_catalog_manifest.json`
- **Entwickler:** Solo, kein kommerzielles Ziel
- **Hardware:** Intel Core Ultra 9 285K · ASUS RTX 5090 32GB · 64GB DDR5

## AI-Pipeline (Ist-Zustand, HEAD)
- C# steuert Geschaeftslogik, UI, Dedup, QualityGate und Persistenz.
- Sidecar `sidecar/sidecar/` liefert YOLO, Grounding DINO und SAM ueber HTTP.
- YOLO: Standard-Gewicht `yolo26m.pt` bzw. TensorRT-Engine, wenn vorhanden; COCO-Fallback `yolo11m.pt`, wenn eigene Gewichte fehlen und Fallback erlaubt ist.
- Qwen3-VL laeuft ueber Ollama fuer Bild-/Code-Analyse. GPU-Auto waehlt ab 24 GB VRAM `qwen3-vl:8b-q8`, sonst Default/Fallback `qwen3-vl:2b`; NIE auf qwen2.5 zurueckfallen. Keine Doku-Annahme zu automatischer 8B->32B-Laufzeit-Eskalation treffen.
- Grounding DINO: on-demand im Sidecar; Loader bevorzugt Swin-B (`grounding_dino_swinb`), Fallback Swin-T OGC (`grounding_dino_1.5`). Swin-B Stresstest 2026-06-20 bestanden (1000 Frames, 0 Timeouts, Forward ~107 ms, VRAM-Peak ~21,3 GB ≪ 29 GB) → behalten.
- SAM: **SAM 2.1** (`sam2.1_hiera_large.pt` unter `models/sam2.1/`, via `SAM2ImagePredictor`, box-getrieben). SAM-1 `vit_h` ist im Sidecar entfernt. SAM 3 nur deaktivierte Experiment-Option (`sam3_weights_path`, Default aus, kein Wrapper/keine Route); alte `models/sam3/`-Ablage entfernt.
- Bogen-Geometrie (`bend_geometry.py`, Fluchtpunkt/Bogen-Veto): im HEAD per Default DEAKTIVIERT (`bend_geometry_enabled=false`).
- Dedup/Merge: C#-framebasiert ueber `TemporalFindingDeduplicator` und `TemporalCodeVotingService`. Keine Annahme zu alten `UpdateActive`-Duplikaten treffen.
- Kein ByteTrack/OC-SORT und kein echtes Multi-Object-Tracking in HEAD.
- Der YOLO-Trainings-Export ist seit AP 0.3 plan-gesteuert: C# erzeugt vor dem
  Sidecar-Healthcheck genau einen unveraenderlichen Plan. Sidecar und lokaler
  Ausfuehrer schreiben nur noch diesen Plan und treffen keine eigene Klassen-,
  Split-, Quarantaene- oder Dateinamenentscheidung.

## Architektur-Prinzipien (NICHT brechen)
- Thin-AI: C# fuer alle Geschaeftslogik, LLM nur fuer Textgenerierung
- Kein grosses Refactoring ohne explizite Diskussion
- Laptop-Mode / Workstation-Mode Hardware-Abstraktion erhalten
- VRAM-Budget: max 29GB stabil, niemals alle Modelle gleichzeitig
- QualityGate Green/Yellow/Red muss immer durchlaufen

### Checkliste bei jedem neuen Service / Tool (vor dem Commit pruefen)
1. **Interface + eigener Service:** Neue Logik als eigener Service mit Interface, nicht in bestehende Klassen quetschen.
2. **Schichten trennen:** Geschaeftslogik in C# (nicht in UI-Code, nicht im Sidecar). UI ruft ViewModel/Service, nie direkt Infrastruktur.
3. **Registrierung:** Service im `ServiceProvider` (DI) eingetragen, kein `new` verstreut im Code.
4. **Fokussierter Test:** Mindestens ein Test fuer die Kernlogik (Parser/Pipeline/ViewModel/QualityGate). Keine riskante Logik ohne Test.
5. **Budget & Gate:** VRAM-Budget (max 29GB) nicht gebrochen, QualityGate laeuft weiter durch.
6. **Klein bleiben:** Kein grosses Refactoring am Bestand ohne Rueckfrage — neues Feature additiv bauen.

## Aktueller Pipeline-Ablauf
1. UI/Service startet Analyse ueber `VideoAnalysisPipelineService`, `SingleFrameMultiModelService` oder `VideoFullAnalysisService`.
2. C# ruft den Sidecar ueber `VisionPipelineClient` auf.
3. Sidecar verwaltet Modell-Locks und GPU-Slots in `sidecar/sidecar/gpu_manager.py`.
4. Multi-Model-Pfad: YOLO -> DINO -> SAM -> Quantifizierung -> optional Qwen.
5. C# mappt VSA-Code, dedupliziert framebasiert und laesst `QualityGateService` laufen.

## Geplant / nicht implementiert (nicht als Ist-Zustand behandeln)
- `ByteTrack` / `OC-SORT`: kein Tracking im aktuellen HEAD.
- `DetectionAggregator` / meterbasierter Merge-Radius / echtes Multi-Object-Tracking: nicht im aktuellen HEAD. Temporal Voting existiert als `TemporalCodeVotingService`, kein separater Aggregator.
- `InferenceOrchestratorService`: keine C#-Klasse im aktuellen HEAD; GPU-Slots liegen im Sidecar.
- Einen produktiven `KbDeduplicationService` gibt es aktuell nicht. Similarity-Checks im
  Trainings-/Review-Kontext nicht mit dem Retrieval-Ranking verwechseln.
- Automatische 8B->32B-Laufzeit-Eskalation: nicht als implementiert annehmen.
- Negativ-/Hintergrundbilder im gemeinsamen Detect-Plan sind noch nicht angeschlossen.
  Der Sidecar-Vertrag kann leere Labeldateien schreiben, der C#-Planner nimmt derzeit
  nur gepruefte Box-Annotationen auf. Das wird erst beim Aufbau des Negativ-Pools erweitert.

## Build & Test
```bash
dotnet build AuswertungPro.sln
dotnet test AuswertungPro.sln
```

## Wichtige Klassen
- `VideoAnalysisPipelineService`  → waehlt Multi-Model- oder Fallback-Pfad fuer Videoanalyse
- `MultiModelAnalysisService`     → YOLO/DINO/SAM/Qwen-Pipeline mit framebasiertem Dedup
- `VideoFullAnalysisService`      → Vollanalyse-/Fallback-Pfad mit eigener Dedup-Logik
- `SingleFrameMultiModelService`  → Live-Einzelframe YOLO/DINO/SAM
- `VisionPipelineClient`          → C#-HTTP-Client zum Sidecar
- `QualityGateService`            → Green/Yellow/Red aus verfuegbaren Evidence-Signalen
- `FullProtocolGenerationService` → KI-Befunde zu Protokolleintraegen mappen
- `KnowledgeBaseManager`          → SQLite-KB: Samples + Embeddings indexieren/retrieven
- `TrainingSamplesStore`          → JSON-Trainingssamples speichern/mergen
- `PhotoMeasurementGeometryService` → stabile oeffentliche Fassade fuer reine Fotomessungs-Geometrie
- `PhotoMeasurementAnglePlanBuilder` → getrennte Winkel-, Abzweig-, Kreis- und Bogenplanung ohne UI-Zustand
- `PipelinePipeRadarRenderer` → zustandslose WPF-Zeichnung des Rohr-Radars; das Fenster liefert nur Daten, Modus und Groesse
- `PipelineLiveFrameOverlayRenderer` → bewahrt Leer-/Groessenregeln des eingebetteten Live-Rings und delegiert die Zeichnung
- `LiveFrameRingOverlayRenderer` → gemeinsame Ring-Zeichnung fuer Hauptfenster, abgedocktes Fenster und Player mit drei getrennten Stilen
- `LiveDetectionGeometryMapper` → gemeinsamer Uhrparser, Uhrwinkel und Fassade auf die zentrale Ringgeometrie
- `PipelineProgressMapper` → laufbezogene Fortschritts-, ETA- und Live-Frame-Abbildung; liefert dem Fenster nur Render-/Weiterleitungs-Hinweise
- `PipelineResultPresenter` → zustandslose Abschlussabbildung fuer Statistik, Telemetrie und hoechstens 250 sichtbare Befunde

- `TrainingDataInventoryService`  -> rein lesendes Inventar fuer Teacher-/Trainingsquellen, Pfade und Eval-Schutz je Eval-Set
- `TrainingInventoryReportValidator` -> strenger Vertrag fuer Schema 2.2, Triage, Pfade, Quellen und Zusammenfassung
- `tools/TrainingDataInventory`   -> AP-0.1-Werkzeug; Bericht plus SHA-256 unter `<KnowledgeRoot>/training/reports`
- `ITrainingYoloClassMapStore`    -> rein lesender, unveraenderlicher class_map-v2-Snapshot fuer den lokalen Detect-Export
- `TrainingYoloClassMapFileStore` -> prueft feste 14 Klassen, echten VSA-Manifest-Hash, Quell-Hashfelder, Zeilenzahlen, Quellenreihenfolge und menschlich freigegebene Migration
- `VsaYoloClassMapFileStore`      -> Teacher-Karte; `GetClassId` liest strikt, nur `GetOrAddClassId` darf bewusst erweitern
- `TrainingExportPlanInputBuilder` -> baut aus einem Live-Inventar-Snapshot den einzigen Planner-Input
- `TrainingExportPlanService`      -> legt Split, Klassen-IDs, Dateinamen, Ausschluesse und SHA-Zusammenfuehrung fest
- `TrainingExportPlanLocalExecutor` -> atomarer lokaler Ausfuehrer desselben Plans
- `TrainingExportSidecarRequestBuilder` -> verpackt den Plan fuer den strikten Sidecar-v2-Vertrag
- `TrainingExportCompletionService` -> markiert nur vom passenden Plan bestaetigte `TrainingSample`-Quellen
- `TrainingExportExecutionService` -> waehlt Sidecar oder den gleichwertigen lokalen Weg und prueft Antwort sowie Zielpfade
- `TrainingYoloExportCoordinator` -> steuert Auswahl, Inventar, Plan, Ausfuehrung und Abschluss ausserhalb der UI
- `TrainingYoloExportComposition` -> baut das Export-Subsystem einmalig zusammen; der zentrale ServiceProvider delegiert nur
- `FullBackupComposition`         -> baut Marker, SQLite-Schnappschuss, Manifestpruefung und Vollsicherung einmalig zusammen; die UI liefert nur die aktuelle Quellenfunktion
- `StoredImportFileService`       -> kopiert Importquellen, loest Namenskollisionen und schreibt die Pfadlisten zentral
- `StoredImportFilePathResolver`  -> liest gespeicherte XTF-/PDF-Listen zentral und loest moderne sowie bestehende Projektpfade sicher auf
- `ImportFileStagingService`      -> bereitet projektbezogene Importkopien geprueft vor und nimmt sie bis zur Projektuebernahme zurueck
- `MediaDistributionService`      -> verteilt Medien hinter `IImportMediaDistributionService`; die UI erzeugt ihn nicht selbst
- `ServiceProviderRegistrationMap` -> ordnet die bereits gebauten Dienste ihren 126 Vertragstypen zu und erzeugt selbst nichts

Der Vollsicherungsaufbau liegt in Infrastructure. `ServiceProvider.FullBackup.cs`
reicht die bisherigen oeffentlichen Dienste unveraendert weiter. Der zentrale
`ServiceProvider` darf `BackupTargetGuard.UseMarkerGuard` nicht aufrufen; der passende
Marker wird direkt an `FullBackupService` uebergeben.

`StoredImportFileService` plant neue Importkopien fuer beide Projektdatei-Strukturen
unter `<Projekt>\Imports\<Art>`. Im manuellen Import schreibt er zunaechst ueber die
laufbezogene `IImportFileStagingSession`; ausserhalb dieses Ablaufs bleibt sein bisheriger
direkter Kompatibilitaetsweg erhalten. `StoredImportFilePathResolver` liest die Metadaten
ueber `StoredImportFileRegistry`, prueft zuerst den echten Projekt-Root und faellt fuer
bestehende Ablagen auf den Ordner der `projekt.json` zurueck. Dadurch bleiben alte
`Projektdateien\Imports`-Dateien lesbar. Fehlende oder unsichere Einzelpfade werden
uebersprungen. `VsaPageViewModel` und `InspectionProtocolFileLocator` besitzen fuer
gespeicherte Importlisten keine eigene JSON- oder Pfadlogik mehr. Die Protokollsuche
behaelt nur PDF-Auswahl und Suchreihenfolge und erhaelt zentral dieselbe Resolver-Instanz.
Die oeffentliche `ImportFileStoreService`-API bleibt nur als duenne
Kompatibilitaetsfassade und delegiert ohne eigene Dateioperationen an dieselbe
Schreib-Implementierung.

Die fuenf manuellen Importwege PDF, XTF, WinCan, IBAK und KINS liegen im internen
`ImportManualWorkflowController`. Er kennt weder `ServiceProvider` noch Shell oder
ViewModel und verwendet fuer Vorschau, Commit, Bericht, Speichern und Projekttausch
weiter den `ImportRunWorkflowController`. `ImportPageViewModel` verbindet nur Befehle
und aktuellen UI-Zustand. Der gemeinsame Importlauf bindet beim Start Projektinstanz,
normalisierten Projektpfad und Berichtsordner. Nach jedem asynchronen Abschnitt prueft
er Projektidentitaet und Abbruch erneut; bei einem Wechsel wird die Arbeitskopie nicht
uebernommen. PDF-/XTF-Quellkopien und die Medienverteilung verwenden dabei dieselbe
`IImportFileStagingSession`. Sie schreibt gepruefte Kopien zuerst neben der Projektdatei
unter `.import-staging`, veroeffentlicht sie erst nach den Nacharbeiten und nimmt nur die
vom Lauf neu angelegten Dateien zurueck, solange das Live-Projekt noch nicht getauscht ist.
Bereits vorhandene oder wiederverwendete Dateien werden nie geloescht. Unvollstaendige
Nacharbeiten und fehlgeschlagenes Speichern bleiben als
eigene Zustaende sichtbar; nach Vorschau plus Echtlauf zeigt der letzte Bericht auf den
Echtlauf. Eine XTF-Vorschau darf weder Quellen ins Rohdatenarchiv kopieren noch das
alte Rohdatenarchiv migrieren; beides geschieht nur beim echten Import.

Noch offen ist die absturzsichere Gesamttransaktion mit dauerhaftem Journal und
vorbereitetem `projekt.json`-Stand. Ein normaler Fehler, Abbruch oder Projektwechsel wird
jetzt zurueckgenommen; ein Prozess- oder Stromabsturz waehrend der Sitzung kann weiterhin
einen Arbeitsordner oder verwaiste neue Dateien hinterlassen. Auch das additive
XTF-Rohdatenarchiv und der
Ein-Knopf-Import laufen noch ausserhalb dieser Sitzung. Diese Grenzen spaeter in einem
eigenen Infrastructure-Transaktionsdienst loesen, nicht im ViewModel verstecken.
Der manuelle PDF-Stapellauf bleibt bewusst getrennt vom fehlertoleranten PDF-Scan des
`ImportPostProcessingController`, weil beide verschiedene Fehlerregeln haben.

Beim Teacher-Store ist die JSON-Karte verbindlich und `classes.txt` nur abgeleitet.
Scheitert das Schreiben der JSON-Karte, wird die vorherige `classes.txt`
wiederhergestellt oder eine neu angelegte Kopie entfernt.

Die versionierten Vorlagen liegen unter `training/class_maps/` und werden beim Build
nach `Data/Training/` kopiert. `detect_class_migration_v2.candidate.json` enthaelt
124 vollstaendige Alt-Zuordnungen, steht aber bis zur fachlichen Abnahme absichtlich
auf `pending`. Unbekannte oder offene Klassen werden vor jeder lokalen Exportausgabe
hart gestoppt; es gibt keine stille neue ID und keinen automatischen SONST-Rueckfall.
Die Migrationsdatei prueft alle vier Herkunfts-Hashfelder auf SHA-256-Format, die
deklarierte Zeilenzahl und die feste Aufloesungsreihenfolge. Nur der VSA-Hash wird
beim Lesen gegen die echte Datei neu berechnet; die anderen drei bleiben Auditwerte
der Erzeugung. `BBD_boden` wird im produktiven Befundweg ueber
`CodingFindingCodeResolver`/`VsaCodeResolver` zu `BBDZ`, nie zum nackten `BBD`.

## Plan-gesteuerter YOLO-Export (AP 0.3)

Der Datenfluss ist verbindlich:

```text
TrainingCenter
  -> duenne UI-Huelle ruft ITrainingYoloExportCoordinator
  -> freigegebenes export_registry_v1.json lesen
  -> einen aktuellen TrainingDataInventoryRuntimeSnapshot erzeugen
  -> class_map v2 strikt lesen
  -> ITrainingExportPlanService erzeugt genau einen Plan
  -> ITrainingExportExecutionService nutzt Sidecar ODER lokalen Ausfuehrer
  -> ITrainingExportCompletionService markiert nur bestaetigte TrainingSamples
```

Wichtige Regeln:

- AP-0.1-`Disposition` ist die einzige Quarantaene-Wahrheit fuer Teacher-Daten.
  Im geprueften Bestand vom 16.07.2026 sind 205 Teacher-Eintraege
  `trainValCandidate`; 288 Herkunft, 30 Geometrie, 10 Eval-Sperren und 171 Archive
  werden nicht exportiert. Die fruehere Rohzahl 245 ist keine Exportfreigabe.
- `TrainingExportRegistryFileStore` liest
  `<KnowledgeRoot>\training\export_registry_v1.json` strikt. Status `candidate`,
  unbekannte Felder, fehlende Schutz-Sets oder abweichende Manifest-Hashes stoppen.
- Der Plan ist pfadfrei und enthaelt feste Klassen, Haltungs-Splits, Ausschluesse,
  Quell-Hashes und stabile `img_<sha256>.<endung>`-Namen. Gleiche Bild-SHAs werden
  einmal geschrieben; unterschiedliche Labels werden zusammengefuehrt.
- Sidecar und lokaler Ausfuehrer schreiben zuerst unter `.staging` und
  veroeffentlichen atomar nach `<KnowledgeRoot>\training\datasets\<plan_id>`.
  Bestehende unvollstaendige oder abweichende Ziele werden nie repariert oder ersetzt.
- Der Sidecar-v2-Vertrag bindet Klassen, Split, Dateiname, Labels, Klassenkarten-,
  VSA- und Registry-Hash an das C#-Manifest. `plan_sha256` muss `plan_id` entsprechen.
- Der KI-Start uebergibt `SEWER_SIDECAR_TRAINING_EXPORT_ROOT` aus demselben
  `KnowledgeRoot`. Eine abweichende Sidecar-Antwort stoppt vor der Abschlussmarkierung.
- Ein Release-Kandidat erhaelt absichtlich einen eigenen Plan mit Inventar-Run und
  Erzeugungszeit. HTTP-Wiederholungen desselben Plans sind idempotent; ein neuer
  Exportbefehl ist ein neuer Kandidat.
- Die gemeinsame Fixture unter `tests/Fixtures/TrainingExport/` fuehrt Train,
  Dev-Val und ein Multi-Label-Bild durch beide Ausfuehrer. Relative Pfade,
  SHA-256 und Bytes aller Ausgabedateien muessen identisch bleiben.
- `TrainingYoloExportWorkflow` enthaelt nur Busy-, Fortschritts- und Fehlermeldungen.
  Auswahl, Dateizugriff, Sidecar-Rueckfall und Abschluss gehoeren nicht in die UI.
- `TrainingYoloExportRuntime` in Infrastructure ist der gemeinsame Aufbaupunkt.
  WPF verwendet `CreateHybrid` (Sidecar mit lokalem Rueckfall), die StageA-CLI
  `CreateLocal`. Roots, Registry und Dataset-Ziel werden einmal gebunden und koennen
  pro Befehl nicht ausgetauscht werden. `TrainingYoloExportComposition` ist nur die
  duenne WPF-Huelle darum.
- Der Live-Inventar-Snapshot ist die einzige Sample-Wahrheit fuer Auswahl und Plan.
  Die sichtbare UI-Liste darf nur nach einem bestaetigten Export die drei abgeleiteten
  Felder `TrainingEligible`, `TrainingEligibilityReason` und `ExportedUtc` empfangen.
  Eligibility und Abschluss werden erst danach gemeinsam einmal gespeichert.
- `PlanOnly` durchlaeuft Registry, Inventar, Klassenkarte und Planer, schreibt aber
  weder `training_samples.json` noch Datensatzdateien und mutiert auch die UI-Liste nicht.
- Die reine Registrierungsliste liegt in `ServiceProviderRegistrationMap`. Sie darf
  keine Dienste erzeugen. `ServiceProvider.cs` enthaelt dadurch nur noch Aufbau und
  den abschliessenden Aufruf der Map.

Produktiv bleibt der Export derzeit bewusst gesperrt, bis
`detect_class_migration_v2.candidate.json` fachlich freigegeben ist und ein menschlich
freigegebenes `export_registry_v1.json` existiert. Diese Sperren nie automatisch umgehen.

`tools/StageAExporter` ist jetzt eine reine Kompatibilitaets-CLI vor derselben Runtime
und demselben Coordinator wie WPF. Sie besitzt keine eigene Klassen-, Split-, Label-
oder Dateilogik mehr. `--dry-run` ist ein echter `PlanOnly`-Lauf; `--val-ratio` und
`--allow-dummy-bbox` sind harte Fehler. Quelle und Ziel muessen den kanonischen Pfaden
`<KnowledgeRoot>\training_samples.json` und `<KnowledgeRoot>\training\datasets`
entsprechen. Das Tool ist Teil der vollstaendigen Solution, aber bewusst nicht des
entwicklungsnahen `AuswertungPro.Dev.slnf` ohne Hilfsprogramme.

Der fruehere `YoloDatasetExportService` ist entfernt. Er war nicht registriert und
duplizierte Klassenbildung, Bild-Split und Dateischreiben ohne den vollstaendigen
Eval-/Registerschutz. Keinen zweiten YOLO-Datensatzschreiber neben dem gemeinsamen
Coordinator und seinen beiden plan-gesteuerten Ausfuehrern einfuehren.

## SAM-Review im Training Center

`TrainingReviewSamWorkflow` prueft Kandidat, Box und Frame, startet den bedarfsgesteuert
erzeugten `ITrainingReviewSamSegmentationService` und bereitet Speichermaske sowie
Statustext auf. Der Rohrdurchmesser kommt ueber die zentrale Fenster-Fabrik; nur bei
fehlendem Wert gilt weiter 300 mm. `TrainingCenterWindow` bleibt fuer Schaltflaeche,
Maskenanzeige und Dialoge zustaendig. Datei-, Einstellungs- und Maskenlogik nicht wieder
in den Fenster-Code verschieben.

Der Pruefplatz im `TrainingStudioWindow` baut Workbench, Warteschlange und
KI-Bereitschaft gemeinsam ueber `TrainingStudioWindowDependencyFactory`. Beim ersten
Oeffnen prueft `TrainingStudioAiReadinessWorkflow` die Sidecar-Gesundheit und verwendet
nur bei einem Offline-Sidecar den zentralen `AiStartupService`; die Schaltflaeche
`KI starten` bietet denselben Weg fuer einen manuellen Wiederholungsversuch. Das Fenster
startet nur den ViewModel-Befehl und enthaelt keine Prozesslogik. Segmentierung und
Code-Vorschlag laufen parallel. Wenn nur einer der beiden Aufrufe scheitert, behaelt das
ViewModel das bereits abgeschlossene Teilergebnis sichtbar.

Die Maus-/Bildabbildung des Pruefplatzes liegt im reinen
`TrainingStudioImageGeometryMapper`. Er beruecksichtigt die tatsaechliche Lage des
`Image` im Overlay, freie Raender durch `Uniform`-Darstellung und begrenzt das Ziehen
bereits sichtbar am Bildrand. Eine Auswahl darf nur im sichtbaren Bild beginnen.
Beim Beginn einer neuen Box entfernt das ViewModel die alte Maske und den alten
Vorschlag sofort; eine alte Maske darf nie zusammen mit einer neuen Box erscheinen.

## Aktive Few-Shot-Wege

- Produktiv gibt es zwei Laufzeit-Kontextwege. Beide liefern Prompt-Beispiele und
  trainieren keine Qwen-Modellgewichte.
- Aehnliche bestaetigte Faelle kommen aus `KnowledgeBase.db` ueber `RetrievalService`.
- Freigegebene Protokolleintraege kommen getrennt aus `protocol_training.json`
  ueber `ProtocolTrainingFileStore`.
- Der fruehere bildbasierte `FewShotExampleStore` samt Builder und der Schaltflaeche
  `Zu FewShot` ist entfernt: Er schrieb Bilder, wurde aber von keinem KI-Prompt gelesen.
- Bestehende `fewshot_examples.json` und `fewshot_images` sind Legacy-Daten. Sie werden
  nicht veraendert und bleiben fuer alte Wissenssicherungen im Dateikatalog enthalten.
  Diese Dateien nie wieder als Prompt- oder Trainingsquelle anschliessen.

## Ereignisbasierte Eval-Messung (AP 0.4a, technische Grundlage)

Die fruehere Sammeldatei `EvalSetBenchmark.cs` ist entfernt. Dataset-Laden,
Benchmark-Scoring, YOLO-Baseline, Router-Plan, Klassen-Mapping, Coverage, Kontext und
CSV-Helfer liegen jeweils in einer gleichnamigen eigenen Datei. Die oeffentlichen
Klassennamen und Signaturen sind unveraendert. Verhaltenstests sichern alle sieben
CSV-/JSON-Ausgaben, inklusive Kopfzeilen und Escaping.

- `EvalSetBenchmarkCase` traegt additiv `HoldingKey`, `ExpectedSeverity`, `EventId`
  sowie den optionalen Bereich `MeterStart`/`MeterEnd`. Alte Eval-Sets bleiben ueber
  `EvalSetBenchmarkDataset.Load` lesbar.
- Ein Release-Kandidat muss stattdessen durch
  `EvalSetReleaseDatasetValidator.LoadAndValidate`. Fehlende Bilder oder
  Haltungskennungen, bei Schaeden fehlende Ereignis-IDs, ungueltige Severity und
  widerspruechliche Meterbereiche stoppen. Nicht-Schaeden brauchen keine kuenstliche
  Ereignis-ID.
- `EvalSetV2Builder` uebernimmt die neuen Felder und verlangt Severity sowie
  Ereignis-ID fuer Schadensfaelle.
- `EvalSetEventScorer` zaehlt ein Schadensereignis ueber mehrere Frames nur einmal.
  Der Schluessel besteht aus Haltung plus EventId; gleiche EventIds in verschiedenen
  Haltungen bleiben deshalb unabhaengige Ereignisse.
  Detect-Treffer und nachgelagertes Gate werden getrennt ausgewiesen. Fuer Severity
  4/5 gilt ein Mindestumfang von 20 unabhaengigen Ereignissen; Wilson- und exakte
  95-Prozent-Fehlergrenzen werden mit ausgegeben.
- Das vorhandene 120er-Set ist noch nicht menschlich mit Severity und EventId
  nachgepflegt. AP 0.4 ist deshalb nicht abgeschlossen und keine Modellfreigabe darf
  allein aus der neuen technischen Messlogik abgeleitet werden.

## Fachdomaene Kanalinspektion

### Grundbegriffe
- **Haltung:** Kanalabschnitt zwischen zwei Schaechten (typisch 30-80m)
- **Schacht:** Zugang zum Kanal (Anfangs-/Endknoten einer Haltung)
- **DN:** Nennweite in mm (DN150=Hausanschluss, DN300=Standard, DN600+=Sammler)
- **OSD:** On-Screen Display im Video — zeigt Meterstand, Haltungsname, Datum
- **Meterstand:** Position der Kamera in der Haltung (0.00m = Anfang, z.B. 45.30m = Ende)

### Schadenscodierung (VSA-KEK / EN 13508-2)
Codes sind hierarchisch aufgebaut: **Hauptcode** (2-3 Buchstaben) + **Char1** (Untertyp) + **Char2** (Lage)

**Grundgeruest (BC-Gruppe, Bestandsaufnahme):**
- BCD = Rohranfang (Kamera faehrt in Rohr ein, Schacht sichtbar)
- BCE = Rohrende (Endknoten erreicht)
- BCA = Seitlicher Anschluss (runde/ovale Oeffnung in Rohrwand)
- BCC = Bogen (Richtungsaenderung, ueber mehrere Frames sichtbar)

**Strukturelle Schaeden (BA-Gruppe):**
- BAA = Verformung (A=vertikal, B=horizontal)
- BAB = Riss (A=laengs, B=quer, C=diagonal, D=ringfoermig, E=verzweigt)
- BAC = Bruch (A=partiell, B=total)
- BAF = Oberflaechenschaden (rauhe Rohrwandung, chemischer Angriff, Korrosion)
- BAH = Schadhafter Anschluss
- BAI = Einragendes Dichtungsmaterial
- BAJ = Verschobene Rohrverbindung (breit, versetzt, Knick)

**Betriebliche Stoerungen (BB-Gruppe):**
- BBA = Wurzeln/Bewuchs
- BBB = Anhaftende Stoffe/Inkrustation/Fett
- BBC = Ablagerung (A=Sand, B=Kies, C=verfestigt)
- BBD* = Eindringender Boden (kein Basiscode BBD, nur Untercodes)
- Die Detect-Klasse `BBD_boden` ist erlaubt; beim Rueckmapping speichert C# den
  gueltigen allgemeinen Untercode `BBDZ`, niemals den nackten Basiswert `BBD`.

### Quantifizierung
- **Uhrlage:** 12:00=Scheitel (oben), 6:00=Sohle (unten), 3:00=rechts, 9:00=links
- **Severity 1-5:** 1=optisch, 2=leicht, 3=mittel (Sanierung mittelfristig), 4=schwer (kurzfristig), 5=kritisch (Sofortmassnahme)
- **Ausdehnung:** Prozent des Rohrumfangs
- **Querschnittsverringerung:** Prozent des freien Querschnitts

### Punktschaden vs. Streckenschaden
- **Punktschaden:** An einer Stelle (z.B. Riss, Anschluss) — ein Meterstand
- **Streckenschaden:** Ueber Laenge (z.B. Korrosion 2.5m-8.0m) — MeterStart bis MeterEnd

## Coding-Regeln
- Bestehenden Code nur aendern wenn explizit gefragt
- Neue Features als separate Services mit Interface
- Tests breit einsetzen: Parser, Import, Pipeline, KnowledgeBase, UI-ViewModels und QualityGate. Keine riskanten Logik-Aenderungen ohne fokussierten Test.
- Keine NuGet-Pakete ohne Rueckfrage
- Kommentare auf Deutsch
- JSON-Schema fuer alle Qwen-Outputs (strict, kein freier Text)
