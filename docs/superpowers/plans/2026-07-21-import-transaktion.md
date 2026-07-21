# Absturzsichere Import-Transaktion — Implementierungsplan

> **For agentic workers:** REQUIRED SUB-SKILL: superpowers:executing-plans (inline) oder
> subagent-driven-development. Schritte nutzen Checkbox-Syntax (`- [ ]`).

**Goal:** Der manuelle Import verliert keine Live-Edits mehr (U4) und überlebt einen
Prozess-/Stromausfall alles-oder-nichts (kein halber Zustand).

**Architecture:** Zwei Bausteine — (1) JSON-Content-Signatur erkennt Live-Edits und weist
den Import ab; (2) Journal-Marker im Projekt-Root + TxId-Commit-Beweis im `projekt.json` +
Recovery beim Projekt-Laden rollt eine unterbrochene Transaktion zurück. Verträge in
Application, Impls in Infrastructure, Verdrahtung im `ImportRunWorkflowController` und im
`ShellViewModel`-Ladeweg. Keine Transaktionslogik im ViewModel.

**Tech Stack:** C#/.NET 10, System.Text.Json, xUnit (Infrastructure.Tests), SHA-256.

## Global Constraints

- Verträge (Interfaces/DTOs) in `AuswertungPro.Next.Application`, Implementierungen in
  `AuswertungPro.Next.Infrastructure`, Tests in `AuswertungPro.Next.Infrastructure.Tests`.
- Kommentare deutsch. Keine neuen NuGet-Pakete.
- `dotnet build AuswertungPro.sln` + betroffene Test-Suite grün vor jedem Commit.
- Commit-Trailer: `Co-Authored-By: Claude Fable 5 <noreply@anthropic.com>`.
- Spec: `docs/superpowers/specs/2026-07-21-import-transaktion-design.md`.

---

### Task 1: Projekt-Content-Signatur

**Files:**
- Create: `src/AuswertungPro.Next.Application/Projects/IProjectContentSignature.cs`
- Create: `src/AuswertungPro.Next.Infrastructure/Projects/JsonProjectContentSignature.cs`
- Modify: `src/AuswertungPro.Next.Infrastructure/Projects/JsonProjectRepository.cs:12-16`
  (`private static readonly ... Opt` → `public static readonly ... SerializerOptions`;
  alle internen `Opt`-Nutzungen anpassen)
- Test: `tests/AuswertungPro.Next.Infrastructure.Tests/Projects/JsonProjectContentSignatureTests.cs`

**Interfaces:**
- Produces: `IProjectContentSignature.Compute(Project project) : string` (Hex-SHA256).

**Vorgehen:** `JsonProjectContentSignature` serialisiert das Projekt mit
`JsonProjectRepository.SerializerOptions`, ersetzt aber vor dem Hash die instabilen
Meta-Felder auf feste Werte, damit sie die Signatur nicht beeinflussen: Es klont das
Projekt NICHT, sondern serialisiert und ersetzt die Meta-Felder im JSON-Objektbaum
(`ModifiedAtUtc`, `Dirty`, `LastCommittedImportTxId`) durch Konstanten, dann SHA256 über
den normalisierten JSON-String.

- [ ] **Step 1: Test schreiben** — `JsonProjectContentSignatureTests`: (a) zwei inhaltlich
  gleiche Projekte → gleiche Signatur; (b) ein geänderter Feldwert (`SetFieldValue` an einem
  Record) → andere Signatur; (c) nur `ModifiedAtUtc`/`Dirty`/`LastCommittedImportTxId`
  geändert → **gleiche** Signatur.
- [ ] **Step 2: Test läuft rot** (`dotnet test ... --filter JsonProjectContentSignatureTests`).
- [ ] **Step 3:** `Opt` → `public static readonly JsonSerializerOptions SerializerOptions`
  umbenennen; `IProjectContentSignature` + `JsonProjectContentSignature` implementieren
  (JSON parsen via `JsonNode`, Meta-Felder normalisieren, SHA256).
- [ ] **Step 4: Test grün.**
- [ ] **Step 5: Commit** `feat(import): Projekt-Content-Signatur (U4-Grundlage)`.

---

### Task 2: U4-Konflikterkennung im Import-Flow

**Files:**
- Modify: `src/AuswertungPro.Next.UI/Services/ImportRunWorkflowController.cs`
  (`ActiveProjectSnapshot`-Record `:435` um `string StartSignature`; `RunAsync` `:53-55`
  berechnet Signatur; `EnsureProjectIsStillCurrent` `:356-384` bekommt Parameter
  `checkContentSignature`; nur der finale Aufruf `:229` übergibt `true`; die Actions
  `:17-41` erhalten `Func<Project,string> ComputeSignature`)
- Modify: `src/AuswertungPro.Next.UI/ViewModels/Pages/ImportPageViewModel.cs:186ff`
  (Actions um `ComputeSignature: p => _sp.ProjectContentSignature.Compute(p)` ergänzen)
- Modify: `src/AuswertungPro.Next.UI/ServiceProvider.cs` (Registrierung
  `ProjectContentSignature = new JsonProjectContentSignature()` + Property)
