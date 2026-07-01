# Architektur- und Code-Audit - Wartbarkeit und Struktur

Datum: 2026-07-01
Branch/Stand bei Audit: `feature/gis-karte`, HEAD nach Messung: `b48a15a3`
Fokus: Wartbarkeit, Struktur, Architekturgrenzen, Refactoring-Prioritaeten
Nicht-Fokus: fachliche Korrektheit einzelner Kanal-TV-Algorithmen, UI-Design, Security-Pentest

## Kurzurteil

Der Code ist deutlich besser als ein klassischer gewachsener WPF-Monolith: Es gibt echte Schichten (`Domain`, `Application`, `Infrastructure`, `UI`), viele Controller-Extraktionen, Architektur-Guard-Tests und eine breite Testsuite. Die kritischste Architekturgrenze - `Application` haengt nicht echt an `UI` oder `Infrastructure` - ist im Wesentlichen intakt.

Die Hauptschwaeche bleibt die UI-Schicht: Sie ist sehr gross, haengt breit an konkreten Infrastructure-Klassen und am konkreten `ServiceProvider`, und enthaelt weiterhin mehrere God- bzw. Near-God-Dateien. Das ist aktuell beherrschbar, aber nicht A- sauber. Die naechsten Wartbarkeitsgewinne kommen nicht durch weitere kleine Controller-Splitter allein, sondern durch klarere Application-Ports und durch Entkopplung der UI von Infrastructure-Implementierungen.

Aktuelle Wartbarkeitsnote: **B / B-**
Realistisches Ziel nach 1-2 fokussierten Wochen: **A-**
A oder A+ waere ein groesseres Architekturprogramm, weil dann UI-Infrastructure-Entkopplung, Tooling, Teststruktur und KI-Pipeline-Grenzen konsequent gezogen werden muessen.

## Belege und Messpunkte

### Codeumfang

| Bereich | Dateien | Zeilen |
|---|---:|---:|
| `src/AuswertungPro.Next.Domain` | 38 | 2.218 |
| `src/AuswertungPro.Next.Application` | 197 | 18.747 |
| `src/AuswertungPro.Next.Infrastructure` | 264 | 44.793 |
| `src/AuswertungPro.Next.UI` | 1.094 | 88.072 |
| `tests` | 1.139 | 108.822 |
| `tools` | 76 | 14.136 |
| `sidecar` | 49 | 3.442 |

Weitere Messwerte:

- 2.860 untersuchte Code-/XAML-/Python-Dateien.
- 68 Dateien ueber 500 Zeilen.
- 20 Dateien ueber 900 Zeilen.
- `tests/AuswertungPro.Next.UI.Tests/UiArchitectureGuardTests.cs` allein: 8.549 Zeilen.
- Vulnerability-Scan fuer `AuswertungPro.sln`: keine bekannten anfaelligen Pakete.

### Groesste Wartbarkeits-Hotspots

| Datei | Zeilen | Bewertung |
|---|---:|---|
| `tests/AuswertungPro.Next.UI.Tests/UiArchitectureGuardTests.cs` | 8.549 | Sehr wertvoll, aber selbst zu gross und brittel durch String-Guards |
| `tools/CadasterDbReader/Program.cs` | 1.464 | Tool-God-Program, nicht kritisch fuer App-Laufzeit, aber schwer wartbar |
| `src/AuswertungPro.Next.Infrastructure/HoldingFolderDistributor.PdfParsing.cs` | 1.448 | Parser-Komplexitaet hoch, fachlich zentral |
| `src/AuswertungPro.Next.UI/ViewModels/Windows/TrainingCenterViewModel.cs` | 1.336 | UI-State, KI-/KB-Workflow, Infrastructure-Erzeugung gemischt |
| `src/AuswertungPro.Next.UI/Views/Windows/PhotoMeasurementWindow.xaml.cs` | 1.300 | Code-behind enthaelt Interaktion, Geometrie, Rendering, Zustand |
| `src/AuswertungPro.Next.Infrastructure/HoldingFolderDistributor.cs` | 1.172 | Breite Distributions-Fassade mit vielen Modi |
| `src/AuswertungPro.Next.UI/Services/SystemMonitorService.cs` | 1.137 | Monitoring/Parsing/State in einer Service-Klasse |
| `src/AuswertungPro.Next.Infrastructure/Import/WinCan/WinCanDbImportService.cs` | 1.076 | Import-Mapping und DB-Leselogik dicht gekoppelt |
| `src/AuswertungPro.Next.UI/Views/Pages/DataPage.xaml.cs` | 1.074 | Weiterhin Code-behind-Hotspot trotz Controller-Extraktionen |
| `src/AuswertungPro.Next.UI/ViewModels/Windows/CostCalculatorViewModel.cs` | 1.056 | Kosten-Domain und UI-State noch stark vermischt |
| `src/AuswertungPro.Next.Application/Reports/ProtocolPdfExporter.cs` | 1.019 | PDF-Erzeugung ist fachlich breit; Extraktion in Sections waere sinnvoll |
| `src/AuswertungPro.Next.UI/Views/Windows/VsaCodeExplorerWindow.xaml.cs` | 1.005 | UI-Code-behind mit Such-/Anzeige-/Foto-Logik |
| `src/AuswertungPro.Next.UI/ViewModels/Pages/ImportPageViewModel.cs` | 924 | Workflow-VM mit direkter Infrastructure-Verdrahtung |
| `src/AuswertungPro.Next.UI/ViewModels/Pages/DataPageViewModel.cs` | 872 | Deutlich verbessert, aber noch zentraler Koordinator |

