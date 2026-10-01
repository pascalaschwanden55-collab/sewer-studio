# CODEBASE-KARTE — SewerStudio

> **Zweck:** Architektur-Landkarte — welche Schicht ruft welche, welche Klasse macht was, wie Verträge zusammenhängen. Gesamtstand: 2026-07-29 (jeder Klassenname per `rg` in `src/`, `sidecar/`, `tools/` bestätigt); Klassenkarten- und Trainingsstand aktualisiert am 2026-08-02; Multi-Objekt-Gold im Training Studio ergänzt am 2026-08-03.
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

## 9. YOLO-Detect-Klassenkarte v3 (AP 0.2, Mehrklassen-Export freigegeben)

Teacher-Karte und Trainingskarte sind absichtlich getrennt:

| Klasse | Rolle |
|---|---|
| `IVsaYoloClassMapStore` / `VsaYoloClassMapFileStore` | Teacher-Karte; `GetClassId` liest strikt, nur der Live-Teacher darf `GetOrAddClassId` erweitern (Konstruktoroption kann sperren) |
| `VsaYoloClassMapDocumentWriter` | JSON-Karte ist verbindlich, `classes.txt` nur abgeleitet; scheitert JSON-Schreiben, wird `classes.txt` wiederhergestellt/entfernt |
| `ITrainingYoloClassMapStore` / `TrainingYoloClassMapFileStore` | strikt lesender, unveränderlicher Snapshot für den Detect-Export: v2 bleibt mit 14 Klassen lesbar, aktiv ist v3 mit exakt 15 festen Klassen/IDs; geprüft werden VSA-Manifest-Hash, Herkunfts- und persönliche Gold-Belege, `entry_counts` und Quellenreihenfolge |
| `CodingFindingCodeResolver` + `VsaCodeResolver` → `YoloClassVsaMapper.ToPersistableVsaCode` | Rückmapping der Detect-Klassen: `BBD_boden` wird gültiges `BBDZ`, `BCC_bogen` wird `BCC` |

Nur der VSA-Hash wird beim Lesen gegen die echte Datei neu berechnet; die historischen
Herkunftshashes bleiben Auditwerte der Erzeugung. Versionierte Vorlagen unter
`training/class_maps/` werden beim Build nach `Data/Training/` kopiert. Die aktive
v3-Migration besitzt 142 Zeilen: 92 Teacher-Codes, 35 Legacy-Schlüssel, 10
produktive Modellnamen und 5 Annotation-Overrides. Für Teacher-Codes sind 60
`map`- und 12 `discard`-Entscheidungen persönlich freigegeben. Insgesamt stehen
73 Zeilen auf `approved`, 69 bleiben `pending`. Die 72 beobachteten Quellcodes
enthalten neu `BAFCZ -> BAF_oberflaeche`. Der persönliche Beleg bindet den
Kandidaten-Audit mit SHA-256
`bb7f01f6b3582029ad4393c7217e5c2bbbb4ed5770ab15c807a574972b4905ba`
und den `training_samples.json`-Snapshot mit SHA-256
`bfcb3362762dc552861feb0680f1267e086e8d7d3fb71d70e5806841b82daa83`.

---

## 10. Plan-gesteuerter YOLO-Export (AP 0.3, DETECT_ALL aktiv)

Verbindlicher Datenfluss — genau **ein** unveränderlicher Plan je Exportbefehl; Sidecar und lokaler Ausführer treffen keine eigene Klassen-/Split-/Datei-Entscheidung.

```text
TrainingCenterViewModel → dünner TrainingYoloExportWorkflow → TrainingYoloExportRuntime.CreateHybrid
tools/StageAExporter                                        → TrainingYoloExportRuntime.CreateLocal
beide → ITrainingYoloExportCoordinator (fest gebundene Roots)
      → export_registry_v1.json (approved)         [ITrainingExportRegistryStore / TrainingExportRegistryFileStore]
      → TrainingDataInventoryRuntimeSnapshot (ein Live-Scan)
      → strikt gelesene class_map v3
      → ITrainingExportPlanService                  erzeugt genau einen Plan
      → ITrainingExportExecutionService             Sidecar ODER lokaler Ausführer
      → ITrainingExportCompletionService            markiert nur bestätigte TrainingSamples
```

`TrainingExportRegistryFileStore.cs` enthält den Ablauf,
`TrainingExportRegistryFileStore.Validation.cs` die strikte Schutzprüfung und
`TrainingExportRegistryFileDocuments.cs` die internen JSON-Modelle.

| Klasse | Schicht | Verantwortung |
|---|---|---|
| `TrainingYoloExportRuntime` | Infrastructure | gemeinsamer Aufbaupunkt; `CreateHybrid` (WPF, Sidecar + lokaler Rückfall), `CreateLocal` (CLI). Roots einmal gebunden |
| `ITrainingYoloExportCoordinator` / `TrainingYoloExportCoordinator` | App / Infra | besitzt Auswahl, Inventar, Klassenkarte, Plan, Ausführung, Abschluss; Root-Pfade nicht austauschbar |
| `ManualGoldTrainingPolicy` | Application | erlaubt nur persönlich bestätigte `ManualCoding`- oder streng belegte `PdfPhoto`-Samples mit vorhandenem Goldbild, vollständig randgültiger BBox und formal gültiger SAM-RLE; mindestens 80 % der Maskenpixel müssen in der Hand-Box liegen, gespeicherte Maskenfläche und RLE müssen übereinstimmen, der Bestätiger muss dem Freigeber entsprechen |
| `GoldDescriptionPolicy` | Application | trennt Geometrie und Text: Platzhalter dürfen historischen reinen YOLO-BBox-Export nicht entwerten, sind aber für KB-Index und Qwen-Retrieval gesperrt |
| `ITrainingExportPlanInputBuilder` / `TrainingExportPlanInputBuilder` | App / Infra | baut den Planner-Input nur aus den freigegebenen persönlichen Gold-TrainingSamples; Teacher-Daten bleiben Inventar |
| `TrainingExportPlanService` | Application | legt Klassen-IDs, Haltungssplit, Ausschlüsse, Dateinamen (`img_<sha256>.<endung>`), Labels und SHA-Zusammenführung fest; pfadfrei; randbündige BBoxen bleiben auch nach Sechsstellen-Rundung im Bild |
| `ITrainingExportPlanLocalExecutor` / `TrainingExportPlanLocalExecutor` | App / Infra | atomarer lokaler Ausführer: `.staging` → atomar nach `<KnowledgeRoot>\training\datasets\<plan_id>` |
| `ITrainingExportSidecarRequestBuilder` / `TrainingExportSidecarRequestBuilder` | App / Infra | verpackt den Plan für den strikten Sidecar-v2-Vertrag; `plan_sha256 == plan_id` |
| `ITrainingExportExecutionService` / `TrainingExportExecutionService` | App / Infra | Healthcheck, Anmeldung, Transport-Rückfall, Zielpfadprüfung; HTTP 4xx führt nicht zum lokalen Bypass |
| `ITrainingExportCompletionService` / `TrainingExportCompletionService` | App | markiert nur `TrainingSample`-Quellen, deren Bild-SHA der passende Plan bestätigt hat |
| `TrainingYoloExportWorkflow` | UI | nur Busy-, Fortschritts-, Fehlermeldungen — keine Datei-/Sidecar-Logik |

