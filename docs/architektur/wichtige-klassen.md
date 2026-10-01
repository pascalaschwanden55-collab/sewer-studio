# Wichtige Klassen (Übersicht)

> Aus `CLAUDE.md` ausgelagert am 30.09.2026 (Wartbarkeitsaudit, Befund Z1). Der Text ist
> **unverändert** übernommen: Geltende Regeln stehen neben datierten Arbeitsständen und
> Messverläufen. Bei Widersprüchen gilt der jüngere Abschnitt und im Zweifel der Code
> samt seinem Test. Veränderliche Zahlen (Dienstanzahl, Testanzahl) sind Momentaufnahmen.
>
> Neue Erkenntnisse zu diesem Bereich hier eintragen, **nicht** in `CLAUDE.md`.

## Inhalt

- Wichtige Klassen

## Wichtige Klassen
- `VideoAnalysisPipelineService`  → waehlt Multi-Model- oder Fallback-Pfad fuer Videoanalyse
- `MultiModelAnalysisService`     → YOLO/DINO/SAM/Qwen-Pipeline mit framebasiertem Dedup; Ausfallschutz und Checkpoint/Resume unten
- `IAnalysisCheckpointJournal`/`AnalysisCheckpointJournal` → append-only JSONL-Checkpoint pro Video (neben der Trace-Datei, Name = SHA256-Kurzhash des Videopfads). Jeder bearbeitete Frame schreibt genau einen Zustand: `update` (mit Befunden), `advance` (normal uebersprungen), `retry_required` (Transport-/Modell-/Verarbeitungsfehler). Ein Resume uebernimmt nur den lueckenlosen, gueltigen Anfang ab Frame 1 und replayt ihn exakt ueber `TemporalFindingDeduplicator.Update(...)` bzw. `AdvanceAll()` — dadurch liefert Abbruch+Fortsetzung dieselben Detections wie ein ununterbrochener Lauf. `retry_required` beendet den verwendbaren Bereich (ab dort neu inferieren, stale Schweif wird abgeschnitten); fehlende/doppelte/ruecklaufende Frame-Nummern, unbekannte Zeilentypen oder eine beschaedigte mittlere Zeile verwerfen das Resume vollstaendig (frischer Start + Logwarnung); nur eine unvollstaendige letzte Zeile wird sicher gekuerzt. Fehlende Pflichtfelder (Zeit, Meter, Schaetzflag, bei update Findings/Meterquelle) werden NICHT durch Standardwerte erfunden, sondern verwerfen das Resume ebenfalls. `CleanupCompletedJournals` loescht ausschliesslich streng lesbare, abgeschlossene, aeltere Journale — offene oder beschaedigte nie; prozessweit auf hoechstens einen Lauf pro Tag gebremst, Alter wird vor dem Einlesen geprueft; Fehler beim Aufloesen/Aufzaehlen der Ablage ueberspringen nur die Bereinigung mit Warnung — die Analyse laeuft immer weiter
- `SidecarOutageGuard`/`QwenOutageTracker` → Ausfallschutz des Multi-Model-Laufs: 8 Folge-Frames mit Sidecar-Transportfehler (YOLO/DINO/SAM gemeinsam, Reset implizit ueber Frame-Indizes) brechen den Lauf degraded ab; Qwen/Ollama ist ein eigener Prozess und erzeugt ab 8 Folgefehlern nur eine Degraded-Notiz (`NotedErrorCount` bleibt nach spaeterem Erfolg erhalten). Ein Nutzerabbruch per CancellationToken wird sofort weitergeworfen und zaehlt nie als Ausfall. Mehr als 10 % fehlerbedingt uebersprungene Frames setzen `Incomplete=true` an `VideoAnalysisResult` und `PipelineResult` (Surfacing ueber den Warnungspfad)
- Sidecar-Haertung (Paket 2): Der Sidecar arbeitet mit besitzbasierten Busy-Leases (`gpu_manager.acquire_busy/release_busy`, uuid-Besitzer-ID): Predict-Lock ZUERST, Lease DANACH; nur der Besitzer entfernt seine Lease; Wartende koennen weder Busy-Uhr noch Zustand verschieben. Einheitlich fuer YOLO (GPU+CPU als logische Lease `YOLO_CPU`), DINO, SAM, BCC und YOLO-cls (`YOLO_CLS`); CPU-Inferenzen werden bewusst ueberwacht, der Watchdog laeuft daher unabhaengig vom Geraet. VRAM-Eviction ist atomar (Auswahl + letzte Lease-Pruefung + Reservierung unter einem kurzen `_global_lock`); Modellreferenzen, `empty_cache` und GC werden danach ohne diesen Lock bereinigt, damit Health/Watchdog auch bei blockierter CUDA-Bereinigung ansprechbar bleiben. `unload` verweigert bei laufender Inferenz; kein sicherer Kandidat → `insufficient_vram` (mit free/required/reserved_gb im 503-Detail). Den GETEILTEN Slot `YOLO_TEST` benutzen BCC-Pilot und Lernstufen-Klassifikation gemeinsam. Welches Gewicht drinliegt, sagt allein `SlotState.content_id` (Gewichts-SHA-256): `ensure_loaded(..., content_id=...)` laedt bei Abweichung neu, `discard_foreign_content` raeumt fremden Inhalt VOR der eigenen Lease (die eigene Lease wuerde das Entladen sonst sperren), und beide Wrapper teilen sich `yolo_test_slot.PREDICT_LOCK`. Nie wieder eine Modulvariable je Wrapper als Slot-Wahrheit einfuehren: Damit sah keiner den Wechsel des anderen und es inferierte still das fremde Modell (Audit 2026-08-14, S-H1)
- `SidecarInsufficientVramException` → C#-Antwort auf `insufficient_vram`: `VisionPipelineClient` parst 503-Bodys defensiv (echter Vertrag: `code` + Zahlen auf Top-Ebene, `detail` als Klartext; verschachteltes Format toleriert, korrupt = allgemeiner Fehler; Vertragstest mit woertlichem Python-JSON); nur dieser Code wird zum eigenen Kapazitaetsfehler (kein HTTP-Retry, kein Outage-Zaehlen, kein Sidecar-Restart; Frame-Catch: Skip-Quote + Trace degraded + Checkpoint retry_required + Degraded-Grund mit VRAM-Zahlen). `model_unloaded` bleibt gezielt retryfaehig, unbekanntes 503 bleibt Transportfehler. Sidecar-seitig sind gleichzeitige Modell-Ladungen ueber In-flight-Reservierungen koordiniert (`_inflight_loads` unter dem kurzen `_global_lock`): zwei Ladevorgaenge sehen nie denselben freien VRAM (effektiv frei = frei − laufende Reservierungen; `reserved_gb` = Ollama-Reserve + In-flight-Summe)
- `SidecarRestartService` → kontrollierter Neustart nur des EIGENEN Sidecars (max 1 Versuch pro Analyselauf): Prozess-Tracking mit PID + Startzeit + Prozessart (`AiStartedProcessKind` Sidecar/Ollama) + Programmpfad; veraltete Eintraege werden bei jeder Abfrage entfernt. Nur die ausdrueckliche Art `Sidecar` beweist Besitz und erlaubt einen Kill; `Unknown`, Ollama oder ein hinterlegter, aktuell nicht lesbarer/abweichender Programmpfad sperren fail-closed. Kill-Fehler oder Timeout → kein Neustart (kein zweiter Sidecar). Ohne /health-PID: ein lebender eigener Sidecar wird zuerst verifiziert beendet (nie daneben gestartet), ein frueher eigener, beendeter Sidecar bleibt ueber `HadTrackedSidecarProcess` wiederstartbar (Start- ≠ Kill-Berechtigung, auch nach Watchdog-Exit), nur Ollama/Unknown → kein Blindstart. Ein Python-Kindprozess ohne eigenen Tracking-Eintrag muss ein Python-Image tragen; Baseline-Snapshot + Re-Probe direkt vor dem Kill binden Startzeit und Programmdatei. Erfolg erst nach 2 aufeinanderfolgenden /health-Polls
- `SidecarRequestTimeoutException` → interner Inferenz-Timeout (getrennt vom Benutzerabbruch, der OCE bleibt): zaehlt als Transportfehler, kein Retry, Meldung mit Modell-Label + Endpunkt, keine Tokens; Health-/Trainingsaufrufe und Ollama-Timeout bleiben unabhaengig
- `VideoFullAnalysisService`      → Vollanalyse-/Fallback-Pfad mit eigener Dedup-Logik
- `SingleFrameMultiModelService`  → Live-Einzelframe YOLO/DINO/SAM
- `VisionPipelineClient`          → C#-HTTP-Client zum Sidecar
- `SidecarEndpointPolicy`         → gemeinsame Token-Grenze fuer Haupt-, Start- und Neustartpfad: `X-Sidecar-Token` wird ausschliesslich an Loopback-Endpunkte gesendet; bei LAN-/Remote-URLs bleibt der Header leer
- `QualityGateService`            → Green/Yellow/Red aus verfuegbaren Evidence-Signalen
- `FullProtocolGenerationService` → KI-Befunde zu Protokolleintraegen mappen
- `IOfferPdfExportService`         → Vertrag (Application/Output): kapselt Vorlagen-/Logo-Pfadbau + PDF-Renderer; ViewModels newen keinen Renderer mehr
- `OfferPdfExportService`          → Impl (Infrastructure): loest Pfade auf, delegiert an `OfferHtmlToPdfRenderer` (injizierbarer Render-Delegate als Test-Seam); Modell typsicher ueber `IOfferPdfModel`
- `IQuickScanService`/`IQuickScanSession` → Vertraege (Application/Ai): KI-Schnellscan + kurzlebige Sitzung (eigener Ollama-Client); DTOs `QuickScanSegment/Progress/Result` liegen ebenfalls in Application.Ai
- `QuickScanSession`               → Impl (Infrastructure): baut ffmpeg-Pfad, eigenen `OllamaClient` und `QuickScanService`, besitzt den Client (`IDisposable`). Erzeugt ueber `ServiceProvider.CreateQuickScanSession(cfg)`; der Player-`QuickScanController` newt keine KI-Infrastruktur mehr
- `KnowledgeBaseManager`          → SQLite-KB: Samples + Embeddings indexieren/retrieven
- `TrainingSamplesStore`          → JSON-Trainingssamples speichern/mergen
- `MeasureRecommendationService`  → lernt bestaetigte Sanierungsmassnahmen nur aus echten
  BA-/BB-Schadenscodes. `MeasureRecordParser` liest Meter-zuerst-, PDF- und Altzeilen ueber
  `PrimaryDamageLineParser`; Meterwerte, Operator-Codes sowie BC-Bestandesmerkmale und
  BD-Allgemeinzustand gelangen nicht ins Modell. `MeasureRecommendationPersistence`
  haelt die reine Bereinigungs-/Migrationslogik aus dem IO-Service heraus. Beim ersten Laden migriert
  `measures_learning.json` atomar auf Version 3 (mit `.bak`) und verwirft ein altes
  Modell der Version 1. Bis zum erneuten Training werden die bereinigten Lerndaten direkt
  verwendet. Die VSA-Bewertung veraendert keine Sanierungsmassnahmen. Ein Vorschlag wird
  nur bewusst fuer die ausgewaehlte Haltung erzeugt; einen Stapelbefehl gibt es dafuer
  nicht. Der Automatikvorschlag ist noch kein Lernfall. Erst die
  anschliessend von der Fachperson bearbeitete und gespeicherte Fassung wird gelernt.
  Hand- und Importwerte bleiben geschuetzt.
