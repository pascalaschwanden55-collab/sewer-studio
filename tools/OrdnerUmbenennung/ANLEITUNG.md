# Programmordner umbenennen: Sewer-Studio_KI_4.5 -> Sewer-Studio_KI_5.0

Stand 13.09.2026. Der Ordnername ist reine Windows-Sache; das Programm selbst (Version 5.0,
Splash, Einstellungen, Sicherungsmanifest) weiss nichts von ihm. Am Pfad haengen aber:

| Was | Wie viele | Wird erledigt durch |
|---|---|---|
| Python-Umgebung `sidecar\.venv\Scripts` (6 Aktivierungsskripte, 37 Launcher-EXEs) | 43 Dateien | Skript, byteweise (gleiche Laenge, kein Neuaufbau) |
| Git-Worktrees (3 im Ordner, 2 Geschwister `-nova`, `-optik`) | 5 | Skript, `git worktree repair` |
| `.claude\settings.local.json` im Repo | 2 Vorkommen | Skript |
| `Desktop\Coding.bat` | 1 Vorkommen | Skript |
| Claude-Gedaechtnis `%USERPROFILE%\.claude\projects\c--Sewer-Studio-KI-4-5` | 1 Ordner | Skript (Kopie, nichts wird geloescht) |
| `bin\`, `obj\`, `.tmp\` Build-Ausgaben | viele | naechster `dotnet build` |
| VS Code «zuletzt geoeffnet», offene Terminals | - | von Hand neu oeffnen |

`sidecar\start_sidecar.ps1` findet die Umgebung relativ (`$scriptDir\.venv`) und nutzt
`Activate.ps1`, das seinen Pfad selbst ableitet - der Sidecar startet auch ohne Reparatur.
Die 37 Launcher (`uvicorn.exe`, `yolo.exe`, ...) sind Bequemlichkeit; das Skript repariert
sie trotzdem. `AppData\Local\SewerStudio` traegt den Pfad nur in alten Logs (egal).

## Reihenfolge

1. **Alles schliessen**: SewerStudio, VS Code, alle Claude-/Codex-Sitzungen, Sidecar.
   Am einfachsten: **Windows neu starten** und danach NICHTS im alten Ordner oeffnen.
2. Im Explorer `C:\Sewer-Studio_KI_4.5` -> `C:\Sewer-Studio_KI_5.0` umbenennen.
   Wahlweise auch `C:\Sewer-Studio_KI_4.5-nova` -> `...5.0-nova` und `-optik` (das Skript
   findet beide Namen).
3. PowerShell im **neuen** Ordner oeffnen (Rechtsklick > «In Terminal oeffnen») und:

   ```powershell
   Set-ExecutionPolicy -Scope Process Bypass
   .\tools\OrdnerUmbenennung\nach-umbenennung.ps1 -Selbsttest     # Beweis auf Kopien
   .\tools\OrdnerUmbenennung\nach-umbenennung.ps1 -Pruefen        # zeigt nur
   .\tools\OrdnerUmbenennung\nach-umbenennung.ps1 -Ausfuehren     # schreibt
   ```

   Erwartete Ausgabe bei -Ausfuehren: `venv: 43 Dateien`, `settings.local.json: 2`,
   `Coding.bat: 1`, Worktree-Liste ohne Fehler, `Restliche venv-Dateien mit altem Pfad: 0`.
4. Nachweis, dass alles laeuft:

   ```powershell
   dotnet build AuswertungPro.sln
   git worktree list                       # alle Zeilen mit 5.0-Pfaden, keine "prunable"
   .\sidecar\start_sidecar.ps1             # dann im Browser http://127.0.0.1:8100/health
   ```

5. VS Code im neuen Ordner oeffnen; die naechste Claude-Sitzung dort starten - das Gedaechtnis
   ist kopiert.

## Erledigt am 13.09.2026

Gelaufen mit genau diesem Ergebnis: venv 43 Dateien / 80 Vorkommen, settings.local.json 2,
Coding.bat 1, alle 5 Worktrees repariert, Rest 0. Die Zeile «repair: gitdir incorrect» ist Gits
Bestaetigung der Reparatur, kein Fehler. Das Gedaechtnis wurde vorher von Hand kopiert (82 Dateien),
weil die neue Claude-Sitzung den Zielordner bereits leer angelegt hatte.

## Wenn etwas schiefgeht

- Skript sagt «muss aus dem UMBENANNTEN Ordner laufen»: Schritt 2 wurde uebersprungen.
- Skript sagt «Zuerst schliessen: ...»: genannte Programme beenden (oder Neustart).
- Rueckweg: `.\tools\OrdnerUmbenennung\nach-umbenennung.ps1 -Rueckgaengig -Ausfuehren`
  schreibt 5.0 -> 4.5 zurueck; danach den Ordner im Explorer zuruecknennen.
- Die Umgebung ist NICHT geloescht oder neu gebaut worden; im Notfall hilft
  `sidecar\setup.ps1` fuer einen frischen Aufbau (mehrere GB Download).

Das Skript loescht nie etwas, aendert keine Dateigroesse und verweigert den Lauf im alten
Ordner oder bei laufenden Programmen.
