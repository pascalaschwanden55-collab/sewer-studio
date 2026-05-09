# ADR-Skizze: Slice 8a — Coding-Mode-Konsolidierung

Datum: 2026-05-09
Status: **In Diskussion** — Frage 1 beantwortet (DataPage-Pfad wichtig), 2+3 offen
Branch: `feature/pdf-import-beobachtungen`

## Kontext

Im Programm gibt es **zwei Coding-Workflows**, die historisch parallel
gewachsen sind und sich heute stark ueberlappen:

| Datei | Zeilen | Rolle |
|---|---|---|
| `CodingModeWindow.xaml.cs` (+ `CodingModeWindow.xaml`) | 3893 | Eigenes Coding-Fenster, von der DataPage aus geoeffnet |
| `PlayerWindow.CodingMode.cs` (Partial von `PlayerWindow`) | 2998 | Coding-Modus innerhalb des PlayerWindow (Phase 6.1.E) |
| Zusammen | **6891** | Coding-Workflow gesamt |

Beide Pfade haben **eigene Implementierungen** fuer:

- `RenderAiOverlays` (KI-Overlays auf Canvas)
- `UpdateStatistics` (Fortschritts-Anzeige)
- `UpdateUi` / `UpdateOverlayInfo`
- `StartAiStatusPulse` / `StartCodingAiPulse` (KI-Status-Animation)
- `RenderPreview` / `RenderOverlayGeometry` (User-Zeichnungen)
- `UpdateDefectDetailPanel` / `UpdateCodingDefectDetailPanel`
- Hotkey-Handling (in `PlayerWindow.Hotkeys.cs` zentral, in `CodingModeWindow`
  separat per `Window_KeyDown`)

Die **Schadens-Zeichenwerkzeuge** (OverlayToolService) werden bereits geteilt
(seit Slice 33 als Partial extrahiert). Die **Session-Logik**
(`CodingSessionService`) ist ebenfalls geteilt. Die Diskrepanz liegt
hauptsaechlich in der **UI-Schicht**: Wer rendert was auf welcher Canvas?

## Problem

Doppelpflege: Wenn ein Bug im Overlay-Rendering gefunden wird, muss er
zweimal gefixt werden — einmal in `CodingModeWindow.RenderAiOverlays` und
einmal in `PlayerWindow.CodingMode.RenderAiOverlays`. Beide haben sich im
Detail auseinanderentwickelt (unterschiedliche Brush-Caches, unterschiedliche
Tag-Strategien, unterschiedliche Reihenfolge bei `Children.Add`).

Hotkey-Konflikte: Im PlayerWindow gilt `PlayerWindow.Hotkeys.cs` fuer
**alles** — auch fuer die Coding-Aktionen (z.B. `M` fuer Mark, `B` fuer BBox).
Im `CodingModeWindow` gilt der lokale `Window_KeyDown`. Wenn beide Windows
gleichzeitig offen sind (was vorkommt: PlayerWindow als Video-Quelle,
CodingModeWindow als Codier-Cockpit), sind die Hotkey-Routen unklar.

State-Verteilung: `CodingSessionService` haelt `Events` und `CurrentMeter`.
Beide Windows lesen daraus, beide schreiben rein. Bei Race Conditions
(z.B. User scrubbt im Player waehrend CodingMode den Meter sucht) kann der
State inkonsistent werden.

## Optionen

### A — Konsolidierung auf PlayerWindow (Einzel-Window-Strategie)

`CodingModeWindow` wird ersatzlos geloescht. Der Coding-Workflow lebt nur
noch im PlayerWindow als `CodingMode`-Modus, der per Toolbar-Button oder
Hotkey aktiviert wird.

**Pro:**
- Eine Ground Truth fuer Overlay-Rendering, Hotkeys, State.
- Spart ~3900 Zeilen Code (mit XAML-Markup ~5000+ Zeilen).
- Koppelt Codieren direkt an die Video-Wiedergabe (was sowieso der
  natuerliche Workflow ist).

**Contra:**
- User muss umlernen: Coding-Workflow startet ueber Player und nicht ueber
  DataPage.
- Wenn der User mit zwei Bildschirmen arbeitet (Video links, Codier-
  Cockpit rechts), waere ein extra Fenster eigentlich praktischer.
  Loesungsvorschlag: PlayerWindow als Sub-Window `Show()`-bar, sodass es
  auf einem zweiten Monitor liegen kann waehrend DataPage links bleibt.

### B — Konsolidierung auf CodingModeWindow (Separate-Window-Strategie)

`PlayerWindow.CodingMode.cs` wird ausgehoehlt. Das PlayerWindow bleibt
ein reiner **Video-Player** (Wiedergabe + Mark/BBox-Zeichnen),
das `CodingModeWindow` uebernimmt allen Coding-Workflow.

