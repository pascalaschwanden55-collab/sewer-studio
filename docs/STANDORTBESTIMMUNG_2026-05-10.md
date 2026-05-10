# SewerStudio — Standortbestimmung 2026-05-10

**Branch:** `feature/pdf-import-beobachtungen` (sync mit origin, keine Ahead-Commits)
**Stichtag:** 2026-05-10
**Vorgaenger-Audit:** `docs/PROGRAMMAUDIT_AKTUELL_2026-05-08.md`

Dieses Dokument korrigiert den Stand vom 08.05., der durch ~150 Commits zwischen
2026-05-08 und 2026-05-10 deutlich ueberholt ist. Es ist der Inventur-Snapshot
vor jeder weiteren Restschuld-Bearbeitung.

## Kurzfazit

Der "grosse Umbau" ist **strukturell abgeschlossen**. Build gruen, ~946 Tests
bestanden (Stand 08.05.; Pipeline-Tests sind seither auf 832-835 gewachsen).
Die ROADMAP-Items P1.2, P1.3, P1.4, P1.5 sind alle gelandet, ebenso die
Phase-6.3-WPF-Entkopplung des `MultiModelAnalysisService`. Die Audit-08.05.-
Punkte 7, 8 (in Teilen), 12, 14, 15, 16 sind durch Slices 1a-1h, 2b-2h, 3a-3b,
4a-4c, 5a-5c, 20a, 21a-b, 36 erledigt.

Echte Restschuld:
1. **Phase 1.5b** — PhotoAssistant-Services BendAngle/Deformation/Lateral in
   Application migrieren (Point2D-Record, ~10 Caller). Build-/Test-pflichtig
   auf Windows.
2. **P2.1 Domain-INPC entkoppeln** — `HaltungRecord` + `SchachtRecord`
   implementieren weiter `INotifyPropertyChanged`. Roadmap selbst flaggt:
   "NICHT in autonomer Session machbar" (3-5 Tage, 50+ UI-Konsumenten,
   133 XAML-Bindings).
3. **Sidecar-Test-Auth** — Live-Batch-Endpoints liefern 401 ohne Token; in
   Test-Fixtures `X-Sidecar-Token`-Pflicht ergaenzen oder Live-Pfade explizit
   markieren.
4. **Operativ (kein Code)** — CategoryWeights-Tabelle/`TrainingRuns` mit
   echten Produktivdaten fuellen. Keine Refactor-Aufgabe, sondern
   1-2 Wochen Live-Betrieb plus Drift-Auswertung.

## ROADMAP-Abgleich

### P1 — KI-Datenqualitaet

| ID | Titel | Stand 08.05. | Stand 10.05. | Quelle |
| --- | --- | --- | --- | --- |
| P1.1 | Active-Learning-Wochenroutine | offen | **operativ, kein Code** | 100 unsichere Samples/Woche manuell labeln; haengt am Live-Betrieb. |
| P1.2 | CategoryWeights aktivieren | offen | **DONE** | Commit `6f9699c` Phase 2.2: CategoryWeights persistieren mit Noise-Filter. |
| P1.3 | TrainingRuns-Provenance | offen | **DONE** | Commits `b63946f` (foundation), `0009959` (YoloRetrainOrchestrator), `57ea3dc` (Self/Video/Batch-Wrapper, Phase 2.3b). |
| P1.4 | KB-Dashboard | offen | **DONE** | Commit `6fe98c7` mit Top-Confusions, Quality-Verteilung, Problem-Score. |
| P1.5 | Brain-Mirror Restore-Drill | offen | **DONE (Skript)** | Commit `152f7d1`: Restore-Drill-Skript angelegt. Echter Drill-Run als Operativ-Task offen. |

### P2 — Architektur-Restschuld