- `PhotoMeasurementGeometryService` → stabile oeffentliche Fassade fuer reine Fotomessungs-Geometrie
- `PhotoMeasurementAnglePlanBuilder` → getrennte Winkel-, Abzweig-, Kreis- und Bogenplanung ohne UI-Zustand
- `PipelinePipeRadarRenderer` → zustandslose WPF-Zeichnung des Rohr-Radars; das Fenster liefert nur Daten, Modus und Groesse
- `PipelineLiveFrameOverlayRenderer` → bewahrt Leer-/Groessenregeln des eingebetteten Live-Rings und delegiert die Zeichnung
- `LiveFrameRingOverlayRenderer` → gemeinsame Ring-Zeichnung fuer Hauptfenster, abgedocktes Fenster und Player mit drei getrennten Stilen
- `LiveDetectionGeometryMapper` → gemeinsamer Uhrparser, Uhrwinkel und Fassade auf die zentrale Ringgeometrie
- `PipelineProgressMapper` → laufbezogene Fortschritts-, ETA- und Live-Frame-Abbildung; liefert dem Fenster nur Render-/Weiterleitungs-Hinweise
- `PipelineResultPresenter` → zustandslose Abschlussabbildung fuer Statistik, Telemetrie und hoechstens 250 sichtbare Befunde

