# Design: Einfache Schacht-Sanierungsmassnahmen (ohne NPK)

Datum: 2026-07-07
Status: Genehmigt (User: "baue es selber")
Branch: feature/gis-karte

## Ziel

Fuer die **Schacht**-Sanierung eine **einfache, selbst gepflegte Massnahmen-Liste
mit manuellen Preisen** — bewusst NICHT ueber das NPK-Katalog-/Template-System.
Per **Rechtsklick auf einen Schacht -> "Sanierungsmassnahmen..."** oeffnet sich ein
Fenster (Layout an die Haltungen angelehnt), in dem der Anwender Massnahmen anklickt.
"Uebernehmen" schreibt die Namen ins Feld **"Massnahmen"** und die Summe ins Feld
**"Kosten"** des Schachts -> beide sind bereits Spalten der `Schaechte.xlsx` (Kopfzeile 12)
und werden vom bestehenden Export geschrieben.

## Entscheidungen (aus Rueckfragen)

1. **Bedienung:** Rechtsklick auf Schacht -> "Sanierungsmassnahmen...", Fenster-Layout
   an die Haltungen angeglichen (schlanke Variante, ohne NPK/KI).
2. **Excel-Ausgabe:** Massnahmen-Namen in Feld "Massnahmen", Gesamtsumme in Feld "Kosten".
3. **Preis pro Schacht anpassbar:** Menge und Preis je Schacht ueberschreibbar, ohne die
   globale Liste zu aendern.
4. **Verhaeltnis zur NPK-Schacht-Matrix:** additiv. Die bestehende NPK-Schacht-Matrix
   (`schacht_costs.json`, Kapitel 700) bleibt vollstaendig unangetastet.
5. **Katalog-Liste global:** eine Liste fuer alle Projekte (wie die bestehenden
   Dropdown-Listen unter `%AppData%`).
6. **Persistenz der Auswahl:** pro Projekt in eigener Datei `schacht_empfehlungen.json`,
   damit die Auswahl inkl. angepasster Preise/Mengen wieder bearbeitbar ist.

## Bestaetigte Fakten aus der Codebase

- `SchaechteRecord` hat kein typisiertes Feldmodell, nur `Fields` (freies Dictionary).
- `Schaechte.xlsx` Kopfzeile 12 enthaelt u.a. Spalte 8 "Massnahmen", Spalte 9 "Kosten".
- `ExcelTemplateExportService.ExportSchaechteToTemplate` wird mit `headerRow: 12, startRow: 13`
  aufgerufen und schreibt fuer jede Kopf-Spalte `rec.GetFieldValue(header)` -> Felder
  "Massnahmen"/"Kosten" landen ohne Vorlagenaenderung im Export.
- Das Schacht-Grid zeigt "Massnahmen" und "Kosten" bereits als Spalten an
  (Feedback nach Uebernehmen sofort sichtbar). "Kosten" wird als Kostenspalte behandelt.
- Muster fuer selbst editierbare Listen: `DropdownOptionsStore` (AppData-JSON, atomar) +
  `OptionsEditor*` / `DropdownOptionGroupController`.
- Muster fuer Record-Feld-Mapping: `SanierungCostFieldMapper` (Haltungen).
- Muster fuer projektlokale Kosten-Persistenz: `ProjectCostStoreRepository`.
- Haltungs-Fenster: `SanierungsmassnahmenWindow` + `SanierungsmassnahmenViewModel`,
  geoeffnet ueber `DataPageSanierungWindowController`.

## Architektur (additiv, kleine Bausteine mit Interface)

### Domain (`AuswertungPro.Next.Domain/Models`)
- `SchachtMassnahmeKatalogEintrag` (record): `Name`, `Preis` (decimal), `Einheit` (string, Default "Stk").
  = ein Eintrag der globalen Liste.
- **Kein neues Auswahl-Modell.** Fuer die pro-Schacht-Auswahl wird bewusst das bestehende,
  getestete `HoldingCost`/`MeasureCost`/`CostLine` wiederverwendet (die Codebase nutzt `HoldingCost`
  bereits fuer Schaechte, siehe `schacht_costs.json`). Eine Auswahl = ein `HoldingCost` mit einer
  Massnahme "Empfohlene Massnahmen", deren `CostLine`s je `Text`=Name, `Qty`=Menge, `UnitPrice`=Preis,
  `Selected`=true tragen. Kein NpkCode/ItemKey noetig.

### Application (reine, testbare Logik)
- `SchachtEmpfehlungTextFormatter` (static): Positionen -> Massnahmen-Text
  (`"A; B; C"`, leere weglassen) und -> Summe. Kultur-invariant.
- `SchachtEmpfehlungRecordMapper` (static): schreibt Text ins Feld "Massnahmen" und
  die Summe (Format wie bestehende Kostenfelder) ins Feld "Kosten" eines `SchachtRecord`;
  `Clear` leert beide. Analog `SanierungCostFieldMapper`.

