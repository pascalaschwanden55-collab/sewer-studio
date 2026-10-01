# SewerStudio — AI Sewer Inspection System

## So ist diese Anleitung aufgebaut (seit 30.09.2026)

Diese Datei enthält nur, was bei **jeder** Arbeit gilt: Projekt, Architekturprinzipien,
Querschnittsregeln, Build und Fachbegriffe. Die Fachregeln der einzelnen Bereiche stehen in
`docs/architektur/`. Bis zum 30.09.2026 stand alles hier; die Bereichsdateien enthalten
diesen Text unverändert, also neben geltenden Regeln auch datierte Arbeitsstände.

- **Vor Arbeit in einem Bereich** die passende Datei unten lesen. Sie sind lang: zuerst das
  Inhaltsverzeichnis oben, dann gezielt nach Klassen- oder Stichwort suchen.
- **Neue Erkenntnisse** gehören in die Bereichsdatei, nicht hierher. Hier nur ergänzen, was
  für jede Arbeit im ganzen Projekt gilt.
- **Keine veränderlichen Zahlen** (Anzahl Dienste, Tests, Klassen) hier eintragen. Die Zahl
  steht im Test, der sie festhält, zum Beispiel `ServiceProviderRegistrationTests`.
- **Widersprüche:** Es gilt der jüngere Abschnitt und im Zweifel der Code samt Test.
- Messwerte und Pläne zur Wartbarkeit: `docs/audits/2026-09-30-wartbarkeit/`.

| Datei unter `docs/architektur/` | Inhalt |
| --- | --- |
| `oberflaeche.md` | Nova-Design, Theme/Tokens, Fenster- und Knopfregel, Dialoge, Toasts, Hilfe/Handbuch, Strg+K, Rückgängig, Leerzustände, Hochkontrast, Tabellen, Aufklapplisten, Startanimation |
| `import-und-verteilung.md` | Ein-Knopf- und manueller Import, Staging-Transaktion, Recovery, Medien-/Protokollverteilung, Dichtheit, Portabilität, Haltungszuordnung, Befundfotos |
| `schaechte.md` | Schachtprotokolle (PDF), PDF-Textleser, SchachtPro-QR und -Archive, Schachtgrafik |
| `xtf-kataster-qgis.md` | XTF-Revision und Neu-Export, SIA405/DSS, Änderungslieferung, GeoShop-Abgleich, Katasterkennungen, QGIS-Nachfüllen und -Brücke |
| `webgis.md` | WebGIS/GEONIS: Senden, Holen, Sanierungsmassnahmen, Schutzregeln, Entscheide |
| `objektakten.md` | Objektakten, WebGIS-Maske, Dropdowns, Einbauten, DSS-Neuexport, Lieferungs-Editor |
| `dossiers.md` | Eigentümerdossiers: Word-Vorlage, PDF, Vorschau, Beilagen, Listen |
| `kosten-und-berichte.md` | Kostenrechner, Fachzahlen, Excel-Vorlagen, Stammdaten-Stores |
| `ki-pipeline-und-codiermodus.md` | Videoanalyse, Codiermodus, Belegbindung, Detektorboxen, KI-Vorschläge (Bogen, Rohranfang/-ende) |
| `training-und-modelle.md` | Modellqualifikation, Goldbestand, Training Studio/Center, YOLO-Export, Holdouts, OSD-Meterleser, Eval-Messung |
| `sicherung-und-dateischutz.md` | Vollsicherung, Echtzeit-Spiegel, Programm-Momentaufnahme, Umbenennen, Pfadschutz |
| `querschnitt-audits.md` | Regeln aus den Gesamtaudits (08-14, 09-19, 09-23), Projektprüfung |
| `wichtige-klassen.md` | Kurzbeschreibung zentraler Klassen |

## Querschnittsregeln (Kurzfassung)

Diese Regeln wirken in mehreren Bereichen. Begründung, Grenzen und Tests stehen in der
genannten Bereichsdatei.

**Daten und Dateien**
- Kundenoriginale nie verändern. Schreiben nur über die Pfadwächter
  (`ProjectWritePathGuard`, `DistributionWritePathGuard`); keine Verknüpfungen/Junctions betreten.
  → `sicherung-und-dateischutz.md`, `import-und-verteilung.md`
- Importe schreiben über die gemeinsame Staging-Transaktion (`ImportFileTransaction`,
  `.import-transaction.json`); Dateigleichheit nur über den vollständigen Inhalt. → `import-und-verteilung.md`
- Handwerte (`FieldMeta.UserEdited`, auch bewusst leer) überschreibt kein Import und kein Abgleich.
  Die Kanalfirma ist der Ist-Zustand und ersetzt Katasterwerte ohne Handmarke; vermessene
  Katasterkoordinaten ersetzt kein Import. → `xtf-kataster-qgis.md`, `webgis.md`
