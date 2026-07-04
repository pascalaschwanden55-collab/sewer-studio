# SewerStudio QGIS Bridge

Dieses Verzeichnis enthaelt ein kleines, versioniertes QGIS-Plugin. Es ist dafuer da,
die Verbindung nach einer neuen QGIS-Installation wiederherstellbar zu machen:
Plugin erneut installieren, in QGIS aktivieren, Datenordner setzen.

## Installation

Standardprofil automatisch suchen und Plugin kopieren:

```powershell
powershell -ExecutionPolicy Bypass -File integrations\qgis\install-sewerstudio-bridge.ps1
```

Alle gefundenen QGIS-Profile aktualisieren:

```powershell
powershell -ExecutionPolicy Bypass -File integrations\qgis\install-sewerstudio-bridge.ps1 -AllProfiles
```

Ein bestimmtes Profil angeben:

```powershell
powershell -ExecutionPolicy Bypass -File integrations\qgis\install-sewerstudio-bridge.ps1 -ProfileRoot "$env:APPDATA\QGIS\QGIS3\profiles\default"
```

Danach QGIS neu starten und unter `Erweiterungen > Erweiterungen verwalten`
`SewerStudio Bridge` aktivieren.

## Nutzung

1. In QGIS `SewerStudio Bridge` oeffnen.
2. `Datenordner` auf `D:\QGIS_V4.03\Export_Sewer_Studio` setzen.
3. `Lokale Export-Layer laden` klicken.
4. Falls SewerStudio den Live-Bridge-Endpoint bereitstellt: `Verbinden` klicken.

Das Plugin kann lokale Exportdaten laden und ist gleichzeitig auf einen Live-Bridge-
Feed vorbereitet. Es nutzt keine externen Python-Pakete, nur PyQGIS und die Python-
Standardbibliothek.

## Datenvertrag

Lokale Dateien im Datenordner:

- `current_haltung.geojson` oder `current.geojson`
- `schaeden.geojson` oder `damages.geojson`
- `netzbewertung.geojson` oder `network.geojson`

HTTP-Bridge, falls SewerStudio sie bereitstellt:

- `GET /qgis/status.json`
- `GET /qgis/current.geojson`
- `GET /qgis/damages.geojson`
- `GET /qgis/network.geojson`

Bestehende Shapefile-Exporte werden ebenfalls erkannt. Das Plugin sucht im
Datenordner den neuesten Unterordner mit `*.shp` und laedt u. a. `Haltungen*`,
`Schaechte*` und `Schaeden*`.

## Nach QGIS-Update

1. QGIS einmal starten, damit das neue Profil angelegt wird.
2. Install-Skript erneut ausfuehren.
3. Plugin in QGIS aktivieren.
4. Datenordner pruefen.

Wenn QGIS seine Profilstruktur aendert, bleibt der Plugin-Quellcode trotzdem hier im
Repo erhalten. Dann muss nur der Zielordner im Install-Skript bzw. per `-ProfileRoot`
angepasst werden.
