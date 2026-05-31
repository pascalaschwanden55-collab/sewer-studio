# Design-Spec: Editierbare Haltungsansicht (alternative DataGrid-Darstellung)

**Datum:** 2026-05-31 · **Status:** Freigegeben (Brainstorming) · **Branch-Ziel:** feature/gis-karte (oder Folge-Branch)

## Ziel
Eine zweite, modernere Darstellung der Haltungsdaten neben der bestehenden Tabelle: **Liste links · editierbares Detail rechts** (gemäß User-Mockup `haltungsansicht-alternative-v2.html`). Alle Felder sind editierbar mit denselben Dropdown-Begriffen wie die Tabelle. Tabelle und Export bleiben **1:1** erhalten. Änderungen sind in beiden Sichten **redundant** (eine Datenquelle, bidirektional live).

## Kernprinzip: eine Datenquelle, zwei Sichten
Tabelle und Haltungsansicht binden an **dieselben `HaltungRecord`-Objekte** aus `Project.Data` — **keine zweite Datenhaltung**.
- `HaltungRecord.SetFieldValue(...)` aktualisiert `Fields[<feld>]`, schützt user-editierte Werte (`HaltungRecord.cs:52-53`) und meldet `PropertyChanged` für `Fields[<feld>]` (`HaltungRecord.cs:70-72`).
- Beide Sichten lesen/schreiben **ausschließlich** über diesen Pfad → Änderung in einer Sicht erscheint sofort in der anderen (bidirektional, ohne Sync-Code). Export liest weiterhin nur `record.Fields` → unverändert.

## Ansatz: C (Hybrid)
Gruppierte Karten wie im Mockup, **Felder je Gruppe dynamisch aus dem `FieldCatalog` gerendert** (Label + Control je `FieldType`, Combos aus `GetComboItems`). Erfüllt „alle Felder editierbar + gleiche Dropdowns" bauartbedingt, weil es dieselbe Quelle wie die Tabelle ist.

## Aufbau der Ansicht
**Platzierung:** Auf der Datenseite, Umschalter **„Tabelle" ↔ „Haltungsansicht"** über dem Datenbereich. Toolbar + Statuszeile bleiben. **Geteilte Auswahl:** dieselbe markierte Haltung in beiden Sichten (beim Umschalten erhalten).

**Liste links:** je Haltung Nr · ID · `DN… · Länge · Nutzung` · farbiger Zustandsklasse-Chip (gleiche Farbe wie die Tabellenspalte, via bestehender `ZustandsklasseCellStyleFactory`). Klick/↑↓ wählt. **Virtualisiert** (wie heutige Tabelle) für große Netze.

**Detail rechts:** Kopf mit Titel „Haltung <ID>" + Aktionen **Video / PDF / Dossier** (rufen bestehende Funktionen). Darunter scrollbarer Body mit gruppierten Karten:
1. **Stammdaten** – Strasse, Rohrmaterial, DN, Nutzungsart, Länge, Inspektionsrichtung …
2. **Zustand** – Zustandsklasse als Farbkarte (editierbar wie heute) + VSA-Noten.
3. **Betrieb / Verwaltung** – Referenzprüfung, Sanieren, Ausgeführt durch, offen/abgeschlossen, Gewässerschutz, Grundwasserspiegel, Funktionale Hierarchie, … (alle restlichen Felder).
4. **Primäre Schäden** – Meter-Leiste + Befundliste, **nur Anzeige**.
5. **Sanierung & Medien** – Aktionskacheln (bestehende Funktionen).

**Scrolling:** Liste (links) und Detail-Body (rechts) scrollen eigenständig; Umschalter/Toolbar/Status/Detail-Kopf bleiben fix.

## Feld-Rendering im Detail
Control je `FieldType` — gespiegelt von der Tabelle:
| FieldType | Control | Quelle |
|---|---|---|
| `Combo` | ComboBox | `FieldCatalog.GetComboItems(feld)` (dieselben Begriffe wie Tabellenspalte) |
| `Int` | TextBox, nur Ziffern (wie `digitsOnly` im Grid) | — |
| `Decimal` | TextBox, Dezimal | — |
| `Text` | TextBox | — |