`PlanOnly` (bzw. StageA `--dry-run`/`--plan-only`) durchläuft Register, Inventar, Klassenkarte und Planer, schreibt aber nichts und mutiert keine UI-Liste. Eligibility/`ExportedUtc` werden erst nach bestätigter Ausführung einmal gemeinsam gespiegelt.

**Freigabestatus:** Das aktive Mehrklassen-Register `DETECT_ALL` nennt mit
`approved_sample_ids` jede erlaubte Goldsample-ID einzeln. Seine 72 persönlich
entschiedenen Teacher-Codes dürfen exportiert werden; nicht belegte und weiterhin
offene Codes bleiben gesperrt. Der enge BCC-Bogen-Pilot bleibt als eigener Altweg
erhalten. Eine Register- oder Exportfreigabe aktiviert kein Modell.
`tools/StageAExporter` ist reine Kompatibilitäts-CLI vor derselben Runtime;
`--val-ratio` und `--allow-dummy-bbox` sind harte Fehler.

`negative_images` kennt zwei klar getrennte Vertraege: Eine alte reine Registry
darf den flachen `negative_pool` weiterhin lesen. Neue Registrys verwenden nur
`source_type=reviewed_negative_set` und binden je Bild Set-/Manifest-ID, echte
Haltung samt Gegenrichtung, festen Split, Review, Queue, Kandidatenliste und
class_map. Gold-Audit, Python-Prepare und C# lesen Manifest, die vier Receipt-Dateien
und Bildbytes erneut; die Set-Datei- und Registry-Bildmengen muessen exakt passen;
Legacy und strikte Eintraege in derselben Registry sind verboten. Der
Kontaminationsscan nimmt sowohl den alten Pool als auch alle streng validierten
Saetze unter `training/negatives/sets` in den Bild- und Haltungsschutz auf.

`training/scripts/derive_negative_set_for_gold_audit.py` erzeugt aus einem
unveränderten, vollständig reviewten Satz einen neuen auditgebundenen Satz. Es
entfernt nur Testhaltungen und Splitkonflikte und protokolliert jeden Ausschluss;
eine bytegleiche Gold-/Negativkollision bleibt ein Fehler. Der aktuelle Satz
`bcc_hn_c25fd2f9d33f` enthält 9 Bilder (7 Train, 2 Validation), Manifest-SHA-256
`518a341419b285da88ce674accfe7b0b41330f8cae736ef87a95ea9a48221772`.

`training/scripts/prepare_detect_gold.py` erstellt das `DETECT_ALL`-Register aus
einem ausdrücklich gebundenen Gold-Audit und ausschliesslich streng reviewten
`all_classes_clear`-Negativen. Das aktuelle Register enthält 898 Goldinstanzen
(713 Train, 185 Validation) und die 9 strikten Negative. Der Exportplan
`9eb020e303225109849cc3a4036cd33288ff0120efd1557a910484f4bd2a61f8`
führt bytegleiche Goldbilder zusammen und enthält 856 Bilder (689 Train,
167 Validation) mit 898 Boxen in 13 der 15 festen Klassen. Das Werkzeug sperrt
negative Bilder gegen alle Auditrollen:
derselbe Bildhash ist immer unzulässig, Testhaltungen samt Gegenrichtung sind
gesperrt und abweichende Train-/Validation-Rollen stoppen ebenfalls. Dadurch darf
auch eine nur im Testsplit vorkommende Haltung nie als Trainingsnegativ dienen.

`training/scripts/train_detect_gold.py` prüft Plan, Receipt, Klassenkarte, alle
Dateihashes und Labels nochmals und schreibt nur Kandidaten mit
`candidate_status=not_deployed`. Der Kandidat `detect_gold_9eb020e30322` hat
40/40 Epochen beendet. Seine interne Validation ergibt P 0,3917, R 0,3129,
mAP50 0,3026 und mAP50-95 0,1726; Gewicht-SHA-256 ist
`fdf30f77b6aa6271014d130248fde99089854bfc0e58b44d75d462b3b9172ebf`.
Das produktive Modell wird nicht ersetzt. Der historische Kandidat
`detect_gold_ffbb8612fe50` beendete 40/40 Epochen mit P 0,4156, R 0,2575,
mAP50 0,2417 und mAP50-95 0,1286; diese Werte sind keine Release-Freigabe.

`training/scripts/prepare_bcc_pilot.py` erstellt das enge Register und einen
Auditbeleg. `training/scripts/train_bcc_pilot.py` prüft Exportbeleg, Hashes,
Klasse ID 14 sowie mindestens 30 BCC-Bilder. `data.yaml` darf nur die lokalen
Ziele `.`, `images/train` und `images/val` verwenden; Receipt-, YAML- und
Klassen-Hash werden in neue Kandidatenmanifeste gebunden. Trainiert wird vom unveränderten
`sidecar/models/yolo26m/yolo26m.pt` nur einen nicht aktivierten Kandidaten unter
`<KnowledgeRoot>\training\models\candidates`. Ein laufender Sidecar oder weniger
als 28000 MB freier VRAM sperrt den Start; das bestehende Modell wird nie ersetzt.
Der BCC-Pilot trainiert mit Batch 3 und standardmässig `patience=10`; nur
`patience=0` erzwingt alle angeforderten Epochen. Er entfernt die nur von
Ultralytics erzeugten Label-Caches anschliessend wieder, damit der exportierte
Datensatz unverändert bleibt.

