# Slice 8a Auto-Kalibrierung-Wiring — Mini-ADR

Datum: 2026-05-10
Status: **Entschieden** (User-Review 2026-05-10 mit zwei Praezisierungen)
- Q1–Q6: zugestimmt (alle A-Empfehlungen)
- Praezisierung 1: `WasManuallyCalibrated=true` aus
  `AutoCalibrationService.TryAutoCalibrate` ist eine **bestehende
  Eigenheit** des Services (Field-Name irrefuehrend), nicht die
  gewuenschte Semantik dieses Slices. Wir uebernehmen die Calibration
  unveraendert; ein Rename / Refactor des Flag-Felds ist eigener
  Folge-Slice falls noetig.
- Praezisierung 2: PNG-Decode-Helper als `internal static` in der
  neuen `AutoCalibration.cs`-Partial. Tests "klein und testbar" —
  nicht zu gross.
Vorgeschichte:
- Audit-Diff: `2026-05-09-slice-8a-1-audit-diff.md` markiert Auto-Kalibrierung
  als "wichtiges UX-Feature" (Step 5 der Migrations-Reihenfolge).
- Pause-Confirm-ADR: `2026-05-10-slice-8a-pause-confirm.md` hat Auto-
  Kalibrierung explizit ausgeklammert ("separater Slice").
- Stop-Liste-ADR: `2026-05-09-slice-8a-2-stop-list-adr.md` mit Calibration-
  Workflow als Block — der manuelle Calibration-Pfad ist seit Slice 8a.2.10
  durch (Commits 23da9b3, a19aa4a, 11885c9). Auto-Kalibrierung ist der
  letzte Calibration-Block, der noch fehlt.

## Was diese ADR macht

Sie beantwortet die Designfragen fuer das Wiring der bestehenden
`AutoCalibrationService.TryAutoCalibrate` (172 LOC, lebt seit langem)
ans neue `CodingModeWindow`. Heute ruft im CodingModeWindow niemand
diesen Service — nur `MultiModelAnalysisService` nutzt ihn intern fuer
seine eigene Pixel-zu-mm-Umrechnung. Folge: jeder neue Coding-Modus-
Lauf startet mit `Nicht kalibriert – Referenz zeichnen`, der User muss
manuell die Kalibrierungs-Linie ziehen.

## Was diese ADR NICHT macht

- Keine Aenderungen an `AutoCalibrationService.cs` selbst (Algorithm
  bleibt). Bei Bedarf gibt es eine eigene ADR fuer Algorithm-Tuning.
- Keine Migration des manuellen Calibration-Pfads (BtnCalibrate +
  ApplyCalibration) — der lebt schon im CodingModeWindow.
- Keine UI-Aenderung am Calibration-Status-Text-Block.
- Keine Calibration-Persistenz (in HaltungRecord oder Session-Level) —
  Kalibrierung lebt session-life-cycle wie heute.

## Bestandsaufnahme

### A) Was AutoCalibrationService kann

`TryAutoCalibrate(BitmapSource frame, int nominalDiameterMm)` →
`PipeCalibration?`. Algorithmus:
- Scannt 9 horizontale Zeilen (30%–70% Bildhoehe) nach starken
  Helligkeitsgradienten (Rohrinnenwand → Rohrwand-Kante).
- Mindestens 5 von 9 Zeilen muessen Kanten finden, sonst null.
- Plausibilitaets-Check: Rohrdurchmesser muss zwischen 25% und 92% der
  Bildbreite liegen.
- Median ueber alle gueltigen Messungen → robust gegen Ausreisser.
- Returns `PipeCalibration { NominalDiameterMm, NormalizedDiameter,
  PipePixelDiameter, PipeCenter, WasManuallyCalibrated=true }`.

Service-Doku: "Ideal bei Rohranfang (BCD) oder Rohrverbindung (Muffe)
wo das Profil gut sichtbar ist."

### B) Was im CodingModeWindow heute fehlt

`CodingModeWindow.xaml.cs:158-169` setzt beim OnLoaded() eine
**Stub-PipeCalibration** mit `PipePixelDiameter=0` und nur dem
NominalDiameterMm aus `_haltung.Fields["DN_mm"]`. Das ist
**unkalibriert** — `IsCalibrated` returns false weil PipePixelDiameter=0.
Der Status-Text zeigt entsprechend `Nicht kalibriert – Referenz
zeichnen`.

