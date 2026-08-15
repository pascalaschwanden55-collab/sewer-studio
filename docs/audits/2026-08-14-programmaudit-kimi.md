# Programmaudit SewerStudio — 2026-08-14 (Kimi, Folgeaudit)

**Vorgänger:** `docs/audits/2026-08-10-programmaudit-kimi.md` (4 HOCH, 13 MITTEL, ~17 NIEDRIG).
Dieses Audit (a) verifiziert den Umsetzungsstand aller Altbefunde am heutigen Code und
(b) prüft sieben Dimensionen neu: Architektur, Code-Qualität C#, Sicherheit,
Python-Sidecar/QGIS, Tests & CI, Repo-Hygiene & Doku-Drift, Build-/Funktionsstatus.

**Umfang:** ~4.560 C#-Dateien Produktivcode (4 src/-Projekte + 43 tools/-Projekte),
~530k LOC C# inkl. Tests, 76 XAML-Dateien, Sidecar ~5.800 LOC Python (77 Dateien inkl.
QGIS-Brücke), ~9.530 Testmethoden (≈11.500 ausgeführte Fälle inkl. InlineData).

**Methode:** 7 parallele Explore-Agenten mit Belegpflicht (Datei:Zeile + gelesener Code +
Schadensmechanismus) und Pflicht-Gegenprobe je Befund; zurückgestufte Befunde sind
explizit aufgeführt. Zusätzlich eigner Verifikations-Build durch den Hauptlauf.

**Build-/Funktionsstatus (selbst gefahren):**
`dotnet build AuswertungPro.Dev.slnf -c Release --no-restore` → **erfolgreich,
0 Warnungen, 0 Fehler** (18,8 s). CI: zwei neueste Runs grün, nachdem drei Blocker am
2026-08-14 behoben wurden (FfmpegProbe, pyproject build-backend, getrackte
yolo26m.names.json-Fixture). Kein Volltestlauf in diesem Audit (Suite war laut CI grün).

---

## 0. Bilanz auf einen Blick

- **Neue HOCH-Befunde: 2** — Sidecar-Modellslot-Verwechslung (S-H1), 40-MB-Modellgewicht
  im Git-Index (R-H1).
- **Neue MITTEL-Befunde: 12** — Service-Locator-Muster wächst ungeschützt (A-H1 wird als
  HOCH architektonisch geführt, siehe unten), Doppelgleisigkeit DI/statische Fassaden,
  Sidecar-Lease-Muster, 422-Body-Spiegelung, veraltete HTTP-Abhängigkeiten mit CVEs,
  Pre-Push-Lücke, Release-Gate-Vakuum, fehlende Branch-Protection u.a.
- **Altbefunde:** Alle 4 HOCH + M5 des Vorgängers **verifiziert behoben** (echte
  Verhaltenstests vorhanden). Von den übrigen MITTEL stehen **M1-M4, M7-M14 weiter offen
  und unverändert im Code** — Welle 2-4 des alten Fahrplans ist grösstenteils nicht
  umgesetzt. NIEDRIG-Liste bis auf Junction-Tests unangetastet.
- **Positiv:** Schichtung formal sauber, ~100 Architekturtests wirksam, Sicherheitsbasis
  (Token-Middleware, XML-Härtung, Import-Pfad-Guards, Secrets-Hygiene) intakt,
  Testkultur ehrlich (keine immer-wahren Asserts, Gates mit Grund).

**Legende:** S- = Sidecar, A- = Architektur, Q- = Code-Qualität, SEC- = Sicherheit,
T- = Tests/CI, R- = Repo-Hygiene. Altbefunde ohne Präfix (M1-M14) beziehen sich auf den
Vorgänger.

---

## 1. Verifikation der Altbefunde (Stand 2026-08-14)

### Behoben (am Code verifiziert, echte Verhaltenstests)

