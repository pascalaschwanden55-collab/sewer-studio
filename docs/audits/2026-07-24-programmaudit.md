# Programm-Audit SewerStudio — Gesamtbericht

Datum: 2026-07-24 · Stand: Branch `feature/gis-karte`, Arbeitsbaum (uncommitted) · Auditor: KI-gestützte Prüfung mit vollständiger Build-/Test-Verifikation

---

## 1. Executive Summary

Ausgangspunkt war ein externer Audit (Entwicklungsstand 6/10, Freigabereife 3/10) mit sechs
Freigabeblockern und sechs weiteren hohen Befunden. Alle zwölf Ausgangsfunde wurden gegen den
Code verifiziert (9 voll bestätigt, 3 teilweise, keiner frei erfunden) und **alle zwölf wurden
behoben**. Zusätzlich wurde eine eigenständige Cybersecurity-Prüfung durchgeführt; ihr
wichtigster Neufund (XTF-Medienpfad-Exfiltration, über Fremd-Importdateien ausnutzbar) wurde
ebenfalls sofort behoben.

Der gesamte Stand ist verifiziert grün:

- Release-Build der Gesamtlösung: **0 Fehler, 0 Warnungen**
- .NET-Tests: **10.408 bestanden, 0 fehlgeschlagen, 3 übersprungen** (Infrastruktur 3.071, Pipeline 1.962, UI 5.313, Modernizer 62)
- Sidecar (CPU): **142 bestanden, 2 übersprungen** (DINO/SAM-Gewichte physisch nicht vorhanden — korrekt hermetisch)
- QGIS-Integration: **5/5 OK**
- Die zwei zuvor roten Tests (Klassenkarte/Golden-Fixture) sind grün; der zuvor tote YOLO-Test läuft jetzt wirklich.

**Neubewertung:** Entwicklungsstand **8/10** (vorher 6/10). Freigabereife für selbstständige
Videoauswertung **5/10** (vorher 3/10) — die Fehlerkultur „leer statt Fehler" ist beseitigt,
aber drei Punkte stehen einer unbeaufsichtigten Freigabe noch entgegen: der aktive
Klassifikator fehlt physisch (Health meldet jetzt ehrlich `degraded`), die Python-
Abhängigkeits-Updates stehen noch aus, und der BCC-Pilot ist bewusst noch nicht produktiv.

Es wurden keine Kunden- oder Quelldaten verändert. Keine git-Mutationen.

## 2. Methodik und Bedrohungsmodell

- Verifikation jeder Audit-Behauptung durch direkte Code-Lektüre (12/12 geprüft).
- Fixes in getrennten Blöcken pro Fund, jeder mit fokussierten Tests (insgesamt ~40 neue Tests).
- Security-Prüfung in drei Strängen: S1 Secrets/Abhängigkeiten (inkl. Live-Abgleich der
  Python-Locks gegen die OSV-API), S2 Eingabeverarbeitung/Injection/Deserialisierung,
  S3 Sidecar-Angriffsfläche und destruktive Dateioperationen.
- **Bedrohungsmodell:** lokale Desktop-App, nicht elevierter Prozess, Loopback-Sidecar mit
  Pflicht-Token. Primärer Angriffsvektor sind **Fremddateien** (XTF/PDF/WinCan/IBAK-Importe,
  ausgetauschte Projektordner), nicht Netzwerkangriffe. Befunde, die lokalen Schreibzugriff
  desselben Benutzers voraussetzen, sind als Robustheit/Härtung kalibriert, nicht als
  Remote-Security.

## 3. Behobene Befunde (F1–F12 + S2)

