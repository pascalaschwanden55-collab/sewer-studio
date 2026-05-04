# Repository-Audit SewerStudio / AuswertungPro

Datum: 2026-05-04  
Scope: gesamtes Repository unter `C:\Sewer-Studio_KI_4.2`  
Schwerpunkt: Architektur, Fehler, Konsistenz, Datenbank/Training/KI/Sanierung, Verschlankung, Layout

## Kurzfazit in einfachen Worten

Das Programm ist fachlich sehr stark und enthaelt viele gute Bausteine: Import, Video, VSA-Codierung, KI-Training, QualityGate, Sanierung und Reports. Das Problem ist nicht, dass "alles schlecht" ist. Das Problem ist, dass zu viel auf einmal im selben UI-Projekt gewachsen ist.

Der wichtigste Befund: Die eigentliche Clean Architecture ist angelegt, aber die UI traegt zu viel Logik. Das macht Fehler wahrscheinlicher, Tests langsamer und das Programm schwerer wartbar.

Der zweite harte Befund: Der aktuelle Stand baut nicht sauber. `dotnet build AuswertungPro.sln -v minimal` scheitert im UI-Projekt mit vielen XAML-CodeBehind-Fehlern wie `InitializeComponent` und fehlenden benannten Controls.

Der dritte Befund: Das Repository ist deutlich zu gross. Modelle, Sidecar-Venvs, Frames, lokale SDK-Caches und Laufzeitdaten liegen im Arbeitsbaum. Das macht Git, Suche, Backups und Builds langsam und unuebersichtlich.

Die beste Strategie ist nicht ein grosses Komplett-Refactoring. Sinnvoller ist eine klare Sanierung in Etappen: erst Build reparieren, dann Repo entmuellen, dann Tests trennen, dann KI/Training/DB aus der UI herausziehen, danach die Oberflaeche vereinfachen.

## Was geprueft wurde

- Solution- und Projektstruktur
- `dotnet build AuswertungPro.sln -v minimal`
- Infrastruktur-Tests
- Pipeline-Testprojekt
- Dateigroessen und grosse Artefakte im Repo
- groesste C#-Dateien
- statische Suche nach Prozessaufrufen, `HttpClient`, Blocking-Waits, `catch { }`, Dispatcher-Nutzung
- KnowledgeBase/SQLite-Schicht
- TrainingCenter und KI-Pipeline
- Sanierungs- und Devis-Bausteine
- XAML-Struktur und visuelle Konsistenz anhand der Dateien

## 1. Architektur

### Befund 1.1: Die Schichten existieren, aber die UI ist zu gross

Es gibt eine gute Grundstruktur:

- `AuswertungPro.Next.Domain`
- `AuswertungPro.Next.Application`
- `AuswertungPro.Next.Infrastructure`
- `AuswertungPro.Next.UI`
- zwei Testprojekte

Das ist grundsaetzlich richtig. In der Praxis liegt aber sehr viel Fachlogik in der UI. Das UI-Projekt hat rund 89.000 C#-Zeilen. Domain hat nur rund 1.700 C#-Zeilen. Damit ist die UI nicht nur Anzeige, sondern Hauptsystem.

Beispiele:

- `src/AuswertungPro.Next.UI/Views/Windows/PlayerWindow.xaml.cs`: 8.581 Zeilen
- `src/AuswertungPro.Next.UI/ViewModels/Windows/TrainingCenterViewModel.cs`: 2.628 Zeilen
- `src/AuswertungPro.Next.UI/Views/Windows/CodingModeWindow.xaml.cs`: ca. 3.459 Zeilen
- `src/AuswertungPro.Next.UI/Ai/**`: KI, Training, QualityGate, KnowledgeBase und Pipeline liegen im UI-Projekt

Risiko: Jede Aenderung an KI, Training oder DB zieht WPF mit. Tests werden schwerer, Builds langsamer und Fehler wirken direkt auf die Oberflaeche.

Verbesserung:

- Eine eigene Schicht fuer KI/Training einfuehren, z.B. `AuswertungPro.Next.Ai` oder die Logik sauber auf `Application` und `Infrastructure` verteilen.
- UI darf nur Commands, Status, Progress und Auswahl halten.
- KI-Orchestrierung, DB-Zugriff, Batch-Training und Dateioperationen in Services verschieben.