## Architektur-Scorecard

| Kriterium | Status | Urteil |
|---|---|---|
| Schichtenmodell | Domain/Application/Infrastructure/UI existiert und ist im Kern eingehalten | Gut |
| Domain-Reinheit | Domain hat keine sichtbaren Abhaengigkeiten nach oben | Gut |
| Application-Grenze | Keine echte Compile-Abhaengigkeit auf UI/Infrastructure; nur Doku-Verweise und `InternalsVisibleTo` | Gut |
| UI-Abhaengigkeiten | UI referenziert Infrastructure direkt an vielen Stellen | Mittel bis kritisch |
| Composition Root | Vorhanden, aber `ServiceProvider` ist selbst zu breit | Mittel |
| Tests | Sehr breite Suite und viele Architektur-Guards | Stark |
| Teststruktur | Guard-Tests teils monolithisch und string-brittle | Mittel |
| Import/Verteilung | Fachlich gut abgesichert, aber Workflow-Orchestratoren bleiben breit | Mittel |
| Pfad-/Projektroot-Regeln | Stark verbessert, aber Reststellen mit `GetDirectoryName` bleiben | Kritisch fuer Portabilitaet |
| KI-/Training-Pipeline | Viele Controller/Services, aber UI-TrainingCenter baut noch Infrastructure direkt | Mittel bis kritisch |
| Tools | Viele Tools, nicht alle gleich sauber in Solution/Dependency-Scan eingebunden | Mittel |

## Findings

### P0 - Projektpfad-Aufloesung ist noch nicht vollstaendig konsistent

