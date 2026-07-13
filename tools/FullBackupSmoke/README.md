# FullBackupSmoke

Startet den echten SewerStudio-Sicherungsdienst ohne Benutzeroberflaeche. Das
Werkzeug ist fuer einen dokumentierten Proberestore oder eine Fehlerdiagnose gedacht.

```powershell
dotnet run --project tools/FullBackupSmoke -- `
  "G:\Systemschutz\12.07.2026" `
  "D:\Projekte" `
  "C:\Sewer-Studio_KI_4.5"
```

Projektvideos sind dabei bewusst ausgeschlossen. Alle anderen Projektdateien,
Fotos und Restore-Points werden gesichert.

Wiederhergestellten Testordner pruefen:

```powershell
dotnet run --project tools/FullBackupSmoke -- `
  --verify-restore "C:\SewerStudio-Proberestore-20260712"
```

Der Prueflauf vergleicht zuerst jede aktuelle Sicherungsdatei mit Laenge und
SHA-256 aus `manifest.json`. Erst danach werden Wissensdatenbank und Projektinhalt
fachlich geprueft. Alte Sicherungen ohne Datei-Hashes werden bewusst abgelehnt.
