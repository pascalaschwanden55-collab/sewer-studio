# Design: Metrierung erst bei Naehe (Proximity-Gate)

**Datum:** 2026-06-03
**Status:** Freigegeben (2026-06-03)
**Grundlage:** User-Freigabe fuer Ansatz 1 (reine C#-Pruef-Funktion in Application), Naehe-Regel mit Toleranz, konservative Grundhaltung.

## 1. Problem / Ausgangslage

Ein KI-Befund bekommt heute den **aktuellen Kamera-Meterstand** zugeordnet — unabhaengig davon, ob das Ereignis nah an der Kamera oder noch weit voraus im Rohr liegt.

- Der OSD-Meter wird per Vision-LLM aus "unten rechts im Bild" gelesen und in `_codingLastOsdMeter` gehalten (`PlayerWindow.Coding.cs`, OSD-Timer alle 3s). Das ist die **Kamera-Position**.
- Erkennt die KI z.B. einen Bogen 3 m voraus und uebernimmt sofort, bekommt der Bogen die Kamera-Position statt seiner echten Position 3 m weiter -> **falsche Metrierung**.
- Zusatzproblem: Bei axialer Sicht maskiert SAM oft das dunkle Rohrinnere (den Tunnel am Fluchtpunkt) als grosse ovale "Fehlmaske". Diese ist weit voraus (Tiefe), nicht codierbar.

Fachregel des Inspekteurs: Die scheinbare Groesse eines Ereignisses im Bild gibt die Distanz. Ein Ereignis ist erst dann "vor Ort", wenn es den vertikalen Bildwinkel ausfuellt (Muffenkreis erreicht oberen und unteren Bildrand). Ein Schaden darf weit voraus **erkannt**, aber erst bei **Naehe metriert/codiert** werden.

Ist-Stellen (am Code bestaetigt):

- Live-Pfad (Codiermodus "Aktuellen Frame analysieren"): `SingleFrameMultiModelService.AnalyzeFrameAsync(...)` -> `RunCodingAnalysisAsync` -> `ShowMultiModelResults` (Overlay, `PlayerWindow.Coding.cs:3025`) -> `AddMultiModelFindingsAsEvents` (Event, `:3033`).
- Vollanalyse-Pfad (Video-Batch): `MaskQuantificationService.QuantifyAll(...)` (`MultiModelAnalysisService.cs:392`) -> `new EnhancedFinding(...)` (`:408`).
- Kein Frame-Tracking in HEAD (bestaetigt) — die Loesung braucht keins.
- Keine Kamera-FOV/Intrinsics vorhanden — werden auch nicht gebraucht (relatives Mass).

## 2. Ziel

Ein Befund erhaelt nur dann einen Meterstand und einen Protokoll-/Event-Eintrag, wenn er **nah genug** ist. Zu weit voraus liegende Befunde werden weiterhin **angezeigt** (Overlay), aber **nicht metriert und nicht codiert**.

Leitprinzip: **konservativ**. Im Zweifel lieber ein fraglicher zentraler Befund als "Voraus / nicht metrieren" einstufen, als einen falschen Meter-/Protokolleintrag erzeugen.

## 3. Architektur-Entscheidung

Gewaehlter Ansatz: **Reine C#-Pruef-Funktion in der Application-Schicht** (`MetrierungProximityEvaluator`).

Nicht gewaehlt:

- Sidecar-seitiges Filtern: beschneidet die Erkennung; Befunde sollen aber sichtbar bleiben.
- Echtes Frame-Tracking (ByteTrack/OC-SORT): grosser neuer Baustein, laut CLAUDE.md nur nach expliziter Diskussion; fuer "erst bei Naehe metrieren" nicht noetig (YAGNI).

Die Logik ist rein (keine HTTP/UI/Timer-Abhaengigkeit), wird an zwei duennen Stellen aufgerufen und ist voll unit-testbar — gleiche Bauweise wie der `PipelineHealthEvaluator`.

## 4. Naehe-Regel

Bezugspunkt ist der **Fluchtpunkt** = Rohrmitte (`PipeCalibration.PipeCenter`, normiert; Fallback Bildmitte 0.5/0.5).

Eingangsgroessen pro Befund (alle normiert 0..1, aus vorhandener Box-/Masken-Geometrie):

- `box` = (X1,Y1,X2,Y2) der Detektion bzw. Masken-Bounding-Box.
- `center` = Box-Mittelpunkt.
- `fillRatio` = Boxhoehe / Bildhoehe (vertikale Bildfuellung).
- `distToVanish` = Abstand(`center`, Fluchtpunkt), normiert auf den Rohrradius.
- `wandnaehe` = der Box-Aussenrand erreicht die kalibrierte Rohrwand **oder** den Bildrand **innerhalb einer Toleranz** (Default 12 % des Rohrradius; kalibrierbar).
- `enthaeltCenter` = Box enthaelt den Fluchtpunkt.

Entscheidung:

- **Voraus / nicht metrieren**, wenn ALLE gelten:
  - `enthaeltCenter` == true, und
  - `center` liegt nahe dem Fluchtpunkt (`distToVanish` < `T_center`), und
  - **keine** `wandnaehe`.
  (Das ist der Tunnel-Fehlmaske-Fall: gross, zentral, aber ringsum helle Rohrwand sichtbar -> Maske beruehrt die Wand nicht.)
- **Codierbar**, wenn:
  - `fillRatio >= T_fill` **und** `wandnaehe` (querschnittsfuellendes Ereignis nah: Muffe/Bogen/Anschluss, dessen Rand bis zur Rohrwand/Bildrand reicht), **oder**
  - `distToVanish >= T_radial` (Befund liegt deutlich ausserhalb des Fluchtpunktbereichs, also nahe der Rohrwand: typischer Wandschaden im Nahbereich).
- **Sonst -> Voraus** (konservativer Default: alles, was nicht klar "Codierbar" ist, gilt als nicht metrierbar).

Damit:

- **Nahe Muffe** (gross, Rand an der Wand) -> Codierbar.
- **Tunnel-Fehlmaske** (gross, zentral, KEINE Wandnaehe) -> Voraus.
- **Wandschaden nah** (klein/mittel, aussen an der Wand) -> Codierbar.
- **Kleiner zentraler Fund weit voraus** -> Voraus.

### Kalibrierbare Parameter (mit Defaults)

- `T_fill` = 0.70 (Anteil Bildhoehe fuer "querschnittsfuellend nah")
- `T_center` = 0.20 (Naehe des Box-Zentrums zum Fluchtpunkt, in Rohrradius)
- `T_radial` = 0.45 (radiale Distanz fuer "klar aussen / an der Wand", in Rohrradius)
- `wandToleranz` = 0.12 (12 % Rohrradius Toleranz fuer Wand-/Bildrand-Kontakt)

Alle vier sind Konstruktor-/Settings-Parameter des Evaluators, damit sie ohne Code-Aenderung nachjustiert werden koennen. Startwerte sind bewusst konservativ.

## 5. Komponenten

### 5.1 `MetrierungProximity` (Ergebnis-Enum)

`Codierbar`, `Voraus`.

Ort: `src/AuswertungPro.Next.Application/Ai/MetrierungProximity.cs`

### 5.2 `MetrierungProximityResult` (Record)

Felder: `MetrierungProximity Decision`, `string Reason`, `double FillRatio`, `double DistToVanish`, `bool WandNaehe`, `bool EnthaeltCenter`.

Ort: gleiche Datei.

### 5.3 `MetrierungProximityInput` (Record)

Reine Eingabe, entkoppelt von DTOs:
`double X1,Y1,X2,Y2` (normiert), `double CenterX, CenterY` (Fluchtpunkt, normiert), `double ImageAspect` (Breite/Hoehe, fuer radiale Distanz-Korrektur), Schwellen via Evaluator (nicht im Input).

Ort: gleiche Datei.

### 5.4 `MetrierungProximityEvaluator` (reine Logik)

`public static MetrierungProximityResult Evaluate(MetrierungProximityInput input, MetrierungProximityThresholds thresholds)`

- Keine Seiteneffekte. Voll testbar.
- `MetrierungProximityThresholds` = Record mit den vier Parametern + statischem `Default`.

Ort: `src/AuswertungPro.Next.Application/Ai/MetrierungProximityEvaluator.cs`

### 5.5 `SegmentedFinding` — feste Kopplungseinheit (ersetzt Index-Kopplung)

Heute werden DINO-Detection, SAM-Maske und QuantifiedMask **per Listen-Index** gekoppelt
(`Coding.cs:3138-3141`: `QuantifiedMasks[i]` <-> `DinoDetections[i]`; `MultiModelAnalysisService.cs:401-407`:
`quantified[i]` <-> `Masks[i]`). Das ist fragil: Der Sidecar ueberspringt Boxen
(`sam_wrapper.py`: `skipped_boxes++` ohne Maske), wodurch die Masken-Liste kuerzer ist als die
DINO-Liste und ab der ersten uebersprungenen Box ALLE Indizes verrutschen. Ein Proximity-Filter,
der zusaetzlich Eintraege als "Voraus" aussortiert, verschaerft das. Diese lose Kopplung wird
durch eine feste Einheit ersetzt, die EINMAL korrekt zusammengebaut wird:

`SegmentedFinding { DinoDetectionDto? Dino; SamMaskResult Mask; QuantifiedMask Quant; MetrierungProximityResult Proximity }`

Aufbauregeln:

- Basis ist die **Masken-Liste** (`samResult.Masks`), NICHT die DINO-Liste. Iteriert wird ueber
  Masken — uebersprungene Boxen existieren dort gar nicht erst.
- `Mask` und `Quant` sind per Index **innerhalb derselben Masken-Liste** sicher gepaart
  (`QuantifyAll` erzeugt `QuantifiedMask` 1:1 aus `samResult.Masks` in gleicher Reihenfolge).
- `Dino` wird der Maske **ueber bbox+Label** zugeordnet, nicht per Listen-Index: Jede
  `SamMaskResult` traegt `label` und die (geclampte) `bbox` ihrer Input-Box
  (`sam_wrapper.py:146-149`). Zuordnung = DINO-Detection mit gleichem Label und hoechster
  bbox-Ueberlappung (IoU). Kein eindeutiger Match -> `Dino = null` (die Maske traegt Label,
  bbox und SAM-Score selbst als Fallback; nur die echte DINO-Confidence fehlt dann).
- `Proximity` wird per `MetrierungProximityEvaluator` gesetzt.

Ort: Der Record liegt in Infrastructure (referenziert die Infrastructure-DTOs
`SamMaskResult`/`DinoDetectionDto`/`QuantifiedMask`). Der Aufbau erfolgt an den Aufrufstellen.

**Alle nachgelagerten Schritte** (Overlay, Event-/`EnhancedFinding`-Erstellung, Suppressed-Zaehler)
arbeiten NUR noch auf `IReadOnlyList<SegmentedFinding>` — kein paralleles Index-Hantieren mehr.

## 6. Einhaeng-Punkte (gleiche Logik, zwei Stellen)

### 6.1 Live (Codiermodus)

In `PlayerWindow.Coding.cs`, `RunCodingAnalysisAsync`, nach `AnalyzeFrameAsync` (`:3002`):

1. Aus `SingleFrameResult` die `SegmentedFinding`-Liste bauen (Abschnitt 5.5).
2. Pro `SegmentedFinding` `Proximity` setzen.
3. `Codierbar` -> Overlay + Event mit Meter aus `_codingLastOsdMeter` (wie bisher, aber ueber die Einheit statt Index).
4. `Voraus` -> Overlay im "Voraus"-Stil (gestrichelt + Label "voraus"), **kein** Event, **kein** Meter.

Statusmeldung (`SetCodingAiState`):

- Wenn **alle** gefundenen Masken `Voraus` sind: `Ereignis voraus erkannt - naeher heranfahren`.
- Wenn **gemischt**: normale codierbare Befunde anzeigen, optional klein `N voraus ignoriert`.
- Wenn gar nichts gefunden: unveraendert "Kein Schaden erkannt".

### 6.2 Vollanalyse (Video-Batch)

In `MultiModelAnalysisService` nach `QuantifyAll` (`:392`), vor `new EnhancedFinding(...)`:

1. `SegmentedFinding`-Liste bauen, `Proximity` setzen.
2. Nur `Codierbar` -> `EnhancedFinding` erzeugen (wie bisher).
3. `Voraus` -> kein `EnhancedFinding` (fliesst nicht in Protokoll/Dedup/Meter).

Leichtgewichtiges Logging (kein UI, kein Log pro Maske):

- Pro Frame/Lauf einen Zaehler `ProximitySuppressedCount` mit Grund `ahead_of_camera` fuehren
  (im vorhandenen `trace`/Progress-Objekt, analog zu `Degraded`/`SkippedBoxes`).
- Hoechstens ein zusammenfassender Log-Eintrag pro Frame, nur falls `ProximitySuppressedCount > 0`.

## 7. Verhalten / UX

- `Voraus`-Befunde sind **sichtbar** (Overlay), damit der Inspekteur sieht, was kommt — aber klar als "noch nicht codierbar" markiert.
- `Voraus` erzeugt **keinen** Meter und **keinen** Protokoll-/Event-Eintrag.
- Faehrt die Kamera naeher, wird derselbe Befund in einem spaeteren Frame `Codierbar` und normal metriert/codiert.
- Konservativ: unklare zentrale Maske -> `Voraus`.

## 8. Tests

Unit-Tests fuer `MetrierungProximityEvaluator` (reine Logik, kein Sidecar):

- Tunnel-Fehlmaske (gross, zentral, enthaelt Center, keine Wandnaehe) -> `Voraus`.
- Nahe Muffe (gross, Wandnaehe) -> `Codierbar`.
- Wandschaden nah (klein, aussen, `distToVanish` hoch) -> `Codierbar`.
- Kleiner zentraler Fund weit voraus -> `Voraus`.
- Grenzfall an `T_fill` / `T_radial` / `wandToleranz` -> deterministisch, konservativ.
- ImageAspect != 1: radiale Distanz korrekt korrigiert.

Ort: `tests/AuswertungPro.Next.Pipeline.Tests/MetrierungProximityEvaluatorTests.cs`

Zusaetzlich Unit-Tests fuer die `SegmentedFinding`-Zuordnung
(`tests/AuswertungPro.Next.Pipeline.Tests/SegmentedFindingBuilderTests.cs`):

- 3 DINO-Boxen, SAM skippt die mittlere -> die 2 Masken werden den RICHTIGEN 2 DINO-Boxen
  zugeordnet (kein Index-Verrutschen).
- Maske ohne passende DINO-Box -> `Dino = null`, Einheit bleibt nutzbar (Label/bbox aus Maske).
- Zwei gleiche Labels mit unterschiedlichen Boxen -> Zuordnung ueber hoechste IoU, nicht ueber Reihenfolge.

Hinweis: passt zur Testregel (Recommendation-/Gate-Logik), analog `PipelineHealthEvaluatorTests`.

## 9. Geltungsbereich

- Beide Pfade: Live-Codiermodus **und** Vollanalyse.
- Alle Ereignistypen (querschnittsfuellend **und** Wandschaeden) — ein gemeinsames Kriterium deckt beide ab.

## 10. Nicht-Ziele

- Kein Frame-Tracking, keine Verfolgung eines Ereignisses ueber mehrere Frames.
- Keine absolute Vorausdistanz-Berechnung / kein "Meter = Kamera + Distanz".
- Keine Sidecar-/Modell-Aenderung.
- Keine Aenderung der OSD-Lese-Logik.
- Kein Tuning der DINO-/SAM-Schwellen (separates Thema).

## 11. Risiken / Trade-offs

- **Nahe Muffe vs. Tunnel** bleibt die schwierigste Unterscheidung. Mitigiert durch `wandnaehe` (Rand erreicht Wand/Bildrand) + konservativen Default. Feinschliff ueber die vier Parameter.
- **Kalibrierung noetig**: Ist `PipeCalibration` nicht gesetzt, faellt der Fluchtpunkt auf Bildmitte und der Rohrradius auf einen Default-Anteil zurueck; die Heuristik wird dann unschaerfer, bleibt aber konservativ.
- **Falsch-"Voraus"** (ein naher Befund wird als voraus eingestuft): bewusst akzeptiert, weil weniger schaedlich als ein falscher Metereintrag. Der Inspekteur kann den Frame erneut/naeher analysieren.
- **bbox+Label-Zuordnung statt Index**: Die geclampte Masken-bbox weicht leicht von der DINO-bbox ab; Zuordnung daher ueber hoechste IoU + Label. Bei zwei sehr aehnlichen Boxen mit gleichem Label theoretisch mehrdeutig — in der Praxis selten, und der Fallback `Dino = null` ist unkritisch (die Maske traegt Label/bbox/SAM-Score selbst; nur die echte DINO-Confidence fehlt dann fuer das QualityGate, das darueber renormalisiert).

## 12. Umsetzungsreihenfolge (fuer den Plan)

1. `MetrierungProximity`, `MetrierungProximityInput`, `MetrierungProximityResult`, `MetrierungProximityThresholds` (Application).
2. `MetrierungProximityEvaluator` + Unit-Tests (TDD).
3. `SegmentedFinding`-Record (Infrastructure) + Zusammenbau-Helfer (Masken-basiert, bbox+Label-Zuordnung) + Unit-Test fuer die Zuordnung inkl. skipped-box-Fall.
4. Live-Einhaengung in `RunCodingAnalysisAsync`: SegmentedFinding bauen, Proximity, "Voraus"-Overlay + Statusmeldung-Regel.
5. Vollanalyse-Einhaengung in `MultiModelAnalysisService`: SegmentedFinding bauen, nur Codierbar -> EnhancedFinding, `ProximitySuppressedCount`.
6. Build + Tests + manuelle Akzeptanzpruefung.

## 13. Akzeptanzkriterien

- Eine grosse zentrale Tunnel-Maske ohne Wandnaehe erzeugt **keinen** Meter/Eintrag (wird als "voraus" angezeigt).
- Eine nahe Muffe (Rand an der Wand) wird normal metriert/codiert.
- Ein Wandschaden im Aussenbereich (nahe Rohrwand) wird metriert/codiert.
- Ein kleiner zentraler Fund weit voraus wird **nicht** metriert.
- Die vier Schwellen sind ohne Code-Aenderung anpassbar.
- Evaluator-Tests laufen ohne Sidecar stabil.

## 14. Spec-Selbstpruefung

- Ansatz festgelegt (1). Geltung beide Pfade, alle Ereignistypen.
- Naehe-Regel mit Toleranz + konservativem Default definiert.
- Vier Parameter benannt, mit Defaults, kalibrierbar.
- Einhaeng-Punkte am Code verifiziert (Methodennamen existieren).
- Tunnel-vs-Muffe als Hauptrisiko benannt und mitigiert.

Getroffene Entscheidungen (Review 2026-06-03):

1. Vollanalyse: leichtgewichtiger Zaehler `ProximitySuppressedCount` (Grund `ahead_of_camera`), kein UI, kein Log pro Maske.
2. Live: Statusmeldung nur wenn ALLE Masken "Voraus" sind (`Ereignis voraus erkannt - naeher heranfahren`); bei gemischt normal + optional `N voraus ignoriert`.
3. Kopplung DINO/SAM/Quant nicht mehr per Index, sondern feste `SegmentedFinding`-Einheit (bbox+Label-Zuordnung, robust gegen skipped boxes).
