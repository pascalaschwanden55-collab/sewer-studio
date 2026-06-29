# Modul I-b — UI „Review & Freigabe" Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: superpowers:subagent-driven-development oder superpowers:executing-plans. **Voraussetzung: Plan 2a ist umgesetzt** — verfügbare 2a-API (Stand der Umsetzung): `IReviewApprovalService` mit **`ApproveSelfTrainingAsync(sampleId, BoundingBox? box, ct)`** + **`RejectSelfTrainingAsync(sampleId, correctedCode, ct)`** (→ `ReviewApplyResult(Found, Indexed, Deindexed, CorrectedSampleId)`), `SelfTrainingReviewRouting`, `BoundingBox.TryCreate(...)`/`ApplyTo(sample)`, `ProtocolReviewCandidateFilter.SelectCandidates(samples, catalog)`, `ReviewQueueItem.SelfTrainingSampleId`. Im ViewModel bereits vorhanden: `ApproveReviewItemAsync`/`RejectReviewItemAsync` (rufen den Service) sowie die Helfer **`ResolveSelfTrainingSampleIdAsync(item)`** + **`BuildReviewApprovalService()`**. **2b baut auf diesen Methoden auf — die Logik NICHT neu implementieren.**

**Goal:** Aus dem heutigen minimalen „Review Queue"-Tab das tägliche Werkzeug machen: links priorisierte Kandidatenliste (KI-Fehler zuerst), rechts große Karte (Bild · Code · Meter · KI-Aussage · Status), Tastatur-Fluss ✓/✎/✕, optionales Box-Ziehen — **nichts lernt ohne bewusste Freigabe**.

**Architecture:** Reine UI/MVVM auf der 2a-Engine. Keine Geschäftslogik/KB-Orchestrierung im Code-Behind oder VM — Aktionen rufen `IReviewApprovalService`. Testbare Teile (Karten-Projektion) als VM/Projektionsklasse mit Unit-Test; das WPF-Layout per manueller Akzeptanzprüfung.

**Tech Stack:** WPF, CommunityToolkit.Mvvm (`[RelayCommand]`/`[ObservableProperty]`), bestehende `FileToImageConverter`, `Eingabemarker`-Rect-Drag-Muster (`PlayerWindow.Coding.cs:2672-2730`).

**Granularität (User-Vorgabe):** Task-Spec, **kein** vollständiges XAML im Plan — aber jede Task hat **klare Akzeptanzkriterien**. Lehrer-Tab bleibt unangetastet. Sammel-Freigabe nur konservativ (Filter, Anzahl, Warnung, Bestätigung).

**Modul-übergreifende Akzeptanz (gilt für alle Tasks):**

- Liste links, große Karte rechts.
- Voller Tastatur-Fluss (kein Maus-Zwang).
- Bestätigen / Korrigieren / Ablehnen vorhanden und wirksam.
- Optionales Box-Zeichnen (Standard: ohne Box).
- **Kein KB-Schreiben ohne explizite Freigabe.**
- Status nach jeder Aktion sichtbar (Kandidat → geprüft → in KB / abgelehnt).

---

## File Structure

- Create: `src/AuswertungPro.Next.UI/ViewModels/Windows/ReviewCardViewModel.cs` (+ Test in UI.Tests).
- Modify: `src/AuswertungPro.Next.UI/Views/Windows/TrainingCenterWindow.xaml` (Review-Tab 609-653 ersetzen) + `.xaml.cs` (Click-Handler/`InputBox` raus).
- Modify: `src/AuswertungPro.Next.UI/ViewModels/Windows/TrainingCenterViewModel.cs` (Review-Commands, `PendingBox`, Startdaten-Aktion — alle delegieren an 2a-Services).
- Create: Korrektur-Dialog (kleines `Window`/`UserControl`).

---

## Task B1: `ReviewCardViewModel` — Projektion eines Kandidaten auf die Karte

**Files:** Create `ReviewCardViewModel.cs` + `tests/AuswertungPro.Next.UI.Tests/ReviewCardViewModelTests.cs`.