### Befund 1.2: Grosse God-Objects

Mehrere Dateien haben zu viele Verantwortlichkeiten:

- `PlayerWindow.xaml.cs`: Video, Codierung, KI, QualityGate, Training, Feedback, Overlays, Snapshot-Logik.
- `TrainingCenterViewModel.cs`: UI-State, Dateiimport, Batch-Training, Ollama/HttpClient, SQLite-KB, QualityGate, Logik fuer mehrere Tabs.
- `HoldingFolderDistributor.cs`: Parsing, Matching, Dateioperationen, Ergebnislogik in einer sehr grossen Klasse.
- `ProtocolPdfExporter.cs`: umfangreiche Reportlogik in einer Datei.

Risiko: Diese Dateien sind schwer zu testen und bei Bugfixes besteht hohe Gefahr fuer Nebenwirkungen.

Verbesserung:

- `PlayerWindow` in kleinere Bausteine trennen:
  - `VideoPlaybackController`
  - `CodingSessionController`
  - `AiDetectionController`
  - `Overlay/MeasurementService`
  - `PlayerWindow` nur noch als Shell
- `TrainingCenterViewModel` trennen:
  - `BatchTrainingOrchestrator`
  - `TrainingRunRepository`
  - `KnowledgeBaseWriter`
  - `TrainingProgressModel`
- `HoldingFolderDistributor` trennen:
  - PDF-Parser
  - Video-Matcher
  - Datei-Kopierer/Verschieber
  - Result-Reporter

### Befund 1.3: Tests haengen an WPF

Das Pipeline-Testprojekt referenziert das UI-Projekt und ist selbst WPF:

- `tests/AuswertungPro.Next.Pipeline.Tests/AuswertungPro.Next.Pipeline.Tests.csproj`
- Referenz auf `src/AuswertungPro.Next.UI/AuswertungPro.Next.UI.csproj`
- `UseWPF=true`

Risiko: Fachlogik kann nicht leicht ohne UI getestet werden. Wenn UI nicht baut, fallen auch Pipeline-Tests weg.

Verbesserung:

- Reine KI-/QualityGate-/Training-Logik aus dem UI-Projekt herausziehen.
- Tests gegen `Application`/`Infrastructure` oder neue KI-Library schreiben.
- WPF-Tests nur fuer echte UI-Integration verwenden.

## 2. Fehler und Stabilitaet

### Befund 2.1: Der aktuelle Stand baut nicht sauber

`dotnet build AuswertungPro.sln -v minimal` scheitert im UI-Projekt. Domain, Application, Infrastructure und Infrastructure.Tests bauen vorher. Danach kommen viele Fehler im WPF-Temp-Projekt:

- `InitializeComponent` fehlt
- benannte Controls fehlen, z.B. `ApplyButton`, `HistoryGrid`, `EntriesGrid`, `HeaderText`
- mehrere XAML-CodeBehind-Dateien koennen ihre generierten Felder nicht sehen
- bei `SchaechtePage.xaml.cs` wird offenbar statt eines benannten Grids der Typ `Grid` aufgeloest

Das ist aktuell der wichtigste technische Fehler.

Verbesserung:

1. UI-Build isoliert reparieren: `dotnet build src/AuswertungPro.Next.UI/AuswertungPro.Next.UI.csproj -v minimal -p:UseAppHost=false`.
2. `bin`/`obj` bereinigen und erneut bauen, weil WPF-Temp-Projekte manchmal stale generierte Dateien behalten.
3. Falls der Fehler bleibt: XAML-Dateien pruefen auf falsche `x:Class`, falschen Namespace, Build Action, doppelte Klassen oder bedingte Compile Includes.
4. Erst wenn der UI-Build gruen ist, weitere Refactorings starten.

### Befund 2.2: Infrastructure-Tests sind gut, Pipeline-Tests sind nicht sauber getrennt

Infrastructure-Tests:

- 135 bestanden
- 1 uebersprungen

Pipeline-Tests:

- konnten im Auditlauf nicht sauber fertiglaufen
- enthalten GPU/Ollama/Langzeittests
- Beispiel: `QwenModelComparisonTest.cs` dokumentiert ca. 100 Minuten Laufzeit
- ein Test ist geskippt, ein anderer `GpuEval`-Test ist aber trotzdem `[Fact]`

