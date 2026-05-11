# Deep-Dive: Fehler und Robustheit — SewerStudio Codiermodus / KI-Pipeline

Datum: 2026-05-10 (spät, nach Bisect-Reset auf Anker)
Anker: `6b95343` (2026-05-07 22:54) — der **letzte bekannte funktionierende
Stand** des User-Workflows „Doppelklick auf Beobachtung → PlayerWindow →
BBox + SAM in-place → Akzeptieren → KB".
Origin: `0b54b85` (2026-05-10 spätabend, viele Verbesserungen aber
Workflow tot — nicht direkt bauen darauf).

## Zweck

Strukturierte Bilanz aller bekannten Bruchstellen + Robustheits-Aspekte
nach der heutigen Session (in der drei UI-Eingriffe revertet wurden und
sechs Pipeline-Robustifizierungen geliefert wurden). Vorbereitung für
das schrittweise Aufbauen ab 2026-05-11.

## Executive Summary

| Bereich | Zustand |
|---|---|
| User-Workflow (DataPage→PlayerWindow→BBox→SAM→KB) | funktioniert auf `6b95343`, tot auf `0b54b85` |
| Tests gesamt | 1056 grün auf `0b54b85`, davon 26 neue Helper-Tests heute |
| Build | 0 Warn / 0 Err konsistent |
| Architektur | zwei Codiermodus-Pfade koexistieren (PlayerWindow in-place + CodingModeWindow modal) |
| KI-Pipeline | Sidecar erreichbar, Qwen aktiv, SAM funktional — aber UI-Pfad fragil |
| Doku | 20+ Markdown-Files in Wurzel, 60+ in docs/adrs (inflation) |
| Hauptproblem | Slice 8a.3 Step 5b (`1997223`) hat den alten Workflow gelöscht ohne vollwertigen Ersatz |

## A — Fehler-Inventar (kritisch nach Schweregrad)

### Schweregrad A: App-killend / Workflow-tot

**A1. Slice 8a.3 Step 5b — alter In-Place-Coding-Mode gelöscht** (Commit
`1997223`, 2026-05-10 00:24)

- Gelöscht: `PlayerWindow.CodingTool.cs`, `PlayerWindow.CodingApply.cs`,
  `PlayerWindow.CodingEvents.cs`, `PlayerWindow.CodingMode.cs`,
  `PlayerWindow.CodingOverlayRender.cs`
- Ersatz: nur eine Bridge (`CodingMode_Click` → `CodingModeWindow.ShowDialog`)
- Folge: User-Workflow „Doppelklick auf Beobachtung → BBox im PlayerWindow"
  ist tot. Im neuen Pfad öffnet sich ein zweites Fenster (CodingModeWindow),
  in dem BBox-Drawing zudem durch andere Bugs (A2, A3, B*) verhindert wird.
- **Verbot für nächste Sessions**: 5b NICHT wiederholen ohne Workflow-Replacement.

**A2. CodingModeWindow Mouse-Event-Routing bei Show ohne Owner**

- Wenn `CodingModeWindow` als Top-Level-Window mit `Show()` ohne Owner
  geöffnet wird (heute Abend in Sub-Slice 2 versucht): OverlayCanvas
  bekommt keine Mouse-Events. Cursor bleibt Pfeil statt Cross. BBox tot.
- Workaround: `Owner = MainWindow` + `ShowDialog()`. Hat heute Abend
  trotzdem nicht zuverlässig funktioniert.
- Wahrscheinliche Ursache: WPF-Mouse-Routing-Subtilität in Modal-Hierarchien
  über VLC-HwndHost. **Ungeklärt.**

**A3. Frame-Freeze-Workaround → native Crash**

- Versuch heute Abend: VideoView.Visibility=Collapsed während Markieren-Tool
  aktiv, statisches Image stattdessen.
- Result: VLC-HwndHost reagiert mit nativer Exception. App stürzte ab.
- Lehre: `VideoView.Visibility=Collapsed` ist mit LibVLCSharp.WPF 3.9.5
  + LibVLC native 3.0.23 nicht sicher.

### Schweregrad B: Funktion falsch / unzuverlässig

**B1. BBox-/SAM-Koordinaten ignorieren Letterbox/Pillarbox**

- `PixelToNormalized` rechnet `canvasX / canvasWidth` — ignoriert die
  schwarzen Balken bei Stretch=Uniform.
