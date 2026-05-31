# Profi-Audit SewerStudio / AuswertungPro

Datum: 2026-05-31  
Branch: `feature/gis-karte`  
Arbeitsweise: keine Codeaenderungen, kein Commit, kein Push

## 1. Management-Kurzfazit

SewerStudio ist kein kleines Bastelprogramm mehr. Die App hat eine echte Fachbasis: Import, VSA-Bewertung, Player, GIS, KI-Pipeline, PDF/Excel-Export und Trainingswerkzeuge sind vorhanden und groesstenteils testbar.

Der Build ist sauber: `dotnet build AuswertungPro.sln -v minimal` lief mit 0 Fehlern und 0 Warnungen. Die Tests liefen ebenfalls sauber: komplette Solution mit Exitcode 0; Infrastructure 180 bestanden / 1 uebersprungen, UI 275 bestanden, Pipeline laut Teil-Audit 281 bestanden.

Die groesste Staerke ist die fachliche Tiefe. Es gibt schon robuste Parser, Konfliktlogik, VSA-Regeln, Kartenbausteine und KI-Traces. Die groesste Gefahr ist nicht ein einzelner Crash, sondern dass manche Funktionen so maechtig sind, dass sie ohne genug Schutz Daten veraendern koennen.

Die wichtigste naechste Entscheidung ist: zuerst Stabilitaet und Datenverlustschutz fertig machen. Danach erst Komfort, Design und neue Funktionen.

## 2. Top-10-Befunde

| Rang | Schwere | Bereich | Befund | Warum wichtig | Beleg | Empfehlung |
|---:|---|---|---|---|---|---|
| 1 | Kritisch | Import | Import-Vorschau ist keine echte Vorschau | Vorschau kann echte Projektdaten veraendern | `ImportPageViewModel.cs:176`, `LegacyPdfImportService.cs:180`, `LegacyXtfImportService.cs:244` | DryRun immer auf Projekt-Kopie ausfuehren |
| 2 | Hoch | Projekt | Projekt oeffnen kann ungespeicherte Aenderungen verlieren | Dirty-Abfrage gibt es nur beim Schliessen | `ShellViewModel.cs:274`, `MainWindow.xaml.cs:18` | Gemeinsame Speichern/Verwerfen-Abfrage vor Oeffnen/Neu |
| 3 | Hoch | UI/Daten | Einzelnes Loeschen und Spalte leeren sind zu gefaehrlich | AutoSave kann Datenverlust sofort speichern | `DataPage.xaml:161`, `DataPageViewModel.cs:431`, `DataPage.xaml.cs:935` | Bestaetigung, Undo oder Papierkorb |
| 4 | Hoch | Player | `CodingModeWindow` hat noch alten VLC-Close-Pfad | Aehnlicher nativer Crash wie beim Player moeglich | `CodingModeWindow.xaml.cs:240`, `CodingModeWindow.xaml.cs:396` | Sicheres Close/Cleanup aus `PlayerWindow` uebernehmen |
| 5 | Hoch | Sicherheit | Live-Control hat keinen Token | Lokaler Prozess kann UI und Pipeline steuern | `LiveControlServer.cs:40`, `LiveControlServer.cs:169`, `LiveControlServer.cs:185` | Start-Token, Body-Limit, Retry separat freischalten |
| 6 | Hoch | KI/Sidecar | Timeouts und VRAM-Plan sind nicht hart erzwungen | Haenger, stille Fallbacks und VRAM-Ueberlast moeglich | `PipelineConfig.cs:19`, `VisionPipelineClient.cs:31`, `EnhancedVisionAnalysisService.cs:148`, `gpu_manager.py:35` | Einheitliche Timeouts, Deep Health, VRAM-Scheduler |
| 7 | Hoch | Import | WinCan/IBAK/KINS ueberschreiben User-Werte | Manuelle Korrekturen koennen verloren gehen | `WinCanDbImportService.cs:1294`, `IbakExportImportService.cs:92`, `KinsImportService.cs:558` | `UserEdited` schuetzen, MergeEngine nutzen |
| 8 | Hoch | Import | IBAK/KINS nutzen unscharfes `Contains`-Matching | Aehnliche Haltungen koennen falsch zusammenlaufen | `IbakExportImportService.cs:125`, `KinsImportService.cs:537` | Nur exakt/Gegenrichtung/Prefix-normalisiert matchen |
| 9 | Hoch | GIS | Kartenpfade und Farbskala sind hart verdrahtet | Karte funktioniert nur auf deinem Rechner; falsche Skala waere fachlich fatal | `KarteViewModel.cs:24`, `KarteViewModel.cs:31`, `KarteViewModel.cs:179` | Pfade in Einstellungen, Skala sichtbar testen/umschalten |
| 10 | Mittel/Hoch | Datenqualitaet | Video-Matching ordnet bei eindeutigem Haltungsnamen automatisch zu | Falsches Video kann in richtigen Ordner kopiert werden | `HoldingVideoMatching.cs:108` | Als unsicher melden, nur optional automatisch uebernehmen |

