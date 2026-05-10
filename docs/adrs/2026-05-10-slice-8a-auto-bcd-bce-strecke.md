# Slice 8a Auto-BCD/BCE/Streckenschaden — Mini-ADR

Datum: 2026-05-10
Status: **Entwurf** (wartet auf User-Review)

Vorgeschichte:
- Audit-Diff: `2026-05-09-slice-8a-1-audit-diff.md` markiert Auto-BCD/BCE/
  Streckenschaden als "fachlich wichtig" (Step 6).
- Slice 8a Pause-Confirm + Auto-Kalibrierung sind durch — der Live-Loop
  hat jetzt Frame-Readiness, Pause-Confirm und Auto-Calibration. Was
  noch fehlt: Boundary-Codes (BCD/BCE) waehrend der Live-Session und
  saubere Streckenschaden-Behandlung.

## Was diese ADR macht

Sie beantwortet die Designfragen fuer das Verhalten von:
- **BCD (Rohranfang)** — Sichtbarkeit waehrend Live-Coding und beim Abschluss.
- **BCE (Rohrende)** — Auto-Insert beim Abschluss + ggf. live.
- **Streckenschaden** — wie mit offenen Streckenschaden-Anfaengen
  beim "Codierung abschliessen" umgehen.

## Was diese ADR NICHT macht

- Keine Aenderungen an `ProtocolBoundaryService` (lebt in
  Application/Protocol, ist getestet).
- Keine Migration von `EnsureHaltungslaenge` (lebt noch in
  `PlayerWindow.CodingApply.cs`) — eigener Folge-Slice falls die
  Bridge-Auflösung das braucht.
- Keine UI-Aenderung am Confirmation-Panel oder DefectDetailPanel.
- Keine Aenderung am Photo-Workflow fuer BCD/BCE
  (`BoundaryPhotoService.GenerateBoundaryPhotosAsync` bleibt im
  OnSessionCompleted-Pfad).

## Bestandsaufnahme

### A) Was heute IST

**ProtocolBoundaryService.EnsureBoundaries**
(`src/AuswertungPro.Next.Application/Protocol/ProtocolBoundaryService.cs`)
ist ein gutes, getestetes Modul:
- Fuegt fehlende BCD bei 0.00m ein.
- Fuegt fehlende BCE/BDC* am Ende ein wenn `haltungslaengeM > 0`.
- Setzt `foto_required` + `auto_boundary` Flags.
- Berechnet Inspektionslaenge.

**Wird zweimal gerufen:**
1. `CodingSessionService.CompleteSession()` Zeile 205 — beim Session-
   Abschluss in der Service-Schicht.
2. `CodingModeWindow.OnSessionCompleted` Zeile 2198 — beim Session-
   Abschluss im Window (fuer den Photo-Pfad).

**Streckenschaden-Behandlung in CodingSessionService.CompleteSession
Zeile 162-176:** Wirft `InvalidOperationException` wenn offene
Streckenschaden vorhanden. User muss erst alle manuell schliessen.

**Was waehrend der Live-Coding-Session passiert:** Nichts. Die
Eventliste zeigt keine BCD/BCE bis zum CompleteSession. Erst dann
springen sie auf — oft in der finalen Protokoll-Ansicht.

### B) Was im Legacy-PlayerWindow gemacht wurde (vor 5b geloescht)

Aus `git show 1997223`:

**EnsureRohranfangExists** (deaktiviert):
```csharp
// BCD wird NICHT mehr automatisch erzeugt — nur durch Eingabemarker
// oder Qwen-Erkennung.
// EnsureRohranfangExists(meter, videoTime, ref anyAdded);
```
Der Legacy-Code hatte eine BCD-Auto-Erzeugung, hat sie aber
**bewusst deaktiviert**. Wichtige User-Information.

**EnsureRohrendeExists** (aktiv, beim ExitCodingMode):
```csharp
private void EnsureRohrendeExists(double meterEnd, TimeSpan videoTime)
{
    if (_codingVm.Events.Any(e => string.Equals(e.Entry.Code, "BCE", ...)))
        return;
    double rohrEndMeter = _codingLastOsdMeter ?? meterEnd;
    var rohrEndTime = _player != null
        ? TimeSpan.FromMilliseconds(_player.Time)
        : videoTime;
    // Aus Import-Referenz BCE-Meter holen falls vorhanden
    // ...
    // Event mit Source=Ai, Confidence=1.0, Decision=Accepted erzeugen
}
```
Live-Insert beim ExitCodingMode mit OSD-Meter-Praeferenz vor EndMeter.

