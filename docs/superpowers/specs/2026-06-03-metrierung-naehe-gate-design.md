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

### 5.5 Adapter zu den Detektions-Typen

Kleine Mapper, die aus den vorhandenen Typen einen `MetrierungProximityInput` bauen:

- Live: aus `DinoDetectionDto` + `SamResponse.ImageWidth/Height` + `PipeCalibration`.
- Vollanalyse: aus `QuantifiedMask` (CentroidX/Y, Box) + Bildmasse + `pipeDiameterMm`.

Ort: bei den jeweiligen Aufrufstellen (Infrastructure/UI), nicht in Application.

## 6. Einhaeng-Punkte (gleiche Logik, zwei Stellen)

### 6.1 Live (Codiermodus)

In `PlayerWindow.Coding.cs`, `RunCodingAnalysisAsync`, nach `AnalyzeFrameAsync` (`:3002`), vor `ShowMultiModelResults`/`AddMultiModelFindingsAsEvents`:

- Pro Befund `Evaluate(...)`.
- `Codierbar` -> wie bisher: Overlay + Event mit Meter aus `_codingLastOsdMeter`.
- `Voraus` -> Overlay im "Voraus"-Stil (gestrichelt + Label "voraus"), **kein** Event, **kein** Meter.

### 6.2 Vollanalyse (Video-Batch)

In `MultiModelAnalysisService` nach `QuantifyAll` (`:392`), vor `new EnhancedFinding(...)`:

- Pro quantifizierter Maske `Evaluate(...)`.
- `Codierbar` -> `EnhancedFinding` wird erzeugt (wie bisher).
- `Voraus` -> kein `EnhancedFinding` (der Befund fliesst nicht in Protokoll/Dedup/Meter).
  - Optional (nicht in Iteration 1): Voraus-Faelle zaehlen/loggen fuer Diagnose.

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

## 12. Umsetzungsreihenfolge (fuer den Plan)

1. `MetrierungProximity`, `MetrierungProximityInput`, `MetrierungProximityResult`, `MetrierungProximityThresholds` (Application).
2. `MetrierungProximityEvaluator` + Unit-Tests (TDD).
3. Live-Einhaengung in `RunCodingAnalysisAsync` + "Voraus"-Overlay-Stil.
4. Vollanalyse-Einhaengung in `MultiModelAnalysisService`.
5. Build + Tests + manuelle Akzeptanzpruefung.

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

Offen fuer die Freigabe:

1. Sollen "Voraus"-Faelle in der Vollanalyse fuer Diagnose mitgezaehlt/geloggt werden (oder erst spaeter)?
2. Soll der Live-Modus bei einem reinen "Voraus"-Ergebnis eine kurze Statusmeldung zeigen ("Ereignis voraus — naeher heranfahren"), oder genuegt das Overlay?