**Unabhaengiger BCC-Release-Holdout:**
`training/scripts/bcc_release_holdout.py` prueft neue XTF-Fotoquellen gegen alle
lokal nachvollziehbaren Kandidaten-, Sample-, Negativ-, Collapse- und Eval-Spuren.
Es sperrt gleiche Bildbytes und dieselbe physische Haltung in beiden Richtungen,
validiert Kandidaten-Datensaetze samt Receipt, lokalen YOLO-Pfaden, Klassen- und
Konfigurationshashes sowie bestehende Eval-Hash-Manifeste. Nur die exakt vier
historischen Kandidaten-IDs mit unveraenderter Manifest-SHA duerfen noch ohne
diese direkte Konfigurationsbindung bestehen. Ein Kandidatenmanifest ohne exakt
`pilot=BCC_bogen` stoppt ebenfalls. Auch alte Collapse-Berichte werden ueber ihre
noch vorhandenen Bildpfade einbezogen; ein falsches `dateien`-Feld, ein leerer
Eintrag oder ein nicht eindeutig aufloesbarer reiner Dateiname stoppt den Scan. Es
kopiert nur verifizierte Bilder und veroeffentlicht atomar unter
`<KnowledgeRoot>\eval_set\subsets\bcc_release_holdout_<sha>`. Der getrennte lokale
Pruefplatz `tools/EvalVisibilityReview/bcc_release_holdout_review_server.py` zeigt
den festen Pruefauftrag `BCC — Bogen`, aber weder bildbezogenen XTF-Untercode,
verdeckte Vorauswahl noch Modellvorhersage. Er speichert die menschliche
Positiv-/Negativ-/Ausschlussentscheidung ausserhalb des eingefrorenen Sets.
Atomisches Speichern, ein prozessweiter Datei-Lock und eine Versionspruefung
verhindern unbemerktes Ueberschreiben durch parallele Pruefplaetze.
`ready_for_binary_evaluation` verlangt eine vollstaendige, hashgebundene Review
mit mindestens 20 Positiv- und 20 Negativhaltungen; das eingefrorene Manifest
bleibt dabei als Erstellungsbeleg `not_evaluated`. Beim V1-Holdout muessen
Kandidatenumfang sowie die aggregierten
Fingerprints der Bild-Hashes und Haltungs-Aliase exakt gleich bleiben; eine
Aenderung dieser Werte verlangt einen neuen Holdout.
Der reale V1-Review ist abgeschlossen: 60/60 Bilder, 29 positiv, 31 negativ,
0 ausgeschlossen. `training/scripts/evaluate_bcc_release_holdout.py` bindet
Review, Kandidatenmanifeste, Gewichte, Aufhebungsmarker, Klassenkarte,
Bild-Momentaufnahmen, Geraet und Qualitaetsgrenzen. Mit festem `conf=0.25`,
`imgsz=1280` und Klasse 14 schreibt es zuerst einen labelblinden Beleg und
wertet nur dessen erneut gelesene SHA-gebundene Bytes aus. Ein technischer
Teilfehler verhindert den endgueltigen Bericht; Training, Aktivierung und
produktive Artefakte bleiben unberuehrt. Der Vergleich vom 28.07.2026 hatte
240 fehlerfreie Vorhersagen. Die zwei relevanten Kandidaten erreichten
TP/FN/TN/FP 24/5/9/22 beziehungsweise 26/3/6/25. Beide haben zu viele
Fehlalarme; es gibt keinen eindeutigen Spitzenreiter und der Status bleibt
`comparison_complete_not_release_qualified`. Nach einer Kandidatenauswahl ist
fuer jede spaetere Aktivierung ein frischer Bestaetigungsholdout erforderlich.
`EvalContaminationGuard.IsEvalHaltung` blockiert fuer alle Eval-Sets auch die
umgekehrte Richtung desselben Schachtpaars.

**Allgemeiner Detect-Release-Holdout:**
`prepare_detect_release_pdf_extraction.py` waehlt frische PDF-/Video-Haltungen
ohne Modellvorhersage. Nur ein exakt stemgleiches Video ist zulaessig.
`tools/DetectReleaseHoldoutPdfExtractor` uebernimmt eindeutig zugeordnete,
bereits vom Operateur codierte PDF-Fotos und einen deterministischen Video-Frame;
PDF, Video, Haltung und Bildbytes bleiben hashgebunden. Der Extraktionsbeleg
sperrt Training und Gold. `prepare_detect_release_holdout.py` prueft Kandidat,
class_map v3, VSA-Hash, alle gebundenen und ungebundenen Trainingsdatasets,
bekannte Bildbytes sowie beide Haltungsrichtungen. Mehrdeutige Bildhashes werden
vollstaendig ausgeschlossen. Vor der atomaren Veroeffentlichung werden Staging,
Dateimenge und alle Hashes erneut geprueft. Der lokale Pruefplatz
`detect_release_holdout_review_server.py` zeigt keine KI-Vorhersage; PDF-Code und
Befund sind nur Operateur-Referenz. Positive Bilder brauchen Boxen fuer alle
sichtbaren Objekte, echte Negative duerfen keine der 15 Klassen enthalten.
`detect_release_holdout_status.py` verlangt 20 bestaetigte Instanzen je Klasse,
75 Negative und 30 negative physische Haltungen. Der am 03.08.2026 eingefrorene
Bestand `detect_release_holdout_45b66da2c778` umfasst 400 Bilder aus 216 frischen
Haltungen. Der Review ist mit 241 positiven, 74 negativen und 85 ausgeschlossenen
Bildern vollstaendig; 315 Bilder sind auswertbar. Er bleibt wegen 74 statt 75
Negativbildern und mehrerer Klassen unter 20 Instanzen `coverage_incomplete`,
nicht trainierbar und nicht Gold-faehig. Fuer `BBD_boden` fehlt jede Review-Box;
fuer `BBD_boden` und `SONST_schaden` hatte auch der gepruefte Modellkandidat null
Trainingsinstanzen.

