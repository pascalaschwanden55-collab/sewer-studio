# SewerStudio auf einem neuen PC einrichten

Diese Anleitung gilt fuer SewerStudio 4.5 unter Windows. Sie trennt Programm,
Projekte und das KI-Gehirn, damit ein Update keine Nutzdaten ueberschreibt.

## 1. Vor dem Wechsel sichern

1. In SewerStudio unter **Einstellungen > Vollsicherung** eine Sicherung auf eine
   USB-Platte erstellen. Die Sicherung muss Projekte enthalten; Videos sind je
   nach Einstellung enthalten oder ausgeschlossen.
2. Den alten Programm-/Release-Ordner zusaetzlich aufbewahren.
3. Die erzeugte `RESTORE-ANLEITUNG.txt` und `manifest.json` auf der USB-Platte
   kontrollieren.

## 2. Programm installieren

1. Den fertigen `SewerStudio-<Version>-win-x64-<Datum>`-Ordner auf den neuen PC
   kopieren, zum Beispiel nach `C:\Programme\SewerStudio\4.5.0`.
2. Nicht aus `bin\Debug` starten und einen neuen Release nie ueber einen alten
   kopieren.
3. `SewerStudio.exe` starten. Das Publish-Paket ist eigenstaendig; fuer die
   normale Anwendung ist kein separates .NET SDK erforderlich.

Fuer Entwickler: Release mit `tools\Publish-SewerStudio.ps1` erzeugen und mit
`Start-SewerStudio.ps1` aus dem Publish-Ordner starten.

## 3. Werkzeuge einrichten

- **PDF:** `pdftotext.exe` nach `<Release>\tools\pdftotext.exe` kopieren oder den
  Pfad in SewerStudio einstellen. Ohne das Werkzeug gibt es einen internen
  PDF-Fallback, die Texterkennung kann aber schlechter sein.
- **Video/KI:** `ffmpeg.exe` und `ffprobe.exe` in den Windows-PATH aufnehmen oder
  `SEWERSTUDIO_FFMPEG` auf den vollstaendigen Pfad zu `ffmpeg.exe` setzen.
- **Ollama:** Ollama installieren und danach ausfuehren:

  ```powershell
  ollama pull qwen3-vl:8b-q8
  ollama pull nomic-embed-text
  ```

- **Python-Sidecar:** Python 3.10 oder neuer installieren. Optional `uv`
  installieren. Danach im Release-Ordner ausfuehren:

  ```powershell
  powershell -ExecutionPolicy Bypass -File .\Install-Sidecar.ps1
  ```

  Das Setup nutzt den festen Lockfile und fuer eine RTX 5090 den PyTorch-cu128-
  Index. Nicht auf cu121 zurueckstellen. Die Ersteinrichtung benoetigt derzeit
  Internet; insbesondere der Grounding-DINO-Tokenizer liegt noch nicht komplett
  lokal im Projekt.

## 4. Sicherung zurueckspielen

1. Die `RESTORE-ANLEITUNG.txt` aus der konkreten Sicherung oeffnen. Ihre Pfade
   haben Vorrang vor Beispielen in diesem Dokument.
2. Einstellungen und Anwendungsdaten in den dort genannten AppData-Ordner
   zurueckkopieren.
3. `KI_BRAIN` an den gesicherten Knowledge-Root zurueckkopieren.
4. Die gesicherten Projektordner an ihre Zielorte kopieren.
5. SewerStudio starten und die Warnung zum Knowledge-Root beachten. Der Pfad
   wird in `settings.json` gespeichert. Eine gesetzte Variable
   `SEWERSTUDIO_KNOWLEDGE_ROOT` hat bewusst Vorrang und muss zum neuen Pfad passen.

## 5. Abnahme nach der Wiederherstellung

- Einstellungen sind vorhanden.
- Ein Projekt laesst sich oeffnen; Haltungen, Fotos, Befunde und Videoverknuepfungen
  sind plausibel.
- Die KnowledgeBase startet ohne Integritaetswarnung und enthaelt die erwarteten
  Lernbeispiele.
- Ein PDF-Import und eine kurze Videoanalyse funktionieren.
- Eine neue Vollsicherung laeuft erfolgreich durch.

Das Ergebnis mit Datum und Dauer in `docs/PROBERESTORE-PROTOKOLL.md` eintragen.

## Update und Rueckweg

Vor jedem Update eine Vollsicherung erstellen. Den neuen Release in einen neuen
Ordner kopieren und dort starten. Bei einem Problem die neue App schliessen und
`SewerStudio.exe` aus dem alten Release-Ordner starten. Projekt- und KI-Daten
nicht aus dem Programmordner loeschen; sie liegen absichtlich ausserhalb davon.
