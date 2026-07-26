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
- Neue Workflow-/Orchestrierungsklassen (Request/Actions/Result) nach
  `src/AuswertungPro.Next.Application/UseCases/` statt nach `UI/Ai/`; der UI/Ai-Bestand
  ist per `UiAiFreezeArchitectureTests` eingefroren (Referenzbeispiel: `CodingModeBackgroundServicesWorkflow`).

### Checkliste bei jedem neuen Service / Tool (vor dem Commit pruefen)
1. **Interface + eigener Service:** Neue Logik als eigener Service mit Interface, nicht in bestehende Klassen quetschen. Neue Workflow-/Orchestrierungsklassen (Request/Actions/Result-Muster) gehoeren nach `src/AuswertungPro.Next.Application/UseCases/`, nicht nach `UI/Ai/` (eingefroren per `UiAiFreezeArchitectureTests`).
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
- Das aktive Detect-Altmodell (yolo26m, 2026-04-11) ist seit 2026-07-25 als NICHT
  qualifiziert markiert (`sidecar/models/model_qualification.json`, BBox-Kollaps,
  alter Trainingsdatensatz fehlt). `/health` meldet den Sidecar als `degraded` samt
  `detector_qualification`. Nur ein ausdrueckliches `qualified=true` gibt das
  Standardmodell frei. Bei false, fehlendem Feld oder Lesefehler sperrt das Training
  Studio den Fototest; Standard-Endpunkt und Warmup laden/verwenden YOLO nicht.
  Die Freigabedatei bindet PT, TensorRT-Engine und ONNX jeweils an Dateiname und
  SHA-256; Abweichungen sperren fail-closed. Gewichte bleiben unveraendert erhalten;
  das getrennte BCC-Testmodell ist davon unberuehrt.
- `training/scripts/model_collapse_check.py` ist das schreibfreie Kollaps-Pruefwerkzeug:
  Box-Statistik (Paar-IoU, Streuung), IoU gegen Gold-Boxen, Aktivierungen auf dem
  Negativ-Pool, optional mAP via `--dataset`. Ein echter Geometrie-Kollaps ergibt
  `FAIL` (Exit 1); Inferenzfehler, zu wenig Bilder/Treffer oder unter 20 %
  Detektionsrate ergeben `INCONCLUSIVE` (Exit 2), niemals einen falschen PASS.
  `PASS` heisst nur „kein BBox-Kollaps", keine Qualitaetsfreigabe. Pruefbestand unabhaengig
  (`--images-dir`, Default eval_set/images), einheitliche Aufloesung (`--imgsz` 1280),
  Bericht unter `<KnowledgeRoot>/training/reports`. Der Altmodell-Kollaps ist belegt;
  ein Kandidat bleibt ohne den ganzen Release-Weg immer `not_deployed`.
- Batch-Video und Player-Einzelframe verwenden YOLO nur bei ausdruecklichem
  `qualified=true`. Bei false, fehlendem Feld oder Health-Lesefehler wird YOLO weder
  als Frame-Filter noch als Confidence-Beweis verwendet. DINO/SAM laufen ohne
  YOLO-Gate weiter; Health-Ampel und Ergebnis bleiben `Degraded`/orange und verlangen
  eine manuelle Pruefung. Der Ollama-only-Pfad traegt die Kennzeichnung nicht.
- `training/scripts/gold_stock_audit.py` prueft den Goldbestand schreibfrei
  (persoenliche Freigabe, lesbares Bild, randgueltige Box, echte Maskenpixel in der
  Hand-Box, Katalogcode, Bildhash und komplette Eval-Haltung). Haltungsnummern werden
  normalisiert; identische Bildbytes verbinden betroffene Haltungen zu einer
  gemeinsamen Split-Gruppe. Ein Pilot braucht >= 30 Samples sowie Train und Val/Test.
  Platzhalter-Beschreibungen sind fuer das reine BBox-Training zulaessig und werden
  nur als `kb_text_offen` markiert — KB-Index und Qwen-Retrieval sperren sie.
  Stand des schreibfreien Berichts vom 2026-07-25 (nach Bereinigung): 218 Eintraege,
  24 Drafts (14 alte Entwuerfe + 10 Bildduplikate, als Draft markiert und in `Notes`
  dokumentiert), 194 verwendbar, 0 Duplikatgruppen, 186 offene KB-Texte. Die
  Haltungsidentitaet aller 194 wurde per Bild-SHA-256-Match gegen die Quelldateien
  in `D:\Trainingsfotos` rekonstruiert (eindeutig, 0 Mehrdeutigkeiten); `CaseId` und
  `Signature` tragen jetzt die echte Haltungsnummer, die alte Pseudo-CaseId steht in
  `Notes`. Der Split ist damit release-faehig: 128/52/14 aus 69 Haltungsgruppen.
  BCC=59 und BCA=42 sind auswertbare Piloten (>= 30, Train und Val/Test), weiterhin
  keine Modellfreigabe. Backups: `training_samples.json.bak_vor_haltung_*`,
  `KnowledgeBase.backup_vor_haltung_*.db`.
- SAM-Video-Regel (Goldgewinnung): SAM 2.1 kann Masken durch Videos propagieren,
  darf aber nur als Pruefwerkzeug fuer den Menschen dienen, nicht als automatische
  Goldfabrik. Propagierte Nachbarframes sind stark voneinander abhaengige
  Vorschlaege; sie werden einzeln ausgewaehlt und menschlich bestaetigt, bevor sie
  Gold werden. Kein automatischer Gold-Export aus Video-Propagation.
- Negativ-/Hintergrundbilder sind seit 2026-07-25 im gemeinsamen Detect-Plan angeschlossen:
  Die Export-Registry traegt optional `negative_images` (Pfad + SHA-256, menschlich kuratiert,
  Default-Pool `<KnowledgeRoot>/training/negatives/bcc_pilot` via
  `prepare_bcc_pilot.py --negatives-dir`). Der Plan prueft Hashes und Eval-Schutz auch fuer
  Negative, verteilt sie deterministisch (ca. 20 % val) und schreibt leere Labeldateien
  (`IsNegative`-Flag, serialisiert nur bei `true` — Plaene ohne Negative bleiben bytegleich).
  `train_bcc_pilot.py` akzeptiert leere Labeldateien als Negative (Positive ohne Labeldatei
  stoppen weiter), trainiert mit `flipud=0.0, fliplr=0.0` (Uhrlage!) und leichter
  HSV-Augmentierung (`hsv_h=0.01, hsv_s=0.3, hsv_v=0.3`) sowie `--patience` Default 10.

