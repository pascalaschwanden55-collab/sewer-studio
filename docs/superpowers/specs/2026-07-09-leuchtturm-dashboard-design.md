# Leuchtturm-Dashboard (Projekt-Cockpit) — Design

> Brainstorming-Ergebnis 2026-07-09, nachgeschärft nach Entwickler-Review. Etappe 1 = Bildschirm-Dashboard. Etappe 2 (Druck/Dossier) bekommt einen eigenen Design-Durchgang; dieser Spec beschreibt sie nur als Ausblick, damit die Logik-Schicht sie nicht verbaut.

## Ziel

Die bestehende Übersichts-Seite (`OverviewPage`) wird zum **Leuchtturm** aufgewertet: ein Kachel-Dashboard, das den Projektzustand auf einen Blick zeigt — Haltungen **und** Schächte, Zustandsverteilung, Schäden, Kosten, Sanier-Fortschritt. Ein Screen, zwei Zustände: markiert man ein Projekt in der Liste, erscheint eine kompakte Vorschau; ist ein Projekt geladen, das volle Dashboard. Grafik-Kacheln sind **klickbar** und führen zur gefilterten Haltungsliste.

## Kontext & Abgrenzung

- **Ansatz:** Die vorhandene Übersicht wird **additiv** aufgewertet — kein zweiter „Übersicht"-Screen, keine Doppelspurigkeit. Links bleibt die Projektliste (neu: einklappbar), rechts wird das Dashboard.
- **Datenquelle ist das Programm selbst**, nie das XTF:
  - **Objektdaten** (Zustand, DN, Länge, Sanieren, Schadenscodes) aus `Project.Data` (Haltungen) und `Project.SchaechteData` (Schächte), inkl. der **manuell gepflegten Zustandsklassen**.
  - **Kosten** kommen NICHT aus dem `Kosten`-Record-Feld (nur ein synchronisiertes Anzeige-Feld, als Summe untauglich), sondern aus denselben Kosten-Stores wie Matrix/Druckcenter: `costs.json` (Haltungen) und `schacht_costs.json` (Schächte), geladen über `ProjectCostStoreRepository`, je Summe der pro-Objekt-Totals — genau wie `GesamtTotal = Rows.Sum(r => r.Total)` in den Matrix-Seiten. So zeigen Dashboard und Druckcenter garantiert dieselbe Summe.
  - Das XTF ist ausschliesslich Import/Export und fürs Dashboard irrelevant.
- **Fundament, das schon existiert:** `DashboardStatisticsBuilder`/`DashboardStatistics` (Application/Dashboard, heute nur Haltungen), `ProjectPreview`/`ProjectPreviewFactory` (Vorschau des markierten Projekts), `ZustandsklasseColorPalette` (Zustandsfarben 0–4), `FilterChipBar` (Filter-Chips der Haltungsliste), `ProjectCostStoreRepository` (costs.json / schacht_costs.json), `OverviewPageViewModel`/`OverviewPage.xaml`.
- **Charts als reines WPF** (Path/`ArcSegment`, `Rectangle`) — **keine neuen NuGet-Pakete** (Projektregel).

## Zustandsskala (verbindlich, 0–4 + ohne)

Gilt für Haltungen UND Schächte, Feld `Zustandsklasse`. **0 = schlechtester**, 4 = bester. Farben aus `ZustandsklasseColorPalette` (Excel-Vorlagen-Palette, konsistent mit Tabelle und Export).

| Klasse | Bezeichnung | Farbe (Palette) |
|--------|-------------|-----------------|
| Z0 | Nicht mehr funktionstüchtig | #FF0000 (rot) |
| Z1 | Starke Mängel | #FF6600 (orange) |
| Z2 | Mittlere Mängel | #FFFF00 (gelb, View darf leicht abdunkeln) |
| Z3 | Leichte Mängel | #AEB135 (oliv) |
| Z4 | Keine Mängel | #92D050 (grün) |
| — | **ohne Zustand** (noch nicht gesetzt) | grau |