| ID | Befund | Schwere (kalibriert) | Fix | Test-Nachweis |
|---|---|---|---|---|
| F1 | Import-Marker kann beliebige Ordner rekursiv löschen | hoch (Robustheit) | `ImportTransactionRecoveryService.CleanupStaging`: Löschung nur unter `<Projekt>\.import-staging` (GetFullPath-Prefix), Reparse-Check via `ImportFileStagingPathGuard`, Verletzung → kein Löschen + Warnhinweis | `ImportTransactionRecoveryStagingGuardTests` (3) |
| F2 | Backup folgt Junctions (kopieren/löschen außerhalb) | hoch (Robustheit) | `DirectoryMirror`: Reparse-Filter in `EnumerateFiles`, `RemoveOrphans`, `DeleteEmptyDirectories`; neuer `ReparsePointGuard`-Helper. Hinweis: `KnowledgeRealtimeMirrorService` war bereits sauber — der Audit warf beide zu Unrecht in einen Topf | `ReparsePointGuardTests` (4), `DirectoryMirrorReparsePointTests` (2) |
| F3 | Goldsample-Speichern still fire-and-forget | hoch (fachlich) | Persistenz liefert Ergebnis (`CodingTrainingSamplePersistenceResult`); bei Fehler bleibt das Panel offen, zeigt Fehlertext + „Erneut speichern"; Erfolgsstatus erst nach erfolgreichem Speichern | `CodingConfirmationSaveRetryTests` (7) |
| F4 | Ollama-Ausfall wurde zu „0 Schäden erkannt" | hoch (fachlich) | `VideoFullAnalysisService` wertet `Outcome`/`Error` aus, zählt `FailedFrames` (Telemetrie), meldet „Analyse unvollstaendig: X von Y Frames fehlgeschlagen" über den Degraded-Kanal | `VideoFullAnalysisFailureTests` (4) |
| F5 | Teilvideo wurde als vollständig gemeldet | hoch (fachlich) | `VideoFrameStream`: stderr-Tail-Puffer, ExitCode-Auswertung, Frame-Soll/Ist-Vergleich, neuer `VideoFrameStreamCompletion`-Status; Aufrufer meldet „Video nur teilweise analysiert (Frames a/b, ffmpeg-Exit n)" | `VideoFrameStreamCompletionTests` (7) + Service-Test |
| F6 | Klassifikator fehlt, `/health` meldet trotzdem ok | mittel | health.py: `status=degraded` + `status_detail=classifier_not_loaded`; .NET-DTO `SidecarHealthResponse.Classifier`; Warnung am Analyse-Start statt stiller Weiterfahrt; `PipelineHealthMonitor` unterscheidet korrekt. Die Gewichte fehlen physisch (Kandidat nicht promovt, Governance verbietet Aktivierung) — `degraded` ist der korrekte Dauerzustand bis zur Promotion | `test_health.py` (erweitert), DTO-Konsumenten geprüft |
| F7 | Echter YOLO-Test immer übersprungen (falscher Funktionsname) | mittel | `test_yolo.py` nutzt `get_runtime_status()["custom_weights_present"]`; der Test läuft jetzt wirklich und ist grün | pytest-Lauf: 5/5 in test_yolo/test_health |
| F8 | BCC_bogen in Klassenkarte V2 ohne Versionsanhebung | hoch (Governance) | V2 eingefroren (14 Klassen, Kandidatentabelle zurück auf committed Stand mit 0 Freigaben); neu `YoloDetectClassMapV3` + `detect_class_map_v3.json` + `detect_class_migration_v3.candidate.json` + `detect_class_migration_v3_review.md`; aktive Verdrahtung (ServiceProvider, StageAExporter) auf V3; Golden-Fixture maschinell neu erzeugt (Verfahren vorher gegen alte Fixture byte-genau validiert), neue plan_id `42b534c6…` | beide Alt-Rottests grün; neuer Freeze-Fact `Eingefrorene_V2_Vorlagen_bleiben_ohne_BCC_lesbar`; V3-Snapshot-Test |
| F9 | Exportregister ignoriert still 121 Goldsamples | mittel (Transparenz) | Pilot-Gate bleibt bewusst manuell; der Coordinator meldet übersprungene exportfähige Goldsamples jetzt mit Anzahl + IDs (Ergebnisfeld `RegistryGateSkippedSampleIds`, Stage `RegistryGateNotice`, UI-Log/Status) | `TrainingYoloExportCoordinatorRegistryGateTests` (2) |
| F10 | „KI-Wissen exportieren" unvollständig + DB-Rohkopie | hoch (fachlich) | Katalog um `personal_gold_v1`-Abschnitt erweitert (`training/`-Baum inkl. `export_registry_v1.json`, `gold_standard`, `gold_migrations`, `eval_set`); DB als geprüfter Online-Snapshot via `SqliteSnapshotCopyService` (Backup-API + integrity_check, wie der Echtzeit-Spiegel); Checkpoint-Fehler bricht den Export ab; Import entfernt veraltete WAL-Begleiter rollback-sicher | `KnowledgeBackupTrainingExportTests` (4) |
| F11 | Befund-Vermischung per Drag-and-drop zwischen Haltungen | hoch (fachlich) | Statische Drag-Felder → Instanz; Payload trägt `SourceSessionKey` (PlayerWindow-Instanz = Haltung); Cross-Session-Drops verworfen + Overlay-Hinweis | `CodingEventDragDropSessionGuardTests` (8) |
| F12 | Dedup verschmilzt getrennte gleichcodierte Schäden im selben Bild | hoch (fachlich) | `TemporalFindingDeduplicator`: Same-Frame-Kollision nur noch bei räumlicher Überlappung (IoU ≥ 0,3, Option `SameFrameMergeMinIoU`); getrennte Befunde laufen eigenständig weiter; ohne BBox altes Verhalten (Bestandsschutz) | `TemporalFindingDeduplicatorSpatialTests` (5) |
| S2 | XTF-Medienpfad kopiert beliebige lokale Dateien ins Projekt (Exfiltration, über Fremd-XTF ausnutzbar) | **hoch (Security)** | `MediaFileAllowlist` (App-kanonische Medien-Extensions + WinCan/IBAK-Container); Resolver verwirft Nicht-Medien, `..\..`-Traversal (Containment gegen XTF-Verzeichnis) und UNC; Kopierpfade (`MediaDistributionService`, `KanalImportDistributionService`, `ProjectPortabilityService`) auf Medien/PDF begrenzt, UNC vor jeder Existenzprüfung abgelehnt. Legitime Workflows (Medien auf anderen Laufwerken) unverändert | `XtfMediaPathSecurityTests` (17) + 224 XTF-/Media-Bestandstests grün |

