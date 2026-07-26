# CODEBASE-KARTE — SewerStudio

> **Zweck:** Architektur-Landkarte — welche Schicht ruft welche, welche Klasse macht was, wie Verträge zusammenhängen. Stand: 2026-07-26 (jeder Klassenname per `rg` in `src/`, `sidecar/`, `tools/` bestätigt).
> **Getrennte Quellen, kein Kopieren:** konkrete **Werte** (Pfade, Sidecar-Routen, Modellnamen, `active.json`, Wissensordner-Auflösung, VSA-Katalogdatei) stehen in `docs/SYSTEM-FAKTEN.md`; **Regeln/Prinzipien** (Thin-AI, Sprache, Arbeitsweise) in `CLAUDE.md`. Diese Karte beschreibt nur die Struktur.

---

## 1. Schichten

Vier .NET-Projekte plus Sidecar und Tools. Abhängigkeitsrichtung strikt von oben nach unten: UI → Infrastructure → Application → Domain. Application kennt nur Verträge (Interfaces), Infrastructure liefert die Umsetzung.

| Schicht | Projekt | Verantwortung | Kennt |
|---|---|---|---|
| Domain | `AuswertungPro.Next.Domain` | Fachmodelle, Feldschlüssel; kein Datei-I/O | nichts |
| Application | `AuswertungPro.Next.Application` | Interfaces, Verträge, reine Fachregeln (Policies, Validatoren, JSON-Verträge) | Domain |
| Infrastructure | `AuswertungPro.Next.Infrastructure` | Datei-I/O, Import, SQLite, HTTP-Sidecar, KI-Dienste, Backup | Application, Domain |
| UI | `AuswertungPro.Next.UI` | WPF, ViewModels, Fenster, Renderer, **Zusammensetzung der Dienste** | alle darunter |
| Sidecar | `sidecar/sidecar/` | lokaler FastAPI-Dienst für YOLO/DINO/SAM (siehe SYSTEM-FAKTEN.md, Abschnitt 3–4) | HTTP-Vertrag |
| Tools | `tools/*` | 41 eigenständige CLI-Werkzeuge (u. a. `StageAExporter`, `TrainingDataInventory`); alle sind in `AuswertungPro.sln` enthalten | eigene Referenzen |

Regel aus CLAUDE.md: neue Fachlogik als eigener Service mit Interface in Application/Infrastructure, nie in UI-Code oder Sidecar. UI ruft ViewModel/Service, nie direkt Infrastruktur.

---

## 2. DI / ServiceProvider-Aufbau

Zentrale Zusammensetzung: `src/AuswertungPro.Next.UI/ServiceProvider.cs`. Muster: **erst alle Dienste einmalig bauen, dann registrieren** — kein verstreutes `new` in ViewModels/Fenstern.

- **`ServiceProviderRegistrationMap`** (UI) — reine Zuordnung bereits gebauter Dienste zu ihren Vertragstypen (aktuell 130 Verträge). Erzeugt selbst nichts; `ServiceProvider.cs` ruft die Map erst nach vollständigem Aufbau auf.
- **`ServiceProvider.FullBackup.cs`** / **`ServiceProvider.TrainingYoloExport.cs`** — partielle Ergänzungen, die die Subsystem-Kompositionen anbinden und deren öffentliche Zugriffe unter ihren Interfaces registrieren.
- **`FullBackupComposition`** (Infrastructure) — baut Zielmarker, SQLite-Schnappschuss, Manifestprüfung und `FullBackupService` einmalig. Die UI liefert nur die Quellenfunktion (`IFullBackupSourcesProvider`). `BackupTargetGuard.UseMarkerGuard` darf beim zentralen Aufbau **nicht** verwendet werden; der Marker geht direkt an `FullBackupService`.
- **`DirectoryMirror` / `BackupTargetMarkerGuardService` / `KnowledgeMirrorMarker`** — sichern Dateiübernahme, exakten Besitz des Vollsicherungsziels und exakten Besitz des Echtzeitspiegels. Einstellungen, Logs und Desktop-Skripte sind optional; `KnowledgeRoot` und jede konfigurierte Projektquelle sind Pflicht.
- **`KnowledgeRealtimeMirrorService`** (Infrastructure) — führt beim Programmstart einen vollständigen inkrementellen Abgleich des aktiven `KnowledgeRoot` aus und überwacht danach jede Änderung. Das Ziel wird über den Datenträgernamen `Elements` als `<Elements>\Brain` gefunden. Normale Dateien werden geprüft und atomar ersetzt, SQLite-Datenbanken als Online-Schnappschuss gesichert. Fehlt die Platte, wird nach dem Wiederanschliessen ein Vollabgleich nachgeholt. Exakter Zielmarker, Quell-/Zielpfadgrenzen und Verknüpfungsschutz sichern alle Mutationen ab; ein Quellfehler stoppt die Löschphase.
- **`TrainingYoloExportComposition`** (UI) — dünne WPF-Hülle um `TrainingYoloExportRuntime`; `TrainingYoloExportDependencies` reicht nur den Coordinator an Fenster/ViewModel.

Gezielte statt globale Injektion: `MediaConflictsPageViewModel` erhält `ISafeShellOpenService` und `IExplorerRevealService` aus dem ServiceProvider — dort kein `Process.Start` und keine statische Shell-Fassade.

Die aktuellen Fabriken arbeiten mit `AiRuntimeSettings` (nicht dem entfernten `AiRuntimeConfig`).

### Wichtige DI-Zugriffe (Interface hinter dem Zugriff)