Risiko: Ein normaler Testlauf wird unberechenbar. CI oder lokale Builds koennen haengen oder sehr lange laufen.

Verbesserung:

- Testkategorien trennen:
  - `Unit`: immer schnell, keine GPU, keine externen Pfade
  - `Integration`: Dateisystem/SQLite, aber kontrolliert
  - `GpuEval`: nur manuell
  - `LongRunning`: nur manuell
- Alle Ollama/GPU/`C:\KI_BRAIN`-Tests standardmaessig deaktivieren oder per Filter explizit starten.
- `dotnet test` muss unter 1-2 Minuten gruen werden.

### Befund 2.3: Externe Prozessaufrufe sind teilweise verbessert, aber noch nicht einheitlich

Positiv:

- `src/AuswertungPro.Next.Application/Common/ProcessRunner.cs` nutzt `ArgumentList`, asynchrones stdout/stderr-Drain und Timeout.
- `PdfProtocolExtractor.cs` nutzt aktuell fuer `pdftotext` und Python bereits `ArgumentList`.

Risiko bleibt:

- Im Repo existieren weiterhin direkte `ProcessStartInfo.Arguments = ...` Stellen, z.B. fuer Explorer, PowerShell, nvidia-smi, Playwright und Sidecar.
- Manche Stellen sind kontrolliert und wenig kritisch, aber der Stil ist uneinheitlich.

Verbesserung:

- Alle neuen Prozessstarts nur noch ueber `ProcessRunner`.
- Bestehende direkte Prozessstarts pruefen:
  - wenn User-/Dateipfade beteiligt sind: auf `ArgumentList` oder `ProcessRunner` umstellen
  - wenn statisch: dokumentieren oder ebenfalls vereinheitlichen

### Befund 2.4: Blocking-Waits in async Code

Es gibt mehrere Stellen mit:

- `.GetAwaiter().GetResult()`
- `.Result`
- `Thread.Sleep`
- synchrones `Dispatcher.Invoke`

Beispiele:

- PDF/OCR-Fallbacks in Training-Services
- `WeakSpotCurator`
- `TrainingCenterViewModel`
- `QuickScanService`
- `PlayerWindow`
- `M150MdbImportHelper`

Risiko: UI-Haenger, Threadpool-Probleme und schwer reproduzierbare Deadlocks.

Verbesserung:

- Sync-Wrapper fuer Async-Code reduzieren.
- `Dispatcher.Invoke` durch `InvokeAsync` ersetzen, wo kein sofortiges Ergebnis noetig ist.
- Datei-/Retry-Sleeps nur in Background-Code erlauben.
- Langfristig: Producer/Consumer mit `Channel<T>` fuer Training/KB-Updates.

### Befund 2.5: WindowStateManager ist teilweise verbessert, aber Save bleibt synchron

In `WindowStateManager.cs` ist der Closing-Hook jetzt mit try/catch abgesichert. Das ist gut.

Aktuell bleibt aber:

- `settings.Save()` laeuft synchron beim Fensterschliessen
- `GetSettings()` schluckt Fehler ohne Log

Risiko: Bei langsamer Platte, Antivirus-Lock oder Profilproblemen kann das Schliessen kurz blockieren.

Verbesserung:

- `settings.Save()` entkoppeln oder in einen zentralen Settings-Save-Debouncer verschieben.
- Fehler in `GetSettings()` mindestens per Debug/Logger protokollieren.

### Befund 2.6: CTS-Race im TrainingCenter ist bereits besser geloest

Das fruehere Muster `Cancel(); Dispose(); new()` ist im aktuellen Code durch `RotateGenCts()` ersetzt. Die alte CTS wird gecancelt und spaeter disposed.

Bewertung: Dieser konkrete externe Audit-Punkt ist im aktuellen Stand bereits sinnvoll entschaerft.

Verbesserung:

- Das Pattern beibehalten.
- Alle anderen CancellationTokenSource-Stellen im Repo nach demselben Muster pruefen.

## 3. Konsistenz

### Befund 3.1: Sprache und Begriffe sind uneinheitlich

Im Code und UI mischen sich Deutsch und Englisch:

- `Haltung`, `Schacht`, `Sanierung`
- `TrainingCase`, `Sample`, `WeakSpot`
- `QualityGate`, `ReviewQueue`, `Teacher`, `FewShot`

Das ist technisch nicht falsch, aber es macht das System schwerer zu verstehen.