**Editierbarkeit = 1:1 wie die Tabelle.** Kein Feld bekommt eine andere Regel als in der Tabelle (löst auch die VSA-Noten-Frage). 

**Gemeinsamer Commit-Pfad (kleiner Refactor, DRY):** Die Tabelle committet heute in `Grid_CellEditEnding` → `record.SetFieldValue(feld, wert, FieldSource.Manual, userEdited:true)` (`DataPage.xaml.cs:1435,1450,1457`), inkl. Ziffern-/Combo-Normalisierung. Diese Normalisierung + Set wird in eine **gemeinsame Methode** extrahiert (z. B. `HaltungFieldEditor.Commit(record, feld, rohwert)`), die **beide** Sichten aufrufen. Damit sind AutoSave, `UserEdited`-Schutz, Normalisierung und Change-Benachrichtigung **garantiert identisch** → Redundanz lückenlos.

## File-Struktur (fokussiert, Layer-Disziplin)
Neue, kleine Einheiten statt die großen `DataPage`-Dateien weiter aufzublähen:
| Datei | Verantwortung |
|---|---|
| `…/Views/Pages/Haltungsansicht/HaltungDetailView.xaml(.cs)` | Detail rechts: gruppierte Karten, rendert Felder dynamisch |
| `…/Views/Pages/Haltungsansicht/HaltungListView.xaml(.cs)` | Liste links (virtualisiert) |
| `…/ViewModels/Pages/HaltungsansichtViewModel.cs` | bindet an `Project.Data` + geteilte Auswahl; keine neue Geschäftslogik |
| `…/Views/Pages/Haltungsansicht/DetailFieldGroups.cs` | Gruppen→Feldlisten-Zuordnung (rein, testbar) |
| `…/Views/Pages/Haltungsansicht/HaltungFieldEditor.cs` | gemeinsamer Commit-/Normalisierungs-Pfad (von Tabelle + Detail genutzt) |
| Umschalter in der bestehenden DataPage | minimal: Tabelle ↔ Haltungsansicht, geteilte Auswahl |

## Tests
**Unit:**
- **Feld-Vollständigkeit:** Vereinigung aller `DetailFieldGroups` == alle editierbaren `FieldCatalog`-Felder (keins fehlt/doppelt) → „alle Felder editierbar" beweisbar.
- **Control-Zuordnung:** `FieldType → Control`; Combo→`GetComboItems` liefert dieselben Begriffe wie die Tabelle.
- **Commit-Pfad:** `HaltungFieldEditor.Commit` → `record.Fields` aktualisiert + `UserEdited` gesetzt + (für Int) Ziffern-Normalisierung; Verhalten identisch zum bisherigen Grid-Commit.
- **Schäden-Anzeige:** falls aus einem Feld/Protokoll geparst → reine Parse-Funktion (Marker/Spannen) testen.

**Manuell (GUI-Abnahme):** Edit-Sync beide Richtungen; alle Felder + richtige Dropdowns; Scrollen Liste/Body; Umschalter behält Auswahl; großes Netz flüssig; Export unverändert.

## Abgrenzung (bewusst NICHT)
- Primäre Schäden/Protokoll bleibt **Anzeige** (Codieren im Player/Codiermodus).
- Keine Änderung an Tabellenstruktur, Spalten, Export.
- Keine neue Geschäftslogik, keine zweite Datenhaltung.
- Tabelle bleibt (kein Ersatz). Meter-Leiste = vereinfachte Visualisierung, kein Protokoll-Editor.

## Offene Punkte / Risiken (im Plan zu klären)
- **Exakter Grid-Commit-Pfad** beim Extrahieren von `HaltungFieldEditor` 1:1 erhalten (Tabelle darf sich nicht ändern; nur Aufrufer-Umstellung). Bestehende Grid-Tests müssen grün bleiben.
- **Datenquelle der Meter-Leiste:** Feld `Primaere_Schaeden` (String parsen) vs. `record.Protocol`/`VsaFindings` — im Plan festlegen.
- **Zustandsklasse-Farbe** über bestehende `ZustandsklasseCellStyleFactory` wiederverwenden (eine Quelle).
- **Berechnete VSA-Noten:** Editierbarkeit exakt wie in der Tabelle spiegeln (keine neue Sonderregel).
