# Slice 8a Pause-Confirm-Workflow — Mini-ADR

Datum: 2026-05-10
Status: **Done** (UI-Smokes 1+2 durch, Slice in 8 Commits abgeschlossen)

Geliefert:
- Step 1a `17cb341` — VM ConfirmationFlow + 12 Tests
- Step 1b `9bd5910` — VM Sperrliste + 15 Tests
- Step 2  `9e6697f` — XAML Confirm-Panel + Click-Stubs
- Step 3  `c162ccf` — Click-Handler-Bodies
- Step 4  `c006ff3` — LiveLoop Pause-Confirm-Gate
- Step 4-fix `d298718` — Edit-Affordance-Sync (LstEvents.SelectedItem +
  ScrollIntoView + UpdateDefectDetailPanel nach AddEventInOrder)
- Step 5  `521a104` — Sperrliste-Filter im Loop + AddRejection
- Step 5-fix `a80c01c` — drei Schaerfungen am AI-Bucket: Label-
  Disambiguator im Schluessel, Per-Code-Tolerance (AI 0.1m / sonst 0.5m),
  AI-Findings nicht in Sperrliste persistieren

Tests-Bilanz: +27 neue Faelle (12 ConfirmationFlow + 15→20
Sperrliste). Pipeline 800 → 832, Gesamt 1017 PASS / 1 SKIP / 0 FAIL.

User-Review-Entscheidungen 2026-05-10:
- Q1–Q5: zugestimmt
- Step 1 in 1a/1b geteilt (ConfirmationFlow vs. Sperrliste)
- Gate-Quelle korrigiert: kein QualityGateResult vorhanden, lokale
  Severity-Policy fuer diesen Slice
- UI-Smoke nach Step 4 + final nach Step 5
- Sperrliste in-memory only, keine Persistenz
- Edit-Affordance: Loop synchronisiert LstEvents + DefectDetailPanel
  nach AddEventInOrder; modaler Editor oeffnet nicht automatisch
- AI-Bucket: drei Fixes layered (Label-Key, 0.1m-Toleranz, Skip)
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
  `CompositeConfidence` existiert in der Application-Schicht — wird
  aber im aktuellen `RunLiveAnalysisAsync`-Pfad **NICHT** erzeugt
  (siehe naechster Abschnitt "Gate-Quelle"). Ein voller QualityGate-
  Service-Umbau ist in diesem Slice ausgeklammert.

### D) Gate-Quelle: was der Loop heute weiss (User-Korrektur 2026-05-10)

`EnhancedVisionAnalysisService.AnalyzeWithEscalationAsync` liefert
`(EnhancedFrameAnalysis Result, bool Escalated)`. Aus den Findings im
`LiveDetectionMapper.FromEnhancedAnalysis(...)`-Resultat steht pro
Finding eine `Severity` (int, 1–5). Ein vollstaendiger
`QualityGateResult` mit `CompositeConfidence` wird im Loop heute
**nicht** berechnet.

Fuer diesen Slice nutzen wir eine **lokale Gate-Policy** aequivalent
zur UI-Severity-Heuristik:

```
confidence = Severity * 0.20  (Sev 1 → 20%, Sev 5 → 100%)

confidence >= 0.85  → Green   (Auto-Accept, kein Pause)
confidence >= 0.60  → Yellow  (Pause + Confirm)
confidence <  0.60  → Red     (Pause + Confirm)
```

Diese Policy lebt als private Helper-Methode in der neuen
`CodingModeWindow.PauseConfirm.cs`-Partial (oder im Loop selbst).
Wenn ein Folge-Slice spaeter `EnhancedVisionAnalysisService` um eine
echte `QualityGateResult`-Ausgabe erweitert, wird die Helper-Methode
durch den Service-Call ersetzt — **ohne Schnittstellen-Aenderung am VM**.

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

In dieser Reihenfolge (alle einzelne Commits, jeweils Build+Test gate).
Step 1 ist auf 1a/1b geteilt (User-Korrektur 2026-05-10).

### Step 1a: VM-API ConfirmationFlow + Tests

Auf `CodingSessionViewModel`:
- `bool IsAwaitingUserDecision` (ObservableProperty fuer XAML-Binding)
- `CodingEvent? PendingConfirmationEvent` (read-only)
- `double? PendingConfirmationConfidence` (read-only, 0..1)
- `bool PendingConfirmationIsRed` (read-only Bool, fuer Ampel-Faerbung)
- `Task<CodingUserDecision> BeginConfirmationAsync(CodingEvent ev, double confidence, bool isRed, CancellationToken ct)`
- `void CompleteConfirmation(CodingUserDecision decision)` (vom UI gerufen)
- Interner `TaskCompletionSource<CodingUserDecision>` plus pending-state-Tuple.

**Tests:** "kein pending → BeginAsync setzt pending → CompleteConfirmation
schliesst → Result kommt zurueck", "Cancel wirft OperationCanceledException",
"BeginAsync waehrend bereits pending → InvalidOperationException oder
documented behavior", "CompleteConfirmation ohne pending → No-Op oder
documented behavior".

### Step 1b: VM-API Sperrliste + Tests

Auf `CodingSessionViewModel`:
- `IReadOnlyCollection<string> RejectedFindings` (read-only Public)
- `void AddRejection(string code, double meter)`
- `bool IsRejected(string code, double meter)` (mit ±0.5m-Toleranz)
- `static string MakeRejectionKey(string code, double meter)` (oder privat
  als Helper)

**Tests:** "Add + IsRejected = true", "Toleranz ±0.5m", "case-insensitive
Code-Match", "MakeRejectionKey-Format ist stable".

### Step 2: CodingModeWindow.xaml — Confirmation-Panel