- Schachtfelder immer über `SchachtFeldnamen` lesen und vergleichen. Am Schacht wird die
  Zustandsklasse nie berechnet. → `xtf-kataster-qgis.md`
- Virtuelle Anzeigespalten (Präfix `Nova_`) landen nie in `Fields`. → `oberflaeche.md`
- Eine Anzeigegrenze darf nie die fachliche Auswahl oder Übernahmeliste begrenzen; ein Teillauf
  bleibt bis ins Ergebnis als unvollständig sichtbar. → `ki-pipeline-und-codiermodus.md`
- Befundfotos gehören nie in den Temp-Ordner. → `import-und-verteilung.md`

**KI und Training**
- Gold nur mit persönlicher Bestätigung, gültiger Hand-Box und gültiger SAM-Maske. Prüf- und
  Eval-Daten nie ins Training; Suche nur über `GuardedRetrievalFactory`. → `training-und-modelle.md`,
  `querschnitt-audits.md`
- Kandidatenmodelle bleiben `not_deployed`; YOLO nur bei ausdrücklichem `qualified=true`.
  → `training-und-modelle.md`
- KI-Markierungen im Player gehen über `toPixel`. → `ki-pipeline-und-codiermodus.md`

**Oberfläche**
- Farben, Schriften, Radien nur über Theme-Tokens (`DynamicResource`), Schrift mindestens 11 px.
  Meldungen über `IDialogService`/`NovaDialog`, reine Erfolge als Toast. Fenster mit
  `NovaDialogHeader` und `DialogButtonBar` nach der Knopfregel. → `oberflaeche.md`
- Sichtbare Texte mit echten Umlauten und «ss» statt «ß»; Fehlermeldungen über `UserError`.
  Datenschlüssel und Log-Zeilen bleiben unverändert. → `oberflaeche.md`
- Isolierte WPF-Tests: nach `Show()` `UpdateLayout()` statt `ApplicationIdle`-Invoke. → `oberflaeche.md`

**Export**
- XTF: stabile Kennungen, `Bemerkung` nie kürzen, ein Änderungsauftrag trägt nie einen Wert,
  die reine Normlieferung braucht kein Zusatzmodell. → `xtf-kataster-qgis.md`
- WebGIS: nur den bestätigten Plan schreiben; Eigentum, Betreiber, Länge und Baujahr nie
  überschreiben; GlobalID nur bei genau einem Treffer. → `webgis.md`

## Projekt-Kontext
- **App:** WPF / .NET 10, MVVM, Windows 11
- **Zweck:** Automatisierte Kanalinspektion, ~3000 Videos aus Kanal-TV-Exporten
- **Standards:** EN 13508-2, VSA-KEK; aktive Quelle: `vsa_kek_2020_catalog_manifest.json`
- **Entwickler:** Solo, kein kommerzielles Ziel
- **Hardware:** Intel Core Ultra 9 285K · ASUS RTX 5090 32GB · 64GB DDR5

## AI-Pipeline (Ist-Zustand, HEAD)
- C# steuert Geschaeftslogik, UI, Dedup, QualityGate und Persistenz.
- Sidecar `sidecar/sidecar/` liefert YOLO, Grounding DINO und SAM ueber HTTP.
- YOLO: Standard-Gewicht `yolo26m.pt` bzw. TensorRT-Engine, wenn vorhanden; COCO-Fallback `yolo11m.pt`, wenn eigene Gewichte fehlen und Fallback erlaubt ist.
- Qwen3-VL laeuft ueber Ollama fuer Bild-/Code-Analyse. GPU-Auto waehlt ab 24 GB VRAM `qwen3-vl:8b-q8`, sonst Default/Fallback `qwen3-vl:2b`; NIE auf qwen2.5 zurueckfallen. Keine Doku-Annahme zu automatischer 8B->32B-Laufzeit-Eskalation treffen.
- Grounding DINO: on-demand im Sidecar; Loader bevorzugt Swin-B (`grounding_dino_swinb`), Fallback Swin-T OGC (`grounding_dino_1.5`). Swin-B Stresstest 2026-06-20 bestanden (1000 Frames, 0 Timeouts, Forward ~107 ms, VRAM-Peak ~21,3 GB ≪ 29 GB) → behalten.
- SAM: **SAM 2.1** (`sam2.1_hiera_large.pt` unter `models/sam2.1/`, via `SAM2ImagePredictor`, box-getrieben). SAM-1 `vit_h` ist im Sidecar entfernt. SAM 3 nur deaktivierte Experiment-Option (`sam3_weights_path`, Default aus, kein Wrapper/keine Route); alte `models/sam3/`-Ablage entfernt.
- `sam_wrapper.segment` setzt nach jeder Anfrage die bildbezogenen SAM-Merkmale
  innerhalb von Predict-Lock und Busy-Lease zurueck, auch bei Inferenzfehlern.
  FakePredictor-Tests: `sidecar/tests/test_sam_predictor_cleanup.py`. GPU-Probe
  mit kuenstlichem 640x480-Bild: 16 MiB weniger `memory_allocated` nach der
  Anfrage; `memory_reserved` blieb wegen PyTorch-Cache gleich (1858 MiB).
