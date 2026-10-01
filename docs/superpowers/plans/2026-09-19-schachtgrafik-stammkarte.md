# Schachtgrafik Stammkarte — Umsetzungsplan

> **Für ausführende Agenten:** Plan wird inline mit TDD abgearbeitet (superpowers:executing-plans). Schritte mit `- [ ]`.

**Ziel:** Die Nova-Schachtansicht zeichnet einen massstäblichen Schnitt und einen Grundriss aus den vorhandenen Daten (Protokoll-PDF, Projekt-Haltungen, QGIS-Kopien, GeoShop-Objektakten), statt fester Zonen mit einer Symbolspalte.

**Architektur:** Ein WPF-freies `SchachtgrafikModell` (Application/Reports) wird von `SchachtgrafikModellBuilder` aus Datensatz, Haltungen, Katalog und einem optionalen `SchachtgrafikZusatz` (Lage aus QGIS-Kopie, Koten aus Objektakten) gebaut. `SchachtgrafikSvgBuilder` zeichnet daraus Schnitt + Grundriss in der bestehenden SVG-Teilmenge. Der Import liefert die Grundlage: Häkchen-Lesung im Kästchenformular, Anschlusstabelle als additive Struktur `SchachtRecord.Anschluesse`.

**Spec:** Vorschlag `https://claude.ai/artifact/RxGFLtq6ZWHgUDebu1QVsa` (Variante A, Entscheide: Auslauf oben + Nordpfeil, Anschlusstabelle importieren, GeoShop-Koten zeigen, Konus/Steigeisen schematisch beschriftet, Schnitt und Grundriss untereinander).

## Globale Randbedingungen

- Keine erfundenen Werte: fehlende Tiefe → kein Massstab (Hinweis), fehlende Richtung → «Richtung nicht erfasst», Konus/Steigeisenseite als «schematisch» beschriftet.
- SVG nur aus der Teilmenge von `SvgTeilmengeZeichner` (Pfade M/L/C/Q/Z; kein `stroke-dasharray` an circle/ellipse/rect; `g` nur mit `transform`), Farben nur aus `SvgFarbZuordnung`.
- Smoke-Regeln: Schrift ≥ 9 px nach Skalierung (Spalte 300 px, SVG-Breite 320 → SVG-Schrift ≥ 10), `line` in Muted/Accent-Farbe ≥ 3,2 Einheiten dick, grösstes Rechteck ≥ ⅓ Breite.
- `SymbolAnzahl` = Anzahl Hinweisflächen; ohne Anschlüsse und Schäden 0, je Schadenseintrag genau eine Marke (Tests `NovaGrafikWiederladenTests`, `SchaechteNovaLayoutIsolatedSmokeTests`).
- Neue Dienste: Vertrag in Application, Implementierung in Infrastructure, Registrierung in `ServiceProvider` + `ServiceProviderRegistrationMap` (Zähler 167 → 168).
- Laufendes SewerStudio sperrt `bin\Debug`: Testprojekte mit `-o .tmp/testout-schacht-*` bauen und die DLL direkt testen.
- Deutsch in Kommentaren, UI-Texten und Commits.

---

## Dateistruktur

