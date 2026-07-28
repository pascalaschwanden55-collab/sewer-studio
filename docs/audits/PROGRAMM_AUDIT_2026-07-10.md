# Programm-Audit SewerStudio / AuswertungPro.Next

**Prüfdatum:** 10. Juli 2026  
**Geprüfter Stand:** `master` / Commit `119a2f1300fdcd49acca3977a432bd728e16d16d`  
**Prüfart:** statische Repository-, Architektur- und Codeanalyse  
**Prüfumfang:** Architektur, Code-Struktur, KI-Pipeline, Daten- und Funktionsrobustheit, Tests, Betrieb sowie fehlende bzw. sinnvolle Funktionen

> Wichtige Abgrenzung: Dieser Audit basiert auf dem Quellcode, den Projektdateien, Tests und vorhandenen Diagnoseunterlagen. Es wurde in dieser Prüfung kein Windows-Build ausgeführt, kein reales Video verarbeitet und kein Ollama-/YOLO-/DINO-/SAM-Benchmark gegen das lokale Eval-Set gestartet. Aussagen zur Modellgüte sind deshalb Architektur- und Implementierungsbefunde, keine neu gemessenen Accuracy-Werte.

---

## 1. Gesamturteil

SewerStudio ist kein Skeleton mehr, sondern eine fachlich umfangreiche Anwendung mit Projektverwaltung, mehreren Importpfaden, Video- und Protokollfunktionen, VSA-Auswertung, Kosten-/Sanierungsfunktionen und einer anspruchsvollen lokalen KI-Pipeline. Positiv sind insbesondere die vorhandene Schichtentrennung, atomare Projekt- und Settings-Speicherung, zahlreiche fachliche Tests, strukturierte KI-Ausgaben, Telemetrieansätze sowie bereits angelegte Komponenten für Quality Gate, Review Queue, Feedback, Modellregister und Eval-Set-Hashing.

Die grösste Schwäche ist nicht das Fehlen von KI-Bausteinen, sondern deren **unvollständige Verdrahtung im Produktionspfad**. Mehrere fortgeschrittene Komponenten existieren, wirken aber zur Laufzeit nicht oder werden von einfacheren UI-Regeln umgangen. Gleichzeitig liegen Datenintegrität, Lebenszyklusverwaltung und zentrale Workflows noch zu stark in grossen ViewModels beziehungsweise Code-behind-Dateien.

### Reifegrad nach Prüfgebiet

| Prüfgebiet | Einschätzung | Kernaussage |
|---|---:|---|
| Fachlicher Funktionsumfang | 7/10 | Sehr breit, mehrere produktive Arbeitsabläufe vorhanden |
| Grundarchitektur | 6/10 | Vier Schichten vorhanden, Grenzen aber häufig durchbrochen |
| Code-Struktur/Wartbarkeit | 4/10 | Mehrere God Classes, Service Locator, UI und Infrastruktur vermischt |
| Datenintegrität/Robustheit | 5/10 | Gute atomare Writes, aber unvollständiger Dirty-/Recovery-/Migrationspfad |
| KI-Orchestrierung | 5/10 | Technisch ambitioniert, jedoch inkonsistente Evidenz- und Fallbackpfade |
| KI-Lern- und Qualitätskreislauf | 3/10 | Feedback wird geloggt, Gewichtslernen und KB-Rückführung greifen praktisch nicht |
| Tests/Qualitätssicherung | 5/10 | Gute Unit-Test-Basis, zentrale Integrations- und Regressionsgates fehlen |
| Betrieb/Sicherheit | 4/10 | Logging und Diagnostik vorhanden, Lifecycle, Secret-Schutz und CI fehlen |

**Gesamtstatus:** bedingt produktionsreif. Für kontrollierte interne Nutzung geeignet; vor unbeaufsichtigter KI-Autoakzeptanz, grösseren Datenmigrationen oder hochkritischen Produktionsläufen sind die P0- und P1-Punkte zu beheben.

---

## 2. Die wichtigsten Befunde

## P0 – vor weiterer Automatisierung beheben

### AI-01 – Der selbstlernende Feedback-Zyklus ist praktisch wirkungslos

**Beobachtung**

`CodingFeedbackRecorder` öffnet für jede Benutzerentscheidung eine neue SQLite-Verbindung und erzeugt jeweils einen neuen `ValidationLogger`, `WeightLearningService` und `FeedbackIngestionService`. Der `FeedbackIngestionService` zählt Feedback nur in einem Instanzfeld und startet das Re-Learning alle 25 Aufrufe. Da die Instanz nach einem einzigen Aufruf verworfen wird, erreicht der Zähler 25 nie. Zusätzlich wird kein `IKnowledgeBaseSampleIndexer` übergeben; akzeptierte Korrekturen werden daher nicht in die Wissensbasis zurückgeführt.

**Auswirkung**

- ValidationLog wächst, aber das angekündigte automatische Re-Learning läuft nicht.
- Menschlich bestätigte Fälle verbessern die Retrieval-Wissensbasis nicht.
- Die UI vermittelt einen selbstlernenden Kreislauf, der technisch nicht geschlossen ist.