„ohne Zustand" ist ein eigener Eimer (kein Wert 0–4 im Feld). Er macht den manuellen Pflege-Stand sichtbar und wird nie unterschlagen.

## Architektur (Etappe 1)

### ① Logik-Schicht — `Application/Dashboard`, rein & testbar

`DashboardStatisticsBuilder` wird erweitert, sodass er das **ganze Projekt** verarbeitet (Haltungen + Schächte). Reine Statistik-Logik, WPF-frei, voll unit-getestet.

Erweiterter Ergebnis-Record (Vorschlag, positional + benannte Argumente):

```text
DashboardStatistics(
  int HoldingCount, int SchachtCount,
  double TotalLengthMeters, decimal TotalCost,   // TotalCost = Haltungs- + Schacht-Store
  ZustandVerteilung Haltungen,      // Z0..Z4 + Ohne, je Count + Percent
  ZustandVerteilung Schaechte,      // dito
  IReadOnlyList<DashboardBucket> TopSchaeden,        // Top-Schadenscode-Gruppen (Haltungen)
  IReadOnlyList<DashboardCostBucket> HaltungDnCosts, // Kosten nach DN — NUR Haltungen
  int SanierenHaltungen, int HaltungenGesamt,        // Fortschritt Haltungen
  int SchaechteMitMassnahmen,                        // Schacht-"saniert" = Store-Eintrag > 0
  int DringendCount, int OhneZustandCount)           // Halt+Sch summiert
```

`ZustandVerteilung` = geordnete Liste `ZustandBucket(string Key /*"0".."4","ohne"*/, int Count, double Percent)`. Normalisierung von `Zustandsklasse` auf `"0".."4"` bzw. `"ohne"` liegt in der Application-Schicht (fachlich identisch zu `ZustandsklasseColorPalette.NormalizeClass`; falls sinnvoll, in einen geteilten Application-Helfer heben statt duplizieren).

