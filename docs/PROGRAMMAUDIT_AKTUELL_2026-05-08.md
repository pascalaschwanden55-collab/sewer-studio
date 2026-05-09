# Programmaudit AuswertungPro / Sewer Studio

Stand: 2026-05-08, aktueller Arbeitsstand nach `f4378a2`  
Branch: `feature/pdf-import-beobachtungen`  
Remote-Stand: 3 Commits ahead von `origin/feature/pdf-import-beobachtungen`  
Arbeitsbaum vor Erstellung dieses Berichts: sauber, nur ignorierte Build-/Cache-/Modellartefakte. Nach Erstellung ist diese Auditdatei untracked.

## Kurzfazit

Der aktuelle Stand ist deutlich stabiler als im vorherigen Audit: die .NET-Solution baut sauber, der komplette .NET-Testlauf ist gruen, die DataPageViewModel-Aufteilung ist erfolgreich abgeschlossen und zentrale Stabilitaets-/KI-Slices sind bereits umgesetzt. Die naechsten groessten Risiken liegen nicht mehr im Build, sondern in UI-Monolithen, Sidecar-Testkonfiguration, restlichen Prozess-/HTTP-/Dialogpfaden und der noch nicht vollstaendig entkoppelten Architektur.

## Verifizierter Stand

| Bereich | Ergebnis |
| --- | --- |
| `dotnet build AuswertungPro.sln -v minimal` | erfolgreich, 0 Warnungen, 0 Fehler |
| `dotnet test AuswertungPro.sln -v minimal --no-restore` | 946 bestanden, 1 uebersprungen, 0 Fehler |
| Infrastructure Tests | 185 bestanden, 1 uebersprungen |
| Pipeline Tests | 761 bestanden |
| Sidecar CI-Auswahl mit Workspace-Temp | 8 bestanden, 1 Warning, Coverage 38 % |
| Voller Sidecar-Testlauf | nicht gruen, Live-Batch-Test bekommt 401 ohne Token/Header |
| Repo-Dateien via `rg --files` | 1121 |
| C# | 686 Dateien, 147904 Zeilen |
| XAML | 63 Dateien, 17091 Zeilen |
| Python | 83 Dateien, 22080 Zeilen |
| Markdown | 81 Dateien, 22349 Zeilen |
| JSON | 133 Dateien, 138278 Zeilen |

## Groesste aktuelle Dateien

| Datei | Zeilen | Bewertung |
| --- | ---: | --- |
| `src/AuswertungPro.Next.UI/Views/Windows/CodingModeWindow.xaml.cs` | 3467 | groesster UI-Monolith |
| `src/AuswertungPro.Next.Application/Reports/ProtocolPdfExporter.cs` | 2648 | grosser Renderer, naechster Composer-Kandidat |
| `src/AuswertungPro.Next.UI/Views/Windows/PlayerWindow.CodingMode.cs` | 2631 | Coding-Workflow im PlayerWindow |
| `src/AuswertungPro.Next.UI/Views/Pages/DataPage.xaml.cs` | 2206 | Code-behind noch gross |
| `src/AuswertungPro.Next.UI/Views/Windows/PhotoMeasurementWindow.xaml.cs` | 1953 | UI + Messlogik noch gekoppelt |
| `src/AuswertungPro.Next.UI/ViewModels/Windows/CostCalculatorViewModel.cs` | 1577 | eigenes ViewModel-Gewicht |
| `src/AuswertungPro.Next.UI/Views/Pages/SchaechtePage.xaml.cs` | 1561 | Schacht-UI noch code-behind-lastig |
| `src/AuswertungPro.Next.UI/Views/Windows/TrainingCenterWindow.xaml.cs` | 1542 | Trainings-UI bleibt gross |
| `src/AuswertungPro.Next.UI/Ai/Pipeline/MultiModelAnalysisService.cs` | 1379 | noch in UI, trotz neuer Imaging-Abstraktion |
| `src/AuswertungPro.Next.Infrastructure/Import/WinCan/WinCanDbImportService.cs` | 1329 | Import-Service gross, aber fachlich fokussierter |