**Massnahme**

- Einen langlebigen `ICodingFeedbackPipeline` als Singleton/Application-Service registrieren.
- Trigger nicht über flüchtigen RAM-Zähler, sondern über persistente, noch nicht verarbeitete Feedback-Events steuern.
- KB-Sample-Indexer verpflichtend verdrahten.
- Idempotente Event-ID und Verarbeitungsstatus einführen.
- Fehler nicht nur schlucken; Retry-/Dead-Letter-Status und Diagnoseanzeige ergänzen.

**Akzeptanzkriterium**

Ein Integrationstest schreibt 25 neue Entscheidungen, weist genau einen Re-Learn-Lauf nach, indexiert akzeptierte Korrekturen und verarbeitet nach einem Neustart keine bereits erledigten Events doppelt.

### AI-02 – Gelernte Quality-Gate-Gewichte werden nicht in den aktiven Quality Gate geladen

**Beobachtung**

`WeightLearningService` speichert `CategoryWeights` in SQLite und kann sie mit `LoadAllWeights()` lesen. Im Produktionscode wurde jedoch kein Aufruf gefunden, der diese Gewichte lädt und über `QualityGateService.SetWeights()` in den aktiven Gate-Service einspielt. `SetWeights()` wird ausserhalb von Tests nicht verwendet. Auch `ModelRegistryService` ist im Produktpfad nicht verdrahtet.

**Auswirkung**

Selbst ein manuell gestartetes oder später repariertes Re-Learning verändert die Entscheidungen des laufenden Systems nicht. Gespeicherte Gewichte und Modellversionen sind faktisch tote Konfiguration.

**Massnahme**

- `IQualityGateProvider` mit versioniertem, atomar austauschbarem Snapshot einführen.
- Gewichte beim Start laden und nach erfolgreichem Lernen transaktional aktivieren.
- Vor Aktivierung Offline-Eval, Kalibrierungsprüfung und Mindestverbesserung verlangen.
- Aktive Gewichts-/Modellversion in jeder KI-Entscheidung protokollieren.

### AI-03 – Auto-Akzeptanz umgeht den strengeren AutoApprovalService

**Beobachtung**

Der vorhandene `AutoApprovalService` verlangt Green im Quality Gate, mindestens 0,92 Confidence, KB-Code-Übereinstimmung und begrenzte epistemische Unsicherheit. Im Codier-ViewModel wird der Status jedoch direkt anhand von `AiContext.Confidence` bestimmt: ab 0,85 gilt ein Befund als `AutoAccepted`. Für diesen Pfad wurde keine Verwendung des `AutoApprovalService` gefunden.

**Auswirkung**

- Ein einzelner, möglicherweise unkalibrierter Confidence-Wert kann fachlich als auto-akzeptiert erscheinen.
- KB-Agreement, Evidenzvollständigkeit und Unsicherheit werden in der UI-Entscheidung ignoriert.
- Zwei parallele Freigabelogiken können zu widersprüchlichem Verhalten führen.

**Massnahme**

Nur einen zentralen `IAiDecisionPolicy` verwenden. Die UI darf Status und Farben lediglich aus dessen Ergebnis darstellen. Fast Mode muss denselben Policy-Pfad wie Batch- und Pipeline-Autoapproval benutzen.

### AI-04 – LLM-Fehler kann ungeprüften KB-Code erzeugen

**Beobachtung**

Wenn der LLM-Aufruf in `FullProtocolGenerationService.MapDetectionAsync` fehlschlägt und KB-Beispiele vorhanden sind, wird der Top-KB-Code sofort als `MappedProtocolEntry` zurückgegeben. Dieser Early Return umgeht Plausibilitätsprüfung, Evidence-Anreicherung, Quality Gate und Unsicherheitsschätzung. Nur der spätere Fallback bei einer formal ungültigen LLM-Antwort durchläuft den normalen Gate-Pfad.

**Auswirkung**

Ein Infrastrukturfehler verändert nicht nur die Verfügbarkeit, sondern kann einen fachlichen Code mit bis zu 0,85 Confidence erzeugen, ohne den sonst vorgesehenen Sicherheitsmechanismus.

**Massnahme**

Alle Pfade – auch LLM-Ausfall, KB-only und Vision-Hint – müssen über dieselbe Mapping-, Plausibilitäts-, Quality-Gate- und Provenance-Funktion laufen. KB-only sollte standardmässig `ReviewRequired` sein, bis ein separat kalibrierter KB-only-Grenzwert nachgewiesen ist.

### DATA-01 – Ungespeicherte Änderungen sind bei „Neu“ und „Öffnen“ nicht zentral geschützt

**Beobachtung**

Der Dirty-Dialog wird beim Schliessen des Hauptfensters geprüft. `ShellViewModel.NewProject()` und `TryOpenProject()` ersetzen das aktuelle Projekt dagegen ohne zentralen Unsaved-Changes-Guard. Zudem setzt `TrySaveProjectAs()` den neuen Pfad in den Settings und markiert das Projekt als bereit, bevor der eigentliche Save erfolgreich war.

