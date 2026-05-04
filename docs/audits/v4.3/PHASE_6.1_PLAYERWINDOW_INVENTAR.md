# Phase 6.1 — PlayerWindow zerlegen (Inventar + Empfehlung)

**Datum:** 2026-05-04
**Auftrag:** "PlayerWindow.xaml.cs in Partials zerlegen (9856 Zeilen)" — Audit A2 (Konsens 3/3, ~1 Woche).
**Resultat:** Inventar + Modul-Vorschlag. KEIN Code-Eingriff.

---

## A. Bestand

| File | Zeilen |
|---|---:|
| `PlayerWindow.xaml.cs` | **9.860** |
| `PlayerWindow.xaml` | 1.592 |
| `PlayerWindow.TrainingMode.cs` (bereits ausgelagerte Partial) | 692 |
| **Total** | **12.144** |

**Metriken:**
- ~185 Methoden
- ~47 private Felder
- 1 Klasse, hauptsaechlich CodeBehind-Logik

**Kontext:** Audit hat in Erstauflage 8.581 Zeilen gemeldet. Heute 9.860 — die Datei waechst trotz Code-Reviews. Ohne Strukturmassnahme weiter Risiko.

---

## B. Verantwortlichkeiten (was alles in PlayerWindow.xaml.cs lebt)

Aus den Methoden- und Field-Namen ableitbar (grobe Cluster):

### B1. Video-Playback / VLC-Steuerung
- LibVLC-Player-Lifecycle, Timer, ScrubTimer, Marquee
- Pause, Play, Skip, FrameStep, Speed
- ~20% des Files

### B2. Codier-Modus (Live-Codierung Frame fuer Frame)
- _codingVm, OverlayCanvas, BtnCodingLiveAi, OnCodingPause, ...
- Live-KI-Aufrufe, Quality-Gate-Trigger
- ~25% des Files

### B3. KI-Detection / Overlay-Rendering
- AnalyzeWithOverlayHintAsync, RenderDetections, SAM-Mask-Rendering
- _currentMmResult, _previewMmResult, _selectedMaskIndex
- ~20% des Files

### B4. Feedback-Loop (Self-Improving)
- SavePositiveFeedbackAsync, SaveNegativeFeedbackAsync (Phase 0.3 migriert)
- _feedbackHttp, ReviewQueue-Integration
- ~10% des Files

### B5. Snapshot / Frame-Export
- Frame-zu-Bitmap, Save-Frame-Funktionalitaet
- TempPath-Cleanup
- ~5% des Files

### B6. UI-State-Management
- Window-Closing, Window-State, ResizeBehavior
- Settings-Persistenz (WindowStateManager)
- ~5% des Files

### B7. Eingebauter TrainingMode
- Bereits in `PlayerWindow.TrainingMode.cs` (692 Zeilen) — Praezedenzfall fuer Aufteilung
- ~5% des Files (im TrainingMode.cs)

### B8. Cross-Cutting (Hilfsmethoden)
- Logging, Error-Handling, Diagnose
- ~10% des Files

---

## C. Vorgeschlagene Aufteilung in Partials / Helper

### Module-Vorschlag

| Modul | Soll-Inhalt | Zielgroesse |
|---|---|---:|
| `PlayerWindow.xaml.cs` (Shell) | Konstruktor, Window-Lifecycle, Field-Deklarationen | 500-800 Zeilen |
| `PlayerWindow.VideoPlayback.cs` (Partial) | B1 — VLC-Steuerung, Timer | 1.500 Zeilen |
| `PlayerWindow.CodingMode.cs` (Partial) | B2 — Codier-Modus | 2.000 Zeilen |
| `PlayerWindow.AiDetection.cs` (Partial) | B3 — KI-Aufrufe, Overlay-Rendering | 1.800 Zeilen |
| `PlayerWindow.Feedback.cs` (Partial) | B4 — Feedback-Loop | 700 Zeilen |
| `PlayerWindow.Snapshot.cs` (Partial) | B5 — Frame-Export | 400 Zeilen |
| `PlayerWindow.UiState.cs` (Partial) | B6 — Window-State | 400 Zeilen |
| `PlayerWindow.TrainingMode.cs` (existiert) | B7 — Training | 692 Zeilen |
| `PlayerWindow.cs` (Helper) | B8 — Logging-Helpers, statische Methoden | 300 Zeilen |