`evaluate_detect_release_holdout.py` ist der fail-closed Diagnoseweg fuer diesen
Bestand. Es prueft die gebundenen Artefakte erneut, verlangt einen ausgeschalteten
Sidecar, kopiert das Kandidatengewicht privat und kontrolliert den exakten
15-Klassen-Vertrag. Der Bildweg bleibt `PIL RGB -> geprueftes BGR-Array ->
Ultralytics`. Mit `conf=0,25`, `imgsz=1280` und `IoU=0,5` werden zuerst alle 400
Bilder labelblind inferiert und in einem SHA-gebundenen Ledger versiegelt; erst
danach wird die Review gelesen. Objekt-TP/FP/FN und Bild-Fehlalarme auf echten
Negativbildern werden getrennt ausgewiesen. Technische Fehler auf gewerteten
Bildern verhindern den Bericht. Der Status bleibt immer Diagnose: keine
Aktivierung, kein Training und keine Modellfreigabe.

Der erste fehlerfreie GPU-Lauf vom 03.08.2026 erreichte auf 350 Soll-Boxen
TP/FP/FN 36/59/314 (P 37,9 %, R 10,3 %, F1 16,2 %). Neun von 74 echten
Negativbildern hatten mindestens einen Fehlalarm. `BCC_bogen` traf 27/37,
`BCA_anschluss` 8/39 und `BAF_oberflaeche` 1/89; alle weiteren gemessenen
Klassen hatten null exakte Treffer. Der Kandidat bleibt `not_deployed`. Der
vollstaendige Beleg steht in
`docs/quality/DETECT-RELEASE-DIAGNOSTIC-2026-08-03.md`.

**BCC-Hard-Negative-Review:** `training/scripts/bcc_hard_negative_review.py`
verwendet frische XTF-Fotos nur fuer eine verdeckte Fehlalarm-Vorauswahl. Es
friert class_map v3, VSA-Manifest, Modellgewichte, Trainings-/Registry-Snapshot
und alle geschuetzten Eval-Sets ein. Gleiche Bildbytes, bekannte Haltungen und
deren Gegenrichtung sind gesperrt; in die Queue gelangt genau ein Vollbild je
physischer Haltung. `bcc_hard_negative_review_server.py` liefert dem Browser
nur Bild-ID und Bildbytes. Nur `all_classes_clear` bedeutet, dass keine der 15
Detect-Klassen sichtbar ist; ein blosses „kein BCC“ reicht nicht. Der aktuelle
Review `bcc_hn_d37e1e0e481c` ist vollstaendig: 10 von 14 Bildern sind
`all_classes_clear`, 4 enthalten eine gemappte Klasse. Der Publisher prueft den
eingefrorenen Schutzstand erneut und hat nur diese 10 Bilder atomar als
`training/negatives/sets/bcc_hn_54f6608b975a` veroeffentlicht. Der Satz besitzt
8 Train- und 2 Validation-Haltungen; sein Manifest und die kopierten Receipts
binden Review, Queue, Kandidatenliste und class_map v3 bytegenau.

Der daraus erzeugte Datensatz
`f23a95b149addf9d24365834b563b7784f76132190d9e4e60f4c61e84a652bc9`
enthaelt 57 BCC-Positive und 10 Negative (48 Train, 19 Validation). Kandidat
`bcc_bogen_f23a95b149ad_hn10_strict` stoppte nach 33 Epochen und bleibt
`not_deployed`. Die interne Validation (P 0,5371; R 0,4706; mAP50 0,4829)
und Aktivierungen auf 2/2 strikten Validation-Negativen sowie 7/14
nicht mittrainierten Altnegativen reichen nicht fuer eine Freigabe.

**Sicherer BCC-Fototest:** `TrainingPreviewDetectionService` ruft den getrennten
Sidecar-Endpunkt `POST /detect/yolo/bcc-test` auf. Der Sidecar wählt selbst den
besten gültigen Kandidaten unter
`<KnowledgeRoot>\training\models\candidates`: nur `not_deployed`, Pilot
`BCC_bogen`, mindestens 30 Bilder, passende SHA-256 und die freigegebene
15er-Klassenkarte mit `BCC_bogen` auf ID 14. Der Kandidat läuft im eigenen
GPU-Slot `YOLO_TEST`; das aktive Standardmodell im Slot `YOLO` bleibt unverändert.
Ein Client kann keinen freien Modellpfad übergeben.

**Training Studio:** Der persönliche Goldstand je Hauptcode wird beim Öffnen und nach
jedem erfolgreichen Speichern live neu berechnet. Album, Fortschritt und Warteschlange
zeigen auch eigene Entwürfe und persönlich bestätigte Reparaturfälle ohne vollständige
Geometrie; sie zählen erst mit randgültiger BBox und gültiger SAM-Segmentierung als Gold.
Ein fertiges Nachlabel ergänzt den bestehenden Datensatz über seine Sample-ID.
`tools/PersonalGoldMigration` dient für den einmaligen Altbestand und erzeugt
`training\gold_standard\main_code_inventory_v1.json` sowie eine Prüfspur unter
`training\gold_migrations`. Wissens-ZIP-Sicherungen nehmen `gold_frames` rekursiv mit.

Der PDF-Prüfimport läuft über den Application-Vertrag
`ITrainingPdfReviewImportService` und
`Infrastructure/Ai/Training/PdfReview/TrainingPdfReviewImportService`. Er liest ein
Haltungsprotokoll unverändert, kontrolliert dessen SHA-256, filtert kleine Logos und
ganzseitige Grafiken und ordnet nur über denselben Fotoblock, eine exakte
Foto-ID/Datei oder die exakte Kombination aus Videozeit, Meter und Befund zu.
Mehrdeutige Kandidaten werden gezählt und übersprungen. Mehrere Codes an einem Foto
bleiben mehrere Prüffälle. Der Reader stoppt bei insgesamt mehr als 256 MiB
extrahierten Fotobytes oder 250 Millionen Fotopixeln fail-closed.

Mehrere PDF-Quellordner laufen über
`Application/UseCases/PdfTrainingReview/TrainingPdfReviewBatchImportUseCase`.
Der Infrastructure-Dienst `TrainingPdfFolderDiscoveryService` sucht rekursiv,
stabil sortiert und ohne Reparse Points; er prüft die komplette Root-Pfadkette
und jeden Ordner nochmals unmittelbar vor dem Lesen. Überlappende Wurzeln,
doppelte Pfade und identische PDF-Inhalte werden dedupliziert. Die PDFs werden
sequenziell verarbeitet, Fehler werden pro PDF sichtbar gesammelt und andere
PDFs laufen weiter. Die WPF-Oberfläche bietet Mehrfachauswahl, Fortschritt und
Abbruch; während des Imports bleiben Review-Aktionen gesperrt. Die horizontale
Bildliste virtualisiert ihre 160-Pixel-Vorschauen.

