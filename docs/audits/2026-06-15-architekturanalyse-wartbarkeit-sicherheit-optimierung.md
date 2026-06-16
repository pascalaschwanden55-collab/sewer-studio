# Architekturanalyse - Wartbarkeit, Sicherheit, Fehler, Optimierung

Stand: 2026-06-15  
Projekt: AuswertungPro / SewerStudio  
Art: Architektur-Audit mit Code-Gegenpruefung, keine fachliche Vollabnahme  
Schwerpunkt: Wartbarkeit, Sicherheit, Fehlerrobustheit, Performance/Optimierung

## 1. Kurzfazit

Die Architektur ist im Kern brauchbar und nicht chaotisch. Die Schichtung `Domain -> Application -> Infrastructure -> UI` ist erkennbar und wird in vielen Teilen respektiert. Die App baut aktuell sauber, und die Testbasis ist gross genug, um wichtige Refactorings abzusichern.

Die groesste Gefahr liegt nicht im Grundentwurf, sondern in der Groesse und Kopplung einzelner Hotspots:

- Sehr grosse UI-/ViewModel-Dateien tragen zu viel Fachlogik.
- Es gibt parallele Service-Erzeugung: zentraler `ServiceProvider`, direkte `new`-Instanzen in Fenstern und Tools, plus Python-Sidecar.
- Viele rekursive Dateisuchen sind fachlich notwendig, aber nicht ueberall gleich sicher, begrenzt oder eindeutig.
- Sicherheitsmechanismen fuer Sidecar und LiveControl sind besser als erwartet, aber lokale Tokens und Default-Zugangsdaten brauchen klare Regeln.
- Viele Fehler werden bewusst best-effort behandelt. Das ist bei Cleanup ok, bei Import, KI und Persistenz aber zu still.

Mein ehrliches Urteil: Das Projekt ist wartbar, wenn ab jetzt diszipliniert verkleinert wird. Ohne diese Disziplin werden Player, Import, TrainingCenter und Export zu teuer fuer jede weitere Aenderung.

## 2. Gepruefte Basis

### Build und Tests

Aktueller Stand dieser Pruefung:

- `dotnet build AuswertungPro.sln -v minimal`: erfolgreich, 0 Warnungen, 0 Fehler.
- `dotnet test AuswertungPro.sln -v minimal --no-restore`: erfolgreich.
- Pipeline-Tests: 547/547 bestanden.
- Infrastructure-Tests: 423 bestanden, 1 uebersprungen, 424 gesamt.
- UI-Tests: 435/435 bestanden.

Das ist ein guter Stand. Es heisst aber nicht, dass alle Laufzeitfehler abgedeckt sind. Gerade UI-Flows, echte VLC-Wiedergabe, grosse Kundenordner, Firebird-/WinCan-Importe und Sidecar-GPU-Verhalten bleiben teilweise schwer automatisierbar.

### Umfang

Grobe Codebasis ohne `bin`, `obj`, `.venv`, Modelle und Node-Artefakte:

- ca. 878 relevante C#-/XAML-/Python-Dateien.
- 37 `.csproj`-Dateien im Repo.
- Die Solution baut aktuell 16 Projekte.
- Viele Tool-Projekte liegen ausserhalb oder nur teilweise im normalen Build-Pfad.

Groesste Wartbarkeits-Hotspots nach Zeilen:

| Datei | Zeilen | Bewertung |
|---|---:|---|
| `src/AuswertungPro.Next.UI/Views/Windows/PlayerWindow.Coding.cs` | 4852 | Hochriskant, Kernlogik im UI-Codebehind |
| `src/AuswertungPro.Next.UI/Views/Windows/CodingModeWindow.xaml.cs` | 2600 | Verdacht auf Alt-/Parallelpfad |
| `src/AuswertungPro.Next.Application/Reports/ProtocolPdfExporter.cs` | 2464 | Export-Monolith |
| `src/AuswertungPro.Next.UI/ViewModels/Windows/TrainingCenterViewModel.cs` | 2045 | Training/State/IO stark gekoppelt |
| `src/AuswertungPro.Next.UI/Views/Pages/DataPage.xaml.cs` | 1926 | UI-Codebehind zu gross |
| `src/AuswertungPro.Next.UI/ViewModels/Pages/DataPageViewModel.cs` | 1899 | ViewModel zu breit |
| `src/AuswertungPro.Next.Infrastructure/HoldingFolderDistributor.PdfParsing.cs` | 1766 | Parser/Matching/IO teilen sich zu viel Kontext |
| `tools/CadasterDbReader/Program.cs` | 1722 | Tool-Monolith |
| `src/AuswertungPro.Next.UI/ViewModels/Windows/CostCalculatorViewModel.cs` | 1567 | Kostenlogik und UI-Zustand eng |
| `src/AuswertungPro.Next.UI/Views/Windows/PlayerWindow.LiveDetection.cs` | 1451 | KI-/UI-/Timer-Logik eng gekoppelt |

## 3. Architekturkarte

### Hauptschichten

| Schicht | Zweck | Bewertung |
|---|---|---|
| `Domain` | Modelle, Records, Projektdaten | Relativ sauber, sollte weiter frei von UI/IO bleiben |
| `Application` | Vertraege, fachliche Services, Reports, KI-Evaluation | Gute Stelle fuer reine Fachregeln, aktuell teils zu grosse Exportklassen |
| `Infrastructure` | Import, Export, Parsing, Persistenz, KI-Pipeline, Media | Leistungsstark, aber mehrere breite Services |
| `UI` | WPF, ViewModels, Player, TrainingCenter, Start/Monitoring | Haupt-Wartbarkeitsrisiko |
| `sidecar` | Python-Dienst fuer YOLO/DINO/SAM/Klassifikator | Klare Prozessgrenze, Security solide fuer lokalen Betrieb |
| `tools` | CLI-Helfer, Audits, Training, Dataset, Import | Wertvoll, aber Build-/Ownership-Drift-Risiko |

### Positive Grundentscheidungen

- Domain bleibt weitgehend frei von UI-Details.
- Sidecar ist als separater lokaler Dienst besser als KI-Logik direkt in WPF.
- Tests existieren in drei Bereichen und laufen aktuell.
- JSON-Projektpersistenz nutzt atomare Muster (`File.Replace`, `.bak`) in `JsonProjectRepository.cs:69`.
- App-Settings haben Quarantaene fuer defekte Settings und atomare Save-Muster in `AppSettings.cs:156`, `AppSettings.cs:292`.
- Sidecar erzwingt Loopback-/Trusted-Host- und Token-Pruefung in `sidecar/sidecar/main.py:152`.
- LiveControl bindet an Loopback und begrenzt Request-Groessen in `LiveControlServer.cs:26`, `LiveControlServer.cs:114`.

## 4. Wartbarkeit

### W1 - UI-Hotspots sind zu gross

Risiko: Hoch.

Der groesste Wartbarkeitsdruck liegt in der UI. Besonders `PlayerWindow.Coding.cs`, `PlayerWindow.LiveDetection.cs`, `DataPage.xaml.cs`, `TrainingCenterViewModel.cs` und `CostCalculatorViewModel.cs` enthalten zu viel Ablauf-, Fach-, KI-, Persistenz- und UI-Logik zusammen.

Konkrete Folgen:

- Kleine Aenderungen brauchen viel Kontext.
- Tests greifen schwer, weil Logik an WPF-Events, Timer, VLC oder UI-Zustand haengt.
- Live-Pfad und Batch-Pfad koennen fachlich auseinanderlaufen.
- Fehler werden spaeter gefunden, weil viele Pfade nur manuell testbar sind.

Empfehlung:

Nicht alles auf einmal umbauen. Besser bei jedem Bug eine reine Regel aus dem UI-Code ziehen:

- Dedup-Logik in Application-Service.
- Code-Auswahl und VSA-Regeln in kleine Policy-Klassen.
- Frame-/Snapshot-Erzeugung hinter Interface.
- Timer-/Polling-Ablauf in kleine Controller-Klasse.
- UI-Codebehind nur noch als Verdrahtung.

Ziel: `PlayerWindow.Coding.cs` unter 2500 Zeilen, danach unter 1500. Das ist realistischer als ein Big-Bang-MVVM-Umbau.

### W2 - Zentraler ServiceProvider ist nuetzlich, aber zu handgebaut

Risiko: Mittel bis hoch.

`src/AuswertungPro.Next.UI/ServiceProvider.cs:43` ist die zentrale Composition Root. Das ist fuer ein Solo-/Fachprogramm in Ordnung. Problematisch wird es, weil daneben weitere Erzeugungswege existieren:

- `ServiceProvider` baut Pipeline und Services.
- Player-/Coding-Fenster erzeugen Teile selbst.
- Tools erzeugen eigene Varianten.
- Python-Sidecar hat eigene Konfiguration.

Beleg:

- `ServiceProvider.cs:109`: Pipeline-Konfiguration.
- `ServiceProvider.cs:190`: Factory fuer Videoanalyse-Pipeline.
- `ServiceProvider.cs:404` ff. bzw. `GetService`: manuelles Service-Locator-Muster.

Empfehlung:

Kurzfristig keinen kompletten DI-Framework-Wechsel erzwingen. Zuerst:

- Alle KI-/Pipeline-Factorys in `ServiceProvider` buendeln.
- PlayerWindow darf Pipeline-Services nicht selbst aus Defaults bauen.
- Konfigurationswerte nur ueber eine Quelle: `PipelineCfg`/`AiSettings`.
- Tools bekommen kleine Factorys aus `Application`/`Infrastructure`, nicht eigene Kopien.

Mittelfristig kann `Microsoft.Extensions.DependencyInjection` helfen. Aber nur, wenn vorher klar ist, welche Lifetimes gebraucht werden.

### W3 - Tools sind wertvoll, aber zu verteilt

Risiko: Mittel.

Es gibt 37 `.csproj`-Dateien, aber der normale Solution-Build umfasst aktuell 16 Projekte. Viele Tools sind fachlich wichtig: Eval, Dataset, Sidecar-Smoke, ClassifierPilot, Cadaster, MCP, Export. Wenn ein src-Refactoring Tools ausserhalb der Solution bricht, faellt es spaet auf.

Empfehlung:

- Tool-Inventar anlegen: kritisch, optional, experimentell, alt.
- Kritische Tools in die Solution aufnehmen oder in einem eigenen `tools-critical.slnf` pruefen.
- Fuer experimentelle Tools eine kurze README mit Zweck und Besitzer.
- Tote Tools loeschen oder als `deprecated` markieren.

### W4 - Parser/Importer brauchen klarere Grenzen

Risiko: Mittel bis hoch.

`HoldingFolderDistributor.cs` und `HoldingFolderDistributor.PdfParsing.cs` leisten viel: PDF-Suche, Parsing, Matching, Kopieren, Sidecar-XTF, Fehlerdateien, Zielstruktur. Das ist fachlich zentral, aber schwer zu aendern.

Empfehlung:

Die vorhandene Logik nicht ersetzen, sondern schneiden:

- `ProtocolPdfParser`: nur Text/Datum/Haltung/Filmname extrahieren.
- `VideoMatcher`: exakt, Suffix, ambiguous, missing.
- `DistributionWriter`: Zielpfade, UniquePath, Copy/Move.
- `DistributionAudit`: Result-Records und Fehlerdateien.

Der Vorteil: Parser- und Matching-Regeln werden testbarer, ohne die UI anzufassen.

## 5. Sicherheit

### S1 - Sidecar-Sicherheit ist fuer lokalen Betrieb ordentlich

Risiko: aktuell niedrig bis mittel.

Gute Punkte:

- Default-Host ist `127.0.0.1` in `sidecar/sidecar/config.py:9`.
- Trusted Hosts sind eingeschraenkt in `sidecar/sidecar/config.py:13`.
- Auth-Token wird beim Start aufgeloest/erzeugt in `sidecar/sidecar/main.py:47`.
- Middleware prueft Trusted Host und `X-Sidecar-Token` in `sidecar/sidecar/main.py:152`.
- Tokenvergleich nutzt `hmac.compare_digest` in `sidecar/sidecar/main.py:165`.
- C#-Client sendet Sidecar-Token nur bei Loopback-URI in `VisionPipelineClient.cs:36`.

Das ist fuer "lokaler KI-Dienst auf derselben Maschine" angemessen.

Empfehlung:

- Diese Grenze klar dokumentieren: Sidecar nicht ins LAN freigeben.
- Bei anderer Host-Konfiguration App hart warnen.
- Token-Datei mit lokalen ACLs absichern.
- Token nie in Logs schreiben.

### S2 - Lokale Tokens duerfen nicht dauerhaft zu breit gespeichert werden

Risiko: Mittel.

`AppSettings.cs:99` enthaelt `PipelineSidecarToken`. Das ist bequem, aber ein Token in normaler JSON-Settings-Datei ist sensibel. Fuer lokalen Betrieb ist das kein akuter GAU, aber es ist vermeidbare Angriffs- und Supportflaeche.

Empfehlung:

- Bevorzugt Token-Datei in `%LOCALAPPDATA%/SewerStudio/.sidecar_token`.
- Optional DPAPI/Windows Credential Manager fuer gespeicherte Secrets.
- `PipelineSidecarToken` nur noch als Migrations-/Legacy-Feld behandeln.
- UI sollte nie den kompletten Token anzeigen.

### S3 - Firebird-Default-Zugangsdaten sind fachlich erklaerbar, aber sicherheitlich unschoen

Risiko: niedrig bis mittel, je nach Einsatz.

Belege:

- `tools/CadasterDbReader/Program.cs:356`: `SYSDBA`.
- `tools/CadasterDbReader/Program.cs:357`: `masterkey`.
- `IbakExportImportService.cs:490`: `SYSDBA`.
- `IbakExportImportService.cs:491`: `masterkey`.
- `KiasFdbTopologyReader.cs:60`: `SYSDBA`.
- `KiasFdbTopologyReader.cs:61`: `masterkey`.

Das sind bekannte Firebird-Defaults und bei lokalen alten Exportdateien oft realistisch. Trotzdem sollte das nicht still und dauerhaft als Default passieren.

Empfehlung:

- Default nur fuer bekannte lokale `.fdb`-Importe erlauben.
- Bei Netzwerkpfaden keine Default-Credentials.
- Klare Warnmeldung: "Firebird Default-Zugangsdaten verwendet".
- Langfristig Credentials ueber Settings/Env, nicht im Code-Fallback.

### S4 - LiveControl ist grundsaetzlich gut abgesichert

Risiko: niedrig bis mittel.

Gute Punkte:

- Request-Koerper auf 64 KB begrenzt in `LiveControlServer.cs:26`.
- Bindung an Loopback in `LiveControlServer.cs:114`.
- Tokenvergleich mit `CryptographicOperations.FixedTimeEquals` in `LiveControlServer.cs:224`.

Rest-Risiko:

Tools, die LiveControl ansprechen, duerfen Tokens nicht an fremde Hosts senden. Der Client nutzt `X-Live-Control-Token` in `tools/SewerStudioMcpServer/LiveControlClient.cs:64`. Das muss strikt Loopback bleiben.

Empfehlung:

- Client-seitig nur `localhost`, `127.0.0.1`, `[::1]` erlauben.
- Token nur bei Loopback setzen.
- Tests fuer externe URLs.

### S5 - Dateipfade und rekursive Suche bleiben die groesste Sicherheits-/Datenrisiko-Flaeche

Risiko: hoch fuer Datenintegritaet, mittel fuer Security.

Das Programm verarbeitet fremde Projektordner, PDFs, Videos, XTF/MDB/FDB und exportiert Dateien. Dadurch sind Pfadgrenzen wichtig.

