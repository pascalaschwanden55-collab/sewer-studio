# Programmaudit SewerStudio — 2026-08-10 (Kimi)

**Umfang:** 270.847 LOC Produktiv-C# (4 Projekte), 253.179 LOC Tests (11.541
Testfaelle), 43 Tools-Projekte, Sidecar 5.827 LOC Python, training/scripts
42.533 LOC Python.
**Methode:** 10 parallele Dimensions-Scans (explore-Subagenten) mit Belegpflicht
(Datei:Zeile + gelesener Code + Schadensmechanismus) und Pflicht-Gegenprobe je Befund;
danach Stichproben-Eigenpruefung der schwersten Befunde durch den Hauptlauf.
**Nicht wiederholt:** die Audits 2026-07-24 (12 Befunde, behoben) und die
Arbeitspakete vom 2026-08-10 (AP-1 bis AP-4, behoben bzw. in Umsetzung).

**Bilanz:** 4 HOCH-, 13 MITTEL-, ~17 NIEDRIG-Befunde. Kein Befund ist unmittelbar
produktiv aktiv; alle vier HOCH brauchen eine unguenstige Gleichzeitigkeit
(Dateisperre waehrend eines Schreibens bzw. parallellaufende App) — aber genau
diese Klasse hat AP-1 als real erwiesen. H1-H3 lassen den bisherigen Stand
zunaechst in `.bak` wiederherstellbar; die aktive Datei ist beschaedigt,
endgueltig verloren waere er erst nach drei weiteren Speicherungen.

## Umsetzungsstand 2026-08-12

- **H1-H3 behoben:** Fehlende Datei bleibt ein leerer Erstlauf; eine vorhandene,
  aber unlesbare oder strukturell ungueltige JSON-Datei bricht Laden und Mutation
  ab. Kein Store speichert danach einen leeren Ersatzbestand. Je Store prueft ein
  Regressionstest sowohl kaputtes JSON als auch ein ungueltiges `null`-Dokument.
- **H4 behoben:** `SelfTrainingHarness` prueft `SewerStudio.exe` vor und waehrend
  des Laufs. Die Ruecksetzung verlangt zusaetzlich einen bytegleichen
  SHA-256-Stand; bei Konflikten bleiben aktueller Store und eindeutige Sicherung
  unangetastet. Vier Verhaltenstests decken Wiederherstellung, App-Sperre,
  Parallelveraenderung und einen anfangs fehlenden Store ab.
- **M5 behoben:** `SidecarEndpointPolicy` ist die gemeinsame Token-Grenze fuer
  Haupt-, Start- und Neustartpfad. Nicht lokale Sidecar-Ziele erhalten keinen
  `X-Sidecar-Token`-Header.

---

## HOCH — sofort angehen (alle vier dasselbe Muster wie AP-1)

### H1 — `TeacherAnnotationFileStore`: Lesefehler → leere Liste → Save
- **Ort:** `src/AuswertungPro.Next.Infrastructure/Ai/Teacher/TeacherAnnotationFileStore.cs:144-149`
  (catch → `new List<TeacherAnnotation>()`), Schreiber `AppendAsync:70-84`, `DeleteAsync:100-111`
- **Schaden:** Eine voruebergehende Sperre (Virenscanner, Spiegeldienst) beim
  Bestaetigen einer Annotation ersetzt `teacher_annotations.json` durch die eine
  neue Zeile. Persoenliche Handlabels. Eigenpruefung Hauptlauf: **bestaetigt.**
- **Fix:** Wie AP-1: fehlende Datei = leere Liste; vorhandene, unlesbare = werfen.

### H2 — `ProtocolTrainingFileStore`: Lesefehler → leerer Bestand → Save
- **Ort:** `src/AuswertungPro.Next.Infrastructure/Ai/Training/ProtocolTrainingFileStore.cs:111-117`
  (catch → `new ProtocolTrainingData()`), Schreiber `AddSample` über `Save:120-124`.
  Aufrufer: Protokoll-Bestaetigung in `ProtocolObservationsWindow.xaml.cs:344`.
- **Schaden:** Alle manuell freigegebenen Protokoll-Lernbeispiele gehen bei einer
  kurzen Sperre verloren; Speichern meldet Erfolg. Eigenpruefung: **bestaetigt.**
- **Fix:** Dieselbe Trennung fehlend ≠ unlesbar.