## 4. Security-Befunde — offene Punkte (Maßnahmenplan)

### S1 Abhängigkeiten & Secrets

| ID | Schwere | Befund | Maßnahme |
|---|---|---|---|
| S1-1 | hoch | `transformers==4.57.6`: 4 OSV-Meldungen, 3× RCE (Vektor: präparierte Modelldateien; Sidecar lädt nur lokale Gewichte) | Update ≥ 5.5.0 (Major — Kompatibilität groundingdino/timm testen) |
| S1-2 | hoch | `pillow==12.2.0`: 13 OSV-Meldungen (OOB read/write, Decompression-Bomb; verarbeitet fremde Inspektionsbilder) | `pillow>=12.3.0` |
| S1-3 | mittel | HTTP-Stack 2022: `requests 2.28.1`, `urllib3 1.26.13`, `idna 3.4`, `certifi 2022.12.7` (Credential-Leaks, DoS, entzogene CA-Roots) | gemeinsames Upgrade (`requests≥2.33`, `urllib3≥2.7`, `idna≥3.15`, certifi aktuell) |
| S1-4 | mittel | `setuptools==78.1.0`: Path-Traversal/Arbitrary-File-Write | ≥ 83.0.0 |
| S1-5 | mittel | `onnx 1.21.0` (DoS/OOB-Read), `starlette 1.1.0` (DoS/Hostname-Poisoning), `torch 2.12.0.dev` (Memory-Corruption via jit.script) | onnx ≥ 1.22, starlette ≥ 1.3.1, torch ≥ 2.13 beim nächsten cu128-Stand |
| S1-6 | mittel | YOLO-Fallback lädt `yolo11m.pt` von GitHub ohne Hash-Prüfung | Fallback-Gewicht pinnen + SHA-256 prüfen, oder `SEWER_SIDECAR_REQUIRE_CUSTOM_YOLO=true` |
| S1-7 | niedrig | `tools/CadasterDbReader`: Fallback-Passwort „masterkey" (Firebird-Vendor-Default) | Fallback entfernen, `FDB_PASSWORD` verpflichtend |
| S1-8 | niedrig | Schreib-Roots (`C:\KI_BRAIN` u. a.) ohne Blocklist gegen Systempfade | Root-Auflösung: Systemverzeichnisse/Laufwerks-Roots ablehnen |
| S1-9 | niedrig | `<NuGetAudit>` nicht explizit; NU190x können untergehen | explizit aktivieren, Warnungen im Release-Gate sichtbar |