Es gibt keinen Aufruf von `AutoCalibrationService.TryAutoCalibrate`
im CodingModeWindow oder seinen Partials.

### C) Was schon da ist (Wiederverwendung)

- `CaptureCurrentFrameAsync` (in `CodingModeWindow.FrameCapture.cs`)
  liefert PNG-Bytes — heute fuer den Vision-Pfad genutzt. Konvertierung
  PNG → BitmapSource ist mechanisch (siehe `BitmapDecoder` in
  `AutoCalibrationService.ConvertToGrayscale`-aehnlichem Pattern).
- `_overlayService.SetCalibration(PipeCalibration)` ist die Standard-
  Schreibstelle.
- `_overlayService.IsCalibrated` Property + UI-Status `TxtCalibrationStatus`.
- `_haltung.Fields["DN_mm"]` als Quelle fuer NominalDiameterMm.
- Frame-Readiness im VM (`_vm.IsFrameReady`) — wir wollen erst nach
  Warmup auto-kalibrieren, sonst riskieren wir Schwenk-/Bewegungs-
  Frames.

## Die sechs Designfragen

### Q1 — Wann triggert die Auto-Kalibrierung?

**Aktuell-Vermutung:** Beim ersten Frame-Ready im Live-Loop (nach Warmup),
solange `_overlayService.IsCalibrated == false`.

**Optionen:**
- **A) Im Live-Loop nach Frame-Readiness-Gate, vor ShowAiResults.**
  Einmalig pro Session — sobald kalibriert, kein Retry mehr.
- **B) Beim Loaded() einmalig vor Loop-Start.** Frame-Capture im
  OnLoaded → blockiert UI bis Capture+Calibrate fertig.
- **C) Bei jedem Frame versuchen solange uncalibriert.** CPU-Last,
  aber maximaler Erfolgs-Chance.
- **D) Erst beim ersten BCD-Finding (Rohranfang) — passt zur Algo-Doku.**
  Setzt voraus dass die Pipeline einen BCD-Code geliefert hat.

**Empfehlung: A.** Live-Loop-Hook ist der natuerlichste Punkt, Frame
ist schon decoded und der Frame-Readiness-Gate hat Schwenks raus-
gefiltert. Einmalig pro Session reicht — wenn der erste Versuch
fehlschlaegt, kann der User immer noch manuell kalibrieren. D waere
spannend ist aber zu spaet (User sieht erst BCD wenn Loop laeuft).

### Q2 — Wer triggert?

**Optionen:**
- **A) Inline in `RunLiveAnalysisAsync` nach RecordFrame-Gate.**
  Direkter Hook, kein zusaetzlicher Async-Lifecycle.
- **B) Separate Methode `TryAutoCalibrateOnceAsync(ct)` im Loop-Body
  am Anfang einer Iteration.** Sauber gekapselt, leichter testbar.
- **C) Eigener Worker/Service.** Overkill fuer einen einmaligen Trigger.

**Empfehlung: B.** Eine eigene Helper-Methode `TryAutoCalibrateOnceAsync`
in einer neuen `CodingModeWindow.AutoCalibration.cs`-Partial. Wird im
Loop nach RecordFrame + IsFrameReady aufgerufen, bevor der Pause-Confirm-
Gate triggert. Saubere Grenze, leichter zu finden, Loop-Code bleibt schlank.

### Q3 — Was macht der Trigger genau?

Sequenz pro Trigger-Aufruf:
1. Wenn `_overlayService.IsCalibrated` schon true → return.
2. DN_mm aus `_haltung.Fields["DN_mm"]` lesen. Wenn 0/missing → return.
3. PNG-Bytes aus aktuellem Frame holen (haben wir schon im Loop —
   `pngBytes` Variable). Alternativ ein neuer `CaptureCurrentFrameAsync`-
   Call.
4. PNG → BitmapSource decoden (PngBitmapDecoder + Frames[0]).
5. `AutoCalibrationService.TryAutoCalibrate(bitmap, dn)` aufrufen.
6. Bei Erfolg: `_overlayService.SetCalibration(result)`, UI-Status-Text
   `TxtCalibrationStatus` aktualisieren ("Auto-kalibriert: DN xxx mm").