| Befund | Beleg |
|---|---|
| H1 TeacherAnnotationFileStore | `TeacherAnnotationFileStore.cs:126-127,136-157` fail-closed + `.corrupt`-Beweissicherung; Tests `TeacherAnnotationStoreTests.cs:85-124` (kaputtes JSON, null-Dokument, Sperre, Byte-Vergleich) |
| H2 ProtocolTrainingFileStore | `ProtocolTrainingFileStore.cs:98-123`; Tests `ProtocolTrainingFileStoreTests.cs:46-85` |
| H3 AiOptimizationSessionFileStore | `AiOptimizationSessionFileStore.cs:90-91,98-108`; Tests `AiOptimizationSessionStoreTests.cs:58-111` |
| H4 SelfTrainingHarness-Restore | `tools/SelfTrainingHarness/Program.cs:27-32,52-57,91-96,111` App-Sperren; `GuardedStoreSnapshot.cs:60-120` SHA-256-Doppelprüfung + atomares `File.Replace` (:139); 4 Verhaltenstests. Restrisiko: minimales TOCTOU-Fenster, bewusst per Doppelprüfung gedämpft |
| M5 Sidecar-Token Loopback | Zentrale `SidecarEndpointPolicy.cs:9-21`; `AiStartupService.cs:195` liefert bei Nicht-Loopback `null`; Verhaltenstest `AiStartupServiceTests.cs:140-165` |
| NIEDRIG Junction-Schutztests | Commit `b22e68ba2` (2026-08-14), beidseitige Schutzwirkung |

### Offen (unverändert im Code, aus Vorgänger übernommen)

- **M1** Zwei Store-Instanzen/eine Datei: `TeacherAnnotationStore.cs:11` (statisch,
  produktiv in `LiveDetectionTrainingAnnotationWriter.cs:52/59`) vs. DI
  (`ServiceProvider.cs:407`, `TrainingCenterWindow.xaml.cs:113/149`); analog
  `AiOptimizationSessionStore.cs:11-12`. Nebenbefund: Kommentar `ServiceProvider.cs:453`
  ist falsch — `Use()` wirft inzwischen `NotSupportedException`.
- **M2** `SchachtMassnahmenKatalogStore.cs:48-52` leerer catch → Defaults überschreiben
  Benutzerliste beim Speichern aus dem Editor.
- **M3** `AppSettings.cs:375-379` generischer catch → Defaults; Start-Speicherung
  `ServiceProvider.cs:734-738` schreibt sie über die echte `settings.json`, keine
  sichtbare Warnung (nur JsonException-Zweig hat Quarantäne).
- **M4** Ungepinnte Fach-Enums: `TrainingSampleModels.cs:11` (Draft=4),
  `ProtocolModels.cs:7-8`; keine Pinning-Tests.
- **M7** DINO/SAM-Gewichte ohne Hashbindung (`sam_wrapper.py:25-27`,
  `dino_wrapper.py:32-36,62-73`); Minimal-Fix (SHA loggen + in /health) nicht umgesetzt.
- **M8** `BendSuggestionScanService.cs:56-76` schreibt Platten-JPEGs statt
  In-Memory-`VideoFrameStream`.
- **M9** Frame-Base64 wird je Request neu serialisiert (`MultiModelAnalysisService.cs:268`
  → `VisionPipelineClient.cs:321,378`).
- **M10** Verwaister `ProtocolEntryEditorDialog` (kein Aufrufer, abweichende
  Accepted-Regel `xaml.cs:666-669`).
- **M11** `training/scripts/import_gold_labels.py:324-326` nicht-atomar, ohne
  App-läuft-Check.
- **M12** `tools/PdfProtocolIngest/enrich_vsa_dataset.py:80-83` fail-open bei falschem
  Eval-Pfad, Schreiben ist Default.
- **M13** `tools/PersonalGoldMigration/Program.cs:6` `dryRun = false` als Default.
- **M14** `VsaKekCatalogBuilderTests.cs:12` hartes Skip — einziger E2E-Test des
  Katalogbaus dauerhaft tot.
- **NIEDRIG-Stichproben offen:** 300-ms-Sperrfenster (`TrainingSampleFileStoreTests.cs:43-49`),
  Mirror-Test-Races (`KnowledgeRealtimeMirrorServiceTests.cs:245,362-363,389-401`),
  `tools/video_ai` Totlast, Migrations-Save im Lese-try (`TrainingSampleFileStore.cs:337-348`),
  `DirectoryMirror.RemoveOrphans` ohne CancellationToken (:201).

---

## 2. Neue Befunde — HOCH

### S-H1 — Slot `YOLO_TEST`: zwei Wrapper teilen einen GPU-Slot mit getrennten Locks → falsches Modell kann inferieren
- **Ort:** `sidecar/sidecar/models/bcc_test_wrapper.py:50` und
  `lernstufe_wrapper.py:45` — je eigenes Modul-`_predict_lock`, je eigenes
  Ownership-Flag (`_loaded_candidate_sha256` vs. `_geladene_sha256`), ohne
  gegenseitige Invalidierung. `gpu_manager.ensure_loaded` Fast-Path
  (`gpu_manager.py:258-261`) prüft keine Modell-Identität.