**Positiv:** keine echten Secrets im Working Tree (keine Keys, Tokens, Connection-Strings);
Sidecar-Token `secrets.token_urlsafe(32)` + `hmac.compare_digest`; .NET-Pakete ohne bekannte
kritische CVEs; `RestorePackagesWithLockFile` aktiv. Die „40 Meldungen in 10 Paketen" des
Ausgangs-Audits bestätigen sich exakt (S1-1 bis S1-5). Empfehlung: `pip-audit` ins Release-Gate.
Python-Updates bewusst **nicht** in dieser Runde durchgeführt (Major-Upgrades erfordern
kontrollierte GPU-/CPU-Testwiederholung).

### S2 Eingabeverarbeitung

Behoben: S2-1/S2-2/S2-3 (siehe Tabelle oben). Offen:

| ID | Schwere | Befund | Maßnahme |
|---|---|---|---|
| S2-4 | niedrig | vereinzelt `Arguments`-String statt `ArgumentList` (nicht injizierbar, inkonsistent) | auf `ArgumentList` umstellen |
| S2-5 | niedrig | WinCan-Katalog-Vorabscan mit `DtdProcessing.Ignore` statt `Prohibit` (kein XXE unter .NET 10) | `SafeXmlDocumentLoader` einheitlich |
| S2-6 | niedrig | Scriban-HTML ohne Auto-Escaping im headless Chromium (Fremddatenfluss nicht belegt) | Modellwerte HTML-encodieren |
| S2-7 | niedrig | `tools/FachwissenIndexer`: `XDocument.Load` ohne DTD-Einschränkung (internes Tool) | gehärteten XmlReader |

**Positiv geprüft:** ZIP-Slip-Schutz im Wissen-Import korrekt; SQL-Zugriffe durchgehend
parametrisiert; kein `BinaryFormatter`/`TypeNameHandling` im gesamten src/; Prozessaufrufe mit
`UseShellExecute=false` + `ArgumentList`; XTF-XML-Reader gehärtet (`DtdProcessing.Prohibit`);
Ollama-Antworten JSON-Schema-gebunden (kein LLM→Pfad-/Shell-Fluss).

### S3 Sidecar & Löschpfade

| ID | Schwere | Befund | Maßnahme |
|---|---|---|---|
| S3-1 | — | Import-Recovery-Löschpfad | **behoben mit F1** (Root-Verankerung + Reparse-Check) |
| S3-2 | mittel | Rollback-Pfad `Path.Combine(projectRoot, RelativePath)` ohne Traversal-Check (entschärft durch SHA-256-Gate) | RelativePath validieren (Muster: `ImportFileStagingSession.DeletePublishedFileIfUnchanged`) |
| S3-3 | niedrig | QGIS-Bridge (Loopback, nur GET) ohne Auth/Host-Header-Prüfung → lokale Prozesse/DNS-Rebinding können Projektsnapshots lesen | Host-Header-Allowlist + `nosniff` |
| S3-4 | niedrig | `tools/VideoLabelTool`: `/session.json` gibt POST-Token ohne Host-Prüfung | an `is_allowed_local_host` binden |
| S3-5 | niedrig | Live-Control `/qgis/*` tokenfrei, kein Host-Header-Check | Host-Header-Allowlist |
| S3-6 | niedrig | `/health` Info-Disclosure (PID, Pfade); kein Rate-Limit | optional reduzieren |
| S3-7 | niedrig | Frames gehen auch an nicht-lokale `PipelineSidecarUrl` (bei Ollama wird gewarnt) | Warnung/Block wie bei Ollama |
| S3-8 | Beobachtung | PowerShell-Quoting in `AiStartupOrchestrator.Quote()` wirkungslos (Pfad kontrolliert, praktisch nicht erreichbar) | `ArgumentList` statt String |

**Positiv:** Sidecar bindet doppelt erzwungen Loopback (pydantic-Validator + Startskript),
Pflicht-Token mit Konstantzeit-Vergleich, Host-Header-Allowlist, kein CORS, der einzige
Schreib-Endpunkt (`/training/export-yolo`) ist hash-, junction- und receipt-gehärtet.
Löschpfade mit sauberer Verankerung (Referenzmuster): `KnowledgeRealtimeMirrorService`,
`FullBackupService`, `BackupTargetGuard` (Marker-Konzept), `ProgramCleanupService`,
`ImportFileStagingSession`, `TrainingExportPlanLocalExecutor`.