- [ ] Projektion aus `ReviewQueueItem` (tatsächliche Felder: `SelfTrainingFramePath`, `SelfTrainingVsaCode`, `SelfTrainingMeter`, `SelfTrainingMatchLevel`, `SelfTrainingSuggestedCode`, `SelfTrainingSampleId`, `PriorityLabel`): exponiert `FramePath`, `ProtocolCode`, `Meter`, `KiAussage`, `StatusLabel`, `PriorityLabel`, `MatchLevel`, `IsNoFindings`, `IsKiError` (NoFindings|Mismatch).
- [ ] **TDD (testbar):** NoFindings → `KiAussage == "nichts erkannt"` und `IsKiError == true`; Mismatch → zeigt KI-Code vs. Protokoll-Code, `IsKiError == true`; ExactMatch → `IsKiError == false`.
- [ ] **Akzeptanz:** Unit-Test grün; reine Projektion ohne I/O. **Commit.**

## Task B2: Review-Ansicht — links Liste, rechts große Karte

**Files:** `TrainingCenterWindow.xaml` (Review-Tab).

- [ ] Zwei-Spalten-`Grid` (~320 | *). Links `ListBox` `ItemsSource={Binding ReviewQueue}`, `SelectedItem={Binding SelectedReviewItem}`, priorisiert (Service sortiert bereits desc), Item-Template: Code · Meter · Prioritäts-Badge; **KI-Fehler (NoFindings/Mismatch) farblich hervorgehoben**.
- [ ] Rechts Karte: großes `Image` (`FileToImageConverter`, `SelectedReviewItem.FramePath`), darunter Protokoll-Code · Meter · KI-Aussage · Status-Badge; unauffälliger Hinweis „Optional: Box ziehen (nur für YOLO)".
- [ ] Lifecycle-Badges: `Status` / `KbIndexState` / `TrainingEligible` als kleine farbige Marker.
- [ ] **Akzeptanz:** App startet, Review-Tab zeigt Liste links + Karte rechts; bei Auswahl erscheint das Frame-Bild groß; KI-Fehler stehen oben und sind erkennbar markiert. **Commit.**

## Task B3: Aktionen ✓/✎/✕ als MVVM-Commands + Tastatur-Fluss

**Files:** `TrainingCenterWindow.xaml` (+ `.xaml.cs`: Click-Handler entfernen), `TrainingCenterViewModel.cs`.

- [ ] `[RelayCommand]` `ApproveSelectedReview` (✓), `CorrectSelectedReview` (✎), `RejectSelectedReview` (✕), je `CanExecute = SelectedReviewItem != null`. Sie **wrappen die bereits vorhandenen VM-Methoden** `ApproveReviewItemAsync(item, feedback, queueService, ct)` / `RejectReviewItemAsync(item, correctedCode, feedback, queueService, ct)` (aus 2a) — diese lösen die SampleId via `ResolveSelfTrainingSampleIdAsync`, bauen den Service via `BuildReviewApprovalService`, rufen `ApproveSelfTrainingAsync`/`RejectSelfTrainingAsync`, entfernen das Item aus Queue + Liste und setzen den Status. Die Commands **ersetzen die Code-Behind-Click-Handler** (kein Re-Implement der Logik).
- [ ] `KeyBinding`s: `Enter`→Approve, `K`→Correct, `Delete`→Reject; Pfeil hoch/runter = Listen-Navigation. Fokus so, dass die Liste die Pfeiltasten erhält.
- [ ] **`Interaction.InputBox` und die alten `ReviewApprove_Click`/`ReviewReject_Click` entfernen.**
- [ ] **Akzeptanz:** Kompletter Durchlauf nur per Tastatur möglich (Pfeil → Enter/K/Entf); nach jeder Aktion ist der neue Status sichtbar; **ohne Freigabe wird nichts in die KB geschrieben** (Approve schreibt, Reject deindexiert, Navigation schreibt nicht). **Commit.**

## Task B4: Korrektur-Dialog (statt InputBox)

**Files:** kleiner modaler Dialog (`Window`/`UserControl`).

