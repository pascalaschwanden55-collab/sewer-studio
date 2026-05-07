# Sicherheits-, Architektur- und Fehleraudit

Stand: 2026-04-25

Projekt: AuswertungPro / Sewer-Studio KI 4.2

## Kurzfazit

Der Code ist funktional weit fortgeschritten und hat bereits einige gute Schutzmechanismen: zentrale Pfad-Sanitization in `ProjectPathResolver`, per-file Import-Fehlerbehandlung in Kernservices, robuste Video-Matching-Logik, Layer-Guard-Tests und mehrere bereits verbesserte Process-Runner-Stellen.

Die wichtigsten Risiken liegen aktuell nicht in einem einzelnen Bug, sondern in vier Querschnittsthemen:

1. Projektdateien koennen an mehreren UI-Stellen relative oder absolute Pfade aufloesen, ohne sicherzustellen, dass diese im Projektordner bleiben.
2. Der Python-Sidecar hat administrative Endpunkte fuer Training, Export, Model-Reload und LoRA-Deploy ohne erkennbare Authentisierung.
3. Mehrere externe Prozesse lesen `stdout`/`stderr` synchron vor dem Timeout. Dadurch sind Timeouts teilweise wirkungslos und Deadlocks moeglich.
4. Architekturgrenzen sind formal vorhanden, aber AI-/Pipeline-Businesslogik liegt in grossem Umfang im UI-Projekt.

## Umfang und Methode

Geprueft wurden:

- C#/.NET Projektstruktur, Referenzen und kritische Services
- WPF/UI-Pfade fuer Datei-, PDF-, Video- und Dossieroperationen
- Python-Sidecar API, Model- und Training-Endpunkte
- externe Prozessaufrufe fuer PDF, OCR, ffmpeg, Python und WinCan-Konvertierung
- DI/Service-Lifetime, Ressourcenbesitz und Test-Reproduzierbarkeit
- Build und vorhandene Tests als Plausibilitaetscheck

Nicht vollstaendig abgedeckt:

- produktive Runtime-Konfiguration auf Zielsystemen
- externe Penetrationstests
- NuGet/Python Dependency-CVE-Scan
- Secret-Scanning auf historischen Git-Objekten

## Kritische Befunde

### C1 - Projektdateien koennen Pfade ausserhalb des Projektordners referenzieren

Bewertung: kritisch, wenn Projektdateien aus fremden Quellen geoeffnet werden.

Mehrere UI-Pfade kombinieren relative Werte mit dem Projektordner und akzeptieren absolute Werte, ohne eine Containment-Pruefung. Dadurch kann eine manipulierte Projektdatei lokale Dateien ausserhalb des Projekts referenzieren, in Exporte/Dossiers einbeziehen oder im Player/Viewer oeffnen.

Beispiele:

- `src/AuswertungPro.Next.UI/ViewModels/Pages/DataPageViewModel.cs:1435-1438`
  - `Path.GetFullPath(Path.Combine(projectDir, path))`
  - keine `StartsWith(projectDir)`/`relative_to`-Pruefung
- `src/AuswertungPro.Next.UI/ViewModels/Pages/DataPageViewModel.cs:2321-2328`
  - `ResolveDossierPhotoPath` akzeptiert absolute Pfade direkt
- `src/AuswertungPro.Next.UI/ViewModels/Pages/DataPageViewModel.cs:2348-2375`
  - `AddResolvedPdf` akzeptiert absolute PDF-Pfade und relative Pfade ohne Containment
- `src/AuswertungPro.Next.UI/ViewModels/Pages/DataPageViewModel.cs:2443-2460`
  - `ResolveExistingPath` gibt absolute existierende Pfade direkt zurueck

Positiv: Es existiert bereits eine bessere Implementierung in `src/AuswertungPro.Next.Application/Common/ProjectPathResolver.cs:33-38` und `:62-67`. Dort wird der aufgeloeste Pfad gegen den Projektordner geprueft. `SanitizePathSegment` in `ProjectPathResolver.cs:103-130` entschaaerft auch `.`/`..`.

Empfehlung:

- Alle UI-Pfadaufloesungen auf `ProjectPathResolver` oder einen neuen zentralen `ProjectFilePathPolicy` umstellen.
- Relative Pfade nur akzeptieren, wenn `fullPath.StartsWith(fullProjectRoot + separator, OrdinalIgnoreCase)`.
- Absolute Pfade aus Projektdateien standardmaessig ablehnen oder explizit als `ExternalFileReference` modellieren.
- Fuer Dossier-/PDF-Merge nur projektinterne Dateien zulassen, ausser der Nutzer waehlt eine externe Datei aktiv im aktuellen Dialog.
- Zusaetzliche Tests: `..\..\Windows\...`, UNC-Pfade, Laufwerkswechsel, Symlinks/Junctions, trailing dots/spaces.

