# CadasterDbReader

Read-only Pilot-Inspector fuer Firebird-Stammdaten-DBs (`.fdb`) unter
`D:/Videoprojekte`. Vendor-neutral: das Tool prueft die Datei am Format
(Firebird + `GISOBJECT`-Schema), nicht am Hersteller.

## Zweck

Das Tool prueft, was in den Stammdaten-Datenbanken wirklich fuer Training
nutzbar ist. Es schreibt nur JSON-Reports nach `tools/CadasterDbReader/output/`
und aendert keine Projekt-, Haltungs- oder KB-Daten.

Pipeline-Grenze:

- Extractor/Inspector, kein Import ins Programm
- Report-Schema `cadaster-db-pilot-v1`
- optionales Trainings-Manifest `cadaster-manifest-v1`
- optionales Verteilungs-Topologie-JSON `haltung-topology-v1`
- keine Vermischung mit PDF-, SQLite-DB- oder XTF-Stages

## Wichtige technische Eigenheit

Die ueblichen Firebird-Embedded-Bibliotheken im Feld sind 32-bit. Deshalb muss
das Tool mit dem 32-bit .NET 6 Runtime gestartet werden:

```powershell
& 'C:\Program Files (x86)\dotnet\dotnet.exe' tools\CadasterDbReader\bin\Debug\net6.0\CadasterDbReader.dll --root "D:\Videoprojekte" --out tools\CadasterDbReader\output\full
```

Bei FDB-Pfaden mit Umlauten kopiert das Tool die jeweilige Datenbank in einen
lokalen ASCII-Cache unter `output/_firebird_ascii_cache/`, weil der alte
Embedded-Client Unicode-Pfade nicht sicher oeffnet. Die Original-FDBs bleiben
unangetastet.

## Aufruf

```powershell
# Build
dotnet build tools\CadasterDbReader\CadasterDbReader.csproj -v minimal

# Alle Arizona.fdb unter D:\Videoprojekte lesen
& 'C:\Program Files (x86)\dotnet\dotnet.exe' tools\CadasterDbReader\bin\Debug\net6.0\CadasterDbReader.dll --root "D:\Videoprojekte" --out tools\CadasterDbReader\output\full

# Zusaetzlich cadaster-manifest-v1 mit Beobachtung + Foto/Video-Bezug schreiben
& 'C:\Program Files (x86)\dotnet\dotnet.exe' tools\CadasterDbReader\bin\Debug\net6.0\CadasterDbReader.dll --root "D:\Videoprojekte" --out tools\CadasterDbReader\output\manifest-full --export-manifest

# Zusaetzlich haltung-topology-v1 fuer den Verteilungs-Validator schreiben
& 'C:\Program Files (x86)\dotnet\dotnet.exe' tools\CadasterDbReader\bin\Debug\net6.0\CadasterDbReader.dll --fdb "G:\<projekt>\...\Data\Arizona.fdb" --root "G:\<projekt>" --fbclient "D:\Videoprojekte\...\Bin\Bin\Bin\fbembed.dll" --out tools\CadasterDbReader\output\<projekt> --export-topology --topology-out tools\HaltungTopologyExtractor\output

# Einzelne FDB lesen
& 'C:\Program Files (x86)\dotnet\dotnet.exe' tools\CadasterDbReader\bin\Debug\net6.0\CadasterDbReader.dll --fdb "D:\Videoprojekte\...\Data\Arizona.fdb" --root "D:\Videoprojekte" --out tools\CadasterDbReader\output\single
```

## Env-Variablen

| Variable | Default | Zweck |
|---|---|---|
| `FDB_USER` | `SYSDBA` | Firebird-User. Default reicht fuer Embedded-Read in der Regel. |
| `FDB_PASSWORD` | `masterkey` | Firebird-Passwort. Default ebenso. |

## Report-Inhalt

Der JSON-Report enthaelt pro FDB:

- Tabellen und Spalten mit RowCounts
- `GISOBJECT` Topologie/Stammdaten-Zahlen
- `STATION` Beobachtungen mit Code-, Distanz-, Bemerkungs- und Sample-Werten
- bekannte Medientabellen wie `FOTO`, `FILMPOS`, `MEDIUMFILE`
- heuristische Kandidatenlisten fuer Media/Observation/Code-Tabellen

## Manifest-Inhalt

Mit `--export-manifest` erzeugt das Tool `cadaster_manifest_<utc>.json`:

- eine Zeile pro `STATION`-Beobachtung
- Code, ContentCode, Distanz, Bemerkung, Quantifizierungsfelder
- einfache `TrainingCategory`: `schaden`, `bauteil`, `meta`, `other`
- Foto-Refs aus `FOTO` plus echter Bildpfad via `MEDIUMFILE.NAME`
- Filmpositionen aus `FILMPOS` plus echter Videopfad via
  `SHOOTINGSEQUENCE -> SHOOTINGFILE -> MEDIUMFILE`

## Topologie-Inhalt

Mit `--export-topology` erzeugt das Tool pro Projekt eine
`topology.json` im gemeinsamen `haltung-topology-v1`-Schema. Die Haltungen
kommen aus den `GISOBJECT`-Stammdatenzeilen `Lt`/`Sc`; deren `OBJ_NAME`
enthaelt das echte Schachtpaar und wird als `canonical_folder_name` verwendet.
Numerische Endungen wie `10113.0` werden zu `10113` normalisiert.

## Aktueller Pilot-Befund (Stand 2026-05-19)

- 4 von 4 Arizona-FDBs verbindbar
- 2'578 `STATION`-Beobachtungen, 100 % mit Code und Distanz
- 1'896 Manifest-Samples mit echtem Foto-Pfad
- 2'242 Manifest-Samples mit echtem Video-Pfad
- 2'433 Manifest-Samples mit mindestens einer Filmposition
- 434 Topologie-Paare und 217 Stammdaten-Zeilen aus `GISOBJECT`
- Kategorien: 661 `schaden`, 516 `bauteil`, 1'352 `meta`, 49 `other`

Ehrliche Einordnung: Die Firebird-Stammdaten-DB ist wertvoll fuer strukturierte
Beobachtungen, Fotos/Filmpositionen und Stammdaten. Sie ist jetzt eine
verwertbare Manifest-Quelle, aber noch keine Trainings-Stage. Der naechste
Schritt ist Cross-Validation mit PDF/SQLite/XTF und danach gezielte
Frame-Extraktion.
