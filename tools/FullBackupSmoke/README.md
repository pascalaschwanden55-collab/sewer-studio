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