## Build & Test
```bash
dotnet build AuswertungPro.sln
dotnet test AuswertungPro.sln
```

`AuswertungPro.sln` enthaelt die vier produktiven Projekte, die vier Testprojekte
und alle 41 `tools/**/*.csproj`. Neue Werkzeugprojekte sofort aufnehmen, damit
verschobene Klassen oder Projektverweise im normalen Release-Build sichtbar brechen.

## Wichtige Klassen
- `VideoAnalysisPipelineService`  → waehlt Multi-Model- oder Fallback-Pfad fuer Videoanalyse
- `MultiModelAnalysisService`     → YOLO/DINO/SAM/Qwen-Pipeline mit framebasiertem Dedup; Ausfallschutz und Checkpoint/Resume unten
- `IAnalysisCheckpointJournal`/`AnalysisCheckpointJournal` → append-only JSONL-Checkpoint pro Video (neben der Trace-Datei, Name = SHA256-Kurzhash des Videopfads). Jeder bearbeitete Frame schreibt genau einen Zustand: `update` (mit Befunden), `advance` (normal uebersprungen), `retry_required` (Transport-/Modell-/Verarbeitungsfehler). Ein Resume uebernimmt nur den lueckenlosen, gueltigen Anfang ab Frame 1 und replayt ihn exakt ueber `TemporalFindingDeduplicator.Update(...)` bzw. `AdvanceAll()` — dadurch liefert Abbruch+Fortsetzung dieselben Detections wie ein ununterbrochener Lauf. `retry_required` beendet den verwendbaren Bereich (ab dort neu inferieren, stale Schweif wird abgeschnitten); fehlende/doppelte/ruecklaufende Frame-Nummern, unbekannte Zeilentypen oder eine beschaedigte mittlere Zeile verwerfen das Resume vollstaendig (frischer Start + Logwarnung); nur eine unvollstaendige letzte Zeile wird sicher gekuerzt. Fehlende Pflichtfelder (Zeit, Meter, Schaetzflag, bei update Findings/Meterquelle) werden NICHT durch Standardwerte erfunden, sondern verwerfen das Resume ebenfalls. `CleanupCompletedJournals` loescht ausschliesslich streng lesbare, abgeschlossene, aeltere Journale — offene oder beschaedigte nie; prozessweit auf hoechstens einen Lauf pro Tag gebremst, Alter wird vor dem Einlesen geprueft; Fehler beim Aufloesen/Aufzaehlen der Ablage ueberspringen nur die Bereinigung mit Warnung — die Analyse laeuft immer weiter
- `SidecarOutageGuard`/`QwenOutageTracker` → Ausfallschutz des Multi-Model-Laufs: 8 Folge-Frames mit Sidecar-Transportfehler (YOLO/DINO/SAM gemeinsam, Reset implizit ueber Frame-Indizes) brechen den Lauf degraded ab; Qwen/Ollama ist ein eigener Prozess und erzeugt ab 8 Folgefehlern nur eine Degraded-Notiz (`NotedErrorCount` bleibt nach spaeterem Erfolg erhalten). Ein Nutzerabbruch per CancellationToken wird sofort weitergeworfen und zaehlt nie als Ausfall. Mehr als 10 % fehlerbedingt uebersprungene Frames setzen `Incomplete=true` an `VideoAnalysisResult` und `PipelineResult` (Surfacing ueber den Warnungspfad)
- Sidecar-Haertung (Paket 2): Der Sidecar arbeitet mit besitzbasierten Busy-Leases (`gpu_manager.acquire_busy/release_busy`, uuid-Besitzer-ID): Predict-Lock ZUERST, Lease DANACH; nur der Besitzer entfernt seine Lease; Wartende koennen weder Busy-Uhr noch Zustand verschieben. Einheitlich fuer YOLO (GPU+CPU als logische Lease `YOLO_CPU`), DINO, SAM, BCC und YOLO-cls (`YOLO_CLS`); CPU-Inferenzen werden bewusst ueberwacht, der Watchdog laeuft daher unabhaengig vom Geraet. VRAM-Eviction ist atomar (Auswahl + letzte Lease-Pruefung + Reservierung unter einem kurzen `_global_lock`); Modellreferenzen, `empty_cache` und GC werden danach ohne diesen Lock bereinigt, damit Health/Watchdog auch bei blockierter CUDA-Bereinigung ansprechbar bleiben. `unload` verweigert bei laufender Inferenz; kein sicherer Kandidat → `insufficient_vram` (mit free/required/reserved_gb im 503-Detail)
- `SidecarInsufficientVramException` → C#-Antwort auf `insufficient_vram`: `VisionPipelineClient` parst 503-Bodys defensiv (echter Vertrag: `code` + Zahlen auf Top-Ebene, `detail` als Klartext; verschachteltes Format toleriert, korrupt = allgemeiner Fehler; Vertragstest mit woertlichem Python-JSON); nur dieser Code wird zum eigenen Kapazitaetsfehler (kein HTTP-Retry, kein Outage-Zaehlen, kein Sidecar-Restart; Frame-Catch: Skip-Quote + Trace degraded + Checkpoint retry_required + Degraded-Grund mit VRAM-Zahlen). `model_unloaded` bleibt gezielt retryfaehig, unbekanntes 503 bleibt Transportfehler. Sidecar-seitig sind gleichzeitige Modell-Ladungen ueber In-flight-Reservierungen koordiniert (`_inflight_loads` unter dem kurzen `_global_lock`): zwei Ladevorgaenge sehen nie denselben freien VRAM (effektiv frei = frei − laufende Reservierungen; `reserved_gb` = Ollama-Reserve + In-flight-Summe)
- `SidecarRestartService` → kontrollierter Neustart nur des EIGENEN Sidecars (max 1 Versuch pro Analyselauf): Prozess-Tracking mit PID + Startzeit + Prozessart (`AiStartedProcessKind` Sidecar/Ollama) + Programmpfad; veraltete Eintraege werden bei jeder Abfrage entfernt. Nur die ausdrueckliche Art `Sidecar` beweist Besitz und erlaubt einen Kill; `Unknown`, Ollama oder ein hinterlegter, aktuell nicht lesbarer/abweichender Programmpfad sperren fail-closed. Kill-Fehler oder Timeout → kein Neustart (kein zweiter Sidecar). Ohne /health-PID: ein lebender eigener Sidecar wird zuerst verifiziert beendet (nie daneben gestartet), ein frueher eigener, beendeter Sidecar bleibt ueber `HadTrackedSidecarProcess` wiederstartbar (Start- ≠ Kill-Berechtigung, auch nach Watchdog-Exit), nur Ollama/Unknown → kein Blindstart. Ein Python-Kindprozess ohne eigenen Tracking-Eintrag muss ein Python-Image tragen; Baseline-Snapshot + Re-Probe direkt vor dem Kill binden Startzeit und Programmdatei. Erfolg erst nach 2 aufeinanderfolgenden /health-Polls
- `SidecarRequestTimeoutException` → interner Inferenz-Timeout (getrennt vom Benutzerabbruch, der OCE bleibt): zaehlt als Transportfehler, kein Retry, Meldung mit Modell-Label + Endpunkt, keine Tokens; Health-/Trainingsaufrufe und Ollama-Timeout bleiben unabhaengig
- `VideoFullAnalysisService`      → Vollanalyse-/Fallback-Pfad mit eigener Dedup-Logik
- `SingleFrameMultiModelService`  → Live-Einzelframe YOLO/DINO/SAM
- `VisionPipelineClient`          → C#-HTTP-Client zum Sidecar
- `QualityGateService`            → Green/Yellow/Red aus verfuegbaren Evidence-Signalen
- `FullProtocolGenerationService` → KI-Befunde zu Protokolleintraegen mappen
- `IOfferPdfExportService`         → Vertrag (Application/Output): kapselt Vorlagen-/Logo-Pfadbau + PDF-Renderer; ViewModels newen keinen Renderer mehr
- `OfferPdfExportService`          → Impl (Infrastructure): loest Pfade auf, delegiert an `OfferHtmlToPdfRenderer` (injizierbarer Render-Delegate als Test-Seam); Modell typsicher ueber `IOfferPdfModel`
- `IQuickScanService`/`IQuickScanSession` → Vertraege (Application/Ai): KI-Schnellscan + kurzlebige Sitzung (eigener Ollama-Client); DTOs `QuickScanSegment/Progress/Result` liegen ebenfalls in Application.Ai
- `QuickScanSession`               → Impl (Infrastructure): baut ffmpeg-Pfad, eigenen `OllamaClient` und `QuickScanService`, besitzt den Client (`IDisposable`). Erzeugt ueber `ServiceProvider.CreateQuickScanSession(cfg)`; der Player-`QuickScanController` newt keine KI-Infrastruktur mehr
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

