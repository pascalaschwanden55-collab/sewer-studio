# Slice 8a — CodingModeWindow auf VideoPlaybackController — Mini-ADR

Datum: 2026-05-10
Status: **Vorgeschlagen** (User-Freigabe ausstehend)

Vorgeschichte:
- Audit-Gegenpruefung 2026-05-10 (`docs/PROGRAMMAUDIT_GEGENPRUEFUNG_AUSSENSICHT_2026-05-10.md`)
  hat den doppelten LibVLC-Lebenszyklus als hoechstes verbleibendes Architektur-Risiko bestaetigt:
  PlayerWindow nutzt `IVideoPlaybackController`, CodingModeWindow nutzt LibVLC direkt
  (8 LibVLC-Treffer, 0 Controller-Treffer; gemessen am Auditstand der Datei).
- Konsolidierungs-ADR: `2026-05-09-slice-8a-coding-mode-konsolidierung.md` (Option B.1)
- Stop-Liste: `2026-05-09-slice-8a-2-stop-list-adr.md`
- Slice-Disziplin (Memory `feedback_slice_8a_migration.md`):
  *„Wenn ein Slice Session-State, Player-Lifecycle oder Pause/Resume beruehren
  wuerde: stop, ADR schreiben, User fragen."*
  Der hier behandelte Eingriff faellt **vollstaendig** in diesen Stop-Block.

## Was diese ADR macht

- Dokumentiert den Ist-Stand der direkten LibVLC-Nutzung im CodingModeWindow.
- Bewertet drei Migrationspfade.
- Legt Slice-Plan, Test-Erwartungen und Push-Punkte fest.
- Gibt Ja/Nein-Frage an den User. Code kommt erst nach Freigabe.

## Was diese ADR NICHT macht

- Sie tauscht **nicht** den Controller-Konstruktor. Backend-Pluggability
  (`IVideoPlaybackBackend`, `LibVlcPlaybackBackend`) bleibt unangetastet.
- Sie aendert **nicht** das CodingSessionViewModel, kein Session-State-Umzug.
- Sie ruehrt **nicht** an Pause-Confirm-Workflow, Auto-BCD/BCE,
  Auto-Kalibrierung — alles eigenstaendige Slices/ADRs.

## Ist-Stand (CodingModeWindow.xaml.cs, Stand 2026-05-10)

29 LibVLC-Touchpoints, gruppiert:

| Gruppe | Stellen | Charakter |
|---|---|---|
| Konstruktion | Z.120-122 (`new LibVLC(...)`, `new MediaPlayer(_libVlc)`, `VideoView.MediaPlayer = _player`) | Lifecycle |
| Event-Subscriptions | Z.125 (`LengthChanged`), Z.135 (`EncounteredError`), Z.145 (`Playing` -> `OnPlayerFirstPlaying`) | Lifecycle |
| Media-Load + Play | Z.147-148 (`new Media(_libVlc, ...)`, `_player.Play(media)`) | Lifecycle |
| Stop + Dispose | Z.290-292 (`_player.Stop`, `_player.Dispose`, `_libVlc.Dispose`) | Lifecycle |
| Time/Length-Reads | Z.437-498, 1473-1474, 1627-1628, 2298 | Read |
| Pause/Resume | Z.441 (`_player.SetPause(true)` nach FirstPlaying), Z.661 (Loop-Pause), Z.1423/1441/1449 (Click-Handler) | Steuerung |

Gleichzeitiger Controller-Betrieb scheidet aus: `VideoPlaybackController` setzt im
Konstruktor `_surface.MediaPlayer = _backend.NativePlayer`. Beide Instanzen wuerden
sich an derselben `VideoView.MediaPlayer`-Property gegenseitig ueberschreiben.

`IVideoPlaybackController` deckt die drei VLC-Events des CodingModeWindow heute
**nicht** ab:
- `LengthChanged` -> Window setzt `_videoDurationMs` und korrigiert `_player.Length`
- `EncounteredError` -> Window-Fehlerpfad
- `Playing` -> einmaliger `OnPlayerFirstPlaying`-Handler

Die Read-API (`TimeMs`, `LengthMs`, `IsPlaying`) und die Steuer-API
(`Pause`, `Resume`, `Play(path)`) sind hingegen vorhanden und ueber die
PlayerWindow-Migration verifiziert.

## Optionen

### Option A — Big-Bang in einem Slice
CodingModeWindow komplett auf Controller umstellen, Interface ergaenzen,
Tests und UI-Smoke alles in einem Commit-Block.

