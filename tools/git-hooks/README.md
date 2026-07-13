# Git-Testschutz

Der Pre-Push-Hook startet vor jedem Push die Infrastructure-, Pipeline- und UI-Tests.
Bei einem Fehler wird der Push abgebrochen.

Damit prueft der Schutz auch die Oberflaechen-Regeln. Dazu gehoert zum Beispiel die
maximale Groesse wichtiger UI-Dateien.

Installation:

```powershell
powershell -ExecutionPolicy Bypass -File tools\git-hooks\Install-PrePushHook.ps1
```

Der Hook liegt absichtlich auch im Projekt. Dateien unter `.git/hooks` werden von Git nicht versioniert.
Nur im begründeten Notfall kann der Schutz mit `git push --no-verify` einmalig umgangen werden.