## 3. Sicherheit

### Gut

- Keine echten API-Keys oder Secrets wurden im Quellcode gefunden.
- Sidecar hat Trusted-Host-Pruefung und optionalen Token: `sidecar/sidecar/main.py:105`, `main.py:115`.
- Bilddecode im Sidecar hat Byte- und Pixel-Grenzen: `sidecar/sidecar/models/image_decode.py:23`.
- Pfadsegmente werden zentral bereinigt: `ProjectPathResolver.cs:101`.

### Problematisch

- Live-Control ist absichtlich optional, aber ohne Authentifizierung. Sobald `SEWERSTUDIO_LIVE_CONTROL=1` aktiv ist, kann ein lokaler Prozess Farben aendern und Pipeline-Retry ausloesen.
- `LiveControlServer` liest `Content-Length` ohne Limit in ein Array. Ein lokaler Missbrauch kann Speicher belegen.
- Der MCP-Parameter `live_control_url` sollte nur Loopback erlauben.
- Sidecar prueft `Host`, aber nicht konsequent die Client-IP. Kritisch wird das, wenn der Server versehentlich auf `0.0.0.0` laeuft.
- Diagnose- und Tool-Ausgaben enthalten echte absolute Pfade zu Videos und Projekten.

### Empfehlung

1. Live-Control nur mit Zufalls-Token betreiben.
2. Body-Limit fuer Live-Control und Sidecar-Endpunkte setzen.
3. Nicht-Loopback bei Ollama/Sidecar nur nach sichtbarer Warnung erlauben.
4. `.gitignore` fuer Tool-Ausgaben und Diagnoseordner verschaerfen.

## 4. Stabilitaet / Player

### Gut

- `PlayerWindow` ist nach dem letzten Fix deutlich robuster.
- Close-Pfad setzt `_closing`, stoppt Timer, trennt `VideoView.MediaPlayer`, stoppt VLC und macht Cleanup idempotent: `PlayerWindow.Playback.cs:346`, `:380`, `:392`.
- Datei-fehlt-Fall wird vor VLC sauber erkannt.

### Problematisch

- `CodingModeWindow` nutzt nicht denselben sicheren Close-Pfad.
- Verzoegerte Tasks greifen dort nach `Task.Delay` weiter auf `_player` und UI zu.
- Einige kurzlebige Timer sind nicht zentral stoppbar.
- Kaputte/undekodierbare Videos werden nicht sauber als VLC-Fehler angezeigt.
- Statisches `_lastOpened` kann bei mehreren Playerfenstern zum falschen Fenster springen.

### Empfehlung

1. `CodingModeWindow` auf denselben Lifecycle wie `PlayerWindow` bringen.
2. VLC-Events wie `EncounteredError` anzeigen.
3. Player pro Haltung/VideoPath registrieren oder nur ein aktives Playerfenster erlauben.