- `ManualGoldTrainingPolicy`      -> erlaubt fuer neues Training nur persoenlich manuell codierte, bestaetigte Goldsamples mit vorhandenem Bild, BBox und SAM-Segmentierung
- `CodingTrainingSamplePersistenceCoordinator` -> uebernimmt persoenliche Annahmen/Korrekturen aus dem Player-Codiermodus nach `gold_frames`, Trainingsliste und KB
- `PersonalGoldProgressCalculator` -> berechnet den Live-Goldstand je Hauptcode (Ziel 30-50), ohne Daten zu veraendern
- `IPersonalGoldAlbumService`/`PersonalGoldAlbumService` -> liefert das rein lesende Fotoalbum der persoenlichen Handlabels nach Hauptcode
- `IPersonalGoldInboxService`/`PersonalGoldInboxFileService` -> verwaltet den vorbereitenden Bildeingang unter `training/gold_inbox`
- `PersonalGoldFrameMigrationService` -> kopiert Altbestand inhaltsadressiert in `gold_frames` und stellt JSON/SQLite gemeinsam um
- `PersonalGoldMigrationCommitter` -> haelt Umschalten, Nachpruefung und Ruecksetzung von JSON/SQLite getrennt von der Auswahl
- `tools/PersonalGoldMigration`   -> wiederholbares Migrations-/Pruefwerkzeug; schreibt Inventar und Pruefspur unter `<KnowledgeRoot>/training`
- `PersonalGoldBrainSeparationService` -> duenne Fassade fuer Gold-only-Arbeitsstand und atomare Umschaltung; Input/Pfade, Workspace, Commit-Journal und Recovery liegen in getrennten internen Diensten
- `PersonalGoldArchiveRecoveryService` -> duenne Fassade zum Nachholen bestaetigter `ManualCoding`-Faelle; Journal, Pfadpruefung, Vorherkopien und Rollback liegen in getrennten internen Diensten
- `tools/GoldBrainSeparation`     -> sicherer Pruef-/Ausfuehrungsweg fuer Altarchiv, Gold-only-Datenbank und neuen Elements-Spiegel
- `TrainingDataInventoryService`  -> rein lesendes Inventar fuer Teacher-/Trainingsquellen, Pfade und Eval-Schutz je Eval-Set
- `TrainingInventoryReportValidator` -> strenger Vertrag fuer Schema 2.2, Triage, Pfade, Quellen und Zusammenfassung
- `tools/TrainingDataInventory`   -> AP-0.1-Werkzeug; Bericht plus SHA-256 unter `<KnowledgeRoot>/training/reports`
- `ITrainingYoloClassMapStore`    -> rein lesender, unveraenderlicher class_map-Snapshot (aktiv v3, v2 eingefroren lesbar) fuer den lokalen Detect-Export
- `TrainingYoloClassMapFileStore` -> prueft feste Klassenzahl je Version (v2 = 14, v3 = 15 inkl. BCC_bogen), echten VSA-Manifest-Hash, Quell-Hashfelder, Zeilenzahlen, Quellenreihenfolge und menschlich freigegebene Migration
- `VsaYoloClassMapFileStore`      -> Teacher-Karte; `GetClassId` liest strikt, nur `GetOrAddClassId` darf bewusst erweitern
- `TrainingExportPlanInputBuilder` -> baut den Planner-Input nur aus freigegebenen persoenlichen Gold-TrainingSamples; Teacher-Daten bleiben Inventar
- `TrainingExportPlanService`      -> legt Split, Klassen-IDs, Dateinamen, Ausschluesse und SHA-Zusammenfuehrung fest
- `TrainingExportPlanLocalExecutor` -> atomarer lokaler Ausfuehrer desselben Plans
- `TrainingExportSidecarRequestBuilder` -> verpackt den Plan fuer den strikten Sidecar-v2-Vertrag
- `TrainingExportCompletionService` -> markiert nur vom passenden Plan bestaetigte `TrainingSample`-Quellen
- `TrainingExportExecutionService` -> waehlt Sidecar oder den gleichwertigen lokalen Weg und prueft Antwort sowie Zielpfade
- `TrainingYoloExportCoordinator` -> steuert Auswahl, Inventar, Plan, Ausfuehrung und Abschluss ausserhalb der UI
- `TrainingYoloExportComposition` -> baut das Export-Subsystem einmalig zusammen; der zentrale ServiceProvider delegiert nur
- `FullBackupComposition`         -> baut Marker, SQLite-Schnappschuss, Manifestpruefung und Vollsicherung einmalig zusammen; die UI liefert nur die aktuelle Quellenfunktion
- `KnowledgeRealtimeMirrorService` -> gleicht den gesamten KnowledgeRoot beim Start ab und spiegelt danach jede Dateiaenderung auf den Datentraeger `Elements` nach `Brain`
- `HoldingRenameFileService`       -> benennt eine Haltung samt Projekt-Verteilordnern und gespeicherten Medienpfaden um; externe Kundenordner sind ausgeschlossen
- `HoldingFolderRenameTransaction` -> benennt Dateien und Unterordner rekursiv, erkennt abweichende datumsbasierte Alt-Dateinamen und kann jeden ausgefuehrten Schritt zurueckrollen
- `StoredImportFileService`       -> kopiert Importquellen, loest Namenskollisionen und schreibt die Pfadlisten zentral
- `StoredImportFilePathResolver`  -> liest gespeicherte XTF-/PDF-Listen zentral und loest moderne sowie bestehende Projektpfade sicher auf
- `ImportFileStagingService`      -> bereitet projektbezogene Importkopien geprueft vor und nimmt sie bis zur Projektuebernahme zurueck
- `MediaDistributionService`      -> verteilt Medien hinter `IImportMediaDistributionService`; die UI erzeugt ihn nicht selbst
- `ServiceProviderRegistrationMap` -> ordnet die bereits gebauten Dienste ihren 130 Vertragstypen zu und erzeugt selbst nichts