7. Bei null: `_calibrationAutoTried = true` setzen damit nicht jeder
   Frame es erneut versucht. Nach erstem Fehlschlag bleibt es beim
   manuellen Pfad.

**Empfehlung: einmaliger Versuch** (`_calibrationAutoTried`-Flag) bei
erstem Ready-Frame. Wenn null → manueller Fallback. Logfile-Eintrag
fuer Diagnostics.

### Q4 — Wie oft retry?

**Optionen:**
- **A) Genau einmal versuchen.** Wenn null → User muss manuell.
  Einfach, deterministisch.
- **B) Bis zu N Frames versuchen, dann aufgeben.** Hoeherer Erfolg,
  mehr CPU.
- **C) Bei jedem uncalibrierten Frame.** Hoechste Erfolgswahrscheinlich-
  keit, aber CPU-Last (172-LOC-Pixel-Scan pro Frame).

**Empfehlung: A.** Einmalig. Wenn der erste Ready-Frame kein erkennbares
Pipe-Profil hat (Schwenk, Boden, Hindernis), ist der zweite/dritte oft
auch nicht besser. Und der User merkt sofort dass nichts kalibriert
wurde, kann manuell ziehen. Kein versteckter CPU-Verbrauch.

(Folge-Slice koennte Option B als opt-in einfuehren wenn UI-Smoke zeigt
dass A zu oft fehlschlaegt.)

### Q5 — Manueller Pfad?

**Optionen:**
- **A) Bleibt unveraendert.** User kann jederzeit `BtnCalibrate`
  klicken, ueberschreibt die Auto-Kalibrierung.
- **B) BtnCalibrate disabled bei erfolgreicher Auto-Kalibrierung.**
  Verhindert versehentliches Ueberschreiben.
- **C) BtnCalibrate-Tooltip zeigt "Auto-kalibriert, klicken um zu
  ueberschreiben".** Mittlere Loesung.

**Empfehlung: A.** Manueller Pfad bleibt jederzeit verfuegbar. Wenn
die Auto-Kalibrierung daneben liegt (z.B. naher Anschluss als Rohrwand
gelesen), muss der User die korrigieren koennen ohne Klimmzug.

### Q6 — Was wenn DN_mm fehlt?

`_haltung.Fields.TryGetValue("DN_mm", ...)` kann fehlschlagen wenn die
Stammdaten unvollstaendig sind (z.B. PDF-Import ohne DN-Feld).

**Optionen:**
- **A) Auto-Kalibrierung skip, manueller Pfad bleibt.** Status: "DN
  unbekannt - bitte manuell kalibrieren".
- **B) DN aus User-Dialog erfragen, dann auto-kalibrieren.** Mehr
  UX-Klimmzug.
- **C) DN aus Vision-Modell schaetzen** (Qwen "PipeDiameterMm" liefert
  manchmal eine Schaetzung).

**Empfehlung: A.** Simpler Skip — das Status-Feld zeigt's schon heute
mit "DN: unbekannt". Folge-Slice koennte C einbauen wenn der Use-Case
real wird.

## Resultierender Migrations-Schnitt (kein Code, nur Liste)

In dieser Reihenfolge, jeder Step eigener Commit + Build/Test-Gate:

### Step 1: PNG → BitmapSource Helper

`internal static` Methode in der neuen `CodingModeWindow.AutoCalibration.cs`-Partial
(User-Praezisierung 2026-05-10):
```csharp
internal static BitmapSource? DecodePngToBitmap(byte[]? pngBytes)
```

Klein und testbar. Tests im Pipeline-Tests-Project bewusst knapp:
- Roundtrip mit einem programmatisch erzeugten kleinen PNG
- null/empty bytes → null
- Korrupte Bytes → null (kein Throw)

Keine grosse Test-Suite — der Helper ist trivial, die Calibration-
Logik selbst lebt in `AutoCalibrationService` und ist nicht Teil
dieses Slices.

### Step 2: AutoCalibration-Helper-Partial