Verbesserung:

- Ein Glossar anlegen:
  - fachliche Begriffe bleiben deutsch
  - technische KI-Begriffe duerfen englisch bleiben
  - UI-Texte fuer Anwender klar deutsch
- Klassen und Views nach diesem Glossar benennen.

### Befund 3.2: Mehrere Daten- und Konfigurationsquellen fuer Sanierung

Sanierungswissen liegt an mehreren Orten:

- `Knowledge/sanierung/*.yaml`
- `Knowledge/sanierung/*.json`
- `Knowledge/measures_learning.json`
- UI-Config-Dateien
- Devis-/Sanierungsservices im Code

Risiko: Zwei Quellen koennen unterschiedliche Regeln oder Preise enthalten. Dann weiss man nicht mehr, was "wahr" ist.

Verbesserung:

- Eine fuehrende Quelle definieren, z.B. `Knowledge/sanierung`.
- UI-Config nur daraus generieren oder laden.
- Jede Sanierungsentscheidung mit `KnowledgeVersion` speichern.

### Befund 3.3: Zwei SQLite-Pakete

Infrastructure referenziert:

- `System.Data.SQLite.Core`
- `Microsoft.Data.Sqlite`

UI nutzt ebenfalls `Microsoft.Data.Sqlite`.

Risiko: Zwei APIs, zwei Verhaltensweisen, mehr kognitive Last.

Verbesserung:

- Wenn moeglich auf `Microsoft.Data.Sqlite` vereinheitlichen.
- Falls `System.Data.SQLite` fuer Spezialfaelle noetig ist, klar dokumentieren.

### Befund 3.4: UI-Styles sind nicht konsequent zentralisiert

Es gibt Theme-Dateien, aber grosse XAML-Dateien enthalten weiterhin viele lokale Farben, Margins, FontSizes und Inline-Styles.

Risiko: Das Programm wirkt uneinheitlich und Aenderungen am Design muessen an vielen Stellen gemacht werden.

Verbesserung:

- Eine Design-System-Datei fuer Farben, Spacing, Buttons, DataGrids, Panels, Toolbars.
- Lokale Inline-Farben stark reduzieren.
- Pro Fenstertyp nur noch bestehende Styles verwenden.

## 4. Datenbank, Training, KI-Pipeline, Sanierung

### Befund 4.1: KnowledgeBase hat gute Grundlagen, aber noch keine robuste Writer-Architektur

Positiv:

- SQLite-KB existiert sauber als `KnowledgeBaseContext`.
- WAL wird aktiviert.
- Es gibt Tabellen fuer Samples, Embeddings, Versions, CategoryWeights und ValidationLog.
- QualityGateLevel ist integriert.

Risiko:

- `KnowledgeBaseContext` oeffnet direkt `SqliteConnection($"Data Source={path}")`.
- Es gibt kein sichtbares `busy_timeout`.
- Es gibt kein explizites `PRAGMA foreign_keys=ON`.
- `RebuildGuard` schuetzt Rebuild und Einzel-Indexierung, aber nicht alle Writes.
- `BackfillQualityGateLevels()` oeffnet eigenen Context und nutzt nicht denselben globalen Writer-Lock.

Verbesserung:

- Einen dedizierten `KnowledgeBaseWriter` einfuehren.
- Alle schreibenden KB-Operationen durch diesen Writer serialisieren.
- SQLite-Verbindung mit ConnectionStringBuilder konfigurieren.
- `PRAGMA busy_timeout`, `foreign_keys`, `journal_mode=WAL` zentral setzen.
- Foreign Key zwischen `Embeddings.SampleId` und `Samples.SampleId` pruefen/einfuehren.

### Befund 4.2: Trainingsdaten brauchen mehr Herkunft und Nachvollziehbarkeit

Aktuell sind Training-Samples und KB-Eintraege fachlich brauchbar, aber fuer spaetere Qualitaetskontrolle fehlt eine harte Laufhistorie.

Wichtige Felder fuer spaeter:

- `RunId`
- Modellname und Modellversion
- Prompt-Version
- Pipeline-Version
- Source-Datei-Hash
- PDF-/Video-Pfad
- QualityGate-Ergebnis
- Menschlich bestaetigt ja/nein
- Sanierungs-/VSA-Kontext

Verbesserung:

- Tabelle `TrainingRuns` einfuehren.
- Jedes Sample bekommt `RunId`.
- Jede KI-Entscheidung bekommt eine Erklaerung oder Evidence-Daten.
- Training-Export nur aus reproduzierbaren Runs erzeugen.

### Befund 4.3: QualityGate ist ein starker Pluspunkt

Positiv:

- Es gibt `SampleQualityGateService`.
- Red/Yellow/Green wird verwendet.
- Red Samples werden abgewiesen.
- Yellow geht in Review.
- Tests fuer QualityGate existieren.

Risiko:

- QualityGate-Logik liegt im UI-Projekt.
- Tests referenzieren deshalb UI.
- Beschreibung/Metadaten werden nicht sichtbar hart auf Laenge und Zeichensatz begrenzt.

Verbesserung:

- QualityGate in `Application` oder neue KI-Core-Library verschieben.
- Vor dem Speichern Validierung ergaenzen:
  - Beschreibung max. Laenge
  - keine Steuerzeichen
  - gueltiger VSA-Code
  - plausible Meterwerte
  - FramePath vorhanden oder bewusst leer markiert

### Befund 4.4: TrainingCenter-Pipeline ist leistungsfaehig, aber schwer zu steuern

Aktuell verarbeitet `TrainingCenterViewModel` Faelle parallel, extrahiert PDF-Fotos vorab, startet Selbsttraining und schreibt danach in Samples/KB. Das ist funktional stark, aber komplex.

Risiko:

- Parallelitaet, UI-Updates, SQLite-Writes und KI-Requests laufen im selben ViewModel zusammen.
- Das macht Hanger und Race Conditions wahrscheinlicher.

Verbesserung:

- `Channel<T>`-Pipeline:
  - Stage 1: PDF/Video vorbereiten
  - Stage 2: KI-Inferenz mit begrenzter Parallelitaet
  - Stage 3: QualityGate
  - Stage 4: genau ein KB-Writer
  - Stage 5: UI-Progress per Events
- ViewModel kennt nur Start, Cancel, Fortschritt, Resultate.

### Befund 4.5: KI-Pipeline hat sehr viele Modi

Im Repo liegen viele KI-Bausteine:

- Ollama/Qwen
- YOLO
- SAM
- Florence
- DINO/MultiModel
- LoRA
- FewShot
- Teacher
- Benchmark
- WeakSpot
- Night Batch / Sidecar
- Live Detection

Das ist fuer Entwicklung beeindruckend, fuer ein produktives Programm aber zu viel sichtbare Komplexitaet.

Verbesserung:

- Zwei Modi einfuehren:
  - `Produktmodus`: nur stabile, benoetigte Funktionen
  - `KI-Labor`: Training, Benchmark, LoRA, FewShot, Teacher, Diagnostik
- KI-Labor optional per Setting aktivieren.
- Produktmodus muss ohne Sidecar/Modelle trotzdem sauber starten.

### Befund 4.6: Sanierungsvorschlaege haben gute Basis, brauchen aber Auditierbarkeit

Positiv:

- Es gibt Regeln, Produkte, Marktpreise und Devis-Strukturen.
- Sanierung ist nicht nur KI-Prompt, sondern hat regelbasierte Komponenten.

Risiko:

- Regeln, Preise und Produkte liegen verteilt.
- Ein spaeterer Anwender muss nachvollziehen koennen, warum eine Massnahme vorgeschlagen wurde.

Verbesserung:

Jeder Sanierungsvorschlag sollte speichern:

- erkannte Schaeden / VSA-Codes
- angewendete Regeln
- ausgeschlossene Massnahmen mit Grund
- erlaubte Massnahmen
- Kostenband
- Datenstand / Knowledge-Version
- KI-Modell und Prompt-Version, falls KI beteiligt war
- manuelle Aenderungen des Users

Fachlich wichtige Tests:

- Schacht != Haltung
- kein Schlauchliner fuer unpassende Geometrien
- DN-/Materialgrenzen
- Anschluss-/Robotersanierung nur bei passenden Codes
- Preisband muss plausibel bleiben

## 5. Programm schlanker gestalten

### Befund 5.1: Repository enthaelt sehr viel Ballast

Groesste Bereiche:

- `sidecar`: ca. 22,9 GB
- lokale `.dotnet_*` Caches: mehrere GB
- `.tmp`: fast 1 GB
- Root-Modellgewichte, z.B. `.pt` Dateien
- generierte Frames und Logs
- alte Skripte und historische Artefakte

Risiko:

- Git wird langsam.
- Suchlaeufe werden riesig.
- Backups werden schwer.
- Es ist unklar, was Code und was Laufzeitdaten sind.

Verbesserung:

- Modelle ausserhalb des Repos speichern, z.B. `C:\KI_BRAIN\models`.
- Repo enthaelt nur Manifest und Download-/Pruefskript.
- Generierte Frames, Logs, Runs und Caches in `.gitignore`.
- `sidecar/.venv` nie versionieren.
- Bestehende Laufzeitdaten in einen Archivordner ausserhalb des Repos verschieben.

### Befund 5.2: Alte oder seltene Funktionen als Kandidaten pruefen

Kandidaten zum Ausblenden, Archivieren oder Zusammenlegen:

- altes Projekt `src/AuswertungPro.Wpf` ist nicht in der Solution
- alte PowerShell-Tools im Root und unter `Services`
- `_legacy`
- Benchmark-Fenster
- VideoAnalysisPipelineWindow
- Teacher/FewShot/LoRA/Yolo-Retrain im normalen UI
- Hardware-Monitor in der normalen Hauptnavigation
- mehrere Kosten-/Offerten-/Sanierungsfenster mit aehnlichem Zweck

Verbesserung:

- Eine Liste "taeglich gebraucht" erstellen:
  - Projekt
  - Import
  - Haltungen/Schaechte
  - Video/Codierung
  - Sanierung
  - Export/Reports
  - Einstellungen
- Alles andere unter `Expertenmodus` oder aus dem Release entfernen.

### Befund 5.3: UI nicht mit Entwicklungsfunktionen ueberladen

Viele KI-Werkzeuge sind fuer dich als Entwickler wertvoll, aber fuer ein professionelles Anwenderprogramm stoeren sie.

Verbesserung:

- Standardoberflaeche schlank.
- KI-Training, Benchmarks, Modellvergleich und Diagnostik nur im Expertenmodus.
- Hardware-/VRAM-/Modellstatus in ein Diagnosefenster verschieben.

## 6. Optische Beurteilung von Layout und Aufbau

### Befund 6.1: Das Programm wirkt leistungsstark, aber zu technisch

Die aktuelle Optik wirkt wie ein Entwickler- oder KI-Cockpit. Das passt fuer Tests, aber weniger fuer ein professionelles Fachprogramm im Alltag.

Ursachen:

- viele Panels gleichzeitig sichtbar
- viele Statusanzeigen
- viele Farben
- technische Begriffe prominent
- Hardware-/KI-Diagnose in der Hauptoberflaeche
- grosse XAML-Dateien mit vielen lokalen Styles

Verbesserung:

- Hauptbildschirm ruhiger machen.
- Nur die wichtigsten Arbeitsablaeufe sichtbar zeigen.
- Diagnostik in ein separates Fenster.
- Weniger Neon-Farben, weniger technische Statusbloecke.

### Befund 6.2: Layout ist sehr dicht

Beispiele:

- `MainWindow.xaml`: 780 Zeilen
- `TrainingCenterWindow.xaml`: 1.061 Zeilen
- `PlayerWindow.xaml`: 1.504 Zeilen
- `PhotoMeasurementWindow.xaml`: sehr viele Toolbuttons und Inline-Icons

Risiko:

- Neue Nutzer fuehlen sich erschlagen.
- Wichtige Aktionen gehen zwischen Spezialfunktionen unter.
- UI wirkt weniger hochwertig, obwohl viel Funktion drin steckt.

Verbesserung:

- Pro Screen eine klare Hauptaufgabe:
  - Import: Dateien rein, Ergebnis raus
  - Codierung: Video + Codes + Befunde
  - Sanierung: Vorschlag + Kosten + Begruendung
  - Training: nur im KI-Labor
- Primaere Buttons klar hervorheben.
- Sekundaere Aktionen in Menues oder Toolbars.
- Einheitliche Abstaende und Buttonhoehen.

### Befund 6.3: Professionelles Zielbild

Empfohlenes Zielbild:

- ruhige Fachsoftware, nicht "Demo-Cockpit"
- Navigation links reduziert
- Arbeitsbereich gross und sauber
- Tabellen gut lesbar
- ein Akzentfarbton fuer wichtige Aktionen
- Statusanzeigen klein und kontextbezogen
- keine dauerhafte Hardware-Diagnose im Hauptscreen
- klare Sprache fuer Anwender

Konkrete UI-Massnahmen:

1. Design-System in `Theme.xaml`/`ThemeLight.xaml` konsequent erweitern.
2. Standard-Styles fuer:
   - PrimaryButton
   - SecondaryButton
   - IconButton
   - ToolbarButton
   - DataGrid
   - FormField
   - StatusBadge
   - SectionHeader
3. Inline-Farben aus grossen Views entfernen.
4. TrainingCenter optisch als Expertenfenster behandeln.
5. MainWindow auf Anwenderablauf statt Systemdiagnose ausrichten.

## Priorisierte Sanierungs-Roadmap

### Phase 1: Build und Tests stabilisieren

Ziel: Der aktuelle Stand muss wieder verlaesslich bauen.

Aufgaben:

1. UI-Buildfehler reparieren.
2. Pipeline-Tests von GPU-/Langzeittests trennen.
3. `dotnet build` und schnelle `dotnet test` als Standard gruen bekommen.

### Phase 2: Repository entmuellen

Ziel: Code und Laufzeitdaten trennen.

Aufgaben:

1. `.gitignore` fuer Modelle, Frames, Logs, Caches, venvs schaerfen.
2. `sidecar/.venv`, Modellgewichte und generierte Frames aus dem Repo nehmen.
3. Altes `AuswertungPro.Wpf`, `_legacy` und alte Skripte pruefen und archivieren.

### Phase 3: KI/Training aus UI herausziehen

Ziel: UI wird wieder UI.

Aufgaben:

1. QualityGate in Application/KI-Core verschieben.
2. KnowledgeBase-Schicht in Infrastructure verschieben.
3. BatchTrainingOrchestrator einfuehren.
4. TrainingCenterViewModel verkleinern.

### Phase 4: Datenbank robuster machen

Ziel: keine SQLite-Races, bessere Nachvollziehbarkeit.

Aufgaben:

1. Globaler KB-Writer.
2. `busy_timeout`, `foreign_keys`, ConnectionStringBuilder.
3. `TrainingRuns` und Provenance-Felder.
4. Schema-/Migrationstests.

### Phase 5: Produktmodus und Expertenmodus

Ziel: Programm wirkt schlanker.

Aufgaben:

1. Hauptnavigation reduzieren.
2. KI-Labor hinter Expertenmodus.
3. Benchmark/Teacher/FewShot/LoRA aus normalem Ablauf ausblenden.
4. Kosten/Offerte/Sanierung zusammenfuehren.

### Phase 6: Professionelles UI-Redesign

Ziel: weniger Entwickler-Cockpit, mehr Fachprogramm.

Aufgaben:

1. Einheitliche Styles.
2. Weniger Farben.
3. Weniger dauerhafte Statusanzeigen.
4. Bessere Priorisierung der Hauptaktionen.
5. Layout pro Arbeitsablauf statt pro Feature-Sammlung.

## Wichtigste konkrete Empfehlungen

1. Sofort: UI-Build reparieren.
2. Danach: Pipeline-Tests trennen, damit normale Tests schnell gruen sind.
3. Dann: Repo von Sidecar-Modellen, Frames und Caches entlasten.
4. Danach: `TrainingCenterViewModel` und `PlayerWindow` schrittweise entkoppeln.
5. Danach: KnowledgeBase mit einem zentralen Writer robuster machen.
6. Dann: Produktmodus/Expertenmodus einfuehren.
7. Zum Schluss: UI optisch vereinheitlichen und beruhigen.

## Gesamturteil

SewerStudio ist kein kleines Tool mehr. Es ist inzwischen eine umfangreiche Fachanwendung mit KI-Labor geworden. Die fachliche Substanz ist gut, aber die Struktur muss nachziehen.

Wenn du es schlanker und professioneller machen willst, ist die wichtigste Entscheidung: Nicht jede gute Entwicklerfunktion gehoert in die Hauptoberflaeche. Das stabile Kernprogramm sollte ruhig, schnell und klar sein. Das KI-Labor darf stark und technisch bleiben, aber es sollte vom normalen Arbeitsablauf getrennt werden.