Der Vollsicherungsaufbau liegt in Infrastructure. `ServiceProvider.FullBackup.cs`
reicht die bisherigen oeffentlichen Dienste unveraendert weiter. Der zentrale
`ServiceProvider` darf `BackupTargetGuard.UseMarkerGuard` nicht aufrufen; der passende
Marker wird direkt an `FullBackupService` uebergeben.

`KnowledgeRealtimeMirrorService` startet durch `App` nach dem Aufbau des
`ServiceProvider`. Er gleicht den gesamten aktiven `KnowledgeRoot` zuerst
inkrementell mit `<Datentraeger Elements>\Brain` ab und verarbeitet danach
Dateiaenderungen in einem Ein-Sekunden-Takt. Der Laufwerksbuchstabe wird ueber die
Datentraegerbezeichnung `Elements` ermittelt. SQLite-Dateien werden als gepruefte
Online-Schnappschuesse geschrieben; WAL/SHM-Dateien werden nicht als halbfertige
Datenbankkopien uebernommen. Ein eigener Zielmarker, Pfadgrenzen und
Verknuepfungsschutz sichern jede Loeschung ab. Ist die Platte nicht angeschlossen,
bleibt die Quelle unveraendert und der Abgleich wird nach dem Wiederanschliessen
automatisch vollstaendig nachgeholt.

`BackupSourcePathGuard` und `BackupTargetPathGuard` pruefen Quelle und Ziel vor
jedem kritischen Dateizugriff erneut. Ein unlesbarer oder verknuepfter Pflichtpfad
bricht Spiegelung/Vollsicherung ab, bevor veraltete Zieldateien entfernt oder
Versionen rotiert werden. Einstellungs-, Log- und Desktop-Skriptquellen duerfen
fehlen; Programm- und Projektkomponenten sind nur dann leer, wenn fuer sie keine
Wurzel konfiguriert wurde. Bestehende Spiegeldateien bleiben bei optionalen
Fehlstellen erhalten. `KnowledgeRoot` und jede tatsaechlich konfigurierte
Projektquelle bleiben Pflicht. `DirectoryMirror`, `BackupTargetMarkerGuardService`
und `KnowledgeMirrorMarker` bilden die zentralen Datei-, Zielbesitz- und
Spiegelbesitz-Grenzen.

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
unter `.import-staging/<Lauf-GUID>`, veroeffentlicht sie erst nach den Nacharbeiten und
nimmt nur die vom Lauf neu angelegten Dateien zurueck, solange das Live-Projekt noch
nicht getauscht ist. Vor dem ersten Datei-Move schreibt der Lauf alle vorbereiteten
Rollback-Ziele samt SHA-256 atomar in `.import-transaction.json`; nach `Publish` wird
der Marker mit dem tatsaechlichen Ist-Stand erneuert.
Bereits vorhandene oder wiederverwendete Dateien werden nie geloescht. Unvollstaendige
Nacharbeiten und fehlgeschlagenes Speichern bleiben als
eigene Zustaende sichtbar; nach Vorschau plus Echtlauf zeigt der letzte Bericht auf den
Echtlauf. Eine XTF-Vorschau darf weder Quellen ins Rohdatenarchiv kopieren noch das
alte Rohdatenarchiv migrieren; beides geschieht nur beim echten Import.

Beim Projektladen vergleicht `ImportTransactionRecoveryService` die Marker-TxId mit
`Project.LastCommittedImportTxId` aus dem atomar gespeicherten `projekt.json`.
Gleiche TxId bedeutet: Dateien behalten und nur den eigenen Arbeitsordner aufraeumen.
Ohne Commit-Beweis werden ausschliesslich die im Marker genannten, unveraenderten
Dateien SHA-geprueft zurueckgenommen. Unlesbare Marker, Hashabweichungen, unklare
Dateiarten, Verknuepfungen oder Aufraeumfehler sperren das Projektoeffnen; der Marker
bleibt zur Pruefung erhalten. Auch bei einem normalen Speicherfehler bleibt er stehen.
Ein spaeterer erfolgreicher Save persistiert die Commit-TxId; entfernt wird der Marker
erst durch den anschliessenden eindeutigen Recovery-Lauf.