`TrainingPdfReviewProtectedImportService` schützt den Einzelweg; der Batch lädt
denselben `TrainingPdfReviewProtectionSnapshot` genau einmal. Der Snapshot
akzeptiert nur 64-stellige SHA-256-Werte und normalisierte numerische
Haltungskeys. Bei konfiguriertem Eval-Root verlangt der PDF-Weg mindestens
Haltungskeys, weil eine sichere CMYK/YCCK-Normalisierung die Bildbytes ändern
kann. Exakte Bildbytes sowie gleiche oder umgedrehte Eval-Haltungen werden pro
Foto vor Matching und vor `pdf_review_imports` ausgelassen. Ungültige oder
unlesbare Schutzdaten sperren den ganzen PDF-Import.
Der `ServiceProvider` registriert unter `ITrainingPdfReviewImportService` nur
diese geschützte Fassade. Der rohe `TrainingPdfReviewReader` ist intern und
wird ausschliesslich für den bereits geschützten Batch verwendet.

PDF-JPEGs mit `DeviceCMYK`/Adobe-YCCK oder einer nicht identischen `Decode`-Regel
laufen vor Vorschau, SAM und Training ueber den Application-Vertrag
`ITrainingPdfJpegColorNormalizer`. `UI/Services/TrainingPdfJpegColorNormalizer`
rekonstruiert zuerst die PDF-DCT-Kanalpolaritaet, wendet danach `Decode` an und
speichert das Ergebnis als RGB-PNG. `TrainingPdfEmbeddedImageReader` kapselt
Format-, Mass- und Farbraumpruefung ausserhalb des Dokument-Readers.
Unbekannte oder nicht sicher dekodierbare Farbraeume sowie CMYK-JPEGs ohne
eindeutigen Adobe-Farbmarker werden ausgelassen; gewoehnliche RGB-JPEGs bleiben
bytegleich.
`WorkbenchSourceSuggestion` hält die sichtbare
Operateurvorgabe getrennt von KI-Vorschlägen und von `ExistingCode`. Die
inhaltsadressierte Arbeitskopie liegt unter
`training\pdf_review_imports\<vollstaendiger-pdf-sha256>`; erst Hand-BBox, gültige
SAM-Maske und persönliche Bestätigung erzeugen Gold.
Das gespeicherte Sample trägt `SourceType=PdfPhoto`; `Notes` bewahrt Dokumentname,
vollständigen PDF-Hash, Seite, Foto-ID und Zuordnungsart als Prüfspur.
`TrainingPdfHaltungId` normalisiert echte Haltungsnummern ohne Eval-Abkürzungen;
kompakte Datumsblöcke vor der Datei-ID werden nur bei passendem Elternordner
abgetrennt. Der Reader dekodiert einen sicher erkannten Custom-Font-Shift je Seite
auch im lokalen Fotokontext. Ein `Haltungsinspektion`-Haupttitel ist kanonisch; nur
die zweizeilige Fretz-Tabelle derselben Titelseite darf einen internen Alias
belegen. `Haltungsbilder`-Titel lernen keine Aliase, während direkte
`Haltung`-Felder ohne Haupttitel echte Abschnittsmarker bleiben. Sammel-PDFs tragen die explizite
Abschnittshaltung pro Foto weiter, sodass `WorkbenchItem.CaseId` verschiedener
Haltungen getrennt bleibt; mehrdeutige Abschnitte und globale Befund-Fallbacks
über Haltungsgrenzen werden ausgelassen. Sichere Abschnittstexte werden einmal je
Haltung materialisiert und bewahren lokale Meter-, Befund- und Streckenschadendaten.
`TrainingPdfProtocolFindingParser` liest Befundzeilen und paart Start/Ende eines
Streckenschadens; `TrainingPdfProtocolMetadataParser` behält Dokument- und
Haltungsmetadaten.
Inspektionsdatum, vollständiger mehrzeiliger Befundtext und sichere Von-Bis-Meter
bleiben erhalten.
`SourceReferenceCode` und `SourceReferenceDescription` bewahren die Operateurangabe
und sind gemeinsam mit der strengen PDF-Prüfspur Pflicht für PDF-Gold.

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
Der aktuelle Audit `gold_stock_audit_20260802_205630_348.json` mit SHA-256
`bb7f01f6b3582029ad4393c7217e5c2bbbb4ed5770ab15c807a574972b4905ba`
prüft 1391 Einträge. Er überspringt 14 Drafts, verwirft 24 Kandidaten und enthält
1353 verwendbare Instanzen; sein Split ist 961 Train / 264 Validation / 128 Test.
Gebunden ist der `training_samples.json`-Snapshot mit SHA-256
`bfcb3362762dc552861feb0680f1267e086e8d7d3fb71d70e5806841b82daa83`.
Die älteren Audits bleiben historische Belege der vorigen Kandidaten.

`training/scripts/repair_gold_holding_ids.py` repariert fehlende `foto_*`-Haltungen
nur über genau einen bytegleichen SHA-256-Quelltreffer mit belastbarer Haltung im
Dateinamen. Dry-Run ist Standard. Der Ausführungsweg verlangt eine ruhige App/DB,
erstellt konsistente JSON-/SQLite-Sicherungen und aktualisiert Sample, Signatur,
Notiz, Teacher-Haltung und SQLite-`CaseId` gemeinsam. Kundenbilder bleiben
unverändert.

Der rohe Detect-Testanteil des an `detect_gold_ffbb8612fe50` gebundenen Audits
umfasste 83 Instanzen auf 79 Bildern. Die Haltung `77457-77453` überschnitt sich
mit einem Trainingsnegativ. Erst nach Ausschluss
der ganzen Haltung bleiben 81 Instanzen auf 77 Bildern aus 30 physischen Haltungen
als sicherer positiver Testbestand.

`detect_gold_holdout_provenance.py` bindet Kandidat, Gewichte, Dataset,
DETECT_ALL-Beleg, Klassenkarte, Migration, Basis-/Aktuell-Audit und aktuelle
Samples fail-closed. Bildhash, Sample-ID und physische Haltung samt Gegenrichtung
werden gegen den Kandidaten-Datensatz geprüft. `evaluate_detect_gold_holdout.py`
arbeitet mit `conf=0,25`, `imgsz=1280`, `IoU=0,5`, privater Gewichtskopie und
zuerst einem labelblinden SHA-Beleg. `detect_gold_holdout_scoring.py` ordnet
Mehrfachboxen mit maximaler Trefferzahl und danach maximalem Gesamt-IoU zu.
Technische Fehler zählen nie als negative Erkennung.