### C2 - Sidecar-Endpunkte erlauben administrative Datei- und Modelloperationen ohne erkennbare Authentisierung

Bewertung: kritisch, falls der Sidecar jemals ausserhalb von localhost erreichbar ist oder lokale Malware/Browser-Skripte ihn ansprechen koennen.

Der FastAPI-Sidecar definiert nur eine VRAM-Middleware, aber keine Authentisierung/CSRF-/Token-Pruefung fuer administrative Endpunkte.

Beispiele:

- `sidecar/sidecar/main.py:166-190`
  - FastAPI-App und Middleware, aber kein Auth-Guard
- `sidecar/sidecar/routes/training.py:359-431`
  - `/training/export-yolo` schreibt in `req.output_dir`
- `sidecar/sidecar/models/yolo_wrapper.py:76-84`
  - absolute Modellpfade werden akzeptiert
- `sidecar/sidecar/models/yolo_wrapper.py:378-385`
  - `reload_model` laedt `.pt`/`.engine`, nur Existenz und Suffix werden geprueft
- `sidecar/sidecar/routes/lora_training.py:352-399`
  - LoRA deployt Adapter, schreibt Modelfile und spricht `req.ollama_base_url` an

Besonders relevant: `.pt`-Modelle sind bei PyTorch/Ultralytics keine untrusted Datenformate. Ein Reload von beliebigen lokalen `.pt`-Dateien ist als Codeausfuehrungsrisiko zu behandeln.

Empfehlung:

- Lokales Bearer-Token fuer alle Endpunkte ausser `/health` einfuehren. Token beim C#-Start generieren und per Header uebergeben.
- Sidecar nur an `127.0.0.1` binden, ausser ein expliziter Unsafe-Flag ist gesetzt.
- Administrative Endpunkte in Gruppen absichern: Training/Export/Reload/Deploy nur mit Token und optionaler Feature-Flag-Freigabe.
- Pfade strikt gegen erlaubte Roots pruefen: `Path.resolve()` plus `relative_to(allowed_root)` statt String-Prefix.
- `model_path` nur aus einer Model-Registry oder aus erlaubten Unterordnern akzeptieren.
- `ollama_base_url` nicht aus Requests akzeptieren oder auf konfigurierte localhost-Ziele beschraenken.
- `base_model` und `model_name` mit Regex validieren und CR/LF verbieten.

## Hohe Befunde

### H1 - Externe Prozesse koennen trotz Timeout haengen oder durch volle Pipes blockieren

Bewertung: hoch. Betrifft Stabilitaet, Importverarbeitung und Fehlerdiagnose.

Mehrere Prozessaufrufe lesen `StandardOutput.ReadToEnd()` oder `StandardError.ReadToEnd()` synchron vor `WaitForExit(timeout)`. Wenn ein Prozess haengt oder eine Pipe voll laeuft, greift der Timeout nicht verlaesslich.

Beispiele:

- `src/AuswertungPro.Next.Infrastructure/Import/Pdf/PdfOcrExtractor.cs:181-190`
  - liest stdout/stderr synchron vor `WaitForExit(timeoutMs)`
- `src/AuswertungPro.Next.Infrastructure/Import/Pdf/PdfTextExtractor.cs:85-104`
  - liest stderr, kein harter Timeout
- `src/AuswertungPro.Next.UI/Ai/Training/Services/PdfProtocolExtractor.cs:322-340`
  - `pdftotext` nutzt `Arguments`-String und liest stdout synchron vor Timeout
- `src/AuswertungPro.Next.UI/Ai/Training/Services/PdfProtocolExtractor.cs:730-740`
  - Kommentar sagt paralleles Lesen, Code liest stdout dennoch synchron
- `src/AuswertungPro.Next.Infrastructure/Import/WinCan/SdfToSqliteConverter.cs:92-151`
  - PowerShell/Python-Konverter ohne robustes Timeout-/Pipe-Pattern
- `src/AuswertungPro.Next.UI/Ai/VideoFrameExtractor.cs:31-66`
  - ffmpeg stdout/stderr redirectet, aber vor `WaitForExit` nicht gedraint

