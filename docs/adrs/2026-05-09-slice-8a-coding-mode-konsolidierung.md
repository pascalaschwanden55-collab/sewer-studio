# ADR-Skizze: Slice 8a — Coding-Mode-Konsolidierung

Datum: 2026-05-09
Status: **Entschieden** — Option **B.1** (Konsolidierung aufs CodingModeWindow). PlayerWindow faellt **ganz weg** — das CodingModeWindow hat eh einen vollwertigen Video-Player eingebaut.
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

**Frage 2 — Zwei-Monitor-Workflow regelmaessig?** → **JA.**

**Frage 3 — Hotkey-Routing?** → **"Theoretisch brauche ich nur das
Codierfenster. Wir verschmelzen den Player zu einem."**

## Entscheidung: Option B (mit Erweiterung)

PlayerWindow als eigenstaendiges Coding-Window **faellt weg**. Das
CodingModeWindow uebernimmt komplett — es **ist** der Player. Im Detail:

- **CodingModeWindow** ist die einzige Buehne fuer:
  - Video-Wiedergabe (LibVLC) inkl. Hotkeys (Play/Pause/Speed/Seek)
  - Coding-Workflow (Mark/BBox/SAM, Schadens-Codierung, Trainingsbox)
  - Live-AI-Overlays + Schema-Rendering
  - Hotkey-Routing (alle `KeyDown`-Events landen hier)
- **PlayerWindow.CodingMode.cs** + alle anderen Coding-bezogenen
  PlayerWindow-Partials (`PlayerWindow.CodingApply.cs`,
  `PlayerWindow.CodingEvents.cs`, `PlayerWindow.CodingTool.cs`,
  `PlayerWindow.CodingOverlayRender.cs`, `PlayerWindow.CodingOverlay
  Schema.cs`, `PlayerWindow.MarkTool.cs`, `PlayerWindow.MaskTriage.cs`)
  → **werden geloescht** oder so weit reduziert, dass nur reine
  Video-Wiedergabe-Funktionalitaet uebrig bleibt.
- **PlayerWindow** als Window kann entweder:
  - **B.1** ganz geloescht werden — alle Aufrufer (DataPage,
    BeobachtungenWindow, etc.) routen direkt aufs CodingModeWindow.
  - **B.2** als reiner Quick-Look-Player ohne Coding bleiben (z.B.
    "Video schauen ohne codieren"-Workflow).

User-Praeferenz fuer B.1 vs B.2 ist offen — wird im Plan adressiert.

## Konsequenzen

**Pro:**
- Eine einzige Coding-Buehne → Doppelpflege weg.
- Hotkey-Routing trivial: alles im CodingModeWindow.
- Zwei-Monitor-Workflow bleibt erhalten (CodingModeWindow ist frei
  positionierbar).
- DataPage → CodingModeWindow-Workflow bleibt unveraendert.
- Spart ~3000 Zeilen Code allein in `PlayerWindow.CodingMode.cs`,
  plus weitere Reduktion in den anderen Coding-Partials.

**Contra / Risiko:**
- Hoeher Migrationsaufwand als Option C.
- Bestehende Tests, die ueber PlayerWindow Coding-Pfade nutzen, muessen
  umgebaut oder geloescht werden.
- Nicht-Coding-Konsumenten von PlayerWindow (z.B. Quick-Look,
  PlaywrightPdfImport-Vorschau) muessen entweder ans CodingModeWindow
  umgeleitet werden oder bekommen ein eigenes minimal-Player-Window.

## Konkreter Migrationsplan (Option B)

Reihenfolge so gewaehlt, dass jeder Slice fuer sich gruen baut + tested
und bei jedem Schritt das CodingModeWindow funktional bleibt.

| Slice | Aktion | Risiko |
|---|---|---|
| **8a.1** | **Audit-Skript**: Diff aller Methoden in PlayerWindow.CodingMode.cs vs. CodingModeWindow.xaml.cs. Liste mit "in beiden / nur Player / nur CodingMode" als Markdown speichern. | niedrig |
| **8a.2** | **Fehlende Funktionalitaet** von Player → CodingModeWindow migrieren (alles aus 8a.1 unter "nur Player"). Pro Methode ein eigener Commit. | mittel |
| **8a.3** | **Aufrufer umleiten**: DataPage, BeobachtungenWindow, ImportPage, MediaConflictsPage usw. sollen statt `new PlayerWindow(...)` mit Coding-Modus jetzt `new CodingModeWindow(...)` oeffnen. | mittel |
| **8a.4** | **Coding-Partials in PlayerWindow loeschen**: alle `PlayerWindow.Coding*.cs` + `PlayerWindow.MarkTool.cs` + `PlayerWindow.MaskTriage.cs` entfernen. PlayerWindow ist dann nur noch Wiedergabe. | hoch |
| **8a.5** | **Entscheidung B.1 vs B.2**: PlayerWindow ganz loeschen (B.1) oder als reiner Quick-Look-Player behalten (B.2). | abhaengig von User |
| **8a.6** | **Hotkey-Routing aufraeumen**: PlayerWindow.Hotkeys.cs verliert alle Coding-Hotkeys. Alle Coding-Hotkeys leben jetzt nur noch im CodingModeWindow. | mittel |
| **8a.7** | **Tests aktualisieren**: alle Tests, die PlayerWindow als Coding-Stage gebraucht haben, auf CodingModeWindow umbauen. | mittel |
| **8a.8** | **Manueller Smoke**: User testet alle Workflows die PlayerWindow betreffen. | (User) |
| **8a.9** | **Rollback-Punkt**: Tag setzen `pre-slice-8a-cleanup`, falls spaeter ein Coding-Bug auftaucht der nur ueber den alten Player-Pfad reproduzierbar ist. | niedrig |

## Vorbereitung vor Slice 8a.1

Vor dem ersten Cut sollten die existierenden Slice-Patterns bestaetigt sein:

1. ✅ **OverlayToolService** ist als `partial class` zerlegt (Slice 33). Beide
   Windows benutzen denselben Service — keine Doppelpflege fuer
   Geometry-Builders.
2. ✅ **CodingSessionService** als gemeinsamer State-Container existiert.
3. ✅ **Tests** sind aktuell gruen (946 + 1 skip).
4. **TODO**: Vor 8a.1 einen **Branch-Tag** setzen (`pre-slice-8a`),
   damit der Migrationsstand klar abgrenzbar bleibt.

## Offene Detail-Fragen

✅ **8a.5 — B.1 entschieden**: PlayerWindow geht ganz weg. CodingMode
Window kann eh alles (Play/Pause/Stop/Speed/Scrub) — der Coding-Modus
ist nur eine optionale Overlay-Schicht.