**CloseOpenStreckenschaeden** (User-Dialog):
```csharp
// Hinweis-Dialog mit Liste der offenen Streckenschaeden
// MessageBoxButton.YesNoCancel
//   Yes  -> alle offenen Streckenschaden bei currentMeter schliessen
//   No   -> ohne Schliessen weiter (bleiben offen im Protokoll)
//   Cancel -> Exit abbrechen, User will weiter codieren
```
**Drei Optionen, nicht eine Exception.** Der User konnte
entscheiden ob er die Streckenschaden bei aktuellem Meter schliessen,
ohne Schliessen weiter machen, oder den Exit-Versuch abbrechen will.

### C) Behavior-Gap zwischen Legacy und Heute

| Aspekt | Legacy (vor 5b) | Heute (nach 8a Pause-Confirm) |
|---|---|---|
| BCD-Auto-Erzeugung | bewusst deaktiviert, nur User/KI | **Auto** via EnsureBoundaries beim CompleteSession |
| BCE-Auto-Erzeugung | aktiv beim ExitCodingMode | Auto via EnsureBoundaries beim CompleteSession |
| Live-Sichtbarkeit BCD/BCE | nichts vor Session-Ende | nichts vor Session-Ende |
| Streckenschaden offen | User-Dialog (Yes/No/Cancel) | **Exception, blockiert CompleteSession** |

Drei Behavior-Diffs. Das Slice korrigiert die wichtigsten zwei.

## Die Designfragen

### Q1 — Was tun wir mit BCD?

**Optionen:**
- **A) Bestehender Pfad bleibt** (Auto-BCD bei CompleteSession via
  EnsureBoundaries). Verhalten ist anders als Legacy aber nicht falsch:
  ein Protokoll **muss** ein BCD haben, sonst ist es nach VSA-KEK
  unvollstaendig. EnsureBoundaries garantiert das.
- **B) Live-BCD bei Session-Start.** Sobald der Coding-Modus laeuft
  und die erste Eventliste leer ist, einen BCD-Event-Stub einfuegen
  (Source=Ai, Decision=Accepted, MeterStart=0.0). User sieht's sofort
  in der Liste.
- **C) Bewusste Rueckkehr zum Legacy-Verhalten** (kein Auto-BCD).
  EnsureBoundaries-Aufruf in CompleteSession entfernen.
  Konsequenz: Protokoll ohne BCD wenn weder User noch KI eins
  produzieren — Validate flagged dann Warnung.

**Empfehlung: A.** EnsureBoundaries ist defensiv und garantiert
VSA-Konformitaet. Live-Sichtbarkeit (Option B) ist nett, aber das
Boundary-Event mitten in der Live-Coding-Session als KI-Vorschlag
zu zeigen kann irritieren (User denkt "ich muss das bestaetigen?"
obwohl es automatisch ist). C verlagert Verantwortung zum User.

### Q2 — Was tun wir mit BCE?

**Optionen:**
- **A) Bestehender Pfad bleibt** (BCE via EnsureBoundaries beim
  CompleteSession).
- **B) Live-BCE im Loop bei `currentMeter >= EndMeter - epsilon`.**
  Sobald die Kamera am Haltungsende anlangt, BCE-Event automatisch
  hinzufuegen. User sieht's sofort.
- **C) Live-BCE beim "Codierung abschliessen"-Klick** (nicht im Loop,
  sondern im Pre-Complete-Hook im Window). Aequivalent zum Legacy-
  ExitCodingMode-Verhalten.

**Empfehlung: A.** Aus dem gleichen Grund wie Q1-A: EnsureBoundaries
hat das im Griff. Wenn User UI-Feedback fehlt ("hat das System
schon BCE registriert?"), kann das in einem Folge-Slice via
Status-Text ergaenzt werden.

### Q3 — Wie handhaben wir offene Streckenschaden beim Abschluss?

**Optionen:**
- **A) Bestehende Exception bleibt** (CodingSessionService wirft).
  Hartes Stop-Signal — User muss erst alle Streckenschaden manuell
  schliessen, dann erneut Abschluss klicken.
- **B) Legacy-Dialog wieder einbauen** (YesNoCancel im Window vor
  CompleteSession-Aufruf):
  - Yes → alle offenen Streckenschaden bei currentMeter schliessen,
    dann CompleteSession.
  - No → ohne Schliessen abschliessen (Streckenschaden bleiben offen
    im Protokoll, Validate flagged sie).
  - Cancel → Abschluss abbrechen, User codiert weiter.