- Bogen-Geometrie (`bend_geometry.py`, Fluchtpunkt/Bogen-Veto): im HEAD per Default DEAKTIVIERT (`bend_geometry_enabled=false`).
- Dedup/Merge: C#-framebasiert ueber `TemporalFindingDeduplicator` und `TemporalCodeVotingService`. Keine Annahme zu alten `UpdateActive`-Duplikaten treffen.
- Kein ByteTrack/OC-SORT und kein echtes Multi-Object-Tracking in HEAD.
- Der YOLO-Trainings-Export ist seit AP 0.3 plan-gesteuert: C# erzeugt vor dem
  Sidecar-Healthcheck genau einen unveraenderlichen Plan. Sidecar und lokaler
  Ausfuehrer schreiben nur noch diesen Plan und treffen keine eigene Klassen-,
  Split-, Quarantaene- oder Dateinamenentscheidung.

## Architektur-Prinzipien (NICHT brechen)
- Thin-AI: C# fuer alle Geschaeftslogik, LLM nur fuer Textgenerierung
- Kein grosses Refactoring ohne explizite Diskussion
- Laptop-Mode / Workstation-Mode Hardware-Abstraktion erhalten
- VRAM-Budget: max 29GB stabil, niemals alle Modelle gleichzeitig. Der Sidecar
  begrenzt CUDA-Modellladungen vor und nach dem Laden anhand der geraeteweiten
  VRAM-Belegung (inklusive anderer Prozesse). Laufende Ladungen zaehlen mit;
  bei Budget- oder Messfehler wird kontrolliert `insufficient_vram` gemeldet.
  `SEWER_SIDECAR_VRAM_BUDGET_GB` darf die Grenze nur absenken. Waehrend einer
  Inferenz kann der Verbrauch weiter wachsen; die Ladepruefung ersetzt keine
  laufende VRAM-Garantie.
- QualityGate Green/Yellow/Red muss immer durchlaufen
- Neue Workflow-/Orchestrierungsklassen (Request/Actions/Result) nach
  `src/AuswertungPro.Next.Application/UseCases/` statt nach `UI/Ai/`; der UI/Ai-Bestand
  ist per `UiAiFreezeArchitectureTests` eingefroren (Referenzbeispiel: `CodingModeBackgroundServicesWorkflow`).

### Checkliste bei jedem neuen Service / Tool (vor dem Commit pruefen)
1. **Interface + eigener Service:** Neue Logik als eigener Service mit Interface, nicht in bestehende Klassen quetschen. Neue Workflow-/Orchestrierungsklassen (Request/Actions/Result-Muster) gehoeren nach `src/AuswertungPro.Next.Application/UseCases/`, nicht nach `UI/Ai/` (eingefroren per `UiAiFreezeArchitectureTests`).
2. **Schichten trennen:** Geschaeftslogik in C# (nicht in UI-Code, nicht im Sidecar). UI ruft ViewModel/Service, nie direkt Infrastruktur.
3. **Registrierung:** Service im `ServiceProvider` (DI) eingetragen, kein `new` verstreut im Code.
4. **Fokussierter Test:** Mindestens ein Test fuer die Kernlogik (Parser/Pipeline/ViewModel/QualityGate). Keine riskante Logik ohne Test.
5. **Budget & Gate:** VRAM-Budget (max 29GB) nicht gebrochen, QualityGate laeuft weiter durch.
6. **Klein bleiben:** Kein grosses Refactoring am Bestand ohne Rueckfrage — neues Feature additiv bauen.

## Build & Test
```bash
dotnet build AuswertungPro.sln
dotnet test AuswertungPro.sln
```

`AuswertungPro.sln` enthaelt die vier produktiven Projekte, die vier Testprojekte
und alle 45 `tools/**/*.csproj`. Neue Werkzeugprojekte sofort aufnehmen, damit
verschobene Klassen oder Projektverweise im normalen Release-Build sichtbar brechen.

Den vollständigen Prüfweg vor einem Commit (Release-Build, vier Testprojekte, Python-Tests)
beschreibt `AGENTS.md`. Der Pre-Push-Hook unter `.githooks/` führt den .NET-Teil aus,
die CI zusätzlich die Python-Tests.

## Fachdomaene Kanalinspektion

