# Ein-Knopf-Import „Kanalfernseh-Projekt" — Design

> Stand 2026-06-30. Konsolidiert aus der Brainstorming-Session mit dem User. Ziel: ein einziger
> Import-Knopf, der einen WinCan- oder IKAS-Quellordner erkennt, die maßgebliche Quelle importiert,
> alles in eine feste Projekt-Ordnerstruktur einsortiert, relativ verlinkt und idempotent bleibt.

## Vision (ein Satz)
**Ein Knopf, ein Quellordner:** Der User wählt nur den Ordner der Kanalfernsehdaten — das Programm
erkennt das Format, importiert die maßgebliche Datenquelle (inkl. Pro-Beobachtung-Fotos), archiviert
die Rohdaten, verteilt Filme/PDFs, legt die Fotos zentral ab, verlinkt alles relativ und hält das
Projekt bei Änderungen (Rename-Sync) konsistent.

## Ziel-Ordnerstruktur (verbindlich)
Die Struktur wird **bei der Projekterzeugung** angelegt (leer); der Import **füllt** sie nur.

```text
<Projekt>\
  Importdateien\
    Datenbanken\        <- Arizona.fdb / *.db3 (Kopie, Archiv)
    XTF\                <- VSA_KEK-XTF, SIA405-XTF (Kopie)
    PDF\                <- Protokoll-PDFs (Kopie)
    TXT\                <- Daten.txt u.ä. (Kopie)
  Haltungen_Verteilt\
    <Haltung>\          <- JJJJMMTT_<Haltung>.mpg, _<Haltung>.pdf, _G.mpg (Gegenrichtung)
  Schächte_Verteilt\
    <Schacht>\          <- JJJJMMTT_<Schacht>_SP.pdf, _<Schacht>_Fotos.pdf (nur PDFs/Dokumente)
  Fotos\
    Haltungen\<Haltung>\<datei>.jpg   <- echte Bilddateien, zentral aber gruppiert
    Schächte\<Schacht>\<datei>.jpg
  Projektdateien\
    projekt.json
  __IMPORT_REPORTS\     <- ein Report je Importlauf
  __RESTORE_POINTS\     <- Restore-Point vor jedem Lauf
```

**Regeln:**
- Echte **Bilddateien** liegen NUR zentral unter `Fotos\Haltungen\…` bzw. `Fotos\Schächte\…` — nicht
  zusätzlich in den Verteil-Ordnern (keine Doppel).
- In den Verteil-Ordnern liegen **Filme** (Haltungen) und **PDF-/Dokumentdateien** (Haltungen + Schächte).
- In den Records werden **ausschließlich relative Pfade** gespeichert (portabel).

## Workflow / Pipeline (`ProjectImportOrchestrator`)
Voraussetzung: ein Projekt ist offen, die leere Struktur existiert. Ein Knopf **„Import
Kanalfernseh-Projekt"** → User wählt Quellordner → Pipeline:

1. **Restore-Point** anlegen (`__RESTORE_POINTS\`) — vor jeder Mutation.
2. **Format erkennen** (`KanalExportDetector`):
   - **IKAS** ⟸ `KiasExportPattern.Detect` (Arizona.fdb + `Film\` + `Dokumente\*.xtf`).
   - **WinCan** ⟸ `*.db3` unter einem `…\DB\`-Ordner (typisch `DISK1\Projects\<name>\DB\`).
   - Beides/keins eindeutig → eine kurze Rückfrage (kein stiller Fehlgriff).
3. **Rohdaten archivieren** → `Importdateien\{Datenbanken,XTF,PDF,TXT}` (Kopie, unverändert; idempotent).
4. **Maßgebliche Quelle parsen** (siehe Datenhoheit) → Haltungen, Schächte, Befunde, Beobachtungen,
   Pro-Beobachtung-Foto-Zuordnung.
5. **SIA405-Whitelist-Anreicherung** (nur IKAS, streng kontrolliert, siehe unten).
6. **Verteilen**: Filme + Protokoll-PDFs → `Haltungen_Verteilt\<Haltung>\`; Schacht-PDFs →
   `Schächte_Verteilt\<Schacht>\`; Bilddateien → `Fotos\Haltungen\<Haltung>\` / `Fotos\Schächte\<Schacht>\`.
7. **Relativ verlinken** (alle Medienpfade relativ zum Projektordner).
8. **Report** schreiben (`__IMPORT_REPORTS\`): Zusammenfassung + jede Konflikt-/Unsicherheits-Zeile.

Die Pipeline ist **nicht-blockierend**: einzelne Fehler (eine kaputte Haltung, ein fehlendes Foto)
isolieren sich in den Report, der Lauf macht weiter.

## Datenhoheit pro Format — eine maßgebliche Quelle
- **IKAS → VSA_KEK-XTF** (`VSA_KEK_2020_LV95`): Wahrheit für Haltung, Befunde, Beobachtungen, Fotos.
  `Arizona.fdb`, `Daten.txt`, Protokoll-PDF werden **nur archiviert** und für Verteilung/Anzeige
  genutzt — **nicht** als zweite Datenquelle geparst.
- **WinCan → `.db3`**: Wahrheit für Stammdaten, Befunde, Fotozuordnung. PDFs/Videos danach nur verteilen.
- **SIA405-XTF (IKAS)**: **optionale Whitelist-Anreicherung**, keine zweite Wahrheit.
- **FDB**: jetzt nicht parsen, nur archivieren.

## Feldpriorität & Konfliktbehandlung
Feste Priorität:

```text
UserEdit  >  Hauptquelle (XTF / db3)  >  SIA405-Whitelist  >  leer
```

- **UserEdit gewinnt immer:** vom User editierte Felder (`FieldSource`/`userEdited`) werden NIE vom
  Import überschrieben.
- **Hauptquelle** setzt alle nicht-editierten Felder.
- **SIA405-Whitelist** füllt NUR, wenn das Feld nach Hauptquelle noch **leer** ist, und NUR diese Felder:
  - `Rohrmaterial` (Material), `DN_mm`, `Nutzungsart`, `Strasse`, **Geometrie** (Lage/Koordinaten).
- **Niemals durch SIA405 überschrieben** (auch nicht wenn gefüllt): `Datum_Jahr`, `Bemerkungen`,
  `Haltungslaenge_m`, **Befunde** — genau diese zeigten bei Fürlauwi Konflikte.
- **Jeder Konflikt** (Hauptquelle-Wert ≠ vorhandener Nicht-leer-Wert; SIA405-Wert ≠ Hauptquelle) →
  **eine Report-Zeile**, **kein Abbruch**.

## Foto-Ablage (zentral + gruppiert)
- Bilddateien → `Fotos\Haltungen\<Haltung>\<datei>` bzw. `Fotos\Schächte\<Schacht>\<datei>`.
- Records (`entry.FotoPaths`, `finding.FotoPath`) speichern den **relativen** Pfad dorthin.
- **Pro-Beobachtung-Zuordnung** kommt aus der Hauptquelle:
  - IKAS-XTF: `KEK.Datei.Objekt → Kanalschaden-TID` (bereits gefixt, Commit f9947528).
  - WinCan-`.db3`: `SECOBSMM.OMM_Observation_FK → SECOBS.OBS_PK` (bereits korrekt, 100 % verifiziert).
- **Anpassung nötig:** die zuletzt eingeführte *flache* `Fotos\`-Ablage (f9947528) wird auf die
  gruppierte Variante `Fotos\Haltungen\<Haltung>\` umgestellt (`MediaDistributionService`,
  `ProjectPhotoAssignmentService`).

## Schächte-Verteilung
- `Schächte_Verteilt\<Schacht>\` enthält **PDFs/Dokumente** (z.B. `…_SP.pdf` Schachtprotokoll,
  `…_Fotos.pdf` falls als PDF vorhanden) — analog zu den Haltungen.
- **Keine** einzelnen Bilddateien hier — die liegen zentral unter `Fotos\Schächte\<Schacht>\`.
- Schacht-Records kommen aus der Hauptquelle (WinCan `NODE`/`NODINSP`; IKAS-XTF Normschacht/Knoten).

## Idempotenz (erneuter Import)
Ein zweiter Importlauf darf **nichts doppeln**:
- **Records:** nach normalisiertem Haltungs-/Schacht-Schlüssel matchen (vorhandene Logik) → vorhandene
  aktualisieren statt neu anlegen; UserEdit-Felder bleiben unangetastet.
- **Dateien:** vorhandene Ziel-Datei mit gleichem Namen + gleicher Größe → wiederverwenden (kein
  Re-Copy, keine `_1`-Duplikate). Abweichende Größe → kollisionssicher (Suffix) + Report-Hinweis.
- **Fotos/Verteilung:** Zielpfad ist deterministisch (Haltung/Schacht im Namen) → erneuter Lauf trifft
  dieselben Pfade.
- Vor jedem Lauf Restore-Point, danach Report — Sicherheitsnetz bleibt.

## Bedienung (Knopf)
- **Neuer Hauptweg:** ein prominenter Knopf **„Import Kanalfernseh-Projekt"** (Quellordner-Auswahl).
- **Alte Knöpfe behalten** (PDF/XTF/WinCan/IBAK/KINS) — unter „Manuell/Spezialfall", nicht prominent,
  aber erreichbar: Rettungsweg für kaputte/ungewöhnliche Exporte. **Nicht löschen.**

## Komponenten
**Neu:**
- `ProjectImportOrchestrator` — die Pipeline (detect → archive → parse → enrich → distribute → link → report).
- `KanalExportDetector` — Format-Erkennung (WinCan vs IKAS), baut auf `KiasExportPattern`.
- `ImportSourceArchiver` — Roh-Quellen nach `Importdateien\{…}` (idempotent).
- SIA405-Whitelist-Merge (kontrollierte Anreicherung mit Konflikt-Logging).

**Anpassen:**
- **Projekterzeugung** legt die feste Struktur an (leer).
- `MediaDistributionService` → Ziele `Haltungen_Verteilt\`, `Schächte_Verteilt\`, gruppierte
  `Fotos\Haltungen\`/`Fotos\Schächte\`.
- `ProjectPhotoAssignmentService` → gruppierte Fotos.
- `ImportPageViewModel` → neuer Hauptknopf + Verschieben der alten Knöpfe nach „Manuell".

**Wiederverwenden (unverändert):**
- `LegacyXtfImportService` (VSA_KEK, Pro-Beobachtung-Fotos), `WinCanDbImportService` (.db3),
  `HoldingFolderDistributor` (Film/PDF-Verteilung + Video-Matching), `HoldingRenameService`
  (Rename-Sync), `ProjectPathResolver` (relative Pfade), `KiasExportPattern`.

## Verifikation (Definition of Done)
- Einen IKAS-Quellordner (Meien) wählen → 1 Knopf → Projekt gefüllt: Haltungen+Befunde aus VSA-XTF,
  Fotos **pro Beobachtung** (118→BCD, 119→BAA…), Filme/PDFs verteilt, Rohdaten in `Importdateien\`,
  Report ohne Abbruch.
- Einen WinCan-Quellordner (Altdorf 1.15) wählen → 1 Knopf → analog, Fotos pro Beobachtung aus `.db3`.
- **Erneuter Import** desselben Ordners → keine Doppel, kontrollierte Aktualisierung, UserEdits intakt.
- Haltungsnummer im Programm ändern → Ordner/Dateien/PDF-Text + Foto-Pfade ziehen mit (Rename-Sync).
- Projektordner an fremden Pfad kopieren → `.json` öffnen → alles da (relative Pfade).

## Bewusst NICHT (jetzt)
- **Arizona.fdb parsen** — Firebird-Weg verworfen (fragil, unnötig: XTF liefert Pro-Beobachtung-Fotos).
  FDB nur archivieren.
- **SIA405 als zweite Wahrheit** — nur Whitelist-Ergänzung leerer Felder.
- **Mehr-Disk-WinCan / Gegeninspektions-2.-Video-Persistenz** — separate, bereits notierte Themen.