| Zugriff | Interface / Zweck |
|---|---|
| `Projects` | `IProjectRepository` |
| `PdfImport` … `KinsImport` | fünf Import-Verträge (siehe §4) |
| `StoredImportFiles` / `StoredImportFilePaths` | Schreiben / Auflösen gespeicherter Importquellen |
| `ImportFileStaging` / `ImportMediaDistribution` | `IImportFileStagingService` / `IImportMediaDistributionService` |
| `KnowledgePaths` / `KnowledgeRoot` | einmal aufgelöster KB-Ort (Auflösung: SYSTEM-FAKTEN.md, Abschnitt 2) |
| `Retrieval` | optionale KB-Suche, kann `null` sein |
| `AiSettings` | `IAiPlatformSettingsResolver` |
| `GpuModels` | `IGpuModelSelector` |
| `TrainingSamples` | `ITrainingSampleStore` |
| `TrainingDataInventory` | `ITrainingDataInventoryService` |
| `TrainingExportRegistry` | `ITrainingExportRegistryStore` |
| `TrainingYoloExportCoordinator` | `ITrainingYoloExportCoordinator` |
| `VsaYoloClasses` / `TrainingYoloClasses` | `IVsaYoloClassMapStore` (erweiterbar) / `ITrainingYoloClassMapStore` (strikt lesend) |

---

## 3. Domänenmodell

- **`HaltungRecord`** (Domain) — zentrales Fachmodell einer Haltung. Enthält `Fields`, `FieldMeta`, `VsaFindings`, `ProtocolEntry`, `Protocol`. Besitzt **keine** `DeepClone()`-Methode.
- **`FieldKeys`** (Domain) — kanonische Feldschlüssel. Zugriff immer über `record.GetFieldValue(FieldKeys.X)` / `SetFieldValue(...)`, nie über neue String-Literale.
- **`ImportStats`** — Ergebniszähler eines Imports: `Found`, `Created`, `Updated`, `Errors`, `Uncertain`, `Messages`.

Fachdomäne (Haltung, Schacht, DN, VSA-KEK-Codegruppen BC/BA/BB, Severity 1–5, Uhrlage, Punkt- vs. Streckenschaden): Definitionen in CLAUDE.md; konkrete Code-Zuordnungen in SYSTEM-FAKTEN.md, Abschnitt 7.

---

## 4. Import-Verträge & Workflow

Fünf manuelle Wege — **PDF, XTF, WinCan, IBAK, KINS** — mit einheitlicher Signatur (`Result<ImportStats>` je Weg, optionaler `ImportRunContext`).

**Ablauf-Steuerung (UI-Schicht, dünn):**
- **`ImportManualWorkflowController`** — kapselt die fünf Wege; kennt weder ServiceProvider noch Shell/ViewModel.
- **`ImportRunWorkflowController`** — bindet Projektinstanz, normalisierten Projektpfad und Berichtsordner; prüft nach jedem asynchronen Abschnitt Projektidentität und Abbruch neu. `ImportPageViewModel` verbindet nur Befehle und UI-Zustand.
- **`ImportPostProcessingController`** — fehlertoleranter PDF-Scan; bewusst **getrennt** vom manuellen PDF-Stapellauf (andere Fehlerregeln).
- **`PdfPrimaryDamageFindingBuilder` / `PdfPrimaryDamageStructureSynchronizer`** — wandeln PDF-Schadenszeilen in `VsaFindings` und Protokolleinträge um. Passende A-/B-Streckenmarker werden verbunden; vorhandene strukturierte oder manuelle Protokolldaten bleiben erhalten.

**Transaktionssicheres Staging (Infrastructure + Application-Vertrag):**
- Der Echtlauf legt eine `IImportFileStagingSession` in `ImportRunContext.FileStaging` ab.
- **`StoredImportFileService`** und der `IImportMediaDistributionService` (Umsetzung `MediaDistributionService`) schreiben geprüfte Kopien zuerst neben die Projektdatei unter `.import-staging/<Lauf-GUID>`. Vor dem ersten Datei-Move enthält `.import-transaction.json` alle vorbereiteten Ziele samt SHA-256; nach `Publish` wird der Ist-Stand geschrieben. `Accept` folgt erst nach `ReplaceProject`; `Dispose` nimmt nur vom Lauf neu angelegte, unveränderte Dateien zurück. Gleichheit wird am Inhalt geprüft, nie werden vorhandene/wiederverwendete Dateien gelöscht.
- **`ImportFileStagingService`** bereitet projektbezogene Kopien vor und nimmt sie bis zur Projektübernahme zurück. **`ImportFileStagingPathGuard`** und **`VerifiedImportFileCopy`** prüfen Pfadgrenzen, Verknüpfungen und Zielinhalt.
- **`FileImportTransactionJournal`** schreibt den atomaren Marker vor der ersten Veröffentlichung.
- **`ImportTransactionRecoveryService`** läuft vor der Projektübernahme. Marker-TxId gleich `Project.LastCommittedImportTxId` bedeutet „gespeichert, nur aufräumen"; sonst werden nur unveränderte, im Marker genannte Dateien zurückgenommen. Unsichere Pfade, Hashabweichungen, unlesbare Marker oder Aufräumreste ergeben `Blocked`; das Projekt bleibt geschlossen und der Marker erhalten.
- **`ImportOneClickProjectController`** arbeitet auf einer Projektkopie und prüft Projektinstanz, Pfad und Inhaltssignatur erneut; seine direkten Archiv-/Medienkopien liegen weiterhin ausserhalb des Journals.