### H3 — `AiOptimizationSessionFileStore`: Lesefehler → `[]` → SaveAsync
- **Ort:** `src/AuswertungPro.Next.Infrastructure/Ai/Sanierung/AiOptimizationSessionFileStore.cs:96-102`,
  Schreiber `SaveAsync:47-53` (laedt alle, ersetzt gleichnamige, schreibt).
- **Schaden:** Alle frueheren KI-Sanierungssitzungen weg bei einem Speichern
  waehrend einer Sperre. Eigenpruefung: **bestaetigt.**
- **Fix:** Wie oben.

### H4 — `SelfTrainingHarness` stellt Store-Snapshot ohne App-Sperre zurueck
- **Ort:** `tools/SelfTrainingHarness/Program.cs:46-48` (Backup), `:128-142`
  (Restore per `File.Copy(..., overwrite: true)` ohne Pruefung).
- **Schaden:** Laueft SewerStudio waehrend des Harness und der Benutzer bestaetigt
  Goldsamples, werden sie beim Restore still mit dem Vorspann-Snapshot
  ueberschrieben. Der App-laeuft-Check existiert im Schwester-Skript
  (`repair_gold_holding_ids.py:335-345`) — hier fehlt er. Eigenpruefung: **bestaetigt.**
- **Fix:** Vor Start auf laufende App sperren; Restore nur bei unveraenderter Datei
  (Byte-/Hash-Vergleich).

---

## MITTEL (Auswahl, vollstaendig belegt)

**Persistenz/Datenkultur**
- **M1 — Zwei Store-Instanzen auf dieselbe Datei (Lost Update):**
  `TeacherAnnotationStore.cs:11` und `AiOptimizationSessionStore.cs:11-12` (statische
  `.Current`-Fassaden) vs. DI-Instanzen (`ServiceProvider.cs:401-402`); die
  `_fileLock`-Semaphores sind Instanz-Locks. `TrainingSampleFileStore:20-23` zeigt,
  wie es geht (pfadbasiertes prozessweites Lock).
- **M2 — `SchachtMassnahmenKatalogStore.cs:48-52`:** leerer catch → Defaults; der
  Editor (`SchachtMassnahmenDialogController.cs:120/128`) speichert die Defaults
  ueber die Benutzerliste mit eigenen Preisen. Mindestens loadError-Muster der
  Kosten-Stores uebernehmen.
- **M3 — `AppSettings.cs:375-379`:** generischer catch (z.B. Sperre beim Start)
  liefert Defaults. Der urspruenglich genannte Startpfad (`App.xaml.cs:94-95`)
  speichert normalerweise nicht — das Grundrisiko steht stattdessen in
  `ServiceProvider.cs:728`, der die Einstellungen beim Start speichert: Danach
  ueberschreiben die Defaults die echte `settings.json`. Restore-Point existiert,
  aber niemand erfaehrt davon. Sichtbare Startwarnung + bei Fehlerlauf nicht
  ohne Rueckfrage persistieren.
- **M4 — Fach-Enums als nackte ints:** `TrainingSampleStatus` wird als int in
  `training_samples.json` gespeichert und hat fuer die Werte 0-3 bereits einen
  Pinning-Test (`TrainingSampleStatusTests.cs:8`); **`Draft=4` und die
  Protokoll-Enums (`ProtocolEntrySource`, `ProtocolChangeKind`) sind dagegen
  ungeschuetzt.** Dort deutet ein Einfuegen in der Mitte Altbestaende still um.
  Fix: die fehlenden Werte pinnen (explizit zuweisen + Pinning-Test).

**Sicherheit**
- **M5 — Sidecar-Token ohne Loopback-Pruefung auf Start-/Neustartpfad:**
  `AiStartupService.BuildSidecarHeaders:187-198` haengt das Token an, ohne die URL
  zu pruefen; der Hauptpfad macht es richtig (`VisionPipelineClient.cs:72-75`,
  `IsLoopbackUri`). Bei einer nicht-lokalen **`http://`**-URL geht das Token als
  Klartext ins LAN (bei HTTPS verschluesselt der Transport). Eigenpruefung: **bestaetigt.**
