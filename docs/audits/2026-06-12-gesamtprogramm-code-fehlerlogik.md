# Audit 2026-06-12 - Gesamtprogramm, Codestruktur, Fehlerlogik

Stand: 2026-06-12  
Art: Read-only Audit, keine Codefixes in diesem Schritt  
Methode: 6 parallele Audit-Agenten plus lokale Gegenpruefung mit `rg`, Build und Tests

## Kurzfazit

Das Programm ist nicht "kaputt": Die Solution baut, grosse Testbereiche laufen gruen, und viele Kernpfade sind bereits besser abgesichert als frueher. Es gibt aber mehrere echte Risiken, die im Betrieb Datenverlust, falsche Medienzuordnung, falsche KI-Ergebnisse oder haengende Imports verursachen koennen.

Die groessten Probleme liegen nicht in Syntax oder Build, sondern in Laufzeitlogik:

- ungespeicherte Seitendaten koennen beim App-Schliessen verloren gehen,
- Medien/Videos koennen bei mehreren Kandidaten falsch zugeordnet werden,
- relative Pfade werden nicht ueberall containment-sicher aufgeloest,
- die KI-Pipeline kann valide Befunde durch ein schlechtes Qwen-/Bildqualitaetsurteil verwerfen,
- UI-gebundene Daten werden teilweise aus Worker-Threads veraendert,
- lokale Tools senden Tokens oder schreiben Dateien ohne genug Eingangsvalidierung.

## Gepruefte Basis

- `dotnet build AuswertungPro.sln -v minimal`: erfolgreich.
- Pipeline-Tests: 495/495 erfolgreich.
- UI-Tests: 354/354 erfolgreich.
- Infrastructure-Tests: sequentiell erfolgreich; ein paralleler Lauf erzeugte einen Build-Lock auf `Domain.dll`, danach einzeln erneut ohne Fehler.
- Solution enthaelt 17 Projekte, aber weitere Tools liegen ausserhalb der Solution.
- Code-/Config-Dateien grob: 988 (`*.cs`, `*.xaml`, `*.py`, `*.json`).

## Agenten-Schnitt

Es wurden sechs Auditbereiche getrennt geprueft:

1. Architektur und Codestruktur
2. Fehlerbehandlung, Logging, Persistenz
3. Import, Export, Mapping, Medienzuordnung
4. UI, MVVM, State, Player
5. KI, Videoanalyse, Self-Training, Sidecar
6. Tests, Security, Tooling

Die Punkte unten sind konsolidiert und nach Risiko sortiert. Wo ein Agentenbefund nur teilweise verifiziert wurde, ist das entsprechend vorsichtig formuliert.

## Top-Befunde

### K1 - App-Schliessen umgeht page-local Dirty-State

Risiko: Datenverlust, besonders bei Sanierungsmatrix/Kosten, weil nicht jeder ungespeicherte Zustand in `Project.Dirty` steckt.

Belege:

- `src/AuswertungPro.Next.UI/MainWindow.xaml.cs:18` bis `:46`: `MainWindow_Closing` prueft nur `vm.Project.Dirty`.
- `src/AuswertungPro.Next.UI/ViewModels/ShellViewModel.cs:145` bis `:146`: Navigation kennt `IConfirmLeave`.
- `src/AuswertungPro.Next.UI/ViewModels/Pages/SanierungsMatrixPageViewModel.cs:485`, `:928`: Seite implementiert `IConfirmLeave`.

Fix:

- In `MainWindow_Closing` zuerst `CurrentPage is IConfirmLeave guard && !guard.ConfirmLeave()` pruefen.
- Erst danach `Project.Dirty` speichern/abbrechen.
- Regressionstest fuer App-Close mit ungespeicherter Matrix/Kosten.

### K2 - KI-Gate kann valide DINO/SAM-Befunde verwerfen

Risiko: False Negatives. Ein Qwen-/Bildqualitaetsurteil kann Detektionen loeschen, obwohl DINO/SAM etwas gefunden haben.

Belege:

- `src/AuswertungPro.Next.Application/Ai/EnhancedVisionModels.cs:14` bis `:16`: `Empty()` setzt `ImageQuality` auf `"schlecht"`.
- `src/AuswertungPro.Next.Infrastructure/Ai/EnhancedVisionAnalysisService.cs:212`, `:217`, `:464`, `:469`: Timeouts/Exceptions geben `EnhancedFrameAnalysis.Empty(...)` zurueck.
- `src/AuswertungPro.Next.Infrastructure/Ai/Pipeline/MultiModelAnalysisService.cs:621`: `badQuality` basiert auf Qwen-ImageQuality.
- `src/AuswertungPro.Next.Infrastructure/Ai/Pipeline/MultiModelAnalysisService.cs:638` bis `:645`: bei `"schlecht"` wird `findings.Clear()` ausgefuehrt.

Fix:

- Qwen-Fehler/Timeout darf nicht als schlechte Bildqualitaet behandelt werden.
- Separates Feld/Status: `qwen_error`, `qwen_timeout`, `image_quality_bad`.
- DINO/SAM-Befunde behalten und nur als "ohne Qwen-Code" markieren.

### K3 - Import/VSA kann UI-gebundene Records aus Worker-Threads mutieren

Risiko: WPF Cross-Thread-Fehler, sporadische UI-Crashes, inkonsistente Bindings.

Belege:

- `src/AuswertungPro.Next.UI/ViewModels/Pages/ImportPageViewModel.cs:180`, `:527`, `:604`, `:619`: schwere Arbeit per `Task.Run`.
- `src/AuswertungPro.Next.Infrastructure/Import/Common/MergeEngine.cs:148`: `target.SetFieldValue(...)`.
- `src/AuswertungPro.Next.Domain/Models/HaltungRecord.cs:70` bis `:72`: `PropertyChanged` direkt auf dem aufrufenden Thread.
- `src/AuswertungPro.Next.UI/ViewModels/Pages/BuilderPageViewModel.cs:499`: UI reagiert auf Record-Events.

Fix:

- Worker produziert DTO-/ChangeSet-Ergebnis.
- Anwendung auf `ObservableCollection`/`HaltungRecord` nur auf UI-Dispatcher.
- Test/Stresslauf mit Import plus geoeffneter Builder-/Datenansicht.

### K4 - Medien-/Videomatching ist in mehreren Importpfaden zu optimistisch

Risiko: Falsches Video wird einer Haltung zugeordnet. Das ist fachlich kritisch, weil danach Auswertung, Training und Export auf falscher Evidenz basieren.

Belege:

- `src/AuswertungPro.Next.Infrastructure/HoldingDistribution/HoldingVideoMatching.cs:108` bis `:130`: Fallback "Haltung only" kann `Matched` liefern.
- `src/AuswertungPro.Next.Infrastructure/Import/Kins/KinsImportService.cs:500` bis `:504`: erste Video-Liste wird genommen.
- `src/AuswertungPro.Next.Infrastructure/Import/Ibak/IbakExportImportService.cs:222`, `:754`, `:755`: erster Match/Kandidat wird genommen.
- `src/AuswertungPro.Next.Infrastructure/Import/WinCan/WinCanDbImportService.cs:427`, `:430` bis `:432`, `:545`, `:666`: erste/ bevorzugte Kandidaten bei Mehrdeutigkeit.

Fix:

- Ein gemeinsames `MediaResolveResult`: `Matched`, `NotFound`, `Ambiguous`, `Unsafe`.
- Auto-Match nur bei exakt einem robusten Kriterium.
- Haltung-only nur Warnung/Review, nicht automatisch `Matched`.
- Ambiguous-Kandidaten sichtbar loggen und nicht still `list[0]` nehmen.

### K5 - Relative Medienpfade werden nicht ueberall root-sicher aufgeloest

Risiko: Dateien ausserhalb des Projektordners koennen gelesen/kopiert/exportiert werden, wenn ein relativer Pfad wie `..\..` in Projektdaten landet.

Belege:

- `src/AuswertungPro.Next.Application/Common/ProjectPathResolver.cs:33` bis `:36`: sichere Containment-Pruefung existiert.
- `src/AuswertungPro.Next.Application/Common/ProjectPathResolver.cs:94` bis `:95`: `IsRelative` prueft nur `!Path.IsPathRooted`.
- `src/AuswertungPro.Next.Infrastructure/Import/MediaDistributionService.cs:96` bis `:98`, `:162` bis `:164`, `:242` bis `:244`, `:299` bis `:301`: relative Pfade werden per `Path.Combine(projectFolder, raw)` aufgeloest.
- `src/AuswertungPro.Next.Application/Reports/HaltungsDossierPdfBuilder.cs:598`: eigener Combine-Pfad.

Fix:

- Nur noch `ProjectPathResolver.ResolveFilePath`/`ResolveDirectoryPath` fuer externe Projektpfade verwenden.
- `IsSafeRelativeProjectPath` verpflichtend vor Copy/Export.
- Tests fuer `..\`, absolute Pfade, UNC, Slash-Varianten.

### K6 - LiveControl-Client kann Token an fremde URLs senden

Risiko: Token-Leak/SSRF, wenn `live_control_url` aus Tool-/MCP-Input auf fremde Hosts zeigt.

Belege:

- `tools/SewerStudioMcpServer/LiveControlClient.cs:55`: `X-Live-Control-Token` wird an Request angehaengt.
- `tools/SewerStudioMcpServer/LiveControlClient.cs:67`, `:77`: `live_control_url` wird als URL ausgegeben/weitergetragen.
- `tools/SewerStudioMcpServer/LiveControlClient.cs:111` bis `:118`: URL wird aus freiem Base-URL-Input gebaut.

Fix:

- Nur `localhost`, `127.0.0.1`, `[::1]` erlauben.
- Token nur bei Loopback senden.
- Tests fuer externe Hosts, LAN-IP, `file:`, `http://evil`, IPv6.

### K7 - LibVLC/WPF-Airspace-Risiko bei Overlays

Risiko: Overlays werden unsichtbar, falsch klickbar oder liegen nicht wirklich ueber dem Video.

Belege:

- `src/AuswertungPro.Next.UI/Views/Windows/PlayerWindow.xaml:170`: `VideoView`.
- `src/AuswertungPro.Next.UI/Views/Windows/PlayerWindow.xaml:177` bis `:196`: `CodingOverlayPopup` ist airspace-freundlich.
- `src/AuswertungPro.Next.UI/Views/Windows/PlayerWindow.xaml:199` bis `:208`: `DetectionOverlayGrid`/`DetectionCanvas` als WPF-Sibling ueber Video.
- `src/AuswertungPro.Next.UI/Views/Windows/PlayerWindow.LiveDetection.cs:724`, `:760`, `:904`: Detection-Overlay zeichnet in dieses Canvas.

Fix:

- Alle Video-Overlays entweder als Popup/Adorner mit getesteter Airspace-Loesung oder als separates Layer-Fenster.
- Manueller UI-Test mit echter VLC-Wiedergabe, Vollbild, DPI-Skalierung, mehreren Monitoren.

### K8 - User-Kataloge koennen bei Ladefehlern leer werden und danach ueberschrieben werden

Risiko: Benutzerdefinierte Preise/Vorlagen verschwinden nach korruptem JSON oder Teil-Write.

Belege:

- `src/AuswertungPro.Next.Infrastructure/Costs/CostCatalogStore.cs:198` bis `:206`: bei Fehler leerer `CostCatalog`.
- `src/AuswertungPro.Next.Infrastructure/Costs/MeasureTemplateStore.cs:160` bis `:168`: bei Fehler leerer Katalog.
- `src/AuswertungPro.Next.Infrastructure/Costs/PositionTemplateStore.cs:82` bis `:89`: bei Fehler leerer Katalog.

Fix:

- LoadResult mit `Catalog`, `LoadError`, `SourcePath`.
- Save sperren/warnen, wenn User-Override nicht sauber geladen wurde.
- Atomare Writes mit `.bak` und Restore.