Der gültige GPU-Lauf vom 2026-08-02 hat Bericht-SHA-256
`9ce6aaad85317061953796085ff7daf921b554295f2bad21e904cc5dc78789f6`
und Vorhersagebeleg-SHA-256
`87002b0aa6cca5d6a5ec33ef05d5662ff80be2f71458ddaba3374916633aa450`.
Ergebnis: TP 17, FP 24, FN 64, Precision 41,5 %, Recall 21,0 %, F1 27,9 %.
`BCC_bogen` trifft 14/16, `BCA_anschluss` 3/17; elf weitere gemessene Klassen
haben keinen exakten Treffer. Ohne frische saubere Negativbilder bleibt der Status
`positive_holdout_only_not_release_qualified`; der Kandidat bleibt `not_deployed`.
Der vorherige Lauf mit Zeitstempel `20260802_120445_930796` ist wegen einer dabei
entdeckten falschen RGB/BGR-Übergabe aufgehoben und darf nicht bewertet werden.

`detect_gold_error_review.py` baut daraus ohne Bildkopien die eingefrorene,
diagnostische Queue `detect_gold_failure_a46a82535c82`. Sie umfasst 80 Fehler auf
67 Bildern: 56 verpasste Goldinstanzen, 8 geometrisch passende Vorhersagen mit
falscher Klasse und 16 geometrisch unzugeordnete Zusatzboxen. Queue-ID, Bericht,
labelblinder Ledger, Vorhersage-Receipt, Kandidatenmanifest, Gewicht, aktueller
Gold-Audit, Trainingssamples, Klassenkarte, Migration und VSA-Manifest sind
SHA-gebunden. Die Queue setzt Training, Export, Quellenmutation und Bildkopien
ausdruecklich auf `false`.

`DetectGoldErrorReviewStore` im lokalen
`detect_gold_error_review_server.py` zeigt die gebundenen Gold- und KI-Overlays.
Es speichert nur `confirmed_model_error`, `gold_suspect` oder
`exclude_uncertain` in einer getrennten Review-Datei unter `eval_review`.
Vor jedem Speichern wird die komplette Quelle erneut geprueft; Browser-Revision,
prozessweiter Lock, atomarer Austausch und Dateiversion verhindern stilles
Ueberschreiben. Der Pruefplatz besitzt keinen Schreibweg zu Gold, KB,
Trainingsdaten, Registry oder Modell. Eine daraus abgeleitete Sammelplanung darf
nur aggregierte Klassenbedarfe enthalten; nach modellbezogener Nutzung ist dieser
Holdout als unabhaengige Release-Abnahme verbraucht.

`publish_detect_gold_collection_plan.py` ist die getrennte, standardmaessig
schreibfreie Abschluss-CLI. Sie verlangt eine vollstaendige Review mit passender
Queue-ID, Manifest-/Kandidaten-SHA und Reviewer. `--execute` schreibt genau einen
atomaren, idempotenten `aggregate_only`-Plan. Dieser enthaelt nur Klassenzaehler
fuer neue Positivbeispiele, neue Negativ-/Verwechslungsfaelle und einen getrennten
Annotation-Audit; Bildpfade, Bildhashes, Sample-, Prediction-, Fall-IDs und
Kommentare bleiben ausgeschlossen. Eine bestaetigte falsche Klasse zaehlt sowohl
als Positivbedarf der Sollklasse als auch als konkrete Klassenverwechslung. Die
Review `detect_gold_failure_a46a82535c82` ist mit 80/80 Entscheidungen beendet:
75 Modellfehler, 0 Gold-Verdachtsfaelle und 5 Ausschluesse. Der gueltige Plan
`detect_gold_collection_874ec160e346` enthaelt 60 positive Fehlerhinweise,
15 Fehlalarm-Hinweise und 6 Verwechslungen in 4 Klassenpaaren. Der vorherige Plan
`detect_gold_collection_44a08fe9895e` ist wegen seiner fehlenden
Verwechslungsliste aufgehoben und darf nicht verwendet werden.

`yolo_wrapper._pil_rgb_to_ultralytics_bgr` wandelt PIL-RGB vor
Ultralytics-NumPy-Inferenz ausdrücklich in zusammenhängendes BGR. Der produktive
Detect-Pfad, Legacy-Classification und beide Holdout-Auswerter verwenden denselben
Helfer; ein Pixeltest schützt die Kanalreihenfolge.

`PersonalGoldAlbumWindow` zeigt diesen Bestand als Fotoalbum mit Hauptcode-Liste,
Kachelstapel und grosser Detailansicht. Hauptcode-Liste, Goldstand und
Ordnerhinweis zeigen Code plus Klartext. Es schreibt nichts. Neue Bilder werden über
`Gold-Eingang öffnen` in Klartext-Hauptcode-Ordner wie `BAB - Riss` gelegt und mit `Eingang laden` als
Prüfplatz-Stapel geöffnet. Der Ordner ist nur ein Hinweis: Erst die persönliche
Codierung mit BBox, SAM-Segmentierung und Akzeptieren erzeugt Goldstandard.
Alte Ordner nur mit Code bleiben lesbar und ihre Bilder werden nicht verschoben.
Fertig bearbeitete Eingangsdateien können manuell nach `_ERLEDIGT` verschoben
werden; dieser Ordner wird beim Laden übersprungen.

`Segmentierung abarbeiten` ist die zentrale Arbeitsliste für vorhandene persönliche
Samples ohne gültige SAM-Maske. `WorkbenchQueueService` prüft dabei auch die echten
Bildmaße und lädt nur tatsächlich lesbare Bilder. Eine gültige vorhandene Hand-Box
wird als `WorkbenchItem.ExistingBox` übernommen und im `TrainingStudioViewModel`
sofort erneut an SAM gegeben; parallel erscheint der Codevergleich. Fehlt eine
gültige Box, zeigt die Foto-KI nur eine Orientierung und der Mensch zeichnet die Box.
Ohne gültige sichtbare Maske kann diese Spezialliste nicht als erledigt fortschalten.
Gespeichert wird weiterhin über die bestehende Sample-ID, nicht als Dublette oder
zweite Ordnerkopie. Laufende Box-Ergebnisse sind an das ursprüngliche Bild gebunden
und werden nach einem Bildwechsel verworfen.
PDF-Entwürfe mit exakt gleicher unveränderlicher PDF-Referenz, Bilddatei, Haltung und
Code werden aus dieser Arbeitsliste ausgeblendet, sobald dafür bereits ein
geometrisch gültiges `Approved`-Sample existiert. Die historischen Entwürfe werden
dabei nicht gelöscht.
Die Thumbnail-Auswahl laeuft ueber `SelectQueueItemAsync`; die Reparaturliste bleibt
dabei strikt und kann vor dem persoenlichen Akzeptieren nicht uebersprungen werden.