- **C) Auto-Close bei currentMeter ohne Dialog.** User hat die
  geringste Friction. Aber: keine Chance zum Abbruch wenn der User
  "Codierung abschliessen" versehentlich geklickt hat.

**Empfehlung: B.** Legacy-Dialog ist der bewaehrte UX-Compromise.
Cancel-Option ist wichtig (User-Klick versehentlich, oder User merkt
beim Anschauen der Liste dass er noch coden will). Yes/No erlaubt
"unsauberes" Protokoll falls der User die Streckenschaden bewusst
offen lassen will.

### Q4 — Wo lebt die Streckenschaden-Dialog-Logik?

**Optionen:**
- **A) Im CodingModeWindow Code-Behind** (analog zum Legacy
  PlayerWindow).
- **B) Im CodingSessionViewModel** (Punkt-4-Pattern).
- **C) Im CodingSessionService** (Service-Layer, mit `IDialogService`
  als Dependency).

**Empfehlung: A.** Die Behandlung ist UI-Workflow ("zeige Liste, frage
3 Optionen, handle die Entscheidung"). VM kann's machen, aber das
Dialog-Wording / Yes/No/Cancel-Mapping ist UI-Detail. Service waere
zuviel Coupling.

Konkret: Der Dialog lebt in einer neuen Partial
`CodingModeWindow.StreckenschadenDialog.cs`. Der "Codierung
abschliessen"-Klick (BtnCompleteSession_Click oder aequivalent) ruft
**vor** `_vm.CompleteSession` einen Pre-Complete-Hook auf, der den
Dialog zeigt und je nach Decision:
- Yes → alle offenen Streckenschaden via `_sessionService.CloseStreckenschaden`
  schliessen, dann CompleteSession.
- No → das Service muss erlauben "abschliessen mit offenen
  Streckenschaden". Heute: Exception. Variante: ein Flag
  `allowOpenStreckenschaden` an `CompleteSession(bool)` durchreichen,
  oder den Exception-Pfad im Service auf eine Domain-Exception
  umstellen die das Window faengt und in den Dialog-Pfad delegiert.
- Cancel → no-op, User codiert weiter.

### Q5 — Wie wird der Service angepasst (fuer Q3-B)?

**Optionen:**
- **A) `CompleteSession(bool allowOpenStreckenschaden = false)`-Overload.**
  Neuer optionaler Parameter; default behaelt das aktuelle Verhalten
  (Exception). Der Window-Pre-Complete-Pfad ruft mit `true` wenn der
  User "No" klickt.
- **B) Eine neue Service-Methode `TryCompleteSession()` oder
  `CompleteSessionWithOpenStreckenschaden()`.** Doppelte API, mehr
  Reibung.
- **C) Domain-Exception (`OpenStreckenschadenException`) statt
  `InvalidOperationException`.** Window faengt sie, zeigt Dialog,
  ruft bei "Yes" CloseStreckenschaden + CompleteSession erneut, bei
  "No" gibts keinen sauberen Re-Entry-Pfad ohne den Service zu
  veraendern.

**Empfehlung: A.** Minimaler Service-Eingriff (ein Default-Parameter),
testbar, klare Semantik. C ist zu indirekt.

### Q6 — Tests

**Optionen:**
- **A) Keine neuen Tests** (Verhalten ist UI-Workflow, smoke reicht).
- **B) VM-Tests fuer den Pre-Complete-Hook** (wenn er ins VM kommt).
  Greift nur bei Q4-B/C.
- **C) Service-Tests fuer `CompleteSession(allowOpen=true)`-Overload**
  — verhaltsequivalenz fuer den Fall mit + ohne offene
  Streckenschaden.

**Empfehlung: C.** Nur der Service-Overload aus Q5-A bekommt einen
Test. Der Window-Dialog-Pfad ist UI-Smoke-Sache.

## Resultierender Migrations-Schnitt

In dieser Reihenfolge, jeder Step eigener Commit + Build/Test-Gate.
**Annahme: Q1=A, Q2=A, Q3=B, Q4=A, Q5=A, Q6=C** — wenn du andere
Optionen waehlst, schreibe ich den Plan um.

### Step 1: Service-Overload `CompleteSession(allowOpen)`

`CodingSessionService.cs`:
- Bestehender `CompleteSession()` ruft `CompleteSession(allowOpen: false)`.
- Neue Variante mit `allowOpen=true` ueberspringt die
  Streckenschaden-Pruefung; Eintraege bleiben mit `MeterEnd=null` im
  Protokoll.

Tests (Pipeline-Tests-Project):
- `CompleteSession()` mit offenen Streckenschaden → wirft (regression
  test).