- `ManualGoldTrainingPolicy`      -> erlaubt fuer neues Training nur persoenlich bestaetigte `ManualCoding`- oder streng belegte `PdfPhoto`-Samples mit vorhandenem Bild, randgueltiger BBox und SAM-Segmentierung; mindestens 80 % der Maskenpixel muessen in der Hand-Box liegen
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
- `tools/DetectReleaseHoldoutPdfExtractor` -> liest codierte PDF-Protokolle und erzeugt einen hashgebundenen, nicht trainierbaren Extraktionsbeleg fuer den Mehrklassen-Release-Holdout
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
- `HoldingNameFromShafts`          -> leitet den Haltungsnamen aus `Schacht_oben`/`Schacht_unten` ab und BEHAELT dabei die vorhandene Reihenfolge (im Bestand steht bei Gegenbefahrung auch der untere Schacht vorn). Ein Name, der auf keines der beiden Muster passt, bleibt unangetastet. `DataPageCellEditController.ApplySchachtChange` ist der gemeinsame Weg fuer Tabellen-Edit und Formular-Editor; die Namensaenderung laeuft danach ueber den normalen Umbenennungsweg, damit Verteilordner, Dateien und PDF-Text mitgehen
- `HoldingRenameFileService`       -> benennt eine Haltung samt Projekt-Verteilordnern und gespeicherten Medienpfaden um; externe Kundenordner sind ausgeschlossen
- `HoldingFolderRenameTransaction` -> benennt Dateien und Unterordner rekursiv, erkennt abweichende datumsbasierte Alt-Dateinamen und kann jeden ausgefuehrten Schritt zurueckrollen
- `StoredImportFileService`       -> kopiert Importquellen, loest Namenskollisionen und schreibt die Pfadlisten zentral
- `StoredImportFilePathResolver`  -> liest gespeicherte XTF-/PDF-Listen zentral und loest moderne sowie bestehende Projektpfade sicher auf
- `ImportFileStagingService`      -> bereitet projektbezogene Importkopien geprueft vor und nimmt sie bis zur Projektuebernahme zurueck
- `MediaDistributionService`      -> verteilt Medien hinter `IImportMediaDistributionService`; die UI erzeugt ihn nicht selbst
- `ShaftDistributionService`      -> kapselt die Schachtverteilung und staged projektinterne Ziele ueber dieselbe Importtransaktion
- `TrainingCenterDocumentFileStore` -> speichert das UI-unabhaengige Training-Center-Dokument atomar mit Backup und Rueckfall
- `KnowledgeBackupEngine`         -> exportiert/importiert Wissens-ZIPs, SQLite-Snapshot, Ruecknahme und Nachbearbeitung ausserhalb der UI
- `ServiceProviderRegistrationMap` -> ordnet die bereits gebauten Dienste ihren 141 Vertragstypen zu und erzeugt selbst nichts

