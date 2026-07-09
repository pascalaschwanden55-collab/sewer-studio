# Leuchtturm-Dashboard (Projekt-Cockpit) — Design

> Brainstorming-Ergebnis 2026-07-09. Etappe 1 = Bildschirm-Dashboard. Etappe 2 (Druck/Dossier) bekommt einen eigenen Design-Durchgang; dieser Spec beschreibt sie nur als Ausblick, damit die Logik-Schicht sie nicht verbaut.

## Ziel

Die bestehende Übersichts-Seite (`OverviewPage`) wird zum **Leuchtturm** aufgewertet: ein Kachel-Dashboard, das den Projektzustand auf einen Blick zeigt — Haltungen **und** Schächte, Zustandsverteilung, Schäden, Kosten, Sanier-Fortschritt. Ein Screen, zwei Zustände: markiert man ein Projekt in der Liste, erscheint eine kompakte Vorschau; ist ein Projekt geladen, das volle Dashboard. Grafik-Kacheln sind **klickbar** und führen zur gefilterten Haltungsliste.

## Kontext & Abgrenzung

- **Ansatz:** Die vorhandene Übersicht wird **additiv** aufgewertet — kein zweiter „Übersicht"-Screen, keine Doppelspurigkeit. Links bleibt die Projektliste (neu: einklappbar), rechts wird das Dashboard.
- **Datenquelle ist das Programm selbst**, nie das XTF: Haltungen aus `Project.Data`, Schächte aus `Project.SchaechteData`, jeweils inkl. der **manuell gepflegten Zustandsklassen**. Ändert der Nutzer einen Zustand im Programm, ändert sich das Dashboard sofort mit. Das XTF ist ausschliesslich Import/Export und für das Dashboard irrelevant.
- **Fundament, das schon existiert:** `DashboardStatisticsBuilder`/`DashboardStatistics` (Application/Dashboard, heute nur Haltungen), `ProjectPreview`/`ProjectPreviewFactory` (Vorschau des markierten Projekts), `ZustandsklasseColorPalette` (Zustandsfarben 0–4), `FilterChipBar` (Filter-Chips der Haltungsliste), `OverviewPageViewModel`/`OverviewPage.xaml`.
- **Charts als reines WPF** (Path/`ArcSegment`, `Rectangle`) — **keine neuen NuGet-Pakete** (Projektregel).

## Zustandsskala (verbindlich, 0–4 + ohne)

Gilt für Haltungen UND Schächte, Feld `Zustandsklasse`. **0 = schlechtester**, 4 = bester. Farben aus `ZustandsklasseColorPalette` (Excel-Vorlagen-Palette, konsistent mit Tabelle und Export). Reines Gelb (Z2) darf in der View für Lesbarkeit leicht abgedunkelt werden.

| Klasse | Bezeichnung | Farbe (Palette) |
|---|---|---|
| Z0 | Nicht mehr funktionstüchtig | #FF0000 (rot) |
| Z1 | Starke Mängel | #FF6600 (orange) |
| Z2 | Mittlere Mängel | #FFFF00 (gelb) |
| Z3 | Leichte Mängel | #AEB135 (oliv) |
| Z4 | Keine Mängel | #92D050 (grün) |
| — | **ohne Zustand** (noch nicht gesetzt) | grau |

„ohne Zustand" ist ein eigener Eimer (kein Wert 0–4 im Feld). Er macht den manuellen Pflege-Stand sichtbar und wird nie unterschlagen.

## Architektur (Etappe 1)

### ① Logik-Schicht — `Application/Dashboard`, rein & testbar

`DashboardStatisticsBuilder` wird erweitert, sodass er das **ganze Projekt** verarbeitet (Haltungen + Schächte) statt nur einer Haltungsliste. Die reine Statistik-Logik bleibt WPF-frei und voll unit-getestet.

Erweiterter Ergebnis-Record (Vorschlag, positional + benannte Argumente):

```text
DashboardStatistics(
  int HoldingCount, int SchachtCount,
  double TotalLengthMeters, decimal TotalCost,
  ZustandVerteilung Haltungen,      // Z0..Z4 + Ohne, je Count + Percent
  ZustandVerteilung Schaechte,      // dito
  IReadOnlyList<DashboardBucket> TopSchaeden,   // Top-Schadenscode-Gruppen (Haltungen)
  IReadOnlyList<DashboardCostBucket> DnCostGroups,
  int SanierenJa, int SanierenGesamt,           // Fortschritt
  int DringendCount, int OhneZustandCount)       // Halt+Sch summiert
```