- **Schaden (Gegenprobe am Code verifiziert):**
  - bcc nach lernstufe: `_discard_stale_candidate` (:337-339) sieht eigenen Hash
    unverändert → kein Unload → Inferenz läuft auf dem Lernstufen-Klassifikator →
    `result.boxes is None` → **stillschweigend leeres Ergebnis mit `available=True`**
    (falsches Negativ im BCC-Pilot).
  - lernstufe nach bcc: Predict auf dem BCC-Detektor → Klassenname "rohranfang" fehlt →
    **StopIteration → 500** (`lernstufe_wrapper.py:245`).
- **Einordnung:** Aktuell kein C#-Aufrufer von `/classify/lernstufe*` im Repo
  (PipeEndpointScan hängt in der Luft), aber beide Routen sind registriert und per HTTP
  erreichbar; wird der Lernstufen-Pfad produktiv, wird der Defekt sofort aktiv.
- **Fix:** `SlotState` um `content_id` erweitern; `ensure_loaded(..., content_id=...)`
  entlädt bei Mismatch selbst und lädt neu; ein gemeinsames Lock für beide Wrapper.
  Regressionstest: bcc→lernstufe→bcc-Wechsel.

### R-H1 — 40-MB-Modellgewicht `sidecar/yolo11m.pt` im Git-Index
- **Ort:** grösster Blob im Index (40.684.120 Bytes, getrackt seit `c458620c4`).
  `.gitignore:88` ignoriert `**/*.pt` — wirkt aber nicht rückwirkend auf Getracktes.
- **Schaden:** Jeder Clone/jede CI zieht dauerhaft 40 MB entgegen der eigenen Repo-Policy
  (Kommentar `.gitignore:81-84`). Fachlich redundant: `yolo_wrapper.py:161` lädt das
  COCO-Fallback zur Laufzeit selbst herunter.
- **Fix:** `git rm --cached sidecar/yolo11m.pt` + committen (keine History-Rewrite nötig);
  lokale Datei im Ordner belassen.

---

## 3. Neue Befunde — Architektur (A-)

### A-H1 — Service-Locator als Ctor-Parameter durch die gesamte VM-Hierarchie
- **Beleg:** `ShellViewModel.cs:111` nimmt den konkreten `ServiceProvider` (958 Zeilen +
  4 Partials, 171 öffentliche Properties) und reicht ihn an **15 weitere ViewModels**
  weiter (u.a. `OverviewPageViewModel.cs:88`, `DataPageViewModel.cs:176`,
  `ExportPageViewModel.cs:66`, `SanierungsMatrixPageViewModel.cs:117`).
- **Schaden:** VMs deklarieren ihre Abhängigkeiten nicht; Tests müssen den
  958-Zeilen-Container bauen; Zyklen unsichtbar. Der Guard
  `UiArchitectureGuardTests.cs:9-38` verbietet `App.Services` als Locator — derselbe
  Mechanismus läuft aber ungeschützt als Ctor-Parameter weiter.
- **Empfehlung (minimal, schrittweise):** pro VM die tatsächlich genutzten `_sp.X`-
  Zugriffe als Interfaces in den Ctor heben; Guard-Test "kein `ServiceProvider` in neuen
  VM-Ctor-Signaturen" mit Allowlist des Bestands (UiAiFreeze-Muster).

### A-M2 — Doppelgleisigkeit DI ↔ statische Fassaden, ungeschützt
- **Beleg:** 50 `public static … Current`-Fassaden (46 Infrastructure, 4 UI), **224**
  `.Current`-Zugriffe aus UI; Convenience-Ctor
  `TrainingCenterViewModel.Dependencies.cs:69-76` zieht 7 `.Current`-Defaults;
  `MultiModelAnalysisService.cs:88` injiziert `PipelineTraceWriter.Current`.
- **Empfehlung:** Architekturtest "keine neuen statischen `Current`-Fassaden" mit
  Allowlist; Neuentwicklung nur per Interface.

### A-M3 — UI bindet konkrete Infrastructure-Typen statt Application-Verträge
- **Beleg:** 216 UI-Dateien mit `using …Infrastructure.*` (371 Usings);
  `TrainingStudioWindowDependencyFactory.cs:66-185` instanziiert ~15 konkrete
  Infrastructure-Klassen. Application hält 194 Interfaces bereit.
- **Empfehlung:** Regel "neue UI-Dateien nur Application-Interfaces" per
  Allowlist-Architekturtest. Kein Umbau des Bestands.