Der **heutige** `DashboardStatistics` (nur Haltungen, Eimer `"0".."5"`/`"Unbekannt"`) wird auf diese Struktur **migriert**: Skala auf 0–4 korrigiert (kein „5"), `Unbekannt` → `ohne`, Schächte + Kosten-Stores ergänzt. Die einzige bestehende Nutzung (`OverviewPageViewModel.Dashboard`) wird mit angepasst.

**Kosten als Eingabe, nicht als I/O:** Der Builder liest **keine Dateien**. Die zwei Cost-Stores (`costs.json`, `schacht_costs.json`) werden in der UI-Schicht via `ProjectCostStoreRepository` geladen — genau wie Matrix/Druckcenter — und als Parameter übergeben (`Build(project, haltungCostStore, schachtCostStore)`). So bleibt die Statistik rein testbar, und die Summe ist per Konstruktion identisch zum Druckcenter. Wenn sich eine gemeinsame Aggregation anbietet, wird sie als geteilter Application-Helfer gebaut, den Dashboard und Druckcenter beide nutzen.

### ② Chart-Bausteine — `UI/Controls`, pure WPF, wiederverwendbar

- **`DonutChart`** — Ringsegmente aus einer Bucket-Liste (`ArcSegment`), Symbol im Zentrum (Rohr für Haltungen, Schacht-Schnitt für Schächte), Gesamtzahl als Unterschrift. Jedes Segment trägt seinen `Key` und feuert beim Klick einen Command mit diesem Key.
- **`CategoryBars`** — Balkenliste, waagrecht (Top-Schäden) und senkrecht (Haltungskosten nach DN). Rectangle-basiert, klickbare Einträge.

Beide sind reine Anzeige-Controls ohne Geschäftslogik; sie bekommen fertige Buckets + Farb-/Symbol-Zuordnung von aussen.

### ③ Screen — `OverviewPage` aufwerten

- `OverviewPage.xaml`: rechte Spalte → Kachel-Dashboard (Layout A). Linke Projektliste bleibt, wird **einklappbar** (Zustand in AppSettings gemerkt).
- `OverviewPageViewModel`: liefert die `DashboardStatistics` und steuert die **zwei Zustände** (Projekt offen → aus `_shell.Project`; kein Projekt offen, eins markiert → kompakte Vorschau aus dem geladenen Vorschau-`Project`, gebaut über denselben Builder) + die Klick-Commands.

### ④ Klick-Navigation — `DataPageStartFilter`

Der heutige Haltungsfilter ist stark in `DataPage.xaml.cs`/`FilterChipBar` verdrahtet und hat **keinen sauberen Startfilter-Einstieg**. Der Plan baut darum einen kleinen, testbaren **`DataPageStartFilter`** (Feld + Wert): Die Übersicht übergibt beim Navigieren einen Startfilter, die DataPage wendet ihn beim Laden über die bestehende Filter-Mechanik an. Die UI-verdrahtete Filterlogik selbst wird **nicht** umgebaut.

- Zustands-Segment „Z0" → Haltungen mit `Zustandsklasse=0`.
- Schadens-Balken „BAB" → Haltungen mit diesem Schadenscode.
- DN-Balken → Haltungen dieser Nennweite.
- **Schacht-Segmente sind in Etappe 1 reine Anzeige (nicht klickbar)** — ein Schacht-Startfilter kommt erst, wenn die Schacht-Seite einen echten Startfilter erhält (späterer Schritt).

### ⑤ Live-Aktualisierung

Damit „ändert sich sofort mit" wirklich stimmt, reicht `Dashboard => Build(...)` nicht — es feuert nicht bei Feld-Änderungen einzelner Records. Der Plan verdrahtet ein **debounced Refresh-Signal** (~300 ms): Neuberechnung beim Betreten der Übersicht, bei Projektwechsel, nach Kosten-Saves (Matrix/Druckcenter) und bei `CollectionChanged` von `Project.Data`/`Project.SchaechteData`. **Keine** teure Pro-Feld-Subscription pro Record — ein leichtes „Dashboard veraltet"-Flag mit Debounce genügt und hält auch grosse Projekte flüssig.

## Layout & Zustände

**Layout A** (vom Nutzer gewählt): Projektliste links (einklappbar), Dashboard rechts.

**Projekt offen — volles Dashboard:**
1. Kennzahlen-Zeile: **Haltungen · Schächte · Gesamtlänge · CHF Sanierung** (CHF = **Massnahmen-Total exkl. MwSt**: Haltungs-Total aus `costs.json` + Schacht-Total aus `schacht_costs.json`).
2. Zustand-Kachel (breit): zwei Donuts (Haltungen | Schächte) mit Symbol im Zentrum + gemeinsame, **tabellarisch ausgerichtete** Legende (Z0–Z4 + „ohne Zustand", je **Anzahl und %**, getrennt Halt/Sch).
3. Häufigste Schäden (waagrechte Balken) | **Haltungskosten nach DN** (senkrechte Balken; die DN-Aufschlüsselung ist naturgemäss nur Haltungen — die Kachel heisst darum ausdrücklich so, damit die Summe erklärbar bleibt).
4. Sanierungs-Fortschritt + Dringliches + „ohne Zustand"-Zähler.
   - **Fortschritt Haltungen:** `Sanieren_JaNein`=„Ja" (case-insensitiv, getrimmt) von **allen** Haltungen (nicht nur bewerteten) → „X von Y Haltungen zu sanieren".
   - **Schächte:** kein hartes Sanieren-Feld → „zu sanieren" = **Eintrag mit Total > 0 in `schacht_costs.json`**, separat als Zahl ausgewiesen (nicht in den Haltungs-Bruch gemischt).
   - **Dringend** = Z0 + Z1 (Halt + Sch summiert); **ohne Zustand** = Halt + Sch summiert.

**Kein Projekt offen — Vorschau des markierten Projekts:** dieselben Kacheln kompakt (Kennzahlen + Zustands-Donut) für das in der Liste markierte Projekt; beim Start ist das zuletzt genutzte Projekt vorausgewählt. „Öffnen" macht daraus das volle Dashboard.

Alle Grafik-Kacheln (ausser Schacht-Segmente) sind klickbar (Cursor/Hover-Hinweis „→ gefilterte Liste").

## Randfälle

- **Leeres Projekt (0 Haltungen/Schächte):** ruhiger „Noch keine Daten"-Zustand statt leerer Diagramme.
- **Keine Zustände gesetzt:** Donut komplett grau („ohne Zustand") — zeigt den Pflege-Bedarf, kein Fehler.
- **Kein Projekt offen und keins markiert:** dezenter Hinweis „Projekt wählen".
- **Kosten-Store fehlt/unlesbar:** Kosten-KPI zeigt „–" statt Absturz; das Dashboard bleibt sonst nutzbar.
- **Kosten/Länge fehlen teilweise:** fehlende Werte zählen als 0 (bestehende Parser sind kulanz-tolerant).

## Tests

- **`DashboardStatisticsBuilder`** (Kernlogik, Unit): Schacht-Einbezug; Z0–Z4-Verteilung + Prozente (Summe ≈ 100 %); „ohne Zustand"-Eimer; **Kosten aus übergebenen Stores** (Halt + Sch); Fortschritt (Ja von allen Haltungen) + Schacht-mit-Massnahmen; Dringend/ohne-Zustand über Halt+Sch; leeres Projekt; fehlender Store.
- **Zustands-Normalisierung** (0–4 + „ohne"): leer, Dezimal, ausserhalb 0–4.
- **`DataPageStartFilter`** (reine Logik): Segment-Key → Feld+Wert-Mapping; DataPage wendet Startfilter korrekt an.
- Bestehende Tests bleiben grün; XAML-Binding-Check für die neue rechte Spalte; beide Themes (hell/dunkel) sichtprüfen (Nutzer).

## Etappe 2 — Ausblick (NICHT Teil dieses Specs)

A4-Übersichtsseite in **QuestPDF** (scharfe Vektor-Seite), Einbau **ganz vorne ins Gesamtdossier** (vor den Detail-Haltungsseiten, via bestehendem `HaltungsDossierPdfBuilder`/`PdfMergeHelper`), plus ein **„Drucken/PDF"-Knopf** auf dem Screen. Nutzt den `DashboardStatistics`-Record aus Etappe 1 unverändert — die Charts werden für QuestPDF einmal nachgezeichnet. Eigener kurzer Design-Durchgang.

## Bewusst NICHT in Etappe 1

- Kein Aggregat über *alle* Projekte (Start-Zustand B) — verworfen zugunsten der Projekt-Vorschau (A).
- Keine Druck-/PDF-/Dossier-Funktion (Etappe 2).
- Keine klickbaren Schacht-Segmente (bis die Schacht-Seite einen Startfilter hat).
- Keine Änderung an Projektliste, Import, Kostenlogik oder Datenmodell über das Nötige hinaus.
- Keine neue Chart-Bibliothek.

## Offene Punkte / Entscheidungen

1. **Schacht-Segmente:** Etappe 1 reine Anzeige, nicht klickbar. Nur Haltungs-Segmente/-Balken navigieren. *(Entschieden.)*
2. **Gelb Z2:** Palette bleibt `#FFFF00`; View darf für Lesbarkeit leicht dunkler rendern. *(Entschieden.)*
3. **Vorschau:** Nur das markierte Projekt einzeln laden (wie `ProjectPreviewFactory`), nicht alle vorab; bei spürbarer Blockade grosser Projekte später async/cancelbar. *(Entschieden.)*
4. **Kostensumme-Bedeutung:** „CHF Sanierung" = **Massnahmen-Total exkl. MwSt** — Summe der pro-Objekt-Totals aus `costs.json` + `schacht_costs.json` (identisch zur Sanierungs-/Schacht-Matrix `GesamtTotal`). Ausdrücklich NICHT der NPK-Angebotsbetrag mit Einrichtung/Zuschlägen/MwSt. *(Entschieden.)*