**Auswirkung**

- Nutzer können Änderungen durch „Neu“ oder „Öffnen“ verlieren.
- Nach einem fehlgeschlagenen „Speichern unter“ kann die Anwendung auf einen Pfad zeigen, an dem kein gültiges Projekt gespeichert wurde.

**Massnahme**

Einen transaktionalen `IProjectSessionCoordinator` einführen:

1. Dirty-Entscheidung für Close, New, Open, Recent und Drag-and-drop zentral ausführen.
2. Zielpfad erst nach erfolgreichem Save committen.
3. Bei Fehler den vorherigen Session-/Pfadzustand vollständig erhalten.
4. Save-Operationen serialisieren und in der UI als laufende Operation anzeigen.

### DATA-02 – KI-Protokoll verletzt Herkunfts- und Revisionsintegrität

**Beobachtung**

KI-generierte `ProtocolEntry`-Objekte erhalten `Source = Manual`, obwohl der Enum-Wert `Ai` vorhanden ist. Beim Aufbau eines neuen Dokuments zeigen `Original` und `Current` auf dieselbe mutable `ProtocolRevision`-Instanz.

**Auswirkung**

- Herkunft und Haftungs-/Reviewpfad sind falsch ausgewiesen.
- Änderungen an `Current` können den vermeintlich unveränderlichen Originalstand mitverändern.
- Auditierbarkeit und Rückvergleich sind nicht zuverlässig.

**Massnahme**

- KI-Einträge mit `ProtocolEntrySource.Ai` speichern.
- `Original` und `Current` als getrennte Deep Copies erzeugen.
- `RequestedBy`, Modell-, Prompt-, Katalog-, KB- und Policy-Version in der Revision/AiMeta erfassen.
- Mutation von Originalrevisionen technisch verhindern, beispielsweise durch immutable Snapshots.

---

## P1 – hohe Priorität

### AI-05 – Die Evidenz für das Quality Gate ist unvollständig und teilweise falsch semantisiert

Im Multi-Model-Pfad wird `YoloConf` auf 1,0 gesetzt, sobald ein Frame als relevant gilt, statt die reale Detektionswahrscheinlichkeit zu verwenden. `SamMaskStability` und `QwenVisionConf` bleiben im gezeigten Pfad `null`, obwohl Kommentare eine spätere Befüllung ankündigen. Tracks aggregieren Signale per Maximum, wodurch längere Beobachtungen zu systematisch optimistischeren Werten tendieren können. Zusätzlich leitet `RawVideoDetection.Confidence` Confidence aus dem Schadens-**Schweregrad** (`high`, `mid`, `low`) ab. Severity und epistemische Sicherheit sind fachlich verschiedene Grössen.

**Massnahmen**

- Evidenzvertrag pro Modell definieren: kalibrierte Probability, Logit/Score, Modellversion, Timestamp, MissingReason.
- Severity vollständig von Confidence trennen.
- YOLO: tatsächliche Box-/Class-Confidence verwenden; bei mehreren Frames robuste Aggregation wie Median/quantilbasierte Fusion statt Maximum prüfen.
- SAM: echte Maskenstabilität/IoU erfassen oder das Signal aus Gewichtung entfernen.
- Qwen: Confidence nicht aus Severity ableiten; kalibrierten Code-Score oder explizit `unknown` verwenden.
- Gate darf bei fehlenden Pflichtsignalen nicht Green werden.

### AI-06 – Das Quality Gate kann bei zu wenig Evidenz Green liefern

Nullable Signale werden entfernt und die restlichen Gewichte vollständig renormalisiert. Ein vorhandener Test bestätigt ausdrücklich, dass nur LLM-Confidence 0,90 und Plausibilität 0,85 zu einer hohen Gesamtconfidence führen. Es gibt keine Mindestzahl an unabhängigen Quellen, keine Pflichtsignale und keine Missingness-Strafe.

**Massnahmen**

- Mindest-Evidenzprofil je Pipeline-Modus definieren.
- Green nur bei mindestens zwei unabhängigen Modellfamilien plus Plausibilität und – falls verfügbar – KB-Agreement.
- Fehlende Signale nicht vollständig weg-normalisieren; Coverage-/Missingness-Penalty einführen.
- Thresholds je Modus und Kategorie kalibrieren und versionieren.

### AI-07 – Feedback „Akzeptiert mit Bearbeitung“ wird zu früh erfasst

`EditDefect()` setzt die Entscheidung und startet Feedback-Aufzeichnung, bevor das Editorfenster die Korrektur abgeschlossen hat. Der Mapper verwendet den zu diesem Zeitpunkt in `Entry.Code` stehenden Code als FinalCode. Damit kann als Korrektur der alte KI-Code geloggt werden. Der Fire-and-forget-Aufruf besitzt zudem keinen benutzerseitigen Abschluss-/Retry-Status.

**Massnahme**

Feedback erst nach erfolgreichem Editor-Commit mit einem unveränderlichen `CodingDecisionEvent` aufzeichnen. Abbruch des Editors darf kein AcceptedWithEdit-Feedback erzeugen.