**Total nach Migration:** ~8.000-9.000 Zeilen verteilt auf 9 Partials. Pro File ~700-2.000 Zeilen — wartbar.

### Alternative: Controller-Pattern statt Partials

Audit-Empfehlung Codex (Befund 1.2): Player als Shell + Controllers:
- `VideoPlaybackController`
- `CodingSessionController`
- `AiDetectionController`
- `Overlay/MeasurementService`

Vorteil: testbar (Controller ist nicht WPF-gebunden), wiederverwendbar.
Nachteil: groesserer Eingriff, Controller braucht Dependency-Injection (wartet auf Phase 5.1).

---

## D. Risiken

| Risiko | Wirkung | Gegenmittel |
|---|---|---|
| Field-Sichtbarkeit zwischen Partials | partial classes teilen Felder, kein Issue | — |
| Methoden-Doppelung beim Verschieben | Compile-Fehler | Schrittweise pro Modul, Tests pro Schritt |
| WPF-Markup-Compile fuer Partials | Mehr-Round-Trip | Akzeptabel, einmalig beim Build |
| Branch-Konflikt mit anderen Eingriffen | Massen-Merge | In eigener Session/Branch, kein Parallel-Refactor |
| Phase 5.3-Wechselwirkung | Player ruft KI direkt — wird schwieriger nach 5.3 | 6.1 vor oder nach 5.3, nicht parallel |

---

## E. Empfohlener gestaffelter Pfad (~5-7 Tage)

### Sub-Phase 6.1.A: Vorbereitung (~4 h)
- Partials-Skelette anlegen (`*.AiDetection.cs`, etc.).
- Cluster-Methoden identifizieren (suchen nach Methoden-Praefixen).

### Sub-Phase 6.1.B: B5 / B6 / B8 zuerst (~1 Tag)
- Snapshot, UiState, Helpers — kleine Cluster, wenig Abhaengigkeiten.

### Sub-Phase 6.1.C: B4 Feedback-Loop (~4 h)
- Klar abgegrenzte Funktion, leicht zu verschieben.

### Sub-Phase 6.1.D: B1 Video-Playback (~1 Tag)
- VLC-Lifecycle, Timer.

### Sub-Phase 6.1.E: B3 AI-Detection (~1 Tag)
- KI-Aufrufe, Overlay-Rendering. Komplex wegen Detection-State.

### Sub-Phase 6.1.F: B2 Coding-Modus (~1 Tag)
- Groesster Cluster, am komplexesten.

### Sub-Phase 6.1.G: Tests + Live-App-Test (~1 Tag)
- Manuelle UI-Tests durchklicken (Codieren, KI starten, Feedback geben).

**Total: ~5-7 Tage, statt 1 Woche geplant — realistisch fuer einen sauberen Eingriff.**

---

## F. Reihenfolge mit Phase 5.3 / 6.2

Wechselwirkung:
- **5.3** zieht KI aus UI raus — PlayerWindow ruft danach Interfaces, nicht Konkrete-Klassen.
- **6.1** zerlegt PlayerWindow in Partials.
- **6.2** zerlegt TrainingCenterVM (gleiche Strategie).

**Empfehlung:** **5.3 → 6.1 → 6.2**.
- Nach 5.3 ruft PlayerWindow nur Interfaces. Partials werden kleiner und sauberer.
- 6.1 und 6.2 koennen parallel sein (verschiedene Klassen).

**Alternative:** **6.1 → 5.3 → 6.2** wenn Player-Refactor zuerst gewollt ist (wartet nicht auf 5.3-Migration).

---

## G. Akzeptanz

- PlayerWindow.xaml.cs verifiziert: 9.860 Zeilen, ~185 Methoden, ~47 Felder.
- 8 Verantwortlichkeits-Cluster identifiziert.
- Module-Vorschlag mit 9 Partials + Aufwandsschaetzung.
- Praezedenzfall: PlayerWindow.TrainingMode.cs (692 Zeilen, schon ausgelagert).
- Reihenfolge-Empfehlung: 5.3 → 6.1 → 6.2.
- ⏸️ Migration in **eigener Mehr-Tages-Session** mit User-Freigabe und Branch-Strategie.
- KEIN Code-Eingriff in dieser Iteration.