- Folge: SAM bekommt Pixel an verschobener Stelle. Mask landet falsch.
- Heute Abend gefixt (mit Cache-Fallback in `ComputeVideoContentRect`),
  aber Fix lebt in `0b54b85` — auf `6b95343` Anker noch nicht drin.
- Test-Coverage: 7 Tests für `ComputeSourceFramePixelDiameter` heute.

**B2. Frame-Capture-Fallback liefert oft schwarz**

- `RenderTargetBitmap` auf VideoView/HwndHost liefert bei aktivem VLC
  oft schwarzes Bild — kommentiert im Code selbst.
- Folge: Wenn TakeSnapshot scheitert, geht ein schwarzer Frame an SAM/Qwen
  → falsche oder leere Klassifikation.
- Heute Abend gefixt (`IsFrameValid` Helligkeits-Spread-Check) — 10 Tests.

**B3. Auto-Kalibrierung One-Shot blind**

- `_calibrationAutoTried = true` wurde gesetzt, **bevor** klar war, ob
  der Frame brauchbar war. Erster Frame schwarz → nie mehr versuchen.
- Heute Abend gefixt: Retry-Limit (5 Versuche), Bitmap-Decode-Fail zählt
  nicht als Versuch.

**B4. Auto-Kalibrierung naiv für realen Inspektions-Frame**

- Algorithmus scannt horizontale Helligkeitskanten.
- Reale Frames haben: OSD-Text (oben/unten), schwarze Pillarbox-Balken
  (links/rechts), helle Wand vs. dunkler Rohrtunnel, Reflexionen.
- Algorithmus detektiert Pillarbox-Kanten als „Rohrwand" → falscher
  Diameter.
- Heute Abend teilweise gefixt: `DetectPillarboxPadding` + ROI-Beschränkung
  im Edge-Scan — 9 Tests.

**B5. Stale `_lastSamTightBbox`**

- Wert wurde nicht zurückgesetzt bei: neuer Geste, SAM-Fehler, leerer Maske.
- Folge: Trainings-Export nutzt eventuell alte Tight-BBox von einer
  früheren BBox-Geste.
- Heute Abend gefixt: 4 Reset-Punkte.

**B6. Manuelle Kalibrierung im Canvas-Pixel statt Source-Frame-Pixel**

- `ApplyCalibration` rechnete `Math.Sqrt(...)` auf Canvas-Pixel-Distanz.
- Bei Letterbox-Video: Canvas-Pixel ≠ Source-Frame-Pixel — Pipe-Pixel-
  Diameter im Service stimmte nicht.
- Heute Abend gefixt: `ComputeSourceFramePixelDiameter` + Pre-Capture im
  `BtnCalibrate_Checked`.

**B7. Meterstand-Diskrepanz VM vs. Video-OSD** ⚠️ **UNGEKLÄRT**

- Heute Mittag beobachtet: VM zeigt 1.45m, Video-OSD zeigt 0.71m.
- Heute Abend nochmal: 1.60m VM vs. 0.71m OSD.
- Ursache unklar — `SyncVideoToMeter` rechnet Meter→Video-Time-Fraction,
  aber irgendwo bricht die Beziehung.
- **Offen für nächste Session**: tiefere Analyse der OSD-Meter-Erkennung
  und der Sync-Logik.

### Schweregrad C: Architektur / Maintainability

**C1. Zwei Codiermodus-Pfade**

- Alt: `PlayerWindow` mit In-Place-Coding-Partials (gelöscht in 5b, lebt
  noch auf `6b95343`-Anker).
- Neu: `CodingModeWindow` als modaler Dialog über Bridge.
- Beide existieren auf `0b54b85`. Auf `6b95343` lebt der alte Pfad
  vollwertig, neu nur als Bridge.
- **Entscheidung offen**: konsolidieren auf einen Pfad — welcher?

**C2. Domain-INPC Tech-Debt**

- `HaltungRecord` und `SchachtRecord` implementieren `INotifyPropertyChanged`
  (UI-Pattern in Domain-Schicht).
- ADR `2026-05-10-p2-1-domain-inpc-decouple.md` dokumentiert 5-Step-
  Migration (3-5 Sessions).
- Blockiert Headless-Use (Sidecar/CLI).