## 5. Import, Export und Datenfluss

### Gut

- Projekt-Speichern ist solide: Temp-Datei, Backup, `File.Replace`: `JsonProjectRepository.cs:52`.
- Unmatched-Kandidaten werden kopiert, nicht verschoben: `HoldingFolderDistributor.cs:486`.
- Viele Importpfade fangen Fehler pro Datei/Chunk ab.
- Es gibt viele gute Tests fuer PDF, XTF, WinCan, KINS, VSA und Export.

### Problematisch

- DryRun/Vorschau veraendert echte Projektobjekte.
- Projektwechsel schuetzt Dirty-Daten nicht.
- Reimporte schuetzen manuell bearbeitete Felder nicht konsequent.
- `Contains`-Matching kann aehnliche Haltungen verwechseln.
- Mehrdeutige Mediendateien werden teils mit erstem Treffer geloest.
- XTF-Rohdaten landen im App-Ausgabeordner statt im Projektordner.
- PDF-Export kann Protokoll-Eintraege beim Export veraendern.

### Empfehlung

1. DryRun read-only machen.
2. Dirty-Guard vor Oeffnen/Neu einbauen.
3. Import-Merge zentralisieren.
4. Automatische Medienzuordnung konservativer machen.

## 6. KI-Pipeline / Sidecar / Leistung

### Gut

- YOLO/DINO/SAM/Ollama-Struktur ist stark.
- Pipeline-Trace pro Frame ist vorhanden: `PipelineTraceWriter.cs:27`.
- Sidecar-Tests laufen mit korrekt gesetztem `SEWER_SIDECAR_MODELS_DIR`: 38/38.
- Trainingssamples werden nach Datum und Katalog gefiltert.

### Problematisch

- `SidecarTimeoutSec` existiert, wird aber bei vorhandenem `HttpClient` praktisch nicht hart durchgesetzt.
- Qwen-Timeouts widersprechen sich: 300s im MultiModel, 60s im Enhanced-Service.
- VRAM-Plan ist dokumentiert, aber nicht technisch erzwungen.
- `/health` ist zu oberflaechlich und prueft DINO/SAM nicht tief genug.
- Feedback-Lernschleife ist nicht langlebig verdrahtet; der Zaehler startet immer wieder bei 0.
- YOLO-CLS-Vorfilter kann Schaeden verlieren, wenn er zu hart Frames ueberspringt.

### Empfehlung

1. Einheitliche Timeouts pro KI-Stufe.
2. Deep-Health fuer YOLO/DINO/SAM/Qwen.
3. VRAM-Scheduler statt nur Dokumentation.
4. Feedback-Lernen persistent machen.
5. Vorfilter nur nach Benchmark hart aktivieren.

## 7. GIS / Karte

### Gut

- XTF wird streamend gelesen, nicht komplett als XML geladen: `XtfNetworkExtractor.cs:16`.
- LV95 nach WebMercator ist getestet.
- Sichtbarer Kartenausschnitt wird gefiltert.
- Uri-WMS und lokale QGIS-Kacheln sind technisch sinnvoll.

### Problematisch

- XTF- und QGIS-Kachelpfade sind fest auf `D:\QGIS_V4\...` gesetzt.
- Cache-Schreiben ist nicht atomar.
- Grosses Netz wird komplett projiziert, auch wenn spaeter nur Ausschnitt gezeichnet wird.
- Fehlerhafte XTF-Geometrien verschwinden ohne sichtbare Warnung.
- Kartenstatus kann Warnungen ueberschreiben.
- Karte hat keine Abbruchlogik beim schnellen Schliessen.

### Empfehlung

1. Kartenpfade in AppSettings/Projekt speichern.
2. Projizierten Cache oder Spatial Index nutzen.
3. Fehlerzaehler fuer XTF-Geometrien anzeigen.
4. Kartenzustand sauber trennen: Hintergrund, Netz, Auswahl.