Noch ausserhalb dieser Transaktion liegen das additive XTF-Rohdatenarchiv und die
Dateioperationen des Ein-Knopf-Imports. Sie koennen bei einem spaeten Projektkonflikt
nicht automatisch zurueckgenommen werden. Diese Restgrenze spaeter in Infrastructure
loesen, nicht im ViewModel verstecken.
Der Ein-Knopf-Import arbeitet datenseitig inzwischen wie der manuelle Lauf auf einer
Arbeitskopie: Die Live-Referenz wird erst bei Erfolg getauscht; Projektinstanz, Pfad
und inhaltliche Projektsignatur werden vor der Uebernahme erneut geprueft. Ein
fehlgeschlagener Projekt-Save wird laut gemeldet statt als „Import abgeschlossen".
Seine Dateioperationen (Archiv, Medienverteilung) laufen weiterhin direkt, also
ausserhalb der Staging-Sitzung.
Der manuelle PDF-Stapellauf bleibt bewusst getrennt vom fehlertoleranten PDF-Scan des
`ImportPostProcessingController`, weil beide verschiedene Fehlerregeln haben.

Geldrelevante Kosten-, Mengen- und Laengentexte in Kostenrechner, Matrix und Export
laufen zentral ueber `FachzahlParser` und nie ueber `CurrentCulture`: Punkt oder Komma
als Dezimaltrenner sowie korrekt gruppierte Schweizer Apostroph-/Leerzeichenwerte
werden auf de-DE, de-CH und en-US identisch behandelt; mehrdeutige Werte werden
abgelehnt. `CostCatalogStore` und
`MeasureTemplateStore` und `PositionTemplateStore` melden beschaedigte Default- oder
Override-Dateien mit
`loadError`. Kostenrechner, Haltungs-/Schachtmatrix und Builder sperren dann
Neuberechnung, Speichern und Geld-Exporte, statt mit leerem Katalog plausible
Nullwerte zu erzeugen. Fehlende, nichtpositive oder ungueltige Haltungslaengen
blockieren laengenbasierte Positionen im Kostenrechner und in der Matrix;
nichtpositive Schachtmengen blockieren Berechnung und Speichern ebenfalls.
Ausgewaehlte Kostenrechner-Zeilen mit negativer Menge oder negativem Preis werden
weder summiert noch gespeichert, uebernommen oder exportiert. NPK-Codes werden in
CSV und Excel als Text ausgegeben, damit etwa `612.110` nicht zu `612.11` gekuerzt
wird.
Die drei Stammdaten-Stores lehnen `null`-Strukturen, doppelte normalisierte
Kosten-/Vorlagen-Identitaeten und negative Mengen ab. Vor jedem Save wird auch eine
vorhandene Override-Datei neu gelesen; ein frisch erzeugter Store darf deshalb keine
beschaedigte Datei ueberschreiben, selbst wenn vorher kein Load aufgerufen wurde.
`CostStoreFileProbe` unterscheidet fehlende Dateien von Ordnern, Verknuepfungen und
unlesbaren Pfaden. `ProjectCostStoreRepository` verwendet diese Pruefung fuer
`costs.json`, `schacht_costs.json` und `schacht_empfehlungen.json`, liest ein
vorhandenes Ziel unmittelbar vor jedem Save erneut und ueberschreibt bei einem
Lesefehler nichts. Der Schacht-Massnahmendialog oeffnet in diesem Zustand nicht.

`PdfPrimaryDamageFindingBuilder` wandelt die aus PDF-Tabellen gelesenen Zeilen aus
`Primaere_Schaeden` in strukturierte `VsaFinding`-Eintraege um. Passende A-/B-
Streckenmarker mit gleicher Nummer und gleichem VSA-Code werden zu einem Bereich
verbunden. `PdfPrimaryDamageStructureSynchronizer` legt daraus bei fehlenden
Strukturdaten auch das Protokoll an. Bereits vorhandene Findings oder manuelle
Protokolle werden nicht ersetzt. Dadurch kann ein erneuter PDF-Import auch bestehende
Text-only-Haltungen sicher nachziehen.

Beim Teacher-Store ist die JSON-Karte verbindlich und `classes.txt` nur abgeleitet.
Scheitert das Schreiben der JSON-Karte, wird die vorherige `classes.txt`
wiederhergestellt oder eine neu angelegte Kopie entfernt.

Die versionierten Vorlagen liegen unter `training/class_maps/` und werden beim Build
nach `Data/Training/` kopiert. `detect_class_migration_v2.candidate.json` enthaelt
124 vollstaendige Alt-Zuordnungen. Davon sind nur die 10 BCC-Zeilen fuer den
persoenlich freigegebenen Bogen-Pilot auf `approved`; 114 Zeilen bleiben `pending`.
Unbekannte oder offene Klassen werden vor jeder lokalen Exportausgabe hart gestoppt;
es gibt keine stille neue ID und keinen automatischen SONST-Rueckfall.
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

- Fuer neues Training sind ausschliesslich persoenlich manuell codierte und
  bestaetigte `TrainingSample`-Eintraege zulaessig. `ConfirmedByUser` muss exakt
  mit `ApprovedBy` der Export-Registry uebereinstimmen; BBox und SAM-Segmentierung
  sind Pflicht. Teacher-, Auto-, Fremdbestaetigungen und unvollstaendige Handlabels
  bleiben im Inventar, werden aber nicht in train/val exportiert.
- `TrainingExportRegistryFileStore` liest
  `<KnowledgeRoot>\training\export_registry_v1.json` strikt. Status `candidate`,
  unbekannte Felder, fehlende Schutz-Sets oder abweichende Manifest-Hashes stoppen.
  Das optionale Feld `approved_sample_ids` begrenzt einen menschlich freigegebenen
  Pilot auf exakt diese TrainingSample-IDs. Ist es leer, bleibt das bisherige
  Verhalten mit allen geeigneten Goldsamples erhalten.