- **M7 — DINO/SAM laden Gewichte ohne Hash/Identitaetsbindung:** alphabetisch
  erste Datei (`sam_wrapper.py:25-28`, `dino_wrapper.py:32-36`), bei DINO Config
  und Gewicht sogar unabhaengig gemischt waehlbar. YOLO/cls/BCC zeigen die
  strenge Variante. Minimal: Auswahl + SHA beim Laden loggen und in /health ausgeben.

**Performance**
- **M8 — Bogen-Scan schreibt tausende JPEGs auf die Systemplatte:**
  `BendSuggestionScanService` nutzt die Platten-Extraktion statt des vorhandenen
  In-Memory-`VideoFrameStream`. Mechanismus bestaetigt; die Groessenordnung des
  I/O (GB je Video) ist eine Schaetzung aus Bildgroesse x Bildzahl, nicht
  gemessen.
- **M9 — Frame-Base64 wird pro Request neu in JSON kopiert (bis zu 5×/Frame):**
  `MultiModelAnalysisService` erzeugt den Base64-String einmal pro Frame;
  `VisionPipelineClient.PostAsync` serialisiert ihn dann je Request neu in eine
  grosse JSON-Nachricht plus UTF-8-Kopie. Mechanismus bestaetigt; die MB-Zahlen
  pro Frame/Lauf sind Schaetzungen aus Stringlaengen, keine Messung. Fix:
  Bytes statt Base64-JSON oder gepufferter Request-Body.

**Wartbarkeit/Fachlich**
- **M10 — Verwaister `ProtocolEntryEditorDialog` mit abweichender Accepted-Regel:**
  736 Zeilen ohne Aufrufer (Volltextsuche), aber eigene Fachregel
  `Ai.Accepted = suggested == final` im Widerspruch zu `AiDecisionAuditMapper`.
  Entweder wieder anklemmen + delegieren, oder samt Architekturtest entfernen.
- **M11 — `import_gold_labels.py:319-326`:** schreibt `training_samples.json`
  nicht-atomar (write_text) und ohne App-laeuft-Check. Muster aus
  `repair_gold_holding_ids.py` uebernehmen.
- **M12 — `tools/PdfProtocolIngest/enrich_vsa_dataset.py`:** Eval-Schutz fail-open
  bei falschem Pfad (leere Signaturmenge) + Schreiben ist Default. Fix: hart
  abbrechen bei 0 Signaturen, `--execute`-Gate.
- **M13 — `tools/PersonalGoldMigration/Program.cs:6`:** `dryRun = false` als
  Default — ein blanker Lauf mutiert den Goldbestand. Default drehen.
- **M14 — Einziger E2E-Test des VSA-KEK-Katalogbaus dauerhaft tot:**
  `VsaKekCatalogBuilderTests.cs:12` hat hartes `Skip` — laeuft nie, auch nicht
  auf einem Rechner mit Fixture. 322-Code-Regelbau unverifiziert. Fix:
  Fixture-Gate statt hartem Skip oder Mini-Fixture ins Repo.

## NIEDRIG (kurz gehalten; Befunde mit Ort direkt im Text)

Test-Races: AP-1-Schutztest mit 300-ms-Sperrfenster (`TrainingSampleFileStoreTests:43-53`),
Poll ohne Sharing-Toleranz (`KnowledgeRealtimeMirrorServiceTests:218-223`),
Negativ-Assert nach 150 ms (`:344-346`). Spiegel: `RemoveOrphans` ohne
CancellationToken bei App-Exit; `_nextFullScanUtc` nicht volatile. Fehlerkultur:
stille Gold-Pruefqueue-Auslassung, XTF-Fallback ohne Log, KINS „kein Format" statt
„nicht lesbar", MergeEngine-Log ohne Fehlertext, Checkpoint-Journal erzwingt
Neuanalyse ohne Eintrag. Sidecar: `extra="forbid"` nur bei BCC/Training;
nvidia-smi-Subprozess unter dem YOLO-Predict-Lock; Telemetrie-Rotation ohne
prozessuebergreifenden Schutz. Performance: GPU-Auto-Erkennung ohne Cache
(nvidia-smi je `PipelineCfg`-Zugriff, auch UI-Thread), SystemMonitor startet
taeglich tausende Hilfsprozesse. Tools: `tools/video_ai` (totes Paket mit drittem,
abweichendem Meterleser — nach `_legacy/` oder loeschen). UI: Datei-Schreiben in
Page-ViewModels (Overview/Builder/Export), `TrainingCenterWindow` komponiert VM mit
15 Singleton-Fallbacks, `AnnotationWorkbenchService` am God-Service-Rand (21
Ctor-Parameter — erst bei naechster Erweiterung spalten).
Serialisierung: Migrations-Save im Lese-try des `TrainingSampleFileStore`
(diagnostiziert Schreibfehler als Korruption — NIEDRIG).

