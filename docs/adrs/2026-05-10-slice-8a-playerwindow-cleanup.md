# Slice 8a PlayerWindow-Cleanup nach 5b — Mini-ADR

Datum: 2026-05-10
Status: **Done** (Slice in 1 Commit abgeschlossen, UI-Smoke an User delegiert)

Geliefert:
- Cleanup `5b3900a` — Dead-Code-Felder (_codingSchemaType,
  _codingLastOsdMeter), pragma-Block, EnsureHaltungslaenge-Methode +
  HasValidLength-Helper entfernt. _codingSchemaManager bleibt (aktiv
  via .Cancel() in MarkTool/Hotkeys).

Befund nach Code-Audit (Q1=B):
- _codingSchemaType: nur null-gesetzt in MarkTool, nirgends gelesen → raus.
- _codingLastOsdMeter: nirgends gesetzt, immer null → raus + einziger
  Aufrufer (OpenCodeCatalogForMark) nutzt jetzt direkt
  GetMeterFromVideoPosition().
- _codingSchemaManager: aktiv in MarkTool.cs:53/116 + Hotkeys.cs:49 via
  .Cancel() → bleibt (entgegen meinem Mini-ADR-Vorschlag, basierend auf
  Live-Code-Audit).
- EnsureHaltungslaenge: kein Caller mehr nach 5b → raus + HasValidLength-
  Helper (einziger Caller war EnsureHaltungslaenge) → raus.

LOC-Delta: -76 netto.

User-Freigabe 2026-05-10:
- Q1=B: pragma-Felder + EnsureHaltungslaenge + ggf. SchemaManager.
- Q2=A: EnsureHaltungslaenge loeschen.
- Q3: Smoke = MarkTool-Workflow, an User delegiert.

UI-Smoke-Pending (User-Item):
- PlayerWindow oeffnen, MarkTool aktivieren (SAM-Markierung zeichnen).
- Code-Catalog oeffnen via Markierung.
- Foto-Aufnahme.
- Verhalten muss unveraendert bleiben.

Vorgeschichte:
- Audit-Diff: `2026-05-09-slice-8a-1-audit-diff.md` Step 9-11 hat
  "Static-Bridge umbauen + Aufrufer umleiten + PlayerWindow loeschen"
  als finale Schritte aufgelistet.
- Slice 8a.3 Step 5b: `1997223` hat alle PlayerWindow.Coding*-Partials
  geloescht (CodingMode/CodingEvents/CodingTool/CodingOverlayRender/
  CodingOverlaySchema/CodingApply teilweise).
- Slice 8a.3 Step 5b-fix: `b009c48` + `81e2124` haben SAM-Sidecar +
  HTTP-Hint-Followup geliefert.

## Reality-Check vor dem Slice

Die Audit-Diff-Annahme Steps 9-11 ist **ueberholt**. Die heutige Lage:

| Aspekt | Audit-Diff-Annahme | Realitaet 2026-05-10 |
|---|---|---|
| PlayerWindow LOC | implizit "nur Coding-Wrapper" | **5478 LOC** in 14 Partials |
| Aktive Features | nur Coding-Modus | LiveDetection (879), OperateurAnnotation (826), MarkTool (709), TrainingMode (689), VideoPlayback (312), Hotkeys, DamageMarkers etc. |
| Static-Bridge-Methoden | "ans CodingModeWindow umhaengen" | bleiben sinnvoll am PlayerWindow weil **PlayerWindow das Default-Video-Fenster ist** (CodingModeWindow nur Modal-Dialog beim "Codier-Modus"-Klick) |
| Aufrufer DataPage etc. | umleiten an CodingModeWindow | greifen weiterhin auf das Default-PlayerWindow zu — das ist genau das Window das sie wollen |

PlayerWindow loeschen wuerde bedeuten, alle obigen Features ans
CodingModeWindow zu verschieben oder neu zu schreiben — das ist
**keine Cleanup-Sache**, sondern ein eigenes mehrwoechiges Re-Design
und stand nie wirklich auf der Agenda.

**Was bleibt vom Audit-Diff Step 9-11 nach Reality-Check:**
- Step 9 "Static-Bridge umbauen": entfaellt — Bridge passt.
- Step 10 "Aufrufer umleiten": entfaellt — Aufrufer ziellen korrekt.
- Step 11 "PlayerWindow loeschen": entfaellt — PlayerWindow lebt.

Was **wirklich** offen ist: Dead-Code-Residuen aus 5b im
`PlayerWindow.CodingApply.cs`-Partial — Felder die nach dem In-Place-
Coding-Mode-Killen leben aber nicht mehr aktiv genutzt werden.

## Was diese ADR macht