## 8. Layout und Bedienung

### Staerken

- Datenansicht nutzt Virtualisierung und eingefrorene Spalten.
- Import hat Vorschau- und Konfliktfenster.
- Detailansicht ist fachlich gruppiert.
- Player ist sehr funktionsreich.

### Schwaechen

- Karte ist versteckt unter Werkzeuge.
- Daten-Toolbar ist ueberladen.
- Tabelle ist nicht sortierbar.
- Suchlabel auf der Haltungsseite sagt `Suche Schacht`.
- Export-Verteilung nutzt Ja/Nein/Abbrechen als Wizard.
- Detailfenster braucht viel Platz.
- Player ist stark, aber sehr dicht.

### Empfehlung

1. Karte als Hauptnavigation neben Haltungen/Schaechte.
2. Daten-Tabelle sortierbar machen.
3. Gefaehrliche Aktionen klar bestaetigen.
4. Export als echten Assistenten bauen.
5. Player-Modi klarer trennen: Basis, Codierung, KI.

## 9. Tests und Build

### Geprueft

- `dotnet build AuswertungPro.sln -v minimal`: 0 Fehler, 0 Warnungen.
- `dotnet test AuswertungPro.sln -v minimal --no-restore`: Exitcode 0.
- Infrastructure: 181 Tests, 180 bestanden, 1 uebersprungen.
- UI: 275 Tests, alle bestanden.
- Pipeline laut Teil-Audit: 281 Tests, alle bestanden.
- Map laut Teil-Audit: 29/29 bestanden.
- Sidecar laut Teil-Audit: zuerst 36/38, danach mit `SEWER_SIDECAR_MODELS_DIR=sidecar/models` 38/38.

### Testluecken

- DryRun darf Projekt nicht veraendern.
- Dirty-Guard vor Projektwechsel.
- CodingModeWindow Close-Smoke-Test.
- Kaputte Videodateien.
- Live-Control Auth/Body-Limit.
- Mehrdeutige Medien.
- UI-Screenshot-Tests fuer Karte/Player/Toolbar.

## 10. Vergleich mit anderen Programmen

### WinCan VX

WinCan positioniert VX als Kernsoftware fuer Inspektion, Codierung, Reporting, Sanierungsplanung sowie Integration mit KI, GIS und Enterprise-Systemen. Die deutsche Produktseite nennt GIS-Kompatibilitaet, ESRI/QGIS-Plugins und WinCan Map.

Ableitung fuer SewerStudio: Nicht versuchen, alles als grosses Enterprise-System nachzubauen. Wichtig sind robuste Datenqualitaet, klare Karte, sicherer Player und nachvollziehbare Berichte.

Quelle: https://www.wincan.com/de/produkt/wincan-vx/  
Quelle: https://www.wincan.com/solutions/operations-management/

### IBAK IKAS

IBAK beschreibt IKAS als Plattform fuer Inspektion, Datenmanagement, Sanierung, Reinigung und Dichtheitspruefung. Die IKAS-Broschuere betont Assistenten, integrierte GIS-Ansicht, Datenkontrolle, Profile und konfigurierbare Uebergaben.

Ableitung fuer SewerStudio: Der wichtigste UI-Schritt ist nicht mehr Feature-Anzahl, sondern gefuehrte Arbeitsablaeufe. Import, Pruefung, Karte, Player und Export sollten wie ein klarer Ablauf wirken.

Quelle: https://www.ibak.de/en/software/ikas-platform  
Quelle: https://www.ibak.de/fileadmin/website/ansprechpartner/flyer_prospekte/ikas_evolution_a5_en.pdf

### QGIS

QGIS unterstuetzt lokale XYZ- und MBTiles-Kacheln sowie WMS/WMTS. Die Raster-Tools koennen XYZ-Kacheln als Ordner oder MBTiles erzeugen.

Ableitung fuer SewerStudio: QGIS-Kacheln sind fuer dich sinnvoller als QGIS-Server, solange die Karte nicht sekundenaktuell sein muss.