## DataPageViewModel Fortschritt

Die DataPage-Aufteilung ist ein klarer Erfolg. Die Hauptdatei liegt bei 606 Zeilen und damit deutlich unter der 1000-Zeilen-Faustregel.

| Datei | Zeilen |
| --- | ---: |
| `DataPageViewModel.cs` | 606 |
| `DataPageViewModel.MediaProtocol.cs` | 756 |
| `DataPageViewModel.Print.cs` | 364 |
| `DataPageViewModel.Options.cs` | 296 |
| `DataPageViewModel.Sanierung.cs` | 250 |
| `DataPageViewModel.Cost.cs` | 238 |
| `DataPageViewModel.Edit.cs` | 178 |
| `DataPageViewModel.Learning.cs` | 134 |
| `DataPageViewModel.SelectedProtocolSync.cs` | 121 |
| `DataPageViewModel.Search.cs` | 37 |

Verbesserungsvorschlag: Phase 1.1 bis 1.3 als abgeschlossen markieren. Nicht weiter an `DataPageViewModel.cs` schneiden, sondern naechste UI-Schnitte auf `ProtocolPdfExporter`, `PhotoMeasurementWindow` und CodingMode fokussieren.

## Auditpunkte mit Verbesserungsvorschlag

| Nr. | Prioritaet | Bereich | Befund | Verbesserungsvorschlag |
| --- | --- | --- | --- | --- |
| 1 | P0 | Build | Build ist aktuell gruen. | Build-Gate beibehalten; vor jedem Slice `dotnet build` ausfuehren. |
| 2 | P0 | .NET-Tests | Voller .NET-Testlauf ist gruen: 946 bestanden, 1 Skip. | Diesen Stand als neue Baseline dokumentieren; bei jedem Commit mindestens relevante Tests, vor Push kompletter Testlauf. |
| 3 | P0 | Git-Stand | Branch ist 3 Commits ahead; Arbeitsbaum sauber. | Vor weiteren riskanten Slices pushen oder bewusst lokalen Stack dokumentieren. |
| 4 | P0 | Sidecar-Tests | Voller `sidecar/tests`-Lauf scheitert lokal bei Live-Batch-Endpoints mit 401, weil keine Auth-Header gesetzt werden. | Live-Sidecar-Tests klar von CI-Mocktests trennen; `SIDECAR_TOKEN`/`X-Sidecar-Token` in Fixture unterstuetzen oder Test explizit als Live/Auth-Test markieren. |
| 5 | P1 | Sidecar-CI | CI-Auswahl laeuft lokal mit `--basetemp .tmp/pytest-sidecar`: 8 passed, Coverage 38 %. Ohne Basetemp blockiert lokales Temp-ACL. | CI-Kommando um explizites `--basetemp` ergaenzen oder lokale Temp-ACL bereinigen; Coverage-Hard-Gate erst nach stabiler Baseline setzen. |
| 6 | P1 | UI-Architektur | `CodingModeWindow.xaml.cs` und `PlayerWindow.CodingMode.cs` sind zusammen ueber 6000 Zeilen und teilen Coding-Verantwortung. | Naechster mittlerer UI-Slice: Verantwortlichkeiten per ADR klaeren, Hotkeys/Event-Routing erfassen, danach gemeinsame Coding-Services extrahieren. |
| 7 | P1 | Reporting | `ProtocolPdfExporter.cs` ist weiterhin ein grosser Renderer mit Header, Fotos, KI-Summary und Sanierung. | Composer-Split fortsetzen: HeaderComposer, PhotoComposer, AiSummaryComposer, SanierungComposer; pro Composer Charakterisierungstest oder Golden-Render-Smoke. |
| 8 | P1 | PhotoMeasurement | `PhotoMeasurementWindow.xaml.cs` mischt noch UI und Messablauf. Application-Services fuer Bend/Deformation/Lateral existieren. | Code-behind auf UI-Glue reduzieren; Messentscheidungen in Services verschieben; danach manueller UI-Smoke. |
| 9 | P1 | DataPage Code-behind | `DataPage.xaml.cs` bleibt mit 2206 Zeilen gross, obwohl ViewModel jetzt sauberer ist. | Fachlogik identifizieren und in ViewModel/Services verschieben; XAML-Code-behind nur Events und UI-Adapter. |
| 10 | P1 | MultiModelAnalysis | `MultiModelAnalysisService` liegt noch in UI; `IImageBitmapAnalyzer` ist als Voraussetzung fuer Migration vorhanden. | Phase 6.3 vorbereiten: UI-Abhaengigkeiten weiter kappen, Service nach Application/Infrastructure migrieren. |
| 11 | P1 | Domain-Kopplung | `HaltungRecord` und `SchachtRecord` implementieren weiter `INotifyPropertyChanged` in Domain. | Als explizite Hochrisiko-Phase lassen; erst nach separatem Go mit ViewModel-Wrappers migrieren. |
| 12 | P1 | Dialoge | `MessageBox.Show` kommt noch 154-mal vor. | Direkte Dialoge stufenweise durch `IDialogService` ersetzen; neue UI-Slices duerfen keine neuen direkten MessageBox-Aufrufe einfuehren. |
| 13 | P1 | Async | `async void` kommt 44-mal vor. | Nur echte WPF-Eventhandler erlauben; alle anderen Pfade ueber `AsyncRelayCommand` oder geloggte Fire-and-forget-Helfer. |
| 14 | P1 | Fehlerbehandlung | Leere/stille `catch {}`-Muster sind auf 115 Treffer gesunken, aber noch hoch. | Erwartete Best-Effort-Catches kommentieren; unerwartete Fehler mindestens debug/loggen und in Result aufnehmen. |
| 15 | P1 | HTTP | `new HttpClient` kommt noch 34-mal vor. | Phase 4.3 planen: typed Clients bzw. `IHttpClientFactory` fuer Sidecar/Ollama/Health-Pfade. |
| 16 | P1 | Prozesse | `Process.Start` kommt noch 41-mal vor; Phase 4.4 ist erst teilweise umgesetzt. | Restliche Aufrufe inventarisieren: externe Oeffner vs. echte Prozesse trennen; `IExternalOpener` und `ProcessRunner` konsequent nutzen. |
| 17 | P1 | Blocking Calls | `GetAwaiter().GetResult`, `.Wait()` und `.Result` zusammen 67 Treffer. | UI-Pfade priorisiert async machen; bei Tests und Konstruktor-Bridges bewusst dokumentieren. |
| 18 | P1 | Test-Traits | 134 Trait-Zeilen in 114 Testdateien; Kategorien Unit/Integration/Slow/GpuEval/LiveSidecar sind sichtbar. | Gut beibehalten; CI-Default sollte LiveSidecar/GpuEval ausschliessen, Speziallaeufe dokumentiert starten. |
| 19 | P1 | Reflection-Tests | Reflection-/Private-Zugriffe bleiben in ca. 10 Testdateien. | Weiter graduell ersetzen: kleine internal Test-Seams + `InternalsVisibleTo`, kein Big-Bang. |
| 20 | P1 | KI-Datenqualitaet | CategoryWeights, TrainingRuns, KbSnapshotJournal und DriftDetector sind implementiert. | Jetzt echte Betriebsdaten pruefen: nach 1-2 Wochen Dashboard-Trends, Tabellenfuellung und QualityGate-Wirkung auswerten. |
| 21 | P1 | Sidecar Security | Fail-closed Auth ist fachlich richtig und schuetzt lokale Endpunkte. Tests/Live-Tools sind aber noch nicht sauber tokenfaehig. | Token-Handling in TestClient-/httpx-Fixtures und manuellen Smoke-Dokumenten vereinheitlichen. |
| 22 | P2 | CPM | `Directory.Packages.props` zentralisiert Hauptprojektpakete. Einige Tool-Projekte haben noch inline Versionen. | Entscheiden: Tools in CPM aufnehmen oder bewusst als externe/legacy Tools markieren. |
| 23 | P2 | Line Endings/Binaries | `.gitattributes` ist vorhanden und schuetzt Source/Binaries. | Einmaligen Normalize-Commit nur planen, wenn Diff-Wellen auftreten; nicht neben Feature-Slices mischen. |
| 24 | P2 | Ignorierte Artefakte | Viele grosse Modell-/Build-/Coverage-Artefakte sind ignoriert, u.a. `.venv`, Modelle, `coverage.xml`, `.tmp`. | Vor Push/Release `git status --ignored` stichprobenartig pruefen; keine Modellgewichte in Git aufnehmen. |
| 25 | P2 | Legacy/Docs | Viele historische Audits, PowerShell-Skripte und grosse Docs liegen weiter im Repo. | Archivstruktur beibehalten/verschaerfen: aktuelle Einstiegspunkte im README klar markieren, alte Audits nach `docs/archive` ziehen. |
| 26 | P2 | Import-Services | WinCan/XTF/Ibak/Legacy-Importe sind teils >900 Zeilen. | Nicht sofort refactoren; erst bei fachlicher Aenderung in Parser/Mapper/IO-Schritte splitten. |
| 27 | P2 | Player Mark Tool | Die 3 lokalen Ahead-Commits betreffen nur `PlayerWindow.MarkTool.cs` und verbessern BBox/SAM/Preselected-Code-Verhalten. | Vor Push manueller Coding-Smoke: IMPORT-Code markieren, Save ohne Dialog, pruefen dass Protokoll nicht veraendert wird und BBox/SAM danach sauber verschwinden. |
| 28 | P2 | Coverage | .NET-Coverage wird erzeugt, Sidecar-Coverage ist Baseline ohne Hard-Gate. | Erst Coverage-Baseline sammeln, dann Schwelle setzen; bei Sidecar realistisch niedriger starten und routeweise ausbauen. |
| 29 | P2 | App-Smoke | Refactors sind testgruen, aber WPF-UI braucht manuelle Validierung. | Kurzer Smoke vor Push: DataPage, Print/PDF, Video/Relink, Coding-Markierung, Diagnose/KB-Dashboard. |
| 30 | P2 | Architektur-Fortschritt | `IImageBitmapAnalyzer` ist ein guter Vorbereitungs-Schnitt fuer WPF-Entkopplung. | Dieses Muster fortsetzen: erst kleine Interfaces, dann Migration des grossen Services, dann UI-Glue entfernen. |