`Goldpruefung (90)` ist die fortsetzbare Qualitaetsrunde fuer je 15 freigegebene
Goldbilder der Hauptcodes `BAB`, `BAF`, `BAI`, `BAJ`, `BBC` und `BBF`.
`GoldQualityReviewQueueUseCase` liegt unter `Application/UseCases/GoldQualityReview`;
`GoldQualityReviewSnapshotProvider` und `GoldQualityReviewSessionFileStore` liegen
unter `Infrastructure/Ai/Training/GoldQualityReview`. Die Auswahl stammt nur aus den
einzeln freigegebenen Sample-IDs des Exportregisters und wird durch einen strikten
Live-Inventarlauf von Eval-Bild-Hashes und Eval-Haltungen getrennt. Das
Sitzungsmanifest unter `<KnowledgeRoot>\training\gold_quality_reviews` bindet
Register, Schutzstand, Bildbytes und Ausgangsbestaetigung. Ein separater
unveraenderlicher Abschlussbeleg je Sample weist die persoenliche Wiederbestaetigung
nach; ein nur extern neu gespeichertes Sample gilt nicht als erledigt. Vor dem
Schreiben prueft der Workbench-Save den gebundenen Bildhash und Sample-Zeitstand und
verwendet fuer Schutzpruefung und Persistenz denselben Bild-Snapshot. Korrigierte
Uhrlage und Schadensstufe werden in `TrainingSample.CodeMeta` erhalten.
`WorkbenchItem.ExistingSegmentation` zeigt die gespeicherte Maske ohne neuen
SAM-Lauf; nur eine neue Hand-Box ersetzt sie. Der Save-Weg verwendet die bestehende
Sample-ID und erzeugt keine Dublette.
`AnnotationWorkbenchService.SampleMapping.cs` kapselt dabei die Uebernahme der
Bestandsmetadaten. Die davon unabhaengige reine Modellvorschau ist in
`TrainingStudioViewModel.PreviewDetection.cs` getrennt und schreibt keine
Gold-Daten.

`TrainingStudioBoxAnalysisUseCase` (`Application/UseCases/TrainingStudioSegmentation`)
orchestriert SAM und Codevorschlag parallel und liefert auch sichere Teilergebnisse.
Sein `ValidateSegmentation`-Ergebnis erhält Fehlerart und Klartextgrund; dadurch
meldet die UI bei einer sichtbaren Maske ausserhalb der Box den echten Anteil statt
pauschal eine fehlende Maske. Der Overlay-Renderer zeigt noch nicht goldfähige
Masken orange, gültige Masken grün. Es findet kein stilles Zuschneiden auf die Box
statt.
`TrainingStudioViewModel.RepairQueue.cs` koordiniert nur Laden, Vorbereiten und
Fortschalten der Spezialliste. `TrainingImageFileProbe` prueft Bildkopf und volle
Dekodierbarkeit. Der Save-Vertrag meldet mit `WorkbenchSaveResult.GoldApproved`
explizit, ob das persoenliche Gold-Gate wirklich bestanden wurde; ein lediglich
gespeicherter Draft bleibt offen. Das gilt auch fuer `PhotoAnnotationUseCase`.