Es gibt bereits `SafeFileEnumeration` in `src/AuswertungPro.Next.Infrastructure/Common/SafeFileEnumeration.cs:21`, aber viele Stellen nutzen weiter direkte rekursive Suchen mit `SearchOption.AllDirectories`.

Beispiele:

- `HoldingFolderDistributor.cs:39`, `:548`, `:704`.
- `WinCanDbImportService.cs:406`, `:958`.
- `IbakExportImportService.cs:675`, `:756`.
- `KinsImportService.cs:287`, `:481`.
- `ProtocolPdfExporter.cs:2581`.
- `MediaDistributionService.cs:425`.

Empfehlung:

- Eine verbindliche Safe-Enumeration-Policy fuer Import/Export.
- Rekursive Suche immer mit Fehlerbehandlung, Abbruchmoeglichkeit und optionaler Max-Datei-Anzahl.
- Bei Matching nie "erster Treffer gewinnt", wenn mehrere fachlich moeglich sind.
- Root-Containment pruefen, bevor Dateien gelesen/kopiert/exportiert werden.

## 6. Fehlerrobustheit

### F1 - Viele stille `catch`-Bloecke verschlucken Ursachen

Risiko: mittel.

Es gibt viele leere oder fast leere `catch`-Bloecke. Bei Cleanup ist das akzeptabel. Bei Import, KI, Training, Monitoring und Player wird es schwierig, wenn echte Ursachen verschwinden.

Beispiele:

- `HoldingFolderDistributor.SidecarXtf.cs:169` bis `:172`.
- `PdfOcrExtractor.cs:78`.
- `TrainingCenterImportService.cs:100`.
- `PlayerWindow.Playback.cs:372` bis `:406`.
- `PlayerWindow.Coding.cs:2582`, `:4663`, `:5544`.
- `SystemMonitorService.cs:398`, `:1342`.

Empfehlung:

- Kleine Hilfsmethode: `BestEffort.Try(action, logger, context)`.
- Cleanup-Fehler auf Debug-Level.
- Import-/Persistenz-/KI-Fehler mindestens als Warning mit Kontext.
- Kein stiller Fallback bei fachlichem Ergebnis.

### F2 - `async void` ist in WPF erlaubt, aber zu breit eingesetzt

Risiko: mittel.

Viele Eventhandler sind `async void`. Das ist in WPF normal, aber Exceptions und Cancellation werden schwer kontrollierbar.

Beispiele:

- `App.xaml.cs:48`.
- `PlayerWindow.LiveDetection.cs:38`, `:361`, `:498`, `:1085`, `:1367`, `:1450`.
- `PlayerWindow.Coding.cs:1033`, `:1711`, `:3030`, `:3321`, `:4623`, `:5173`.
- `TrainingCenterWindow.xaml.cs:265`, `:479`, `:499`, `:597`, `:688`, `:733`.
- `DataPageViewModel.cs:1362`, `:1464`.

Empfehlung:

- Eventhandler nur als duenne Shell lassen.
- Inhalt in `Task`-Methoden verschieben.
- Zentraler `RunUiAsync`-Wrapper fuer Exception-Handling, Busy-State und Cancellation.
- Bei Timer-Events Reentrancy verhindern.

### F3 - Persistenz ist teilweise sehr gut, sollte aber einheitlich werden

Risiko: mittel.

Stark:

- `JsonProjectRepository.cs:69` nutzt `File.Replace`.
- `AppSettings.cs:292` nutzt ebenfalls `File.Replace`.
- Defekte Settings werden quarantiniert in `AppSettings.cs:314`.

Unklar/riskant:

- Mehrere Training-/Review-/Template-Stores haben eigene Speicherlogik.
- Best-effort Fehler koennen dazu fuehren, dass UI "weiterlaeuft", obwohl Zustand nicht gesichert wurde.

Empfehlung:

- Ein gemeinsamer `AtomicJsonFileWriter`.
- Einheitliches `LoadResult<T>` mit `Value`, `Recovered`, `Error`.
- UI-Warnung, wenn ein User-Katalog nur aus Fallback geladen wurde.

### F4 - KI-Fehler muessen fachlich anders wirken als "keine Erkennung"

Risiko: hoch fuer Detektionsqualitaet.

Eine KI-Pipeline darf nicht still zwischen diesen Faellen vermischen:

- Modell nicht erreichbar.
- Timeout.
- Bildqualitaet schlecht.
- Modell sagt "kein Befund".
- DINO/SAM finden etwas, Qwen/Ollama klassifiziert nicht.

Empfehlung:

- Ergebnisstatus explizit halten: `Ok`, `NoFinding`, `ModelUnavailable`, `Timeout`, `LowQuality`, `Uncertain`.
- UI/Protokoll darf Fehler nicht als "sicher leer" anzeigen.
- Training nur aus klar markierten Quellen speisen.

## 7. Optimierung und Performance

### O1 - Startanimation wurde sinnvoll entschlackt

Risiko vorher: sichtbares Stottern beim Start.

Aktueller Stand:

- `StartupSplashWindow.xaml.cs:17`: NodeCount ist auf 112 reduziert.
- `StartupSplashWindow.xaml.cs:30`: `Stopwatch` fuer Frame-Zeit.
- `StartupSplashWindow.xaml.cs:217`: Rendering ueber `CompositionTarget.Rendering`.

Das ist die richtige Richtung. Die Animation haengt dadurch enger am WPF-Renderloop und erzeugt weniger Druck pro Frame.

Weitere Empfehlung:

- Im Splash keine neuen Brushes/Effekte pro Frame.
- Keine DropShadows auf vielen bewegten Elementen.
- Bei sehr schwachen PCs optional "reduzierte Animation".

### O2 - Rekursive Dateisuchen brauchen Budget und Index

Risiko: hoch bei grossen Kundenordnern.

Viele Imports muessen grosse Ordnerbaeume durchsuchen. Ohne Budget kann das langsam wirken oder haengen, besonders auf Netzlaufwerken.

Empfehlung:

- Einmaliger Media-/Projektindex pro Importlauf.
- CancellationToken fuer lange Scans.
- Progress mit "Dateien gefunden/geprueft".
- Max-Datei-Anzahl oder Warnung bei extrem grossen Roots.
- `SafeFileEnumeration` als Standard.

### O3 - Export/PDF-Erzeugung ist ein Monolith

Risiko: mittel.

`ProtocolPdfExporter.cs` hat 2464 Zeilen. PDF-Export ist typischerweise CPU-/IO-lastig und fachlich empfindlich. Gleichzeitig ist er schwer testbar, wenn Layout, Datenaufbereitung, Pfadsuche und Asset-Aufloesung in einer Klasse liegen.

Empfehlung:

- Datenmodell fuer Export vor dem Rendering bauen.
- Asset-/Foto-/Protokollpfad-Aufloesung separat testen.
- Layout-Komponenten klein schneiden.
- Keine rekursive Suche im Renderpfad, wenn vorher indexiert werden kann.

### O4 - AI-Warmup und Sidecar muessen gestaffelt bleiben

Risiko: mittel.

`AiStartupService.cs:407` startet externe Prozesse ueber `ProcessStartInfo`. Das ist richtig, aber AI-Warmup kann CPU/GPU und IO stark belasten. Startanimation und UI sollten nicht von Modell-Warmup blockiert werden.

Empfehlung:

- App zuerst benutzbar machen, KI danach warm starten.
- Warmup abbrechbar machen.
- Sidecar-Status klar anzeigen: aus, startet, warm, degraded, fehler.
- Keine grossen Modelle laden, wenn der Nutzer nur Import/Export machen will.

## 8. Priorisierte Massnahmen

### Sofort, klein, hoher Nutzen