| Datei | Verantwortung |
|---|---|
| `Domain/Models/SchachtAnschluss.cs` (neu) | Ein Anschluss: Nr, Art, DN, Tiefe, Material, Uhr, Richtung, Haltungsname, Quelle |
| `Domain/Models/SchachtRecord.cs` | additive Liste `Anschluesse` + `SetzeAnschluesse` (meldet) |
| `Application/Import/SchachtProtocolZusatz.cs` (neu) | Ergebnis der Zusatzlesung (Anschlüsse, Medium, Materialien, Deckel-Ø, Steighilfe, Tauchbogen) |
| `Infrastructure/Import/Pdf/SchachtProtocolKaestchenformular.cs` (neu) | Erkennung Kästchenformular, Markenbindung «Marke → folgendes Wort», Zeilen Verkalkung/Fremdwasser |
| `Infrastructure/Import/Pdf/SchachtProtocolZusatzParser.cs` (neu) | Anschlusstabelle, Kopfdaten (Medium, Material, Deckel DN), Steighilfe/Tauchbogen |
| `Infrastructure/Import/Pdf/SchachtProtocolParser.cs` | Abschnittsende «Anschlüsse» (Plural), Kästchenweg einhängen |
| `Infrastructure/Import/Pdf/SchachtProtocolApplier.cs` | `ApplyZusatz` |
| `Infrastructure/Import/Pdf/LegacyPdfImportService.cs`, `Import/Protocols/SchachtProtocolImportService.cs` | Zusatz lesen und anwenden; `SchachtProtocolParseResult.Zusatz` |
| `Infrastructure/Import/SchachtPro/SchachtProProtocolMapper.cs`, `SchachtProImportService.cs` | Anschlüsse strukturiert übernehmen |
| `Application/Lookup/ISchachtLageQuelle.cs` (neu) | Vertrag: Schachtpunkt + Azimut je Haltung |
| `Application/Xtf/GpkgGeometrie.cs` | `Punkt(blob)` |
| `Infrastructure/Lookup/QgisGpkgSchachtLageLeser.cs` (neu) | gezielte Abfrage Schachtpunkt und Leitungslinien aus den QGIS-Kopien |
| `Application/Reports/SchachtAnschlussRichtung.cs` (neu) | reine Azimut-Rechnung |
| `Application/Reports/SchachtKotenQuelle.cs` (neu) | Deckel-/Sohlen-/Anschlusskoten aus Objektakten |
| `Application/Reports/SchachtgrafikModell.cs` (neu) | Modell-Records, `SchachtBauteil` |
| `Application/Reports/SchachtgrafikModellBuilder.cs` (neu) | Regeln: Tiefe, Anschlüsse zusammenführen, Richtungen, Koten, Schäden mit Ort, Bemerkung «Einlauf 3 …» |
| `Application/Reports/SchachtgrafikSvgBuilder.cs` (neu geschrieben) | Schnitt + Grundriss |
| `Application/Reports/SchachtgrafikAnsichtBuilder.cs` | Zusammenbau, Überladung mit Zusatz, Legende |
| `Application/Reports/SchachtSchadenOrtRegel.cs` | `BestimmeBauteil`, `AnschlussNr` |
| `UI/ServiceProvider.QgisBestand.cs`, `ServiceProviderRegistrationMap.cs` | `SchachtLage` |
| `UI/ViewModels/Pages/SchaechtePageViewModel.cs` | `SchachtLage` durchreichen |
| `UI/DataPage/SchaechteNovaWorkspaceController.cs` | Zusatz im Hintergrund laden, an Panel reichen |
| `UI/Views/Pages/Schachtansicht/SchachtUebersichtPanel.xaml(.cs)`, `SchachtgrafikControl.xaml(.cs)` | DP `Zusatz`, Höhe nach Seitenverhältnis, Legende |
| Tests | `SchachtProtocolKaestchenformularTests`, `SchachtgrafikModellBuilderTests`, `SchachtAnschlussRichtungTests`, `SchachtKotenQuelleTests`, `GpkgGeometriePunktTests`, `QgisGpkgSchachtLageLeserTests`; angepasst: `SchachtgrafikSvgBuilderTests`, `SchachtgrafikAnsichtBuilderTests`, `SchachtgrafikSvgBuilderTeilmengeTests`, `SchachtgrafikControlIsolatedSmokeTests`, `ServiceProviderRegistrationTests` |

Vorlage: `tests/Fixtures/Schachtprotokolle/80409_seite1_layout.txt` (pdftotext -layout, Visum anonymisiert).

---

### Task 1: Import — Häkchen zählen, Anschlusstabelle lesen

**Files:** Domain `SchachtAnschluss.cs`, `SchachtRecord.cs`; Application `Import/SchachtProtocolZusatz.cs`; Infrastructure `SchachtProtocolKaestchenformular.cs`, `SchachtProtocolZusatzParser.cs`, `SchachtProtocolParser.cs`, `SchachtProtocolApplier.cs`, `LegacyPdfImportService.cs`, `SchachtProtocolImportService.cs`, `ISchachtProtocolImportService.cs` (Zusatz-Eigenschaft), SchachtPro-Mapper/-Service.
**Test:** `tests/AuswertungPro.Next.Infrastructure.Tests/SchachtProtocolKaestchenformularTests.cs`.