- [ ] Code-Auswahl/Suche aus `ICodeCatalogProvider.AllowedCodes()` (kein Freitext, keine Phantom-Codes). `CorrectSelectedReview` öffnet ihn; gewählter Code → `RejectReviewItemAsync(item, correctedCode, ...)` (ruft intern `RejectSelfTrainingAsync`, erzeugt das `_corr`-Sample, indexiert es — Semantik aus 2a).
- [ ] **Akzeptanz:** Korrigieren öffnet die Auswahl, nur gültige Codes wählbar, Abbruch ändert nichts; nach Bestätigung ist das korrigierte Sample geprüft/indexiert und das Original abgelehnt+deindexiert. **Commit.**

## Task B5: Optionales Box-Zeichnen auf der Karte

**Files:** `TrainingCenterWindow.xaml` (+ Maus-Handler/Behavior), `TrainingCenterViewModel.cs` (`PendingBox`).

- [ ] `Canvas`-Overlay über dem Karten-`Image`; Maus-Drag zeichnet ein Rechteck (Vorschau); MouseUp → Pixel→Norm (über `ActualWidth/Height`, Mindestgröße 0.02) → `BoundingBox.TryCreate` (2a) → `PendingBox`. `Esc`/„Box löschen" setzt `PendingBox=null`. Bei Auswahlwechsel `PendingBox` zurücksetzen.
- [ ] `ApproveSelectedReview` übergibt `PendingBox` an die Freigabe. Dazu `ApproveReviewItemAsync` um einen optionalen Parameter `BoundingBox? box = null` erweitern, der an `ApproveSelfTrainingAsync(sampleId, box, ct)` durchgereicht wird (heute ruft es `box: null`).
- [ ] **Akzeptanz:** Mit gezogener Box → freigegebenes Sample hat `HasBbox==true` (YOLO-fähig); **ohne** Box → Few-Shot-Freigabe ohne Box (kein Dummy); gezogene Box bleibt nach Freigabe persistent (2a Task 3). **Commit.**

## Task B6: Protokoll-Startdaten vorschlagen + konservative Sammel-Freigabe (Spec §6)

**Files:** `TrainingCenterViewModel.cs` (+ Knopf in `TrainingCenterWindow.xaml`).

- [ ] `[RelayCommand] SuggestProtocolStartdata`: nimmt geprüfte Protokoll-Samples (`Status=New`), filtert über `ProtocolReviewCandidateFilter` (2a Task 5), `EnqueueFromSelfTraining(..., matchLevel:"…", sampleId: s.SampleId)` als Quelle „ProtocolStartdata" (Priorität **unter** KI-Fehlern), lädt die Queue neu.
- [ ] **Sammel-Freigabe konservativ:** separate, bewusste Aktion mit **Filter (pro Projekt/Code)**, **Anzahl-Anzeige**, **Warnhinweis** und **expliziter Bestätigung** — kein „alles auf einmal" still. Jede Freigabe läuft über `ApproveReviewItemAsync`/`ApproveSelfTrainingAsync` (kein Direkt-KB-Schreiben).
- [ ] **Akzeptanz:** Startdaten erscheinen als Kandidaten **unter** den KI-Fehlern; Sammel-Freigabe zeigt vorab Anzahl + Warnung + Bestätigung und verlangt einen Filter; ohne Bestätigung wird nichts gelernt. **Commit.**

---

## Abschluss Plan 2b

- [ ] **Voller Testlauf:** `dotnet test AuswertungPro.sln` — grün, 0 Skips.
- [ ] **Manueller Akzeptanz-Durchlauf** gegen die modul-übergreifende Akzeptanzliste (Liste/Karte/Tastatur/✓✎✕/optionale Box/kein-KB-ohne-Freigabe/Status sichtbar).
- [ ] **Final-Review** (superpowers:requesting-code-review) der gesamten Modul-I-Implementierung (2a + 2b).

**Danach:** Plan 3 (Modul ② „Trainingsdaten & Modell": Samples-Verwaltung/Dubletten, YOLO-Export-UI, **Benchmark vorher/nachher**, Experten-Batch-Import).

## Nicht-Ziele (Scope-Grenzen)

- **Lehrer-Tab bleibt unangetastet** (eigenes Thema).
- Kein Massen-/Auto-Lernen ohne bewussten Freigabe-Schritt.
- Kein KB-/Engine-Umbau über das in 2a Gebaute hinaus.