### AI-08 – Vorhandene Modell- und Review-Komponenten sind nur teilweise integriert

`ModelRegistryService`, `AutoApprovalService` und Teile der Review Queue sind implementiert und getestet, im zentralen Pipelinepfad aber nicht konsistent angeschlossen. Die Review Queue ist in-memory und daher nach Neustart leer. Modell-Rollback gibt nur Gewichte zurück; eine transaktionale Aktivierung oder Verbindung zum Quality Gate fehlt.

**Massnahme**

Ein zusammenhängendes `AiGovernance`-Modul schaffen:

- persistente Review Queue
- Modell-/Gewichtssnapshot
- Offline-Eval und Promotion Gate
- Canary-/Shadow-Modus
- Rollback mit aktiver Versionsumschaltung
- vollständige Entscheidungshistorie

### AI-09 – Pipeline-Degradation ist zu still und schwer reproduzierbar

Im Auto-Modus kann die Sidecar-Pipeline bei Fehlern auf Ollama-only zurückfallen. Dieser Moduswechsel muss im Ergebnis, in der Revision und in der UI eindeutig sichtbar sein. Retrieval-Fehler werden an mehreren Stellen als leere Trefferliste behandelt. Dadurch ist ein Lauf formal erfolgreich, obwohl er ohne zentrale Evidenzquelle gearbeitet hat.

**Massnahmen**

- `PipelineExecutionManifest` pro Lauf: angeforderter/effektiver Modus, Modelle, Versionen, Thresholds, Fallback-Gründe, Laufzeit und Artefakthashes.
- Degraded Runs in UI und Export markieren.
- Circuit Breaker und begrenzte Retries mit Backoff für Sidecar/Ollama.
- Fehlerbudgets und Rate der Degradierungen im Diagnosebereich anzeigen.

### AI-10 – Performancepotenzial: Mapping und Kontextaufbau sind unnötig teuer

Detektionen werden sequenziell einzeln per LLM gemappt. Der komplette Allowed-Code-Katalog wird in jeden Prompt geschrieben. Ohne injizierten Retrieval-Service baut `FullProtocolGenerationService` einen eigenen KB-Kontext und Embedder auf. Die Retrieval-Anfrage enthält Haltungs-ID und absolute Meterwerte; diese Attribute können semantische Ähnlichkeit über verschiedene Haltungen verschlechtern. Meterdistanz wird auch zwischen unabhängigen Haltungen als Rankinggewicht verwendet.

**Massnahmen**

- Erst hierarchisch Kandidaten auf wenige Haupt-/Untercodes reduzieren, dann constrained mapping.
- Mehrere ähnliche Detektionen in einem strukturierten Batch mappen oder mit begrenzter Parallelität verarbeiten.
- Prompt- und Retrieval-Caches anhand kanonischer Befundmerkmale verwenden.
- Haltungs-ID aus semantischem Query entfernen; Meter nur als lokales Kontextsignal, nicht globales KB-Ranking verwenden.
- Shared Retrieval/DB/HttpClient über DI injizieren und sauber disposen.
- Adaptive Frame-Sampling anhand Bewegung, Szenenwechsel, OSD und Pipelineunsicherheit prüfen.

### ARCH-01 – Schichten sind vorhanden, aber Abhängigkeiten laufen zu häufig quer

Die Solution besitzt Domain, Application, Infrastructure und UI. Dennoch referenziert die UI alle Schichten und instanziert in ViewModels/Code-behind zahlreiche konkrete Infrastructure-Klassen. Die Application-Schicht bindet QuestPDF direkt. UI und Infrastructure referenzieren teilweise dieselben technischen Pakete. `Microsoft.Data.Sqlite` liegt mit unterschiedlichen Major-Versionen in Infrastructure und UI; Infrastructure nutzt zusätzlich `System.Data.SQLite.Core`.

**Risiken**

- schwierige Austauschbarkeit und Testbarkeit
- unnötig grosser Deployment-/Native-Abhängigkeitsraum
- Version-/Binding-Konflikte
- unklare Zuständigkeit für Reporting, Datenzugriff und Browserautomation

**Zielbild**

- Domain: reine fachliche Typen und Invarianten
- Application: Use Cases und Ports, keine PDF-/DB-/WPF-Engine
- Infrastructure: Adapter für Dateien, SQLite, Ollama, Sidecar, PDF, Browser
- UI: Views/ViewModels plus ein schmaler Composition Root
- eigenständige Module `Ai.Orchestration`, `Ai.Evaluation`, `Project.Persistence`

### ARCH-02 – Eigener Service Locator statt kontrolliertem Dependency-Lifecycle

`ServiceProvider` ist ein grosser manueller Container und gleichzeitig Pfadauflöser, Katalogbuilder, AI-Factory und Infrastruktur-Owner. ViewModels greifen global über `App.Services` darauf zu. Ein HttpClient und KnowledgeBaseContext werden erzeugt, aber der Provider implementiert kein `IDisposable`; `App.OnExit` flusht nur Settings. Zusätzlich wird der VSA-Resolver global statisch konfiguriert.