### Infrastructure (Persistenz)
- `ISchachtMassnahmenKatalogStore` (Interface in `Application.Schacht`) + `SchachtMassnahmenKatalogStore`
  (Impl in `Infrastructure.Schacht`): Load()/Save(list) nach
  `%AppData%(Roaming)\SewerStudio\dropdowns\schacht_massnahmen.json` (gleicher Ordner wie die
  bestehenden Dropdown-Listen; `AtomicTextFileWriter`), mit sinnvoller Default-Liste.
  Verzeichnis fuer Tests injizierbar.
- **Auswahl-Persistenz:** bestehenden `ProjectCostStoreRepository("schacht_empfehlungen.json")`
  wiederverwenden (kein neuer Store). Speichert `HoldingCost` je Schachtnummer nach
  `<Projekt>\costs\schacht_empfehlungen.json` — getrennt von der NPK-Datei `schacht_costs.json`.

### UI (`AuswertungPro.Next.UI`)
- `SchachtMassnahmenViewModel`:
  - `Katalog` (ObservableCollection der Listen-Eintraege), `Positionen` (gewaehlte),
    `Total`, Schacht-Kontext (Nr./Funktion/Zustandsklasse), Titel.
  - Commands: `AddFromKatalog`, `RemovePosition`, `Uebernehmen`, `Abbrechen`, `ListeBearbeiten`.
  - Add: Menge=1, Preis=Listenpreis; Menge/Preis pro Position editierbar -> Total live.
  - Uebernehmen: `SchachtEmpfehlungStore.Save` + `SchachtEmpfehlungRecordMapper.ApplyTo(record)`
    + Projekt dirty + Grid-Refresh.
- `SchachtMassnahmenWindow` (Fenster): Layout wie Haltungen, schlank:
  links Schacht-Info, mitte klickbare Katalog-Liste, darunter gewaehlte Positionen
  (Menge/Preis editierbar, Zeilensumme), Footer mit Total + "Uebernehmen"/"Abbrechen"
  + "Liste bearbeiten...". Theme-Brushes wie bestehende Fenster (sewer-wpf-ui).
- `SchachtMassnahmenKatalogEditor` (kleines Fenster/Dialog): 2-Spalten (Name + Preis),
  Hinzufuegen/Loeschen/Editieren, speichert ueber `ISchachtMassnahmenKatalogStore`.
- `SchachtMassnahmenWindowController` (UI): oeffnet das Fenster fuer den gewaehlten Schacht
  (Muster `DataPageSanierungWindowController`).
- **Verdrahtung:** Rechtsklick-Eintrag "Sanierungsmassnahmen..." im Kontextmenue der
  Schacht-Seite (`SchaechtePage.xaml`) + Aktion im Schachtansicht-Kachel-Menue
  (`RouteSchachtansichtAction`).

### DI
Neue Stores + Controller im `ServiceProvider` registrieren (kein verstreutes `new`).

## Datenfluss

1. Rechtsklick auf Schacht -> "Sanierungsmassnahmen..." -> Controller oeffnet Fenster.
2. Fenster laedt globalen Katalog + evtl. bestehende Auswahl dieses Schachts.
3. Anwender klickt Massnahmen an (Menge/Preis pro Schacht anpassbar).
4. "Uebernehmen": Auswahl -> `schacht_empfehlungen.json` (Schachtnummer-Key);
   Text -> Feld "Massnahmen", Summe -> Feld "Kosten" des Records.
5. Excel-Export (bestehend) schreibt "Massnahmen"/"Kosten" in `Schaechte.xlsx`.

## Tests (fokussiert)

- `SchachtEmpfehlungTextFormatter`: Positionen -> Text + Summe (inkl. leere/kultur).
- `SchachtMassnahmenKatalogStore`: Save/Load Roundtrip + Defaults.
- `SchachtEmpfehlungStore`: Save/Load Roundtrip (mehrere Schaechte).
- `SchachtEmpfehlungRecordMapper`: ApplyTo/Clear setzt/leert "Massnahmen"/"Kosten".
- `SchachtMassnahmenViewModel`: Add (Menge 1, Listenpreis), Preisaenderung -> Total,
  Uebernehmen -> Record-Felder + Store gerufen.

## Bewusst NICHT enthalten (YAGNI)

- Keine NPK-Codes / kein Katalog-Zwang / kein `cost_catalog.json`-Bezug.
- Keine KI-/Regel-Empfehlung, keine Mehrfach-Massnahmen-/Override-Merger-Logik.
- Kein Eingriff in die NPK-Schacht-Matrix, den Sidecar, die KI-Pipeline oder das VRAM-Budget.
- Keine automatische Umbenennungs-Migration von `schacht_empfehlungen.json`
  (gleiches Verhalten wie bestehende Schacht-Kostendatei).