- Test: `tests/AuswertungPro.Next.UI.Tests/ImportRunWorkflowControllerConflictTests.cs`

**Interfaces:**
- Consumes: `IProjectContentSignature` (Task 1).

- [ ] **Step 1: Test schreiben** — Flow-Test mit Fake-Actions: Snapshot-Signatur = "A";
  beim finalen Check liefert `GetProject()` ein Projekt mit Signatur "B" → `ReplaceProject`
  wird NICHT aufgerufen, Summary enthält „nicht übernommen".
- [ ] **Step 2: Test rot.**
- [ ] **Step 3:** Implementierung: `StartSignature` im Snapshot, finaler
  `EnsureProjectIsStillCurrent`-Aufruf vergleicht `ComputeSignature(GetProject())` mit
  `StartSignature`; bei Abweichung dieselbe Abweisungs-Logik wie Projektwechsel (neue
  Detailmeldung „Während des Imports wurde das Projekt bearbeitet …").
- [ ] **Step 4:** Bestehende `ImportRunWorkflowController`-Tests + neuer Test grün.
- [ ] **Step 5: Commit** `fix(import): U4 — Live-Edit während Import weist Ergebnis ab`.

---

### Task 3: Import-Transaktions-Journal (Marker)

**Files:**
- Create: `src/AuswertungPro.Next.Application/Import/IImportTransactionJournal.cs`
  (record `ImportTransactionMarker(string TxId, DateTime StartedUtc, string Label,
  string StagingRoot, IReadOnlyList<PublishedFileInfo> PublishedTargets,
  string? RestorePointPath)`; record `PublishedFileInfo(string RelativePath, string Sha256)`;
  interface `IImportTransactionJournal { void Begin(string projectRoot, ImportTransactionMarker m);
  ImportTransactionMarker? TryRead(string projectRoot); void Clear(string projectRoot); }`)
- Create: `src/AuswertungPro.Next.Infrastructure/Import/FileImportTransactionJournal.cs`
  (`.import-transaction.json` im projectRoot, `AtomicTextFileWriter` + System.Text.Json)
- Test: `tests/AuswertungPro.Next.Infrastructure.Tests/Import/FileImportTransactionJournalTests.cs`

**Interfaces:**
- Produces: `IImportTransactionJournal`, `ImportTransactionMarker`, `PublishedFileInfo`.

- [ ] **Step 1: Test schreiben** — Begin→TryRead liefert denselben Marker (alle Felder);
  Clear→TryRead liefert null; TryRead ohne Datei → null; kaputtes JSON → null (kein Wurf).
- [ ] **Step 2: Test rot.**
- [ ] **Step 3:** Vertrag + `FileImportTransactionJournal` (Serialisieren via
  `AtomicTextFileWriter.WriteAllText`, Pfad `Path.Combine(projectRoot, ".import-transaction.json")`).
- [ ] **Step 4: Test grün.**
- [ ] **Step 5: Commit** `feat(import): Transaktions-Journal-Marker`.

---

### Task 4: Project-Feld + Staging-PublishedFiles + Flow-Integration

**Files:**
- Modify: `src/AuswertungPro.Next.Domain/Models/Project.cs` (Feld
  `public string? LastCommittedImportTxId { get; set; }`)
- Modify: `src/AuswertungPro.Next.Application/Import/IImportFileStagingService.cs:18-31`
  (Session um `IReadOnlyList<PublishedFileInfo> PublishedFiles { get; }`)
- Modify: `src/AuswertungPro.Next.Infrastructure/Import/ImportFileStagingSession.cs`
  (in `Publish` je Datei RelativePath+Sha256 sammeln; Property zurückgeben)
- Modify: `src/AuswertungPro.Next.UI/Services/ImportRunWorkflowController.cs`
  (Actions um `IImportTransactionJournal Journal` + `Func<string?> GetProjectRoot`;
  vor `Publish()` `:202` `Journal.Begin(...)` mit frischer `TxId`; `targetProject
  .LastCommittedImportTxId = txId` vor `ReplaceProject` `:236`; im `finally` `:275`
  `Journal.Clear(projectRoot)`)
- Modify: `ImportPageViewModel.cs` (Actions verdrahten: `Journal: _sp.ImportTransactionJournal`,
  `GetProjectRoot`)
- Test: `tests/AuswertungPro.Next.UI.Tests/ImportRunWorkflowControllerJournalTests.cs`

**Interfaces:**
- Consumes: `IImportTransactionJournal`, `ImportTransactionMarker`, `PublishedFileInfo` (Task 3).
- Produces: `Project.LastCommittedImportTxId`, `IImportFileStagingSession.PublishedFiles`.

- [ ] **Step 1: Test schreiben** — erfolgreicher Flow: `Journal.Begin` wurde gerufen,
  am Ende ist der Marker gelöscht (`TryRead==null`), `targetProject.LastCommittedImportTxId`
  == der TxId aus dem Marker.
- [ ] **Step 2: Test rot.**
- [ ] **Step 3:** Feld + Staging-Property + Flow-Integration.
- [ ] **Step 4:** Neuer Test + bestehende Import-/Staging-Tests grün.
- [ ] **Step 5: Commit** `feat(import): Transaktion — Marker im Flow + Commit-Beweis`.

---

### Task 5: Recovery-Service

**Files:**
- Create: `src/AuswertungPro.Next.Application/Import/IImportTransactionRecoveryService.cs`
  (record `ImportRecoveryResult(ImportRecoveryOutcome Outcome, string? Message)`;
  enum `ImportRecoveryOutcome { None, RolledBack, CompletedCleanup }`;
  interface `RecoverIfNeeded(string projectRoot) : ImportRecoveryResult`)
- Create: `src/AuswertungPro.Next.Infrastructure/Import/ImportTransactionRecoveryService.cs`
  (nutzt `IImportTransactionJournal` + `IProjectRepository` zum Laden des `projekt.json` und
  Lesen von `LastCommittedImportTxId`; SHA-256-Prüfung pro Datei wie Staging.Dispose)
- Test: `tests/AuswertungPro.Next.Infrastructure.Tests/Import/ImportTransactionRecoveryServiceTests.cs`

**Interfaces:**
- Consumes: `IImportTransactionJournal`, `IProjectRepository`.
- Produces: `IImportTransactionRecoveryService`.

**Entscheidungsmatrix** (Marker mit `TxId=X`):
- `projekt.json.LastCommittedImportTxId == X` → `CompletedCleanup`: nur Staging-Ordner +
  Marker löschen, Dateien behalten.
- `!= X` (oder null) → `RolledBack`: jede `PublishedTargets`-Datei nur löschen, wenn ihr
  aktueller SHA256 == Marker-Wert; danach Staging + Marker löschen.
- kein Marker → `None`.

- [ ] **Step 1: Tests schreiben** (Temp-Projektordner): (a) Marker + `projekt.json` mit
  passender TxId → `CompletedCleanup`, Datei bleibt, Marker weg; (b) Marker + alte TxId →
  `RolledBack`, Datei mit passendem SHA gelöscht, Marker weg; (c) Datei-SHA abweichend →
  Datei bleibt; (d) kein Marker → `None`; (e) zweiter Lauf nach RolledBack → `None`
  (idempotent).
- [ ] **Step 2: Tests rot.**
- [ ] **Step 3:** Service implementieren.
- [ ] **Step 4: Tests grün.**
- [ ] **Step 5: Commit** `feat(import): Transaktions-Recovery-Service`.

---

### Task 6: Recovery-Verdrahtung beim Projekt-Laden

**Files:**
- Modify: `src/AuswertungPro.Next.UI/ServiceProvider.cs` (Registrierung
  `ImportTransactionJournal`, `ImportTransactionRecovery` + Properties)
- Modify: `src/AuswertungPro.Next.UI/ViewModels/ShellViewModel.cs` (im Projekt-Öffnen-/
  Startup-Ladeweg nach erfolgreichem `Load`: `var r = _sp.ImportTransactionRecovery
  .RecoverIfNeeded(projectRoot); if (r.Outcome != None) SetStatus/Toast(r.Message);`)
- Test: `tests/AuswertungPro.Next.UI.Tests/ShellViewModelImportRecoveryTests.cs`
  (oder Erweiterung eines bestehenden ShellViewModel-Tests; mit Fake-Recovery-Service prüfen,
  dass er beim Laden mit dem richtigen projectRoot gerufen wird und die Meldung gesetzt wird)

**Interfaces:**
- Consumes: `IImportTransactionRecoveryService` (Task 5).

- [ ] **Step 1: Test schreiben** — Fake-Recovery liefert `RolledBack` + Message; nach dem
  Projekt-Laden ist die Status-/Toast-Meldung gesetzt und der projectRoot stimmt.
- [ ] **Step 2: Test rot.**
- [ ] **Step 3:** Verdrahtung im Ladeweg.
- [ ] **Step 4:** Test + volle UI.Tests grün.
- [ ] **Step 5: Commit** `feat(import): Recovery beim Projekt-Laden + Nutzer-Hinweis`.

---

## Self-Review

- **Spec-Abdeckung:** U4 (Task 1-2), Journal/Marker (Task 3-4), Commit-Beweisfeld (Task 4),
  Recovery-Matrix (Task 5), Laden-Verdrahtung+Hinweis (Task 6), Wiederverwendung
  AtomicTextFileWriter/Staging/JsonProjectRepository (Task 1,3,5). Alle Spec-Punkte gedeckt.
- **Typkonsistenz:** `ImportTransactionMarker`, `PublishedFileInfo`, `IProjectContentSignature
  .Compute`, `Project.LastCommittedImportTxId`, `IImportFileStagingSession.PublishedFiles`,
  `ImportRecoveryResult/Outcome` durchgängig gleich benannt.
- **Reihenfolge-Abhängigkeiten:** Task 2 braucht 1; Task 4 braucht 3; Task 5 braucht 3;
  Task 6 braucht 5. Linear ausführbar.