- Der Plan ist pfadfrei und enthaelt feste Klassen, Haltungs-Splits, Ausschluesse,
  Quell-Hashes und stabile `img_<sha256>.<endung>`-Namen. Gleiche Bild-SHAs werden
  einmal geschrieben; unterschiedliche Labels werden zusammengefuehrt.
  Beim Runden auf sechs YOLO-Nachkommastellen werden randbuendige BBox-Groessen
  erforderlichenfalls minimal nach innen begrenzt, damit eine vorher gueltige Box
  nicht durch reine Rundung ausserhalb des Bildes liegt.
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

Produktiv bleibt der allgemeine Export bewusst gesperrt, solange seine
Migrationszeilen nicht fachlich freigegeben sind. Der getrennte BCC-Bogen-Pilot ist
mit `BCC_bogen` und fester ID 14 freigegeben; sein Register darf nur die einzeln
persoenlich bestaetigten Goldsample-IDs enthalten. Diese Sperren nie automatisch umgehen.

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

`training/scripts/prepare_bcc_pilot.py` erzeugt nach einer schreibfreien Vorpruefung
das enge BCC-Register und den Auditbeleg. `training/scripts/train_bcc_pilot.py`
akzeptiert danach nur einen vollstaendig gehashten Export unter
`<KnowledgeRoot>\training\datasets`, trainiert vom unveraenderten
`sidecar/models/yolo26m/yolo26m.pt` und schreibt ausschliesslich einen nicht
aktivierten Kandidaten unter `training\models\candidates`. Es startet nie bei
erreichbarem Sidecar oder weniger als 28000 MB freiem VRAM und ersetzt keine
produktiven Gewichte. Der kleine BCC-Pilot verwendet die auf dieser Hardware
gemessene Batch-Groesse 3; `patience=0` fuehrt die verlangten Epochen vollstaendig
aus. Von Ultralytics erzeugte `train.cache`/`val.cache` werden nach jedem Lauf
entfernt, damit der plan-gesteuerte Datensatz unveraendert bleibt.

Der nicht aktivierte BCC-Kandidat kann ausschliesslich im Training Studio als
reiner Fototest verwendet werden. `TrainingPreviewDetectionService` ruft dafuer
den getrennten Sidecar-Endpunkt `POST /detect/yolo/bcc-test` auf. Der Sidecar
waehlt den Kandidaten selbst unter `<KnowledgeRoot>\training\models\candidates`,
akzeptiert nur `not_deployed`, Pilot `BCC_bogen`, mindestens 30 Bilder, die
freigegebene 15er-Klassenkarte und eine passende SHA-256. Er laedt ihn in den
eigenen GPU-Slot `YOLO_TEST`; das aktive Standardmodell im Slot `YOLO` wird weder
ersetzt noch entladen. Der Client darf keinen Modellpfad liefern.

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

Das Fenster bietet fuer den reinen Fototest `Aktives Standardmodell` und
`BCC-Testmodell (nicht aktiv)` an. Automatische Treffer erscheinen nur als blaue
Vorschau-Boxen mit Code und Klartext. Sie werden nie in `CurrentBox`, die SAM-Maske
oder einen Goldsample uebernommen. Nur die rote, vom Menschen gezogene Box kann
ueber Akzeptieren/Korrigieren gespeichert werden.
Das aktive Standardmodell darf nur bei ausdruecklichem `qualified=true` laufen.
Fehlende oder unlesbare Qualifikation sperrt den Fototest ebenfalls. Der await im
ViewModel bleibt auf dem WPF-UI-Kontext; danach gesetzte Anzeige-Eigenschaften duerfen
nicht mit `ConfigureAwait(false)` vom UI-Thread abgekoppelt werden.

Die Schaltflaeche `Foto allgemein mit KI pruefen` ist davon getrennt. Sie ruft ueber
`AnnotationWorkbenchService.SuggestPhotoAsync` den zentralen `IProtocolAiService`
mit dem ganzen Foto und dem aktiven VSA-Codekatalog auf. Der kataloggepruefte
Qwen-/KB-Vorschlag wird nur angezeigt und muss bewusst angeklickt werden. Rote
Hand-Box, SAM-Maske, bestehender Code und Beschreibung bleiben unveraendert; der
Aufruf schreibt weder Goldsamples noch KB-Daten. Der schnelle Vorschlag beim
Box-Ziehen bleibt der getrennte YOLO-Classifier-Weg. Nicht geladene Modelle und
unbekannte Klassen werden sichtbar abgewiesen statt als VSA-Code ausgegeben.
`AiInput.RequireImage` erzwingt fuer diesen Weg ein wirklich lesbares Foto; ein
reiner Text-/KB-Vorschlag ohne Bild ist verboten. Wechselt der Nutzer waehrend des
Aufrufs das Bild, wird das spaete Ergebnis verworfen.

Eine persoenlich uebernommene Auswahl aus dem VSA-Codierfenster ist dagegen eine
bewusste Handcodierung. `WorkbenchCodeSelectionMapper` uebernimmt deshalb neben
Code, Uhrlage und Stufe auch `ProtocolEntry.Beschreibung`.
`TrainingStudioViewModel.ApplyCodeSelection` ersetzt damit nur ein leeres Feld oder
den automatischen Platzhalter durch eine fertige Katalogbeschreibung mit Code;
selbst geschriebener Text bleibt erhalten. KI-Vorschlaege und direkt eingetippte
Codes erhalten weiterhin keine automatische Goldfreigabe. Rote Hand-Box, gueltige
SAM-Maske und persoenliches Akzeptieren bleiben fuer Gold immer Pflicht.

Das Training Studio zeigt den durch `PersonalGoldProgressCalculator` berechneten
Goldstand je Hauptcode mit Ziel 30-50 an und aktualisiert ihn nach jedem erfolgreichen
Speichern. Die Warteschlange `Unvollstaendige Goldframes` laedt nur persoenlich
bestaetigte Handlabels ohne Box oder SAM-Segmentierung. Beim Nachlabeln wird das
bestehende Sample anhand seiner ID ergaenzt; es entsteht kein doppelter Datensatz.