Positiv: Gute Patterns existieren bereits, z.B. in `InspectionFrameExtractor` und AI-Videoanalyse-Services, die stdout/stderr parallel lesen.

Empfehlung:

- Einen zentralen `ProcessRunner.RunAsync(...)` einfuehren.
- Immer `ArgumentList` statt `Arguments` verwenden.
- stdout/stderr direkt nach Prozessstart asynchron lesen.
- Timeout mit `WaitForExitAsync` plus `Task.WhenAny` implementieren.
- Bei Timeout `Kill(entireProcessTree: true)`, danach Reader-Tasks awaiten.
- ExitCode, stderr-Auszug, Dauer und Command-Name als strukturiertes Result zurueckgeben.

### H2 - AI-/Pipeline-Businesslogik liegt massiv im UI-Projekt

Bewertung: hoch fuer Wartbarkeit, Testbarkeit und langfristige Architektur.

Das UI-Projekt enthaelt sehr viele AI-/Pipeline-Services, darunter Aggregation, Quality Gates, Batch-Pipeline, Training, Knowledge Base und Modellorchestrierung. Im Audit wurden 137 C#-Dateien unter `src/AuswertungPro.Next.UI/Ai` gefunden, gegenueber 9 unter `Application/Ai` und 1 unter `Infrastructure/Ai`.

Der vorhandene Layer-Guard beschreibt eine 4-Layer-Architektur, importiert aber selbst UI-AI-Klassen:

- `tests/AuswertungPro.Next.Pipeline.Tests/ArchitectureLayerGuardTests.cs:6-7`
- `tests/AuswertungPro.Next.Pipeline.Tests/ArchitectureLayerGuardTests.cs:12-21`

Empfehlung:

- Neues Projekt oder klare Module fuer AI-Kernlogik schaffen, z.B. `AuswertungPro.Next.Ai` oder Verschiebung nach `Application`/`Infrastructure`.
- Zuerst pure, stateless Klassen verschieben: `DetectionAggregator`, `QualityGateService`, `VsaCodeResolver`, Pipeline-Konfiguration, VideoProbe/Frame-Auswahl.
- UI soll nur ViewModels, Dialoge, Bindings und Visualisierung enthalten.
- Architecture-Tests erweitern: keine Namespaces wie `*.UI.Ai.QualityGate`, `*.UI.Ai.Pipeline`, `*.UI.Ai.Training.Services` fuer Businesslogik.

### H3 - ServiceProvider besitzt Ressourcen, entsorgt sie aber nicht zentral

Bewertung: hoch bis mittel. Betrifft Leaks, DB-Kontexte, HttpClient und Sidecar-Lebensdauer.

`ServiceProvider` ist ein eigener DI-Container, aber implementiert kein `IDisposable`/`IAsyncDisposable`.

- `src/AuswertungPro.Next.UI/ServiceProvider.cs:48`
- `src/AuswertungPro.Next.UI/ServiceProvider.cs:226-234`

Beim Aufbau von Knowledge-Base-Komponenten werden `HttpClient`, `KnowledgeBaseContext`, `EmbeddingService` und `RetrievalService` erzeugt. Wenn `CheckModelConsistency()` wirft, werden lokale Ressourcen nicht sichtbar entsorgt. Bei Erfolg bleibt unklar, wer die Lebensdauer beendet.

Empfehlung:

- `ServiceProvider : IServiceProvider, IDisposable` oder `IAsyncDisposable`.
- Besitzverhaeltnisse dokumentieren: wer erzeugt, entsorgt.
- Konstruktion mit Owner-Transfer-Pattern:
  - lokale Ressourcen erzeugen
  - bei Fehler im catch/finally entsorgen
  - erst nach erfolgreichem Aufbau in Felder uebernehmen
- `App.OnExit` muss den Provider geordnet entsorgen.

### H4 - Sidecar-Start nutzt zusammengesetzten Arguments-String und hartes Killen

Bewertung: hoch bis mittel.

- `src/AuswertungPro.Next.UI/Ai/PythonSidecarService.cs:72-80`
  - `Arguments = "-m uvicorn ... --host " + _host + " --port " + _port`
- `src/AuswertungPro.Next.UI/Ai/PythonSidecarService.cs:190-194`
  - Stop killt den Prozessbaum direkt

Da `UseShellExecute=false` gesetzt ist, ist das keine klassische Shell-Injection. Trotzdem koennen ungepruefte Host-/Port-Strings zusaetzliche uvicorn-Argumente einschleusen. Das harte Killen verhindert zudem sauberen FastAPI-Lifespan-Shutdown.

Empfehlung:

- `ArgumentList` verwenden.
- `_host` als IP/Hostname validieren, `_port` als int 1..65535.
- Graceful shutdown einbauen: z.B. lokaler `/admin/shutdown`-Endpoint mit Token oder kontrollierter Signalweg, danach Kill-Fallback.

## Mittlere Befunde

### M1 - Service Locator in ViewModels und Pages

Bewertung: mittel.

Es gibt zahlreiche direkte `App.Services`-Zugriffe. Besonders fragil sind Feldinitialisierer in ViewModels, weil sie App-Startup-Reihenfolge und Tests koppeln.

Beispiele:

- `src/AuswertungPro.Next.UI/ViewModels/ShellViewModel.cs:13`
- `src/AuswertungPro.Next.UI/ViewModels/Pages/DataPageViewModel.cs:49`
- `src/AuswertungPro.Next.UI/ViewModels/Pages/BuilderPageViewModel.cs:35`
- `src/AuswertungPro.Next.UI/ViewModels/Pages/SchaechtePageViewModel.cs:18`

Empfehlung:

- Constructor Injection fuer ViewModels.
- Window/Page-Factories fuer WPF-Instanziierung.
- `App.Services` nur noch in Composition Root und Legacy-Bruecken.

### M2 - Domain-Modelle enthalten UI-Binding-Konzepte

Bewertung: mittel.

Domain-Klassen sind nicht komplett UI-neutral:

- `src/AuswertungPro.Next.Domain/Models/HaltungRecord.cs:3`
  - implementiert `INotifyPropertyChanged`
- `src/AuswertungPro.Next.Domain/Models/HaltungRecord.cs:92-95`
  - feuert Binding-Events
- `src/AuswertungPro.Next.Domain/Models/Project.cs`
  - nutzt `ObservableCollection`

Empfehlung:

- Domain-Modelle schrittweise zu POCOs/Records entkoppeln.
- UI-spezifische Observable-/Notify-Schicht in ViewModels oder Adapter legen.
- Migration zuerst bei neuen Modellen erzwingen, alte Modelle nicht sofort komplett umbauen.

### M3 - Sehr grosse Klassen erschweren Fehlerisolation

Bewertung: mittel.

Groesse einzelner Dateien:

- `PlayerWindow.xaml.cs`: 8273 Zeilen
- `HoldingFolderDistributor.cs`: 4007 Zeilen
- `DataPageViewModel.cs`: 2795 Zeilen
- `CodingModeWindow.xaml.cs`: 2679 Zeilen
- `ProtocolPdfExporter.cs`: 2443 Zeilen

Empfehlung:

- Erst nach Verantwortlichkeiten schneiden, nicht blind refactoren.
- `PlayerWindow` weiter in Partial-Dateien und Services aufteilen: VLC, Coding, Live-AI, Feedback, Snapshots, Tastatur/Commands.
- `HoldingFolderDistributor` in PDF-Parsing, Video-Matching, Dateioperationen, Result-Building, Textkorrektur splitten.
- Fuer jede Extraktion vorher kleine Regressionstests anlegen.

### M4 - Tests schreiben auf absolute Maschinenpfade

Bewertung: mittel.

Der Pipeline-Testlauf erreichte 430 erfolgreiche Tests, schlug aber in 3 Tests fehl, weil nach `C:\KI_BRAIN\...` geschrieben wurde:

- `tests/AuswertungPro.Next.Pipeline.Tests/QwenModelComparisonTest.cs:36`
- `tests/AuswertungPro.Next.Pipeline.Tests/QwenModelComparisonTest.cs:133`
- `tests/AuswertungPro.Next.Pipeline.Tests/QwenModelComparisonTest.cs:240`
- `tests/AuswertungPro.Next.Pipeline.Tests/SdfProfileExtractionTest.cs:26-32`

Empfehlung:

- Lange GPU-/Modellevaluationen mit Trait markieren, z.B. `[Trait("Category", "GpuEval")]`.
- Standard-`dotnet test` darf keine lokalen Spezialdaten oder absolute Schreibpfade brauchen.
- Output in `TestContext`/Temp-Verzeichnis oder konfigurierbares Workspace-Artefaktverzeichnis schreiben.
- `C:\KI_BRAIN` nur ueber Environment Variable erlauben und Tests bei fehlender Variable sauber skippen.

## Niedrigere Befunde und Hygiene

### L1 - Encoding/Mojibake in Kommentaren und Ausgaben