### K9 - TrainingCenter-Persistenz hat mehrere Verlust-/Race-Risiken

Risiko: Root-Folder, Reviews oder Trainingsstand gehen bei Parallel-Saves/Fehlern verloren.

Belege:

- `src/AuswertungPro.Next.UI/Ai/Training/TrainingCenterModels.cs:31`: `RootFolders` ist Teil des State.
- `src/AuswertungPro.Next.UI/ViewModels/Windows/TrainingCenterViewModel.cs:641` bis `:647`, `:659` bis `:665`: normale Saves enthalten `RootFolders`.
- `src/AuswertungPro.Next.UI/ViewModels/Windows/TrainingCenterViewModel.cs:1470`, `:1514`: weitere Save-Pfade bauen neuen `TrainingCenterState`; Agentenbefund: RootFolders dort nicht mitgefuehrt.
- `src/AuswertungPro.Next.UI/Ai/Training/TrainingCenterStore.cs:88`, `:105`: fixer `.tmp`-Pfad, `File.Move`.
- `src/AuswertungPro.Next.UI/Ai/Training/TrainingCenterStore.cs:107` bis `:110`: Save-Fehler werden weitgehend verschluckt.
- `src/AuswertungPro.Next.Infrastructure/Ai/SelfImproving/ReviewQueueService.cs:175`, `:207`: Persistenzfehler best-effort/still.

Fix:

- Zentrale `BuildState()`-Methode fuer jeden Save.
- `SemaphoreSlim` fuer Store-Saves.
- temp-Datei mit GUID plus `File.Replace`/Backup.
- Persistenzfehler sichtbar im UI/Log, nicht nur Debug/best-effort.

### K10 - Externe Prozesse und PDFs haben uneinheitliche Timeouts/Budgets

Risiko: Import/Analyse kann haengen oder sehr grosse PDFs koennen Laufzeit/Speicher sprengen.

Belege:

- `src/AuswertungPro.Next.Infrastructure/Import/Pdf/PdfTextExtractor.cs:103` bis `:104`: `ReadToEnd` plus `WaitForExit()` ohne Timeout.
- `src/AuswertungPro.Next.Infrastructure/Import/Pdf/PdfOcrExtractor.cs:185` bis `:187`: synchrones `ReadToEnd` vor `WaitForExit(timeoutMs)`, Timeout kommt zu spaet.
- `src/AuswertungPro.Next.Infrastructure/Import/Pdf/PdfTextExtractor.cs:129` bis `:132`: PdfPig liest alle Seiten ohne zentrale Groessen-/Seitenbudget-Pruefung.
- `src/AuswertungPro.Next.Infrastructure/Ai/Training/Services/PdfProtocolExtractor.cs:204`, `:276`, `:421`: ebenfalls PDF-Iteration ohne zentrale Policy.

Fix:

- Gemeinsamer `ProcessRunner`: async stdout/stderr, `WaitForExitAsync`, Timeout, Kill entire process tree.
- Zentrale PDF-Policy: maximale MB, maximale Seiten, CancellationToken, klares Result.

## Architektur und Codestruktur

### A1 - Globaler Service Locator

Belege:

- `src/AuswertungPro.Next.UI/App.xaml.cs:35`, `:42`, `:85`: statischer ServiceProvider.
- `src/AuswertungPro.Next.UI/ViewModels/Pages/DataPageViewModel.cs:50`: ViewModel zieht `App.Services`.
- `src/AuswertungPro.Next.UI/MainWindow.xaml.cs:56`: UI castet `App.Services` auf konkrete Implementierung.

Bewertung: Testbarkeit und Abhaengigkeiten sind schwer erkennbar. Das ist kein Sofort-Bug, aber es verlangsamt jede groessere Stabilisierung.

Fix-Richtung: neue ViewModels ueber Konstruktor-Injection bauen; `App.Services` nur noch am Composition Root.

### A2 - Layer-Grenzen sind weich

Belege:

- `src/AuswertungPro.Next.Application/AuswertungPro.Next.Application.csproj:9`: Application referenziert QuestPDF.
- `src/AuswertungPro.Next.Application/Reports/ProtocolPdfExporter.cs:10` bis `:12`: konkrete PDF-Bibliothek in Application.
- `src/AuswertungPro.Next.UI/AiInfrastructureGlobalUsings.cs:1`: UI globalisiert Infrastructure-AI.
- `src/AuswertungPro.Next.UI/ViewModels/Pages/DataPageViewModel.cs:19` bis `:22`: UI nutzt Infrastructure direkt.

Bewertung: Application ist nicht mehr reine Use-Case-Schicht. Das ist aktuell tolerierbar, sollte aber nicht weiter wachsen.

Fix-Richtung: Export/PDF/IO mittelfristig in Infrastructure verschieben oder ueber Ports kapseln.

### A3 - `ServiceProvider` mischt zu viele Aufgaben

Belege:

- `src/AuswertungPro.Next.UI/ServiceProvider.cs:91` bis `:103`: Import/Export/Services werden manuell konstruiert.
- `src/AuswertungPro.Next.UI/ServiceProvider.cs:111` bis `:134`: Katalogaufloesung und globale Resolver-Konfiguration.
- `src/AuswertungPro.Next.UI/ServiceProvider.cs:138` bis `:142`: Ollama/KnowledgeBase-Setup.
- `src/AuswertungPro.Next.UI/ServiceProvider.cs:140`: `KnowledgeBaseContext` wird erzeugt.
- `src/AuswertungPro.Next.Infrastructure/Ai/KnowledgeBase/KnowledgeBaseContext.cs:17`, `:36`: Context ist `IDisposable`.

Risiko: Lifetime/Dispose unklar, Startup schwer testbar.

Fix-Richtung: kleine Factory-/Registration-Methoden, klare Ownership fuer `IDisposable`.

### A4 - Grosse Hotspots

Beispiele:

- `PlayerWindow.Coding.cs`: ca. 4185 Zeilen.
- `CodingModeWindow.xaml.cs`: ca. 2613 Zeilen.
- `ProtocolPdfExporter.cs`: ca. 2464 Zeilen.
- `TrainingCenterViewModel.cs`: ca. 2059 Zeilen.
- `DataPage.xaml.cs`: ca. 1959 Zeilen.
- `HoldingFolderDistributor.PdfParsing.cs`: ca. 1652 Zeilen.

Bewertung: Diese Dateien sind nicht automatisch falsch, aber sie sind Fehler-Multiplikatoren. Neue Aenderungen dort brauchen kleine, gezielte Tests.

## Import, Export und Mapping

### I1 - Re-Import ist nicht sauber idempotent

Agentenbefund:

- KINS/IBAK/WinCan schieben vorhandene Daten bei Re-Import in History, ohne stabilen Fingerprint.

Risiko: Doppelte History und Drift bei wiederholtem Import derselben Quelle.

Fix:

- Pro Protokoll/Importrevision Hash/Fingerprint speichern.
- Gleiche Quelle + gleicher Inhalt nicht erneut historisieren.

### I2 - Per-Haltung-Fehlerbehandlung ist uneinheitlich

Agentenbefund:

- IBAK/WinCan haben grosse Try/Catch-Bloecke auf Importebene.
- XTF ist tendenziell besser pro Datei/Record isoliert.

Risiko: Ein kaputter Datensatz kann zu viel Importfortschritt blockieren oder nur grobe Fehler liefern.

Fix:

- Result-Records pro Haltung/Medium.
- Fehler enthalten Quelle, Haltung, Datei, Status, Aktion.

### I3 - Excel-Zahlenformat kann bei Schweizer/DE-Formaten kippen

Agentenbefund:

- `ExcelTemplateExportService` normalisiert Zahlen wie `1.234,56` nicht robust genug.

Risiko: Excel bekommt Text statt Zahl.

Fix:

- Culture-aware Decimal-Parser mit `de-CH`/`de-DE` plus invariant fallback.
- Tests fuer `1234.56`, `1'234.56`, `1.234,56`, `1234,56`.

## UI, MVVM und Player

### U1 - Dirty-State wird direkt gesetzt

Belege:

- `src/AuswertungPro.Next.UI/ViewModels/ShellViewModel.cs:479` bis `:485`: zentraler Weg existiert.
- Direkte Setzungen u.a. `DataPageViewModel.cs:360`, `:470`, `:534`; `SchaechtePageViewModel.cs:260`; `SanierungsMatrixPageViewModel.cs:1266`.

Risiko: Titel/Save-State/UI-Aktualisierung laufen auseinander.

Fix:

- Nur noch `ShellViewModel.MarkDirty(...)`.
- Direkte `Project.Dirty = true` verbieten oder per Analyzer/rg-Test abdecken.

### U2 - ViewModel-Lifecycle/Events brauchen harte Dispose-Regeln

Belege:

- `src/AuswertungPro.Next.UI/ViewModels/ShellViewModel.cs:154` bis `:155`: `CurrentPage` wird ersetzt.
- `src/AuswertungPro.Next.UI/ViewModels/Pages/BuilderPageViewModel.cs:122`, `:453`, `:459`, `:489`: Event-Subscriptions/Unsubscribe vorhanden, aber nur wenn Dispose/Detach sicher laeuft.
- `src/AuswertungPro.Next.UI/ViewModels/Pages/DataPageViewModel.cs` hat viele direkte Event-/State-Pfade.

Risiko: Leaks, doppelte Events, veraltete UI-Aktionen.

Fix:

- `CurrentPage` vor Ersetzen disposen, wenn `IDisposable`.
- Unit-/UI-Test fuer mehrfaches Navigieren Builder <-> Daten <-> Matrix.

### U3 - Rename-Fehler duerfen nicht still durchlaufen

Agentenbefund:

- Direkte Binding-/Rename-Pfade koennen bei Fehlern nur `return` machen, ohne Edit sauber abzubrechen.

Risiko: UI zeigt alten/neuen Zustand uneindeutig.

Fix:

- Rename als Command mit Validierung/Result.
- Bei Fehler `e.Cancel` oder explizites Rollback.

## KI, Pipeline, Training, Sidecar

### P1 - Meter-Quelle wird am Ende zu pauschal als LinearEstimate markiert

Belege:

- `src/AuswertungPro.Next.Infrastructure/Ai/Pipeline/MultiModelAnalysisService.cs:626` bis `:630`: Qwen-Meter kann uebernommen werden.
- `src/AuswertungPro.Next.Infrastructure/Ai/Pipeline/MultiModelAnalysisService.cs:715` bis `:721`: Dedup bekommt trotzdem `meterSource: "LinearEstimate"` und `isMeterEstimated: true`.
- `src/AuswertungPro.Next.Infrastructure/Ai/Pipeline/TemporalFindingDeduplicator.cs:319` bis `:320`: MeterSource/Estimated wird zusammengefuehrt.

Risiko: Spaeter ist nicht mehr klar, ob Meter aus OSD/Qwen oder linearer Schaetzung kam.

Fix:

- `MeterReadResult`/Quelle pro Frame mitfuehren.
- Dedup bekommt echte Quelle: `Osd`, `Qwen`, `LinearEstimate`, `Protocol`.

### P2 - Sidecar-/Pipeline-Health ist zu grob

Agentenbefund:

- Health kann "Full" melden, obwohl YOLO/DINO/SAM-Komponenten fehlen oder nicht nutzbar sind.

Risiko: UI/Automatik vertraut einem Modus, der faktisch degradiert ist.

Fix:

- Health pro Modell/Endpoint: available, loaded, degraded, error.
- UI zeigt degradierten Modus ehrlich.

### P3 - Retry/Backoff fehlt fuer Sidecar/Ollama

Agentenbefund:

- Einzelne 503/OOM/Timeouts fuehren schnell zu Frame-Skips oder degradierten Ergebnissen.