Die Schaltflaeche `Goldalbum` oeffnet `PersonalGoldAlbumWindow`. Das Fenster liest
ueber `IPersonalGoldAlbumService` ausschliesslich persoenlich bestaetigte Handlabels,
gruppiert sie nach Hauptcode und zeigt Bild, Code, Beschreibung, Datei- und
Geometriestatus. Es ist rein lesend und veraendert weder Bilder noch Trainingsdaten.

Neue Bilder koennen unter `<KnowledgeRoot>\training\gold_inbox` vorbereitet werden.
`PersonalGoldInboxFileService` legt die Hauptcode-Unterordner mit Code und Klartext,
zum Beispiel `BAB - Riss` und `BCA - Seitlicher Anschluss`, sowie
`_OHNE_ZUORDNUNG` sowie `_ERLEDIGT` an. Es liest nur JPG/JPEG/PNG aus der Wurzel
und der ersten Ordnerebene; alte reine Codeordner wie `BAB` bleiben lesbar,
`_ERLEDIGT` wird uebersprungen,
und folgt keinen Datei- oder Ordnerverknuepfungen. `Gold-Eingang oeffnen` zeigt den
Ordner, `Eingang laden` uebergibt den Stapel an den vorhandenen Pruefplatz. Der
Ordnername ist nur ein sichtbarer Hauptcode-Hinweis und wird nie automatisch als
finaler VSA-Code akzeptiert. Eingangsdateien bleiben unveraendert. Erst Codieren,
BBox, SAM-Segmentierung und persoenliches Akzeptieren erzeugen das Goldsample und
die inhaltsadressierte Kopie unter
`gold_frames\<Hauptcode - Klartext>\gold_<sha256>.<endung>`.

Goldstand, Goldalbum und Ordnerhinweis zeigen Hauptcodes ebenfalls mit Klartext.
Der nicht als Basiscode vorhandene BBD-Anker wird dabei fachlich als
`BBD - Eindringender Boden` bezeichnet und nicht mit der allgemeinen BB-Gruppe.

Beim Bestaetigen legt `AnnotationWorkbenchService` das unveraenderte Bild zuerst
inhaltsadressiert unter
`<KnowledgeRoot>\gold_frames\<Hauptcode - Klartext>\gold_<sha256>.<endung>` ab.
Der endgueltig gespeicherte Code bestimmt den Ordner; das gilt auch nach einer
persoenlichen Korrektur eines KI-Vorschlags.
Das Kundenoriginal bleibt unberuehrt. Scheitert die sichere Goldkopie, wird
nichts gespeichert. `TrainingFrameFileStore` prueft bestehende und neue Bildbytes;
eine beschaedigte alte Zieldatei wird nicht als Treffer akzeptiert, sondern durch
eine gepruefte atomare Kopie ersetzt.