Sie definiert einen kleinen **PlayerWindow-Cleanup-Slice**, der die
nach 5b residualen Dead-Code-Felder in `PlayerWindow.CodingApply.cs`
auditiert und entfernt.

## Was diese ADR NICHT macht

- Keine Migration von PlayerWindow-Features (LiveDetection, MarkTool,
  TrainingMode, OperateurAnnotation) — die bleiben im PlayerWindow.
- Keine Rename-Aktion (z.B. `_codingVm` → `_markVm`) — der Prefix
  `_coding` verraet zwar die Coding-Mode-Origin, aber die Felder sind
  inzwischen "shared infrastructure" fuer MarkTool und Helpers. Rename
  wuerde Risiko ohne klaren Nutzen bringen.
- Keine Aenderung an den Static-Bridge-Methoden (TrySeekTo etc.).
- Keine Aenderung an externen Aufrufern.

## Bestandsaufnahme

### Felder in PlayerWindow.CodingApply.cs nach 5b

```csharp
// Aktiv genutzt (MarkTool/OperateurAnnotation/Hotkeys/LiveDetection):
private CodingSessionViewModel? _codingVm;             // gesetzt in MarkTool.cs:95
private ICodingSessionService? _codingSessionService;  // gesetzt in MarkTool.cs:95
private IOverlayToolService? _codingOverlayService;    // gesetzt in MarkTool.cs:95
private readonly SchemaOverlayManager _codingSchemaManager = new();
private VisionPipelineClient? _codingVisionClient;     // gesetzt in MarkTool.cs:668

// Pragma-suppress (CS0414, CS0649):
private SchemaType? _codingSchemaType;                 // nur in MarkTool.cs:54 _ = null gesetzt, NIRGENDS gelesen
private double? _codingLastOsdMeter;                   // NIRGENDS gesetzt, nur in CodingApply.cs:58/106 gelesen (immer null)
```

Plus eine Methode:
- `EnsureHaltungslaenge(HaltungRecord)` — Fallback-Kette fuer Haltungs-
  laenge_m. Wird heute (nach 5b) **nicht mehr** gerufen — der alte
  Coding-Mode-Pfad war der einzige Aufrufer.

### Use-Case-Analyse

**`_codingVm` / `_codingSessionService` / `_codingOverlayService`:**
- Lebenden Use-Case: MarkTool.ShowSamPreviewAtMarkAsync und friends.
  MarkTool initialisiert sie lazy (MarkTool.cs:92-95) wenn der User
  zum ersten Mal eine SAM-Markierung zieht.
- Diese Felder sind weiter **aktiv** genutzt; bleiben.

**`_codingSchemaManager`:**
- Lebende Use-Cases? grep zeigt keine Aufrufer mehr nach 5b.
- Pruefen ob entfernbar (ggf. Folge-Slice).

**`_codingVisionClient`:**
- Lazy-init in MarkTool.cs:668, genutzt fuer SAM-Aufrufe.
- Bleibt.

**`_codingSchemaType`:**
- Reset auf null in MarkTool.cs:54 (= TrySAM.MarkPanelClose).
- Niemand liest es. Reset-Zuweisung kann mit dem Field zusammen weg.

**`_codingLastOsdMeter`:**
- Niemand schreibt. Liest in `OpenCodeCatalogForMark` als Fallback fuer
  GetMeterFromVideoPosition (Zeile 58) und in `xaml.cs:789` fuer
  GetMeterFromVideoPosition selbst.
- Da nie gesetzt, ist der Wert immer null → der Fallback aus
  GetMeterFromVideoPosition greift sowieso. Field + Reads entfernbar.

**`EnsureHaltungslaenge`-Methode (CodingApply.cs:164):**
- Kein Caller mehr nach 5b (grep).
- Kann komplett raus.

### Umfang

- 2 Felder (`_codingSchemaType`, `_codingLastOsdMeter`) loeschen.
- 1 Methode (`EnsureHaltungslaenge` + Helper `HasValidLength` falls
  nur dort genutzt) loeschen.
- 1 SchemaOverlayManager (`_codingSchemaManager`) — wenn nirgends
  mehr genutzt, mit raus.
- Pragma-Block (`#pragma warning disable CS0414, CS0649`) entfernen.
- Reset-Zuweisung in MarkTool.cs:54 anpassen (kein Field mehr).

Erwarteter LOC-Delta: **-30 bis -50 LOC**.

## Die Designfragen

### Q1 — Wie weit geht der Cleanup?

**Optionen:**
- **A) Nur die zwei pragma-suppressed Felder + `EnsureHaltungslaenge`.**
  Kleinster Slice. Was klar tot ist, kommt raus. Schmierige Dinge
  bleiben.
