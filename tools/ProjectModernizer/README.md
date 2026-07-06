# ProjectModernizer

CLI-Werkzeug, um alte SewerStudio-Projektordner in die aktuelle portable Struktur zu ueberfuehren.

Das Tool arbeitet gegen einen Projektordner. Externe Originalordner werden nur gelesen. Dateien werden in den Projektordner kopiert; vorhandene Quelldateien werden nicht geloescht oder verschoben.

## Sicherer Probelauf

```powershell
dotnet run --project tools\ProjectModernizer\ProjectModernizer.csproj -- `
  --project-folder "D:\Projekte\Zone 1.15" `
  --source-folder "D:\Videoprojekte\GEP_Altdorf_2025_Zone_1.15_29261_925_Export" `
  --dry-run
```

Der Dry-Run schreibt keine Dateien. Er zeigt, welche Pfade geloest, kopiert, bereinigt oder nicht gefunden wuerden.

## Echte Modernisierung

```powershell
dotnet run --project tools\ProjectModernizer\ProjectModernizer.csproj -- `
  --project-folder "D:\Projekte\Zone 1.15" `
  --source-folder "D:\Videoprojekte\GEP_Altdorf_2025_Zone_1.15_29261_925_Export"
```

Vor dem Speichern erstellt das Tool ein Backup der Projektdatei unter `RestorePoints\modernize`. Medien bleiben additiv erhalten: das Tool kopiert benoetigte Dateien in die neue Struktur und loescht keine Legacy-Medien.

## Flatten-only

```powershell
dotnet run --project tools\ProjectModernizer\ProjectModernizer.csproj -- `
  --project-folder "D:\Projekte\Zone 1.15" `
  --flatten-only `
  --dry-run
```

`--flatten-only` bereinigt nur `Haltungen_Verteilt` und fuehrt keinen neuen Import-/Quellabgleich aus.

## Wichtige Regeln

- Projektdatei wird in `Projektdateien\projekt.json` gespeichert.
- Externe Pfade in Metadaten und Protokoll-Snapshots werden entfernt oder in projektinterne relative Pfade ersetzt.
- Videos, PDFs und Fotos werden nur aus bekannten Quellen in den Projektordner uebernommen.
- Unaufgeloeste Pfade werden im Modernisierungsbericht protokolliert.
- `bin` und `obj` sind Build-Artefakte und werden nicht versioniert.
