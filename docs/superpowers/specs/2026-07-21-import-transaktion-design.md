# Absturzsichere Import-Transaktion — Design

**Datum:** 2026-07-21
**Status:** Genehmigt (Brainstorming)
**Betrifft:** Import-Commit-Flow (`ImportRunWorkflowController`), Projekt-Persistenz

## Ziel

Zwei Garantien für den manuellen Import (PDF/XTF/WinCan/IBAK/KINS):

1. **U4 — kein stiller Datenverlust:** Editiert der Nutzer das Live-Projekt, während ein
   Import läuft, dürfen diese Änderungen beim finalen `ReplaceProject` nicht still
   überschrieben werden.
2. **Atomarität bei Absturz:** Ein Prozess-/Stromausfall mitten im Import hinterlässt
   **keinen** halben Zustand — entweder der alte Projektstand bleibt vollständig intakt
   oder der neue ist vollständig da. Keine verwaisten Arbeitsordner, keine
   veröffentlichten Dateien ohne passende `projekt.json`.

**Bewusst NICHT im Umfang (YAGNI):**
- Kein Fortsetzen/Replay eines abgestürzten Imports (Neustart des Imports ist zumutbar).
- Kein Merge von Live-Edits mit dem Importergebnis (bei Konflikt gewinnen die Edits).
- Keine Änderung am bestehenden atomaren Save, File-Staging oder Restore-Point-Store.

## Kern-Entscheidungen (mit dem Nutzer abgestimmt)

- **Absturz-Garantie:** Alles-oder-nichts (Atomarität), nicht Fortsetzbarkeit.
- **U4-Konflikt-Policy:** Import abweisen — die manuellen Edits gewinnen, konsistent mit
  dem heutigen Verhalten von `EnsureProjectIsStillCurrent` bei Projektwechsel.
- **Marker-Ort:** `.import-transaction.json` im Projekt-Root (neben `.import-staging`).
- **Recovery-Trigger:** beim Projekt-Laden (deckt App-Start mit letztem Projekt und
  manuelles Öffnen ab; der Marker ist projekt-lokal).

## Architektur — zwei getrennte Bausteine

Verträge in `Application`, Implementierungen in `Infrastructure`, Verdrahtung im
`ImportRunWorkflowController`. Keine Transaktionslogik im ViewModel (CLAUDE.md).

### Baustein 1 — Live-Edit-Konflikterkennung (U4)

**Problem:** `EnsureProjectIsStillCurrent`
(`ImportRunWorkflowController.cs:356-384`) prüft nur `ReferenceEquals(GetProject(),
snapshot.Project)` — also Projekt-**Identität**. Editiert der Nutzer dieselbe Instanz,
bleibt `ReferenceEquals` true, und `ReplaceProject(targetProject)` (`:236`) überschreibt
die Edits.

**Lösung: JSON-Content-Signatur.**

- Neuer Vertrag `IProjectContentSignature` (Application/Projects) mit
  `string Compute(Project project)`.
- Implementierung `JsonProjectContentSignature` (Infrastructure/Projects): serialisiert
  das Projekt mit denselben `System.Text.Json`-Optionen wie `JsonProjectRepository`
  (WriteIndented, CaseInsensitive) und bildet SHA-256 über die Bytes. **Meta-Felder, die
  sich ohne echte Datenänderung ändern (`ModifiedAtUtc`, `Dirty`,
  `LastCommittedImportTxId`), werden vor dem Hash ausgeklammert** — sonst gäbe es
  Fehl-Positive.
- `JsonProjectRepository` exponiert seine `JsonSerializerOptions` als
  `public static JsonSerializerOptions SerializerOptions` (heute privat `Opt`), damit die
  Signatur exakt dieselbe Serialisierung nutzt.
- `ActiveProjectSnapshot` (`ImportRunWorkflowController.cs:435`) erhält zusätzlich
  `string StartSignature`, berechnet in `RunAsync` beim Snapshot-Aufbau.