## Empfohlene naechste Reihenfolge

1. Aktuelle 3 Commits nach manuellem Coding-Smoke pushen.
2. Sidecar-Test-Fixtures tokenfaehig machen oder Live-Tests sauber markieren.
3. `ProtocolPdfExporter` Composer-Split starten oder `PhotoMeasurementWindow` entkoppeln.
4. Danach CodingMode-Verantwortung klaeren, weil dort das groesste UI-Risiko liegt.
5. Nach 1-2 Wochen echter Nutzung KI-Datenqualitaet messen: CategoryWeights, TrainingRuns, KbSnapshotJournal, DriftDetector.

## Konkreter Smoke-Test vor Push

- App starten.
- Projekt mit PDFs/Videos laden.
- DataPage: Suche, Filter, Edit, Kosten/Sanierung.
- PDF/Print aus DataPage ausloesen.
- Video oeffnen und Relink pruefen.
- Coding: IMPORT-Code vorauswaehlen, Rectangle/BBox setzen, SAM-Vorschau sehen, Save ohne VSA-Dialog.
- Danach pruefen: bestehender Protokollcode bleibt unveraendert, Trainingsannotation wurde geschrieben, BBox und SAM-Maske sind nach erfolgreichem Save weg.
- Diagnose: KB-Dashboard und Log aktualisieren.

## Schlussbewertung

Release-nahe technische Basis: gut.  
Build-/Testzustand: gut.  
UI-Wartbarkeit: deutlich verbessert, aber weiter groesster Code-Hotspot.  
KI-Datenqualitaet: Infrastruktur vorhanden, jetzt echte Betriebswirkung messen.  
Sidecar: Security verbessert, Test-/Live-Konfiguration muss nachgezogen werden.  
Architektur: Richtung stimmt, aber Domain-INPC und UI-lokalisierte KI-Services bleiben groessere Restschuld.