1. `LiveControlClient` hart auf Loopback beschraenken.
2. Firebird-Default-Credentials sichtbar warnen und bei Netzwerkpfaden blockieren.
3. `BestEffort`-Logging-Helfer einfuehren und stille catches in Import/KI/Training zuerst ersetzen.
4. `SafeFileEnumeration` fuer neue rekursive Suchen verpflichtend machen.
5. `PipelineSidecarToken` als Legacy markieren und Token-Datei bevorzugen.

### Naechste 2 bis 4 Wochen

1. `PlayerWindow.Coding.cs` in kleine Application-/Infrastructure-Policies schneiden.
2. KI-Service-Erzeugung aus PlayerWindow in `ServiceProvider`/Factory verlagern.
3. `HoldingFolderDistributor` in Parser, Matcher, Writer und Audit-Result aufteilen.
4. Export-Pfadsuche aus `ProtocolPdfExporter` auslagern.
5. Kritische Tools in normalen Build/Test-Pfad aufnehmen.

### Danach

1. Pruefen, ob `Microsoft.Extensions.DependencyInjection` lohnt.
2. Projektweiter `AtomicJsonFileWriter`.
3. Medien-/Projektindex fuer grosse Ordner.
4. Formale Security-Scan-Pipeline mit Findings und Regressionstests.
5. Performance-Profiling fuer Import, PDF-Export und Sidecar-Warmup.

## 9. Risikomatrix

| Bereich | Risiko | Grund | Naechster Schritt |
|---|---|---|---|
| Wartbarkeit UI | Hoch | sehr grosse Codebehind-/ViewModel-Dateien | schrittweise Policies extrahieren |
| Import/Medien | Hoch | rekursive Suche, Mehrdeutigkeit, Pfadgrenzen | Safe-Enumeration und eindeutige Match-Resultate |
| KI-Qualitaet | Mittel bis hoch | Fehlerstatus und "kein Befund" koennen fachlich verschwimmen | Ergebnisstatus schaerfen |
| Security Sidecar | Niedrig bis mittel | Loopback+Token gut, Token-Speicherung offen | Token-Datei/ACL/DPAPI |
| LiveControl | Niedrig bis mittel | Server gut, Client muss Loopback erzwingen | Client-Guard testen |
| Persistenz | Mittel | Kernprojekt gut, Nebenstores uneinheitlich | AtomicJsonFileWriter |
| Performance | Mittel | grosse Ordnerscans, PDF-Export, AI-Warmup | Index, Cancellation, Profiling |
| Tooling | Mittel | viele Tools ausserhalb normaler Pruefung | Tool-Inventar und Build-Pfad |

## 10. Was nicht sofort geaendert werden sollte

- Keine komplette Neuschreibung der WPF-App.
- Kein Microservice-Umbau.
- Kein grosser MVVM-Big-Bang.
- Kein Entfernen des Sidecars, solange lokale KI gebraucht wird.
- Kein blinder DI-Framework-Wechsel ohne Lifetimes und Factory-Grenzen.

Die bessere Strategie ist: kleine reine Fachregeln extrahieren, testen, dann den naechsten Bereich anfassen.

## 11. Schlussbewertung

Das Projekt hat eine solide Basis und aktuell gruene Tests. Die groesste technische Schuld steckt in grossen UI- und Import-/Export-Monolithen, nicht in der Grundarchitektur.

Sicherheit ist fuer lokalen Desktop-Betrieb besser als bei vielen vergleichbaren Tools, vor allem durch Loopback und Token beim Sidecar. Trotzdem muessen Tokens, Default-Credentials, Pfadgrenzen und lokale HTTP-Tools klarer geregelt werden.

Der wichtigste Wartbarkeitshebel ist nicht ein neues Framework, sondern konsequentes Verkleinern:

1. Fachregeln aus UI herausziehen.
2. Rekursive Dateioperationen vereinheitlichen.
3. Fehler sichtbar machen.
4. Tooling in den Build holen.
5. KI-Status fachlich eindeutig halten.

Wenn diese Punkte umgesetzt werden, bleibt das Projekt auch mit KI-, Video-, Import- und Exportumfang beherrschbar.
