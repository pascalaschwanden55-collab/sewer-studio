# SewerStudio — Roadmap

**Stand:** 2026-05-08
**Branch:** `feature/pdf-import-beobachtungen`

## Was ist erreicht (2026-04-19 bis 2026-05-08)

Das System ist von "ambitioniertes Solo-Projekt" zu einem
**architektonisch sauberen Fachsystem** gewachsen:

### Sicherheit + Stabilitaet (April 2026)
- 8 Command-Injection-Stellen geschlossen
- Path-Traversal-Schutz durchgaengig
- Sidecar-Auth-Token-Pflicht
- HttpClient-Leaks geschlossen
- async-void-Timer mit Closed-Guard
- XXE-Schutz im SafeXmlLoader

### Architektur (Mai 2026)
- Microsoft.Extensions.DependencyInjection eingefuehrt
- Provider/Bridge-Pattern fuer UI-Application-Trennung
- 76 Files aus UI-Schicht in Application/Infrastructure migriert
- PlayerWindow von 5.370 auf < 850 Zeilen reduziert
- **HoldingFolderDistributor von 4.576 auf 187 Zeilen** in 16 Partials
- 5 Architecture-Decision-Records dokumentiert

### Testbasis
- 654 → **819 Tests** gruen (+165 Tests, +25 %)
- 22 Charakterisierungs-Tests fuer HFD
- 15 Sidecar-Contract-Tests (12 Stub + 3 Live)
- 2 Architektur-Tests fuer Domain-Sauberkeit
- Build durchgehend 0 Warnungen, 0 Fehler

### Prozess
- CI-Pipeline (GitHub Actions) Build + Test bei jedem Push
- ADR-Verzeichnis mit 5 wichtigen Entscheidungen
- Index-Cleanup: 67 grosse Generierte-Files raus, ~50 MB Index kleiner
- README + Doku auf aktuellem Stand

## Offene Punkte — priorisiert

### Hoechste Prioritaet: KI-Datenqualitaet

Das ist der **eigentliche Hebel** fuer Produkt-Qualitaet. Die Architektur
ist nicht das Problem — die KI-Erkennung ist es (52 % ValidationLog-Accuracy).

#### P1.1 — Active-Learning-Wochenroutine
- 100 unsichere Samples pro Woche manuell labeln
- KB waechst gezielt um Confusion-Cluster
- Aufwand: ~2 h pro Woche, dauerhaft
- Erwartete Wirkung nach 12 Wochen: 52 % → 65-75 %

#### P1.2 — `CategoryWeights` aktivieren
- Tabelle existiert in der KB, ist aber leer
- Per-Code-Gewichte aus echtem Feedback lernen
- Quality-Gate kann dann pro Code unterschiedlich strikt sein
- Aufwand: 1-2 Tage Code + Daten

#### P1.3 — `TrainingRuns`-Provenance
- Bei jedem echten Training/Export-Run einen Eintrag schreiben
- Damit ist nachvollziehbar welches Sample aus welchem Run kommt
- Voraussetzung fuer Regression-Detection
- Aufwand: 1 Tag

#### P1.4 — KB-Dashboard im Diagnose-Tab
- Green/Yellow/Red-Verteilung sichtbar machen
- Top-Confusion-Codes
- Drift-Erkennung
- Aufwand: 2-3 Tage

#### P1.5 — Brain-Mirror Restore-Drill
- Ein einziges Mal einen kompletten Restore-Test durchfuehren
- Ergebnisse dokumentieren
- Aufwand: 1 Tag

### Mittlere Prioritaet: Architektur-Restschuld

#### P2.1 — Domain-Layer entkoppeln (ADR-0004)
- ViewModel-Wrapper komplettieren (HaltungRecord ✓, SchachtRecord fehlt)
- 50+ UI-Konsumenten auf Wrapper umstellen
- 133 XAML-Bindings ueberfuehren
- INotifyPropertyChanged aus Domain entfernen
- **Aufwand: 3-5 Tage** (NICHT in autonomer Session machbar)

#### P2.2 — KI-Schicht (`MultiModelAnalysisService`) aus UI-Layer
- Aktuell in `src/AuswertungPro.Next.UI/Ai/Pipeline/`
- Soll nach `Application/Ai/Pipeline/` migriert werden
- Voraussetzung: Canvas-/Color-Refactor (ARCH-H5)
- Aufwand: 2-3 Tage

#### P2.3 — Logging-Strategie zentralisieren
- Aktuell streckenweise `Debug.WriteLine` direkt im Code
- Eine zentrale `ILogger`-Schnittstelle (Microsoft.Extensions.Logging)
  wuerde strukturiertes Logging ermoeglichen
- Aufwand: 1-2 Tage Migration

### Niedrige Prioritaet: Operational

#### P3.1 — UI-Smoke-Tests
- App-Start-Test, ServiceProvider-Resolved-Test
- 3-5 neue Tests, halber Tag

#### P3.2 — Git-History-Rewrite (BFG / git filter-repo)
- Pack-Size von 338 MB schrumpfen
- Force-Push erforderlich → Team-Abstimmung
- Aufwand: 2 h + Risiko-Diskussion

#### P3.3 — Externe Tests aktiv koordinieren
- TEST_BRIEFING ist da, Tester muessen aktiviert werden
- Wochenweise Feedback-Auswertung
- Aufwand: dauerhaft

### Zukunftsoptionen

#### Z.1 — BenchmarkDotNet-Integration
- Performance-Regressions-Tests fuer kritische Pipelines
- z.B. PdfTextExtractor, YOLO-Sidecar-Calls, KB-Queries
- Aufwand: 2-3 Tage initial

#### Z.2 — OpenTelemetry-basierte Telemetrie
- Aktuell nur PipelineTelemetry (SQLite)
- App-weite Error-/Performance-Telemetrie waere wertvoll
- Aufwand: 3-5 Tage

#### Z.3 — Dependabot/Renovate fuer Dependency-Updates
- NuGet-Pakete automatisch auf neuere Versionen pruefen
- Aufwand: 1 h Setup

#### Z.4 — Code-Coverage-Report
- Coverlet + ReportGenerator in CI
- Coverage als PR-Gate
- Aufwand: 1 Tag

#### Z.5 — Stufe 2 der KI-Codier-Vision
- "Operateur setzt nur BCD/BCE, KI macht den Rest"
- Voraussetzung: > 75 % ValidationLog-Accuracy, > 500 manuell
  validierte Faelle
- Aufwand: 6-12 Monate Reifezeit

## Bewusst NICHT geplant

- **All-LLM-Pipeline**: gegen ADR-0005 (Thin-AI-Prinzip).
- **Cloud-Migration**: gegen Datenschutzversprechen (lokal, kein Upload).
- **Multi-User-Server-Variante**: Solo-Setup ist explizites Ziel.
- **Internationalisierung**: deutscher Sprachraum, Schweizer Markt.

## Fortschritts-Tracking

Jeder Punkt sollte beim Bearbeiten:

1. Einen Branch oder PR bekommen
2. Tests vor jeder Aenderung gruen halten
3. Nach Abschluss: Eintrag in CHANGELOG.md (sobald angelegt) +
   Tick in dieser Roadmap.