`ZustandVerteilung` = geordnete Liste `ZustandBucket(string Key /*"0".."4","ohne"*/, int Count, double Percent)`. Normalisierung von `Zustandsklasse` auf `"0".."4"` bzw. `"ohne"` liegt in der Application-Schicht (fachlich identisch zu `ZustandsklasseColorPalette.NormalizeClass`; falls sinnvoll, die Normalisierung in einen geteilten Application-Helfer heben statt duplizieren).

Der **heutige** `DashboardStatistics` (nur Haltungen, Eimer `"0".."5"`/`"Unbekannt"`) wird auf diese Struktur **migriert**: Skala auf 0–4 korrigiert (kein „5"), `Unbekannt` → `ohne`, Schächte ergänzt. Die einzige bestehende Nutzung (`OverviewPageViewModel.Dashboard`) wird mit angepasst.

**Wichtig für Etappe 2:** Genau dieser Record ist die einzige Wahrheit — Bildschirm und späteres Druck-PDF lesen beide daraus.

### ② Chart-Bausteine — `UI/Controls`, pure WPF, wiederverwendbar

- **`DonutChart`** — zeichnet Ringsegmente aus einer Bucket-Liste (`ArcSegment`), Symbol im Zentrum (Rohr für Haltungen, Schacht-Schnitt für Schächte), optional Gesamtzahl als Unterschrift. Jedes Segment trägt seinen `Key` und feuert beim Klick einen Command mit diesem Key.
- **`CategoryBars`** — Balkenliste, Modus waagrecht (Top-Schäden: Label + Balken + Zahl) und senkrecht (Kosten nach DN). Rectangle-basiert, ebenfalls klickbare Einträge.

Beide sind reine Anzeige-Controls ohne Geschäftslogik; sie bekommen fertige Buckets + Farb-/Symbol-Zuordnung von aussen.

### ③ Screen — `OverviewPage` aufwerten

- `OverviewPage.xaml`: Die **rechte Spalte** wird das Kachel-Dashboard (Kennzahlen-Zeile + Grafik-Kacheln in Layout A). Die **linke Projektliste bleibt**, wird aber **einklappbar** (schmale Leiste ↔ volle Liste; Zustand in AppSettings gemerkt).
- `OverviewPageViewModel`: liefert die `DashboardStatistics` und steuert die **zwei Zustände**:
  - *Projekt offen* → Dashboard aus `_shell.Project` (live).
  - *kein Projekt offen, aber eins markiert* → kompakte Vorschau aus dem geladenen Vorschau-Projekt (baut auf `ProjectPreview`/`ProjectPreviewFactory` auf; die Vorschau-Statistik wird über denselben `DashboardStatisticsBuilder` aus dem geladenen Vorschau-`Project` gebaut).
  - Klick-Commands (Segment/Balken → Navigation, siehe ④).

### ④ Klick-Navigation

Klick auf ein Donut-Segment oder einen Balken navigiert zur Haltungsliste (`DataPage`) und setzt dort den passenden Filter über die bestehende Filter-Mechanik (`FilterChipBar`/DataPage-Filter):

- Zustands-Segment „Z0" → Haltungen mit `Zustandsklasse=0`.
- Schadens-Balken „BAB" → Haltungen mit diesem Schadenscode.
- DN-Balken → Haltungen dieser Nennweite.

Die Verdrahtung läuft über `ShellViewModel` (Navigation zur DataPage) plus Übergabe eines Start-Filters. Die genaue API (bestehender Filter-Setter vs. kleiner neuer Eintrittspunkt) klärt der Implementierungsplan; Schacht-Segmente führen analog zur Schacht-Ansicht, falls die Zielansicht das unterstützt — sonst bleiben Schacht-Segmente in Etappe 1 nicht-klickbar (siehe Offene Punkte).

## Layout & Zustände

**Layout A** (vom Nutzer gewählt): Projektliste links (einklappbar), Dashboard rechts.

**Projekt offen — volles Dashboard:**
1. Kennzahlen-Zeile: **Haltungen · Schächte · Gesamtlänge · CHF Sanierung**.
2. Zustand-Kachel (breit): zwei Donuts (Haltungen | Schächte) mit Symbol im Zentrum + gemeinsame, **tabellarisch ausgerichtete** Legende (Z0–Z4 + „ohne Zustand", je **Anzahl und %**, getrennt Halt/Sch).
3. Häufigste Schäden (waagrechte Balken) | Kosten nach DN (senkrechte Balken).
4. Sanierungs-Fortschritt (Balken „x von y sanieren") + Dringliches (Z0–1, Halt+Sch) + „ohne Zustand"-Zähler.

**Kein Projekt offen — Vorschau des markierten Projekts:** dieselben Kacheln kompakt (Kennzahlen + Zustands-Donut) für das in der Liste markierte Projekt; beim Start ist das zuletzt genutzte Projekt vorausgewählt. „Öffnen" macht daraus das volle Dashboard.

Alle Grafik-Kacheln sind klickbar (Cursor/Hover-Hinweis „→ gefilterte Liste").

## Randfälle

- **Leeres Projekt (0 Haltungen/Schächte):** ruhiger „Noch keine Daten"-Zustand statt leerer Diagramme.
- **Keine Zustände gesetzt:** Donut komplett grau („ohne Zustand") — zeigt den Pflege-Bedarf, kein Fehler.
- **Kein Projekt offen und keins markiert:** dezenter Hinweis „Projekt wählen".
- **Kosten/Länge fehlen teilweise:** fehlende Werte zählen als 0, kein Absturz (bestehende Parser in `DashboardStatisticsBuilder` sind bereits kulanz-tolerant).

## Tests

- **`DashboardStatisticsBuilder`** (Kernlogik, Unit): Schacht-Einbezug; Z0–Z4-Verteilung + Prozente (Summe ≈ 100 %); „ohne Zustand"-Eimer; leeres Projekt; gemischte/teilweise leere Felder; Dringend-/Fortschritt-Zählung über Halt+Sch.
- **Zustands-Normalisierung** (0–4 + „ohne"): Grenzfälle (leer, Dezimal, ausserhalb 0–4).
- **Klick→Filter-Mapping** (reine Logik): Segment-Key → Filterwert.
- Bestehende Tests bleiben grün; XAML-Binding-Check für die neue rechte Spalte; beide Themes (hell/dunkel) sichtprüfen (Nutzer).

## Etappe 2 — Ausblick (NICHT Teil dieses Specs)

A4-Übersichtsseite in **QuestPDF** (scharfe Vektor-Seite), Einbau **ganz vorne ins Gesamtdossier** (vor den Detail-Haltungsseiten, via bestehendem `HaltungsDossierPdfBuilder`/`PdfMergeHelper`), plus ein **„Drucken/PDF"-Knopf** auf dem Screen, der genau diese Seite einzeln ausgibt. Nutzt den `DashboardStatistics`-Record aus Etappe 1 unverändert — die Charts werden für QuestPDF einmal nachgezeichnet. Bekommt einen eigenen kurzen Design-Durchgang.

## Bewusst NICHT in Etappe 1

- Kein Aggregat über *alle* Projekte (Start-Zustand B) — verworfen zugunsten der Projekt-Vorschau (A).
- Keine Druck-/PDF-/Dossier-Funktion (das ist Etappe 2).
- Keine Änderung an Projektliste, Import oder Datenmodell über das Nötige hinaus.
- Keine neue Chart-Bibliothek.

## Offene Punkte / Annahmen

1. **Schacht-Klick-Ziel:** Ob Schacht-Segmente zur Schacht-Ansicht mit Filter springen, hängt davon ab, ob diese Ansicht einen Start-Filter annimmt. Falls nicht trivial: in Etappe 1 sind Schacht-Segmente reine Anzeige (Haltungs-Segmente klickbar). Im Plan verifizieren.
2. **Gelb-Ton Z2:** #FFFF00 ist auf hellem Grund schwach lesbar; die View darf einen minimal abgedunkelten Gelb-Ton verwenden, ohne die Palette selbst zu ändern.
3. **Vorschau-Kosten für alle Listenprojekte:** Die Vorschau lädt das markierte Projekt einzeln (wie heute `ProjectPreviewFactory`); es wird NICHT die ganze Projektliste vorab eingelesen (Performance).
