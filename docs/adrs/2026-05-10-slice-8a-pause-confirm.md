# Slice 8a Pause-Confirm-Workflow — Mini-ADR

Datum: 2026-05-10
Status: **Entwurf** — wartet auf User-Review, kein Code bevor freigegeben
Vorgeschichte:
- Konsolidierungs-ADR: `2026-05-09-slice-8a-coding-mode-konsolidierung.md` (Option B.1)
- Audit-Diff: `2026-05-09-slice-8a-1-audit-diff.md` (Pause-Confirm dort als kritisch markiert)
- Stop-Liste: `2026-05-09-slice-8a-2-stop-list-adr.md`
- Mini-ADR Frame-Readiness: `2026-05-09-slice-8a-3-mini-adr-frame-readiness.md` (Schritt 6 = "Pause-Confirm-Workflow als separater Sub-Slice")
- Slice 8a.3 in 16 Commits abgeschlossen — der alte In-Place-Coding-Pfad
  (PlayerWindow.CodingMode.cs + Partials) ist seit 5b geloescht. Das schliesst
  auch den **alten** Pause-Confirm-Workflow ein.

## Was diese ADR macht

Sie beantwortet die fuenf Designfragen fuer die Re-Implementierung des
Pause-Confirm-Workflows im neuen `CodingModeWindow`-Pfad und legt einen
Step-Plan fest. Code kommt erst nach User-Freigabe.

## Was diese ADR NICHT macht

Sie ist keine Auto-BCD/BCE-Migration und kein Auto-Kalibrierungs-Slice.
Diese stehen separat im Audit-Diff und werden bei Bedarf eigene ADRs
bekommen.

## Bestandsaufnahme

### A) Was der alte Workflow im PlayerWindow gemacht hat (vor 5b geloescht)

- **Trigger:** Im Live-Coding-Loop wurde nach `AddAiFindingsAsEvents`
  geprueft: wenn ein Finding im Yellow- oder Red-Bereich des
  QualityGate war (`firstUnsure != null && firstUnsureGate != null`),
  rief der Loop `PauseAndAskConfirmation(event, gate)`.
- **PauseAndAskConfirmation:**
  1. `_player.SetPause(true)`
  2. `_codingSessionService.SetWaitingForInput()`
  3. State zwischenspeichern: `_codingPendingConfirmEvent` +
     `_codingPendingGateResult`
  4. UI: `CodingConfirmationPanel.Visibility = Visible`, Ampel-Farbe
     (gelb/rot), Code-Text, Konfidenz, Beschreibung, Detail-Hinweis
- **Drei User-Aktionen:**
  - `ConfirmAccept_Click` → Decision=Accepted,
    `PersistSingleEventAsTrainingSample`, panel zu, video weiter
  - `ConfirmEdit_Click` → Decision=AcceptedWithEdit, panel zu, Event
    selektieren (User editiert in der Liste), video weiter
  - `ConfirmReject_Click` → Sperrliste-Entry adden
    (`_rejectedFindings.Add(MakeRejectionKey(...))`),
    Decision=Rejected, Event aus Session+VM entfernen, panel zu,
    video weiter
- **Resume:** `ResumeAfterPause` mit 2s-Cooldown gegen sofortiges
  Re-Triggern. `ResumeAfterConfirmation` schaltet Player + Session
  zurueck auf Running.
- **State:** `_rejectedFindings` (HashSet&lt;string&gt;) blieb ueber
  die ganze Session und blockierte spaetere Re-Erkennung des gleichen
  Findings.

### B) Was im CodingModeWindow heute fehlt

- Kein Pause-Confirm-Panel, kein Trigger im Loop
  (`CodingModeWindow.LiveLoop.cs` bekommt das Quality-Gate-Resultat aus
  `EnhancedVisionAnalysisService`, nutzt es aber heute nicht zum
  Pausieren).
- Kein `_rejectedFindings`-State.
- Kein WaitingForInput-Pfad.

### C) Was bereits da ist (Wiederverwendung)

- `CodingSessionViewModel` ist seit Slice 8a.2.9 der Single-Source
  fuer Session-State (Punkt-4-Pattern).
- `ICodingSessionService.SetWaitingForInput()` existiert noch
  (steckte schon im alten Pfad, wird aber aktuell von keinem
  Caller benutzt). API ist bereit.
- `_player` (LibVLC MediaPlayer) im CodingModeWindow vorhanden,
  Pause/Resume per `SetPause(true/false)`.
- `CodingUserDecision`-Enum (Accepted/AcceptedWithEdit/Rejected/...)
  ist Domain-Model und verfuegbar.
- `QualityGateResult` mit `IsGreen/IsYellow/IsRed` und
  `CompositeConfidence` existiert in der Application-Schicht.

## Die fuenf Designfragen

### Q1 — Wer entscheidet "Pause oder nicht"?

**Aktuell-Vermutung:** Der Loop sieht das `QualityGateResult` und
entscheidet inline.