**Lesen gespeicherter Importlisten:**
- **`StoredImportFilePathResolver`** liest Listen über `StoredImportFileRegistry.Load`, prüft zuerst den echten Projekt-Root, dann alte `Projektdateien\Imports`-Ablagen. Fehlende/unsichere/doppelte Treffer werden verworfen.
- **`VsaPageViewModel`** und **`InspectionProtocolFileLocator`** dürfen diese Pfadlogik **nicht** duplizieren; sie erhalten dieselbe Resolver-Instanz.
- **`ImportFileStoreService`** bleibt eine dateifreie Kompatibilitätsfassade (Leser-Rückfall für Altbestände erhalten).

**Bekannte Grenze:** Das additive XTF-Rohdatenarchiv und die Archiv-/Medienoperationen des Ein-Knopf-Imports liegen noch ausserhalb des Journals. Der Ein-Knopf-Import prüft zwar Projektinstanz, Pfad und Inhaltssignatur vor der Übernahme, kann direkt geschriebene Dateien bei einem späten Konflikt aber nicht automatisch zurücknehmen.

### Geldrelevante Kosten-/Exportpfade

- **`FachzahlParser`** (Application) parst Längen, Mengen und Preise kulturunabhängig. Punkt/Komma und korrekt gruppierte Schweizer Apostroph-/Leerzeichenwerte liefern unter de-DE, de-CH und en-US dasselbe Ergebnis; Mehrdeutiges wird abgelehnt.
- **`CostCatalogStore` / `MeasureTemplateStore` / `PositionTemplateStore`** melden beschädigte Default- und Benutzerdateien über `loadError`. Kostenrechner, Haltungs-/Schachtmatrix, Builder und Editoren sperren in diesem Zustand Neuberechnung, Speichern und Geld-Exporte.
- Die Stores lehnen `null`-Strukturen, doppelte normalisierte Kosten-/Vorlagen-Identitäten und negative Mengen ab. Vor jedem Save wird eine vorhandene Override-Datei erneut gelesen; auch eine neue Store-Instanz überschreibt deshalb keine beschädigte Datei.
- **`CostStoreFileProbe` / `ProjectCostStoreRepository`** schützen `costs.json`, `schacht_costs.json` und `schacht_empfehlungen.json` vor Ordnern, Verknüpfungen, Lesefehlern und direktem Überschreiben einer beschädigten Datei.
- Fehlende, nichtpositive oder ungültige Haltungslängen erzeugen weder im Kostenrechner noch in der Matrix Default-Meterkosten; nichtpositive Schachtmengen werden nicht still als `1` berechnet. Ausgewählte Kostenrechner-Zeilen mit negativer Menge oder negativem Preis werden weder summiert noch gespeichert, übernommen oder exportiert. Die betroffenen Pfade bleiben gesperrt.
- NPK-Codes werden in CSV und Excel als Text geschrieben, damit beispielsweise `612.110` erhalten bleibt.

---

## 5. KI-Pipeline

**Fluss:** C# steuert alles; der Sidecar liefert nur Inferenz.

```text
UI/Service  →  VideoAnalysisPipelineService | SingleFrameMultiModelService | VideoFullAnalysisService
            →  VisionPipelineClient (HTTP an Sidecar, X-Sidecar-Token)
            →  Sidecar: YOLO → DINO → SAM → Quantifizierung → optional Qwen
            →  C#: VSA-Code-Mapping → zeitliche Zusammenführung → QualityGate
```

| Klasse | Schicht | Verantwortung |
|---|---|---|
| `VideoAnalysisPipelineService` | Infrastructure | wählt Multi-Model- oder Ollama-Pfad für Videoanalyse |
| `MultiModelAnalysisService` | Infrastructure | gemeinsame Bildanalyse; YOLO nur bei Freigabe, sonst DINO/SAM ohne YOLO-Gate und Review-Pflicht |
| `VideoFullAnalysisService` | Infrastructure | Vollanalyse-/Rückfallpfad mit eigener Dedup-Logik |
| `SingleFrameMultiModelService` | Infrastructure | Live-Einzelframe; dieselbe fail-closed Detektorfreigabe wie der Batch-Pfad |
| `VisionPipelineClient` | Infrastructure | C#-HTTP-Client zum Sidecar |
| `AiStartupOrchestrator` | Application | lädt konfigurierte Ollama-Modelle vor, ruft danach `/warmup` |
| `GpuModelSelector` | Infrastructure | GPU-Automatik der Modellwahl (`IGpuModelSelector`) |
| `QualityGateService` | Infrastructure | Green/Yellow/Red aus verfügbaren Evidence-Signalen |
| `FullProtocolGenerationService` | Infrastructure | KI-Befunde → Protokolleinträge |
| `LiveFindingSummaryBuilder` | Application | Aufbereitung der Live-Befundzusammenfassung |

Modellstart, VRAM-Budget, Modellnamen und Sidecar-Routen: siehe SYSTEM-FAKTEN.md, Abschnitte 3–5. Kein ByteTrack/OC-SORT, kein echtes Multi-Object-Tracking im HEAD.

---

## 6. Merge- / Dedup-Semantik

**Framebasiert in C#**, kein Tracking.

- **`TemporalFindingDeduplicator`** — führt Videobefunde über Frames zusammen.
- **`TemporalCodeVotingService`** — zeitliche Code-Abstimmung über mehrere Frames.
- **`TrainingSampleGenerator`** — vermeidet Duplikate über die kanonische `Signature`.

Es gibt **keinen** separaten `DetectionAggregator` und keinen produktiven `KbDeduplicationService` (siehe §14). `UpdateActive` ist keine Dedup-Wahrheit.

---

## 7. KnowledgeBase & Laufzeit-Kontextwege

