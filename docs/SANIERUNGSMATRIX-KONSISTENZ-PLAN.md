# Sanierungsmatrix & Massnahmen — Konsistenz-Fixplan (Audit 2026-07-04)

**Übergabefähig an Codex oder Opus. Self-contained; alle Fundstellen am 2026-07-04 im Code verifiziert (2 unabhängige Audit-Agenten, Branch `feature/gis-karte`).**

## Audit-Ergebnis in einem Satz

Das Rechen-Rückgrat ist konsistent — Matrix, Einzelfenster (Kostenrechner) und alle Ausdrucke teilen `CostCalculatorLogicService` (MwSt 8,1 % zentral aus `cost_catalog.json`, Rundung `AwayFromZero`), dieselben Engines (`HoldingMeasureFactory`, `MeasurePricingEngine`, `MeasureRuleService`) und dieselbe Speicherdatei `costs/costs.json`. **Die Inkonsistenzen sitzen an den Rändern**: ein toter Legacy-Template-Editor, Summen-Divergenz Matrix ↔ Ausdruck durch Pauschal-Kosten, still verworfene Hand-Preise, doppelte NPK-Nummern, doppelte Statistik-Klassifizierer und gemischte Währungsformate.

**Fachreferenz:** `D:\Fachwissen\Revision_NPK135.pdf` (NPK 135) und `D:\Fachwissen\Offerten` (echte Offerten) — für Paket K3 als Abgleichsquelle lesen.

**Wichtiger Kontext:** Einen `DevisGenerator`/`DevisExcelExporter` gibt es NICHT (toter Code-Verdacht in älteren Notizen). Die realen Ausgabewege sind: (1) Druckcenter-PDF „Kostenzusammenstellung" (`BuilderPageViewModel.ExportPdfAsync:163` → `cost_summary.sbnhtml` → Scriban+Playwright), (2) NPK-Leistungsverzeichnis-CSV (`ExportNpkLeistungsverzeichnis:268` → `ProjectPositionAggregator` + `NpkLeistungsverzeichnisExporter`), (3) Kostenrechner-Einzel-PDF, (4) Haltungsdossier (QuestPDF) mit optionaler Kostenschätzung. `offer.sbnhtml`/`offer_profi.sbnhtml` + `CostCalculationService.CalculateOffer/CalculateCombinedOffer` + `LegacyOfferTotalsCalculator` haben KEINEN Aufrufer.

## Regeln

- Kommentare deutsch, keine neuen NuGet-Pakete, bestehende Tests grün halten, jede Logik-Änderung mit fokussiertem Test.
- „Daten nie verlieren": Legacy-DATEIEN (`%APPDATA%\AuswertungPro\legacy_costs\*`) NICHT löschen — nur toten Code entfernen.
- Verhaltensänderungen an Beträgen/Summen immer mit Vorher/Nachher-Test belegen.

---

## Paket K1 — Toter Template-Editor + Legacy-Doppelsystem (fachlich dringend)

**Befund 1 (Mittel/fast Kritisch):** `ShellViewModel.cs:635-645` öffnet `MeasureTemplateEditorWindow` mit dem Legacy-`CostCalculationService` → speichert nach `legacy_costs\measure_templates.json`, das NIEMAND liest. Matrix/Kostenrechner lesen `MeasureTemplateStore` (`measure_templates.user.json`, `MeasureTemplateStore.cs:165-172`). **Wer Vorlagen bearbeitet, bearbeitet ins Leere.**
- Fix: Editor auf `MeasureTemplateStore` umstellen — exakt wie der bereits umgestellte Preis-Katalog-Editor (`ShellViewModel.cs:626-633` als Vorbild). Beim ersten Öffnen: falls `legacy_costs\measure_templates.json` neuer ist als die user.json → Hinweis-Dialog „Alte Vorlagen-Bearbeitungen gefunden — übernehmen?" (einmalige Migration, nicht still).