| ID | Titel | Stand 08.05. | Stand 10.05. | Anmerkung |
| --- | --- | --- | --- | --- |
| P2.1 | Domain-Layer entkoppeln (ADR-0004) | offen | **offen, weiter Hochrisiko** | `HaltungRecord` + `SchachtRecord` haben weiter `INotifyPropertyChanged`. ROADMAP flaggt "NICHT in autonomer Session machbar". |
| P2.2 | KI-Schicht aus UI-Layer | offen | **DONE** | Commits `f25ec49` (WPF-frei via IPipeCalibrationFromBytes), `ec89cbc` (File-Move nach Infrastructure). |
| P2.3 | Logging-Strategie zentralisieren | offen | **teilweise** | Catch-Hygiene Phase A 1-6 hat viele Best-Effort-Catches kommentiert oder mit ILogger versehen. Restl. Migration `Debug.WriteLine` -> `ILogger` ist mechanische Klein-Arbeit. |

### P3 — Operational

| ID | Titel | Stand 10.05. |
| --- | --- | --- |
| P3.1 | UI-Smoke-Tests | **DONE** (Commit `ebb8af8`) |
| P3.2 | Git-History-Rewrite | offen, nicht autonom |
| P3.3 | Externe Tests koordinieren | dauerhaft, nicht-Code |

### Z — Zukunft

| ID | Titel | Stand 10.05. |
| --- | --- | --- |
| Z.1 | BenchmarkDotNet-Skeleton | **DONE** (Commit `efb32c6`) |
| Z.3 | Dependabot/Renovate | **DONE** (Commit `ebb8af8`) |
| Z.4 | Code-Coverage | **Skeleton DONE** (Commit `152f7d1`); Hard-Gate offen |
| Z.5 | Stufe 2 KI-Vision | weiterhin 6-12 Monate Reifezeit |

## Audit-08.05.-Abgleich

| Audit-Pkt | Bereich | Stand 08.05. | Stand 10.05. |
| ---: | --- | --- | --- |
| 7 | ProtocolPdfExporter Composer-Split | 2648 LOC, Composer-Split offen | **erledigt** durch Slices 1a-1h, 21a-b, 36 — auf 997 LOC geschrumpft, in Partials zerlegt (Header/Frame, AI-Summary, Photo-Block, ObservationTables, ObservationText, SVG-Pipeline, DamageSymbols, Grafik-Helpers, Format-Helpers, Pfad-Aufloesung, Import-Eintragsbau) |
| 8 | PhotoMeasurementWindow Entkopplung | 1953 LOC, mischt UI + Messlogik | **teilweise**: Mouse-/Tool-Events in Partial extrahiert (Slice 20a), Drawing-Helpers in Partial (5a), Multi-Punkt-Werkzeuge (5b), Result-Block (5c). Service-Migration nach Application = Phase 1.5b weiterhin offen. |
| 10 | MultiModelAnalysis in UI | offen | **erledigt** (Phase 6.3 — siehe P2.2 oben) |
| 12 | MessageBox.Show: 154 -> IDialogService | offen | **erledigt** durch Slices 2b-2h. 6 verbleibende Stellen sind legitim: 3 in App.xaml.cs (Startup-Fehler vor DI), 2 im DialogService-Wrapper selbst, 1 Doku-Kommentar |
| 13 | async void: 44 | offen | 34 — saemtliche verbleibenden sind WPF-Eventhandler oder `SafeFireAndForget`. Nur `DataPageViewModel.Print.cs:94/197` sind faktisch keine Eventhandler — Migration auf `AsyncRelayCommand` waere kosmetisch korrekt. |
| 14 | Empty `catch {}`: 115 | offen | **1** — Catch-Hygiene Phase A 1-6 hat das praktisch eliminiert. |
| 15 | new HttpClient: 34 | offen | **erledigt** durch Slices 3a-3b (`Microsoft.Extensions.Http` + named Clients, PythonSidecarService nutzt IHttpClientFactory). 16 verbleibende Stellen sind kurzlebige `using HttpClient`-Pattern fuer Health-Checks/Warmup oder dokumentierte Fallbacks bei kurzen Polls. |
| 16 | Process.Start: 41 | Phase 4.4 teilweise | **erledigt** durch Slices 4a, 4b, 4c, ProcessRunner.TryOpenWithDefaultProgram, Phase 4.4 Teil 1. |
| 4  | Sidecar-Test-Auth (401 ohne Token) | offen | **offen** — Live-Batch-Tests verlangen `X-Sidecar-Token`. Test-Fixtures muessen tokenfaehig werden oder die Tests klar als Live/Auth-Tests markiert werden. |
| 5  | Sidecar-CI Coverage | offen | offen, Hard-Gate explizit nicht gesetzt |
| 6  | CodingMode XAML-CodeBehind | offen | **deutlich kleiner** — `CodingModeWindow.xaml.cs` von 3467 auf 3075 LOC, viele Helfer in Partials extrahiert (Slices 8a.2.1-8a.2.11, 8a.3, Pause-Confirm, Auto-Kalibrierung, Auto-BCD/BCE). Coding-Logik konsolidiert. |
| 11 | Domain INPC | offen | **offen** — Hochrisiko, ROADMAP P2.1 |