- `CompleteSession(allowOpen=true)` mit offenen Streckenschaden →
  wirft nicht, Protokoll enthaelt die Eintraege.
- `CompleteSession()` ohne offene Streckenschaden → identisch zu
  `CompleteSession(allowOpen=false)`.

### Step 2: Pre-Complete-Hook im Window

`CodingModeWindow.StreckenschadenDialog.cs` (neue Partial):
- `bool ConfirmOpenStreckenschadenAndChooseAction(out bool allowOpen)`:
  - Liefert `false` wenn User "Cancel" klickt (Abschluss abbrechen).
  - Liefert `true` mit `allowOpen=false` wenn User "Yes" — ruft vorher
    intern `_sessionService.CloseStreckenschaden(...)` fuer alle
    offenen.
  - Liefert `true` mit `allowOpen=true` wenn User "No".
  - Liefert `true` mit `allowOpen=false` wenn keine offenen Streckenschaden.

Wiring im "Codierung abschliessen"-Klick (heute via
`_vm.CompleteSessionCommand`):
```csharp
if (!ConfirmOpenStreckenschadenAndChooseAction(out var allowOpen))
    return; // User-Cancel
_vm.CompleteSession(allowOpen); // statt CompleteSessionCommand
```

(Annahme: der CompleteSessionCommand ruft heute
`_sessionService.CompleteSession()` ohne Parameter. Wir muessen
entweder das Command durchreichen lassen oder den Pre-Complete-Hook
einbauen vor dem Command-Trigger.)

### Step 3: VM-Anpassung (CompleteSession-Befehl)

`CodingSessionViewModel.cs`:
- `CompleteSessionCommand` ruft heute via `[RelayCommand]`
  `_sessionService.CompleteSession()`. Anpassung: Default mit `allowOpen=false`.
- Optional eine VM-Methode `CompleteSession(bool allowOpen)` hinzufuegen
  fuer den Dialog-Pfad.

### Step 4: UI-Smoke + Doku

UI-Smoke:
- Coding-Modus, einen Streckenschaden anlegen ohne Ende → "Codierung
  abschliessen" → Dialog erscheint mit der Liste.
- Yes → Streckenschaden bei aktuellem Meter geschlossen, Protokoll OK.
- No → Streckenschaden bleibt offen, Protokoll enthaelt ihn ohne MeterEnd.
- Cancel → Dialog zu, User codiert weiter, Window bleibt offen.

ADR auf Done flippen, CHANGELOG-Eintrag.

### Verifikation pro Step

- **1:** Build + Tests reichen (reines Service-Refactor mit Tests).
- **2, 3:** Build + Tests reichen (UI-Workflow ohne Verhaltensaenderung
  bis Step 4 zusammenkommt).
- **4:** **UI-Smoke faellig** — Streckenschaden-Workflow durchspielen.

## Was diese ADR explizit ausklammert

- **Live-BCD/BCE-Sichtbarkeit waehrend Coding** — eigener Folge-Slice
  falls UX zeigt dass das fehlt.
- **EnsureHaltungslaenge** — lebt in PlayerWindow.CodingApply.cs,
  Migration kommt mit der PlayerWindow-Aufloesung.
- **PlayerWindow-Aufloesung Steps 9-11** (eigener Slice).
- **OperateurAnnotation UI-Smoke** (Memory-TODO).
- **Boundary-Photo-Workflow** (`BoundaryPhotoService` bleibt
  unveraendert im OnSessionCompleted).

## Offene Punkte fuer Dich (Reviewer)

1. **Q1 (BCD-Auto):** A (Status quo) ist mein Vorschlag — EnsureBoundaries
   garantiert VSA-Konformitaet. Lieber B (Live-Visibility) oder C
   (Legacy-Stil ohne Auto-BCD)?
2. **Q2 (BCE-Auto):** A (Status quo) — analog zu Q1. Lieber Live-Variante?
3. **Q3 (Streckenschaden-Dialog):** B (YesNoCancel) ist mein Vorschlag.
   Lieber A (Exception bleibt) oder C (Auto-Close)?
4. **Q4 (Dialog-Owner):** A (Window Code-Behind) — ok?
5. **Q5 (Service-API):** A (CompleteSession-Overload) — ok?
6. **Q6 (Tests):** C (nur Service-Overload-Test) — ok?

Wenn die sechs Punkte ok sind, schreibe ich die Steps 1-4 in
einzelnen Commits, Build/Test-Gate und UI-Smoke-Stop nach Step 3.