**Mehrere Goldobjekte auf demselben Bild (2026-08-03):** Nach einem Gold-Save im
Normal-Modus öffnet `OpenImageCompletionChoice` die ausdrückliche Bildentscheidung
„Weiteres Ereignis auf diesem Bild" / „Bild fertig"
(`TrainingStudioViewModel.MultiObject.cs`, Schaltflächen in
`TrainingStudioWindow.xaml`). Voraussetzung: kein offener PDF-Operateurbefund
desselben Fotos mehr in der Queue (`CanOfferMultipleObjects` prüft
`HasPendingPdfReferenceForSameImage` — PDF-Befunde eines Bildes werden also zuerst
vollständig abgearbeitet) und ein neues Objekt der laufenden Sitzung.
`TrainingStudioAdditionalObjectPolicy.CreateManualObject`
(`Application/UseCases/TrainingStudioMultiObject`) erzeugt den Arbeitskontext für
das Zusatzobjekt: gleiche Haltung und derselbe Meter, `MeterEnd = MeterStart`,
`IsStreckenschaden = false` — ein Zusatzereignis ist immer ein Punktbefund und erbt
nie den Bereich eines Streckenschadens. PDF-/Bestandsidentität wird bewusst nicht
weitergegeben: eigene Sample-ID, gilt als `ManualCoding`; der geschützte
`ExpectedImageSha256` wird weitergereicht. Jedes bestätigte Ereignis wird ein
eigenes Goldsample; die Dedup-Signatur mit Box-Geometrie
(`TrainingSample.BuildCanonicalSignature`, „…|b:x,y,w,h") erkennt dieselbe Box
weiterhin als Dublette, eine andere Box bleibt ein erlaubtes zweites Objekt.
Absicherungen: `_completedQueueItemIndices` verhindert Doppelzählung und
Doppelspeicherung fertiger Bilder; `BindSavedDraftToCurrentItem` bindet Sample-ID,
Code, Box und Zeitstand (`ExpectedConfirmedAtUtc`) an das Queue-Element;
`BindOpenPdfReferencesToStoredImage` bindet offene PDF-Referenzen desselben Bildes
an denselben gespeicherten Bildhash. Reparatur- und Goldprüfungs-Queues bleiben
Einzelfall-Queues ohne diese Bildentscheidung.

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
| `EvalReviewedDamageDataset` | bindet die getrennte menschliche V1-Schadensreview nur bei passendem Kandidaten-Hash, Vollständigkeit und null Konflikten ein |
| `EvalReviewedDamageScorer` | misst Schadenspräsenz, Fehlalarme, Code, Stufe und Ereignisse; Nicht-Schadenscodes auf ausgeschlossenen Bildern sind kein Schadens-Fehlalarm |
| `EvalSetManifestHasher` | Manifest-Hashing des Eval-Sets |

Die 32 BA-/BB-Vorgabebilder des realen 120er-Sets sind menschlich geprüft. Der
übrige Bestand ist noch nicht vollständig mit Severity/EventId nachgepflegt.
`EvalSetBenchmark --review-file` misst nur das Ollama-Bildmodell ohne
YOLO-/DINO-/SAM-Hinweise und ohne QualityGate. AP 0.4 ist deshalb nicht
abgeschlossen; keine Modellfreigabe allein aus dieser Teilmessung.

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
| `PlayerVideoAspectResolver` | sichere native LibVLC-Grössenabfrage samt Sample-Aspect-Ratio und Ausrichtung; hält falsche oder noch nicht verfügbare Metadaten aus dem Overlay-Ablauf |

**VSA-Codierfenster:** `VsaCodeTreeSelectionCatalog` verbindet zwei getrennte
Wahrheiten: Das VSA-KEK-2020-Manifest gibt die tatsächlich auswählbaren Endcodes und
deren exakten Klartext vor; `VsaCodeTree` liefert für den derzeit angebotenen
Kanal-Picker die Navigation sowie die gegen WinCan VSA-2019 abgeglichenen
Quantifizierungsregeln. `CodeCatalogSelectionCatalog` übernimmt deshalb keine
WinCan-Zwischenüberschriften wie `Status` oder `Vertikale Richtung` als Codebezeichnung.
`VsaCodeExplorerQuantPanelPresenter` und
`VsaCodeExplorerQuantPanelRenderer` zeigen Q1 und Q2 jeweils mit Bezeichnung,
Einheit (`mm`, `%`, `°`, `Stk.`) und Bereich. `VsaCodeEntryValidator` prüft genau
dieselbe Regel. Nicht auswählbare Endcodes werden beim Anzeigen, Wiederöffnen und
Speichern gesperrt; abweichende Endcodes wie `BAG → BAGA` löst
`VsaCodePathResolver` in beide Richtungen auf.

**Streckenschaden-Endmeter:** Der manuelle Schliessweg in
`PlayerWindow.Coding.Events.Actions.cs` benutzt
`ResolveCodingMeterForFrame(CurrentSeconds)`. Damit gilt dieselbe Priorität wie beim
Anlegen eines Befunds: frische OSD-Metrierung, dann Timeline-Schaetzung, zuletzt der
Sessionwert.

**Manuelle SAM-Vorschau im Player:** Vor der Zeicheneingabe übernimmt
`PlayerMediaRuntime.TryGetVideoAspect` das native Video-Seitenverhältnis. Nach einer
gezogenen Box segmentiert `LiveDetectionMarkSegmentationController` den aktuellen
Frame. `LiveDetectionMarkSamMaskRenderWorkflow` zeigt immer die echte SAM-Maske für
drei Sekunden vor dem VSA-Codierfenster; ein Bogen-Signal ersetzt sie nie durch ein
Oval. Die Maske bleibt als `OverlayGeometry.SamMask` am manuellen Ereignis,
`CodingEventColumnTransfer` klont sie tief und `CodingEventToSampleMapper` übernimmt
sie nach strenger RLE-Prüfung in das Trainingssample, ohne dafür einen KI-Kontext
vorzutäuschen. Vor einem neuen Segmentierungsversuch wird eine alte Maske entfernt.

**SAM-Markierung im Foto-Assistenten:** `PhotoAnnotationUseCase` unter
`Application/UseCases/PhotoAnnotations` liest das unveränderte Originalfoto vor und
nach SAM, vergleicht den SHA-256 und bindet danach eine private Byte-Momentaufnahme
fest an die normierte Hand-Box und die gültige, nicht degradierte SAM-RLE.
`PhotoMeasurementWindow.Sam` zeigt diese echte Maske auf einer eigenen Canvas-Ebene;
sie wird deshalb nicht in das erzeugte Overlay-Foto eingebrannt. Erst die ausdrückliche
Bestätigung im VSA-Fenster speichert Original, Box, Maske und finalen Code über
`IAnnotationWorkbenchService`; Eval-Schutz, Goldkopie, Dublettschutz, KB und Teacher
bleiben dort zentral. Eval-Hashprüfung und `StoreBytesAsync` arbeiten mit exakt
derselben Momentaufnahme; der Originalpfad wird beim Speichern nicht erneut gelesen.
`ProtocolEntry.OriginalFotoPaths` hält pro Fotoslot die unveränderte Quelle getrennt
vom möglichen Overlay in `FotoPaths`. Der VSA-Foto-Assistent öffnet nur diesen
Originalpfad; Altprotokolle füllen fehlende Originalslots aus ihren bisherigen
Fotopfaden, und eine neue Videoaufnahme setzt beide Listen auf denselben neuen Frame.
`PhotoAnnotationBatchSaveUseCase` validiert bei mehreren Masken zuerst das komplette
Paket aus eingefrorenem Eintrag und Drafts. Nach einem spaeten Teilerfolg wird genau
dieser Eintrag mit den schon gespeicherten Sample-IDs abgeschlossen, statt eine
Umcodierung oder einen Abbruch zuzulassen.
Der finale Eintrag gibt `IsStreckenschaden` an das TrainingSample weiter. Die
Foto-Maske wird nicht zusätzlich an das Coding-Ereignis gehängt. Beim automatischen
Streckenschaden-Ende entstehen daher weder eine zweite Maske, ein zweites Foto noch
eine geerbte Trainings-Sprungmarkierung.
Ist der Streckenschaden beim Foto-Save noch offen, steht das Goldfoto fuer den
Startpunkt (`MeterEnd = MeterStart`). Das spaetere Ende aendert dieses Bildsample
nicht.

`ProtocolEntry.Training` haelt die separat gespeicherten
`PhotoAnnotationSampleIds`. Sein Schalter `SkipAutomaticPersistence` verhindert im
`CodingTrainingSamplePersistenceCoordinator` die zweite Speicherung beim einzelnen
Bestaetigen und beim Session-Abschluss. Protokoll- und Coding-Kopierwege klonen diese
Metadaten samt ID-Liste tief.

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