- **`KnowledgeBaseManager`** (Infrastructure) — SQLite-KB: blockiert Eval-kontaminierte Samples, akzeptiert nur indexwürdige, menschlich bestätigte Daten, schreibt per UPSERT nach `SampleId`.
- **`RetrievalService`** — Suche über Kosinus-Ähnlichkeit, prüft das Embedding-Modell; kann als `Retrieval`-Zugriff `null` sein.
- **`KnowledgeBasePathService`** — löst den KnowledgeRoot auf (Reihenfolge: SYSTEM-FAKTEN.md, Abschnitt 2).
- **`TrainingSamplesStore`** (`ITrainingSampleStore`) — JSON-Trainingssamples speichern/mergen.
- **`TrainingFrameFileStore.StoreExistingAsync` / `StoreBytesAsync`** — prüft vorhandene und neue Bildbytes, ersetzt ein beschädigtes inhaltsadressiertes Ziel atomar und kopiert nach `<KnowledgeRoot>\gold_frames\<Hauptcode - Klartext>`; das Original bleibt unberührt.
- **`CodingTrainingSamplePersistenceCoordinator`** — verbindet persönliches `Accepted`/`AcceptedWithEdit` im Player-Codiermodus mit Goldbild, `training_samples.json` und KB-Index. Eval-Bilder werden vor dem Goldschreiben gesperrt; BBox und vorhandene SAM-Maske bleiben erhalten. Stapelfehler werden als Ergebnis bis zum roten Player-Overlay „Training nicht gespeichert" weitergegeben.
- **`PersonalGoldFrameMigrationService`** — übernimmt vorhandene persönliche Handlabels wiederholbar in Klartext-Hauptcode-Unterordner von `gold_frames`; `PersonalGoldMigrationCommitter` hält Umschalten, Nachprüfung und Rücksetzung von `training_samples.json`/`KnowledgeBase.db` getrennt und schreibt Inventar, Prüfspur und ein erneuertes Dateimanifest.
- **`PersonalGoldBrainSeparationService`** — dünne Fassade für Gold-only-Arbeitsstand und atomare Aktivierung. `PersonalGoldBrainSeparationInput`, `PersonalGoldBrainWorkspace`, `PersonalGoldBrainFileService`, `PersonalGoldBrainDatabaseBuilder`, `PersonalGoldBrainManifestWriter`, `PersonalGoldBrainCommitJournalStore`, `PersonalGoldBrainCommitExecutor` und `PersonalGoldBrainCommitRecovery` trennen Pfadprüfung, Arbeitsstand, Datei-Sicherheitsgrenze, Datenbank/Manifest, Journal, Commit und Neustart-Rollback.
- **`PersonalGoldArchiveRecoveryService`** — dünne Fassade für den Vergleich von Trainingsliste und Altarchiv-KB. `LegacyPersonalGoldDatabaseReader` und `PersonalGoldArchiveDatabaseImporter` lesen/importieren nur erlaubte Altzeilen; `PersonalGoldArchiveRecoveryInput`, `PersonalGoldArchiveRecoveryTransaction`, `PersonalGoldArchiveRecoveryJournalStore`, `PersonalGoldArchiveRecoveryValidator`, `PersonalGoldArchiveRecoveryArtifacts` und `PersonalGoldArchiveRecoveryOutput` trennen Auswahl, Journal, Pfad-/Besitzprüfung, Vorherkopien/Rollback und Ausgabe.
- **`PersonalGoldProgressCalculator`** — berechnet rein lesend den Stand je Hauptcode; nur persönliche Handlabels mit BBox und SAM-Segmentierung zählen zum Ziel 30–50.
- **`IPersonalGoldAlbumService` / `PersonalGoldAlbumService`** — liefert alle persönlich bestätigten Handlabels gruppiert nach Hauptcode für das rein lesende Goldstandard-Fotoalbum.
- **`IPersonalGoldInboxService` / `PersonalGoldInboxFileService`** — legt den vorbereitenden Eingang unter `<KnowledgeRoot>\training\gold_inbox` mit lesbaren Ordnern wie `BAB - Riss` an, liest auch alte reine Codeordner und verändert keine Quellen.

**Zwei produktive Few-Shot-Kontextwege** (liefern nur Prompt-Beispiele, trainieren keine Modellgewichte):
1. Ähnliche bestätigte Fälle aus `KnowledgeBase.db` über `RetrievalService`.
2. Freigegebene Protokolleinträge getrennt über **`ProtocolTrainingFileStore`** (`<KnowledgeRoot>\protocol_training.json`).

Der frühere bildbasierte Few-Shot-Weg ist entfernt; `fewshot_examples.json`/`fewshot_images` sind nur noch unveränderte Legacy-Daten und werden nie wieder als Prompt-/Trainingsquelle angeschlossen.

---

## 8. TrainingDataInventory (AP 0.1)

Rein lesendes Inventar der Teacher-/Trainingsquellen; verändert nie Annotationen, Bilder oder Pfade. Bewusst getrennt in Verträge (Application), Datei-I/O (Infrastructure) und CLI (Tool).

**Application** (`Ai/Training/Inventory/`):

| Klasse | Verantwortung |
|---|---|
| `ITrainingDataInventoryService` / `TrainingDataInventoryRequest` | Vertrag; `InspectRuntimeSnapshotAsync` liefert in einem Live-Scan Bericht, typisierte Daten und Schutz-Snapshot |
| `TrainingDataInventoryRuntimeSnapshot` | der Live-Snapshot (einzige Sample-Wahrheit für Auswahl/Plan; nicht als zweite Datei gespeichert) |
| `TeacherInventoryPolicy` / `TeacherInventoryTriagePolicy` / `TeacherInventoryReasonPolicy` | verteilte Teacher-Regeln |
| `TrainingInventorySummaryBuilder` | baut die abgeleitete Zusammenfassung |
| `TrainingInventoryReportValidator` | strenger Vertrag (Schema 2.2): Pfad-, Triage-, Quellen-, Zusammenfassungsregeln vor Schreiben / nach Lesen |
| `TrainingDataInventoryJson` | strikter JSON-Vertrag (String-Enums, Pflichtfelder, lehnt unbekannte Felder ab) |
| `TrainingInventoryExitPolicy` | Erfolg nur bei je genau einer aktuellen Teacher- und Sample-Quelle, ohne Error-Issue |