Quelle: https://documentation.qgis.org/3.40/de/docs/user_manual/working_with_vector_tiles/vector_tiles.html  
Quelle: https://documentation.qgis.org/3.40/de/docs/user_manual/processing_algs/qgis/rastertools.html

### ArcGIS / Utility Network

ArcGIS legt den Schwerpunkt auf Netzwerkmanagement, Karten im Feld und Buero, Asset-Reports, Tracing, Topologie und Qualitaetspruefung.

Ableitung fuer SewerStudio: Sinnvoll waeren spaeter Topologie-Pruefungen und Plausibilitaetschecks. Ein komplettes ArcGIS-Utility-Network nachzubauen waere uebertrieben.

Quelle: https://www.esri.com/en-us/industries/water-utilities/business-areas/network-management  
Quelle: https://learn.arcgis.com/en/projects/get-started-with-arcgis-utility-network-for-stormwater/

### NASSCO / KI

NASSCO weist darauf hin, dass ADR/KI nicht als voll zertifizierter Ersatz fuer gepruefte Codierung gilt. KI sollte als Assistenz mit menschlicher Kontrolle behandelt werden.

Ableitung fuer SewerStudio: Genau dieser Kurs ist richtig: KI vorschlagen lassen, Mensch bestaetigt, Feedback wird gelernt.

Quelle: https://www.nassco.org/education-and-training/pacp-lacp-macp/pacp-software-hidden/  
Quelle: https://nassco.org/resource/pacp%EF%B8%8F-codes-and-automated-defect-recognition/

## 11. Verbesserungsplan

### Sofort: 1-2 Tage

1. DryRun wirklich read-only machen.
2. Dirty-Guard vor Projekt oeffnen/neu.
3. Einzelnes Loeschen und Spalte leeren absichern.
4. `CodingModeWindow` sicher schliessen.
5. Live-Control mit Token und Body-Limit versehen.

### Kurzfristig: 1-2 Wochen

1. WinCan/IBAK/KINS mit `UserEdited`-Schutz.
2. Unscharfes `Contains`-Matching entfernen.
3. Medien-Matching konservativer machen.
4. Karte konfigurierbar machen.
5. KI-Timeouts vereinheitlichen.
6. Sidecar Deep-Health anzeigen.

### Mittelfristig: 1-2 Monate

1. VRAM-Scheduler fuer KI.
2. Feedback-Lernschleife persistent.
3. Export-Assistent statt Ja/Nein-Kette.
4. Karte als Hauptnavigation.
5. Sortierbare Daten-Tabelle.
6. UI-Screenshot-Smoke-Tests.

### Spaeter / optional

1. QGIS-Kachel-Export automatisieren.
2. Topologie-Pruefungen fuer Netzlogik.
3. Bessere Sanierungsplanungsansicht.
4. Projektweite Diagnose-Seite.
5. Paketierte Release-Version mit sauberem Datenordner.

## 12. Staerken

- Fachlich sehr breites Programm.
- Build und Tests sind sauber.
- VSA-Regellogik ist deutlich besser abgesichert als typisch bei Solo-Projekten.
- Projekt-Speichern ist robust.
- PlayerWindow wurde gezielt stabilisiert.
- GIS-Bausteine sind testgetrieben aufgebaut.
- KI-Pipeline hat nachvollziehbare Traces.
- Viele Importformate werden realistisch abgedeckt.

## 13. Offene Fragen

1. Soll die Karte pro Projekt eigene XTF-/Kachelpfade speichern oder global in den Einstellungen?
2. Soll Live-Control spaeter nur fuer dich aktiv sein oder als dauerhaftes Diagnosewerkzeug bleiben?
3. Soll KI bei fehlendem Sidecar hart stoppen oder sichtbar auf Qwen-only zurueckfallen?
4. Soll bei Video-Matching konservativ immer gefragt werden, sobald Datum/Filmname fehlt?