## Verbleibende konkrete Restschuld

### A. Phase 1.5b — PhotoAssistant-Services nach Application

**Begruendung:** Die 3 Services (`BendAngleToolService`, `DeformationToolService`,
`LateralToolService`) sind reine Math-/Geometry-Logik, koennen aber wegen
`System.Windows.Point` aktuell nicht in `Application` (kein UseWPF) verschoben
werden.

**Schritt-fuer-Schritt-Plan (build-pflichtig auf Windows):**

1. `src/AuswertungPro.Next.Application/Ai/PhotoAssistant/Point2D.cs` anlegen:
   `public readonly record struct Point2D(double X, double Y);`
2. `BendAngleToolService.cs` von UI nach Application verschieben, alle
   `System.Windows.Point` durch `Point2D` ersetzen. Achtung: `ProjectedRing.AxisCenterScreen`,
   `ProjectedRing.RingPoints`, `KinkPointScreen` sind Returntypen.
3. `DeformationToolService.cs` analog: `center`-Param + `IReadOnlyList<Point>`.
4. `LateralToolService.cs` analog: `pipeCenter`-Param + Returntyp.
5. In `PhotoMeasurementWindow.PhotoAssistant.cs` ~10 Aufrufstellen anpassen.
   Empfohlener Helper: kleine Extension `static Point2D ToPoint2D(this Point p)`
   und `static Point ToWpfPoint(this Point2D p)` im UI-Layer
   (`AuswertungPro.Next.UI/Ai/PhotoAssistant/Point2DExtensions.cs`).
6. Drei Test-Dateien umstellen:
   - `tests/AuswertungPro.Next.Pipeline.Tests/PhotoAssistant/BendAngleToolServiceTests.cs`
   - `tests/AuswertungPro.Next.Pipeline.Tests/PhotoAssistant/DeformationToolServiceTests.cs`
   - `tests/AuswertungPro.Next.Pipeline.Tests/PhotoAssistant/LateralToolServiceTests.cs`
7. `dotnet build AuswertungPro.sln` + `dotnet test --filter Category!=LiveSidecar` gruen pruefen.

**Aufwand:** halb-tag bis 1 Tag.
**Risiko:** mittel — API-Bruch, viele Aufrufer. Build-/Test-Verifikation auf
Windows zwingend.

### B. Sidecar-Test-Fixtures tokenfaehig

**Schritte:**

1. In `sidecar/tests/conftest.py` Fixture `auth_headers` einfuehren, die
   `X-Sidecar-Token` aus `os.environ.get("SIDECAR_TOKEN", "test-token")`
   zieht.
2. Alle Live-Endpoint-Tests (`test_batch_endpoints.py`, `test_pipeline.py`,
   ggf. `test_sam.py`/`test_yolo.py`) den `headers=auth_headers`-Param mitgeben.