**Massnahmen**

- `Microsoft.Extensions.Hosting`/DI als Composition Root verwenden.
- Scoped/Singleton/Transient-Lebenszeiten explizit definieren.
- `IHostedService`/`IAsyncDisposable` für Monitor, Datenbank, HttpClientFactory, Logger und Hintergrundjobs.
- Statische globale Katalogkonfiguration entfernen.

### ARCH-03 – DataPage ist eine God Class und besitzt Lifecycle-Leaks

`DataPageViewModel` bündelt Tabellenbearbeitung, Dropdownverwaltung, Autosave, Video, KI, Kosten, PDF, Hydraulik, Media-Suche, Training und Modellstatus. Es registriert Timer und eine anonyme Subscription auf `ShellViewModel.PropertyChanged`, implementiert aber kein `IDisposable`. Der Shell-Navigator erzeugt bei Navigation neue Page/ViewModel-Instanzen. Dadurch können alte ViewModels über Event-Handler erreichbar bleiben und ihre Timer weiterleben.

**Massnahmen**

- Zerlegen in `HoldingGrid`, `ProjectSave`, `Media`, `Protocol`, `Measures`, `Hydraulics`, `AiPipeline` Use Cases/ViewModels.
- Page/ViewModel-Caching oder sauberes Dispose beim Navigationswechsel.
- Weak events nur als Zwischenlösung; bevorzugt expliziter Lifecycle.
- UI-Code-behind auf View-Verhalten begrenzen.

### ROB-01 – Globaler UI-Exception-Handler setzt jeden Dispatcherfehler auf „Handled“

Die Anwendung zeigt einen Fehler an und läuft nach beliebigen unbehandelten UI-Ausnahmen weiter. Bei unbekanntem Zustand kann dies Folgefehler oder stille Datenkorruption verursachen.

**Massnahme**

Fehlerklassen trennen: bekannte recoverable UI-Fehler dürfen behandelt werden; unbekannte Invariant-/State-Ausnahmen müssen kontrolliert speichern/recovern und die betroffene Session oder Anwendung beenden.

### ROB-02 – Autosave und manuelles Speichern können die UI blockieren

Im Defaultmodus `OnEachChange` ruft `ScheduleAutoSave()` sofort den synchronen Dateisave auf dem UI-Thread auf. Der manuelle Save iteriert zusätzlich über alle Haltungen, lernt Massnahmen und kann ein Modelltraining synchron auslösen.

**Massnahmen**

- debounce-basierter asynchroner Save Worker mit Serialisierung und Cancellation
- Dirty-Snapshot statt Liveobjekt schreiben
- Training nie an den Save-Button koppeln; als Hintergrundjob mit Status und expliziter Version ausführen
- Last-write-wins-Rennen durch monotonen Save-Sequence-Wert verhindern

### ROB-03 – Projektformat besitzt Versionsfeld, aber keinen formalen Migrations- und Validierungspfad

`Project.Version` ist vorhanden, `Load()` deserialisiert jedoch direkt und ruft nur Default-/Legacy-Hilfen auf. ImportHistory und Conflicts sind untypisierte `JsonObject`-Listen. Nullable ist in Domain/Application/Infrastructure nicht aktiviert. Fehlende Feldmetadaten erhalten beim Laden `DateTime.UtcNow`, wodurch historische Provenienz verändert wird. Eine `.bak` wird geschrieben, aber beim Laden nicht automatisch validiert oder als Recovery angeboten.

**Massnahmen**

- versionierte DTOs und explizite `IProjectMigration`-Kette
- JSON-Schema-/Invariantvalidierung vor Domain-Materialisierung
- typisierte Import-/Conflict-Records
- Nullable in allen Projekten aktivieren
- Recovery-Assistent für `.bak`, Restore Points und korrupte Hauptdatei
- migrationsbedingte Default-Zeitstempel als `Unknown/ImportedLegacy`, nicht als aktuelle Bearbeitung

### ROB-04 – Settings-/Secret-Verwaltung braucht Härtung

Der Sidecar-Token wird als normales Feld der lokalen Settings gespeichert. Die Settings-Speicherung selbst ist positiv atomar und besitzt Backup/Quarantäne, aber Secrets sollten nicht als Klartext-JSON behandelt werden. AI-Zahlenwerte und URLs werden nur begrenzt auf fachlich sinnvolle Bereiche validiert.

**Massnahmen**

- Windows DPAPI/Credential Manager für Tokens
- zentraler Settings Validator mit Min/Max, URI-Schema, Timeout-Grenzen und Modellnamenprüfung
- redacted Export/Diagnosepaket

### QA-01 – Tests prüfen Komponenten, aber nicht die kritischen Systemkreisläufe

Es existieren drei Testprojekte und zahlreiche gute Unit Tests. Nicht abgedeckt sind unter anderem:

- 25 Feedbacks → genau ein Re-Learn → Gewichte laden → Gate-Verhalten ändert sich
- AcceptedWithEdit erst nach Editor-Commit
- KB-Indexierung akzeptierter Korrekturen
- alle Fallbackpfade laufen durch Quality Gate
- Auto-/Fast-Mode verwendet zentrale Approval Policy
- Original und Current sind getrennte Snapshots
- Dirty Guard für New/Open/Drag-and-drop
- Save-As-Rollback bei IO-Fehler
- Navigation erzeugt keine Timer-/Event-Leaks
- Projektmigration, Nullfelder, beschädigte JSON-Datei und `.bak`-Recovery
- kompletter Windows-Publish-/Startup-Smoke-Test

In der Repository-Suche wurde keine GitHub-Actions-Konfiguration mit `actions/setup-dotnet` gefunden. Build und Tests sind damit nicht als verpflichtendes Pull-Request-Gate erkennbar.

### DOC-01 – Dokumentation und Source-Encoding sind inkonsistent

`README.md` bezeichnet die Anwendung noch als Skeleton und beschreibt Teile als Platzhalter. `ARCHITECTURE.md` dokumentiert überwiegend die alte PowerShell-Struktur und verweist auf eine aktuelle Programmprüfung, die unter dem genannten Pfad nicht gefunden wurde. Mehrere aktuelle C#-Dateien enthalten sichtbare Mojibake-Sequenzen. In UI-Text ist das störend; in LLM-Prompts kann es die Modellqualität direkt verschlechtern.

**Massnahmen**

- UTF-8 ohne inkonsistente Re-Encoding-Schritte erzwingen (`.editorconfig`, CI-Encoding-Scan)
- aktuelle C4-/Container-/Komponentensicht dokumentieren
- ADRs für Projektformat, AI Quality Gate, Modellpromotion, Fallback-Policy und Datenprovenienz
- README auf realen Funktionsstand, Setup, Dependencies und Recovery aktualisieren

---

## 3. KI-Pipeline: Optimierungsprüfung

## 3.1 Was bereits gut ist

- strukturierte JSON-Schemas für Vision-/LLM-Ausgaben
- deterministische Ollama-Optionen und per-Frame-Timeout
- YOLO → DINO → SAM → Qwen-Orchestrierung mit Telemetrie
- klassenspezifische YOLO-Schwellen
- Frame-Streaming statt vollständigem Laden des Videos
- Dedup-/Tracking-Logik über mehrere Frames
- Katalogvalidierung für Code-Hints
- Eval-Tool mit Exact/Main/Group, Nullrate, Per-Code-Auswertung und Confusion Matrix
- Hash-Tool für eingefrorene Eval-Artefakte
- Presence-only-YOLO-Metrik wird korrekt als Health- und nicht als fachlicher Qualitätsbeweis gekennzeichnet

## 3.2 Optimierungsrangfolge

### 1. Qualitätssignale reparieren, bevor Thresholds getunt werden

Ein Threshold-Tuning auf den aktuell teilweise konstanten oder fehlenden Signalen würde Scheingenauigkeit erzeugen. Zuerst müssen reale, kalibrierbare Evidenzen und klare Missingness erfasst werden.

### 2. Eine einzige Decision Policy erzwingen

Mapping, Fallback, Batch, Live-Coding, Fast Mode und Review Queue müssen denselben Policy-Service verwenden. Alle Entscheidungen benötigen einen reproduzierbaren Decision Manifest.

### 3. Benchmark zu einem echten Release Gate ausbauen

Der aktuelle Benchmark schreibt gute Einzelartefakte. Es fehlen jedoch ein automatisch verifizierter Eval-Hash, eine versionierte Baseline-/Zeitreihe und harte Nicht-Regressionsgrenzen.

Empfohlene Gate-Metriken:

- Exact-, Main- und Group-Accuracy
- Null-/Error-Rate
- Recall je wichtige VSA-Hauptgruppe und Negativklasse
- Macro-F1 und class-balanced score
- Calibration: ECE/Brier Score
- Autoapproval Precision – wichtigste Sicherheitsmetrik
- Review-Rate und Coverage
- P50/P95-Latenz, VRAM, Queue Wait
- Degraded-/Fallback-Rate

### 4. Router-/Kandidatenarchitektur vollenden

Die Diagnose vom Mai zeigt, dass ein achtklassiger YOLO-Klassifikator nicht die detaillierten VSA-Zielcodes abdecken kann. YOLO sollte primär Routing/Presence übernehmen. Die Feinklassifikation muss kataloggeführt über Vision, Regeln, Retrieval und constrained LLM erfolgen. Leere Frames, Rohranfang/-ende, Wasserstand, Telemetrie und nicht codierbare Zustände brauchen explizite Klassen.

### 5. Kosten senken

- statischen Systemprompt und Katalogkandidaten cachen
- nur Top-N fachlich passende Codes an das LLM senden
- ähnliche Tracks vor Mapping clustern
- bounded concurrency statt vollständig sequenzieller Einzelaufrufe
- Qwen nur bei unzureichender lokaler Evidenz oder periodischem Audit-Sampling aufrufen
- Sidecar Batch-Endpunkte für mehrere Frames/Boxes prüfen

## 3.3 Vorgeschlagener AI-Entscheidungsvertrag