## 5. Architektur- und Qualitätsbewertung

- **Stärken:** ungewöhnlich große Testsuite (10.400+ .NET-Tests) mit Golden-Fixture-Verträgen,
  Architektur-Scantests und echten GPU-/Import-End-to-End-Prüfungen; Guards existieren als
  wiederverwendbare Bausteine (`ImportFileStagingPathGuard`, `BackupTargetGuard`,
  `SqliteSnapshotCopyService`); atomare Speichermuster; strenge persönliche Gold-Regel im
  YOLO-Export.
- **Erkannte Schwäche (Muster, kein Einzelfall):** „leer statt Fehler" — Ausfälle wurden an
  mehreren Stellen in leere Ergebnisse übersetzt und downstream als Erfolg behandelt
  (Ollama, ffmpeg, Goldsave, Health). Alle vier Pfade melden Fehler jetzt explizit; für neue
  Services gilt: Fehler gehören ins Ergebnisobjekt, nicht nur ins Log.
- **Zweite Schwäche:** Guards wurden nicht überall angewendet, wo sie gebraucht wurden
  (Recovery-Löschung, DirectoryMirror). Jetzt geschlossen; bei neuen Lösch-/Kopierpfaden sind
  die vorhandenen Guards Pflicht.
- **Governance-Gewinn:** Klassenkarten sind ab v3 sauber versionsgrenzüberschreitend
  (v2 eingefroren, Freeze-Test nagelt das fest); die menschliche Freigabe ist versioniert
  dokumentiert (`detect_class_migration_v3_review.md`).

## 6. Restrisiken & Maßnahmenplan (Priorität)

1. **Python-Abhängigkeiten aktualisieren** (S1-1 bis S1-5), danach CPU- und GPU-Tests
   wiederholen; `pip-audit` ins Release-Gate.
2. **Klassifikator promovieren oder Ersatz trainieren** (Kandidat
   `manual1286_fixedval_round3train_v8n_320_dropout02` liegt vor, ist aber nicht evaluiert;
   erst danach hebt sich `degraded`).
3. **S3-2** Rollback-Pfad-Validierung (kleiner Fix, Muster liegt im Repo).
4. S2-4/S2-5/S2-6/S2-7, S3-3/S3-4/S3-5/S3-7 (kleine Härtungen, eine Arbeitssitzung).
5. BCC-Pilot: Negativbilder ergänzen, unabhängige Eval, erst dann Produktivschaltung
   (mAP50–95 = 0,198 ist noch kein produktiver Stand).
6. Folge-Verbesserungen (kein Blocker): `MultiModelAnalysisService` wertet
   `VideoFrameStreamCompletion` noch nicht aus; PDF-Kopie auf die PDF-Felder einschränken.
7. Skill `sewer-architektur` abgleichen (AGENTS.md-Pflicht nach Pipeline-/Service-Änderungen) —
   in dieser Umgebung nicht registriert; CLAUDE.md wurde bereits präzisiert (v2/v3-Klassenzahl).

## 7. Anhang — ausgeführte Verifikation

```
dotnet build AuswertungPro.sln -c Release --no-restore        → 0 Fehler, 0 Warnungen
dotnet test Infrastructure.Tests  → 3.071 bestanden, 0 Fehler, 1 übersprungen
dotnet test Pipeline.Tests        → 1.962 bestanden, 0 Fehler, 1 übersprungen
dotnet test UI.Tests              → 5.313 bestanden, 0 Fehler, 1 übersprungen
dotnet test ProjectModernizer     →    62 bestanden, 0 Fehler
sidecar: pytest -m "not gpu" -q   →   142 bestanden, 2 übersprungen
integrations/qgis: unittest       →     5 bestanden
```

Neue Tests in dieser Runde: ~40 (F1: 3, F2: 6, F3: 7, F4/F5: 12, F6: 2, F8: 2, F9: 2,
F10: 4, F11: 8, F12: 5, S2: 17 — teils in geteilten Dateien). Geänderte Produktionsdateien:
~30; neue Produktionsdateien: 5 (`ReparsePointGuard`, `MediaFileAllowlist`,
`YoloDetectClassMapV3`, `detect_class_map_v3.json`, `detect_class_migration_v3.candidate.json`).
