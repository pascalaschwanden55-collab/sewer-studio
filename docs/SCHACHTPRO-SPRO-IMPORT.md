# SchachtPro-Import (.spro) — Stufe A + B

Stand: 2026-07-28. Additiver sechster manueller Importweg neben PDF, XTF, WinCan,
IBAK und KINS. Der bestehende PDF-Import bleibt unveraendert.

## Quellformat

`.spro` ist ein ZIP-Archiv der Android-App **SchachtPro** (Repo `D:/SchachtPro_4.5`):

- `manifest.json` — `ArchiveManifest` (formatVersion=1, dbSchemaVersion=21, Projektliste)
- `projects/<exportId>.json` — `ProjectSnapshot` (ProjectDto + Protokolle)
- `photos/<exportId>/<protokollIdx>_<fotoIdx>.jpg` — Protokoll-Fotos
- `logos/<exportId>.jpg` — Auftraggeber-Logo (wird aktuell nicht importiert)

Autoritative Felder: `ProtocolEntity.kt` / `ProjectArchive.kt`. Die JSON-Felder
`lv95East`/`lv95North` enthalten **LV95 Ost/Nord (kein WGS84!)** — sie werden
unveraendert als `Koordinate_East`/`Koordinate_North` am Schacht gespeichert,
ohne Transformation. Projekt-Modus `PRO|LITE`: LITE importiert nur Schachtnummer,
Datum, Bemerkung, GPS und Fotos.

## Aufbau

| Datei | Zweck |
| --- | --- |
| `Application/Import/IImportServices.cs` | Vertrag `ISchachtProImportService` |
| `Infrastructure/Import/SchachtPro/SchachtProArchiveReader.cs` | ZIP-Lesen mit den Limits des App-Importers (10'000 Eintraege, 200 MB/Eintrag, 2 GB gesamt, 5 MB Manifest, 20 MB Projekt-JSON), Zip-Slip-/Pfad-Guard, Versions-Guard (formatVersion > 1 oder dbSchemaVersion > 21 → `UNSUPPORTED_VERSION`) |
| `Infrastructure/Import/SchachtPro/SchachtProArchiveDtos.cs` | System.Text.Json-DTOs mit den exakten Gson-Feldnamen |
| `Infrastructure/Import/SchachtPro/SchachtProFieldNames.cs` | Zentrale kanonische Feldnamen (Stammdaten teilen sich die Namen mit dem PDF-/WinCan-Import) |
| `Infrastructure/Import/SchachtPro/SchachtProZustandMapper.cs` | Stufe B: Label → D-Code + Charakterisierung1 + Schadensklasse |
| `Infrastructure/Import/SchachtPro/SchachtProProtocolMapper.cs` | ProtocolDto → Felder + Protokoll-Eintraege |
| `Infrastructure/Import/SchachtPro/SchachtProImportService.cs` | Orchestrierung, Matching, Fotos, Statistik |

## Abbildung

- **Stammdaten** in die kanonischen Felder des PDF-/WinCan-Imports: `Schachtnummer`
  (+ `NR.`/`Nr.`), `Funktion`, `Schachtform`, `Dimension` + `Durchmesser`,
  `Schachttiefe`, `Material`, `Ausführung Datum/Jahr` + `Datum/Jahr`, `Bemerkungen`,
  `Primäre Schäden` (Zusammenfassung, `Maengelfrei` wenn nichts codiert).
  Nicht-leere Archivwerte ueberschreiben (Konvention des Schacht-PDF-Imports);
  leere Archivwerte loeschen nichts. Fuer `Ausführung Datum/Jahr` und
  `Primäre Schäden` werden zusaetzlich die ASCII-Legacy-Aliase
  (`Ausfuehrung Datum/Jahr`, `Primaere Schaeden`) mitgeschrieben, wie es der
  Schacht-PDF-Import tut (Mojibake-Varianten bewusst nicht).
- **Schachtaufbau/Anschluesse/GPS/Fotos** als neue kanonische Felder, zentral in
  `SchachtProFieldNames` definiert (z.B. `Deckelform`, `Schachthals-Form`,
  `Konus-Höhe`, `Anschlüsse`, `Fotos`, `Koordinate_East`/`_North`).
- **Schaeden** als `ProtocolEntry`: `Code` = Bauteil in der Ordnung des PDF-Imports
  (`SchachtProtocolParser.SchachtComponentOrder`), `Beschreibung` =
  „<Label> — <D-Code>-<Char>, <Klasse>" (z.B. „gerissen — DAB-B, K2").
  Das Protokoll-Dokument hat dieselbe Form wie beim PDF-Import (Original-Revision
  + Arbeitskopie), damit Dossier-Export und UI beide Wege gleich darstellen.
