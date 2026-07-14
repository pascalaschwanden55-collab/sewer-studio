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

Jede Installation sichert das Plugin zusaetzlich ins zentrale Plugin-Archiv
`D:\QGIS_V4.03\AWU_Plugins` (entpackter Ordner + versioniertes ZIP, gleiche
Konvention wie die uebrigen AWU-Plugins). Anderer Ort: `-BackupDir <Pfad>`.

## Nutzung

1. In QGIS `SewerStudio Bridge` oeffnen.
2. `Datenordner` auf `D:\QGIS_V4.03\Export_Sewer_Studio` setzen.
3. `Lokale Export-Layer laden` klicken.
4. Falls SewerStudio den Live-Bridge-Endpoint bereitstellt: `Verbinden` klicken.

Das Plugin kann lokale Exportdaten laden und ist gleichzeitig auf einen Live-Bridge-
Feed vorbereitet. Es nutzt keine externen Python-Pakete, nur PyQGIS und die Python-
Standardbibliothek.

Der reine HTTP-Vertrag kann auch ohne installiertes QGIS geprüft werden:

```powershell
python -m unittest discover integrations\qgis\tests -v
```

## Datenvertrag

Lokale Dateien im Datenordner:

- `current_haltung.geojson` oder `current.geojson`
- `schaeden.geojson` oder `damages.geojson`
- `netzbewertung.geojson` oder `network.geojson`

HTTP-Bridge (liefert SewerStudio ab Version 4.5 live auf `http://127.0.0.1:8765`):

- `GET /qgis/status.json` — Status inkl. aktuell gewaehlter Haltung (`currentHolding`)
- `GET /qgis/current.geojson` — Linie der aktuell gewaehlten Haltung
- `GET /qgis/damages.geojson` — alle Schaeden des Projekts als Punkte (Protokoll-Eintraege
  bevorzugt, importierte VSA-Feststellungen als Fallback), verortet ueber den Meterstand
  entlang der Kataster-Geometrie
- `GET /qgis/network.geojson` — ganzes Netz mit Zustandsklasse/-farbe
- `GET /qgis/sanierungstyp.geojson` — Haltungen nach `Ausgefuehrt durch`
- `GET /qgis/schaechte.geojson` — alle Kataster-Schächte mit Projektbezug
- `GET /qgis/current_schacht.geojson` — aktuell gewählter Schacht
- `GET /qgis/schacht_sanierungstyp.geojson` — Schächte nach `Ausgefuehrt durch`

Hinweise zum Bridge-Server:

- Laeuft automatisch mit der App; abschaltbar mit `SEWERSTUDIO_QGIS_BRIDGE=0`,
  Port aenderbar mit `SEWERSTUDIO_QGIS_BRIDGE_PORT`.
- Ist Live-Control aktiv (`SEWERSTUDIO_LIVE_CONTROL=1`), teilt sich die Bridge den
  Port 8765 mit Live-Control: die `/qgis`-Endpunkte sind dort ohne Token lesbar,
  die Steuer-Endpunkte bleiben Token-geschuetzt.
- Die "aktuelle Haltung" folgt der Auswahl auf der Haltungen-Seite und in der Karte
  (auch im separaten Kartenfenster) und bleibt beim Seitenwechsel erhalten.
- Auch das QGIS-Plugin akzeptiert als Bridge-Ziel nur lokale HTTP-Adressen
  (`127.0.0.1`, `localhost` oder `::1`).

### Sicherheitsgrenze

Die Live-Bridge ist bewusst fuer einen Windows-Einzelplatz ausgelegt. Sie bindet nur an
`127.0.0.1`, akzeptiert ausschliesslich `GET`/`HEAD` und liefert nur Projekt- und
Geometriedaten zum Lesen. Sie besitzt deshalb kein Token; ein Prozess auf demselben PC
koennte die Daten ebenfalls lesen, aber weder das Projekt noch die App damit veraendern.

Auf einem Mehrbenutzer- oder Terminalserver gilt diese Annahme nicht. Dort die Bridge mit
`SEWERSTUDIO_QGIS_BRIDGE=0` deaktivieren. Vor einer spaeteren Freigabe fuer solche Systeme
muss ein gemeinsamer Token fuer SewerStudio und das QGIS-Plugin eingefuehrt werden.

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