- **Pro**: Eine Lifecycle-Wahrheit am Ende des Tages.
- **Contra**: 29 Touchpoints + 3 fehlende Events + UI-Smoke in einem Slice
  widerspricht der 8a-Disziplin („pro Slice eine kleine Gruppe Helfer/Guards").
- **Risiko**: Hoch. Bei Regression fehlt der saubere Bisect-Punkt.

### Option B — Backend-Slice, dann UI-Slice (empfohlen)
**Slice-1 (Infrastructure-only, kein UI-Touch)**: `IVideoPlaybackController` und
`IVideoPlaybackBackend` um die drei fehlenden Events ergaenzen
(`LengthChanged`, `EncounteredError`, `FirstPlayingOnce`). `LibVlcPlaybackBackend`
verdrahtet sie auf die nativen LibVLC-Events; das `FakeBackend` aus den
Controller-Tests bekommt parallele Trigger. PlayerWindow ist nicht betroffen
(es benutzt diese Events heute nicht — wenn doch, eigener Sub-Slice).

**Slice-2 (UI-Lifecycle, CodingModeWindow only)**: Konstruktion + Dispose +
Events + Reads + Pause/Resume in einem Rutsch auf den Controller umstellen.
Reads und Pause/Resume **lassen sich technisch nicht vom Lifecycle trennen**,
weil `_player` mit Slice-2 verschwindet — daher in einem Commit. UI-Smoke
direkt im Anschluss.

- **Pro**: Slice-1 ist mechanisch (Backend + Tests, kein UI-Risiko). Slice-2
  ist focussed (eine Datei, eine Verantwortung). Bisect bleibt sauber.
- **Contra**: Zwei Slice-Sessions statt einer.
- **Risiko**: Mittel. Slice-2 bleibt Lifecycle, aber das Interface ist
  bei Beginn schon vorbereitet und unter Test.

### Option C — „Read-Only zuerst" (verworfen)
Ursprungsidee: nur `_player.Time`/`_player.Length` auf Controller mappen,
Lifecycle bleibt direkt. **Geht so nicht**: der Controller braucht eigene
Backend-Instanz und ueberschreibt `VideoView.MediaPlayer`. Ein paralleler
Read-Pfad ohne Lifecycle-Migration ist nicht sauber realisierbar, ohne
einen Read-Adapter aufzubauen, der `_player` direkt anzapft — das waere
Wegwerfcode.

## Empfehlung

**Option B**.

Konkrete Schritte:

### Slice-1 — Interface + Backend Events
1. `IVideoPlaybackController` ergaenzen:
   - `event EventHandler<long>? LengthChanged;`
   - `event EventHandler<string>? EncounteredError;`
   - `event EventHandler? FirstPlayingOnce;` (nur erstes `Playing` pro Play-Aufruf)
2. `IVideoPlaybackBackend` und `LibVlcPlaybackBackend`:
   - LibVLC-Events durchreichen, `FirstPlayingOnce` per Flag gateen
   - `Cleanup()` haengt sauber ab
3. `FakeBackend` in `VideoPlaybackControllerTests` parallele Trigger spendieren
4. Mindestens drei neue Test-Faelle: Length-Update, Error-Pfad, FirstPlaying-Once-Garantie
5. Build + `dotnet test` gruen
6. Commit + Push (vor Slice-2, weil Slice-2 riskanter)

### Slice-2 — CodingModeWindow Lifecycle umstellen
1. DI-/Konstruktion: `VideoPlaybackController` per `App.Resolve` oder Factory
   bauen, `VideoView` als `IVlcSurface` adapten (Player Window-Pattern uebernehmen)
2. Felder `_libVlc`/`_player` entfernen, Reads auf `_controller.TimeMs`/`LengthMs`/`IsPlaying`
3. Pause/Resume/Play -> `_controller.Pause()/Resume()/Play(_vm.VideoPath)`
4. Events (`LengthChanged`, `EncounteredError`, `FirstPlayingOnce`) am Controller abonnieren,
   bestehende Handler-Bodies bleiben unveraendert
5. Stop+Dispose -> `_controller.Cleanup()` im `Window_Closing`
6. Build + `dotnet test` gruen
7. **Manueller UI-Smoke** (nicht skippen, weil Lifecycle):
   - Codiermodus oeffnen, Video laedt
   - Play/Pause/Scrub funktionieren
   - LengthChanged setzt `_videoDurationMs` korrekt
   - Live-Loop laeuft, Pause-Confirm-Pfad funktioniert
   - Window schliessen ohne AccessViolation
8. Commit + Push

## Stop-Liste / Wenn-dann

- Wenn Slice-1 unerwartet PlayerWindow tangiert (z.B. weil dort verdrahtete
  Events anders heissen): **Slice-1 stoppen, Sub-Slice-ADR fuer PlayerWindow-Adoption**.
- Wenn Slice-2 in einen Session-State-Konflikt laeuft (z.B. weil
  CodingSessionViewModel Time-Reads erwartet, die jetzt vom Controller kommen):
  **Stop, Mini-ADR fuer VM-Read-Path**.
- Wenn der UI-Smoke einen native Crash zeigt (haeufiger Fall bei VLC.Dispose):
  **Slice-2 reverten**, ADR-Erweiterung mit konkretem Stack-Trace, dann erst
  Re-Try mit gezieltem Cleanup-Pfad.

## Test-Erwartung

- Slice-1: +3 Tests in `VideoPlaybackControllerTests`. Pipeline-Tests
  unveraendert. UI baut weiter.
- Slice-2: keine neuen automatisierten Tests (UI-Window). Pipeline-Tests
  unveraendert. Manueller UI-Smoke ist der echte Gate.

## User-Freigabe

Bitte bestaetigen:
- (Q1) Optionswahl B inkl. Verzicht auf Option C (ja/nein)?
- (Q2) Drei Event-Namen (`LengthChanged`, `EncounteredError`, `FirstPlayingOnce`)
  oder andere Praeferenz?
- (Q3) Slice-1 sofort umsetzen oder ADR erst „liegen lassen" und naechste Session?
