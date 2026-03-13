# AGENTS.md — Projektanweisungen für Codex/Agenten

## Projekt
**AuswertungPro** (C#/.NET) – Verarbeitung von VSA-Inspektionsdaten:
- Import von PDF-Protokollen und Videos
- Verteilung pro Haltung in Zielordnerstruktur
- Video-Playback in UI (LibVLCSharp)
- Export/Excel-Vorlage (Statistiken/KPIs)

## Zielstruktur (Output)
Für jede Haltung:
<ProjektRoot>\Haltungen\<Gemeinde>\<Haltung>\
  - <yyyyMMdd>_<Haltung>.pdf
  - <yyyyMMdd>_<Haltung>.<videoext>
Optional:
<ProjektRoot>\Haltungen\<Gemeinde>\__UNMATCHED\<Haltung>\...

## Kernlogik (Parsing & Zuordnung)
### PDF Parsing (Quelle der Wahrheit)
- Aus PDF-Text: "Haltungsinspektion - dd.MM.yyyy - <Haltung>"
- Haltung-Regex: [0-9.]+-[0-9.]+
- Videodateiname aus PDF: "Film <filename>.<ext>"

### Video Matching (robust)
1) Exakt: Filmname aus PDF == Video-Dateiname im Video-Ordner
2) Falls Video noch falsch benannt:
   - Suffix-Match ab erstem "_" (GUID/Suffix bleibt gleich)
   - Wenn genau 1 Treffer -> verwenden
   - Wenn mehrere -> Ambiguous (nicht automatisch zuordnen)
   - Wenn keiner -> NotFound
3) Kein Crashing: pro Datei Fehler abfangen und Ergebnis protokollieren

### Unmatched Handling
- PDF wird immer korrekt kopiert/verschoben und umbenannt
- NotFound -> *_VIDEO_MISSING.txt im Haltung-Ordner
- Ambiguous -> *_VIDEO_AMBIGUOUS.txt + Kandidaten nach __UNMATCHED kopieren (COPY, nie MOVE)

## UI / Video Player
- WPF: LibVLCSharp.WPF + VideoLAN.LibVLC.Windows
- Player oeffnet Video per gespeichertem VideoPath in Datenzeile (HaltungRow.VideoPath)
- Keine UI-Overlays direkt ueber VideoView (Airspace in WPF beachten)

## Build / Run
- .NET SDK: (Projektziel eintragen, z.B. net8.0)
- Standard: `dotnet build`
- Tests: `dotnet test` (falls vorhanden)

## Coding Standards
- Kein "magischer" Parsing-Code in UI: Parsing/IO in Services (z.B. Distributor-Klassen)
- Alle File-Operationen: Path-Sanitizing, EnsureUniquePath bei overwrite=false
- Exceptions pro Datei fangen, als Result zurueckgeben (kein globaler Abbruch)
- Logging: klare Messages (Datei, Haltung, Datum, Status)

## Dependencies (NuGet)
- PDF: UglyToad.PdfPig
- VLC: LibVLCSharp.WPF, VideoLAN.LibVLC.Windows

## Deliverables bei Änderungen
Wenn du neue Features implementierst:
- Update/Erweiterung der Result-Records (Success/Message/Paths)
- Mini-Beispiel/CLI zum manuellen Testen
- (Optional) Unit Tests fuer Parser/Matcher