**Pro:**
- Klare Trennung: Player macht Video, CodingModeWindow macht Codes.
- Zwei-Monitor-Setup natuerlich (Video links, Cockpit rechts).
- PlayerWindow wird wieder schlanker (von 8000+ auf ~5000 Zeilen).

**Contra:**
- Zwei Windows muessen synchronisiert bleiben (Meter aus Video → Coding,
  Sprung zu Code → Meter im Video).
- IPC-aehnliche State-Synchronisation noetig (heute: ueber den
  CodingSessionService geteilt — aber unsauber, weil PlayerWindow schon
  eigene Session-Felder hat).

### C — Status quo + Shared-Component-Layer (Pragmatismus)

Beide Windows bleiben, aber der duplizierte Code wandert in **shared
Components**:

- `CodingOverlayRenderer` als `UserControl` mit eigenen Render-Methoden.
- `CodingStatisticsPanel` als `UserControl`.
- `CodingDefectDetailPanel` als `UserControl`.
- `CodingHotkeyHandler` als reiner Service mit `OnKey(Key, EventArgs)`-API,
  beide Windows leiten ihre `KeyDown`-Events dahin durch.

**Pro:**
- Risikoarm: Keine Window-Migration.
- Loest Doppelpflege-Problem ohne Workflow zu aendern.
- Inkrementell: pro Sprint ein Component extrahieren.

**Contra:**
- Lange Migration (~5-8 Sprints).
- State-Verteilung bleibt unsauber.

## User-Antworten (2026-05-09)

**Frage 1 — DataPage-Pfad wichtig?** → **JA, sehr wichtig.**

Damit ist **Option A (Konsolidierung auf PlayerWindow) ausgeschlossen** —
wenn der DataPage → CodingModeWindow-Workflow primaer ist, darf das
CodingModeWindow nicht ersatzlos verschwinden.

Verbleibende Optionen:
- **Option C** (Shared-Component-Layer) — Doppelpflege loesen ohne
  Workflow-aenderung. Endzustand: beide Windows bleiben.
- **Option B** (Konsolidierung aufs CodingModeWindow) — PlayerWindow.
  CodingMode wird ausgehoehlt, CodingModeWindow uebernimmt allein.
  Erfordert Antwort auf Frage 2 (Zwei-Monitor-Workflow).

## Empfehlung (zur Diskussion, aktualisiert)

**Option C als Endzustand.**

Begruendung:
1. DataPage-Pfad ist primaer, also bleibt CodingModeWindow als
   eigenstaendiges Window.
2. PlayerWindow.CodingMode wird trotzdem genutzt (z.B. wenn User direkt
   aus Video heraus codiert) — also bleibt auch der zweite Pfad.
3. Doppelpflege wird durch Shared-UserControls geloest, ohne irgendeinen
   Workflow zu brechen.
4. Kein State-Big-Bang — inkrementell pro Sprint ein Component.

Option B bleibt nur dann attraktiv, wenn die Zwei-Monitor-Antwort kommt
und PlayerWindow.CodingMode in der Praxis kaum genutzt wird.

## Konkrete naechste Schritte (Option C)

| Sprint | Aktion |
|---|---|
| 1 | `CodingOverlayRenderer` als `UserControl` mit Schnittstelle `IRenderTarget` (Canvas + Brushes + Layer-Tags). Beide Windows nutzen die gleiche Instanz. |
| 2 | `CodingStatisticsPanel` als `UserControl` mit `ISessionStats`-DataContext. |
| 3 | `CodingDefectDetailPanel` als `UserControl`. |
| 4 | `ICodingHotkeyHandler` Service: zentrale Registrierung, beide Windows routen Tastatur-Events. |
| 5 | `ICodingSessionState`-Interface (gemeinsamer State-Lese-Pfad). Schreibender Pfad bleibt im SessionService. |
| 6 | (Option-A geloescht — DataPage-Pfad bleibt erhalten.) |

## Offene Fragen fuer den User

1. ~~Ist der heutige **DataPage → CodingModeWindow**-Pfad noch wichtig?~~ →
   **JA. CodingModeWindow bleibt.**
2. Wird der **Zwei-Monitor-Workflow** regelmaessig genutzt (Player auf
   Monitor 1, CodingModeWindow auf Monitor 2)? Falls ja, sollte
   PlayerWindow.CodingMode-Pfad evtl. ganz weg (Option B). Falls nein,
   bleiben beide Pfade (Option C).
3. Welche Hotkeys sollen "winning" haben, wenn beide Windows gleichzeitig
   im Fokus konkurrieren? (Heute eher Zufall.)

Sobald Frage 2 beantwortet ist, kann ein konkreter Plan (mit
Slice-Aufteilung wie Slice 8a.1, 8a.2 usw.) geschrieben werden.