Neuer `Border x:Name="ConfirmationPanel"` ueber `OverlayCanvas`. Visibility
binding gegen `_vm.IsAwaitingUserDecision`. Ampel-Ellipse (`Fill` aus
Helper-Converter, der `PendingConfirmationIsRed` interpretiert), Code-Text,
Konfidenz, Beschreibung, drei Buttons (`Click="ConfirmAccept_Click"`/`...Edit`/`...Reject`)
mit Handlern in der neuen `CodingModeWindow.PauseConfirm.cs`-Partial.

### Step 3: CodingModeWindow.PauseConfirm.cs

Click-Handler die `_vm.CompleteConfirmation(...)` rufen. Auf Edit-Click
zusaetzlich `_vm.SelectedDefect = _vm.PendingConfirmationEvent` setzen,
damit das DefectDetailPanel den User in den Edit-Modus bringt.

Plus die lokale Gate-Policy als private Helper:
```
private static (bool isGreen, bool isYellow, bool isRed, double confidence)
    EvaluateGate(LiveFrameFinding f)
{
    var conf = Math.Clamp(f.Severity * 0.20, 0.0, 1.0);
    if (conf >= 0.85) return (true, false, false, conf);
    if (conf >= 0.60) return (false, true, false, conf);
    return (false, false, true, conf);
}
```

### Step 4: Loop-Integration in CodingModeWindow.LiveLoop.cs

`RunLiveAnalysisAsync` bekommt nach jedem analyzed Frame die
`LiveDetection.Findings`-Liste. Erstes Finding mit `IsYellow || IsRed`
laut lokaler Gate-Policy wird nicht direkt akzeptiert sondern:
- Player auf Pause via `_player.SetPause(true)`
- `await _vm.BeginConfirmationAsync(event, confidence, isRed, ct)`
- Decision-Branches:
  - `Accepted` / `AcceptedWithEdit` → CodingEvent in VM setzen
    (heute via `_vm.AddEventInOrder`?), Loop continues, Player resume
  - `Rejected` → KEIN Event-Add. Loop continues, Player resume.
    (Sperrliste folgt in Step 5.)

→ **UI-Smoke 1 faellig** (siehe unten).

### Step 5: Sperrliste-Filter

Im Loop vor dem `BeginConfirmationAsync`-Aufruf: pruefe
`_vm.IsRejected(code, meter)`. Wenn true → Finding ueberspringen, kein
Pause/Confirm. Bei `Rejected`-Decision in Step 4 dann
`_vm.AddRejection(code, meter)`.

→ **UI-Smoke 2 faellig** (siehe unten).

### Step 6: Doku + Memory

Memory-TODO "OperateurAnnotation-Smoke" + dieser Slice abschliessen.
Mini-ADR Status auf Done flippen.

### Verifikation pro Step

- **1a, 1b, 2, 3:** Build + Tests reichen (deterministisches Refactor +
  XAML-Layout, keine User-sichtbare Verhaltensaenderung).
- **4:** **UI-Smoke 1 faellig** — Coding-Modus, ein Yellow/Red-Finding
  triggert Pause + Panel + Ampel; Accept resumed Player; Edit setzt
  SelectedDefect korrekt; Reject dropped Event ohne Sperrliste-Wirkung.
- **5:** **UI-Smoke 2 faellig** — gleiche Stelle nochmal codieren,
  zuvor Rejected: dann triggert kein Pause-Panel mehr.
- **6:** kein Smoke (nur Doku).

## Was diese ADR explizit ausklammert

- **Auto-BCD/BCE/Streckenschaden** (audit-diff "Auto-BCD/BCE", separater Slice).
- **Auto-Kalibrierung** (audit-diff "TryAutoCalibrationFromCurrentFrame", separater Slice).
- **OperateurAnnotation UI-Smoke** (Memory-TODO, kein Pause-Confirm-Bezug).
- **Vollintegration der YOLO-first/SAM/Multi-Model-Eskalation**, die im PlayerWindow vor 5b
  Findings produzierte. Heute liefert `EnhancedVisionAnalysisService` einfaches Qwen — fuer
  den Pause-Confirm-Workflow reicht das, weitere Pipeline-Stufen sind ein eigenes Thema.

## Entscheidungs-Protokoll

User-Review am 2026-05-10:

1. **Q1=A, Q2=A, Q3=A, Q4=A, Q5=A:** zugestimmt.
2. **Step 1 in 1a/1b geteilt:** ConfirmationFlow/TCS-State (1a)
   getrennt von Sperrliste/Reject-Key (1b). Saubere Test-Granularitaet.
3. **Gate-Quelle korrigiert:** mein urspruenglicher Entwurf hatte
   faelschlich behauptet, der Loop bekomme schon ein
   `QualityGateResult`. Tatsaechlich liefert
   `EnhancedVisionAnalysisService.AnalyzeWithEscalationAsync` nur
   `(EnhancedFrameAnalysis, bool Escalated)`. Fuer diesen Slice nutzen
   wir die lokale Severity-Policy (siehe Bestandsaufnahme Abschnitt
   D). Ein voller QualityGate-Service-Umbau bleibt ausserhalb dieses
   Slices.
4. **UI-Smoke nach Step 4 + final nach Step 5.** Step 4 prueft Pause/
   Panel/Accept/Edit/Reject/Resume; Step 5 prueft zusaetzlich
   Sperrliste-Wirkung.
5. **Sperrliste in-memory only.** Persistenz-in-CodingSession ist ein
   eigener spaeterer Slice (sonst wuerde aus dem Mini-Slice ein
   Datenmodell-/Serialisierungs-Umbau).

Slice 8a Pause-Confirm ist freigegeben und startet mit Step 1a.