**Befund 2 (Mittel):** Komplettes Legacy-System ungenutzt: `CostCalculationService.CalculateOffer/CalculateCombinedOffer`, `LegacyOfferTotalsCalculator` (rundet Banker's statt AwayFromZero!), `OfferPdfModelFactory.Create`, `offer.sbnhtml`, `offer_profi.sbnhtml`.
- Fix: NACH dem Editor-Umbau toten Code + Templates entfernen (eigener Commit, leicht revertierbar). Tests des toten Codes (`LegacyOfferTotalsCalculatorTests`, Teile von `CostCalculationServiceTests`) mit entfernen.

## Paket K2 — Summen-Konsistenz Matrix ↔ Druckcenter ↔ NPK-LV

**Befund 3 (Mittel):** Pauschale Tabellen-Kosten (Feld `Kosten` ohne Matrix-Maßnahmen) zählen im Druckcenter-Netto und in der PDF-Summe (`BuilderPageViewModel.cs:609-617`, `BuilderPageSummaryEntryBuilder.cs:44-55` erzeugt synthetische „PAUSCHALE"-Position), aber NICHT im Matrix-Gesamt (`SanierungsMatrixPageViewModel.cs:643-648,757`). → Matrix-Summe < gedruckte Summe.
- Fix: Matrix-Kopf zeigt zusätzlich „+ Pauschalen aus Tabelle: X CHF (n Haltungen)" als separate, klar beschriftete Zeile (NICHT still ins Total mischen — Transparenz). Berechnung über denselben `BuilderPageSummaryEntryBuilder`-Fallback-Mechanismus, in testbaren Helper extrahieren.

**Befund 5-B (Mittel):** Dieselben „PAUSCHALE"-Einträge landen im NPK-Leistungsverzeichnis als Zeile ohne NPK-Nummer im Kapitel „Übrige Positionen" und fließen ins LV-Total (`ProjectPositionAggregator` nimmt jede Zeile mit Qty>0). Ein NPK-135-LV sollte nur echte Positionen führen.
- Fix: Export-Dialog-Option „Pauschalen ohne NPK-Position ausweisen" (Default AUS fürs LV); wenn aus → Pauschalen weglassen UND unter dem LV als Fußnote „Nicht enthaltene Pauschalkosten: X CHF" ausweisen, damit keine Beträge still verschwinden.

**Befund 10-A (Gering):** Matrix-Kopf `Total: … CHF` (`SanierungsMatrixPage.xaml:27`) ohne MwSt-Kennzeichnung → in „Total (exkl. MwSt): …" ändern.

**Neuer Guard-Test (wichtigster Test des Plans):** Integrationstest, der für dasselbe synthetische `costs.json` belegt: Matrix-Gesamt == Druckcenter-Netto == NPK-LV-Gesamttotal (Toleranz ±0.05 wegen dokumentierter Rundungsebenen) — inkl. Fall „Haltung nur mit Pauschale".

## Paket K3 — NPK-Katalog-Hygiene (mit Fachreferenz!)

**Befund 6-A (Mittel):** Doppelte NPK-Nummern im Seed-Katalog: `VORARBEIT_FRAESEN` und `HAUPTARBEIT_HINDERNISSE_ROBOTER` beide **311.110** (`cost_catalog.json:74,292` — einmal EH „m", einmal „h"!), `VORARBEIT_TV_VORKONTROLLE` und `QK_TV_ABNAHME` beide **221.110** (`:107,314`). Das LV druckt dieselbe NPK-Nummer zweimal mit verschiedener Einheit/EP — fachlich falsch.
- Fix in 3 Teilen: (a) **`D:\Fachwissen\Revision_NPK135.pdf` lesen** und die korrekten Positionsnummern für Fräsen/Roboter-Hindernisse/TV-Vorkontrolle/TV-Abnahme ermitteln; Seed-`cost_catalog.json` korrigieren (nur Nummern, keine Preise). Falls die PDF die Nummern nicht eindeutig hergibt → Nummern auf „" leeren und TODO-Kommentar, lieber keine als falsche. (b) Katalog-Validierung: beim Laden/Speichern des Katalogs Warnung, wenn dieselbe NPK-Nummer mit unterschiedlicher Einheit vorkommt (Toast/Dialog, kein Block). (c) `NpkLeistungsverzeichnisExporter`: bei Duplikat-Nummern Suffix-Kennzeichnung oder Warnhinweis im CSV-Kopf.
- Abgleich mit `D:\Fachwissen\Offerten` (echte Offerten als Struktur-Referenz): Kapitel-Reihenfolge, Positions-Schreibweise (xxx.xxx), Zwischentotale — kurz prüfen, ob unser LV-Aufbau dem entspricht; Abweichungen als Notiz in den PR, nicht eigenmächtig umbauen.

## Paket K4 — Stiller Datenverlust im Matrix-Detail

**Befund 4-A (Mittel):** Manuell überschriebene Einheitspreise im Detail-Panel (`IsPriceOverridden`, `SanierungsMatrixDetailEditLineVm.cs:127-135`) werden beim Zellen-Edit derselben Zeile (Häkchen/Menge in der Matrix) kommentarlos verworfen: `RecomputeRow` → `RefreshSelectedDetailIfNeeded` → `LoadDetailForRow` baut bedingungslos neu (`SanierungsMatrixPageViewModel.cs:847-880`); der Dirty-Guard greift nur beim Zeilenwechsel.
- Fix: vor dem Neuaufbau `IsDetailDirty` prüfen → Ja/Nein-Dialog „Ungespeicherte Detail-Änderungen an dieser Haltung — übernehmen und neu berechnen / verwerfen?" (Übernehmen = erst `ResolveDirtyDetail`-Speicherlogik, dann Recompute). Test: Override setzen → Matrix-Häkchen ändern → Override überlebt bzw. Dialog erscheint (Dialog-Service mocken).

## Paket K5 — Prüflogik an Engine angleichen

**Befund 5-A (Mittel):** `CostConsistencyChecker.ResolveCatalogPrice` (`CostConsistencyChecker.cs:347-361`) ignoriert Mengen-Staffeln (`QtyFrom/QtyTo`) und den Nächster-DN-Fallback — die Engine nutzt beides (`MeasurePricingEngine.cs:82-99`). Der Checker meldet falsche Abweichungen / schlägt falsche Fixpreise vor.
- Fix: Checker auf die ECHTE Preisauflösung umstellen — `MeasurePricingEngine`-Auflösung als gemeinsame, öffentliche Methode extrahieren und vom Checker aufrufen (eine Wahrheit statt drei: Engine, `CatalogPriceApplier.ResolveExactCatalogPrice`, Checker). Applier-Semantik (bewusst OHNE DN-Fallback, Audit K2) beibehalten und als benannten Modus derselben Methode ausdrücken. Test mit mengengestaffeltem ByDN-Preis (0-50 m=120, ab 50 m=90; Zeile 60 m): Checker darf bei korrektem Preis 90 KEINE Abweichung melden.

**Befund 7-A (Gering):** Eingefrorener Nächster-DN-Preis („Preis von DN 500 übernommen", `PriceHint`) wird bei „Preise/Katalog anwenden" nicht aktualisiert, wenn später echte DN-Preise gepflegt wurden (`CatalogPriceApplier.cs:80-98` überspringt).
- Fix: im Applier Zeilen MIT Fallback-`PriceHint` zusätzlich gegen exakte neue Katalogtreffer prüfen; bei Treffer Preis aktualisieren + Hint entfernen. Nur wenn kein manueller Override. Test dazu.

## Paket K6 — Ausdruck: eine Statistik, ein Währungsformat, ehrliche Aktualität

**Befund 2-B (Mittel):** Spezialstatistik doppelt implementiert und divergent: UI `BuilderPageSpecialCategoryResolver.cs:18-47` (Token „LEM" ohne Leerzeichen) vs. PDF `SpecialStatsClassifier.cs:82-84` (" LEM" mit Leerzeichen) — „…Element…" zählt in der UI als Linerendmanschette, im PDF nicht.
- Fix: EIN Klassifizierer (Infrastructure `SpecialStatsClassifier` als Wahrheit), UI-Resolver löschen bzw. delegieren; Token-Verhalten des PDF-Klassifizierers übernehmen (Wortgrenzen-Logik, kein blankes Substring-`lem`). Cross-Test: gleiche Positionsliste → UI-Statistik == PDF-Statistik, inkl. „Element"-Falle.

**Befund 4-B (Mittel):** Drei Währungsformate für denselben Betrag: `OfferPdfModelFactory.Money:138` de-CH „12'345.67 CHF" · `BuilderPageHoldingDataLineBuilder.cs:24` „12345.67 CHF" · `HaltungsDossierPdfBuilder.cs:766` invariant „12,345.67 CHF" (Komma = für CH irreführend) + Record-Rohtext `:685`.
- Fix: zentraler `ChfFormat.Money(decimal)` (Application\Common, de-CH, Apostroph-Tausender, 2 Dezimalstellen, „CHF" einheitlich hinten) — alle vier Stellen umstellen; Tests mit 12'345.67 und 1'234'567.89.

**Befund 1-B (Mittel):** Haltungsdossier druckt Kostenschätzung vom zuletzt GESPEICHERTEN Stand ohne Warnung (`DataPagePrintController.cs:358-366`), während das NPK-LV bei dirty warnt (`BuilderPageViewModel.cs:317-321`).
- Fix: identische Dirty-Warnung vor dem Dossier-Druck (gleicher Wortlaut).

**Befund 3-B (Mittel):** Alte MwSt-Sätze aus `costs.json` (z. B. 7,7 %) werden weitergedruckt; die PDF-Quote wird rückgerechnet (`OfferPdfModelFactory.cs:298-304,447`).
- Fix minimal-invasiv: Wenn gespeicherte Haltungs-Sätze vom aktuellen Katalogsatz abweichen → im PDF-Fuß Hinweiszeile „Enthält Positionen mit abweichendem MwSt-Satz (7.7 %)" + Toast im Druckcenter mit Angebot „Alle Haltungen mit aktuellem Satz neu berechnen?" (Ja → Recompute über die bestehende Engine + speichern). KEINE stille Neuberechnung.

**Befund 6-B (Mittel):** Dossier zeigt Record-Feld „Kosten (exkl. MWST)" UND Live-Detail-Total nebeneinander; der Kostenrechner-Pfad (`ApplyTotal`) stempelt das Record-Feld nicht zwingend.
- Fix: Beim Schreiben von `costs.json` IMMER auch das Record-Feld über `SanierungCostFieldMapper.ApplyCosts` stempeln (Kostenrechner-Pfad nachziehen); zusätzlich druckt das Dossier nur noch EINEN Netto-Wert (Detail-Total), das Record-Feld entfällt in der Sektion.

**Kleinkram (Gering, je 1 Zeile):** `cost_summary.sbnhtml`: `.group-row { page-break-inside: avoid; }` + `page-break-after: avoid` für Gruppenköpfe, `thead { display: table-header-group; }` (Befund 10-B). Positionen mit `UnitPrice == 0` im PDF sichtbar als „Preis fehlt" markieren statt „0.00 CHF" (Randfall-Befund).

## Paket K7 — Einheiten-Synonyme (Robustheit, Gering)

**Befund 9-A:** Mengenautomatik hängt an exakten Strings: Länge nur bei `"m"` (`CostCalculatorLogicService.IsMeterUnit`), Anschluss an Substring „ANSCHLUSS" (`:222-237`), manuelle Menge nur `"Stk"/"h"` (`MatrixMeasureOptionBuilder.cs:57-58`), Checker akzeptiert zusätzlich `"Std"` (`CostConsistencyChecker.cs:178-179`). Katalog-Umbenennung („Lfm", „Std") bricht die Automatik still.
- Fix: zentrale `UnitKinds`-Klasse (Application): `IsLength` („m", „lfm", „m1"), `IsHour` („h", „std"), `IsPiece` („stk", „st", „stück") — case-insensitive; alle vier Stellen umstellen; Theory-Tests je Synonym.

---

## Reihenfolge & Commits

| # | Paket | Aufwand | Nutzen |
|---|---|---|---|
| 1 | K1 Template-Editor umhängen (+ Legacy-Abbau als 2. Commit) | M | verhindert Arbeit ins Leere |
| 2 | K2 Summen-Konsistenz + Guard-Test | M | Matrix = Ausdruck |
| 3 | K3 NPK-Katalog (mit PDF-Referenz) | S–M | Offerten-Qualität |
| 4 | K4 Detail-Dirty-Guard | S | kein stiller Verlust |
| 5 | K5 Checker=Engine | M | Prüfung wird glaubwürdig |
| 6 | K6 Ausdruck-Fixes | M | konsistente Belege |
| 7 | K7 UnitKinds | S | Robustheit |

Ein Commit pro Befund (`fix(kosten):`/`fix(druck):`), Guard-/Cross-Tests im jeweiligen Commit.

## Verifikation

1. `dotnet build AuswertungPro.sln` — 0 Fehler; `dotnet test` — alle bestehenden + neuen Tests grün (bestehende Kosten-Tests: HoldingMeasureFactory, MeasurePricingEngine, CostCalculatorLogicService, CatalogPriceApplier, CostConsistencyChecker, ProjectPositionAggregator, OfferPdfModelFactory, BuilderPage*, SpecialStatsClassifier…).
2. Neue Pflicht-Tests: Cross-Summen-Guard (K2), Checker-Staffelpreis (K5), UI==PDF-Statistik (K6), ChfFormat (K6), UnitKinds (K7), NpkLeistungsverzeichnisExporter-Basistest (fehlt heute komplett!), Detail-Dirty-Guard (K4).
3. Manueller Smoke (User): Testprojekt mit (a) Haltung mit Maßnahmen, (b) Haltung nur mit Tabellen-Pauschale, (c) Haltung mit Hand-Preis-Override → Matrix-Kopf, Druckcenter-PDF, NPK-CSV und Dossier vergleichen: gleiche Netto-Summe bzw. ausgewiesene Pauschalen-Zeile; Vorlagen-Editor ändern → Matrix zeigt Änderung nach Neuaufbau; LV enthält keine doppelte NPK-Nummer.

## Bewusst NICHT in diesem Plan

- Kein Umbau der Rundungsarchitektur (Positions- vs. Haltungs-Ebene, Befund 8-A/7-B): dokumentierte Rappen-Differenzen, Aufwand/Nutzen schlecht — nur der Guard-Test mit Toleranz macht sie sichtbar.
- Keine 5-Rappen-Endbetrags-Rundung (wäre fachliche Änderung — nur auf User-Wunsch).
- Kein neuer Excel-Detailpositionen-Export (Befund 8-B) — Backlog-Kandidat, erst User fragen.
- Keine echte „Offerte/Devis"-Dokumentvorlage nach NPK 135 (Deckblatt, Firmenkopf, Konditionen) — das wäre ein eigenes Feature; heute ist der Ausdruck bewusst eine „Kostenzusammenstellung". Bei Interesse: eigener Plan mit `D:\Fachwissen\Offerten` als Vorlage.