**Befund:** Das neue Projektlayout legt `projekt.json` unter `Projektdateien\` ab. Relative Projektpfade muessen deshalb gegen den Projekt-Root aufgeloest werden, nicht gegen `Path.GetDirectoryName(lastProjectPath)`. Das Handoff dokumentiert diese Regel korrekt. Trotzdem gibt es noch Reststellen:

- `src/AuswertungPro.Next.UI/Views/Windows/BeobachtungenWindow.xaml.cs:136`
- `src/AuswertungPro.Next.UI/Views/Pages/SchaechtePage.xaml.cs:1017`
- `src/AuswertungPro.Next.UI/Ai/CodingProtocolPdfExportPlanner.cs:26`
- Fallbacks in `ProtocolObservationsWindow.xaml.cs:351` und `ProtocolEntryEditorDialog.xaml.cs:833`

**Risiko:** Medien, Fotos oder Protokolle werden bei portablen Projekten, neuem Projektordner-Layout oder `Projektdateien\projekt.json` falsch gesucht.

**Empfehlung:** Ein kleiner, testgetriebener Slice:

- Einheitlichen Resolver nutzen: `ProjectFileLocator.ProjectRootFromFile(lastProjectPath)` oder vorhandene `ShellViewModel.GetProjectFolder()`.
- Architektur-Guard ergaenzen: UI darf `Path.GetDirectoryName(LastProjectPath)` nicht mehr fuer Projektpfade nutzen.
- Spezifische Tests fuer `BeobachtungenWindow`/`SchaechtePage`/`CodingProtocolPdfExportPlanner`.

**Aufwand:** 0,5-1 Tag.
**Nutzen:** Hoch, weil es direkte Nutzerfunktionen betrifft.

### P0 - UI haengt zu breit an konkreten Infrastructure-Klassen

**Befund:** `src/AuswertungPro.Next.UI` enthaelt viele direkte `using AuswertungPro.Next.Infrastructure...`-Referenzen und konkrete `new`-Aufrufe. Beispiele:

- `ServiceProvider.cs:16-29`, `108-202`: zentrale direkte Erzeugung vieler Infrastructure-Services.
- `TrainingCenterViewModel.cs:20-25`, `726`, `1202`, `1233`, `1255`, `1277`, `1511`: UI-VM erzeugt KnowledgeBase-/Ollama-/Pipeline-Infrastruktur.
- `ImportPageViewModel.cs:7-8`, `458`, `507`, `596`, `636`: Import-VM erzeugt Distribution/Portability/PhotoAssignment/WinCan direkt.
- `DataPageViewModel.cs:17-19`, `205`: UI-VM haengt noch an Media/Cost-Infrastructure.
- `BuilderPageViewModel.cs:17-18`, `CostCalculatorViewModel.cs:12-14`, `SanierungsMatrixPageViewModel.cs:11-12`: Kosten-/Output-/VSA-Infrastructure direkt in UI.

**Risiko:** UI-Tests bleiben schwer, Refactorings erzeugen breite Merge-Konflikte, und Domain-/Application-Vertraege werden umgangen.

**Empfehlung:** Schrittweise Application-Ports statt Big Bang:

- `IProjectImportWorkflow`, `IProtocolRegenerationWorkflow`, `ICostCatalogService`, `IMediaConflictWorkflow`, `ITrainingCenterRuntimeFactory`.
- Implementierungen bleiben in Infrastructure/UI-Composition, aber ViewModels sprechen gegen Interfaces aus Application oder UI-Abstractions.
- `ServiceProvider` bleibt vorerst Composition Root, wird aber schlanker.

**Aufwand:** 3-5 Tage fuer die wichtigsten UI-Pfade.
**Nutzen:** Sehr hoch. Das ist der groesste Hebel Richtung A-.

### P1 - TrainingCenterViewModel ist noch ein God-ViewModel

**Befund:** `TrainingCenterViewModel.cs` hat 1.336 Zeilen plus Partial-Dateien. Es verwaltet UI-State, Batch-Import, Self-Training, KnowledgeBase-Diagnostik, Review-Queue, Few-Shot, Weight-Learning und Infrastructure-Erzeugung.

Konkrete Symptome:

- Viele ObservableProperties und Collections in einer Klasse.
- Direkte Infrastructure-Imports (`KnowledgeBaseContext`, `EmbeddingService`, `RetrievalService`, Pipeline/Teacher/Training Services).
- Methoden erzeugen konkrete Runtime-Services statt gegen vorbereitete Use-Case-Services zu sprechen.

**Risiko:** Jede Erweiterung am Training Center kollidiert mit mehreren Verantwortungen. Fehler in KI-/KB-Workflows lassen sich schwer isolieren.

**Empfehlung:** Kein weiterer rein kosmetischer Split. Stattdessen Ownership nach Use-Case:

- `TrainingCenterDashboardState` fuer reine Anzeige.
- `TrainingCenterKnowledgeBaseController` fuer KB-Diagnostik/Stats.
- `TrainingCenterReviewQueueController` fuer Review/Approve/Reject.
- `TrainingCenterSelfTrainingUseCase` als Application-/Infrastructure-Service.
- `TrainingCenterRuntimeFactory` hinter Interface, damit VM nicht mehr `KnowledgeBaseContext`/`EmbeddingService` erzeugt.

**Aufwand:** 2-3 Tage fuer eine erste tragfaehige Entkopplung.
**Nutzen:** Hoch.

### P1 - PhotoMeasurementWindow ist Code-behind-dominiert

**Befund:** `PhotoMeasurementWindow.xaml.cs` hat 1.300 Zeilen. Es enthaelt Tool-Zustand, Letterbox-Koordinaten, Dragging, Undo, Rendering, Ergebnisbildung und UI-Eventlogik.

**Positiv:** Es gibt bereits Services wie `PhotoMeasurementGeometryService`; die Richtung stimmt.

**Risiko:** WPF-Code-behind ist schwer automatisiert zu testen. Geometry- und Rendering-Regressionen landen schnell in manueller QA.

**Empfehlung:** In drei kleinen Slices:

- `PhotoMeasurementInteractionController`: Tool-Auswahl, Dragging, Click-Point-State.
- `PhotoMeasurementOverlayRenderer`: Canvas-Element-Erzeugung aus neutralen Render-Commands.
- `PhotoMeasurementSessionState`: Undo/Redo, aktive Kalibrierung, Ergebnis.

**Aufwand:** 2 Tage.
**Nutzen:** Hoch fuer Stabilitaet der Foto-Messwerkzeuge.

### P1 - HoldingFolderDistributor bleibt zu breit

**Befund:** `HoldingFolderDistributor.cs` (1.172 Zeilen) und `HoldingFolderDistributor.PdfParsing.cs` (1.448 Zeilen) bilden eine statische Partial-Fassade fuer viele Modi: PDF, TXT, Schacht, Dichtheit, Video-Matching, PDF-Splitting, Record-Updates.

**Positiv:** Es gibt bereits Partial-Dateien und Tests, und die Logik ist fachlich zentral abgesichert.

**Risiko:** Neue Verteilvarianten erhoehen die Gefahr, dass eine bestehende Variante unabsichtlich mitveraendert wird.

**Empfehlung:** Nicht blind zerlegen. Erst explizite Strategien einfuehren:

- `IHoldingDistributionSource` fuer PDF/TXT/XTF/DB-Quelle.
- `HoldingDistributionPlan` als neutrale Zwischenstruktur.
- `HoldingDistributionExecutor` fuer Copy/Move/Unmatched/Record-Update.
- Bestehende statische API als Kompatibilitaets-Fassade belassen.

**Aufwand:** 3-4 Tage, nur testgetrieben.
**Nutzen:** Hoch, aber riskanter als UI-Slices.

### P1 - Import-Orchestrator ist fachlich sauberer, aber weiterhin workflow-monolithisch

**Befund:** `ProjectImportOrchestrator.cs:37` orchestriert acht Schritte in einer Methode: Struktur, Restore, Detection, Archiv, Parse, SIA405, Medien, Dirty-State. Das ist lesbar kommentiert und getestet, aber die Fehlerbehandlung ist breit (`try/catch` pro Schritt mit Message-Sammlung).

**Risiko:** Neue Importregeln veraendern schnell mehrere Stufen gleichzeitig. Integrationstests erkennen das, aber lokale Ursachenanalyse bleibt aufwendig.

**Empfehlung:** Pipeline-Step-Modell:

- `IProjectImportStep` mit `Execute(ProjectImportContext)`.
- Kontext enthaelt `Messages`, `Stats`, `Detection`, `ArchiveResult`.
- Orchestrator wird reine Step-Liste.
- Bestehende Integrationstests bleiben, Step-Tests kommen hinzu.

**Aufwand:** 1,5-2 Tage.
**Nutzen:** Mittel bis hoch.

### P1 - Dynamische Feldnamen sind flexibel, aber stringly-typed

**Befund:** `HaltungRecord` arbeitet mit `GetFieldValue("Feldname")`/`SetFieldValue(...)`. Das ist fuer Importformate flexibel und wurde durch den MergeEngine-Wurzelfix verbessert. Gleichzeitig entstehen viele magische Strings: `PDF_Path`, `PDF_Eigen`, `Schacht_oben`, `Link`, `Haltungsname`, usw.

**Risiko:** Tippfehler, uneinheitliche Feldbedeutung, schwer auffindbare Abhaengigkeiten. Neue Felder werden schnell "nebenbei" eingefuehrt.

**Empfehlung:**

- `ProjectFieldKeys` oder `HoldingFieldKeys` als zentrale Konstanten.
- Feld-Metadatenkatalog fuer dynamische Felder.
- Architektur-Guard: neue Kernfelder nicht als freie Stringliteral in UI/Infrastructure.

**Aufwand:** 1-2 Tage fuer die Kernfelder.
**Nutzen:** Hoch fuer Refactoring-Sicherheit.

### P2 - ServiceProvider ist Composition Root und Service Locator zugleich

**Befund:** `ServiceProvider.cs:43` implementiert `IServiceProvider`, aber wird in vielen UI-Klassen konkret injiziert. Das ist besser als globales `App.Services`, aber es bleibt ein breites Objekt mit vielen Properties und Factory-Methoden.

**Positiv:** `ArchitectureFitnessTests` verhindert neue direkte `App.Services`-Zugriffe ausserhalb erlaubter Stellen.

**Risiko:** ViewModels bekommen faktisch Zugriff auf "alles". Dadurch wandert Orchestrierung in UI statt in Use-Case-Services.

**Empfehlung:**

- Kurzfristig: pro ViewModel kleinere Dependency-Records, z.B. `ImportPageDependencies`, `DataPageDependencies`.
- Mittelfristig: Application-Interfaces im Konstruktor, `ServiceProvider` nur noch in Shell/Composition Root.
- Langfristig: optional echter DI-Container, aber erst nach Interface-Schnitt.

**Aufwand:** 2-4 Tage inkrementell.
**Nutzen:** Hoch.

### P2 - Architektur-Tests sind stark, aber selbst zu monolithisch

**Befund:** `UiArchitectureGuardTests.cs` hat 8.549 Zeilen und sehr viele String-basierte Guards. Das hat viele Regressionen verhindert, erzeugt aber eigene Wartbarkeitskosten.

**Risiko:** Guards werden bei legitimen Refactorings zum Reibungsverlust. String-Asserts koennen Implementierungen erzwingen statt Architekturrichtung.

**Empfehlung:**

- Nach Thema splitten: `PlayerArchitectureGuardTests`, `DataPageArchitectureGuardTests`, `ImportArchitectureGuardTests`, `TrainingArchitectureGuardTests`.
- Wo moeglich Roslyn-basierte Checks statt Stringsuche.
- Guard-Tests sollen Grenzen pruefen, nicht jede triviale Methode.

**Aufwand:** 1-2 Tage.
**Nutzen:** Mittel.

### P2 - Tools sind nuetzlich, aber als Architekturzone uneinheitlich

**Befund:** Viele Tools referenzieren `Infrastructure` direkt, einige sind grosse `Program.cs`-Dateien. Nicht alle Tool-Projekte erscheinen im Standard-Solution-Vulnerability-Scan. Beispiel: `tools/SewerStudioMcpServer/packages.lock.json` musste separat aktualisiert werden.

**Risiko:** Tooling driftet von App-Abhaengigkeiten weg; Sicherheits- oder API-Brueche werden spaeter entdeckt.

**Empfehlung:**

- `Tools.slnf` oder eigene `tools/AuswertungPro.Tools.sln`.
- Gemeinsamer Dependency-Scan fuer `src`, `tests`, `tools`.
- Fuer grosse Tools: `Program.cs` nur CLI-Parsing, Logik in `tools/<Tool>/Core`.

**Aufwand:** 1 Tag Basis, mehr fuer Refactoring.
**Nutzen:** Mittel.

### P2 - Python-Sidecar ist klein, aber Architekturvertrag sollte expliziter werden

**Befund:** `sidecar` ist mit 3.442 Zeilen klein. Das C#-System haengt aber fachlich stark an Sidecar-Verhalten fuer YOLO/DINO/SAM.

**Risiko:** API-/Modellveraenderungen brechen C#-Pipeline spaet. Wartbarkeit haengt an impliziten JSON-Vertraegen.

**Empfehlung:**

- OpenAPI/JSON-Schema fuer `/detect`, `/segment`, `/ground`.
- Contract Tests C# gegen gespeicherte Sidecar-Beispielantworten.
- Versioniertes `sidecar/api_contract.json`.

**Aufwand:** 1-2 Tage.
**Nutzen:** Mittel.

## Was bereits gut ist

- `Domain` ist klein und sauber isoliert.
- `Application` ist als Vertrags- und Logikschicht erkennbar und weitgehend frei von Infrastruktur.
- `Infrastructure` enthaelt die schweren Imports/Exports/Pipeline-Implementierungen, also grundsaetzlich an der richtigen Stelle.
- Viele ehemalige UI-God-Methoden sind bereits in Controller/Builder/Policy-Klassen extrahiert.
- Import/Medien/PDF/Rename haben mittlerweile viele Charakterisierungs- und Regressionstests.
- Architektur-Guard-Tests existieren und verhindern Rueckfaelle wie neue `App.Services`-Nutzung.
- Paket-Security-Scan ist aktuell sauber.

## Empfohlene Roadmap Richtung A-

### Phase 1 - Sofort, risikoarm (1-2 Tage)

1. Restliche Projektroot-Aufloesungen ersetzen.
2. Guard gegen `Path.GetDirectoryName(LastProjectPath)` in UI.
3. `PDF_Path`/`PDF_Eigen`/`Link`/`Haltungsname` in zentrale Field-Key-Konstanten ziehen.
4. `UiArchitectureGuardTests` mindestens in thematische Dateien splitten, ohne Verhalten zu aendern.

Ergebnis: weniger Portabilitaetsfehler, weniger String-Streuung, weniger Test-Wartungsschmerz.

### Phase 2 - UI-Infrastructure-Entkopplung (3-5 Tage)

1. `ImportPageViewModel`: direkte Infrastructure-Erzeugung hinter `IImportWorkflowFacade`.
2. `TrainingCenterViewModel`: KB-/Pipeline-Factories hinter `ITrainingCenterRuntime`.
3. `DataPageViewModel`: verbleibende Cost/Media-Infrastructure-Zugriffe in Dependencies kapseln.
4. `CostCalculatorViewModel` und `BuilderPageViewModel`: Output/Cost-Infrastructure hinter Application-Ports.

Ergebnis: UI ist testbarer und weniger merge-konflikttraechtig.

### Phase 3 - grosse Hotspots schneiden (4-7 Tage)

1. `PhotoMeasurementWindow.xaml.cs`: Interaction/State/Renderer trennen.
2. `TrainingCenterViewModel`: Dashboard, ReviewQueue, SelfTraining, KB-Diagnostik trennen.
3. `HoldingFolderDistributor`: Plan/Executor/Source-Strategien einfuehren, Fassade behalten.
4. `ProtocolPdfExporter`: Section-Builder oder Layout-Komponenten extrahieren.

Ergebnis: keine akuten God-Class-Risiken mehr in den Kernpfaden.

### Phase 4 - Architekturhaertung (laufend)

1. Roslyn-basierte Architekturtests fuer Layer-Regeln.
2. Tool-Projekte in Dependency-/Build-Scan aufnehmen.
3. Sidecar-API-Contracts versionieren.
4. Performance-/Smoke-Tests fuer Import, Verteilung, Rename, Protokoll-Regeneration.

## Priorisierte Backlog-Liste

| Prioritaet | Aufgabe | Erwarteter Effekt |
|---|---|---|
| P0 | Root-Pfad-Reststellen fixen | Verhindert Medien-/Protokollfehler in echten Projekten |
| P0 | UI direkte Infrastructure-New-Aufrufe reduzieren | Senkt Kopplung und Merge-Konflikte |
| P1 | TrainingCenterRuntime abstrahieren | KI-/KB-Workflows testbarer |
| P1 | PhotoMeasurement in Controller/Renderer/State teilen | Code-behind-Risiko runter |
| P1 | Field-Key-Konstanten einfuehren | Weniger Stringfehler |
| P1 | HoldingFolderDistributor Plan/Executor | Verteilung stabiler erweiterbar |
| P2 | Guard-Tests splitten | Tests wartbarer |
| P2 | Tool-Solution/Tool-Scan | Weniger Drift |
| P2 | Sidecar-Contract-Tests | KI-Pipeline stabiler |

## Schlussbewertung

Die Architektur ist nicht kaputt. Sie ist ein funktionierendes Schichtenmodell mit starker Testkultur, aber mit einer noch zu schweren UI-Schicht und zu vielen direkten Infrastructure-Abhaengigkeiten in ViewModels und Windows. Der naechste Qualitaetssprung entsteht durch Entkopplung, nicht durch mehr kleine Helper-Klassen.

Wenn die P0- und P1-Punkte umgesetzt sind, ist **A- realistisch**: keine gefaehrlichen God-Classes in Hauptpfaden, klare Projektpfadregel, weniger UI-Infrastructure-Kopplung, und Guards, die Architekturgrenzen pruefen statt Implementierungsdetails festzunageln.