Jede KI-Entscheidung sollte mindestens enthalten:

```text
DecisionId
RunId
HaltungId / DetectionId
RequestedMode / EffectiveMode / FallbackReason
ModelVersions (YOLO, DINO, SAM, Vision, Text, Embedding)
PromptVersion / CatalogVersion / KBVersion / WeightVersion
RawScores + calibratedScores + MissingReasons
SuggestedCode / CandidateSet
QualityGateResult / DecisionPolicyResult
Uncertainty / ReviewRequired
UserDecision / FinalCode / DecisionTimestamp
InputArtefactHashes
```

Damit werden Reproduktion, Benchmarking, Haftung, Feedback und Rollback möglich.

---

## 4. Zielarchitektur

```text
SewerStudio.Desktop
  Views + kleine ViewModels
  Composition Root
        |
        v
SewerStudio.Application
  ProjectSession use cases
  Import/Export use cases
  Protocol use cases
  AI decision/review use cases
  Ports / Commands / Events
        |
        +-------------------+
        v                   v
SewerStudio.Domain      SewerStudio.Ai.Contracts
  Project aggregate       Evidence/Decision manifests
  typed Holding data       Model/Eval contracts
  Protocol revisions       Review/Feedback events
        |                   |
        +---------+---------+
                  v
SewerStudio.Infrastructure
  Project JSON + migrations/recovery
  SQLite repositories
  Ollama/Sidecar adapters
  PDF/Excel/Playwright/Files
  Background job store
```

### Zentrale neue Services

- `IProjectSessionCoordinator`
- `IProjectMigrationPipeline`
- `IBackgroundJobCoordinator`
- `IAiDecisionPolicy`
- `IAiFeedbackProcessor`
- `IModelPromotionService`
- `IAiRunManifestStore`
- `IHealthReadinessService`

---

## 5. Sinnvolle fehlende oder unvollständige Funktionen

### Unbedingt sinnvoll

1. **Recovery Center**  
   Zeigt Hauptdatei, `.bak`, Restore Points und Autosave-Snapshots; validiert und vergleicht sie vor Wiederherstellung.

2. **Unsaved-Changes- und Session-Manager**  
   Einheitlicher Save/Discard/Cancel-Dialog für Schliessen, Neu, Öffnen, Recent, Drag-and-drop und Projektwechsel.

3. **KI-Entscheidungsinspektor**  
   Für jeden Befund: Bild/Frame, alle Evidenzen, Kandidaten, Modellversionen, Fallback, Quality-Gate-Begründung und Userentscheidung.

4. **Persistente Review Queue**  
   Filter nach Unsicherheit, Kategorie, Projekt, Modellversion und Fehlerart; Batch-Aktionen nur mit Audittrail.

5. **Health-/Readiness-Seite**  
   Prüft Ollama, Modelle, Sidecar, GPU, FFmpeg/ffprobe, pdftotext, Chromium, Katalog, KB-Embeddingmodell, Schreibrechte und freien Speicher. Zeigt klar den effektiven Degraded Mode.

6. **Hintergrundjob-Center**  
   Import, Videoanalyse, PDF/Excel-Export, KB-Rebuild und Training mit Pause/Abbruch, Fortschritt, Retry und Wiederaufnahme nach Neustart.

7. **Undo/Redo und Änderungsjournal**  
   Besonders für Tabellen-Bulkedit, Import-Merge, Reihenfolge, Protokolle und Massnahmen.

8. **Import Dry Run / Diff Center**  
   Vor Commit: neue/geänderte/konfliktäre Felder, Quellen, Medienzuordnung und erwartete Dateibewegungen anzeigen.

9. **Projekt-Integritätsprüfung**  
   Doppelte Haltungen, fehlende Medien/PDFs, verwaiste Dateien, ungültige DN/Längen, widersprüchliche Protokollrevisionen und Hashabweichungen.

10. **Modellpromotion mit Shadow/Canary**  
    Neues Modell zunächst nur parallel bewerten; Promotion erst nach Eval- und Autoapproval-Precision-Gate.

### Danach sinnvoll

- Vergleich zweier Projekt-/Protokollrevisionen
- Batch-Reparatur für Medienlinks mit Vorschau
- lokale Diagnosepakete mit redigierten Secrets
- Feature Flags und Safe Mode ohne KI/Browser/Hardwaremonitor
- Katalog-/Regelversionsanzeige in Exporten
- Qualitätsdashboard nach Code, Gemeinde, Kamera, Material und Modellversion
- Datensatz-Balance-/Coverage-Assistent für das Training Center
- reproduzierbarer Export eines Review-Falls für Entwickler

---

## 6. Umsetzungsplan

## Phase 0 – Sicherheitsstopp und Messbasis, 2–4 Tage

- Fast-/Auto-Accept nur über zentrale Policy; bis dahin standardmässig deaktivieren
- LLM-Fehler-Fallback durch Quality Gate leiten
- AI Source und Original/Current-Alias korrigieren
- Dirty Guard und Save-As-Rollback implementieren
- CI-Basis für Build/Test/Encoding anlegen