**Optionen:**
- **A) Loop entscheidet, ruft VM-Methode `BeginConfirmation(event, gate)`.**
  VM speichert pending-state, feuert `ConfirmationRequested`-Event,
  Window bindet darauf und zeigt Panel.
- **B) VM bekommt jeden Frame, entscheidet selbst.** Loop ruft
  `_vm.RecordFinding(...)`, VM macht intern Pause-Logik via Player-
  Reference.
- **C) Reiner Service-Ansatz: `IPauseConfirmationCoordinator`.**
  Eigener kleiner Service, der zwischen Loop + UI vermittelt. Owns
  pending-state.

**Empfehlung: A.** Loop kennt den Gate-Threshold (Green=Auto-Accept,
Yellow/Red=Pause-Confirm); VM ist der Owner des State; Window ist der
UI-Renderer. Sauberes Punkt-4-Pattern, kein neuer Service-Layer.

### Q2 — Wie wartet der Loop auf die User-Entscheidung?

**Optionen:**
- **A) `Task<CodingUserDecision>`-API auf VM.**
  `await _vm.AwaitUserDecisionAsync(event, gate, ct)` → Loop blockiert
  bis Window Accept/Edit/Reject feuert. VM speichert intern eine
  `TaskCompletionSource<CodingUserDecision>`.
- **B) Event-driven: Loop kehrt sofort zurueck, Window haelt
  Player-Pause + ruft Loop-Resume ueber Continuation.** Loop muss
  re-entrant gemacht werden.
- **C) Polling im Loop: solange `_vm.IsWaitingForUserInput`, schlafen.**
  Schmierig, kein klares Cancel.

**Empfehlung: A.** TaskCompletionSource ist der idiomatische Pfad,
laesst sich sauber per CancellationToken cancelen (Loop-CTS aus
Mini-ADR-Q3 schiesst die TCS ab), und die Single-Frame-Variante
(oneShot=true) bekommt die Entscheidung wie jede andere Iteration.

### Q3 — Wo lebt der pending-state und die Sperrliste?

**Optionen:**
- **A) Beides ins VM** (analog Frame-Readiness in 8a.3 Step 2a).
  `_pendingConfirmation : (CodingEvent ev, QualityGateResult gate, TaskCompletionSource&lt;CodingUserDecision&gt; tcs)?`
  + `RejectedFindings : HashSet&lt;string&gt;` als VM-Feld.
- **B) Pending-state ins VM, Sperrliste in einen `ICodingRejectionStore`-
  Service.** Mehr Architektur, weniger VM-Wachstum.
- **C) Beides in einen neuen `IPauseConfirmationCoordinator` Service.**
  VM bleibt schlank.

**Empfehlung: A.** Punkt-4-Pattern beibehalten. Sperrliste ist
session-life-cycle gebunden (genau wie das VM), kein Bedarf fuer einen
eigenen Service.

### Q4 — UI-Render-Strategie

**Optionen:**
- **A) Panel im `CodingModeWindow.xaml` (Inline-Overlay).** Wie
  PlayerWindow.xaml frueher: ein `Border x:Name="ConfirmationPanel"`
  ueber dem Video, mit Code-Text, Ampel, drei Buttons. Sichtbarkeit
  per `IsVisible`-Binding ans VM.
- **B) Eigenes `ConfirmationDialog`-Window.** Sauber gekapselt,
  ShowDialog() blockiert. Aber UX-bruch: Dialog vor dem Video ist
  unhandlich, Player ist dahinter pausiert.
- **C) Existing Pattern wiederverwenden.** Es gibt schon
  `ShowBboxResultPanel` in CodingModeWindow.BboxResultPanel.cs mit
  einem dynamisch konstruierten Panel. Aber der ist NICHT modal
  (Mauseingaben gehen weiter ans Video) und hat keine Buttons.

**Empfehlung: A.** Inline-Overlay ist UX-konsistent zur alten Erfahrung
und einfacher zu binden. Visibility-Property auf VM, Buttons binden
auf `_vm.AcceptConfirmation`/`EditConfirmation`/`RejectConfirmation`-
Methoden, die intern die TaskCompletionSource setzen.

### Q5 — Was passiert bei der Edit-Aktion?

**Aktuell-Vermutung (alt):** Edit selektierte das Event in
`LstCodingEvents`, erwartete dass der User ueber das DefectDetailPanel
manuell editiert. Das ist eine Folge-Interaktion ausserhalb der
TCS-Wartezeit.

**Optionen:**
- **A) Edit selektiert Event in `_vm.SelectedDefect`, completed TCS
  mit `AcceptedWithEdit`, schliesst Panel.** Nutzer editiert den Code
  ueber die normale Liste (DefectDetailPanel in CodingModeWindow).
  Loop laeuft weiter.
- **B) Edit oeffnet sofort `VsaCodeExplorerWindow` per ShowDialog,
  setzt das Code-Feld, dann TCS=AcceptedWithEdit.**