Fix:

- Kleine Retry-Policy fuer transiente Fehler.
- OOM klar als degraded reason, nicht als "kein Befund".

### P4 - Dedup-Key kann bei spaeterer Code-Anreicherung splitten

Agentenbefund:

- Dedup nutzt Label/Code-nahe Keys; Qwen kann Code spaeter nachreichen.

Risiko: derselbe Befund kann in zwei Gruppen enden.

Fix:

- Stabiler Detection-Key vor Qwen-Code.
- Code-Anreicherung nur als Attribut, nicht als primaerer Dedup-Key.

### P5 - QualityGate-Level ist nicht durchgehend persistiert

Agentenbefund:

- Schema/Retrieval kennen QualityGate-Level, Upsert/Persistenz nicht vollstaendig.

Risiko: Training/KB verliert Qualitaetsinformation.

Fix:

- QualityGateLevel in alle Save-/Upsert-/Export-Pfade aufnehmen.
- Migration/Test fuer alte Eintraege.

### P6 - PDF-Groundtruth-Schwere wird zu wenig genutzt

Agentenbefund:

- PDF-Parser extrahiert Severity, Vergleich bewertet aber vor allem Kategorie/Plausibilitaet.

Risiko: Modelltraining lernt "richtige Kategorie", aber falsche Schweregrade bleiben unbemerkt.

Fix:

- Severity in Eval-Metriken aufnehmen.
- Fehlerschwere separat berichten.

### P7 - Sidecar-Modell-Parallelitaet ist nicht ueberall serialisiert

Agentenbefund:

- SAM hat Lock, YOLO/DINO-Routen wirken weniger geschuetzt.

Risiko: Race/OOM bei parallelen Requests.

Fix:

- Pro Modell ein Semaphore/Queue.
- Backpressure statt paralleler GPU-Ueberlastung.

## Security und Tooling

### S1 - VideoLabelTool ist loopback, aber ohne eingehende Auth

Belege:

- `tools/VideoLabelTool/server.py:357`: `do_POST`.
- `tools/VideoLabelTool/server.py:359`: POST auf `/save`, `/segment`.
- `tools/VideoLabelTool/server.py:361`: Body-Length wird gelesen.
- `tools/VideoLabelTool/server.py:417`, `:453`: Writes nach `GOLD_ROOT`.
- `tools/VideoLabelTool/server.py:485`: Server bindet an `127.0.0.1`.

Bewertung: Nicht direkt remote offen, aber lokale Browser-/CSRF-/Tool-Angriffe sind moeglich.

Fix:

- Lokales Token fuer eingehende Requests.
- Origin-/Host-Pruefung.
- Engeres Body-Limit.

### S2 - XML/XTF sollte zentral SafeXmlLoader nutzen

Belege:

- `src/AuswertungPro.Next.Infrastructure/Import/Xtf/XtfHelper.cs:29`: Kommentar nennt SafeXmlLoader mit DTD-Verbot und `XmlResolver=null`.

Fix:

- Alle XTF/XML-Reader ueber zentrale SafeXmlLoader-Fabrik.
- Test mit DTD/XXE-Fixture.

### S3 - Tests koennen false-green sein, wenn Fixtures fehlen

Agentenbefund:

- `VsaKekCatalogBuilderTests` beendet einzelne Tests still, wenn Fixture fehlt.

Risiko: CI wirkt gruen, obwohl ein wichtiger Katalogtest gar nicht gelaufen ist.

Fix:

- Fixture als Testasset einchecken oder Test explizit `Skip` mit Grund.
- Kein stilles `return`.

### S4 - Tools ausserhalb der Solution

Belege:

- Solution listet Tools bis `AuswertungPro.sln:42`.
- Weitere Toolprojekte existieren z.B. `tools/InspectionDateAudit/InspectionDateAudit.csproj`, `tools/SewerStudioMcpServer/SewerStudioMcpServer.csproj`, `tools/PdfCoverageAudit/PdfCoverageAudit.csproj`.