**Infrastructure** (`Ai/Training/Inventory/`):

| Klasse | Verantwortung |
|---|---|
| `TrainingDataInventoryService` | rein lesende Orchestrierung |
| `TrainingInventorySourceReader` | stabile Datei-Schnappschüsse, protokolliert SHA-256, Größe, Änderungszeit |
| `TrainingInventoryPathResolver` | trennt Existenz, Schutz, Reparaturvorschlag, Hash |
| `TrainingInventoryFileEnumerator` | folgt keinen Links/Junctions/Reparse-Points |
| `TrainingInventoryEvalProtectionReader` | verlangt je Eval-Set `frozen=true`, prüft Bild-Hashes und Manifest, je Eval-Bild genau ein Kandidat |
| `TrainingInventoryIssueCollector` | hält die Orchestrierung frei von Meldeformatierung |
| `TrainingInventoryReportOutputPolicy` | prüft das Ziel vor Scan und vor Schreiben; Eval-Root bleibt immer geschützt |

**CLI:** `tools/TrainingDataInventory/` — Bericht + SHA-256 unter `<KnowledgeRoot>\training\reports\`. Eval-Schutz arbeitet fehlersicher: unvollständiger Schutz gibt keine Train/Val-Freigabe.

**Gold-Gehirn-Trennung:** `tools/GoldBrainSeparation/` prüft standardmässig nur.
Mit `--execute` baut es zuerst einen vollständigen Gold-only-Arbeitsstand und schaltet
erst nach feldgenauer JSON-/SQLite-, Frame- und Pfadprüfung atomar um. Alte absolute
Goldbildpfade unter der bisherigen Wissenswurzel werden auf das Lokalarchiv abgebildet,
externe Bildpfade bleiben unverändert. Überlappende Wissens-, Archiv-, Spiegel-,
Staging- oder Legacy-Protokollpfade werden vor dem Commit abgelehnt.
Das atomare Journal `<KnowledgeRoot>.gold-brain-separation.commit.json` liegt vor der
ersten Umbenennung vor. Ein späterer Ausführungslauf setzt einen unterbrochenen Commit
auf den eindeutig geprüften Ausgangsstand zurück;
ein Dry-Run verändert ein offenes Journal nie. Der lokale Altstand bleibt als
`<KnowledgeRoot>_ALT_<Zeitstempel>` erhalten, der bisherige Elements-Spiegel unter
`<Elements>\Brain_Archiv\KI_BRAIN_ALT_<Zeitstempel>`. Das neue aktive Gehirn enthält
nur persönlich bestätigte Handlabels samt Embeddings; Teacher- und Protokoll-Kontext
starten leer. Beleg und Dateimanifest liegen unter
`<KnowledgeRoot>\training\gold_standard`. Anschliessend erstellt
`KnowledgeRealtimeMirrorService` den neuen Spiegel `<Elements>\Brain`.
Anschliessend prüft `PersonalGoldArchiveRecoveryService` auf reine Datenbankreste:
Nur `ManualCoding` + persönliche Bestätigung + Bild + Embedding werden nachgeholt.
`TeacherAnnotation` und `VideoTimestamp` bleiben ausgeschlossen. Das Werkzeug kann
den Schritt einzeln mit `--recover-from <Altarchiv>` prüfen oder ausführen. Vor der
ersten Mutation schreibt es
`<KnowledgeRoot>.gold-archive-recovery.transaction.json` und geprüfte Vorherkopien
für SQLite, Trainings-JSON, Inventar, Beleg und Manifest. Beim Neustart wird ein
unterbrochener Lauf idempotent zurückgesetzt. Fremde Artefakte, Hashabweichungen oder
Verknüpfungen führen sicher zum Abbruch; das Journal verschwindet erst nach dem
vollständigen neuen Manifest.

---

## 9. YOLO-Detect-Klassenkarte v2 (AP 0.2, BCC-Pilot freigegeben)

Teacher-Karte und Trainingskarte sind absichtlich getrennt:

| Klasse | Rolle |
|---|---|
| `IVsaYoloClassMapStore` / `VsaYoloClassMapFileStore` | Teacher-Karte; `GetClassId` liest strikt, nur der Live-Teacher darf `GetOrAddClassId` erweitern (Konstruktoroption kann sperren) |
| `VsaYoloClassMapDocumentWriter` | JSON-Karte ist verbindlich, `classes.txt` nur abgeleitet; scheitert JSON-Schreiben, wird `classes.txt` wiederhergestellt/entfernt |
| `ITrainingYoloClassMapStore` / `TrainingYoloClassMapFileStore` | strikt lesender, unveränderlicher Snapshot für den Detect-Export: prüft Version 2, exakt 15 feste Klassen/IDs, echten VSA-Manifest-Hash, vier Quell-Hashfelder, `entry_counts` und Quellenreihenfolge |
| `CodingFindingCodeResolver` + `VsaCodeResolver` → `YoloClassVsaMapper.ToPersistableVsaCode` | Rückmapping der Detect-Klassen: `BBD_boden` wird gültiges `BBDZ`, `BCC_bogen` wird `BCC` |

Nur der VSA-Hash wird beim Lesen gegen die echte Datei neu berechnet; die drei übrigen Hashes sind Auditwerte der Erzeugung. Versionierte Vorlagen unter `training/class_maps/` werden beim Build nach `Data/Training/` kopiert. Von 124 Migrationszeilen sind nur die 10 BCC-Zeilen für den persönlich freigegebenen Bogen-Pilot `approved`; 114 bleiben `pending`.

---

## 10. Plan-gesteuerter YOLO-Export (AP 0.3, BCC-Pilot aktiv)

Verbindlicher Datenfluss — genau **ein** unveränderlicher Plan je Exportbefehl; Sidecar und lokaler Ausführer treffen keine eigene Klassen-/Split-/Datei-Entscheidung.

```text
TrainingCenterViewModel → dünner TrainingYoloExportWorkflow → TrainingYoloExportRuntime.CreateHybrid
tools/StageAExporter                                        → TrainingYoloExportRuntime.CreateLocal
beide → ITrainingYoloExportCoordinator (fest gebundene Roots)
      → export_registry_v1.json (approved)         [ITrainingExportRegistryStore / TrainingExportRegistryFileStore]
      → TrainingDataInventoryRuntimeSnapshot (ein Live-Scan)
      → strikt gelesene class_map v2
      → ITrainingExportPlanService                  erzeugt genau einen Plan
      → ITrainingExportExecutionService             Sidecar ODER lokaler Ausführer
      → ITrainingExportCompletionService            markiert nur bestätigte TrainingSamples