- **C) Edit erweitert das Inline-Panel um TextBox/ComboBox.**

**Empfehlung: A.** Minimaler UX-Eingriff. Wenn das CodingModeWindow-
DefectDetailPanel die Edit-Erfahrung schon abdeckt, gibt es keinen
Grund einen zweiten Edit-Pfad zu bauen. Wenn der User sieht "ich
muesste hier eigentlich gleich editieren koennen", waere Option C
ein Folge-Slice.

## Resultierender Migrations-Schnitt (kein Code, nur Liste)

In dieser Reihenfolge (alle einzelne Commits, jeweils Build+Test gate):

1. **VM-API erweitern:** auf `CodingSessionViewModel`:
   - `IReadOnlyCollection&lt;string&gt; RejectedFindings` (read-only Public)
   - `bool IsAwaitingUserDecision` (ObservableProperty fuer XAML-Binding)
   - `Task&lt;CodingUserDecision&gt; BeginConfirmationAsync(CodingEvent ev, QualityGateResult gate, CancellationToken ct)`
   - `void CompleteConfirmation(CodingUserDecision decision)`  (vom UI gerufen)
   - `void AddRejection(string code, double meter)`
   - Plus interne `_pendingConfirmation`-Tuple und HashSet.
   - **Tests:** state-machine-Tests fuer "kein pending → BeginAsync setzt
     pending → Complete schliesst → Result kommt zurueck", "Cancel
     wirft OperationCanceledException", "Sperrliste-MakeKey/Add/Contains".

2. **CodingModeWindow.xaml-Panel:** neuer `Border x:Name="ConfirmationPanel"`
   ueber `OverlayCanvas` mit Visibility binding `_vm.IsAwaitingUserDecision`.
   Ampel-Ellipse, Code-Text, Konfidenz, Beschreibung, drei Buttons
   (`Click="ConfirmAccept_Click"` etc. — die Click-Handler liegen in
   einer neuen `CodingModeWindow.PauseConfirm.cs`-Partial).

3. **CodingModeWindow.PauseConfirm.cs:** Click-Handler die
   `_vm.CompleteConfirmation(...)` rufen, plus auf Edit-Click den
   `_vm.SelectedDefect`-Setzer.

4. **Loop-Integration:** `RunLiveAnalysisAsync` in
   `CodingModeWindow.LiveLoop.cs` bekommt nach jedem analyzed Frame
   das `QualityGateResult`-Tuple aus `EnhancedVisionAnalysisService`
   (das schon zurueckkommt). Wenn nicht-green → erstes Finding +
   gate-Result an `_vm.BeginConfirmationAsync` uebergeben, await.
   Decision-Branches:
   - Accepted/AcceptedWithEdit → Loop continues
   - Rejected → Sperrliste-Entry, Event aus VM entfernen, continue.

5. **Sperrliste in Filter:** im Loop oder in
   `EnhancedVisionAnalysisService.AnalyzeWithEscalationAsync`-Caller:
   findings deren `(code, meter)` in `_vm.RejectedFindings` ist,
   werden vor Hinzufuegen verworfen.

6. **UI-Smoke** (User-Aufgabe): Coding-Modus, gemeldetes Yellow-Finding
   triggert Pause + Panel, Accept/Edit/Reject werden visuell richtig
   verarbeitet, Player resumed sauber, Sperrliste verhindert
   Re-Erkennung des gleichen Findings.

## Was diese ADR explizit ausklammert

- **Auto-BCD/BCE/Streckenschaden** (audit-diff "Auto-BCD/BCE", separater Slice).
- **Auto-Kalibrierung** (audit-diff "TryAutoCalibrationFromCurrentFrame", separater Slice).
- **OperateurAnnotation UI-Smoke** (Memory-TODO, kein Pause-Confirm-Bezug).
- **Vollintegration der YOLO-first/SAM/Multi-Model-Eskalation**, die im PlayerWindow vor 5b
  Findings produzierte. Heute liefert `EnhancedVisionAnalysisService` einfaches Qwen — fuer
  den Pause-Confirm-Workflow reicht das, weitere Pipeline-Stufen sind ein eigenes Thema.

## Offene Punkte fuer Dich (Reviewer)

1. Stimmst Du den Empfehlungen Q1=A, Q2=A, Q3=A, Q4=A, Q5=A zu?
2. Reicht der 6-Schritt-Schnitt oder ist Schritt 1 (VM-API) zu gross
   und braucht Sub-Aufteilung (z.B. Sperrliste vs. ConfirmationFlow
   getrennt)?
3. UI-Smoke nach Schritt 5 reicht, oder willst Du Zwischen-Smoke nach
   Schritt 4 (Loop ohne Sperrliste, nur Pause-Confirm)?
4. Soll die Sperrliste persistent sein (z.B. in der CodingSession
   serialisiert) oder bewusst nur in-memory pro Session?