**C3. UI/Ai/* lebt in UI-Schicht**

- 23 .cs-Files unter `src/AuswertungPro.Next.UI/Ai/`.
- Sollte in Infrastructure-Schicht laut Clean Architecture.
- Audit 2026-04-23 markiert das als „CRITICAL".

**C4. SyncCodingToPrimaryDamages dupliziert**

- Lebt in `PlayerWindow.ImportProtocol.cs` (für Bridge-Pfad).
- Heute Abend dupliziert in `DataPageViewModel.MediaProtocol.cs` (für
  Direkt-Öffnung-Pfad).
- Gehört eigentlich ins Application-Layer.

**C5. PlayerWindowOptions, DamageMarkerInfo, PlayerDamageOverlayData**

- Records leben in `PlayerWindow.xaml.cs` (Z.43-90).
- Wenn `PlayerWindow` gelöscht würde (Slice 8a.5.4), sind sie weg —
  obwohl andere Caller sie noch nutzen.
- Sollte extrahiert werden in eigene Datei vor PlayerWindow-Removal.

### Schweregrad D: Test-Lücken

**D1. WPF-UI-Smoke nicht automatisiert**

- Aktuelle Tests sind alle Unit-Tests + Architektur-Guards.
- Kein Test der `OverlayCanvas_MouseLeftButtonDown` triggert oder
  BBox-Drag durchspielt.
- Tool-Optionen: FlaUI, Microsoft.UI.Xaml.Tests, Appium.
- **Ohne UI-Smoke**: Build/Tests grün → keine Garantie dass App
  funktioniert. (Heute mehrfach bewiesen.)

**D2. SAM-/Qwen-Sidecar-Integration nicht in CI**

- Sidecar (Python FastAPI auf :8100) wird im pytest-Pfad getestet,
  aber nicht im C#-Build.
- Integration-Tests: keine.
- Wenn Sidecar-Protokoll ändert, brechen Caller still.

**D3. LibVLC-Native-Lifecycle nicht getestet**

- `MediaPlayer.Dispose`-Race, `IsHitTestVisible` auf HwndHost,
  `Visibility=Collapsed` auf VideoView — alles im Code als „weiß nicht
  ob das nativ kracht" kommentiert.
- Tests können das nicht abdecken (kein VideoView im Test-Process).

**D4. Frame-Capture-Fehlerpfade nicht reproduzierbar getestet**

- `IsFrameValid` hat 10 Tests heute (gut).
- Aber: Wann liefert `TakeSnapshot` null, wann schwarz? Nur durch reale
  VLC-States reproduzierbar.

## B — Robustheit-Inventar (was hält)

### Solid (keine offenen Probleme)

- **Domain-Schicht** — keine UI-Referenzen außer INPC-Debt (C2).
- **ProcessRunner.RunAsync** — async, sicher, ArgumentList statt String-
  Konkatenation (kein Command Injection).
- **ProjectPathResolver** — Pfad-Containment-Check, Sanitizing.
- **Sidecar Auth fail-closed** — kein Token, kein Service (außer Dev-Modus).
- **PdfPig + WinCan-Import** — 836 Pipeline-Tests, robuste Fallback-Kette.
- **Helper-Pure-Functions** (heute Abend):
  - `IsFrameValid` (10 Tests)
  - `ComputeSourceFramePixelDiameter` (7 Tests)
  - `DetectPillarboxPadding` (9 Tests)

### Fragil-aber-funktioniert

- **LibVLC-MediaPlayer-Lifecycle** im PlayerWindow: `TrySeekRobust`-Pattern
  fängt native AccessViolations bei Buffering/Codec-Wechsel.
- **TakeSnapshot mit Wartelogik**: 1.5s Wait + File-Size-Check.
- **Auto-Calibration-Retry-Limit** (heute eingeführt): 5 Versuche.
- **Sidecar lazy-init** im CodingModeWindow: bei Sidecar-Down kein Crash.
- **OllamaClient mit Polly Retry + Circuit Breaker**: defensiv.

### Versteckt-Fragil (Risiko nicht offensichtlich)

- **Workflow-Pfad-Hierarchie**: DataPage → PlayerWindow → Bridge →
  CodingModeWindow. Alte+neue Pfade koexistieren auf `0b54b85`. Welcher
  läuft? Hängt von User-Aktion ab. Schwer zu debuggen.
- **`_lastSamTightBbox`-Lifecycle**: Reset-Punkte heute hinzugefügt, aber
  was passiert wenn neue Code-Pfade hinzukommen?
- **Video-Dimensionen-Cache** (`_videoFrameWidthCache/Height`): erst
  nach erstem Capture verfügbar. Vorher Fallback auf Canvas (Letterbox-
  unsicher). Edge-Case: User kalibriert vor erstem Capture.
- **WPF-Airspace bei VLC-VideoView**: `IsHitTestVisible="False"` ist
  gesetzt, aber LibVLCSharp.WPF 3.9.5 hat bekannte Bugs wo das nicht
  respektiert wird. Verhalten unzuverlässig.

## C — Heutige Fehler-Genealogie (chronologisch)

| Zeit | Aktion | Effekt |
|---|---|---|
| 00:24 | Slice 8a.3 Step 5b commited (`1997223`) | alter PlayerWindow-In-Place-Coding gelöscht |
| Vormittag | Sub-Slice 2: DataPage öffnet CodingModeWindow direkt | Mouse-Routing-Bug — BBox tot |
| Mittag | Show vs. ShowDialog + Owner-Versuch | hilft nicht zuverlässig |
| Nachmittag | Frame-Freeze-Workaround | native Crash |
| Abend | Sub-Slice 1: Player-Toolbar im CodingMode | BBox kaputt durch Outer-Grid-Layout-Touch — Ursache ungeklärt |
| Abend | Robustifizierungen .A-.E + 26 Tests | wertvoll, aber löst BBox nicht |
| Spät | Sub-Slice .F.1 (Buttons in Steuerung-Row) | BBox auch dort kaputt — revert |
| 23:50 | Bisect-Reset auf `6b95343` (2026-05-07) | Workflow läuft wieder ✓ |

## D — Empfehlungen für nächste Sessions

### Phase 1: Schadensbegrenzung (sicher, klein, eilig)

1. **Anker schützen**: `6b95343` ist der funktionierende Stand. Lokale
   Working-Tree-Touches auf diesem Stand nur **per Cherry-Pick**, nicht
   per `git pull` von `0b54b85`.
2. **Memory-Direktive verstärken**: heute geschrieben. Vor Re-Try von
   Slice 8a.3 Step 5b → explizit lesen.
3. **Stand-Indikator**: User braucht eine einfache Anzeige in der App
   (z.B. Window-Title) welche Commit-Hash gerade läuft. Verhindert
   „wir glaubten 0b54b85 läuft, war aber alter Cache".

### Phase 2: Helper cherry-picken (Pure-Functions, niedriges Risiko)

Aus `0b54b85` einzeln auf `6b95343`-Basis übernehmen, **ohne** die
Caller-Umstellungen:

1. `AutoCalibrationService.DetectPillarboxPadding` + ROI-Anwendung
   in `TryAutoCalibrate` (9 Tests, statisch testbar)
2. `CodingModeWindow.IsFrameValid` als statische Helper-Methode (10 Tests)
3. `CodingModeWindow.ComputeSourceFramePixelDiameter` (7 Tests)
4. `IVideoPlaybackBackend` + Controller Lifecycle-Events
   (LengthChanged/EncounteredError/FirstPlayingOnce + 4 Tests)

Diese 30 Tests sind **alle** als Cherry-Picks ohne Workflow-Risiko
möglich.

### Phase 3: gezielte Bugfixes mit User-Smoke (vorsichtig)

Auf das alte PlayerWindow.Coding*-Pfad applizieren, **nicht** auf
CodingModeWindow (das wird nicht der primäre Workflow):

5. Frame-Capture-Härtung (.A) → `PlayerWindow.Snapshot.cs` oder
   wo Capture lebt
6. Stale `_lastSamTightBbox`-Reset (.B) → `PlayerWindow.CodingTool` oder
   wo SAM-Pfad lebt
7. Auto-Kal-Retry-Limit (.D) → `AutoCalibrationService`-Caller

Pro Bugfix: User-Smoke pflicht — BBox + SAM weiter funktionsfähig?

### Phase 4: kritische ungelöste Fragen

8. **Meterstand-Diskrepanz B7** klären — VM 1.45m vs. OSD 0.71m
9. **Letterbox/Pillarbox bei manueller Kalibrierung** — wie wird das
   im alten Workflow gehandhabt? Vermutlich gar nicht, also Helper
   integrieren wenn Workflow-konform.
10. **CodingModeWindow vs. PlayerWindow** — explizite User-Entscheidung
    nötig: Welcher Pfad bleibt? Beide? Wenn ja, klare Use-Case-Trennung.

### Phase 5: Architektur-Konsolidierung (groß, mehrere Sessions, eigene ADR)

11. Domain-INPC raus (ADR `2026-05-10-p2-1-domain-inpc-decouple.md`)
12. UI/Ai/* nach Infrastructure migrieren
13. `PlayerWindowOptions`/`DamageMarkerInfo` extrahieren

## E — Anti-Patterns (NICHT machen)

- **Mehrere Slices in einer Session bei UI-Touch** — heute mehrfach
  bewiesen dass das in Reverts endet.
- **Outer-Grid-Layout-Restruktur im CodingModeWindow** ohne UI-Smoke —
  BBox geht kaputt aus ungeklärten Gründen.
- **Frame-Freeze mit `VideoView.Visibility=Collapsed`** — native Crash.
- **DataPage-Caller-Umstellung von PlayerWindow → CodingModeWindow ohne
  Workflow-Replacement** — User-Workflow stirbt.
- **Cherry-Pick von Slice 8a.3 Step 5b** — Workflow-Killer.
- **Build/Tests grün als „funktioniert"-Beweis akzeptieren** ohne UI-Smoke
  — Pflicht-Disziplin.

## F — Was heute objektiv gewonnen wurde (trotz Flickenteppich-Eindruck)

- **Tiefenanalyse** mit 8 Bruchstellen identifiziert (extern + intern
  bestätigt)
- **26 neue Unit-Tests** für Pure-Functions (alle grün)
- **ADR** `2026-05-10-coding-mode-robustifizierung.md` mit 6-Slice-
  Roadmap und Risiko-Schätzung
- **Hartes Wissen**: Slice 8a.3 Step 5b war der Workflow-Killer
- **Bisect-Anker**: `6b95343` als gesicherter Punkt
- **Memory aktualisiert** mit klaren Verboten für nächste Sessions

## G — Was im Stash liegt (Recovery-Material)

`git stash show stash@{0}`:
- `CodingModeWindow.xaml.cs` Diagnose-Slice (MouseDown/MouseUp Status-Output,
  SAM-Diagnose mit StringBuilder)
- `CodingModeWindow.FrameCapture.cs` mit User-Warmup-Task
  (`EnsureVideoFrameDimensionsAsync`)
- Diverse weitere Edits aus heutigen Sub-Slices

**Verwendung**: NICHT pauschal poppen (würde Workflow wieder brechen).
Selektiv extrahieren wenn einzelne Snippets nützlich (z.B. Diagnose-Code
zum manuellen Hineinhängen wenn ein konkretes Symptom analysiert wird).

## H — Klare Don'ts für 2026-05-11+

1. ⛔ Slice 8a.3 Step 5b NICHT wiederholen ohne expliziten Workflow-Ersatz
2. ⛔ Mehrere UI-Slices ohne Smoke zwischen jeden
3. ⛔ Direkt `git pull` von `0b54b85` ohne Cherry-Pick-Strategie
4. ⛔ Frame-Freeze mit `VideoView.Visibility=Collapsed`
5. ⛔ DataPage-Caller von PlayerWindow → CodingModeWindow ohne dass
   Codiermodus den vollen Workflow erfüllt

## I — Klare Do's für 2026-05-11+

1. ✅ Auf `6b95343` aufbauen, **klein-mechanisch** pro Slice
2. ✅ Helper-Pure-Functions cherry-picken (30 Tests im Paket)
3. ✅ Pro Slice: Build + Tests + **manueller UI-Smoke** vor Commit
4. ✅ Memory-Direktiven vor jedem Slice neu lesen
5. ✅ Bei Symptom-Diagnose: erst gezielte Status-Telemetrie einbauen,
   dann fixen
6. ✅ Bei Architektur-Entscheidung: ADR vorher, nicht parallel

---

**Status 2026-05-10 23:55**: Anker steht, App läuft, Memory aktuell.
Heute Abend Schluss. 2026-05-11 frisch, mit Disziplin.
