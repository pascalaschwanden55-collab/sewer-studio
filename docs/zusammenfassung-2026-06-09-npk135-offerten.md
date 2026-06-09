# Zusammenfassung 2026-06-09 — NPK-135-Offerten-Workflow + UI

**Branch:** `feature/gis-karte` · **5 Commits** · Build durchgehend **0/0**, **41 Cost-Tests grün**

Roter Faden: Aus dem App-Kostensystem einen durchgängigen **NPK-135-Offerten-Workflow** gemacht —
pro Haltung Massnahme wählen → Auto-Mengen + Regeln → **ein** aggregiertes Leistungsverzeichnis.
Plus: **ein** anwendbarer Preis-Katalog und die Haltungsansicht als Standard.

Kern-Erkenntnis am Anfang: Die App konnte das **schon zu 80–90 %** (Massnahme=Bündel, Auto-Mengen
inkl. Anschluss-Dedup, projektweite Aggregation im PDF). Es fehlten NPK-Nummern, die Ablaufregeln,
ein nutzbarer Aggregator-Export und die Massen-Matrix. Nichts neu erfunden — nachgerüstet.

---

## Commit 1 — `7bc23784` · Phase 1+2: NPK-Katalog, Regeln, aggregiertes LV

### Phase 1 — Katalog + Ablaufregeln
- **`CostCatalogItem`** um `NpkCode` + `Chapter` erweitert (additiv); `CloneItem`/`Normalize` in
  `CostCatalogStore` mitgepflegt (sonst Merge-Verlust).
- **`cost_catalog.json`** auf **volle NPK-135-Nummern** umgestellt (612 Liner, 621 Endmanschette,
  515 Anschluss verpressen, 521 Kurzliner, 522 Manschette, 351 einmessen, 211/221 Reinigung/TV,
  311 Fräsen, 411 Wasserhaltung).
- **Einheiten korrigiert:** Vorreinigung / Fräsen / TV-Vor / TV-Abnahme → **m** (damit die
  bestehende Auto-Längen-Logik greift: jede m-Zeile = Haltungslänge). m-Referenzpreise aus den
  Bürglen-Offerten (Reinigung 5.–/m, Fräsen 29.–/m, TV 3.–/m).
- **`measure_templates.json`** an die Regeltabelle angepasst: Fräsen ankreuzbar, Anschluss-Auffräsen
  + Endmanschette automatisch.
- **`EnforceEndManschetteRule`** (neu, Vorbild `EnforceInstallationRule`): Endmanschette 2 Stk
  **nur ab DN 200**, darunter deaktiviert. Trigger bei DN-Änderung.

### Phase 2 — Eine aggregierte NPK-Liste über alle Haltungen
- **`AggregatedPosition`** (Domain-Record) + **`ProjectPositionAggregator`** (Service): zählt
  gleiche Position über alle Haltungen zusammen; **DN-Split nur bei ByDN-Positionen** (Liner/
  Manschetten), Fixed (Reinigung/Fräsen) über alle DN zusammen; NpkCode/Chapter via ItemKey.
- **`NpkLeistungsverzeichnisExporter`**: CSV, gruppiert nach NPK-Kapitel, Zwischen- + Gesamttotal,
  EP-Spalte leer wo Preis variiert.
- **Druckcenter**: Command + Button „NPK-Leistungsverzeichnis (CSV)" über die gefilterten Haltungen.
- **6 Aggregator-Tests.**

---

## Commit 2 — `aee92d29` · Phase 3: Sanierungs-Matrix

- **`HoldingMeasureFactory`**: baut **headless** (ohne Kostenfenster) via `MeasureBlockVm` für eine
  Haltung + Hauptarbeit dasselbe Bündel wie das Einzelfenster — gleiche Auto-Mengen (DN, Länge,
  Anschluss-Dedup über `ConnectionCountEstimator`) und Regeln. **4 Tests beweisen: läuft headless.**
- **`SanierungsMatrixPageViewModel`** + Row-VM: alle Haltungen als Tabelle, pro Zeile eine
  Hauptarbeit-Auswahl, Live-Total, Speichern → `ByHolding` + `ApplyCosts` pro Haltung.
- **`SanierungsMatrixPage`** (DataGrid) als neue Nav-Seite; Andock additiv (App.xaml DataTemplate +
  ShellViewModel NavItem).

---

## Commit 3 — `edec26e4` · Matrix: Renovierung/Reparatur + Zusatzoptionen

- **Dropdown nach Kategorie sortiert + mit Präfix** „Renovierung · …" / „Reparatur · …"
  (Nadelfilz / GFK / Open-End bzw. Manschette / Kurzliner). *Hinweis: keine echte ComboBox-
  Gruppierung mit Überschriften — nur Sortierung + Präfix.*