Interfaces (Produces):
- `SchachtProtocolKaestchenformular.IstKaestchenformular(string normalisierterText) : bool` — Kopfzeile «Zustand der Bauteile» trägt «Mängelfrei» ODER Text enthält «Aus/Ein» und «Tiefe m».
- `SchachtProtocolKaestchenformular.MarkierteSchaeden(string component, string tail) : IReadOnlyList<(string Component, string Damage)>` — Marke bindet an das FOLGENDE Wort; Zeilen «Verkalkung»/«Fremdwasser» liefern (markiertes Bauteil, Zeilenname); «Gerinne» → «Durchlaufrinne».
- `SchachtProtocolZusatzParser.Parse(string text) : SchachtProtocolZusatz`.
- `SchachtProtocolApplier.ApplyZusatz(SchachtRecord target, SchachtProtocolZusatz zusatz, bool rebuildFromProtocol, bool onlyMissing)` — Felder `Medium`, `Material`, `Deckelmaterial`, `Deckeldurchmesser` (mm), `Steighilfe`, `Tauchbogen`; `target.SetzeAnschluesse(...)`.

- [ ] Test rot: Vorlage 80409 → genau 4 Schäden (Konus: Verkalkung, Bankett: ausgebrochen, Bankett: Ablagerungen, Durchlaufrinne: Ablagerungen); Marke bindet nach vorn; Zusatz liefert 4 Anschlüsse, Medium, Materialien, Deckel 660, Steighilfe «vorhanden».
- [ ] Umsetzung; bestehende `SchachtProtocolParserTests` und `SchachtPdfImportMappingTests` bleiben grün.
- [ ] Commit «Schachtimport: Häkchen zählen, Anschlusstabelle lesen».

### Task 2: Geometrie — Azimut, GeoPackage-Punkt, Lagequelle

**Files:** `Application/Xtf/GpkgGeometrie.cs`, `Application/Lookup/ISchachtLageQuelle.cs`, `Application/Reports/SchachtAnschlussRichtung.cs`, `Infrastructure/Lookup/QgisGpkgSchachtLageLeser.cs`.
**Tests:** `GpkgGeometriePunktTests`, `SchachtAnschlussRichtungTests` (Pipeline.Tests), `QgisGpkgSchachtLageLeserTests` (Infrastructure.Tests, eigene kleine GeoPackage-Datei mit Microsoft.Data.Sqlite).

Interfaces:
- `GpkgGeometrie.Punkt(byte[]? blob) : XtfPunkt?` (WKB Point = 1, auch Z/M).
- `SchachtAnschlussRichtung.Azimut(XtfPunkt schacht, IReadOnlyList<XtfPunkt> verlauf, double toleranzM = 1.0) : double?` — Ende näher am Schacht wählen, Richtung zum nächsten Stützpunkt, 0° = Nord, im Uhrzeigersinn; Ende weiter als Toleranz → null.
- `record SchachtLage(XtfPunkt Schachtpunkt, IReadOnlyDictionary<string,double> AzimutJeHaltung)`; `interface ISchachtLageQuelle { SchachtLage? Lies(string schachtnummer, IReadOnlyCollection<string> haltungsnamen); }` — mehrdeutige Namen liefern nichts; fehlende Datei → null.

- [ ] Tests rot, Umsetzung, grün, Commit «Schachtgrafik: Lage aus der QGIS-Kopie».

### Task 3: Koten aus Objektakten

**Files:** `Application/Reports/SchachtKotenQuelle.cs`. **Test:** `SchachtKotenQuelleTests`.
- `record SchachtKoten(decimal? Deckel, decimal? Sohle, IReadOnlyDictionary<string,decimal> JeHaltung)`; `SchachtKotenQuelle.Lies(Project projekt, SchachtRecord schacht, IReadOnlyList<HaltungRecord> haltungen) : SchachtKoten?` — Deckel über `SchachtDeckelAnzeige.Waehle` (`deckel.hoehe`), Sohle `schacht.sohlenhoehe`, je Haltung `haltung.fromlevel` (Schacht ist oben) / `haltung.tolevel` (Schacht ist unten); Zahlen über `FachzahlParser`.
- [ ] Test rot, Umsetzung, grün, Commit.