Neue `CodingModeWindow.AutoCalibration.cs`:
```csharp
private bool _calibrationAutoTried = false;

private async Task TryAutoCalibrateOnceAsync(byte[] pngBytes)
{
    if (_calibrationAutoTried) return;
    if (_overlayService.IsCalibrated) return;
    if (!_haltung.Fields.TryGetValue("DN_mm", out var dnStr) ||
        !int.TryParse(dnStr, out var dn) || dn <= 0) return;
    _calibrationAutoTried = true;
    var bitmap = DecodePngToBitmap(pngBytes);
    if (bitmap == null) return;
    var result = AutoCalibrationService.TryAutoCalibrate(bitmap, dn);
    if (result == null) return;
    _overlayService.SetCalibration(result);
    await Dispatcher.InvokeAsync(() =>
        TxtCalibrationStatus.Text = $"Auto-kalibriert: DN {dn} mm");
}
```

**Hinweis zu `WasManuallyCalibrated=true`:** Die zurueckgegebene
`PipeCalibration` aus `TryAutoCalibrate` setzt `WasManuallyCalibrated=true`.
Das ist eine **bestehende Eigenheit des AutoCalibrationService**
(Field-Name irrefuehrend — er markiert "Calibration ist gueltig",
nicht "vom User gesetzt"). Wir uebernehmen den Wert unveraendert.
Ein Rename / Refactor des Felds ist eigener Folge-Slice falls noetig.

### Step 3: Loop-Integration

In `CodingModeWindow.LiveLoop.RunLiveAnalysisAsync`, nach
`_vm.RecordFrame(result)` und vor PauseConfirmCheck:

```csharp
if (_vm.IsFrameReady)
    await TryAutoCalibrateOnceAsync(pngBytes);
```

### Step 4: UI-Smoke + Doku

- UI-Smoke: Coding-Modus oeffnen mit DN-bekanntem Video, erster Ready-
  Frame loest Auto-Kalibrierung aus, Status zeigt "Auto-kalibriert: DN
  300 mm". Manueller Override durch BtnCalibrate funktioniert weiter.
- Edge-Case-Smoke: Coding-Modus ohne DN_mm → Status bleibt "DN
  unbekannt", manueller Pfad funktioniert.
- ADR-Status auf Done.
- CHANGELOG-Eintrag.

### Verifikation pro Step

- **1, 2:** Build + Tests reichen (reines Helper-Refactor, keine
  Verhaltensaenderung im Loop).
- **3:** **UI-Smoke faellig** — Coding-Modus mit DN-Video starten,
  Auto-Kalibrierung beobachten.
- **4:** kein Smoke (nur Doku).

## Was diese ADR explizit ausklammert

- **Auto-BCD/BCE/Streckenschaden** (eigener Slice, fachlich kritisch).
- **PlayerWindow-Aufloesung** (Audit-Diff Step 9-11, eigener Slice).
- **OperateurAnnotation UI-Smoke** (Memory-TODO, kein Calibration-
  Bezug).
- **Calibration-Persistenz** (in HaltungRecord oder Session) — wenn
  spaeter gewuenscht: eigene ADR.
- **Calibration-Quality-Score / Multi-Frame-Sampling** — wenn UI-Smoke
  zeigt dass Single-Shot zu unzuverlaessig ist: eigener Folge-Slice.
- **Algorithmus-Tuning von AutoCalibrationService** (MinGradientStrength,
  ScanLines, Plausibilitaets-Schwellen) — separater Slice.

## Entscheidungs-Protokoll

User-Review 2026-05-10:

1. **Q1=A, Q2=B, Q3=OK, Q4=A, Q5=A, Q6=A:** zugestimmt.
2. **Praezisierung 1 — `WasManuallyCalibrated=true`:** Bestehende
   Eigenheit von `AutoCalibrationService.TryAutoCalibrate`
   (Field-Name irrefuehrend, markiert "Calibration gueltig" nicht
   "User-gesetzt"). Im ADR als bestehender Artefakt dokumentiert,
   nicht als gewuenschte Semantik. Rename ist Folge-Slice falls noetig.
3. **Praezisierung 2 — PNG-Decode-Helper:** `internal static` in
   der neuen Partial. Tests bewusst klein gehalten (Roundtrip + null
   + korrupt) — der Helper ist trivial, Calibration-Logik selbst
   lebt im AutoCalibrationService und ist nicht Teil dieses Slices.

Slice freigegeben, startet mit Step 1.