- **5 ankreuzbare Zusatzoptionen** pro Haltung: Verkehrsdienst · Wasserhaltung · Fräsen ·
  Dichtheitsprüfung · Dokumentation (als deaktivierte Zeilen im Bündel, von der Factory aktiviert).
- **Reparatur-Menge manuell** eingebbar (Stk); Liner-Menge = Länge (gesperrt).
- `HoldingMeasureFactory` erweitert: `extraOptionKeys` + `hauptarbeitMenge`-Override. **2 neue Tests.**

---

## Commit 4 — `645b2a12` · Ein anwendbarer Preis-Katalog

- Es gab **zwei** Katalog-Systeme — der richtige ist `cost_catalog.json` (CostCatalogEditorDialog),
  das alte `PriceCatalogEditor` war verwirrend.
- **Matrix-Knopf „Preise / Katalog"** öffnet den richtigen Katalog und **wendet** die geänderten
  Preise nach dem Schliessen sofort auf alle Zeilen an (Totals neu).
- **Hauptmenü „Preiskatalog" umgeleitet** auf denselben Katalog → es gibt nur noch **einen**.
- Preis-Typen: **Fixed** (1 Preis: Reinigung/m, VD/Tag, Dichtheit/Stk) vs **ByDN** (Preis je DN:
  Liner, Manschetten). User-Override-Datei: `%AppData%\AuswertungPro\cost_catalog.user.json`.

---

## Commit 5 — `d8c06bde` · Haltungsansicht als Standard

- Die **Haltungen**-Seite öffnet jetzt direkt in der **Haltungsansicht** (Liste + Detail) statt der
  Tabelle; Umschalten auf die Tabelle bleibt möglich. (`DataPage`-Konstruktor setzt den Toggle aktiv.)

---

## Das Ablauf-Regelwerk (Grundlage der Automation)

`[I]` immer · `[A]` automatisch bei Bedingung · `[H]` ankreuzbar

| Position | NPK | Menge | Regel |
|---|---|---|---|
| Baustelleneinrichtung | 100 | 1 pausch | `[I]` |
| Vorreinigung (Spüler) | 211 | = Länge (m) | `[I]` |
| TV-Voraufnahme | 221 | = Länge (m) | `[I]` |
| Anschluss einmessen | 351 | = Anschlusszahl | `[A]` wenn Anschl. > 0 |
| Fräsen | 311 | = Länge (m) | `[H]` |
| Wasserhaltung | 411 | 1 | `[H]` |
| **Liner** (Nadelfilz/GFK/Open-End) | 612 | = Länge (m), Preis nach DN | Hauptarbeit |
| Anschluss auffräsen | 616 | = Anschlusszahl | `[A]` bei Liner + Anschl. |
| Anschluss verpressen (Epoxid) | 515 | schadhafte Anschl. | `[H]` |
| Endmanschette | 621 | 2 Stk | `[A]` **nur DN ≥ 200** |
| TV-Abnahme | 221 | = Länge (m) | `[I]` |
| Dichtheitsprüfung / Doku | — | 1 | `[H]` nur auf Verlangen |

**Anschlusszahl** = Dedup-Zählung (`ConnectionCountEstimator`): mehrere Codes am selben Ort = 1.
**Reparatur-Menge** (Manschette/Kurzliner) = manuell. **Eine** NPK-Liste pro Zone, nicht 50.

---

## Der fertige Workflow

1. **Sanierungs-Matrix** (Nav-Seite): pro Haltung Massnahme wählen (Renovierung/Reparatur), Optionen
   ankreuzen, Reparatur-Menge tippen → Total live → **Speichern**.
2. **„Preise / Katalog"**: Fixwerte eintragen (pro m / Stk / DN) → wird sofort angewendet.
3. **Druckcenter → „NPK-Leistungsverzeichnis (CSV)"**: eine aggregierte NPK-Liste über alle
   (gefilterten) Haltungen, gruppiert nach Kapitel.

---

## Status & offene Punkte

- **Build 0/0**, **41 Cost-Tests grün** (Logik: Aggregator, Factory, Dedup, Regeln getestet).
- **UI-Laufzeit nur build-verifiziert** (Bindings/ComboBox/Speichern/Checkboxen) — bitte in der App
  gegenprüfen.
- **Voraussetzung LV**: Haltungen brauchen gespeicherte Massnahmen (`costs.json`/ByHolding); Haltungen
  ohne Positionsdetails erscheinen als Pauschale unter „Übrige Positionen".
- **NPK-Lücken** bewusst leer: Verkehrsdienst, Dichtheitsprüfung, Haltung-Einmessen (keine saubere
  NPK-Position).
- Preise sind aktuell **Bürglen-Referenzwerte** als Default — mit deinen Fixwerten überschreiben.