### A-M4 — UiAiFreeze schützt nur `UI/Ai`
- **Beleg:** `UiAiFreezeArchitectureTests.cs:18` friert nur `UI/Ai` ein (603 Dateien,
  wirksam: +0,3 % LOC seit 2026-07-26). Gleichartige Orchestrierung in `UI/Services`
  (z.B. `TrainingStudioWindowDependencyFactory`) ist ungeschützt.
- **Empfehlung:** Freeze auf Benennungsmuster (`*Workflow`, `*RequestFactory`,
  `*Controller`) in `UI/Services`, `UI/Player`, `UI/DataPage` ausweiten, Bestand per
  Allowlist.

### A-N (kurz)
- `PlayerWindow`: 80 Code-Behind-Partials, 4.903 Zeilen — durch ~40 Architekturtests
  stabilisiert, daher NIEDRIG; Controller-Muster fortsetzen.
- Ctor-Umfänge: `AnnotationWorkbenchService` 19 Parameter (10 `Func<>`-Delegaten),
  `TrainingCenterViewModel` 19 — erst bei nächster Erweiterung Parameter-Objekte
  (Muster `TrainingYoloExportDependencies` existiert).

---

## 4. Neue Befunde — Code-Qualität C# (Q-)

Kontext: 522 `catch (Exception…)`-Stellen (Stichprobe ~40: fast alle mit
Log/Degradation), 11 catch-ohne-Variable, 8 leere catch (alle Dispose/Stop),
22 `async void` (20 Event-Handler), 7 blockierende `.GetAwaiter().GetResult()`
(davon 1 relevant). Globale Fangnetze in `App.xaml.cs:153-169` vorhanden.

### Q-B1 (MITTEL) — `VsaClassificationTable.LoadFromFile` schluckt Fehler → stille leere Klassifizierung
- **Ort:** `Vsa/Classification/VsaClassificationTable.cs:51-63` catch-all ohne Log → leere
  Tabelle. Aufrufer `VsaEvaluationService.cs:493-501` will `VSA_TABLE_PARSE_FAILED`
  liefern — **unerreichbarer Code**, Ergebnis ist `Success` mit 0 Regeln.
- **Schaden:** Zustandsklassifizierung läuft mit leerem Regelwerk, ohne Fehlermeldung.
- **Fix:** `LoadFromFile` wirft bei Parse-/Lesefehler (fehlende Datei separat); der
  vorhandene Fail-Pfad greift dann.