## Phase 1 – Lernkreislauf reparieren, 4–7 Tage

- langlebige Feedback-Pipeline
- persistenter Outbox-/Processed-Status
- KB-Indexer verdrahten
- Gewichtlernen auslösen, laden und aktivieren
- AcceptedWithEdit nach Commit erfassen
- End-to-End-Integrationstests

## Phase 2 – Evidenz und Governance, 1–2 Wochen

- Evidence Contract überarbeiten
- Severity/Confidence trennen
- Missingness Policy und Mindest-Evidenz
- Decision Manifest und persistente Review Queue
- Modellregister an Promotion/Rollback anschliessen
- AI-Inspector-UI

## Phase 3 – Architektur und Robustheit, 2–4 Wochen inkrementell

- ProjectSessionCoordinator und Background Jobs
- DataPage zerlegen und Lifecycle bereinigen
- DI/Hosting einführen
- Projektmigration/Recovery Center
- Paket- und SQLite-Provider konsolidieren

## Phase 4 – Performance und Modelloptimierung, fortlaufend

- Kandidatenreduktion, Batching, Caching, adaptive Frames
- eval-basierte Thresholds und Kalibrierung
- Router-Datensatz vervollständigen
- Shadow/Canary und Baseline-Zeitreihe

---

## 7. Empfohlene Testmatrix

| Ebene | Pflichtprüfungen |
|---|---|
| Domain | Invarianten, Revision-Snapshots, Migrationen, typed fields |
| Persistence | Roundtrip, null/corrupt/truncated JSON, atomic save, backup recovery, concurrent saves |
| Application | New/Open/Save/Discard-Use-Cases, Import-Diff, Job-Idempotenz |
| AI Unit | Evidence-Missingness, Fallback-Policy, calibration, prompt candidate constraints |
| AI Integration | 25 Feedbacks, KB indexing, relearn/load/apply, restart, duplicate event |
| AI Regression | frozen hashes, baseline, per-code metrics, autoapproval precision, latency |
| UI | Navigation lifecycle, Dirty dialogs, edit commit/abort, drag reorder persistence |
| System | Windows x64 publish, first start, missing dependencies, degraded mode, recovery |

---

## 8. Positive Befunde

- Projekt- und Settings-Dateien werden über Temp-Datei und Replace/Backup geschrieben.
- Korrupte Settings werden quarantänisiert.
- Mehrere fachlich getrennte Testprojekte existieren.
- Vision-LLM nutzt ein strukturiertes JSON-Schema und deterministische Optionen.
- Multi-Model-Pipeline besitzt Frame-/Phasentelemetrie und Trace-Ansätze.
- Code-Hints können gegen den aktiven VSA-Katalog validiert werden.
- Eval-Auswertung enthält Per-Code- und Confusion-Matrix-Artefakte.
- Eval-Set-Hashing ist implementiert.
- Der YOLO-Presence-Benchmark kennzeichnet selbst, dass er kein fachlicher Qualitätsbeweis ist.
- Der strengere `AutoApprovalService` ist konzeptionell eine gute Basis – er muss nur zum einzigen produktiven Freigabepfad werden.

---

## 9. Definition of Done für „robust produktionsreif“

SewerStudio sollte erst dann als robust produktionsreif für KI-gestützte Autoentscheidungen gelten, wenn:

1. kein Code ohne einheitliche Decision Policy und Quality Gate gespeichert oder auto-akzeptiert wird;
2. jede Entscheidung reproduzierbare Modell-/Prompt-/Katalog-/KB-/Weight-Versionen besitzt;
3. Feedback nachweisbar KB und Gate verbessert und nach Neustart korrekt fortgesetzt wird;
4. Autoapproval Precision auf einem eingefrorenen, gehashten Eval-Set ein vereinbartes Sicherheitsziel erreicht;
5. New/Open/Save/Recovery keine ungespeicherten Daten verlieren;
6. Build, Tests, Encoding und AI-Regressionschecks als CI-Gates laufen;
7. Hintergrundjobs abbrechbar, wiederaufnehmbar und diagnostizierbar sind;
8. unbekannte globale Ausnahmen nicht in einem inkonsistenten Zustand weiterlaufen;
9. Originalrevisionen unveränderlich und Herkunftsdaten korrekt sind;
10. der produktive Pfad keine unverdrahteten Parallelmechanismen für Approval, Review oder Modellgewichte mehr besitzt.

---

## 10. Direkt empfohlene nächste Umsetzung

Als erstes zusammenhängendes Arbeitspaket empfehle ich **„AI Safety & Data Integrity Sprint“**:

- AI-01 bis AI-07
- DATA-01 und DATA-02
- vier End-to-End-Tests für Feedback, Fallback, Autoapproval und Projektwechsel
- kleine Diagnoseanzeige für effektiven Pipeline-Modus und aktive Weight-Version

Dieses Paket reduziert gleichzeitig fachliches Fehlentscheidungsrisiko, Datenverlustgefahr und die Diskrepanz zwischen vorhandener KI-Architektur und tatsächlichem Laufzeitverhalten.