Risiko: `dotnet build AuswertungPro.sln` deckt nicht alle Tools ab.

Fix:

- Entweder in Solution aufnehmen oder eigene Tooling-CI/Script.

## Empfohlene Fix-Reihenfolge

### Batch 1 - Datenverlust und Sicherheit

1. `MainWindow_Closing` mit `IConfirmLeave` absichern.
2. Relative Pfadaufloesung zentral ueber `ProjectPathResolver`.
3. `LiveControlClient` auf Loopback beschraenken.
4. User-Kataloge bei LoadError vor Save schuetzen.

Warum zuerst: Diese Punkte koennen Daten verlieren oder sensible Tokens/Pfade betreffen.

### Batch 2 - Medienzuordnung

1. Gemeinsames `MediaResolveResult`.
2. KINS/IBAK/WinCan/HoldingDistribution auf `Matched/NotFound/Ambiguous` umstellen.
3. Haltung-only-Fallback nur noch als Review/Warnung.
4. Tests mit exakt, suffix, not found, ambiguous, haltung-only.

Warum: Falsches Video ist fachlich schlimmer als "kein Video".

### Batch 3 - UI-Threading und Player

1. Import-Worker duerfen keine UI-Records direkt mutieren.
2. `CurrentPage` sauber disposen.
3. Player-Overlays Airspace-sicher machen.
4. Dirty-State nur ueber Shell-Methode.

Warum: Das reduziert sporadische UI-Fehler und schwer reproduzierbare Zustandsbugs.

### Batch 4 - KI-Wahrheit und Degradation

1. Qwen-Timeout/Error getrennt von Bildqualitaet.
2. DINO/SAM-Befunde bei Qwen-Fehler behalten.
3. MeterSource korrekt durchreichen.
4. Health/Degraded Reasons ehrlich anzeigen.

Warum: Die KI darf nicht "sicher falsch" wirken.

### Batch 5 - Persistenz und Training

1. TrainingCenter `BuildState()` zentral.
2. Store-Saves serialisieren und atomar machen.
3. ReviewQueue-Fehler sichtbar machen.
4. AppSettings `.bak` optional aktiv wiederherstellen.

Warum: Training/Review ist langfristiger Wert; stille Verluste sind teuer.

### Batch 6 - Architektur-Aufraeumen

1. ServiceProvider in kleinere Fabriken/Registrierungen teilen.
2. Neue ViewModels nur noch mit Konstruktor-Abhaengigkeiten.
3. Application/Infrastructure-Grenzen bei neuen Features respektieren.
4. Hotspot-Dateien nur schrittweise mit Tests verkleinern.

Warum: Das ist wichtig, aber weniger dringend als Daten- und Zuordnungsfehler.

## Minimaler Regressionstest-Plan

- App-Close mit ungespeicherter Sanierungsmatrix: Cancel verhindert Schliessen.
- Relative Pfade: `..\outside.jpg` wird abgelehnt, normaler Projektpfad akzeptiert.
- Medienmatching: exakt, suffix eindeutig, suffix ambiguous, haltung-only, not found.
- Import unter geoeffneter UI: keine Cross-Thread-Exception.
- Qwen Timeout: DINO/SAM-Finding bleibt erhalten, Status `qwen_timeout`.
- Qwen ImageQuality schlecht: nur dann clear/drop, wenn echte Bildqualitaetsantwort vorliegt.
- CostCatalog kaputtes JSON: UI warnt, Save ueberschreibt nicht leer.
- LiveControl externe URL: Request wird blockiert, Token nicht gesendet.

## Schlussbewertung

Der technische Zustand ist arbeitsfaehig, aber nicht fertig abgesichert. Die naechste sinnvolle Arbeit ist nicht ein grosser Refactor, sondern eine harte Stabilisierung der oben genannten Laufzeitkanten: Schliessen/Dirty-State, Pfadaufloesung, Medienmatching, KI-Degradation und Persistenz. Danach lohnt sich Architekturarbeit deutlich mehr, weil dann die fachlich riskanten Fehler zuerst aus dem System sind.