3. Live-only-Tests mit `@pytest.mark.live` dekorieren — Default-CI-Lauf
   skippt diese ueber `-m "not live"`.
4. README/Sidecar-Doku ergaenzen: "Live-Sidecar-Tests brauchen
   `SIDECAR_TOKEN=<token> pytest -m live`".

**Aufwand:** 2-3 h.
**Risiko:** niedrig — pytest-Aenderungen, kein Produktiv-Code.

### C. P2.1 Domain INPC entkoppeln

ROADMAP-Empfehlung: **NICHT in einer autonomen Session**. 3-5 Tage,
50+ UI-Konsumenten, 133 XAML-Bindings. Plan:

1. `HaltungRecord` als POCO-Record halten, neue `HaltungViewModel`-Wrapper-Klasse
   bekommt das `INotifyPropertyChanged`.
2. UI-Konsumenten (Pages, Windows, Dialoge) nacheinander auf Wrapper umstellen.
3. XAML-Bindings ueber Resharper/Roslyn-Refactor-Skript transformieren.
4. Architektur-Test ergaenzen, der INPC im Domain-Namespace verbietet.

**Aufwand:** 3-5 Tage konzentrierter Arbeit.
**Risiko:** hoch — bricht UI-Bindings flaechig. Manueller WPF-Smoke unverzichtbar.

### D. Operativ (kein Code)

- 1-2 Wochen Live-Betrieb fuer CategoryWeights / TrainingRuns / KbSnapshotJournal /
  DriftDetector — danach Wirkungs-Auswertung in KB-Dashboard.
- Brain-Mirror Restore-Drill einmalig ausfuehren (Skript existiert).
- Externe Tester aus `TEST_BRIEFING_2026-05-07.md` aktivieren, Wochenfeedback.

## Empfohlene Reihenfolge fuer naechste Session

1. **Sidecar-Test-Auth** (B) — niedriges Risiko, beseitigt 401-Krach im
   vollen Sidecar-Testlauf.
2. **Phase 1.5b** (A) — mittleres Risiko, sauberer Architektur-Schnitt.
3. **Live-Betrieb 1-2 Wochen** (D) — operativ, parallel laufen lassen.
4. Erst danach **P2.1** (C) als grosse Phase mit eigenem Plan.

## Was ist KEINE Empfehlung mehr

Aus dem Audit 08.05. nicht mehr handlungsleitend:

- "ProtocolPdfExporter Composer-Split starten" (erledigt)
- "Restliche `MessageBox.Show` durch `IDialogService` ersetzen" (de-facto erledigt)
- "Process.Start inventarisieren" (erledigt)
- "MultiModelAnalysisService aus UI ziehen" (erledigt)

## Anhang — Verifizierte Stand-Daten 10.05.

| Metrik | Wert |
| --- | ---: |
| MessageBox.Show in src/ | 6 (3 legitim Startup, 3 Wrapper/Doku) |
| `new HttpClient` in src/ | 16 (groesstenteils kurzlebig `using` oder Fallback) |
| `async void` in src/ | 34 (alle WPF-Eventhandler bzw. SafeFireAndForget bis auf 2 in `DataPageViewModel.Print.cs`) |
| Leere `catch {}` in src/ | 1 |
| Pipeline-Tests | ~835 |
| Branch-Stand | sync mit origin/feature/pdf-import-beobachtungen |
| Groesste Dateien | `CodingModeWindow.xaml.cs` 3075 LOC, `MultiModelAnalysisService.cs` 1558 LOC, `WinCanDbImportService.cs` 1095 LOC, `BuilderPageViewModel.cs` 1033 LOC, `ProtocolPdfExporter.cs` 997 LOC |

---

Stand verfasst nach Inventur 10.05.2026; vergleicht git log seit
`78e39eb` (Untracking des 08.05.-Audit) mit ROADMAP/AUDIT-Vorgabe.