### Q-B2 (MITTEL) — `RouterDatasetBuilder` verwirft still alle Pfade ausserhalb C:\/D:\
- **Ort:** `Application/Ai/Evaluation/RouterDatasetBuilder.cs:167-176` — `StartsWith`
  auf `C:\`/`D:\`; UNC-Pfade, andere Laufwerke, relative Pfade fallen ungezählt weg.
- **Fix:** `Path.IsPathFullyQualified` + verworfene Zeilen zählen/loggen.

### Q-NIEDRIG (kurz)
- **Q-B3** `SystemMonitorService.cs:89` Ctor startet synchron Prozess
  (`FindNvidiaSmi` → `.GetAwaiter().GetResult()`, :831-841) im UI-Startpfad — bis 3 s
  Startblockade im Edge-Case. Fix: in den vorhandenen Hintergrund-Init (:103) verschieben.
- **Q-B4** Einziger async-void-Nicht-Event-Handler:
  `VsaCodeExplorerWindow.xaml.cs:715` `ApplyAndClose()` — try/catch oder
  SafeFireAndForget-Muster.
- **Q-B5** Sync-`CompleteSession()` mit Fire-and-Forget-Persistenz
  (`CodingSessionService.cs:175`, Interface-Default `ICodingSessionService.cs:37-40`) —
  aktuell ohne Aufrufer, aber Falle für künftige Nutzung. Fix: entfernen/`[Obsolete]`.
- **Q-B6** Hartcodierte Entwicklermaschinen-Defaults: `AppSettings.cs:21` (`D:\QGIS_…`),
  `:149` (`C:\KI_BRAIN\eval_set`), `:291` (`c:\Sewer-Studio_KI_4.5\basemap_tiles` →
  Offline-Basemap auf frischen Rechnern still tot). Fix: Defaults relativ/leer + Hinweis.
- **Q-B7** `DateTime.Now` in Persistenz-/Exportpfaden (`XtfRevisionWriter.cs:42`,
  `ImportSourceHistoryService.cs:24`, `ProjectRecoveryService.cs:131`,
  `MediaConflictCenterService.cs:451`) — auf UtcNow/Invariant umstellen.

---

## 5. Neue Befunde — Sicherheit (SEC-)

### SEC-S1 (MITTEL) — Sidecar-Lock pinnt HTTP-Stack mit bekannten CVEs
- **Ort:** `sidecar/requirements-lock.txt:74` `requests==2.28.1` (CVE-2023-32681),
  `:96` `urllib3==1.26.13` (CVE-2024-37891, CVE-2023-45803), `:22`
  `certifi==2022.12.7` (3,5 Jahre altes CA-Bundle).
- **Relevanz:** Modell-Downloads (huggingface-hub/ultralytics) nutzen requests/urllib3
  zur Laufzeit; Ausnutzung braucht Proxy/Redirect-Szenario — auf Einzelplatz klein,
  aber die drei Pakete sind eine Altinsel im sonst aktuellen Lock (fastapi 0.136.3,
  pydantic 2.13.4, uvicorn 0.48.0).
- **Fix:** `requests>=2.32.x`, `urllib3>=2.x`, certifi aktuell; Lock neu einfrieren +
  GPU-Smoke-Test.

### SEC-NIEDRIG (kurz)
- **SEC-S2** Drei ffmpeg-Aufrufe mit String-Konkatenation statt `ArgumentList`
  (`VideoFrameExtractionService.cs:32-38`, `VideoClipExtractionService.cs:56-65`,
  `VideoFrameSequenceExtractor.cs:47`). Kein aktiver Vektor (UseShellExecute=false,
  NTFS-Quoting), Konsistenz-Fix zum Referenzmuster `VideoFrameStream.cs:65-76`.
- **SEC-S3** Drei `XDocument.Load(path)` ohne projekteigenen Safe-Loader auf
  XTF-Fremddaten (`XtfKanalschadenElementReader.cs:20`,
  `XtfStammdatenElementReader.cs:17`, `XtfRevisionWriter.cs:41`). .NET-Defaults
  verhindern XXE wirksam; Fix: auf `SafeXmlLoader.Load` umstellen (explizit/einheitlich).
- **SEC-S4** Firebird-Default-Credential `SYSDBA`/`masterkey`
  (`IbakFdbConnectionOptions.cs:86-87`) — Hersteller-Default, Env-Override vorhanden;
  geringe Tragweite, dokumentiert halten.
- **SEC-S5** `UglyToad.PdfPig 1.7.0-custom-5` (Eigenbuild) — Fork entfällt aus dem
  CVE-Tracking; Fork-Basis/Diff dokumentieren und Upstream-Security-Releases manuell
  nachziehen.

### Sicherheit — geprüft und in Ordnung (Auswahl)
- Keine Secrets/Keys/`.env` im Repo; Sidecar-/LiveControl-Tokens in `%LOCALAPPDATA%`,
  in `settings.json` DPAPI-geschützt (inkl. Klartext-Abwehr-Test).
- Token-Pflicht auf **allen** Sidecar-Routen inkl. `/docs` (Middleware `main.py:191-224`,
  hmac-Konstantzeit), Loopback-Validator fail-closed, Trusted-Hosts gegen DNS-Rebinding.
- LiveControl: Loopback + `CryptographicOperations.FixedTimeEquals`
  (`LiveControlServer.cs:259-262`). QGIS-Bridge: Loopback, nur GET/HEAD, lesend.
- ZipSlip-Containment im Knowledge-Restore gegengeprüft (`KnowledgeBackupPathMapper.cs:67-78`).
- Kein Newtonsoft/BinaryFormatter/XmlSerializer auf Fremddaten; XML-Importe über
  `ISafeXmlDocumentLoader`.
- NuGet-CVE-Lage sauber: SQLite-Kette löst auf SQLite 3.50.4 (CVE-2025-6965 nicht
  betroffen), LibVLC 3.0.23 aktuell; übrige Pakete ohne bekannte verwundbare Versionen.

---

## 6. Neue Befunde — Python-Sidecar (S-, Fortsetzung)

### S-M1 (MITTEL) — `lernstufe_wrapper.einordnen` hält das Lease-Muster nicht
- **Ort:** `lernstufe_wrapper.py:235-246` — `ensure_loaded` + `zustand.model` laufen
  **vor** `acquire_busy`; der None-Check aus bcc (`bcc_test_wrapper.py:440-443`) fehlt.
  Paralleler OOM-Evict → `AttributeError` → 500 statt 503; Watchdog sieht den Ladevorgang
  nicht.
- **Fix:** bcc-Muster kopieren: `busy_slot` um `ensure_loaded` legen + None-Check.

### S-M2 (MITTEL) — `LernstufeError` wird zu generischem 500
- **Ort:** `routes/yolo.py:236` ohne try/except → zentraler Handler `main.py:139`.
  Erwartbarer Fachfehler ("keine Freigabe mit diesem Hash") ununterscheidbar von Defekt;
  inkonsistent zu bcc (kuratiertes `available=False`, `yolo.py:86-88`).
- **Fix:** In der Route fangen → 400/404 mit kuratierter Meldung.

### S-M3 (MITTEL) — Kein Gesamt-Body-Limit; 422-Antwort spiegelt kompletten Request-Body
- **Ort:** `TrainingExportRequest` erlaubt 500×25 MB (`segmentation.py:175`,
  `config.py:52`) → bis ~17 GB JSON im Speicher vor den 413-Checks
  (`training.py:222-229`). Experimentell bestätigt (FastAPI 0.136.3/pydantic 2.13.4):
  422-Antwort enthält das gesamte Modell als `input` (100 KB Payload → 134 KB Antwort).
- **Fix:** (a) `model_validator` mit Summen-Deckel (z.B. 512 MB) oder C#-Chunking;
  (b) eigener `RequestValidationError`-Handler ohne `input`/`ctx`.

### S-M4 (MITTEL) — Re-Hashing ganzer Gewichtsdateien pro Request
- **Ort:** `bcc_test_wrapper.py:163` hasht bei jedem Request alle Kandidaten;
  `lernstufe_wrapper.py:118` bei jedem `waehlen()`. Kein mtime/size-Cache, obwohl
  `detector_qualification._sha256_cached` (:305-328) das Muster fertig hat.
- **Fix:** Cache-Muster übertragen.

### S-NIEDRIG (kurz)
- **S-N1** HTTP-Testlücken: `/classify/lernstufen`, `/classify/lernstufe` ganz ohne Test;
  kein Test spielt den bcc↔lernstufe-Wechsel (hätte S-H1 gefangen). Fix: TestClient-Tests.
- **S-N2** `GET /warmup` mit Nebenwirkungen (lädt Modelle) — nur POST anbieten.
- **S-N3** IPv6-Falle: Host-Validator erlaubt `::1`, `trusted_hosts`-Default nicht
  (`config.py:16,37-40`) → 403 bei `SEWER_SIDECAR_HOST=::1`. Fix: `::1` in Default.
- **S-N4** `warmup.py:100-101` nutzt private SAM-APIs (`_resolve_device`/`_load_sam_on`)
  — öffentliche `warm()`-Funktion schaffen.
- **S-N5** QGIS-Brücke: `_write_layer_file`-Fallback ausserhalb des try
  (`sewerstudio_bridge.py:444-445`) → unbehandelter `OSError` im QTimer-Poll.
- **S-N6** Validierung: `BoundingBox` ohne `allow_inf_nan=False` (`detection.py:141-147`),
  `text_prompt` ohne Längenlimit, Client-`label` ungefiltert in Logs (Log-Injection via `\n`).

### Sidecar — geprüft und in Ordnung (Auswahl)
Kein Stacktrace nach aussen, OOM→LRU-Evict+503, atomare VRAM-Admission unter einem Lock
(`gpu_manager.py:540-583`), Bild-Limits vor Dekodierung, Junction/Symlink-Schutz +
Receipt-Hashing beim Training-Export, `/health` ohne Informationsleck.

---

## 7. Neue Befunde — Tests & CI (T-)

### T-M1 (MITTEL) — Pre-Push-Hook lässt `ProjectModernizer.Tests` aus und baut Debug
- **Ort:** `.githooks/pre-push:51-53` testet nur 3 von 4 Projekten (AGENTS.md verlangt
  alle 4; CI fährt alle 4, `ci.yml:38-39`) und ohne `-c Release`.
- **Fix:** Viertes Projekt + Release ergänzen oder Auslassung begründen.

### T-M2 (MITTEL) — `ki-release-gate.ps1` kann vakuum-grün werden
- **Ort:** `scripts/ki-release-gate.ps1:45` filtert `Category=Integration`; 0 Treffer →
  `dotnet test` exit 0 (empirisch bestätigt). Aktuell matchen 2 fail-closed Tests — kein
  aktuelles Fehlgrün, aber ein Trait-Refactoring macht das Gate still nutzlos.
- **Fix:** Auf "Kein Test entspricht"/0 gelaufene Tests prüfen → `exit 2`
  (Referenzmuster: `JunctionCapabilityGateTests.cs:14-27`).

### T-M3 (MITTEL) — Rote CI blockiert nichts
- **Beleg:** 7+ rote Runs in Folge (2026-08-08→08-14) folgenlos; alle Branches
  `"protected": false`, kein Required Status Check. Die drei Blocker wurden am
  2026-08-14 behoben (FfmpegProbe-Echttest, pyproject build-backend, getrackte
  names.json-Fixture) — aber ohne Protection kann Rot wieder Normalzustand werden.
- **Fix:** Branch-Protection mit Required-Check "CI" auf dem Zielbranch.

### T-NIEDRIG (kurz)
- **T-N1** `YoloClassVsaMapperTests.cs:124-126` stiller Pass statt sichtbarem Skip (bares
  `return`, wenn Gewichte fehlen — in CI immer). Fix: Gate-Attribut mit Skip-Grund.
- **T-N2** LFS-Totlast: Hook erzwingt git-lfs (`pre-push:8-12`), CI checked mit
  `lfs: true` — aber 0 LFS-Objekte, kein `.gitattributes`. Fix: streichen oder nutzen.
- **T-N3** Neue feste-ms-Races: `SettingsStorePersistHardeningTests.cs:62` (250 ms),
  `TrainingSamplesStorePersistenceTests.cs:480`, `KnowledgeBackupServiceIsolationTests.cs:158`.
- **T-N4** 46 Testdateien >500 Zeilen (Spitze: `TrainingCenterSelfTrainingArchitectureTests.cs`
  2.896) — bei nächster Erweiterung teilen.
- **T-N5** `CollectionBehavior(DisableTestParallelization)` versteckt in
  `AiPlatformConfigTests.cs:11` statt `AssemblyInfo.cs`.
- **T-N6** CI ohne NuGet-/pip-Caching (`ci.yml:19-21`) — ~9 Min Laufzeit; `cache: true`.

### Tests/CI — geprüft und in Ordnung (Auswahl)
AGENTS.md ↔ CI decken sich exakt; Parallelisierungs-Races gegengeprüft (Env-mutierende
Klassen in serialisierter `[Collection("EnvironmentVars")]`); keine immer-wahren Asserts,
keine assert-losen Tests (12 Kandidaten manuell widerlegt); Gate-Ehrlichkeit vorbildlich
(Still-Skip wird hart rot); Fixtures schlank (~20 KB, keine Binar-Fixtures).

---

## 8. Neue Befunde — Repo-Hygiene & Doku (R-)

### R-M1 (MITTEL) — `src/AuswertungPro.Next.UI/AppSettings.zip` (2,5 MB): toter Code-Snapshot
- **Beleg:** Zweitgrösster Blob im Index (Feb-2026-Stände), **null Referenzen** in
  Code/csproj/Doku. Aufräum-Commit existiert nur auf Archiv-Branch, nie gemergt.
- **Fix:** `git rm` (Historie bewahrt den Inhalt).

### R-M2 (MITTEL) — Amtsblatt-Monitor: wöchentliche Excel-Ausgaben im Repo
- **Beleg:** `Amtsblatt-Monitor/Amtsblatt_Uri_KW29_2026_*.xlsx` getrackt; RUNBOOK
  beschreibt sie als wöchentliche Ausgaben; `.gitignore:149-150` deckt sie nicht ab.
- **Fix:** Ignore-Regel `Amtsblatt-Monitor/Amtsblatt_Uri_KW*.xlsx` + `git rm --cached`.

### R-NIEDRIG (kurz)
- **R-N1** `docs/tools/tool-inventory.md` nennt 14 der ~50 Tools nicht (u.a.
  `SewerStudioMcpServer`, `ProjectModernizer`). → Abgleich gegen `ls tools/`.
- **R-N2** SYSTEM-FAKTEN.md Zeilenreferenzen gedriftet (`sam3_enabled` :136→:143).
- **R-N3** CODEBASE-KARTE.md sagt "41 CLI-Werkzeuge", tatsächlich 43 csproj.
- **R-N4** ADR-Nummernkollision: zwei ADR-007-Dateien.
- **R-N5** Vier Altdateien im tools/-Root löschen (`MdbSchemaReader.cs`,
  `MdbVideoMapping.cs`, `mdb_si_t_fields.txt`, `mdb_video_mapping.txt` mit absoluten
  Pfaden; bereits 2026-07-21 als Kandidaten genannt).
- **R-N6** TFM-Drift: `tools/CadasterDbReader` auf net8.0, alle anderen net10.0.
- **R-N7** Zehn lose MD/TXT im Repo-Root → nach `docs/` verschieben.
- **R-N8** `.gitignore` ohne `training/class_maps/*.bak_*`-Regel (3 Dateien liegen
  untracked herum).
- Offenes Arbeitspaket aus Briefing 2026-08-10: `FullBackupProgressTests.cs` existiert
  bis heute nicht.

### Repo — geprüft und in Ordnung (Auswahl)
Keine Artefakt-Verzeichnisse im Index (`artifacts/`, `.tmp/`, `obj/`, `_legacy/` u.a.
sauber ignoriert); `Export_Vorlage/*.xlsx` aktiv referenziert; sln ↔ Dateisystem exakt
konsistent (51/51); `Directory.Build.props` zentral (Nullable, LangVersion, Analyzer,
Lockfile-Restore); 43 `packages.lock.json` getrackt; CLAUDE.md 45/48 Pfadreferenzen
verifiziert, keine Versions-Drift bei Paketen (kein Paket in zwei Versionen).

---

## 9. Priorisierter Verbesserungsfahrplan

**Welle 1 — sofort (klein, Daten-/Modellschutz): UMGESETZT am 2026-08-15.**
R-H1, S-H1, S-M1 und M13 sind behoben (`SlotState.content_id` +
`discard_foreign_content` + gemeinsames `yolo_test_slot.PREDICT_LOCK`, neuer
Wechseltest `sidecar/tests/test_yolo_test_slot_switch.py`, 294 Sidecar-Tests grün).
Offen aus dieser Welle: R-M1, R-M2, S-M2.

1. R-H1: `git rm --cached sidecar/yolo11m.pt` (+ R-M1 AppSettings.zip, R-M2
   Amtsblatt-Ignore) — null Risiko, sofortige Repo-Entlastung.
2. S-H1 + S-M1 + S-M2: Slot-`content_id`, Lease-Muster und kuratierte Fehler im
   Lernstufen-Pfad — ein zusammenhängender Eingriff plus Wechsel-Regressionstest.
3. Altbefund M13 (`dryRun = true` Default) — eine Zeile.

**Welle 2 — UMGESETZT am 2026-08-15**, ausser SEC-S1 (Lock-Update braucht einen
GPU-Verifikationslauf) und T-M3 (Branch-Protection ist eine GitHub-Einstellung).
Erledigt: Q-B1, Q-B2 entfällt zugunsten der vier Datenschutz-Punkte, T-M1, T-M2,
A-H1/A-M2 (Wächter), R-M1, R-M2 sowie die Altbefunde M2, M3, M11, M12.
Nebenbefund beim Härten: `ki-release-gate.ps1` war wegen Gedankenstrichen unter
Windows PowerShell 5.1 gar nicht lauffähig (Parserfehler) — Datei ist jetzt ASCII.

4. Q-B1 (LoadFromFile wirft), Q-B2 (Pfad-Filter ehrlich) — beide klein, Fehler werden
   sichtbar statt still.
5. T-M1 (Hook), T-M2 (Gate-Vakuum), T-M3 (Branch-Protection) — Prozesshärtung.
6. SEC-S1: requests/urllib3/certifi im Lock heben + GPU-Smoke.
7. Altbefunde M2, M3 (catch→Defaults-Muster), M4 (Enum-Pinning-Tests).

**Welle 3 — danach:**
8. A-H1/A-M2: zwei Guard-Tests (kein `ServiceProvider` in neuen VM-Ctors; keine neuen
   `.Current`-Fassaden) mit Allowlists — stoppt Architektur-Drift ohne Umbau.
9. S-M3 (Body-Limit + 422-Handler), S-M4 (SHA-Cache), M7 (Gewichts-Hash-Logging).
10. Altbefunde M1 (Lock teilen), M10 (Dialog anklemmen oder entfernen), M11, M12, M14.

**Welle 4 — bei Gelegenheit:**
11. M8 (In-Memory statt Platten-JPEGs), M9 (gepufferter Request-Body) — mit Messung
    vorher/nachher.
12. NIEDRIG-Listen (R-N1-N8, T-N1-N6, S-N1-N6, Q-B3-B7, SEC-S2-S5) als Aufräum-Batch.

---

## 10. Grenzen dieses Audits

Kein Volltestlauf (CI-Stand grün, lokale Suite nicht gefahren), keine
Last-/Nebenläufigkeits-Stresstests, keine manuelle UI-Prüfung, kein
`dotnet list package --vulnerable`-Lauf (CVE-Einordnung per Versionsvergleich/Websuche),
kein Fuzzing der Parser. Die 522 catch-all-Stellen wurden stichprobenartig (~40)
gelesen. Agenten-Gegenproben sind Code-Lektüre (ein S-Befund experimentell bestätigt);
die HOCH-Befunde S-H1/R-H1 sind statisch belegt, nicht zur Laufzeit reproduziert.