## Zurueckgestuft nach Gegenpruefung

- **SQL-Injection ueber MDB-Tabellennamen (`M150MdbRowReader.cs:181`):**
  Zunaechst als MITTEL gemeldet — nach Gegenpruefung nicht belegt: Access
  verbietet `]` in Objektnamen, ein normal gueltiger MDB-Tabellenname kann den
  Ausdruck `[$table]` nicht verlassen. Escaping bleibt gute Vorsorge, ein
  Sicherheitsbefund ist es erst mit einer reproduzierbaren praeparierten MDB.

## Geprueft und in Ordnung (die Bilanz gehoert dazu)

- **Referenzmuster halten:** Kosten-Stores mit `CostStoreFileProbe` + Speicher-Sperre
  bei loadError; `projekt.json` mit Versions-Gate, atomarem Replace, Restore-Points;
  `KnowledgeBaseContext` mit user_version-Gate, WAL, additiven Migrationen;
  Export-Registry/Holdout-Stores durchgaengig fail-closed mit SHA-256-Bindung.
- **Concurrency-Hygiene im src/-Prozess gut:** Locks in finally, CancellationToken-
  Disziplin breit eingehalten, Dispatcher-Marshalling konsistent, VideoFrameStream
  mit Backpressure/Timeout/Prozessbaum-Kill. (Gilt fuer Threads/Locks im Prozess;
  die Sperren-Faelle H1-H3 und M1 sind Interprozess-/Persistenz-Themen und stehen
  dort.)
- **Sidecar-Kern hart:** GPU-Lease-Atomaritaet (Eviction unter einem Lock),
  fail-closed BCC-Kandidatenpruefung, saubere Fehlerwege (503 statt Stacktrace),
  hmac.compare_digest, Loopback-Validatoren, Bild-Limits vor Dekodierung.
- **Testbilanz ehrlich:** 11.541 Testfaelle aus den erkannten Testmethoden,
  17 hinter Gates (alle mit Grund), keine immer-wahren Asserts gefunden,
  Top-10-Grossklassen alle testabgedeckt.
- **Tools-Hauptbestand diszipliniert:** repair_*/prepare_*/publish_* mit
  Dry-Run-Default, atomaren Writes, App-laeuft-Sperren und Verifikationen.
- **Sicherheitsbasis solide:** XTF-Fix intakt, WinCan/IBAK/KINS reduzieren
  Fremdpfade auf Dateinamen, C#-SQL parametrisiert, LiveControl/MCP loopback-
  + token-erzwungen, Diagnosepaket redigiert.

## Grenzen dieses Audits

Kein Volltest gefahren (die Suite war zuletzt nach den AP-Aenderungen gruen),
keine Last-/Nebenlaeufigkeits-Stresstests, keine manuelle UI-Pruefung. Python-
Sidecar und C#-Vertraege nur statisch geprueft. Die Agenten-Gegenproben sind
Code-Lektuere, keine Ausfuehrung — die vier HOCH und die Sicherheitsbefunde
M5/M6 wurden zusaetzlich vom Hauptlauf am Code verifiziert.

## Vorschlag zur Reihenfolge

1. **Welle 1 (diese Woche):** H1-H3 in einem Zug (identischer Fix wie AP-1,
   ein gemeinsamer Test pro Store) + H4 (App-Sperre + Hash-Vergleich).
   **Stand: umgesetzt** — alle drei Stores fail-closed mit Tests, der Harness
   sperrt bei laufender App und stellt nur bei unveraenderter Datei zurueck.
2. **Welle 2:** M1 (Lock teilen), M3 (Startwarnung), M5 (Loopback-Gate) —
   alle klein, alle Daten-/Geheimnis-schuetzend. **M5 umgesetzt** (zentrale
   `SidecarEndpointPolicy`).
3. **Welle 3:** M11-M13 (Tool-Haertungen nach Muster), M4 (Draft=4 und die
   Protokoll-Enums pinnen).
4. **Welle 4 (bei Gelegenheit):** M7-M9, M10, M14, die NIEDRIG-Liste.