- **Kein CodeMeta/Severity**: die D-Code-Information steht vollstaendig im Text
  (Konvention des Schacht-PDF-Imports; bewusst kein VSA-CodeMeta, damit der
  Haltungs-Code-Picker keine D-Codes sieht).
- **Fotos** nur mit Datei-Staging (echter UI-Importlauf): Ablage unter
  `Fotos/Schächte/<Schacht>/`, Verlinkung als relative Pfade im Feld `Fotos`
  (`;`-getrennt). Re-Import ist idempotent (gleicher Inhalt wird erkannt).
  Ohne Staging (Vorschau) wird nichts kopiert.
- **Matching/Idempotenz**: SchachtRecord wird ueber die Schluessel-Felder wie im
  WinCan-Import gesucht (`Schachtnummer`, `SchachtNr`, ...); Treffer → Updated,
  sonst Neuanlage → Created.
- **Re-Import-Schutz (manuelle Arbeit bleibt bestehen)**: Beim erneuten Import
  wird das Protokoll nicht blind ersetzt (Konvention wie
  `VsaFindingProtocolSynchronizer`: ein Import loescht keine Benutzerarbeit):
  - Manuell **hinzugefuegte oder veraenderte** Eintraege der Arbeitskopie werden
    uebernommen (inkl. EntryId, Fotos, Metadaten).
  - Manuell **geloeschte** Import-Eintraege werden nicht wieder hinzugefuegt
    (im neuen Original-Snapshot sind sie weiterhin dokumentiert).
  - Die bisherige Arbeitskopie wandert als Revision in die **History**
    („... (vor SchachtPro-Re-Import)").
  - Ein inhaltsgleicher Re-Import laesst das Dokument komplett unangetastet
    (EntryIds stabil, keine History-Flut).
  - Der Abgleich ist inhaltsbasiert (Bauteil + Beschreibung), weil jeder Import
    neue EntryIds erzeugt. Folge: ein vom Benutzer **umformulierter** Eintrag
    erscheint nach dem Re-Import doppelt (korrigierte Fassung + frische
    Import-Fassung) — bewusst so: lieber sichtbar doppelt als still verloren;
    wer die Import-Fassung loescht, bleibt bei Folgeimporten unbehelligt.

## Norm-Mapping (Stufe B)

Universelle Labels und Sonderschluessel (Steighilfe, Tauchbogen) gemaess
`docs/SchachtPro_XTF_Export_Konzept.xlsx`; die Label-Strings folgen der App
(`ZustandPage.kt`, autoritativ). `Mängelfrei`, `verschraubt`, `vorhanden`,
`nicht notwendig`, `leiter`, `steigeisen` erzeugen bewusst keine Codierung.
Unbekannte Labels (z.B. die Anschluss-Optionen „mangelhaft eingebunden",
„breite Fuge", „verschlossen", „einragend") werden nicht geraten: sie zaehlen
als `Uncertain` und gehen als Klartext in die Beschreibung. Label-Vergleich ist
Gross-/Kleinschreibung-tolerant (App schreibt bei Anschluessen „infiltration" klein).

## Fehlerstrategie

- Archiv-Level (Zip-Slip, Limit-Verletzung, Manifest ungueltig, zu neue Version):
  harter Fehler (`Result.Fail`), nichts wird importiert.
- Projekt-Level (fehlendes/beschaedigtes Projekt-JSON, abweichende Export-ID):
  Fehler gezaehlt, andere Projekte laufen weiter.
- Protokoll-Level (beschaedigtes Protokoll-Element): Fehler gezaehlt, andere
  Protokolle desselben Projekts laufen weiter. Protokolle ohne Schachtnummer:
  `Uncertain`, kein Record.

## UI-Anbindung

Import-Seite → „Weitere Quellen" → „SchachtPro-Archiv (.spro)". Der Lauf nutzt
denselben `ImportManualWorkflowController` wie die fuenf bestehenden Wege
(Vorschau, Restore-Point, Transaktions-Marker, Quell-Ablage unter
`Imports/SchachtPro`, Bericht).

## Tests

`tests/AuswertungPro.Next.Infrastructure.Tests/Import/SchachtProImportServiceTests.cs`
(+ `...Helpers.cs`): Happy Path (2 Protokolle, Fotos, GPS), komplette
Mapping-Tabelle, Versions-Guard, Fehlerisolierung, Zip-Slip, Idempotenz,
LITE-Modus, Re-Import-Schutz (manuelle Eintraege/Loeschungen/Edits bleiben
bestehen, History-Archivierung, No-op bei identischem Import). Das Test-Archiv
wird zur Laufzeit als ZIP erzeugt.