### Grundbegriffe
- **Haltung:** Kanalabschnitt zwischen zwei Schaechten (typisch 30-80m)
- **Schacht:** Zugang zum Kanal (Anfangs-/Endknoten einer Haltung)
- **DN:** Nennweite in mm (DN150=Hausanschluss, DN300=Standard, DN600+=Sammler)
- **OSD:** On-Screen Display im Video — zeigt Meterstand, Haltungsname, Datum
- **Meterstand:** Position der Kamera in der Haltung (0.00m = Anfang, z.B. 45.30m = Ende)

### Schadenscodierung (VSA-KEK / EN 13508-2)
Codes sind hierarchisch aufgebaut: **Hauptcode** (2-3 Buchstaben) + **Char1** (Untertyp) + **Char2** (Lage)

**Grundgeruest (BC-Gruppe, Bestandsaufnahme):**
- BCD = Rohranfang (Kamera faehrt in Rohr ein, Schacht sichtbar)
- BCE = Rohrende (Endknoten erreicht)
- BCA = Seitlicher Anschluss (runde/ovale Oeffnung in Rohrwand)
- BCC = Bogen (Richtungsaenderung, ueber mehrere Frames sichtbar)

**Strukturelle Schaeden (BA-Gruppe):**
- BAA = Verformung (A=vertikal, B=horizontal)
- BAB = Riss (A=laengs, B=quer, C=diagonal, D=ringfoermig, E=verzweigt)
- BAC = Bruch (A=partiell, B=total)
- BAF = Oberflaechenschaden (rauhe Rohrwandung, chemischer Angriff, Korrosion)
- BAH = Schadhafter Anschluss
- BAI = Einragendes Dichtungsmaterial
- BAJ = Verschobene Rohrverbindung (breit, versetzt, Knick)

**Betriebliche Stoerungen (BB-Gruppe):**
- BBA = Wurzeln/Bewuchs
- BBB = Anhaftende Stoffe/Inkrustation/Fett
- BBC = Ablagerung (A=Sand, B=Kies, C=verfestigt)
- BBD* = Eindringender Boden (kein Basiscode BBD, nur Untercodes)
- Die Detect-Klasse `BBD_boden` ist erlaubt; beim Rueckmapping speichert C# den
  gueltigen allgemeinen Untercode `BBDZ`, niemals den nackten Basiswert `BBD`.

### Quantifizierung
- **Uhrlage:** 12:00=Scheitel (oben), 6:00=Sohle (unten), 3:00=rechts, 9:00=links
- **Severity 1-5:** 1=optisch, 2=leicht, 3=mittel (Sanierung mittelfristig), 4=schwer (kurzfristig), 5=kritisch (Sofortmassnahme)
- **Ausdehnung:** Prozent des Rohrumfangs
- **Querschnittsverringerung:** Prozent des freien Querschnitts
- Das VSA-Codierfenster zeigt an jedem sichtbaren Q1-/Q2-Feld die fachliche
  Einheit direkt neben der Zahl (`mm`, `%`, `°` oder `Stk.`) und den erlaubten
  Bereich. Dieselbe Regel validiert die Eingabe; ein sichtbares Mengenfeld ohne
  Einheit ist nicht zulässig.
- Der aktive VSA-KEK-2020-Manifestkatalog entscheidet, welche Endcodes
  auswählbar sind. Die code- und charakterabhängigen Einheiten und Grenzwerte
  des Kanal-Pickers sind gegen den lokal installierten WinCan-Katalog
  `EN13508_VSA-2019_CH_DEU_SEC.xml` abgeglichen. WinCan-Zwischenüberschriften
  wie `Status` oder `Vertikale Richtung` sind keine Code-Klartexte.

### Punktschaden vs. Streckenschaden
- **Punktschaden:** An einer Stelle (z.B. Riss, Anschluss) — ein Meterstand
- **Streckenschaden:** Ueber Laenge (z.B. Korrosion 2.5m-8.0m) — MeterStart bis MeterEnd
- Beim manuellen Schliessen wird der Endmeter aus dem aktuellen Videoframe
  ermittelt: frische OSD-Metrierung vor Timeline-Schaetzung vor dem letzten
  Sessionwert. So darf ein sichtbarer neuer Meterstand nicht durch einen alten
  Startwert ersetzt werden.

## Coding-Regeln
### Allgemein
- Bestehenden Code nur aendern wenn explizit gefragt
- Neue Features als separate Services mit Interface
- Tests breit einsetzen: Parser, Import, Pipeline, KnowledgeBase, UI-ViewModels und QualityGate. Keine riskanten Logik-Aenderungen ohne fokussierten Test.
- Keine NuGet-Pakete ohne Rueckfrage
- Kommentare auf Deutsch
- JSON-Schema fuer alle Qwen-Outputs (strict, kein freier Text)