### Task 4: Modell und Modellbauer

**Files:** `SchachtgrafikModell.cs`, `SchachtgrafikModellBuilder.cs`, `SchachtSchadenOrtRegel.cs` (+`BestimmeBauteil`, `AnschlussNr`), `SchachtSchadenKategorieRegel.cs` (`KategorieAusText` öffentlich).
**Test:** `SchachtgrafikModellBuilderTests` (80409-Daten, Fälle ohne Tiefe/Masse/Haltungen).

Regeln:
- Tiefe: Feld `Schachttiefe`; sonst Koten Deckel − Sohle (Quelle «Kataster»).
- Anschlüsse: `record.Anschluesse` zuerst; Haltungen der Seite über `Schacht_oben/unten`, ersatzweise Namensmuster `<schacht>-<x>` (Auslauf) / `<x>-<schacht>` (Einlauf); Zuordnung Anschluss ↔ Haltung über Art + DN (eindeutig), sonst bleibt der Anschluss ohne Haltung bzw. die Haltung ohne Tabellenzeile.
- Richtung je Haltung aus `Zusatz.Lage`; Anschluss ohne Haltung → keine Richtung.
- Kote je Anschluss aus `Zusatz.Koten.JeHaltung`; Tiefe des Anschlusses: Tabellenwert, sonst Deckelkote − Kote.
- Schäden: je Eintrag ein `SchachtgrafikSchaden(Nr, Bauteil, AnschlussNr?, Kategorie, Farbe, Tooltip)`; Bemerkungen-Zeile `^(Einlauf|Auslauf|Anschluss)\s*(\d+)\s*[:\-]?\s*(.+)$` erzeugt zusätzlich einen Schaden am Anschluss, wenn die Nr existiert.
- Steighilfe: Feld `Steighilfe` (nicht leer, nicht «fehlt»/«nicht notwendig») → `SteigeisenVorhanden = true`.
- [ ] Tests rot, Umsetzung, grün, Commit.

### Task 5: SVG-Bauer Schnitt + Grundriss

**Files:** `SchachtgrafikSvgBuilder.cs` (neu geschrieben), `SchachtgrafikAnsichtBuilder.cs` (Legende, Zusatz-Überladung).
**Tests:** `SchachtgrafikSvgBuilderTests`, `SchachtgrafikAnsichtBuilderTests` (neu geschrieben), `SchachtgrafikSvgBuilderTeilmengeTests` (angepasst: Beispiel über Modell).

Massstab: `s = clamp(420 / TiefeM, 100, 130)` Einheiten je Meter; Breite 320; Schnitthöhe `70 + Tiefe·s + 90`; Grundriss 320 × 330 darunter; ohne Tiefe: Schnitt mit fester Höhe und Hinweis «Tiefe nicht erfasst».
- [ ] Tests rot, Umsetzung, grün (inkl. Teilmengen-Wächter), Commit.

### Task 6: Einbau in Schachtansicht

**Files:** ServiceProvider (+Map, Zähler 168), `SchaechtePageViewModel.SchachtLage`, `SchaechteNovaWorkspaceController` (asynchrones Laden mit Generationszähler), `SchachtUebersichtPanel.xaml(.cs)` (DP `Zusatz`, ohne feste Höhe, Legende), `SchachtgrafikControl.xaml(.cs)`.
**Tests:** `ServiceProviderRegistrationTests` (168), `SchachtgrafikControlIsolatedSmokeTests` (Höhe frei), bestehende Nova-Smoke-Tests.
- [ ] Umsetzung, Tests grün, Commit.

### Task 7: Doku und Abnahme

- [ ] CLAUDE.md-Abschnitt «Schachtgrafik Stammkarte (19.09.2026)» mit den nicht zurückdrehbaren Regeln.
- [ ] Gesamtlauf der betroffenen Testfilter; Sichtprobe durch Pascal im Programm (nach Schliessen und Neubau von `bin\Debug`).