Der Speicherweg trennt seit 2026-07-25 streng zwischen Entwurf und Gold
(„Gold-Wahrheit"). Vor dem Schreiben lehnt `GoldBeschreibungGuard`
Platzhalter-Texte („Ausmass ergaenzen") ab, und `SamMaskValidator`
(Infrastructure, neben `SamMaskDecoder`) prueft die Maske: nicht `Degraded`,
RLE strikt dekodierbar (Laufsumme = Breite x Hoehe), mindestens ein gesetztes
Pixel und mindestens ein echter Maskenpixel-Mittelpunkt innerhalb der Hand-Box.
Gerade und ungerade RLE-Tokenzahlen sind erlaubt, weil der echte Sidecar-Encoder
keinen kuenstlichen Abschlussrun anhaengt; Startwert und Runs bleiben streng.
Nur mit gueltiger Maske entsteht ein
Goldsample (`Status = Approved`, Gruen) mit KB-Index und Teacher-Eintrag; die
Teacher-Annotation traegt dabei `SourceSampleId` als Fremdschluessel. Ohne
gueltige Maske wird nur ein Entwurf (`TrainingSampleStatus.Draft`, Gelb, kein
KB-/Teacher-Eintrag) gespeichert, der in der Warteschlange „Unvollstaendige
Goldframes" zur Reparatur erscheint; das Nachlabeln mit Maske fuehrt ueber
denselben Weg zum Goldsample. Zusaetzlich verlangt
`KnowledgeBaseManager.IsIndexWorthy` die vollstaendige persoenliche
`ManualGoldTrainingPolicy`, Box, Maske und einen fertigen Text. Platzhalter duerfen
fuer historischen reinen YOLO-BBox-Export weiterverwendet werden, gelangen aber
weder beim Neuindexieren noch aus vorhandenen KB-Zeilen ins Qwen-Retrieval.
Damit werden Entwuerfe, fremde/alte Auto-Freigaben und unfertige Texte auch bei
Nachhol-/Rebuild-Laeufen gesperrt. Das Akzeptieren ist waehrend
eines laufenden SAM-Laufs sowie bei bereits laufendem Speichern gesperrt
(ViewModel-Flags).

Die Sample-Identitaet ist die `SampleId`: `MergeOrUpdateAsync` matcht zuerst
per Id, erst danach per Signatur (Alt-Aufrufer). Eine Codekorrektur an einem
Bestandssample ersetzt den Eintrag atomar ueber
`ITrainingSampleStore.ReplaceBySampleIdAsync` (ein Sperrvorgang, ein Schreiben)
und bereinigt den alten Stand: KB-Deindex ist produktiv verdrahtet
(`TrainingKnowledgeBaseSampleDeindexer`, kein No-op), alte Teacher-Eintraege
werden per `SourceSampleId` entfernt (Mehrdeutigkeit im Altbestand → Warnung
statt Loeschen). `MergeAndSaveAsync` dedupliziert per Signatur als Sperre
gegen versehentliches Doppel-Akzeptieren; der Neuanlage-Pfad nutzt
`TryAddNewAsync`, das eine uebersprungene Dublette sichtbar abweist statt
still fortzufahren (fruehere KB-Waisen entstanden genau so).

Mehrfachobjekte werden seit 2026-07-25 unterstuetzt: Neue Samples bauen ihre
Signatur mit Box als `caseId|code|meter|meter|b:x,y,w,h` (normalisiert, 3
Dezimalstellen). Zwei Schaeden mit gleichem Code am selben Meter, aber
verschiedenen Boxen, werden dadurch als zwei eigenstaendige Objekte mit
eigener SampleId, KB- und Teacher-Eintrag gespeichert; ein erneutes
Akzeptieren desselben Objekts (gleiche Box) wird weiterhin entdoppelt.
Altbestand mit 4-teiliger Signatur (ohne Box) bleibt gueltig.
Der Player-Codiermodus prueft Masken mit demselben strengen Format
(`SamMaskFormatValidator` in Application; `SamMaskValidator` in Infrastructure
delegiert dorthin und ergaenzt Degraded/Dekodierung/Box-Schnitt); ungueltige
Masken werden nicht uebernommen, das Sample bleibt sichtbar unvollstaendig.

Persoenliche Entscheidungen im Player-Codiermodus verwenden denselben Goldspeicher.
`CodingEventToSampleMapper` markiert nur `Accepted` oder `AcceptedWithEdit` mit
gesetztem Benutzer und Bestaetigungszeitpunkt als `ManualCoding` sowie
`ReviewApproved`/`ReviewCorrected`. `CodingTrainingSamplePersistenceCoordinator`
prueft zuerst den Eval-Schutz, kopiert vorhandene Fotos oder den bestaetigten
Player-Frame inhaltsadressiert in den Klartext-Hauptcode-Unterordner von
`gold_frames` und speichert danach
`training_samples.json` und den KB-Status. BBox und vorhandene SAM-RLE-Daten werden
aus dem Coding-Ereignis uebernommen. Fehlt Bild, Box oder SAM, bleibt der Eintrag
sichtbar unvollstaendig und darf nicht in den Trainings-Export.
Auch die Stapelspeicherung liefert ein echtes Ergebnis zurueck. Ein Fehler wird im
Player als rotes Overlay „Training nicht gespeichert" angezeigt und nicht mehr nur
im Hintergrundprotokoll versteckt.
Auch `CodingSessionService` indexiert aus diesem Weg nur strikt persoenlich
bestaetigte Goldsamples mit vorhandenem Goldbild. Der allgemeine Session-Abschluss
darf weder fremde Freigaben aufnehmen noch persoenliche Gold-Metadaten ueberschreiben.

`tools/PersonalGoldMigration` uebernimmt bestehende persoenliche Handlabels
wiederholbar in dieselben Klartext-Hauptcode-Unterordner. Vor dem Umschalten werden alle Quelldateien
geprueft; SQLite und `training_samples.json` werden bei einem Fehler zurueckgesetzt.
Nach erfolgreicher Umstellung wird auch das Gold-Gehirn-Dateimanifest erneuert.
Die nachvollziehbare Verteilung liegt unter
`<KnowledgeRoot>\training\gold_standard\main_code_inventory_v1.json`, die Pruefspuren
unter `<KnowledgeRoot>\training\gold_migrations`. Wissens-ZIP-Sicherungen enthalten
`gold_frames` rekursiv; Kundenoriginale werden nie veraendert.

`tools/GoldBrainSeparation` trennt einen vorhandenen Mischbestand einmalig vom neuen
Gold-Gehirn. Ohne `--execute` wird nur geprueft. Im Ausfuehrungsmodus baut
`PersonalGoldBrainSeparationService` zuerst einen vollstaendigen Arbeitsstand auf,
prueft JSON, Frames und SQLite feldgenau und benennt erst dann die Ordner auf demselben
Datentraeger atomar um. Alte absolute Goldbildpfade unter der bisherigen Wissenswurzel
werden dabei sicher auf das Lokalarchiv abgebildet; externe Bildpfade bleiben
unveraendert. Ueberlappende Wissens-, Archiv-, Spiegel-, Staging- oder
Legacy-Protokollpfade sperren den Lauf vor dem Commit.
Vor der ersten Umbenennung wird neben der Wissenswurzel das atomare Journal
`<KnowledgeRoot>.gold-brain-separation.commit.json` geschrieben. Ein spaeterer
Ausfuehrungslauf setzt einen unterbrochenen Commit nur anhand kanonisch gebundener
Pfade, Besitzmarker und gepruefter Vorherzustaende auf den Ausgangsstand zurueck;
ein Dry-Run meldet das
offene Journal nur und veraendert nichts. Die Fassade bleibt klein: Input-/
Pfadpruefung, Arbeitsstand, Journal, Commit und Recovery liegen in getrennten
internen Diensten. Der komplette lokale Altstand bleibt als
`<KnowledgeRoot>_ALT_<Zeitstempel>` erhalten; der bisherige Elements-Spiegel liegt
unter `<Elements>\Brain_Archiv\KI_BRAIN_ALT_<Zeitstempel>`. Das neue aktive Gehirn
enthaelt nur persoenlich bestaetigte Handlabels und deren Embeddings. Teacher- und
Protokoll-Kontext starten leer. Der Pruefbeleg und ein Dateimanifest liegen unter
`<KnowledgeRoot>\training\gold_standard`. Altarchive tragen einen Schutzmarker und
duerfen nicht wieder als aktive Wissenswurzel angeschlossen werden.
Nach dem Umschalten prueft `PersonalGoldArchiveRecoveryService`, ob persoenlich
bestaetigte `ManualCoding`-Faelle nur noch in der archivierten SQLite-KB stehen.
Nur solche Faelle mit vorhandenem Bild und Embedding werden inhaltsadressiert
nachgeholt. Alte `TeacherAnnotation`- und `VideoTimestamp`-Zeilen werden dadurch
nicht zu Hand-Gold umgedeutet. Vor der ersten Mutation liegt das atomare Journal
`<KnowledgeRoot>.gold-archive-recovery.transaction.json` samt geprueften
Vorherkopien fuer SQLite, Trainings-JSON, Inventar, Beleg und Manifest vor. Ein
Neustart setzt diese Dateien und neu angelegte Frames idempotent zurueck. Fremde
Audit-Artefakte, Hashabweichungen, unsichere Pfade oder Junctions werden niemals
geloescht, sondern sperren die automatische Recovery zur manuellen Pruefung. Das
Journal wird erst nach dem vollstaendigen neuen Manifest entfernt. Der Nachholbeleg
`gold_brain_archive_recovery_v1.json` dokumentiert IDs und neue Goldpfade.

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
- Freigegebene Protokolleintraege kommen getrennt aus
  `<KnowledgeRoot>\protocol_training.json`
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