- **B) A + `_codingSchemaManager` wenn keine Aufrufer.** Etwas weiter,
  noch immer mechanisch.
- **C) A + B + Rename `_coding*`-Prefix entfernen** (auf z.B. `_mark*`,
  da MarkTool primaerer Caller ist). Mehr Risiko, semantisch besser.

**Empfehlung: B.** Was tot ist, kommt raus. Rename ist Folge-Slice
falls semantische Klarheit gewuenscht wird.

### Q2 — Was machen wir mit `EnsureHaltungslaenge`?

**Optionen:**
- **A) Loeschen** (kein Caller mehr).
- **B) In CodingModeWindow oder ProtocolBoundaryService verschieben**
  falls die Fallback-Kette spaeter wieder gebraucht wird.

**Empfehlung: A.** YAGNI. Die ProtocolBoundaryService.EnsureBoundaries
nutzt heute `_session.EndMeter` direkt. Wenn ein zukuenftiger Slice
die Fallback-Kette braucht, kann er sie aus dem git-history holen.

### Q3 — Wie verifizieren wir?

- Build: 0 Warn / 0 Err nach Cleanup.
- Tests: keine Regression.
- UI-Smoke: PlayerWindow MarkTool durchklicken (SAM-Markierung
  zeichnen, Code-Catalog oeffnen, Foto-Aufnahme) — Verhalten muss
  unveraendert bleiben.

## Resultierender Migrations-Schnitt

Klein, ein Commit:

### Step 1: Dead-Code-Felder + EnsureHaltungslaenge entfernen

In `PlayerWindow.CodingApply.cs`:
- `_codingSchemaType`-Field + `#pragma warning disable CS0414, CS0649`
  Block entfernen.
- `_codingLastOsdMeter`-Field entfernen.
- Aufrufer von `_codingLastOsdMeter` (in OpenCodeCatalogForMark Zeile 58
  und xaml.cs:789) anpassen — der Fallback `GetMeterFromVideoPosition()`
  greift sowieso wenn das Field null ist.
- `EnsureHaltungslaenge`-Methode entfernen.
- `HasValidLength`-Helper aus `PlayerWindow.Helpers.cs` pruefen — wenn
  nur EnsureHaltungslaenge ihn nutzt, mit raus.
- `_codingSchemaManager` pruefen — wenn keine Aufrufer mehr, raus.

In `PlayerWindow.MarkTool.cs:54`:
- `_codingSchemaType = null;` Zeile entfernen.

### Verifikation

- Build: 0 Warn / 0 Err.
- Tests: keine Regression.
- UI-Smoke: PlayerWindow → MarkTool → SAM-Preview-Workflow durchspielen.
  Verhalten unveraendert.

## Nach diesem Slice

- Slice 8a Stop-Liste vom 2026-05-09 ist endgueltig durch.
- Audit-Diff-Plan ist abgeschlossen (Steps 9-11 sind by-design entfallen).
- Naechste Themen ausserhalb 8a-Slice: OperateurAnnotation UI-Smoke
  (Memory-TODO), `_coding`-Prefix-Rename (Folge-Slice falls gewuenscht).

## Was diese ADR explizit ausklammert

- **Rename `_coding*` → `_mark*`** (Folge-Slice falls semantische
  Klarheit gewuenscht).
- **Migration von Live-Detection / Operateur / Training / MarkTool ans
  CodingModeWindow** — nicht in Sicht, PlayerWindow lebt als Default-
  Video-Window weiter.
- **OperateurAnnotation UI-Smoke** (Memory-TODO).

## Entscheidungs-Protokoll

User-Freigabe 2026-05-10:
- Q1=B: pragma-Felder + EnsureHaltungslaenge + ggf. SchemaManager.
- Q2=A: EnsureHaltungslaenge loeschen.
- Q3: Smoke = MarkTool-Workflow, an User delegiert.

Cleanup wird in 1 Commit autonom ausgefuehrt.

## Wie lange geht das noch?

**Klare Schaetzung fuer den Audit-Diff-Plan-Abschluss:**

- **Step 4 der laufenden Slice 8a Auto-BCD/BCE/Streckenschaden**
  (Doku/CHANGELOG nach UI-Smoke): ~5 Min nach deinem Smoke-Greenlight.
- **Slice 8a PlayerWindow-Cleanup** (dieser ADR): ~15-30 Min Code +
  ~5 Min UI-Smoke.
- **OperateurAnnotation UI-Smoke** (Memory-TODO): User-Aktion, kein
  Coding-Aufwand meinerseits.

Insgesamt **noch ~30-45 Min Coding-Arbeit** plus zwei UI-Smokes deinerseits.
Danach ist der Audit-Diff-Plan vom 2026-05-09 endgueltig durch.