- `EnsureProjectIsStillCurrent` prüft **nur im finalen Aufruf vor `ReplaceProject`**
  (`:229`) zusätzlich: aktuelle Live-Signatur neu berechnen und mit `StartSignature`
  vergleichen. Bei Abweichung → dieselbe Abweisung wie bei Projektwechsel
  („Während des Imports wurde das Projekt bearbeitet. Das Importergebnis wurde nicht
  übernommen — bitte erneut importieren."). Die drei früheren Zwischenaufrufe
  (`:109, :133, :208`) bleiben reine Identitätsprüfungen (Performance; kein Hash im Loop).

**Warum verlässlich:** Der Content-Hash erfasst jede echte Datenänderung, unabhängig
davon, ob der Edit-Weg `ModifiedAtUtc`/`Dirty` setzt. Während des Imports wird das
Live-Projekt nie gespeichert, also ändert sich sein serialisierter Inhalt ausschließlich
durch Nutzer-Edits.

### Baustein 2 — Absturz-Atomarität (Journal-Marker + Recovery)

**Beobachtung:** Der einzige wirklich atomare Schritt ist der `projekt.json`-Save
(`JsonProjectRepository.SaveInternal` via `File.Replace` + `.bak`). Das ist der
**Commit-Punkt**. Der Import berührt zwei Ressourcen (Mediendateien + `projekt.json`);
ohne verteilte Transaktion wird Atomarität über einen Marker + Recovery erreicht.

**Neue Bausteine:**

- Vertrag `IImportTransactionJournal` (Application/Import) mit `Begin`, `Complete`,
  `TryRead`, `Clear`.
- Implementierung `FileImportTransactionJournal` (Infrastructure/Import): schreibt/liest/
  löscht `.import-transaction.json` im Projekt-Root, atomar über `AtomicTextFileWriter`.
  Marker-Inhalt (`ImportTransactionMarker`, record):
  - `TxId` (Guid-String)
  - `StartedUtc`, `Label`
  - `StagingRoot` (der `.import-staging`-GUID-Ordner dieser Session)
  - `PublishedTargets`: Liste `{ RelativePath, Sha256 }` der vom Staging veröffentlichten
    Ziel-Dateien (`RelativePath` relativ zum Projekt-Root)
  - `RestorePointPath` (falls vorhanden)
- Neues `Project`-Feld `string? LastCommittedImportTxId` (additiv; alte `projekt.json`
  laden es als null). Es ist der **Commit-Beweis** im atomaren Save.
- `IImportFileStagingSession` wird um `IReadOnlyList<PublishedFileInfo> PublishedFiles`
  erweitert (RelativePath + Sha256), damit der Marker die veröffentlichten Ziele kennt.
  Die Session sammelt diese Info ohnehin bereits intern in `Publish`.

**Geänderter Commit-Flow** (`ImportRunWorkflowController.RunCoreAsync`):

1. Vor `fileStaging.Publish()` (`:202`): `journal.Begin(marker)` mit frischer `TxId` und
   der Staging-Info. (Restore-Point wurde schon in `:112-113` erstellt.)
2. `fileStaging.Publish()` — Dateien an ihre Ziele (`:202`, unverändert).
3. `targetProject.LastCommittedImportTxId = TxId` setzen; `ReplaceProject(targetProject)`
   (`:236`).
4. `fileStaging.Accept()` (`:238`, unverändert).
5. `projekt.json` atomar speichern — enthält jetzt die `TxId`. **← Commit-Punkt.**
   (Bei `SaveProjectAfterCommit=false` gibt es keinen Save → der `projekt.json` trägt die
   `TxId` nicht → das nächste Laden behandelt die Transaktion als „nicht committed"; siehe
   Recovery.)
6. Der bestehende `finally`-Block (`:275-289`) räumt Staging via `Dispose` auf. Dort wird
   **immer auch der Journal-Marker gelöscht** (`journal.Clear()`): Bei Erfolg ist der neue
   Zustand committed; bei normalem Abbruch/Fehler hat `Dispose` die veröffentlichten
   Dateien bereits zurückgerollt — in beiden Fällen ist der Zustand konsistent und der
   Marker unnötig. **Nur wenn der `finally`-Block durch einen Prozess-Absturz nicht läuft,
   bleibt der Marker stehen und triggert beim nächsten Laden das Recovery.** (Damit ist die
   Marker-Semantik eindeutig: Marker vorhanden ⇔ Prozess starb mitten in der Transaktion.)

**Recovery** — `ImportTransactionRecoveryService` (Infrastructure/Import), aufgerufen beim
Projekt-Laden mit dem Projekt-Root:

- Kein Marker → nichts tun.
- Marker mit `TxId=X` vorhanden → aktuellen `projekt.json` laden, `LastCommittedImportTxId`
  prüfen:
  - **== X** → Commit ging durch (Absturz zwischen Save und Marker-Löschen) → nur
    Staging-Ordner + Marker aufräumen; veröffentlichte Dateien **behalten**. Zustand: neu,
    konsistent.
  - **≠ X (oder null)** → Commit nicht durchgelaufen → **Rollback:** jede Datei aus
    `PublishedTargets` nur dann löschen, wenn ihr aktueller SHA-256 dem Marker-Wert
    entspricht (verhindert das Löschen inzwischen anderweitig geänderter Dateien — gleiche
    Regel wie `ImportFileStagingSession.Dispose`); danach Staging-Ordner + Marker
    aufräumen. `projekt.json` bleibt unangetastet (der alte Stand). Zustand: alt,
    konsistent.
- Ergebnis wird als Rückgabe gemeldet (`ImportRecoveryResult`), damit der Aufrufer einen
  Hinweis anzeigen kann („Unvollständiger Import vom &lt;Zeit&gt; wurde zurückgenommen"
  bzw. „abgeschlossen").

**Verdrahtung Recovery:** Der Aufrufer ist der Projekt-Öffnen-Weg im `ShellViewModel`
(`OpenProject`/Startup-Laden). Der ViewModel ruft nur
`recoveryService.RecoverIfNeeded(projectRoot)` und zeigt den zurückgegebenen Hinweis als
Toast/Status — die gesamte Logik liegt im Infrastructure-Service.

## Wiederverwendete Bausteine (kein Neubau)

- `JsonProjectRepository` / `IProjectRepository` — Serialisierung, atomarer Save mit `.bak`.
- `AtomicTextFileWriter` — atomares Schreiben des Markers.
- `IImportFileStagingSession` (`.import-staging`, SHA-verifizierter Rollback via `Dispose`).
- `ProjectRestorePointStore` — Vorab-Kopie der `projekt.json` (bereits vor dem Import).

## Fehlerbehandlung & Randfälle

- **Marker schreiben schlägt fehl:** Import bricht vor `Publish` ab (kein halber Zustand);
  bestehende Fehlerpfade greifen.
- **`SaveProjectAfterCommit=false`:** Kein Commit-Punkt auf Disk. Bei Absturz bleibt der
  Marker mit `TxId ≠ projekt.json` → Recovery rollt die Dateien zurück. Korrektes
  Alles-oder-nichts (In-Memory-Änderung ist ohnehin weg).
- **Restore-Points deaktiviert:** Recovery funktioniert trotzdem (Rollback der Dateien +
  alter `projekt.json`); der Restore-Point ist nur ein zusätzliches Netz.
- **Zweiter Absturz während Recovery:** Recovery ist idempotent — SHA-Prüfung verhindert
  Doppellöschung, erneuter Lauf setzt sauber fort.
- **Marker eines fremden/alten Projekts:** Marker liegt im Projekt-Root, wird nur für das
  gerade geladene Projekt geprüft.

## Teststrategie

Pro Etappe fokussierte Tests (Infrastructure.Tests, keine echte UI nötig):

1. `JsonProjectContentSignature`: gleiche Daten → gleiche Signatur; Feld-Edit → andere
   Signatur; `ModifiedAtUtc`/`Dirty`/`LastCommittedImportTxId`-Änderung → **gleiche**
   Signatur.
2. Flow: Live-Edit zwischen Snapshot und finalem Check → Import wird abgewiesen,
   `ReplaceProject` NICHT aufgerufen.
3. `FileImportTransactionJournal`: Begin/TryRead/Clear-Roundtrip; atomar geschrieben.
4. Flow: erfolgreicher Import → Marker am Ende weg, `LastCommittedImportTxId` gesetzt.
5. `ImportTransactionRecoveryService`: (a) Marker + alter `projekt.json` → Dateien
   zurückgerollt; (b) Marker + neuer `projekt.json` (TxId==) → Dateien behalten, nur
   aufgeräumt; (c) SHA abweichend → Datei nicht gelöscht; (d) kein Marker → No-op;
   (e) idempotenter zweiter Lauf.
6. Recovery-Verdrahtung: `ShellViewModel` ruft den Service beim Laden und zeigt den
   Hinweis.

## Umsetzung in 6 getesteten Etappen

1. `IProjectContentSignature` + `JsonProjectContentSignature` (+ `SerializerOptions`
   exponieren) + Tests.
2. U4-Konflikterkennung: `ActiveProjectSnapshot.StartSignature` +
   `EnsureProjectIsStillCurrent` finaler Check + Flow-Test.
3. `IImportTransactionJournal` + `FileImportTransactionJournal` + Marker-Modell + Tests.
4. `Project.LastCommittedImportTxId` + Staging-`PublishedFiles` + Flow-Integration
   (Marker Begin/Clear) + Flow-Test.
5. `ImportTransactionRecoveryService` + Recovery-Tests.
6. Recovery-Verdrahtung im `ShellViewModel`-Ladeweg + Nutzer-Hinweis + Test.

Jede Etappe ist ein eigener Commit mit grüner Suite.