```

| Klasse | Schicht | Verantwortung |
|---|---|---|
| `TrainingYoloExportRuntime` | Infrastructure | gemeinsamer Aufbaupunkt; `CreateHybrid` (WPF, Sidecar + lokaler Rückfall), `CreateLocal` (CLI). Roots einmal gebunden |
| `ITrainingYoloExportCoordinator` / `TrainingYoloExportCoordinator` | App / Infra | besitzt Auswahl, Inventar, Klassenkarte, Plan, Ausführung, Abschluss; Root-Pfade nicht austauschbar |
| `ManualGoldTrainingPolicy` | Application | erlaubt nur persönlich manuell codierte und bestätigte Samples mit vorhandenem Goldbild, vollständig randgültiger BBox und formal gültiger SAM-RLE; mindestens ein echter Maskenpixel-Mittelpunkt muss in der Hand-Box liegen, der Bestätiger muss dem Freigeber entsprechen |
| `GoldDescriptionPolicy` | Application | trennt Geometrie und Text: Platzhalter dürfen historischen reinen YOLO-BBox-Export nicht entwerten, sind aber für KB-Index und Qwen-Retrieval gesperrt |
| `ITrainingExportPlanInputBuilder` / `TrainingExportPlanInputBuilder` | App / Infra | baut den Planner-Input nur aus den freigegebenen persönlichen Gold-TrainingSamples; Teacher-Daten bleiben Inventar |
| `TrainingExportPlanService` | Application | legt Klassen-IDs, Haltungssplit, Ausschlüsse, Dateinamen (`img_<sha256>.<endung>`), Labels und SHA-Zusammenführung fest; pfadfrei; randbündige BBoxen bleiben auch nach Sechsstellen-Rundung im Bild |
| `ITrainingExportPlanLocalExecutor` / `TrainingExportPlanLocalExecutor` | App / Infra | atomarer lokaler Ausführer: `.staging` → atomar nach `<KnowledgeRoot>\training\datasets\<plan_id>` |
| `ITrainingExportSidecarRequestBuilder` / `TrainingExportSidecarRequestBuilder` | App / Infra | verpackt den Plan für den strikten Sidecar-v2-Vertrag; `plan_sha256 == plan_id` |
| `ITrainingExportExecutionService` / `TrainingExportExecutionService` | App / Infra | Healthcheck, Anmeldung, Transport-Rückfall, Zielpfadprüfung; HTTP 4xx führt nicht zum lokalen Bypass |
| `ITrainingExportCompletionService` / `TrainingExportCompletionService` | App | markiert nur `TrainingSample`-Quellen, deren Bild-SHA der passende Plan bestätigt hat |
| `TrainingYoloExportWorkflow` | UI | nur Busy-, Fortschritts-, Fehlermeldungen — keine Datei-/Sidecar-Logik |

`PlanOnly` (bzw. StageA `--dry-run`/`--plan-only`) durchläuft Register, Inventar, Klassenkarte und Planer, schreibt aber nichts und mutiert keine UI-Liste. Eligibility/`ExportedUtc` werden erst nach bestätigter Ausführung einmal gemeinsam gespiegelt.

**Freigabestatus:** Nur der getrennte BCC-Bogen-Pilot ist freigegeben. Sein
`export_registry_v1.json` nennt mit `approved_sample_ids` jede erlaubte Goldsample-ID
einzeln. Andere Codes bleiben durch ihre offenen Migrationszeilen gesperrt.
`tools/StageAExporter` ist reine Kompatibilitäts-CLI vor derselben Runtime;
`--val-ratio` und `--allow-dummy-bbox` sind harte Fehler.

`training/scripts/prepare_bcc_pilot.py` erstellt das enge Register und einen
Auditbeleg. `training/scripts/train_bcc_pilot.py` prüft Exportbeleg, Hashes,
Klasse ID 14 sowie mindestens 30 BCC-Bilder. Es trainiert vom unveränderten
`sidecar/models/yolo26m/yolo26m.pt` nur einen nicht aktivierten Kandidaten unter
`<KnowledgeRoot>\training\models\candidates`. Ein laufender Sidecar oder weniger
als 28000 MB freier VRAM sperrt den Start; das bestehende Modell wird nie ersetzt.
Der BCC-Pilot trainiert mit Batch 3 und standardmässig ohne Early Stopping über alle
angeforderten Epochen. Er entfernt die nur von Ultralytics erzeugten Label-Caches
anschliessend wieder, damit der exportierte Datensatz unverändert bleibt.

**Sicherer BCC-Fototest:** `TrainingPreviewDetectionService` ruft den getrennten
Sidecar-Endpunkt `POST /detect/yolo/bcc-test` auf. Der Sidecar wählt selbst den
besten gültigen Kandidaten unter
`<KnowledgeRoot>\training\models\candidates`: nur `not_deployed`, Pilot
`BCC_bogen`, mindestens 30 Bilder, passende SHA-256 und die freigegebene
15er-Klassenkarte mit `BCC_bogen` auf ID 14. Der Kandidat läuft im eigenen
GPU-Slot `YOLO_TEST`; das aktive Standardmodell im Slot `YOLO` bleibt unverändert.
Ein Client kann keinen freien Modellpfad übergeben.

**Training Studio:** Der persönliche Goldstand je Hauptcode wird beim Öffnen und nach
jedem erfolgreichen Speichern live neu berechnet. Die eigene Warteschlange für
unvollständige Goldframes lädt nur persönlich bestätigte Handlabels ohne BBox oder
SAM-Segmentierung; ein fertiges Nachlabel ergänzt den bestehenden Datensatz über seine
Sample-ID. `tools/PersonalGoldMigration` dient für den einmaligen Altbestand und erzeugt
`training\gold_standard\main_code_inventory_v1.json` sowie eine Prüfspur unter
`training\gold_migrations`. Wissens-ZIP-Sicherungen nehmen `gold_frames` rekursiv mit.
Im Modelltest sind `Aktives Standardmodell` und `BCC-Testmodell (nicht aktiv)`
wählbar. Automatische Treffer werden blau mit Code und Klartext gezeichnet und nie
als Hand-Box, SAM-Maske oder Goldsample gespeichert. Nur die rote, persönlich
gezogene Box erreicht den bestehenden Akzeptieren-/Korrigieren-Speicherweg.
Das Standardmodell darf nur bei ausdrücklichem `detector_qualification.qualified=true`
laufen. False, fehlender Status oder Lesefehler sperren den Fototest. Der BCC-Testpfad
bleibt getrennt. In Batch- und Player-Analyse umgehen dieselben Zustände YOLO; DINO/SAM
laufen weiter und Health sowie Ergebnis bleiben sichtbar eingeschränkt/review-pflichtig.
Der Sidecar prüft Dateiname und SHA-256 des konkret aktiven PT-/TensorRT-/ONNX-Artefakts
gegen `sidecar/models/model_qualification.json`. Standard-Endpunkt und Warmup laden
ein nicht ausdrücklich freigegebenes YOLO nicht.

`training/scripts/model_collapse_check.py` liefert nur einen Geometrie-Beleg:
`PASS`, `FAIL` oder bei Inferenzfehlern/zu wenig verwertbaren Treffern
`INCONCLUSIVE`. Ein PASS aktiviert kein Modell.

`training/scripts/gold_stock_audit.py` prüft den Bestand schreibfrei. Es sperrt
Bildhash und komplette reservierte Eval-Haltung, normalisiert Haltungsnummern und
hält gleiche Haltungen sowie identische Bildbytes in einer gemeinsamen Split-Gruppe.
Stand 25.07.2026: 218 Einträge, 14 Drafts, 204 geometrisch verwendbar,
10 Bildduplikatgruppen und 196 noch nicht KB-/Qwen-taugliche Platzhaltertexte.
Der Vorschlag 143/25/36 ist nicht release-fähig, weil alle 204 verwendbaren Samples
noch Pseudo-CaseIds statt belastbarer Haltungsidentitäten besitzen.

`PersonalGoldAlbumWindow` zeigt diesen Bestand als Fotoalbum mit Hauptcode-Liste,
Kachelstapel und grosser Detailansicht. Hauptcode-Liste, Goldstand und
Ordnerhinweis zeigen Code plus Klartext. Es schreibt nichts. Neue Bilder werden über
`Gold-Eingang öffnen` in Klartext-Hauptcode-Ordner wie `BAB - Riss` gelegt und mit `Eingang laden` als
Prüfplatz-Stapel geöffnet. Der Ordner ist nur ein Hinweis: Erst die persönliche
Codierung mit BBox, SAM-Segmentierung und Akzeptieren erzeugt Goldstandard.
Alte Ordner nur mit Code bleiben lesbar und ihre Bilder werden nicht verschoben.
Fertig bearbeitete Eingangsdateien können manuell nach `_ERLEDIGT` verschoben
werden; dieser Ordner wird beim Laden übersprungen.

Dasselbe gilt im Player-Codiermodus: Eine ausdrückliche persönliche Annahme oder
Korrektur wird als `ManualCoding` mit `ReviewApproved` beziehungsweise
`ReviewCorrected` gespeichert. Ein vorhandenes Foto oder der bestätigte Frame wird
zuerst in `gold_frames\<Hauptcode - Klartext>` übernommen. Der endgültig
gespeicherte Code bestimmt den Ordner. Danach folgen Trainingsliste und KB-Index.
Ohne sicheres Bild bleibt der Eintrag nachvollziehbar, aber für Modelltraining gesperrt.
`CodingSessionService` indexiert dabei ebenfalls nur persönlich bestätigte
Goldsamples mit vorhandenem Goldbild; ein allgemeiner Session-Abschluss darf diese
Gold-Metadaten nicht überschreiben.

---

## 11. Ereignisbasierte Eval-Messung (AP 0.4a, technische Grundlage)

Die frühere Sammeldatei `EvalSetBenchmark.cs` ist aufgeteilt; öffentliche Klassennamen/Signaturen bleiben gleich (Dateien unter `Application/Ai/Evaluation/`).

| Klasse | Verantwortung |
|---|---|
| `EvalSetBenchmarkCase` (in `EvalSetBenchmarkModels.cs`) | trägt additiv `HoldingKey`, `ExpectedSeverity`, `EventId`, optional `MeterStart`/`MeterEnd` |
| `EvalSetBenchmarkDataset` | toleranter Loader für Altbestand-Sets |
| `EvalSetReleaseDatasetValidator` | `LoadAndValidate` für Release-Abnahme: fehlende Bilder/Haltungen, ungültige/widersprüchliche Werte stoppen; `EventId` nur bei Schäden Pflicht |
| `EvalSetV2Builder` | übernimmt die neuen Felder, verlangt Severity + Ereignis-ID bei Schäden |
| `EvalSetEventScorer` | zählt ein Ereignis über mehrere Frames einmal (Schlüssel Haltung+`EventId`), trennt Detect-Treffer vom Gate; Severity 4/5 ≥ 20 unabhängige Ereignisse, Wilson-/exakte 95%-Grenzen |
| `EvalSetManifestHasher` | Manifest-Hashing des Eval-Sets |

Das reale 120er-Set ist noch nicht mit Severity/EventId nachgepflegt — AP 0.4 ist nicht abgeschlossen; keine Modellfreigabe allein aus der Messlogik.

---

## 12. Zustandslose UI-Helfer & Renderer

Diese Klassen sind reine UI-Helfer und werden **nicht** im ServiceProvider registriert; die Fenster liefern nur Daten, Modus und Größe.

| Klasse | Verantwortung |
|---|---|
| `PhotoMeasurementGeometryService` | stabile öffentliche Fassade für reine Fotomessungs-Geometrie |
| `PhotoMeasurementAnglePlanBuilder` | zustandslose Winkel-, Abzweig-, Kreis-, Bogenplanung (nicht in WPF zurückschieben) |
| `PipelinePipeRadarRenderer` | zustandslose Zeichnung des Rohr-Radars der Videoanalyse |
| `LiveFrameRingOverlayRenderer` | gemeinsame Ring-Zeichnung mit drei Stilen (`Compact`/`Detail`/`Interactive`) |
| `PipelineLiveFrameOverlayRenderer` | Leer-/Größenregeln des eingebetteten Live-Rings |
| `LiveDetectionGeometryMapper` | gemeinsamer Uhrparser, Uhrwinkel, Fassade auf die zentrale Ringgeometrie |
| `RingSectorGeometry` | geometrische Ringform |
| `StatusColors` | zentrale Statusfarben (u. a. `Current.SeverityOverlay`) |
| `PipelineProgressMapper` | laufbezogene Fortschritts-/ETA-/Live-Frame-Abbildung (eine Instanz je Lauf) |
| `PipelineResultPresenter` | zustandslose Abschlussabbildung; baut ≤ 250 sichtbare `DetectionItem`-Zeilen |

**SAM-Review im Training Center:** `TrainingReviewSamWorkflow` prüft Kandidat/Box/Frame, startet den bedarfsgesteuerten `ITrainingReviewSamSegmentationService` (Umsetzung `TrainingReviewSamSegmentationService`) und bereitet Maske/Status auf. Der Durchmesser kommt aus `TrainingCenterWindowDependencyFactory`; nur `null` wird zu 300 mm. Datei-/Masken-/Einstellungslogik nicht ins Fenster zurückschieben.

---

## 13. Test-Guards

- **Golden-Test** unter `tests/Fixtures/TrainingExport/` — führt denselben Fall (Train, Dev-Val, Multi-Label-Bild) durch **beide** Export-Ausführer; relative Pfade, SHA-256 und Bytes aller Ausgabedateien müssen identisch sein.
- **Eval-Kontaminationsschutz** — `EvalContaminationGuard` (Application) plus Blockierung in `KnowledgeBaseManager`; `TrainingInventoryEvalProtectionReader` sichert Freeze-/Hash-Integrität je Eval-Set.
- **Inventar-Vertrag** — `TrainingInventoryReportValidator` prüft Schema 2.2 vor Schreiben und nach Lesen; fokussierte Tests unter `tests/AuswertungPro.Next.Infrastructure.Tests/Ai/Training/Inventory/`.
- **BBD-Rückmapping** — Integrationstest schützt die Kette `BBD_boden → BBDZ`.
- **Eval-CSV/JSON-Ausgaben** — Verhaltenstests sichern alle sieben Ausgaben samt Kopfzeilen und Escaping.
- Weitere Testprojekte: siehe SYSTEM-FAKTEN.md, Abschnitt 1.

---

## 14. Entfernte / nicht existente Klassen (per `rg` bestätigt, 0 Treffer)

Diese Namen dürfen **nicht** als Ist-Zustand auftauchen (Erwähnung nur als „entfernt"/„existiert nicht"):

`FewShotExampleStore` · `DetectionAggregator` · `YoloDatasetExportService` · `InferenceOrchestratorService` · `KbDeduplicationService` · `BenchmarkMetricsStore` · `BenchmarkRunner` · `EvalSetGenerator` · `PdfProtocolTableParser` · `CreateFewShotStore` · `EvalSetBenchmark` (Sammeldatei, aufgeteilt) · `AiRuntimeConfig` (durch `AiRuntimeSettings` ersetzt).

Ebenfalls nicht im HEAD: ByteTrack/OC-SORT/echtes Multi-Object-Tracking, automatische 8B→32B-Laufzeit-Eskalation, `UpdateActive` als Dedup-Wahrheit. Vollständige Negativliste mit Pfaden/Modellen/Routen: SYSTEM-FAKTEN.md, Abschnitt 8.