Mehrere Dateien zeigen mojibakeartige Zeichen in Kommentaren/Ausgaben. Funktional meist unkritisch, aber schlecht fuer Wartbarkeit und Logs.

Empfehlung:

- UTF-8 ohne BOM konsistent erzwingen.
- `.editorconfig` erweitern.
- Nur gezielte Korrektur in beruehrten Dateien, kein globaler Formatting-Noise.

### L2 - Sidecar-Path-Prefix-Pruefung ist prefix-bypassbar

`sidecar/sidecar/routes/lora_training.py:355-359` nutzt `str(adapter_path).startswith(str(allowed_root))`. Das kann bei Pfaden mit gleichem Prefix fehlschlagen, z.B. `sidecar_evil`.

Empfehlung:

- `adapter_path.relative_to(allowed_root)` verwenden und `ValueError` abfangen.

## Positive Befunde

- `ProjectPathResolver` hat bereits sinnvolle Path-Traversal-Schutzlogik.
- Haltungs-/Pfadsegmente werden an zentraler Stelle gegen `.`/`..` gehaertet.
- Infrastructure-Tests fuer Import/Distribution laufen erfolgreich.
- Build der Solution ist ohne Fehler und Warnungen durchgelaufen.
- Einige Process-Runner-Stellen verwenden bereits `ArgumentList` und paralleles Lesen.
- Layer-Guard-Tests existieren und sind ein guter Ansatz, muessen aber erweitert werden.
- Die Sidecar-Default-Konfiguration bindet an `127.0.0.1`.

## Priorisierte Verbesserungs-Roadmap

### Sofort, 0-1 Tag

1. Zentrale sichere Pfadauflosung fuer UI-Projektdateien verwenden.
2. Sidecar-Token fuer alle nicht-Health-Endpunkte einfuehren.
3. `deploy-lora` Prefix-Check auf `relative_to` umstellen.
4. Absolute Test-Schreibpfade in Pipeline-Tests entschaaerfen oder Tests als explizite lokale Eval-Tests markieren.

### Kurzfristig, 1 Woche

1. Zentralen `ProcessRunner` implementieren und PDF/OCR/ffmpeg/Python/WinCan-Aufrufe umstellen.
2. `PythonSidecarService` auf `ArgumentList`, Host-/Port-Validierung und Graceful Shutdown umstellen.
3. `ServiceProvider` entsorgbar machen und `App.OnExit` sauber verdrahten.
4. Architecture-Tests erweitern: AI-Businesslogik darf nicht im UI-Projekt wachsen.

### Mittelfristig, 2-4 Wochen

1. AI-Kernlogik aus `UI/Ai` in Application/Infrastructure oder eigenes AI-Projekt migrieren.
2. `App.Services`-Zugriffe in ViewModels schrittweise durch Constructor Injection ersetzen.
3. Domain-Modelle von UI-Binding-Konzepten entkoppeln.
4. God Classes nach stabilen Verantwortlichkeiten zerlegen.

## Verifikation

Ausgefuehrt:

- `dotnet build AuswertungPro.sln -v minimal`
  - Ergebnis: erfolgreich, 0 Warnungen, 0 Fehler
- `dotnet test tests/AuswertungPro.Next.Infrastructure.Tests/AuswertungPro.Next.Infrastructure.Tests.csproj -v minimal`
  - Ergebnis: 116 erfolgreich, 1 uebersprungen, 117 gesamt
- `dotnet test tests/AuswertungPro.Next.Pipeline.Tests/AuswertungPro.Next.Pipeline.Tests.csproj --no-build -v minimal`
  - Ergebnis: 430 erfolgreich, 3 fehlgeschlagen
  - Ursache: `UnauthorizedAccessException` beim Schreiben nach `C:\KI_BRAIN\...`

## Empfohlene erste Pull Requests

PR 1 - Sichere Projektpfade:

- `DataPageViewModel`, Dialoge und Window-Code auf zentrale Projektpfadpolicy umstellen.
- Tests fuer Traversal und absolute Pfade.

PR 2 - Sidecar Auth und Admin-Hardening:

- lokales Token, Header-Pruefung, sichere Path-Root-Checks.
- `reload_model`, `export-yolo`, `deploy-lora` einschraenken.

PR 3 - Zentraler ProcessRunner:

- neue Application/Infrastructure-Hilfsklasse.
- alle bekannten synchronen Process-Aufrufe ersetzen.

PR 4 - Test-Reproduzierbarkeit:

- GPU-/KI-Eval-Tests sauber kategorisieren.
- absolute Pfade durch Temp/Env-Konfiguration ersetzen.

