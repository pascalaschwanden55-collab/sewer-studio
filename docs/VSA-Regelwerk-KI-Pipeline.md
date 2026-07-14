# SewerStudio — Vollständiger Nachbau-Prompt (Master-Spezifikation)

> **Stand:** HEAD am 2026-06-20, Branch `feature/gis-karte`.
> **Was das ist:** Ein vollständiger, eigenständiger Auftrag, um das Programm **SewerStudio**
> (KI-gestützte automatische Kanalinspektion) an einem anderen Ort von Grund auf neu zu bauen.
> Teil A (Architektur) wurde aus dem **echten Quellcode** rekonstruiert (9 parallele
> Code-Lese-Agenten über alle Teilsysteme). Teil B ist das **verbindliche Fachregelwerk**
> (VSA-KEK 2020 / EN 13508-2 / VSA-Richtlinie 2023). Teil C ist die Bau- und Abnahme-Anleitung.
>
> **Achtung Rollen:** Dieses Dokument mischt bewusst drei Sorten Inhalt — **Ist-Zustand** (aus Code),
> **verbindliches Fachregelwerk** und **Empfehlungen/empirische Erkenntnisse**. Was was ist, steht in der
> **Status-Legende** unten. Nur **Teil A** beschreibt den realen HEAD-Code; **Teil B §14–§15** sind
> Erfahrungswerte/Empfehlungen und NICHT zwingend der aktuelle Code. Einzelne, noch nicht verifizierte
> oder per Default deaktivierte Punkte sind im Text markiert.

---

## Inhalt — alles in dieser einen Datei

> Diese Datei ist **selbsttragend**: Alles, was man zum Nachbau braucht, steht hier (Architektur +
> Fachregelwerk + Bau-Anleitung). `CLAUDE.md` und die Memory sind nur projektinterne Arbeitshilfen,
> nicht Teil des Nachbaus.

**Frontmatter**
- Wie dieser Prompt zu benutzen ist · **Status-Legende** (welcher Teil ist Ist / Fachregel / Empfehlung)
- **Teil 0 — Auftrag, Kontext, Prinzipien** (Auftrag · Projekt-Kontext · Hardware · Tech-Stack · Architektur-Prinzipien · Gesamt-Datenfluss · Geplant/NICHT implementiert · Wegweiser)

**TEIL A — Programm-Architektur** *(Ist-Zustand aus echtem Code)*
- A1 · Solution-Struktur, Projekt-Layering und Build/Test
- A2 · Domain-Modelle und VSA-Katalog/Manifest
- A3 · VSA-Codierungslogik, Zustandsbewertung und QualityGate
- A4 · C#-KI-Pipeline-Services, Dedup und Quantifizierung
- A5 · Python-FastAPI-Sidecar
- A6 · Ollama / Qwen-Integration
- A7 · KnowledgeBase, Self-Training und Eval
- A8 · WPF-UI-Architektur
- A9 · Import/Export und Datenformate

**TEIL B — Fachdomäne / VSA-Regelwerk** *(§0–§13 verbindlich · §14–§15 = empirische Empfehlungen)*
- §0 Quellen · §1 Grundbegriffe · §2 Code-Struktur · §3 Steuercodes/Grundgerüst
- §4 Vollständiger Kanal-Schadenskatalog · §5 Schacht-Codes (D*) · §6 Quantifizierung
- §7 Uhrlage · §8 Distanzmessung · §9 Punkt- vs. Streckenschaden · §10 Gegenbefahrung
- §11 Zustandsbewertung (VSA 2023, vollständige Tabellen) · §12 Aufnahmetechnik · §13 Datenformate
- §14 KI-Pipeline-Erkenntnisse *(empirisch/Empfehlung)* · §15 Anti-Pattern

**TEIL C — Nachbau & Abnahme**
- Empfohlene Bau-Reihenfolge · Harte Invarianten (must-not-break) · Abnahme-Checkliste · Bekannte Doku-/Code-Diskrepanzen

**TEIL D — Offene Punkte & Backlog** *(externe Reviews; eval-gated, inkl. bewusst verworfener Vorschläge)*
- D1 Jetzt · D2 Später (eval-gated) · D3 Bewusst NICHT übernommen · D4 Schon vorhanden

---

## Wie dieser Prompt zu benutzen ist

Du (Mensch oder KI) sollst damit **SewerStudio neu bauen**. Regeln:

1. **Teil A ist die Bau-Spezifikation.** Sie nennt pro Teilsystem die exakten Verträge,
   Reihenfolgen, Schwellen und Fallstricke. Baue genau das nach — nicht mehr, nicht weniger.
2. **Teil B ist die fachliche Wahrheit.** Jede Codier-, Quantifizierungs- und Bewertungsentscheidung
   muss mit Teil B konsistent sein. Bei Widerspruch zwischen „schönem Code" und Fachregel gewinnt die Fachregel.
3. **Erfinde keine Features**, die als „nicht implementiert / geplant" markiert sind (z. B. ByteTrack,
   automatische 8B→32B-Eskalation). Sie gehören NICHT in den Ist-Nachbau.
4. **Halte das Thin-AI-Prinzip ein:** Die gesamte Geschäftslogik (VSA-Mapping, Dedup, Quantifizierung,
   QualityGate, Zustandsbewertung) liegt in C#. Das LLM/VLM liefert nur Text bzw. striktes JSON.
5. **Sprache:** Code-Kommentare, UI-Texte und Commit-Messages auf Deutsch.
6. **Reihenfolge & Abnahme** stehen in Teil C. Arbeite sie der Reihe nach ab.

### Status-Legende (welcher Teil ist was — wichtig)

| Teil | Rolle | Verbindlichkeit |
|---|---|---|
| **Teil A** | **Ist-Zustand**, rekonstruiert aus dem echten HEAD-Code | Bau-Spezifikation (1:1 nachbauen) |
| **Teil B §0–§13** | **Verbindliches Fachregelwerk** (VSA-KEK / EN 13508-2 / VSA 2023) | fachlich bindend |
| **Teil B §14–§15** | **Empirische Erkenntnisse & Empfehlungen** | NICHT zwingend = aktueller Code; Erfahrungswerte/Leitplanken |
| **Teil C** | Bau-Reihenfolge, Invarianten, Abnahme | Anleitung |

Konventionen im Text: **„Default AUS"/„deaktiviert"** = im Code per Default ausgeschaltet (nicht Teil
des normalen Ablaufs). **„Smoke-Test offen"** = im Code vorbereitet, aber real noch nicht verifiziert.

---

## Teil 0 — Auftrag, Kontext, Prinzipien

### Auftrag in einem Satz

Baue eine **Windows-Desktop-Anwendung (WPF/.NET 10, MVVM)**, die Kanal-TV-Inspektionsvideos
und Fremdprotokolle einliest, mit einer lokalen KI-Pipeline (YOLO + Grounding DINO + SAM + optional
Qwen-VL über einen Python-Sidecar) Schäden erkennt, diese nach **VSA-KEK 2020 / EN 13508-2**
codiert und quantifiziert, den Zustand nach **VSA-Richtlinie 2023** bewertet, ein Prüfprotokoll
erzeugt und aus bestätigten Befunden eine selbstlernende Wissensdatenbank aufbaut.

### Projekt-Kontext

- **Zweck:** Automatisierte Kanalinspektion, ~3000 Videos aus Kanal-TV-Exporten.
- **Standards:** EN 13508-2, VSA-KEK; aktive Quelle `vsa_kek_2020_catalog_manifest.json` (680 Codes).
- **Entwickler:** Solo, kein kommerzielles Ziel — pragmatisch, keine Enterprise-Overhead-Lösungen.
- **Region:** Schweiz (Abwasser Uri), deutschsprachig.

### Hardware-Zielprofil

- Workstation: Intel Core Ultra 9 285K · NVIDIA RTX 5090 32 GB · 64 GB DDR5.
- **VRAM-Budget: max ~29 GB stabil, NIE alle Modelle gleichzeitig** in voller Größe.
- **Laptop-Mode / Workstation-Mode**-Abstraktion erhalten (kleinere Modelle bei wenig VRAM).
- RTX 5090 = `sm_120` → Python-Stack zwingend **CUDA cu128 (Nightly)**; cu121 zerstört die GPU-Pipeline.

### Tech-Stack (verbindlich)

| Schicht | Technik |
|---|---|
| App | WPF, **.NET 10** (`net10.0-windows10.0.19041`), MVVM via `CommunityToolkit.Mvvm` |
| DI | **Selbstgebauter** minimaler Container (`ServiceProvider : IServiceProvider`) — kein MS-Hosting/DI |
| Video | `LibVLCSharp` + `LibVLCSharp.WPF` + `VideoLAN.LibVLC.Windows` |
| Karte | `Mapsui.Wpf` + `SkiaSharp.Views.WPF` (Version gepinnt, s. Teil A) |
| PDF lesen | `UglyToad.PdfPig` (Custom-Build `1.7.0-custom-5`) + OCR-Fallback |
| PDF schreiben | `QuestPDF` |
| Excel | `ClosedXML` |
| HTML-Templates | `Scriban` |
| KnowledgeBase | SQLite via `Microsoft.Data.Sqlite` (+ `SQLitePCLRaw.bundle_e_sqlite3` gepinnt) |
| Firebird-Import | `FirebirdSql.Data.FirebirdClient` |
| Tests | **xUnit** |
| Sidecar | Python **FastAPI** + `uvicorn`, **PyTorch cu128**, `ultralytics`, `groundingdino-py`, `sam2` |
| LLM/VLM | **Ollama** lokal: `qwen3-vl` (Vision/Text) + `nomic-embed-text` (Embeddings) |

### Architektur-Prinzipien (NICHT brechen)

- **Thin-AI:** C# = alle Geschäftslogik; LLM/VLM nur Textgenerierung / striktes JSON.
- **Schichtung:** `Domain` (abhängigkeitsfrei) ← `Application` (Verträge/Logik) ← `Infrastructure`
  (I/O-Implementierungen) ← `UI` (WPF). Domain referenziert nichts, hat keine NuGet-Pakete.
- **Sidecar liefert nur Roh-Signale** (Boxen, Masken, Klassen, Telemetrie). Kein VSA-Wissen im Sidecar.
- **VRAM-Budget** strikt einhalten; statische Modellwahl nach VRAM, keine Laufzeit-Eskalation.
- **QualityGate Green/Yellow/Red** muss jeden KI-Befund durchlaufen.
- **Kein großes Refactoring** ohne expliziten Auftrag; neue Features als separate Services mit Interface.
- **Ehrlichkeit vor Schönfärberei:** `degraded`/Fehler nie als „sauberes Rohr" verbuchen; Eval-Set
  niemals kontaminieren; Trainingsdaten nie verlieren.

### Gesamt-Datenfluss (End-to-End)

```
[Import]  PDF / XTF(SIA405) / WinCan-DB3 / IBAK-Daten.txt / KINS  ──►  HaltungRecord + ProtocolDocument
                                                                          │
[Analyse] UI/Service ──► VideoAnalysisPipelineService ──► (Multi-Model ODER Ollama-Only)
            Multi-Model je Frame (C# orchestriert, Sidecar rechnet):
              Frame-Quality-Gate ─► YOLO-cls ─► (Bogen-Geometrie · Default AUS) ─► YOLO-Detect ─► Grounding DINO
              ─► SAM (Box→Maske) ─► Quantifizierung (mm/%/Uhrlage) ─► Nähe-Gate (DN-Kreis)
              ─► (Klassifikator-Code + Temporal-Voting) ─► optional Qwen-VL ─► framebasiertes Dedup
                                                                          │
[Fachlogik] VsaCodeResolver (Code-Mapping)  ─►  QualityGateService (Green/Yellow/Red)
            ─► VsaEvaluationService (EZ/ZN/DZ nach VSA 2023)  ─►  Protokoll-Eintrag
                                                                          │
[Lernen]  bestätigter Befund ─► TrainingSamplesStore (JSON) ─► Review ─► KnowledgeBase (SQLite+Embeddings)
          ─► Retrieval (Few-Shot)  •  Eval-Set (eingefroren, hash-verifiziert) als Messlatte
                                                                          │
[Export]  Excel (Vorlage) / CSV / PDF-Protokoll
```

### Geplant / NICHT implementiert (nicht als Ist-Zustand behandeln)

- **ByteTrack / OC-SORT / echtes Multi-Object-Tracking:** existiert NICHT. Dedup ist framebasiert (C#).
- **DetectionAggregator / meterbasierter Merge-Radius / Temporal Voting als eigenständiger Aggregator:**
  nicht als zentrale Architektur; es gibt nur `TemporalCodeVotingService` + `TemporalFindingDeduplicator`.
- **InferenceOrchestratorService in C#:** existiert nicht; GPU-Slots liegen im Sidecar (`gpu_manager.py`).
- **Automatische 8B→32B-Laufzeit-Eskalation pro Frame:** existiert NICHT. Modellwahl ist statisch nach VRAM.
- **Dedizierter Devis-/Excel-Export:** im HEAD nicht als eigener Service (Kostenexport
  läuft über Excel/CSV).

### Wegweiser durch dieses Dokument

- **Teil A — Programm-Architektur:** 9 Teilsysteme als Bau-Spezifikation (Struktur, Domain, VSA-Logik,
  C#-Pipeline, Sidecar, Ollama, KnowledgeBase/Training, UI, Import/Export).
- **Teil B — Fachdomäne / VSA-Regelwerk:** Code-Katalog, Codier-/Quantifizierungs-/Bewertungsregeln,
  empirische Pipeline-Erkenntnisse. **§14.3 gegen den realen Code korrigiert (SAM 2.1, DINO Swin-B).**
- **Teil C — Nachbau & Abnahme:** Bau-Reihenfolge, harte Invarianten, Abnahme-Checkliste, bekannte
  Doku-/Code-Diskrepanzen.

---

# TEIL A · PROGRAMM-ARCHITEKTUR — Bau-Spezifikation (aus echtem Code)

> Die folgenden 9 Abschnitte wurden direkt aus dem aktuellen Quellcode rekonstruiert. Sie sind
> die maßgebliche Bauanleitung pro Teilsystem (exakte Pfade, Klassen, Verträge, Schwellen, Fallstricke).

## A1 · Solution-Struktur, Projekt-Layering und Build/Test

Dieser Abschnitt beschreibt, wie die .NET-Lösung aufgebaut ist, welche Projekte in welcher Schicht liegen, welche NuGet-Pakete sie binden, wie die App startet und wie man baut/testet. Alle Angaben stammen aus dem HEAD-Zustand der `.csproj`/`.sln`-Dateien — nicht aus Dokumentation.

### Ziel-Framework und SDK (WICHTIG: weicht von Projekt-Doku ab)

- **SDK / Sprache:** `.NET 10` (real installiert: SDK `10.0.109`). `global.json` pinnt `"version": "10.0.108"` mit `"rollForward": "latestFeature"`. Die `CLAUDE.md` nennt "net8.0" — das ist **veraltet**; alle `.csproj` zielen tatsächlich auf `net10.0`.
- **Ziel-Frameworks pro Projekttyp:**
  - Domain / Application / Infrastructure: `net10.0`
  - UI (WPF, ausführbar): `net10.0-windows10.0.19041`, `<OutputType>WinExe</OutputType>`, `<UseWPF>true</UseWPF>`, `<PlatformTarget>x64</PlatformTarget>`, `<AssemblyName>SewerStudio</AssemblyName>`, `<Product>SewerStudio</Product>`
  - Tests (Infrastructure, Pipeline): `net10.0`; UI.Tests: `net10.0-windows10.0.19041` mit `UseWPF=true`
  - Tools: meist `net10.0` (Konsolen-`Exe`)
- **Globale Build-Eigenschaften** (`Directory.Build.props` im Repo-Root, gilt für ALLE Projekte):
  - `<LangVersion>latest</LangVersion>`
  - `<Nullable>enable</Nullable>`
  - `<ImplicitUsings>enable</ImplicitUsings>`
  - `<RestorePackagesWithLockFile>true</RestorePackagesWithLockFile>` — jedes Projekt führt eine `packages.lock.json` für reproduzierbare Restores (lokal aktualisiert der Restore die Lock-Datei automatisch; ein erzwungener Locked-Mode wäre nur CI-Sache).

### Lösung und Projekt-Layout

Haupt-Solution: **`AuswertungPro.sln`** (Repo-Root, `c:\Sewer-Studio_KI_4.4`). Visual-Studio-Format 12.00, VS17. Sie gruppiert die Projekte in drei Solution-Folders: `src`, `tests`, `tools`. (Daneben existieren pro Projekt einzelne `*.sln`-Dateien wie `src\AuswertungPro.Next.UI\AuswertungPro.Next.UI.sln` — diese sind Einzelprojekt-Hüllen und nicht der Einstieg.)

In `AuswertungPro.sln` enthalten:

| Solution-Folder | Projekt | Pfad |
|---|---|---|
| src | AuswertungPro.Next.Application | `src\AuswertungPro.Next.Application\` |
| src | AuswertungPro.Next.Domain | `src\AuswertungPro.Next.Domain\` |
| src | AuswertungPro.Next.Infrastructure | `src\AuswertungPro.Next.Infrastructure\` |
| src | AuswertungPro.Next.UI | `src\AuswertungPro.Next.UI\` |
| tests | AuswertungPro.Next.Infrastructure.Tests | `tests\AuswertungPro.Next.Infrastructure.Tests\` |
| tests | AuswertungPro.Next.Pipeline.Tests | `tests\AuswertungPro.Next.Pipeline.Tests\` |
| tests | AuswertungPro.Next.UI.Tests | `tests\AuswertungPro.Next.UI.Tests\` |
| tools | IliCatalogReader, VsaClassificationRuleBuilder, VsaShadowReport, SidecarE2eSmoke, EvalSetBenchmark, SelfTrainingHarness, ClassifierPilot, EvalSetManifestHasher, ClassifierDatasetBuilder | `tools\...` |

**Fallstrick:** Im Verzeichnis `tools\` liegen **30 Tool-Projekte**, aber nur **9** sind in `AuswertungPro.sln` referenziert. Die übrigen (z. B. `BefundMatcher`, `KbCodeCleanup`, `SewerStudioMcpServer`, `CadastreTableBuilder`, alle PDF-Analyzer) sind **nicht** Teil der Solution und werden einzeln per `dotnet run --project tools/<Name>` gebaut/ausgeführt. Wer "die ganze Lösung" baut, baut diese Tools NICHT mit.

### Schichten und Abhängigkeitsregeln (Clean-Architecture-artig)

Die ProjectReferences erzwingen eine strikte Schichtung (Pfeil = "referenziert / hängt ab von"):

```
UI  ──▶  Infrastructure  ──▶  Application  ──▶  Domain
 │                                              ▲
 ├──────────────────────────────────────────────┤
 └──▶ Application ──▶ Domain   (UI referenziert alle drei direkt)
```

- **Domain** (`AuswertungPro.Next.Domain`): KEINE Projektabhängigkeiten, KEINE NuGet-Pakete. Reines Kern-Modell. Ordner: `Models/`, `Protocol/`, `Vsa/`, `VsaCatalog/`. Hier liegen Records/Enums der Fachdomäne (Haltung, Schacht, VSA-Codes/Katalog).
- **Application** (`AuswertungPro.Next.Application`): referenziert nur `Domain`. Enthält Interfaces, Ausgabeoptionen und Geschäftslogik-Verträge (Thin-AI-Prinzip: C# = Geschäftslogik). Ordner u. a.: `Ai/`, `Common/`, `Diagnostics/`, `Export/`, `Import/`, `Media/`, `Projects/`, `Protocol/`, `Reports/`, `Vsa/`. Keine konkrete PDF-Bibliothek wird hier referenziert.
- **Infrastructure** (`AuswertungPro.Next.Infrastructure`): referenziert `Domain` + `Application`. Implementiert die Interfaces (Persistenz, Import/Export, QuestPDF-Renderer, KI-Sidecar-Client, KnowledgeBase). Ordner u. a.: `Ai/` (Pipeline, KnowledgeBase, Ollama, Training, Sanierung), `Costs/`, `Export/`, `HoldingDistribution/`, `Import/` (Pdf, Xtf, WinCan, Ibak, Kins), `Map/`, `Media/`, `Output/`, `Projects/`, `Reports/`, `Vsa/`.
- **UI** (`AuswertungPro.Next.UI`, Assembly `SewerStudio.exe`): referenziert `Domain` + `Application` + `Infrastructure` direkt. WPF/MVVM. Ordner: `Views/` (mit `Controls/`, `Pages/`, `Windows/`), `ViewModels/`, `Theme/`, `Controls/`, `Dialogs/`, `Player/`, `Mapping/`, `Hydraulik/`, `LiveControl/`, `Services/`, `Ai/`, `Helpers/`, `Logging/`, `Data/`, `Config/`, `Templates/`, `Assets/`.

**Regel zum Nachbauen:** Domain bleibt abhängigkeitsfrei; Geschäftslogik/Interfaces in Application; konkrete I/O-Implementierungen in Infrastructure; UI verdrahtet alles im Composition Root. Kein Microsoft-Hosting/DI-Paket — siehe nächster Abschnitt.

### Wichtigste NuGet-Pakete (pro Projekt, mit Versionen)

- **Application:** keine externen NuGet-Pakete.
- **Infrastructure:**
  - `QuestPDF 2026.2.0` (PDF-Erzeugung)
  - `ClosedXML 0.105.0` (Excel-Export)
  - `UglyToad.PdfPig 1.7.0-custom-5` (PDF-Parsing; **Custom-Build-Version**, kein Standard-NuGet — Fallstrick beim Nachbau, eigene Feed-/Paketquelle nötig)
  - `Microsoft.Playwright 1.50.0` (Headless-Browser, z. B. HTML→PDF/Render)
  - `Microsoft.Extensions.Logging.Abstractions 10.0.2`
  - `Scriban 7.2.0` (HTML-Templates `*.sbnhtml`)
  - `Microsoft.Data.Sqlite 10.0.3` + `SQLitePCLRaw.bundle_e_sqlite3 3.0.3` (KnowledgeBase-SQLite; die 3.0.3 ist **explizit gepinnt** gegen die Sicherheitslücke NU1903/GHSA-2m69-gcr7-jv3q in der transitiven 2.1.11 — diesen Pin nicht entfernen)
  - `FirebirdSql.Data.FirebirdClient 10.0.0` (Firebird-DB-Import, z. B. WinCan/Kins)
- **UI:**
  - `CommunityToolkit.Mvvm 8.4.0` (MVVM: `ObservableObject`, `RelayCommand`)
  - `LibVLCSharp 3.9.5` + `LibVLCSharp.WPF 3.9.5` + `VideoLAN.LibVLC.Windows 3.0.23` (Video-Player im PlayerWindow)
  - `Mapsui.Wpf 5.1.0` + `SkiaSharp.Views.WPF 3.119.4` (GIS-Karte). **Fallstrick:** `SkiaSharp.Views.WPF` ist explizit auf `3.119.4` gepinnt, weil Mapsui sonst transitive net462-Assets zieht → unter .NET 10 weißer Bildschirm bei Pan/Drag. Dieser Pin und das TFM `windows10.0.19041` gehören zusammen.
  - `LibreHardwareMonitorLib 0.9.6` (Hardware-/VRAM-Anzeige, Laptop/Workstation-Mode)
  - außerdem `UglyToad.PdfPig 1.7.0-custom-5`, `Microsoft.Playwright 1.50.0`, `Microsoft.Data.Sqlite 10.0.3`, `Microsoft.Extensions.Logging 10.0.2`
- **Alle Test-Projekte:** `Microsoft.NET.Test.Sdk 17.10.0`, `xunit 2.7.0`, `xunit.runner.visualstudio 2.5.7` (Test-Framework = **xUnit**).
- **SewerStudioMcpServer** (Tool, nicht in der Solution): Konsolen-`Exe`, `AssemblyName SewerStudio.McpServer`, referenziert Domain/Application/Infrastructure; MCP-Server ohne externes MCP-NuGet (selbst implementiertes stdio-Protokoll).

### Mitkopierte Daten-/Config-Dateien (UI build copy)

Die UI kopiert beim Build viele Ressourcen ins Output (`CopyToOutputDirectory=PreserveNewest`) — beim Nachbau zwingend mitliefern, sonst startet die App nicht vollständig:
- `Data\classification_channels.json`, `Data\classification_manholes.json`, `Data\vsa_zustandsklassifizierung_2023_channels.json`, `Data\vsa_zustandsklassifizierung_2023_manholes.json`, `Data\seed_price_catalog.json`, `Data\vsa_kek_2020_catalog_manifest.json` (aktive VSA-Quelle), `Data\measure_templates.json`
- `Config\cost_catalog.json`, `Config\measure_templates.json`, `Config\position_templates.json`
- Excel-Vorlagen aus `..\..\Export_Vorlage\Haltungen.xlsx` / `Schächte.xlsx`
- Scriban-Templates `Templates\offer.sbnhtml`, `offer_profi.sbnhtml`, `cost_summary.sbnhtml`
- `Assets\Brand\**\*` (Logo/Icon; `abwasser-uri-logo.png` wird als Default-Fenstericon geladen)

### Einstiegspunkt und Startup-/Warmup-Reihenfolge

Einstieg: **`src\AuswertungPro.Next.UI\App.xaml`** + **`App.xaml.cs`** (`public partial class App : System.Windows.Application`). `App.xaml` mergt die Theme-Dictionaries (`Theme/ThemeLight.xaml`, `Theme/Controls.xaml`) und registriert die MVVM-`DataTemplate`s (ViewModel→View, z. B. `ProjectPageViewModel`→`ProjectPage`).

Ablauf in `App.OnStartup` (Reihenfolge ist load-bearing):
1. `ShutdownMode = OnExplicitShutdown`; Typografie-Defaults (`Segoe UI Variable Display…`, FontSize 14) und Default-Fenstericon registrieren.
2. `StartupSplashWindow` zeigen; `CodePagesEncodingProvider` registrieren (für Umlaute/Codepages).
3. `AppSettings.Load()`. Wenn `AiStartOnProgramStart`, vorab `AiStartupService.ApplyRuntimeDefaults(settings)`. Theme via `ThemeManager.ApplyTheme`.
4. **Logging:** Logverzeichnis `AppSettings.AppDataDir\logs`, alte Tageslogs >60 Tage (`LogRetentionDays`) löschen, Datei-Logger `app-yyyyMMdd.log` (eigener `FileLoggerProvider`).
5. Optional `LiveControlServer.TryStartFromEnvironment(...)` (HTTP-Steuerschnittstelle, vom MCP-Server genutzt).
6. **Composition Root:** `_services = new ServiceProvider(settings, diagnostics, logger, loggerFactory)` — ein **selbstgebauter, minimaler DI-Container** (`UI\ServiceProvider.cs`, implementiert `IServiceProvider`), der ALLE Services per `new` verdrahtet (kein `Microsoft.Extensions.DependencyInjection`). Zugriff app-weit über `App.Services`.
7. Globale Exception-Handler binden: `DispatcherUnhandledException`, `AppDomain.UnhandledException`, `TaskScheduler.UnobservedTaskException` → alle in `HandleException` (Fehlercode via `ErrorCodeGenerator`, Log, optional Dialog).
8. `base.OnStartup(e)` zuletzt aufrufen (damit DI bereit ist), dann `MainWindow` mit Opacity 0 zeigen.
9. Wenn `AiStartOnProgramStart`: **`StartAiOnStartupAsync` → `AiStartupService.StartAsync`** (KI-Hochfahren, siehe unten) als Fire-and-Forget.
10. Splash ausblenden (auf Fortschritt warten, Kappe 15 s), `MainWindow` auf Opacity 1 einblenden, `ShutdownMode = OnMainWindowClose`.

`OnExit`: `LiveControlServer` disposen, `AppSettings.FlushPendingSave()`.

**KI-Warmup-Reihenfolge** (`AiStartupService.StartAsync`, relevant fürs Nachbauen der Laufzeit):
1. Defaults erzwingen (`AiEnabled=true`, `PipelineMultiModelEnabled=true`, `PipelineMode="multimodel"`, `AiOllamaUrl=http://localhost:11434`, `PipelineSidecarUrl=http://localhost:8100`, `AiOllamaKeepAlive="24h"`).
2. **Ollama** prüfen (`GET /api/tags`); falls nicht erreichbar `ollama serve` starten und bis ~40 s (80×500 ms) auf Kaltstart warten (GPU-Discovery dauert).
3. Ollama-Modelle vorladen (Vision-/Text-Modell via `/api/generate`, Embed-Modell via `/api/embed`, mit `keep_alive`); danach via `/api/ps` verifizieren, dass Modelle resident sind (sonst einmal nachladen).
4. **Vision-Sidecar** prüfen (`GET /health` mit Header `X-Sidecar-Token`, Token aus Env `SEWER_SIDECAR_AUTH_TOKEN`/`SEWER_SIDECAR_TOKEN` oder `%LOCALAPPDATA%\SewerStudio\.sidecar_token`). Falls nicht erreichbar: `sidecar\start_sidecar.ps1` über PowerShell starten (per Verzeichnis-Aufstieg gesucht) und bis 120 s (240×500 ms) auf `/health` warten (TensorRT-Kaltstart).
5. **Sidecar-Modelle laden** via `POST /warmup`, bis zu 3 idempotente Versuche, erwartet werden `{yolo, classifier, dino, sam}`; 404 = alter Sidecar ohne `/warmup` → Abbruch ohne Retry.

`AppDataDir` = `%LOCALAPPDATA%\SewerStudio` (überschreibbar per Env `SEWERSTUDIO_APPDATA_DIR`). Produktidentität in `UI\AppIdentity.cs`: `ProductName="SewerStudio"`, `Version="4.4"`. Settings unter `AppDataDir\settings.json`.

### Build- und Test-Kommandos

Aus dem Repo-Root (`c:\Sewer-Studio_KI_4.4`):

```bash
# Build der gesamten Solution
dotnet build AuswertungPro.sln

# Alle Tests der Solution (xUnit)
dotnet test AuswertungPro.sln
```

Feiner granular:

```bash
# Nur App bauen / starten
dotnet build src/AuswertungPro.Next.UI/AuswertungPro.Next.UI.csproj
dotnet run  --project src/AuswertungPro.Next.UI            # bzw. in VS per F5

# Einzelne Testprojekte
dotnet test tests/AuswertungPro.Next.Infrastructure.Tests
dotnet test tests/AuswertungPro.Next.Pipeline.Tests
dotnet test tests/AuswertungPro.Next.UI.Tests

# Einzelnes Tool (viele Tools sind NICHT in der .sln)
dotnet run --project tools/<ToolName> -- <args>
```

Hinweise: SDK `.NET 10` muss installiert sein (`global.json` rollt auf neuestes 10.0-Feature). UI/UI.Tests bauen nur unter **Windows** (WPF, TFM `windows10.0.19041`). Tests = xUnit. Reproduzierbare Restores über `packages.lock.json` (durch `RestorePackagesWithLockFile=true`). Die App wird in der Praxis aus Visual Studio per F5 gestartet; das Skript `SewerStudio.bat` gibt nur die GPU frei, der Sidecar wird separat über `sidecar\start_sidecar.ps1` betrieben (vom `AiStartupService` automatisch angestoßen).

### Wichtigste `tools/` CLI-Werkzeuge (Zweck)

Konsolen-Tools, jeweils per `dotnet run --project tools/<Name> -- <args>`. Nur 9 davon sind in `AuswertungPro.sln`; der Rest steht standalone.

**KI-Evaluierung / Training:**
- `EvalSetBenchmark` — fährt die echte Pipeline über das eingefrorene Eval-Set (`EvalSetBenchmarkDataset.Load`), berechnet Metriken; kann Router-Datensatz bauen.
- `EvalSetManifestHasher` — erzeugt/aktualisiert `_manifest.json` mit SHA-256-Hashes des Eval-Sets (Integritäts-/Freeze-Schutz).
- `SelfTrainingHarness` — headless-Beweis des echten Self-Training-Orchestrators (alle Samples nur als Review-Kandidaten, nichts in die KB indexiert, keine Auto-Approve, keine YOLO-Dummy-Box).
- `ClassifierPilot` — fährt die echte Multi-Model-Pipeline (YOLO→DINO→SAM→Klassifikator→Qwen) headless über ein Video mit bekanntem PDF-Protokoll und vergleicht KI-Detektionen vs. Ground-Truth.
- `ClassifierDatasetBuilder` — baut aus `training_frames` (+ optional `gold_labels`) einen eval-freien YOLO-cls-Datensatz; doppelter Eval-Schutz (Dateiname UND SHA-256).
- `BefundMatcher` — dünnes CLI um den geteilten `Application.Ai.Evaluation.BefundMatcher`; rechnet ClassifierPilot-Reports mit gestufter Methode neu.
- `SidecarE2eSmoke` — End-to-End-Smoke-Test gegen den laufenden Sidecar.

**KnowledgeBase / Daten:**
- `KnowledgeBaseInspector` — KB-SQLite-Statistik (Top-Codes, Größe, Verteilung).
- `KbCodeCleanup` — normalisiert kaputte VSA-Codes in der Samples-Tabelle (nur UPDATE, kein DELETE; gleiche Logik wie der WinCan-Import-Fix).
- `FachwissenIndexer` — indexiert Fachwissen-Dokumente in die KB.
- `StammdatenExporter` — ruft den `StammdatenAggregator` (XTF+PDF+FDB) je Cadaster-Export auf und schreibt `haltungs_stammdaten.json`.
- `StageAExporter` — exportiert Stage-A-Trainingsdaten.

**VSA / Katalog / Kostensystem:**
- `VsaClassificationRuleBuilder` — baut Kanal-/Schacht-Klassifizierungsregeln.
- `VsaShadowReport` — wertet das Shadow-Log aus (KI- vs. Regel-Abweichungen, expected_drift).
- `VsaPdfExtractor` — extrahiert VSA-Katalog aus PDF nach JSON.
- `IliCatalogReader` — liest INTERLIS/ILI-Katalog.
- `AuswertungPro.MeasureCatalogCli` — Maßnahmen-/Kostenkatalog-CLI.

**PDF-/DB-Import-Diagnose:**
- `PdfCoverageAudit` — read-only Coverage-Check des echten `PdfProtocolExtractor` über alle PDFs; meldet PDFs mit 0 Befunden (nicht erkannte Formate), schreibt CSV.
- `PdfHeaderReader`, `DiagnosticPdfParser`, `IbakPdfAnalyzer`, `QuickPdfAnalyzer`, `PdfImageAnalyzer` (listet PDF-Bilder mit Maßen/ColorSpace), `AiDocPdf` — PDF-Inspektions-/Analyzer-Werkzeuge.
- `MdbSchemaReaderApp` (Assembly `MdbSchemaReader`) — liest MDB-Schema/Video-Mapping.
- `CadasterDbReader`, `CadastreTableBuilder` — bauen aus SIA405-XTF die eigenständige Haltungs-Tabelle und testen den Schacht-Paar-Nachschlag.
- `InspectionDateAudit` — beweist deterministisch über `D:\Haltungen`, wie viele Fälle der `yyyyMMdd`-Datums-Fix entsperrt.
- `DichtheitDistributeTest` — testet Dichtheits-Verteilung der KIT-Prüfberichte (mit/ohne Kataster-Abgleich).

**Sonstige:**
- `SewerStudioMcpServer` (Assembly `SewerStudio.McpServer`) — MCP-Server (stdio) mit read-only Funktionen wie `get_kb_summary`, `get_latest_benchmark`; spricht optional die `LiveControlServer`-HTTP-Schnittstelle der laufenden App an (Default `http://127.0.0.1:8765/`).

Relevante Pfade: Solution `c:\Sewer-Studio_KI_4.4\AuswertungPro.sln`; Composition Root `c:\Sewer-Studio_KI_4.4\src\AuswertungPro.Next.UI\ServiceProvider.cs`; Einstieg `c:\Sewer-Studio_KI_4.4\src\AuswertungPro.Next.UI\App.xaml.cs`; KI-Warmup `c:\Sewer-Studio_KI_4.4\src\AuswertungPro.Next.UI\Services\AiStartupService.cs`; globale Build-Props `c:\Sewer-Studio_KI_4.4\Directory.Build.props`; SDK-Pin `c:\Sewer-Studio_KI_4.4\global.json`.

## A2 · Domain-Modelle und VSA-Katalog/Manifest

Dieses Teilsystem definiert das gesamte **Datenmodell** (Projekt, Haltung, Schacht, Protokoll, VSA-Feststellungen) und das **VSA-KEK-Katalogsystem** (Single Source of Truth fuer alle Schadenscodes). Es ist reine Geschaeftslogik in C# (Thin-AI-Prinzip): es enthaelt keine UI- und keine LLM-Aufrufe. Zwei Assemblies sind beteiligt: `AuswertungPro.Next.Domain` (POCOs/Records/Enums, statischer Code-Baum) und `AuswertungPro.Next.Application.Protocol` (Katalog-Provider, Interfaces, Manifest-Bauer). Zielframework: net10.0 (Domain plattformunabhaengig).

### Architektur-Ueberblick / Datenfluss

- Ein `Project` haelt zwei `ObservableCollection`s: `Data` (Haltungen) und `SchaechteData` (Schaechte).
- Jeder `HaltungRecord` traegt seine Daten als String-`Dictionary` (`Fields`) plus pro Feld eine `FieldMetadata` (Herkunft + Userschutz). Zusaetzlich strukturierte `VsaFindings` (aus XTF, fuer Berechnung) und ein optionales `ProtocolDocument` (Beobachtungen mit Revisionshistorie).
- Der VSA-Katalog wird beim App-Start aus `vsa_kek_2020_catalog_manifest.json` geladen (read-only) und ueber `ICodeCatalogProvider` exponiert. Daraus baut `CodeCatalogSelectionCatalog` die UI-/Quantifizierungs-Sicht (`IVsaCodeSelectionCatalog`).
- Fallstrick: Es gibt **zwei** statische Code-Wahrheiten — den dynamischen Manifest-Katalog (Laufzeit, fuer App/Validierung) und einen hartcodierten `VsaCodeTree` (Domain, nur als strenger Eintrittsfilter fuer Trainingslabels). Beim Nachbau beide getrennt halten.

### Feld-Infrastruktur (Pfad: `Domain/Models/`)

`FieldSource` (enum, `FieldSource.cs`) — Herkunft eines Feldwerts mit Prioritaet (hoch -> niedrig): `Manual=10 > Pdf=7 > Ili=6 > Xtf405=5 > Xtf=3 > Legacy=1 > Unknown=0`. Die Numerik ist die Prioritaet; der Merge-Engine nutzt sie.

`FieldType` (enum, `FieldType.cs`): `Text, Multiline, Combo, Int, Decimal`.

`FieldDefinition` (record, `FieldDefinition.cs`): `(string Name, string Label, FieldType Type, IReadOnlyList<string>? ComboItems = null)`.

`FieldMetadata` (class, `FieldMetadata.cs`): `string FieldName`, `FieldSource Source` (default `Manual`), `bool UserEdited`, `DateTime LastUpdatedUtc`, `JsonObject? Conflict` (Konfliktdetails als roher JSON-Knoten).

`FieldCatalog` (static, `FieldCatalog.cs`) — 1:1-Port aus der alten PowerShell-Version. Enthaelt:
- `AppVersion = "0.1.0"`.
- `ColumnOrder` (34 Felder, feste Reihenfolge): `NR, Haltungsname, Strasse, Rohrmaterial, DN_mm, Nutzungsart, Haltungslaenge_m, Inspektionsrichtung, Primaere_Schaeden, Zustandsklasse, VSA_Zustandsnote_D, Pruefungsresultat, Referenzpruefung, Sanieren_JaNein, Empfohlene_Sanierungsmassnahmen, Kosten, Eigentuemer, Ausgefuehrt_durch, Bemerkungen, Link, Renovierung_Inliner_Stk, Renovierung_Inliner_m, Anschluesse_verpressen, Reparatur_Manschette, Linerendmanschette_LEM, Reparatur_Kurzliner, Erneuerung_Neubau_m, Offen_abgeschlossen, Datum_Jahr, VSA_Zustandsnote_S, VSA_Zustandsnote_B, Gewaesserschutz, Grundwasserspiegel, FunktionHierarchisch`.
- `Definitions` (Dictionary `FeldName -> FieldDefinition`) mit Label + Typ je Feld (z.B. `DN_mm`=Int, `Haltungslaenge_m`=Decimal, `Primaere_Schaeden`=Multiline, `Rohrmaterial`=Combo).
- ComboItems-Listen (jeweils mit fuehrendem `""`-Leereintrag), u.a.: `Rohrmaterial` (PVC, PE, PP, GFK, Beton, Steinzeug, Guss, ...), `Nutzungsart` (Schmutzwasser, Regenwasser, Mischabwasser), `Inspektionsrichtung` (In Fliessrichtung, Gegen Fliessrichtung), `Zustandsklasse` (0–5), `Sanieren_JaNein`/`Referenzpruefung` (Ja/Nein), `Gewaesserschutz` (S, Au, Zu, Ao), `Grundwasserspiegel` (unterhalb, oberhalb, unbekannt), `FunktionHierarchisch` (PAA.Sammelkanal, PAA.Hauptsammelkanal, ...).
- API: `Get(fieldName)` (faellt auf `FieldDefinition(name, name, Text)` zurueck), `GetComboItems(fieldName)`.

### Kern-Datensaetze

`HaltungRecord` (`Models/HaltungRecord.cs`, `sealed`, `INotifyPropertyChanged`):
- `Guid Id` (auto), `Dictionary<string,string> Fields` (`StringComparer.Ordinal`), `Dictionary<string,FieldMetadata> FieldMeta` (Ordinal), `List<VsaFinding> VsaFindings`, `ProtocolEntry? ProtocolEntry` (Legacy-Einzeleintrag), `ProtocolDocument? Protocol`, `DateTime CreatedAtUtc/ModifiedAtUtc`.
- Konstruktor initialisiert **alle** Felder aus `FieldCatalog.ColumnOrder` mit `""` und `FieldMetadata{ Source=Manual, UserEdited=false }`.
- `string GetFieldValue(string fieldName)` — leerstring-sicher.
- `void SetFieldValue(string fieldName, string? value, FieldSource source, bool userEdited)` — **Userschutz-Regel**: wenn vorhandenes Meta `UserEdited==true` und der neue Schreibvorgang `userEdited==false` ist, wird der Wert NICHT ueberschrieben (frueher Return). Sonst: Wert setzen, Meta aktualisieren (Source/UserEdited/LastUpdatedUtc), `ModifiedAtUtc` setzen, und `PropertyChanged` fuer `Fields`, `Fields[<name>]` und `ModifiedAtUtc` feuern (damit das DataGrid sofort aktualisiert). Wichtig: Import-/Prioritaetsentscheidungen liegen NICHT hier, sondern im MergeEngine — dieser Setter schuetzt nur user-editierte Werte.

`SchachtRecord` (`Models/SchachtRecord.cs`): schlanker als Haltung — `Guid Id`, `Dictionary<string,string> Fields` (Ordinal), optional `ProtocolDocument? Protocol`, Timestamps. `SetFieldValue(fieldName, value?)` **ohne** Source/UserEdited (Schaechte tragen keine FieldMeta).

`Project` (`Models/Project.cs`, `sealed`):
- `int Version=2`, `string Name="Neues Projekt"`, `Description`, `Guid Id`, Timestamps, `AppVersion=FieldCatalog.AppVersion`.
- `Dictionary<string,string> Metadata` (Ordinal), `ObservableCollection<HaltungRecord> Data`, `ObservableCollection<SchachtRecord> SchaechteData`, `List<JsonObject> ImportHistory`, `List<JsonObject> Conflicts`, `[JsonIgnore] bool Dirty` (Laufzeit-Flag, bewusst nicht serialisiert).
- `EnsureMetadataDefaults()` legt feste Metadata-Schluessel an (`Zone, Gemeinde, Strasse, FirmaName, FirmaAdresse, FirmaTelefon, FirmaEmail, Bearbeiter, Auftraggeber, AuftragNr, InspektionsDatum, Sanieren, Eigentuemer`); validiert `Eigentuemer` gegen {AWU, Privat, Gemeinde, Kanton, Bund} (Default Privat) und `Sanieren` gegen {Ja, Nein} (Default Nein).
- `EnsureRecordDefaults()` (Migrationslogik): fuellt fehlende Felder/Meta auf, migriert Legacy `ProtocolEntry` -> `ProtocolDocument` (Original + Arbeitskopie `Current`), und migriert Altfeld `Fliessrichtung` -> `Inspektionsrichtung`.
- `CreateNewRecord()` vergibt automatisch die naechste `NR` (max+1) wie in der PS-Version. `AddRecord/RemoveRecord/GetRecord` setzen `Dirty=true`.

### Protokoll-Modell (Pfad: `Domain/Protocol/ProtocolModels.cs`)

Enums: `ProtocolEntrySource { Imported, Manual, Ai }`, `ProtocolChangeKind { Add, Edit, Delete, Restore, Reorder, AttachPhoto, DetachPhoto }`.

`ProtocolEntry` (eine codierte Beobachtung): `Guid EntryId`, `string Code`, `string Beschreibung`, `double? MeterStart`, `double? MeterEnd`, `bool IsStreckenschaden`, `string? Mpeg`, `TimeSpan? Zeit`, `List<string> FotoPaths`, `ProtocolEntrySource Source` (default Manual), `bool IsDeleted`, optional `ProtocolEntryCodeMeta? CodeMeta`, optional `ProtocolEntryAiMeta? Ai`.

`ProtocolEntryCodeMeta` (Parametrisierung): `string Code`, `Dictionary<string,string> Parameters` (OrdinalIgnoreCase), `string? Severity`, `int? Count`, `string? Notes`, `DateTimeOffset UpdatedAt`.

`ProtocolEntryAiMeta` (Human-in-the-loop): `string? SuggestedCode`, `double Confidence`, `string? Reason`, `List<string> Flags`, `bool Accepted`, `string? FinalCode`, `string? MeterSource`, `bool IsMeterEstimated`, `DateTimeOffset SuggestedAt`.

`ProtocolChange`: `DateTimeOffset At`, `string? User`, `ProtocolChangeKind Kind`, `Guid EntryId`, `string? Before`, `string? After`.

`ProtocolRevision`: `Guid RevisionId`, `Guid? BasedOnRevisionId`, `DateTimeOffset CreatedAt`, `string? CreatedBy`, `string? Comment`, `List<ProtocolEntry> Entries`, `List<ProtocolChange> Changes`.

`ProtocolDocument`: `string HaltungId`, `ProtocolRevision Original`, `ProtocolRevision Current`, `List<ProtocolRevision> History`. Muster: `Original` = importierter/initialer Stand, `Current` = editierbare Arbeitskopie, `History` = aeltere Revisionen.

### VSA-Feststellungen und -Bewertung (Pfad: `Domain/Models/` und `Domain/Vsa/`)

Fallstrick: Es existieren **zwei** Klassen namens VsaFinding in verschiedenen Namespaces — beim Nachbau bewusst unterscheiden:
- `AuswertungPro.Next.Domain.Models.VsaFinding` (`Models/VsaFinding.cs`, von `HaltungRecord.VsaFindings` benutzt): `string KanalSchadencode`, `string? Quantifizierung1/2`, `double? SchadenlageAnfang/Ende` (Uhrlage), `double? LL`, `string? Raw`, plus WinCan/Overlay-Felder `double? MeterStart/MeterEnd`, `string? MPEG`, `DateTime? Timestamp`, `string? FotoPath`, plus VSA-Auswertungsnoten `int? EZD/EZS/EZB` (Einzelzustand pro Anforderung).
- `AuswertungPro.Next.Domain.Vsa.VsaCodeFinding` (`Vsa/VsaFinding.cs`, getrennte Parser-/Editor-Sicht): `string Code`, `string? Ch1`, `string? Ch2`, `string? QuantUnit`, `double? QuantValue`, `double? Length_m`, `string Raw`, plus Meter/MPEG/Timestamp/FotoPath.

VSA-Bewertung (`Domain/Vsa/`):
- `VsaRequirement` (enum): `Dichtheit` (D), `Standsicherheit` (S), `Betriebssicherheit` (B).
- `VsaClassificationResult` (record): `(int? EZD, int? EZS, int? EZB)` — Einzelzustaende 0..4 oder null (nicht klassifizierbar).
- `VsaConditionResult`: `VsaRequirement Requirement`, `double? Zustandsnote` (0.00–4.00), `double? Abminderung` (A), `int? WorstEinzelzustand` (EZmin), `double? Dringlichkeitszahl` (DZ), `List<string> Notes`.

### VSA-Katalog: Manifest, Provider, Auswahl-Sicht

**Datendatei** `vsa_kek_2020_catalog_manifest.json` liegt unter `src/AuswertungPro.Next.UI/Data/` (wird ins Output (`<Output>/Data/`) kopiert; Dateiname-Konstante `VsaKekManifestFileName` in `ServiceProvider.cs`). Im HEAD: `version=1`, 680 Codes. Quellen-Verteilung: `VSA-KEK-2020-ILI` 657, `VSA-KEK-2020-Heading` 6, `VSA-KEK-2020-ICM` 16, `VSA-XTF-Observed` 1; 678 selektierbar, 190 mit Parametern.

**Manifest-JSON-Struktur** (Wurzel: `{ "version": int, "codes": [...] }`). Jeder Eintrag = `CodeDefinition`:
```json
{
  "code": "AECXA", "title": "Rohrprofilwechsel", "canonicalCode": "AECXA",
  "source": "VSA-KEK-2020-ILI", "isObservedExtension": false, "isSelectable": true,
  "standardAnnotation": null, "group": "VSA-KEK 2020/Kanal/AEC",
  "description": "Rohrprofilwechsel", "categoryPath": ["VSA-KEK 2020","Kanal","AEC"],
  "parameters": [], "examples": [],
  "requiresRange": false, "rangeThresholdM": null, "rangeThresholdText": null
}
```

**Modelltypen** (`Application/Protocol/JsonCodeCatalogProvider.cs`, alle mit `[JsonPropertyName]` camelCase):
- `CodeCatalogDocument`: `int Version=1`, `List<CodeDefinition> Codes`.
- `CodeDefinition`: `Code`, `Title`, `CanonicalCode?`, `Source?`, `bool IsObservedExtension`, `bool IsSelectable=true`, `StandardAnnotation?`, `Group="Unbekannt"`, `Description?`, `List<string> CategoryPath`, `List<CodeParameter> Parameters`, `List<string> Examples`, `bool RequiresRange`, `double? RangeThresholdM`, `string? RangeThresholdText`.
- `CodeParameter`: `Name`, `DataKey?`, `Type="string"` (Werte u.a. `number`, `clock`, `string`), `List<string>? AllowedValues`, `Unit?`, `bool Required`.

**Interface** `ICodeCatalogProvider` (Vertrag, `JsonCodeCatalogProvider.cs`):
```csharp
IReadOnlyList<CodeDefinition> GetAll();
bool TryGet(string code, out CodeDefinition def);
void Save(IReadOnlyList<CodeDefinition> codes);
IReadOnlyList<string> AllowedCodes();
IReadOnlyList<string> Validate(IReadOnlyList<CodeDefinition>? codes = null);
```
Zentrale Regeln aller Provider: Codes werden via `NormalizeCode` (Trim + `ToUpperInvariant`) normalisiert; Vergleiche `OrdinalIgnoreCase`. `AllowedCodes()` liefert nur `IsSelectable && !IsObservedExtension`, distinct, alphabetisch sortiert — das ist die Whitelist gueltiger Codes fuer KI-Mapping und UI.

**Provider-Implementierungen** (`Application/Protocol/`):
- `ManifestCodeCatalogProvider(catalogPath)` — laedt das Manifest (`PropertyNameCaseInsensitive`, Kommentare/Trailing-Commas erlaubt). **Read-only**: `Save(...)` wirft `InvalidOperationException`. Bei fehlender Datei: leere Liste + `LastLoadErrors`. Beim Laden Dedup nach Code (`DeduplicateCodes`, erste Definition gewinnt, Duplikate als Warnung). Exponiert `LastLoadErrors`/`LastLoadWarnings`.
- `JsonCodeCatalogProvider(catalogPath)` — schreibbare JSON-Variante (`WriteIndented`); `EnsureCatalogExists` legt leeren Katalog an; `Save` validiert vor dem Schreiben; Dedup waehlt bei Konflikt die "reichere" Definition (`Score`: Titel != Code +3, Description +2, Group +1, CategoryPath/Parameters/Examples gewichtet).
- `XmlCodeCatalogProvider` — liest WinCan-/XML-Kataloge (Fallback-Quelle).
- `SourceDecoratingCodeCatalogProvider(inner, source)` — setzt fehlendes `Source`/`CanonicalCode` (im HEAD genutzt fuer `WinCan-Fallback`).
- `CompositeCodeCatalogProvider(IReadOnlyList<...>)` — merged mehrere Provider; **erster Provider gewinnt** bei gleichem Code (`TryAdd`). `Save` setzt nur einen In-Memory-Override. `GetWarnings()` aggregiert Provider-Warnungen.

**Wiring** (`UI/ServiceProvider.CreateCodeCatalog`): baut die Provider-Liste in Prioritaetsreihenfolge: zuerst `ManifestCodeCatalogProvider` (wenn Datei existiert), dann je XML-Pfad ein `SourceDecoratingCodeCatalogProvider(XmlCodeCatalogProvider(...), "WinCan-Fallback")`, alles in einen `CompositeCodeCatalogProvider`. Das Manifest hat also Vorrang vor WinCan-Fallback-Codes. Exponiert als `CodeCatalog` (Typ `ICodeCatalogProvider`) im DIY-ServiceProvider (`GetService`).

**Quellen-Konstanten** (`VsaKekCatalogBuilder.cs`, Klasse `VsaKekCatalogSources`): `Ili="VSA-KEK-2020-ILI"`, `Icm="VSA-KEK-2020-ICM"`, `Heading="VSA-KEK-2020-Heading"`, `XtfObserved="VSA-XTF-Observed"`, `WinCanFallback="WinCan-Fallback"`.

### Manifest-Generierung (`Application/Protocol/VsaKekCatalogBuilder.cs`)

Das Manifest ist generiert, nicht handgepflegt. `VsaKekCatalogBuilder.Build(iliText, sectionIcmText?, manholeIcmText?, observedXtfTexts?)` erzeugt ein `CodeCatalogDocument` aus den offiziellen VSA-KEK-2020-Quellen:
- Parst die ILI-Enums `KanalSchadencode` und `SchachtSchadencode` (Code + Kommentartitel) -> Source `Ili`, `Group="VSA-KEK 2020/{Kanal|Schacht}/{BaseCode}"`.
- Parst ICM-XML (`artistStation`/`setAttribute`) zu Quantifizierungs-/Positions-/Connection-Regeln (`Q1`, `Q2`, `Pos1/Pos2`, `Connection`) mit Praesenz `None|Optional|Required`.
- Fallback-Pflichtregeln (hardcoded): `RequiredChannelQ1` = {BAB, BAC, BAG, BAI, BAJ, BBA, BBB, BBC, BCA, BDD}; `OptionalChannelQ2` = {BCA}; `RequiredManholeQ1` = {DCA, DCG}; `OptionalManholeQ2` = {DCA, DCG}.
- Ergaenzt offizielle Hauptcode-Titel (`OfficialBaseCodeTitles`, z.B. BAB="Riss", BAC="Leitungsbruch / Einsturz", BAF="Oberflächenschaden", BCA="Seitlicher Anschluss") und Headings (`OfficialChannelHeadings`, u.a. BAA="Verformung", BCC="Bogen") fuer Codes, die in der ILI-Enum nur Untercodes besitzen — sonst wuerde z.B. "BCC" roh statt "Bogen" angezeigt.
- Parameter werden via `ApplyParameters` angehaengt: `Quantifizierung 1` (DataKey `Q1`, Type `number`), `Quantifizierung 2` (`Q2`), `Uhrlage Anfang/Ende` (DataKeys `SchadenlageAnfang`/`SchadenlageEnde`, Type `clock`), `Verbindung` (`Connection`).
- `VsaKekCatalogArchiveReader` liest die Quelltexte per `tar -xOf` aus dem WinCan-Archiv (feste Entry-Pfade fuer ILI/Section-ICM/Manhole-ICM).

### Auswahl-/Quantifizierungs-Sicht (`Application/Protocol/IVsaCodeSelectionCatalog.cs`)

`IVsaCodeSelectionCatalog` transformiert die flache `CodeDefinition`-Liste in eine UI-taugliche Gruppen-/Hierarchie-Sicht:
```csharp
IReadOnlyDictionary<string, GroupDef> Groups { get; }
(QuantField? Q1, QuantField? Q2) GetQuantRule(string codeKey, string? char1Key);
ClockRule GetClockRule(string codeKey);
IReadOnlyDictionary<string,string>? GetChar2Options(VsaCodeDef codeDef, string char1Key);
bool IsInvalidCombo(VsaCodeDef codeDef, string char1Key, string char2Key);
```
- `EmptyVsaCodeSelectionCatalog.Instance` — Null-Object (leere Gruppen, ClockRule `none`).
- `CodeCatalogSelectionCatalog(ICodeCatalogProvider)` — baut die Sicht im Konstruktor (`Build`): gruppiert selektierbare Codes nach 2-Buchstaben-Praefix; `CreateGroup` mappt feste Labels/Farben/Icons (`BA`="Baulicher Zustand"/#DC2626, `BB`="Betrieblicher Zustand"/#F59E0B, `BC`="Anschluesse/Reparaturen"/#2563EB, `BD`, `AE`, `DA`–`DD`). Quant-Regeln (`Pflicht` "P"/"O"/"V") und Clock-Regeln (Mode `range`/`none`, Hint) werden aus `Parameters` abgeleitet (`SchadenlageAnfang/Ende` => Clock-Range).

**VSA-Katalog-Records** (`Domain/VsaCatalog/VsaCatalogModels.cs`, Records):
- `GroupDef(string Label, string Color, string Icon, Dictionary<string,VsaCodeDef> Codes)`.
- `VsaCodeDef`: `Label`, `FinalCode?`, `bool IsSteuer`, `Note?`, `Warn?`, `Source?`, `CanonicalCode?`, `StandardAnnotation?`, `bool XPrefix`, `Dictionary<string,CharDef>? Char1`, `Dictionary<string,string>? Char2`, `Dictionary<string,Dictionary<string,string>>? Char2PerChar1`, `Dictionary<string,HashSet<string>>? Invalid`, `bool AllValid`.
- `CharDef`: `Label`, optional eigenes `Char2`.
- `QuantRule`: `Q1?`, `Q1PerChar1?`, `Q2?`; `QuantField`: `Pflicht` ("O"/"P"/"V"), `Einheit?`, `Label?`, `Min?`, `Max?`, `Hint?`; `ClockRule`: `Mode` ("range"/"none"), `Hint?`.

### Statischer Code-Baum + strenger Validator (`Domain/VsaCatalog/`)

Getrennt vom dynamischen Manifest existiert ein **hartcodierter** Baum `VsaCodeTree.Groups` (`VsaCodeTree.cs`, EN 13508-2 / VSA-KEK 2018-Stand): Hierarchie Gruppe -> Hauptcode -> Char1 -> Char2 (z.B. `BA`="Struktur der Rohrleitungen", `BAA`="Verformung", `BAB`="Risse" mit Char1 A/B/C und Char2 A–E `AllValid=true`, `BAF` mit `Invalid`-Kombinationsregeln). Dieser Baum dient ausschliesslich `VsaCodeValidator`.

`VsaCodeValidator` (`VsaCodeValidator.cs`, static) — strenger Eintrittsfilter fuer Trainingslabels aus freiem PDF-Text (UI/KI-Resolver duerfen toleranter sein):
- `bool IsKnownCode(string?)`: normalisiert (Punkte raus, Upper), prueft Regex `^[A-Z]{3,8}$`, dann ob die ersten 2 Zeichen eine bekannte Gruppe und die ersten 3 einen bekannten Hauptcode in `VsaCodeTree` sind.
- `string? TryNormalizeKnownCode(string?)`: behaelt nur Buchstaben (entfernt Punkt-Trenner `BCA.F.A` und Meter-Suffixe `BCD0.00.0`), begrenzt auf `MaxKnownCodeLength=5` (Hauptcode 3 + Char1 + Char2), liefert den Code nur bei bekanntem Hauptcode. Char1/Char2 selbst werden bewusst NICHT vollstaendig gegen den Katalog geprueft, um katalog-unbekannte echte Codes nicht zu verwerfen.

### Hilfs-/Result-Typen

`Result` / `Result<T>` (`Application/Common/Result.cs`, `sealed`, immutable): `Result` = `bool Ok`, `string? ErrorCode`, `string? ErrorMessage`; Factories `Result.Success()`, `Result.Fail(code, message)`. `Result<T>` zusaetzlich `T? Value`; Factories `Success(value)`, `Fail(code, message)`.

`ImportStats` / `ImportMessage` (`Infrastructure/Import/Common/ImportModels.cs`): `ImportStats` = `int Found, CreatedRecords, UpdatedRecords, UpdatedFields, Conflicts, Errors, Uncertain` plus `List<JsonObject> ConflictDetails` und `List<ImportMessage> Messages`. `ImportMessage` = `string Level` ("Info"/"Warn"/"Error"), `Message`, `Context`. Diese Typen sammeln Import-Ergebnisse beim Einlesen von PDF/XTF/WinCan in das oben beschriebene Datenmodell.

Relevante Dateipfade (absolut):
- `c:\Sewer-Studio_KI_4.4\src\AuswertungPro.Next.Domain\Models\` — `HaltungRecord.cs`, `SchachtRecord.cs`, `Project.cs`, `FieldCatalog.cs`, `FieldDefinition.cs`, `FieldMetadata.cs`, `FieldSource.cs`, `FieldType.cs`, `VsaFinding.cs`
- `c:\Sewer-Studio_KI_4.4\src\AuswertungPro.Next.Domain\Protocol\ProtocolModels.cs`
- `c:\Sewer-Studio_KI_4.4\src\AuswertungPro.Next.Domain\Vsa\` — `VsaFinding.cs`, `VsaRequirement.cs`, `VsaClassificationResult.cs`, `VsaConditionResult.cs`
- `c:\Sewer-Studio_KI_4.4\src\AuswertungPro.Next.Domain\VsaCatalog\` — `VsaCatalogModels.cs`, `VsaCodeTree.cs`, `VsaCodeValidator.cs`, `VsaObservationMap.cs`
- `c:\Sewer-Studio_KI_4.4\src\AuswertungPro.Next.Application\Protocol\` — `JsonCodeCatalogProvider.cs` (Interface + CodeDefinition/CodeParameter/CodeCatalogDocument), `ManifestCodeCatalogProvider.cs`, `CompositeCodeCatalogProvider.cs`, `IVsaCodeSelectionCatalog.cs`, `VsaKekCatalogBuilder.cs`
- `c:\Sewer-Studio_KI_4.4\src\AuswertungPro.Next.Application\Common\Result.cs`
- `c:\Sewer-Studio_KI_4.4\src\AuswertungPro.Next.Infrastructure\Import\Common\ImportModels.cs`
- `c:\Sewer-Studio_KI_4.4\src\AuswertungPro.Next.UI\Data\vsa_kek_2020_catalog_manifest.json` (Datenquelle, read-only, 680 Codes)
- `c:\Sewer-Studio_KI_4.4\src\AuswertungPro.Next.UI\ServiceProvider.cs` (Methode `CreateCodeCatalog`, Konstante `VsaKekManifestFileName`)

## A3 · VSA-Codierungslogik, Zustandsbewertung und QualityGate

Dieses Teilsystem nimmt rohe KI-Befunde (YOLO-Klassifikation, DINO-Boxen, Labels, Meterstand) und bildet daraus (1) einen normalisierten VSA-KEK-Code, (2) eine fachliche Zustandsbewertung nach VSA-Richtlinie 2023 (ZN/DZ/EZ pro Anforderung D/S/B) und (3) eine Vertrauensbewertung (QualityGate Green/Yellow/Red). Strikt Thin-AI: **die gesamte Geschaeftslogik liegt in C#**, das LLM/Sidecar liefert nur rohe Predictions und Text. Alle Klassen sind als reine, deterministische `static`- oder zustandslose Services gebaut und ohne UI-Abhaengigkeiten testbar.

### Schichtung und Dateien

- `src/AuswertungPro.Next.Infrastructure/Ai/VsaCodeResolver.cs` — `static class VsaCodeResolver`: Code-Normalisierung, Label-Lookup, Keyword→Code-Heuristik, Clock-Normalisierung, Sensor-Fusion YOLO+Meter+Import.
- `src/AuswertungPro.Next.Application/Ai/CodingDedupPolicy.cs` — `static class CodingDedupPolicy`: Live-Codier-Dedup, Einmal-Codes, BCE-Plausibilitaet, Rohrende-Meter-Korrektur.
- `src/AuswertungPro.Next.Application/Ai/MetrierungProximity.cs` + `MetrierungProximityEvaluator.cs` — Naehe-Gate / DN-Kreis (geometrisch, ob ein Befund nah genug zum Metrieren ist).
- `src/AuswertungPro.Next.Application/Vsa/IVsaEvaluationService.cs` + `src/AuswertungPro.Next.Infrastructure/Vsa/VsaEvaluationService.cs` — Zustandsbewertung (ZN/DZ/EZ/Randbedingungen B1–B4).
- `src/AuswertungPro.Next.Infrastructure/Vsa/Classification/VsaClassificationTable.cs` (Legacy-Engine) und `VsaClassificationRuleSelector`/`VsaClassificationRuleSet` (v2-Engine, datengetrieben aus JSON).
- `src/AuswertungPro.Next.Infrastructure/Ai/QualityGate/QualityGateService.cs` + `CategoryWeights.cs`; Vertraege in `src/AuswertungPro.Next.Application/Ai/QualityGate/EvidenceVector.cs`.
- Domaene: `Domain/Vsa/VsaConditionResult.cs`, `VsaRequirement.cs`.
- Fachreferenz (verbindlich, kein Code): `docs/VSA-Regelwerk-KI-Pipeline.md` (Formeln, Faktoren, Cheatsheet, 142-Regel-Klassifizierung).

### VsaCodeResolver — KI-Befund → VSA-Code

Einzige Quelle fuer Code-Normalisierung und Label-Lookup. Statisch konfiguriert per `ConfigureCatalog(ICodeCatalogProvider?)`; ohne Katalog werden Codes durchgereicht (`CatalogValidated` gibt dann den Code unvalidiert zurueck). `ICodeCatalogProvider.TryGet(code, out CodeDefinition)` liefert Titel und `RequiresRange` (Streckenschaden-Flag).

- **`NormalizeFindingCode(string? raw) → string?`**: Trim, Punkte entfernen, Uppercase. Akzeptanz nur wenn `^[A-Z]{2,8}$` und Laenge 2–8. Dann: (1) exakter Katalog-Lookup, sonst (2) Hauptcode = die ersten 3 Zeichen im Katalog vorhanden → voller Code akzeptiert. Sonst `null`.
- **`LookupLabel(code)`**: Fallback-Kette voller Code → 3-Zeichen → 2-Zeichen → `null`.
- **`IsStreckenschadenCode(code)`**: prueft Katalog-`RequiresRange` (exakt, dann Praefix-Abstieg), danach hartkodiertes Fallback-Set `StreckenschadenCodes` (z.B. `BABA…`, `BAFA…`, `BBA`, `BBB`, `BBC`, `BBD…`). Wichtig: dieses Set ist reine Strecken-Heuristik, **keine Fachbedeutungen** — Bedeutungen kommen aus dem Katalog.
- **`InferCodeFromLabel(string? label) → string?`**: Keyword-Heuristik (Umlaut-normalisiert, Substring `Has` vs. Wortgrenze `HasWord`). Mapping u.a.: anschluss/abzweig/lateral→`BCA`; bogen/bend/kruemmung→`BCC`; rohranfang/manhole→`BCD`; rohrende/endknoten→`BCE`; riss/crack→`BAB`; bruch/collapse→`BAC`; verformung/oval→`BAA`; muffe/versatz/offset→`BAJ`; wurzel/root→`BBA`; einragung/intrusion→`BAI`; korrosion/erosion→`BAF`; inkrustation/kalk/sinter→`BBB`; ablagerung/sediment/silt→`BBC`; Wasserspiegel/Rueckstau/standing water→`BDDC`. **Fallstrick (bewusst):** Normalfluss bzw. „trueb + Abwasser" OHNE Rueckstaubegriff → `null` (kein Wasserstands-Befund, verhindert Falschcodierung von normalem Abwasserfluss). Jeder Treffer laeuft durch `CatalogValidated` (verwirft Codes, die nicht im Katalog sind).
- **`NormalizeClock(string? raw) → string?`**: oben/scheitel/krone→`12:00`, unten/sohle→`6:00`, rechts→`3:00`, links→`9:00`, sonst Zahl 1–12 via Regex `\b(1[0-2]|0?[1-9])\b` → `"N:00"`.
- **`ResolveFromClassifier(predictions, currentMeter, totalLength, importContext?) → ResolvedCode?`** (Sensor-Fusion, Reihenfolge ist verbindlich):
  1. **BCD-Regel**: `currentMeter < 0.5` UND (Top-1=BCD ODER BCD-Conf > 0.20) → `BCD`, Conf `max(bcdConf, 0.80)`.
  2. **BCE-Regel**: `totalLength > 1` UND `currentMeter > totalLength*0.90` UND (Top-1=BCE ODER BCE-Conf > 0.20) → `BCE`, Conf `max(bceConf, 0.80)`.
  3. **Import-Boost**: Top-1-Conf > 0.30 und ein Import-Befund gleicher Code-Familie (erste 3 Zeichen) innerhalb ±1.5 m → uebernimm Import-Code.
  4. **Negativ-Gate (ortsgebunden)**: Top-1=BCD bei `currentMeter > 1.5` → `null`; Top-1=BCE bei `currentMeter < totalLength*0.85` → `null` (BCD nur am Anfang, BCE nur am Ende; ein „BCE" mitten im Rohr ist meist ein offener Anschluss).
  5. **Reine YOLO-Klassifikation**: Top-1 ≠ `OTHER` und Conf > 0.40 → Top-1.
  6. **Fallback**: Top-1=`OTHER` → Top-2, wenn Conf > 0.15 und ≠ `OTHER`.
  - `ResolvedCode(string Code, double Confidence, string Source)` — `Source` ist Klartext-Begruendung fuer Audit/Logging.

### CodingDedupPolicy — Einmal-Codes, Dedup, BCE-Plausibilitaet

- **`IsOneTimeCode(code)`**: `BCD`, `BCE`, `BDC` (Grundgeruest/Schacht-Ende, duerfen pro Haltung nur einmal vorkommen).
- **`CodesMatch(existing, new)`**: gleich wenn identisch oder gleicher Hauptcode (erste 3 Zeichen).
- **`ShouldStopAnalysisAfterTerminalCode(...)`**: stoppt Analyse, sobald `BCE`/`BDC` erreicht ist (Meter ≥ Kandidat−`TerminalMeterTolerance`=0.05, sonst Videozeit, sonst ohne Position sofort).
- **`ShouldDeferSpatialCodeUntilCloser(code, proximity)`**: nur fuer `BCC` (Bogen) — verschieben, solange das Naehe-Gate nicht `Codierbar` meldet.
- **`IsBoundaryEndCodePlausible(code, currentMeter, endMeter)`** — beidseitiges Plausibilitaets-Gate, **nur fuer BCE** (alle anderen Codes immer `true`):
  - Konstanten: `EndMeterAbsoluteTolerance=0.20 m`, `EndMeterRelativeThreshold=0.90`, `EndMeterOvershootTolerance=1.0 m`.
  - Unbekanntes Ende (`endMeter ≤ 0`/null) oder unbekannte Position → konservativ `true`.
  - Zu weit darueber: `currentMeter > endMeter + 1.0` → `false` (kaputter OSD-Meter, z.B. 114 m bei 15.82 m Haltung).
  - Zu frueh: akzeptiert ab `threshold = min(endMeter − 0.20, endMeter × 0.90)`; darunter `false` (verhindert, dass das dunkle Tunnelende am Fluchtpunkt faelschlich als Rohrende codiert wird).
- **`ResolvePlausibleEndMeter(osdMeter, importEndMeter, vmEndMeter)`**: liefert verlaesslichen BCE-Meter — Import-BCE vor VM-EndMeter; liegt der OSD-Meter mehr als 1.0 m ueber dem verlaesslichen Ende → auf das Ende korrigieren, sonst OSD behalten.

### Naehe-Gate / DN-Kreis (MetrierungProximityEvaluator)

Reine Geometrie-Pruefung, ob ein Befund nah genug vor der Kamera liegt, um korrekt metriert/codiert zu werden. Bezug ist der **Fluchtpunkt** (Rohrmitte); alle Distanzen in Einheiten des **Rohrradius** (1.0 = Rohrwand = DN-Kreis). Konservativ: was nicht klar nah ist → `Voraus` (nur merken, nicht protokollieren).

`Evaluate(MetrierungProximityInput i, MetrierungProximityThresholds t)`:
- Input (alle Koordinaten normiert 0..1): Box `X1,Y1,X2,Y2`; Fluchtpunkt `VanishX,VanishY`; `ImageAspect` (Breite/Hoehe); `PipeRadiusNorm` (= NormalizedDiameter/2, Fallback 0.5); `IsDirectionalEvent` (Bogen BCC).
- Distanz isotrop: `dx = ax−bx`, `dy = (ay−by)/aspect`, danach `/pipeR` → Einheit Rohrradius. (Wichtiger Fallstrick: Hoehe wird durch Aspect **geteilt**, nicht Breite multipliziert, weil `pipeR` als Breitenanteil bestimmt wird.)
- `outerR` = groesste Box-Eckendistanz zum Fluchtpunkt; `fillRatio = Y2−Y1` (Box-Hoehenanteil); `wandnaehe` = Bildrand-/Wandkontakt (`outerR ≥ 1.0 − WallTolerance` oder Box beruehrt Bildrand innerhalb `WallTolerance`).
- Entscheidungsreihenfolge: (1) `fillRatio ≥ FillNear` UND `wandnaehe` → `Codierbar` (querschnittsfuellend, grosse Muffe direkt vor Kamera). (2) `outerR ≥ 1.0 − WallTolerance` → `Codierbar` (**zentrale Regel**: Befund ueberschreitet den DN-Kreis nach aussen in den Ring DN-Kreis..Bildrand). (3) Sonderfall Bogen: `IsDirectionalEvent` UND `distToVanish ≥ RadialOutside` → `Codierbar` (seitlich verschobener Fluchtpunkt). (4) sonst → `Voraus`.
- Schwellen (`MetrierungProximityThresholds`, kalibrierbar): `FillNear=0.70`, `CenterNear=0.20`, `RadialOutside=0.45`, `WallTolerance=0.12`.
- Fachregel (Inspekteur): codiert wird erst, wenn das Ereignis **zwischen DN-Kreis und Bildrand** liegt; solange es ganz innerhalb des DN-Kreises (Richtung Tunnel) liegt, ist es zu weit voraus — nur merken.

### Zustandsbewertung (VsaEvaluationService, VSA-Richtlinie 2023)

`IVsaEvaluationService`: `Evaluate(Project)` (alle Records), `EvaluateRecord(HaltungRecord)`, `Explain(Project, record)` (Klartext-Rechnungsweg). Alle geben `Result<T>` zurueck (Fehlercodes z.B. `VSA_TABLE_MISSING`, `VSA_V2_TABLE_PARSE_FAILED`). Ergebnis pro Record sind **3** `VsaConditionResult` (D, S, B).

Zwei Engines (Flag `useV2Engine`, Default `true`):
- **v2 (datengetrieben, autoritativ)**: laedt `vsa_zustandsklassifizierung_2023_channels.json` und `…_manholes.json` ueber `VsaClassificationRuleSelector`. 142 Regeln ueber 26 Codes, je mit Achse D/S/B, Char1/Char2, Parameter+Einheit, EZ-Schwellen.
- **Legacy**: `VsaClassificationTable` (statische `EZD/EZS/EZB` plus optionale `QuantRules` mit `QuantRange{Min,Max,EZ}`; `Min` inklusiv, `Max` exklusiv).
- **Shadow-Mode** (`shadowModeEnabled`, Default true): laeuft beide Engines parallel und schreibt EZ-Abweichungen per `VsaShadowTelemetryWriter` (markiert `ExpectedDrift` fuer `BAA/BAB/BAC/BAF/BBA/BDD`).

**EZ-Skala — invertiert (0–4):** `EZ = 0` schlechtester Zustand … `EZ = 4` bester. `EZmin` = schlechtester (= numerisch kleinster) EZ aller Findings einer Anforderung.

**KI-Severity ↔ EZ (Fachregel, `docs` Kap. 11.4):** `EZ = 5 − Severity` (Severity 1–5, hoch=schlecht): 1→4, 2→3, 3→2, 4→1, 5→0. Diese Formel ist die fachliche Bruecke zwischen KI-Output (Severity) und VSA-EZ; im produktiven Bewertungspfad wird EZ jedoch **nicht** arithmetisch aus Severity berechnet, sondern aus den Klassifizierungstabellen via Q1/Q2 (Severity dient nur Live-Anzeige/Sortierung/Confidence-Mapping).

**Klassifizierung pro Finding (v2, `ClassifyFindingsV2`):** Code in baseCode (3Z) + Ch1 (4. Zeichen) + Ch2 (5. Zeichen) zerlegen; `selector.Classify(VsaClassificationRequest{Code,Ch1,Ch2,Q1,Q2,Material,AssetKind})` (`AssetKind = "manhole"` wenn baseCode mit `D` beginnt, sonst `"channel"`). Sonderfaelle:
- **Bestandsaufnahme/Steuercodes** (bekannt, kein EZ, alle Diagnostics `rule-not-found`): werden herausgefiltert → eine Haltung mit nur solchen Codes bekommt Zustandsklasse 4 („i.O."). Codes ohne ZN u.a.: `BCA, BCB, BCC, BCD, BCE, BDA, BDB, BDC, BDG, AEC, AED, AEF`.
- **Naeherung** (`approxEz`, Fallback wenn kein Messwert): Basiscode-EZ aus `approximateEzWhenUnquantified`; landet auf `EZB` bei `BB*` (betrieblich), sonst `EZS` (strukturell); Record-Feld `VSA_Geschaetzt = "ja"`.
- **Achsen-Ausnahmen**: `BAG` nur Achse B, `BAI` nicht Achse S (aus `nonAssessableRequirements`).

**Kernformeln (`ComputeForRequirement`, je D/S/B):**
- `EZmin = min(EZ_i)`. Wenn `EZmin == 4` → `ZN = 4.00`, A=0.
- Sonst `ZN_start = EZmin + 0.4`.
- Abminderung `A = 0.4 × Σ((4 − EZ_i) × LF_i) / ((4 − EZmin) × LA)`, gekappt `A ≤ 0.8` (`LA` = Haltungslaenge).
- `ZN = max(ZN_start − A, 0)`, gerundet auf 2 Stellen, gekappt auf ≤ 4.00.
- `DZ = ZN × 100 × B1 × B2 × B3 × B4` (niedrig = dringend).
- **Laengenfaktor `LF_i`** (`ComputeLengthFactor`): tatsaechliche Schadenlaenge (`SchadenlageEnde−Anfang` bzw. `MeterEnd−MeterStart`) wenn > `minLength`, sonst `minLength`. `minLength = 3.0 m` (Kanaele; Schaechte 0.5 m).
- Keine bewertbaren EZ, aber Findings vorhanden, alle bekannt und ohne Schaden → `ZN=4.00`, `EZmin=4`, `DZ = 4 × 100 × ΠB`, Note „Leitung i.O.". Nur unbekannte Codes → `ZN=null` („Bewertung nicht moeglich").

**Randbedingungen B1–B4** (Produkt `ΠB = B1×B2×B3×B4`, je kleiner = dringlicher; Defaults 1.00):
- **B1 Gewaesserschutz** (`Gewaesserschutz`): `S=0.90`; `AU/ZU/AO=0.95`.
- **B2 Nutzungsart** (`Nutzungsart`): Industrieabwasser `0.90`; Schmutz/Schmutzabwasser `0.95`; Mischabwasser `1.00`; Regen/Meteorwasser `1.05`; Bachwasser `1.10`.
- **B3 Grundwasserspiegel** (`Grundwasserspiegel`): unterhalb `0.90`; oberhalb `1.10`.
- **B4 Funktionale Hierarchie** (`FunktionHierarchisch`, PAA): Hauptsammelkanal_regional `0.90`; Hauptsammelkanal `0.95`; Sammelkanal/Sanierungsleitung/Strassen-/Gewaesser `1.00`; Liegenschaftsentwaesserung `1.10`.

**Gesamtbewertung & Record-Felder** (`ApplyRecordFields`): Gesamt-ZN = `min(ZN_D, ZN_S, ZN_B)` (schlechteste Achse). Felder (alle `FieldSource.Legacy`, `userEdited:false`): `VSA_Zustandsnote_D/S/B`, `Zustandsklasse` (= gerundete Gesamt-ZN, geklemmt 0..4), `Pruefungsresultat`, `VSA_Geschaetzt`.

**Mappings:**
- `Pruefungsresultat` (`BuildPruefungsresultat`, ZN 0=schlecht/4=gut): `≥3.0` → „i.O."; `≥1.5` → „beobachten"; sonst „Sanierungsbedarf". (docs-ZN-Schwellen: ≥3.0 / ≥1.5 / <1.5.)
- `MapDringlichkeit(DZ)` → Dringlichkeitsstufe: `<50` „Sofort"; `<150` „Kurzfristig (3J)"; `<250` „Mittelfristig (8J)"; `<350` „Langfristig"; `≥350` „Keine".

### QualityGateService — Green/Yellow/Red (Evidence-Fusion)

`Evaluate(EvidenceVector) → QualityGateResult(CompositeConfidence, TrafficLight{Green,Yellow,Red}, WeightsUsed, Explanation)`. Gewichtetes Mittel ueber **vorhandene** Evidenzsignale; null-Signale werden uebersprungen und die Gewichte ueber die vorhandenen Signale renormalisiert.

**Evidenzsignale** (`EvidenceVector`, alle nullable 0..1, geklemmt): `YoloConf`, `DinoConf`, `SamMaskStability`, `QwenVisionConf`, `LlmCodeConf`, `KbSimilarity`, `KbCodeAgreement` (bool→1.0/0.0), `PlausibilityScore`; plus `DamageCategory` (Gewichts-Auswahl) und `FrameCount`.

**Gewichte** (`CategoryWeights`, pro Schadenskategorie, default-Prior; in SQLite-Tabelle `CategoryWeights` persistiert, durch Validierung optimierbar): `WYolo=0.10`, `WDino=0.15`, `WSam=0.10`, `WQwen=0.15`, `WLlm=0.20`, `WKb=0.10`, `WKbAgreement=0.10`, `WPlausibility=0.10`. Auswahl: Kategorie-spezifisch, sonst „default", sonst `CategoryWeights.Default()`. `Normalize()`/`ToArray()`/`FromArray()` (kanonische 8er-Reihenfolge) fuer den Optimizer.

**Berechnung:** `composite = Σ(value_i × weight_i) / Σweight_i`, geklemmt 0..1 (`totalWeight ≤ 0` → Gleichgewichtung). Keine Signale → `Red`, composite 0.

**Schwellen:** `GreenThreshold = 0.75`, `YellowThreshold = 0.45`. `composite ≥ 0.75` → Green; `≥ 0.45` → Yellow; sonst Red.

**Ehrlichkeits-Deckel (`MinSignalsForGreen = 2`):** Ein Green mit weniger als 2 **vorhandenen** Signalen wird auf **Yellow** gedeckelt (geprueft wird die Anzahl, nicht die fachliche Uebereinstimmung). Verhindert, dass eine einzelne, evtl. halluzinierte YOLO-Box (`YoloConf=0.9`, Rest null) ungeprueft als „bestaetigt" durchlaeuft. `Explanation` enthaelt composite, Signalzahl, Kategorie und je Signal `value×normalisiertesGewicht` sowie ggf. den Deckel-Hinweis.

### Datenfluss (Ende-zu-Ende) und Fallstricke

1. Sidecar liefert rohe Predictions/Labels → `VsaCodeResolver.ResolveFromClassifier`/`InferCodeFromLabel` erzeugt normalisierten VSA-Code (Meter+Import als Fusion).
2. Geometrie: `MetrierungProximityEvaluator` entscheidet `Codierbar`/`Voraus`; `CodingDedupPolicy` filtert Einmal-Codes, prueft BCE-Plausibilitaet und stoppt nach Terminalcode.
3. Befund → `VsaEvaluationService`: Code→EZ via Klassifizierungstabelle (Q1/Q2), ZN/A/DZ pro D/S/B, Gesamt = schlechteste Achse, Record-Felder + Klartext-`Explain`.
4. Evidenzsignale aller Stufen → `QualityGateService` → Green/Yellow/Red (z.B. in `FullProtocolGenerationService` mit `LlmCodeConf`, `KbSimilarity`, `KbCodeAgreement`, `PlausibilityScore`).

Fallstricke beim Nachbau: EZ-Skala ist **invertiert** (0 schlecht, 4 gut) — `EZmin = min`, nicht max. Gesamtnote ist **min** ueber D/S/B. DZ ist **niedrig = dringend**. Bestandsaufnahme-/Steuercodes erzeugen bewusst KEINE Note (sonst faelschlich „beschaedigt"). BCD nur am Rohranfang, BCE nur am Rohrende (Negativ-Gate + Plausibilitaet gegen Fluchtpunkt-Verwechslung und kaputte OSD-Meter). Naehe-Gate-Distanzen sind in Rohrradius-Einheiten (Aspect-Korrektur ueber Division der Hoehe). QualityGate-Green verlangt mindestens 2 Signale (Ehrlichkeits-Deckel). Severity→EZ (`EZ = 5 − Severity`) ist Fachregel/Doku, der produktive EZ kommt aus den 2023er-Klassifizierungstabellen.

## A4 · C#-KI-Pipeline-Services, Dedup und Quantifizierung

Dieses Teilsystem ist die gesamte Geschaeftslogik der KI-Bildanalyse in C# (Thin-AI-Prinzip: C# orchestriert, entscheidet, dedupliziert, quantifiziert; LLM/Modelle liefern nur Roh-Signale). Es ruft den Python-Sidecar (YOLO/DINO/SAM) per HTTP und optional Qwen-VL ueber Ollama. Alle Pfade liegen unter `src/AuswertungPro.Next.Infrastructure/Ai/` (Implementierung) und `src/AuswertungPro.Next.Application/Ai/` (Vertraege/reine Logik).

### Datenfluss-Ueberblick (Multi-Model-Hauptpfad)

Pro Frame, in dieser Reihenfolge, mit Drop an jeder Stelle:

```
YOLO-cls Vorfilter (Quality-Gate + LEER/OTHER-Skip)
   -> YOLO-Detect Pre-Screening (IsRelevant?)
      -> Grounding DINO (Open-Vocabulary Boxen)
         -> SAM (Boxen -> Pixel-Masken)
            -> Quantifizierung (Masken -> mm/%/Uhrlage)
               -> Naehe-Gate (DN-Kreis: codierbar vs. "voraus")
                  -> Klassifikator-Code-Entscheidung (optional) + Temporal-Voting
                     -> Qwen-VL VSA-Code-Enrichment (optional)
                        -> framebasiertes Dedup (TemporalFindingDeduplicator)
```

Ergebnis je Lauf: `VideoAnalysisResult` mit `IReadOnlyList<RawVideoDetection>` (nach `MeterStart` sortiert) plus `TelemetrySummary`.

### Pfadwahl: `VideoAnalysisPipelineService`

Datei: `src/AuswertungPro.Next.Infrastructure/Ai/VideoAnalysisPipelineService.cs`, implementiert `IVideoAnalysisPipelineService` (`Application/Ai/VideoPipelineContracts.cs`).

`RunAsync(PipelineRequest, IProgress<PipelineProgress>, ct)` ist der Einstieg. Ablauf:
1. Wenn `AiRuntimeSettings.Enabled == false`: `PipelineResult.Failed("KI ist deaktiviert ...")`.
2. `ShouldUseMultiModelAsync` entscheidet Multi-Model vs. Ollama-Only:
   - `PipelineMode.OllamaOnly` -> immer Ollama-Only, kein Warn-Fallback.
   - `MultiModelEnabled == false` UND `Mode != MultiModel` -> Ollama-Only (Master-Kill-Switch).
   - Sonst Sidecar-Health pruefen (`VisionPipelineClient.HealthCheckAsync`): `health == null || health.Status != "ok"`. Bei `Mode == MultiModel` wird ein Fehler GEWORFEN (Sidecar erzwungen). Bei `Mode == Auto` Fallback auf Ollama-Only mit sichtbarer `fallbackReason`-Warnung.
3. Phase 1 Video-Analyse: bei Multi-Model wird `VisionPipelineClient` + `EnhancedVisionAnalysisService` (Qwen) gebaut und an `MultiModelAnalysisService` uebergeben; `FrameStepSeconds`, `DedupWindowFrames`, `EstimatedReachLengthM` (aus `request.ReachLengthM`, Fallback 50m) gesetzt. Sonst `VideoFullAnalysisService.Create(...)` (reiner Ollama-Pfad).
4. Phase 2 Code-Mapping: `FullProtocolGenerationService.GenerateFromDetectionsAsync` mappt die bereits erzeugten `RawVideoDetection` zu Protokolleintraegen (Video wird NUR EINMAL analysiert; kein zweites `AnalyzeAsync`).

Wichtiger Fallstrick (Audit R1): Der `HttpClient` wird geteilt. Deshalb setzt `VisionPipelineClient` NIEMALS `HttpClient.BaseAddress`, sondern baut immer absolute URIs in `BuildUri()`. Ein gesetztes `BaseAddress` auf einem bereits benutzten geteilten Client wirft `InvalidOperationException` und kippt den Hauptpfad.

`PipelineRequest` (Application/Ai/VideoPipelineContracts.cs): `HaltungId`, `VideoPath`, `AllowedCodes`, `ProjectFolderAbs?`, `RequestedBy?`, `FrameStepSeconds = 3.0`, `DedupWindowFrames = 3`, `ReachLengthM?` (echte Haltungslaenge; null = 50m-Annahme).

### HTTP-Client zum Sidecar: `VisionPipelineClient`

Datei: `src/.../Ai/Pipeline/VisionPipelineClient.cs`. Konstruktor `(Uri baseUri, HttpClient? httpClient = null, string? sidecarToken = null)`; Default-Timeout 15 Minuten, falls kein Client uebergeben.

JSON-Konvention: `JsonNamingPolicy.SnakeCaseLower` + `PropertyNameCaseInsensitive`. Alle DTOs in `VisionPipelineDtos.cs` tragen zusaetzlich explizite `[JsonPropertyName("snake_case")]`.

Endpunkte/Methoden:
- `HealthCheckAsync` -> GET `/health` -> `SidecarHealthResponse?` (null = nicht erreichbar). `OperationCanceledException` wird durchgereicht (Abbruch ist NICHT "offline").
- `CheckHealthDetailedAsync` -> GET `/health` -> `PipelineHealthCheckResult(IsReachable, IsAuthorized, StatusCode, Health, Error)`; unterscheidet offline / 401 / ok, damit die UI Token-Fehler nicht als "offline" zeigt.
- `DetectYoloAsync(YoloRequest)` -> POST `/detect/yolo` -> `YoloResponse`.
- `DetectDinoAsync(DinoRequest)` -> POST `/detect/dino` -> `DinoResponse`.
- `SegmentSamAsync(SamRequest)` -> POST `/segment/sam` -> `SamResponse`.
- `ClassifyYoloAsync(YoloClassifyRequest)` -> POST `/classify/yolo` -> `YoloClassifyResponse`.
- `ExportTrainingAsync(TrainingExportRequestDto)` -> POST `/training/export-yolo`.

Querschnittsverhalten:
- Token (`X-Sidecar-Token`-Header) wird NUR bei Loopback-URIs (`localhost`/`127.0.0.1`/`::1`) gesendet. Quellen in Reihenfolge: Konstruktor-Argument, Env `SEWER_SIDECAR_AUTH_TOKEN`, Env `SEWER_SIDECAR_TOKEN`, Datei `%LOCALAPPDATA%\SewerStudio\.sidecar_token`.
- Genau EIN Retry bei transienten Fehlern (`PostAsync`): HTTP 503 (Sidecar raeumt VRAM/laedt Modell um) oder Transportfehler (`StatusCode == null`, Verbindung abgelehnt/abgerissen). 1500 ms Delay, dann ein zweiter Versuch. KEIN Retry bei Abbruch durch den Aufrufer. Andere Fehler scheitern sofort ehrlich.
- Nicht-2xx wird zu `HttpRequestException` mit Statuscode + Body. YOLO-Responses werden zusaetzlich an `SidecarTelemetryWriter` geschrieben.

### Sidecar-DTOs (Vertraege)

Datei: `src/.../Ai/Pipeline/VisionPipelineDtos.cs`. Zentrale Felder:
- `YoloRequest(image_base64, confidence_threshold)`; `YoloResponse(is_relevant, detections[], frame_class, inference_time_ms, model_name?, model_backend?, device?, queue_wait_ms, vram_*)`. `YoloDetectionDto(x1,y1,x2,y2,class_name,confidence)`.
- `YoloClassifyRequest(image_base64, top_k=5)`; `YoloClassifyResponse(predictions[], inference_time_ms, usable=true, quality_reason="ok", model_name, model_source, model_sha256, imgsz, preprocessing, device, bend_shift, is_bend=false, vanish_x=0.5, vanish_y=0.5)`. `usable=false` = Frame-Quality-Gate des Sidecars (schwarz/ueberbelichtet/strukturlos/unscharf). `is_bend` = geometrisches Bogen-Veto (cls hat keine Bogen-Klasse).
- `DinoRequest(image_base64, text_prompt?, box_threshold, text_threshold)`; `DinoResponse(detections[], inference_time_ms, degraded=false, error?, error_code?)`. `DinoDetectionDto(x1,y1,x2,y2,label,confidence,phrase)`.
- `SamRequest(image_base64, bounding_boxes[], pipe_diameter_mm?)`; `SamResponse(masks[], image_width, image_height, inference_time_ms, degraded=false, requested_boxes, skipped_boxes, low_score_boxes, error?, bend_shift, is_bend, vanish_x, vanish_y)`. `SamMaskResult(label, confidence, bbox[4], mask_rle, mask_area_pixels, image_area_pixels, height_pixels, width_pixels, centroid_x, centroid_y)`.

Ehrlichkeits-Vertrag (zentral fuer QualityGate): `degraded=true` heisst Modell-/Inferenzfehler bzw. Teilverlust. Eine leere `detections`-Liste bei `degraded=true` ist KEIN sauberer Negativbefund, sondern ein Review-Signal. Dies darf nie wie "sauberes Rohr" verbucht werden.

### Multi-Model-Orchestrierung: `MultiModelAnalysisService`

Datei: `src/.../Ai/Pipeline/MultiModelAnalysisService.cs`. Konstruktor nimmt `VisionPipelineClient`, `PipelineConfig`, `ffmpegPath`, optional `EnhancedVisionAnalysisService qwenVision`, optional Logger. Public Stellschrauben: `FrameStepSeconds = 3.0`, `DedupWindowFrames = 3`, `QwenFrameTimeout = 120s`, `UseClsPrefilter = true`, `EstimatedReachLengthM = 50.0`, `ClassifierDecisionEnabled` (Env `SEWERSTUDIO_CLASSIFIER_DECISION`, Default aus).

`AnalyzeAsync(videoPath, progress, ct)` streamt Frames via `VideoFrameStream.Open(ffmpeg, video, FrameStepSeconds, duration, ct)` (PNG-Bytes je Frame). `pipeDiameterMm = PipeDiameterMmOverride ?? 300`. Pro Frame:

1. Leerer Frame -> Drop (`empty_frame`), `deduplicator.AdvanceAll()`.
2. Telemetrie-Bypass-Heuristik (YOLO erkennt nur Schaeden, nicht Bestandsaufnahme): `isBcdZone` (t>20s, Meter<1.5m, frameIndex<=10), `isBceZone` (t > Dauer - 2*Step), `isPeriodicSweep` (t>20s und jeder 3. Frame). Bei Bypass wird YOLO-Detect uebersprungen (`frame_class="sweep"`), Frame geht direkt an DINO/Qwen.
3. YOLO-cls Vorfilter (`UseClsPrefilter`, `ClassifyYoloAsync(top_k=3)`), gilt AUCH fuer Bypass-Frames:
   - `usable==false` -> Skip (`cls_quality_skip`).
   - Nur bei `ClassifierDecisionEnabled`: top-Pred `LEER` mit Conf>0.70 -> Skip (`cls_leer_skip`), Voting-Fenster altert.
   - top-Pred `OTHER`/`NORMAL` mit Conf>0.70 -> Skip (`yolo_cls_skip`).
   - cls nicht verfuegbar -> kein harter Fehler, weiter zum Detektionspfad.
4. Step 1 YOLO-Detect (`DetectYoloAsync` mit niedrigster klassenspezifischer Schwelle `_minClassConfidence`). COCO-Fallback-Warnung: weicht `model_name` vom erwarteten `yolo26m` (Env `SEWERSTUDIO_EXPECTED_YOLO_MODEL`) ab, wird einmal pro Lauf gewarnt (Schadenserkennung faktisch blind). Klassenspezifische Nachfilterung: pro Detection `YoloClassVsaMapper.ToVsaMainCode(class_name)` -> Schwelle aus `YoloClassConfidence`, sonst `YoloConfidence`. `IsRelevant` wird nach Filter neu gesetzt. `!IsRelevant` -> Skip (`yolo_irrelevant`).
5. Step 2 DINO (`DetectDinoAsync(box=DinoBoxThreshold, text=DinoTextThreshold)`). `Degraded` -> Skip als Review (`dino_degraded`, NICHT als Negativbefund). 0 Boxen -> Skip (`dino_no_boxes`).
6. Step 3 SAM (`SegmentSamAsync(samBoxes, pipeDiameterMm)`; Boxen aus DINO-Detections). `Degraded` -> Frame wird weiterverarbeitet (Masken existieren), aber als Review markiert (`sam_skipped_n_of_m`).
7. Step 4 Quantifizierung: `MaskQuantificationService.QuantifyAll(samResult, pipeDiameterMm)`, Meter via `EstimateMeter`. Dann `SegmentedFindingBuilder.Build(...)` mit Naehe-Gate (im Batch ohne Kalibrierung: `vanishX/Y=0.5`, `pipeRadiusNorm=0.5`, Schwellen `MetrierungProximityThresholds.Default`). Pro Segment: leeres Label -> skip; `!Proximity.IsCodierbar` -> als `ahead_of_camera` gemerkt aber NICHT metriert. Sonst `EnhancedFinding` gebaut: `VsaCodeHint = VsaCodeResolver.InferCodeFromLabel(label)`, `Severity = EstimateSeverity(q)`, `PositionClock` normalisiert, plus Quant-Felder und normierte Bbox.
8. Klassifikator-Entscheidung (nur `ClassifierDecisionEnabled`): `VsaCodeResolver.ResolveFromClassifier(predictions, meter, EstimatedReachLengthM)` -> `TemporalCodeVotingService.RegisterAndVote`. Ein im Fenster bestaetigter Code ueberschreibt `VsaCodeHint` ALLER Findings des Frames und gilt danach als fuehrend (Qwen darf ihn nicht mehr aendern).
9. Step 5 Qwen-VL (nur wenn `qwenVision != null` und Findings>0). Per-Frame-Timeout `QwenFrameTimeout`. Uebergibt `MultiModelFrameResult` + optional vorigen Befund als Kontext (nur wenn <1m entfernt). Verarbeitung:
   - OSD-Meter wird nur uebernommen, wenn plausibel (`MeterPlausibility.IsPlausible`, 0..500m) UND Bildqualitaet nicht "schlecht" (Audit R7: schlechtes Bild = unzuverlaessiges OSD-Lesen; sonst vergiftet ein falscher Meter die Timeline `lastMeter`).
   - `ImageQuality == "schlecht"` -> alle Findings des Frames verworfen (`image_quality_bad`).
   - Qwen-Findings werden per Label-Aehnlichkeit an die quantifizierten Findings gematcht; ein leerer `VsaCodeHint` wird mit Qwens Hint gefuellt. Ist ein Klassifikator-Code bestaetigt, ueberschreibt Qwen ihn NICHT (nur leere Hints fuellen).
10. Dedup-Update: `meterSource/isMeterEstimated` via `GetDedupMeterMetadata(qwenMeterAccepted)` (`("QwenOsd", false)` vs. `("LinearEstimate", true)`), dann `deduplicator.Update(findings, meter, frameEvidence, meterSource, isMeterEstimated)`.

Am Lauf-Ende: `deduplicator.Flush()`, Telemetrie-Summary, Trace-Summary.

`EstimateMeter(t, duration, ref lastMeter)`: linear `t/duration * EstimatedReachLengthM`, monoton (`lastMeter = Max(lastMeter, estimated)`), auf 2 Nachkommastellen gerundet. Wird durch akzeptiertes Qwen-OSD ueberschrieben.

`EstimateSeverity(QuantifiedMask)` (Heuristik, 1..5): CrossSectionReduction >50 -> 5; >25 oder Extent>50 -> 4; Height>50 oder Extent>25 -> 3; Height>10 -> 2; sonst 1.

`PipelineFrameTrace` (Sichtbarkeit, kein Verhalten) wird pro Frame ueber `PipelineTraceWriter` geschrieben; `DropReason` dokumentiert jeden Abbruch (`empty_frame`, `cls_quality_skip`, `cls_leer_skip`, `yolo_cls_skip`, `yolo_irrelevant`, `dino_no_boxes`, `dino_degraded`, `sam_error`, `ahead_of_camera`, `no_findings`, `all_findings_missing_code`, `qwen_timeout`, `qwen_error`, `image_quality_bad`).

### Naehe-Gate / DN-Kreis: `SegmentedFinding` + `MetrierungProximityEvaluator`

Dateien: `src/.../Ai/Pipeline/SegmentedFinding.cs` und `Application/Ai/MetrierungProximity*.cs`.

`SegmentedFindingBuilder.Build` koppelt Maske + Quant + DINO fest (statt fragiler Listen-Index ueber drei Listen). Iteriert ueber SAM-Masken (uebersprungene Boxen existieren dort nicht), paart `mask[m]`/`quant[m]` per Index, ordnet DINO ueber Containment (Schnitt/Maskenflaeche, nicht IoU; Schwelle >=0.5) + gleiches Label zu. Bogen-Labels (`bend`/`bogen`/`kruemm`/`kurve`) werden als `IsDirectionalEvent` markiert.

`MetrierungProximityEvaluator.Evaluate` (Fachregel des Inspekteurs, konservativ "im Zweifel Voraus"): Bezug ist der Fluchtpunkt (Rohrmitte). Distanzen in Einheiten Rohrradius (1.0 = Rohrwand), Hoehe durch `ImageAspect` geteilt (isotrope Distanz). Entscheidung `Codierbar` wenn: (1) `fillRatio >= FillNear(0.70)` UND Wandnaehe; ODER (2) `outerR >= 1.0 - WallTolerance(0.12)` (Befund ueberschreitet DN-Kreis nach aussen = Nahbereich, zentrale Regel); ODER (2b) Bogen mit `distToVanish >= RadialOutside(0.45)` (seitlich verschobener Fluchtpunkt). Sonst `Voraus` (komplett im DN-Kreis Richtung Tunnel -> nur merken, nicht codieren; Distanz waere falsch). Schwellen-Defaults: `FillNear=0.70`, `CenterNear=0.20`, `RadialOutside=0.45`, `WallTolerance=0.12`.

### Quantifizierung: `MaskQuantificationService`

Datei: `src/.../Ai/Pipeline/MaskQuantificationService.cs`. Statisch, wandelt SAM-Pixelmasken in reale Masse (mm, %, Uhrlage).

Grundannahme: Rohr fuellt ~70 % der Bildbreite (`PipeImageWidthRatio = 0.70`). Mit `PipeCalibration` (kalibriert) wird stattdessen `NormalizedDiameter` genutzt.
- `pxToMm = pipeDiameterMm / (imageWidth * ratio)`.
- `HeightMm`, `WidthMm` = Maskenpixel * `pxToMm`, gerundet.
- `pipeRadiusPx = (imageWidth*ratio)/2`. ExtentPercent = `WidthPixels / Umfang(2πr) * 100`, geklammert 0..100.
- CrossSectionReductionPercent = `MaskAreaPixels / (π r²) * 100`, geklammert 0..100.
- IntrusionPercent nur bei Labels mit `intrusion`/`einragung`/`root`: `heightMm/pipeDiameterMm * 100`, geklammert 0..100.
- Uhrlage `ComputeClockPosition(centroidX, centroidY, w, h)`: Rohrmitte = Bildmitte; `atan2(dx, -dy)` (Y gespiegelt), 30 Grad je Stunde, 12:00 = oben. Mit Kalibrierung: `PipeCalibration.PointToClockHour` gegen die echte Rohrmitte.
- Bei `imageWidth<=0 || pipeDiameterMm<=0` werden Masse `null`, nur Uhrlage berechnet.

### Framebasiertes Dedup: `TemporalFindingDeduplicator`

Datei: `src/.../Ai/Pipeline/TemporalFindingDeduplicator.cs`, `internal`. Konfiguration `TemporalDedupOptions`: `DedupWindowFrames=3`, `NormalizeFallbackLabels=true`, `NormalizeOutputClock=false`, `MinStretchLengthMeters=1.0`, `MeterMergeGapMaxMeters?`, `ClockInKey=true`.

Kernmethode `Update(current, meter, evidence?, meterSource?, isMeterEstimated)`:
- Dedup-Schluessel `BuildFindingKey`: `NormalizeFindingCode(VsaCodeHint)` -> sonst `InferCodeFromLabel(Label)` -> sonst normalisiertes Fallback-Label. Wenn `ClockInKey`, wird normalisierte Uhrlage angehaengt (`label|12:00`). Im Klassifikator-Regime ist `ClockInKey=false`, weil ein Ganzbild-Code sonst ueber Masken-Uhrlagen aufsplittet (Pilot: 12x BDD statt 1 Befund).
- Aktive Befunde, die im aktuellen Frame wieder auftauchen, werden aktualisiert (Meter-Range erweitert, MaxSeverity, max. Quant-Werte, Evidence gemergt via Max). Fehlt ein aktiver Befund: `MissedFrames++`; ab `>= DedupWindowFrames` wird er als `RawVideoDetection` abgeschlossen.
- `ShouldStartNewFinding`: ueberschreitet der neue Meter die beobachtete Range um mehr als `MeterMergeGapMaxMeters`, wird der laufende Befund geschlossen und ein neuer gestartet (Meterluecke = getrennter Schaden).
- `AdvanceAll()`: alle aktiven altern lassen (fuer Skip-Frames). `Flush()`: alle restlichen abschliessen.

Meterbereich (`ResolveMeterRange`): Nur fuer Streckenschaden-Codes (`VsaCodeResolver.IsStreckenschadenCode`) und nur wenn `end-start >= MinStretchLengthMeters` wird ein echtes `MeterStart..MeterEnd` ausgegeben; sonst Punktschaden (`firstObservedMeter, firstObservedMeter`).

`RawVideoDetection` (Ausgabe, Application/Ai/VideoPipelineContracts.cs): `FindingLabel`, `MeterStart`, `MeterEnd`, `Severity` (Label "high"/"mid"/"low" -> Confidence 0.90/0.70/0.50), `VsaCodeHint?`, `PositionClock?`, Quant-Felder, `Evidence?` (`EvidenceVector` mit `FrameCount`), `MeterSource?`, `IsMeterEstimated`. `SeverityLabel`: >=4 "high", ==3 "mid", sonst "low".

### Temporal-Voting: `TemporalCodeVotingService`

Datei: `src/.../Ai/Pipeline/TemporalCodeVotingService.cs`. Mehrheits-Fenster gegen Einzelbild-Ausreisser. Defaults: `WindowSize=3`, `MinAgreement=2`, `MeterRadius=1.5`. `RegisterAndVote(code, meter)`: ein Code gilt bestaetigt, sobald er in >= `MinAgreement` der letzten `WindowSize` Entscheidungen vorkommt UND die Treffer innerhalb `MeterRadius` der aktuellen Position liegen. Hysterese: ein bereits bestaetigter Code bleibt am selben Meter aktiv, solange das Fenster noch eine Stimme dafuer enthaelt (gegen Flattern bei stehender Kamera). `Reset()` pro Video-Lauf.

### Klassifikator-Code-Fusion: `VsaCodeResolver`

Datei: `src/.../Ai/VsaCodeResolver.cs` (statisch). Einzige Quelle fuer Code-Normalisierung, Label-Lookup, Clock-Normalisierung; nutzt einen optionalen `ICodeCatalogProvider` (via `ConfigureCatalog`).
- `NormalizeFindingCode`: akzeptiert 2..8 Grossbuchstaben, exakt im Katalog ODER 3-Zeichen-Hauptcode im Katalog; sonst null.
- `InferCodeFromLabel`: deutsche/englische Keyword-Heuristik -> VSA-Hauptcode (z. B. anschluss->BCA, bogen->BCC, rohranfang->BCD, rohrende->BCE, riss->BAB, bruch->BAC, verformung->BAA, versatz->BAJ, wurzel->BBA, einragung->BAI, korrosion->BAF, inkrustation->BBB, ablagerung->BBC, wasserspiegel/rueckstau->BDDC). Normaler/trueber Abwasserfluss ohne Rueckstau -> null.
- `IsStreckenschadenCode`: Katalog `RequiresRange` ODER feste `StreckenschadenCodes`-Liste (BAB*/BAF*/BBA*/BBB*/BBC*/BBD* u. a.).
- `ResolveFromClassifier(predictions, currentMeter, totalLength, importContext?)` -> `ResolvedCode(Code, Confidence, Source)` (Sensor-Fusion, reine C#-Regeln):
  - BCD wenn `meter<0.5` und (BCD Top-1 ODER bcdConf>0.20) -> Conf `Max(bcdConf,0.80)`.
  - BCE wenn `meter > totalLength*0.90` und (BCE Top-1 ODER bceConf>0.20) -> Conf `Max(bceConf,0.80)`.
  - Import-Boost: gleiches Familien-Praefix in +/-1.5m, top1Conf>0.30.
  - Negativ-Gate (ortsgebundene Grundgeruest-Codes): BCD bei `meter>1.5` -> null; BCE bei `meter < totalLength*0.85` -> null (ein "BCE" mitten im Rohr ist meist ein offener Anschluss).
  - Reine YOLO-Klasse wenn `top1 != OTHER` und top1Conf>0.40; Fallback Top-2 wenn Top-1=OTHER und top2Conf>0.15.

### Einzelframe-Pfad: `SingleFrameMultiModelService`

Datei: `src/.../Ai/Pipeline/SingleFrameMultiModelService.cs`. Fuer den Live-Codiermodus ("Jetzt analysieren" auf dem aktuellen Frame), ohne Video-Streaming und ohne Temporal-Dedup. Defaults aus denselben Env-Vars wie der Batch-Pfad: `yoloConfidence=0.25`, `dinoBoxThreshold=0.25`, `dinoTextThreshold=0.20`.

`AnalyzeFrameAsync(pngBytes, pipeDiameterMm, calibration?, ct, currentMeterM?, reachLengthM?)` -> `SingleFrameResult`. Ablauf:
1. YOLO-cls (`ClassifyYoloAsync(top_k=5)`): wenn `usable` und Meter+Laenge bekannt -> `ResolveFromClassifier`, plus `ResolveBoundaryFromPosition` (Positionsregel BCD/BCE) und `ResolveVisibleFrameCandidateFromRawClassifier`. Erkennt das Sidecar einen Bogen (`is_bend`), wird in der Endzone NICHT faelschlich BCE gesetzt (Bogen-Veto). Liefert die Grenze BCD/BCE, kehrt der Service sofort mit reinem Klassifikator-Ergebnis zurueck (kein DINO/SAM).
2. YOLO-Detect: `!IsRelevant` -> Rueckgabe (ausser bei klassifikator-only Struktur-Code BCA/BCC, dann weiter). Echte `YoloMaxConfidence` (hoechste Box) wird ans QualityGate weitergereicht.
3. DINO: 0 Boxen -> Rueckgabe.
4. SAM (DINO-Boxen).
5. Quantifizierung pro Maske (mit oder ohne `PipeCalibration`).

`SingleFrameResult` traegt zusaetzlich `ClassifierCode/Confidence/Source/TimeMs` und `YoloMaxConfidence`.

### Ollama-Only-Fallback: `VideoFullAnalysisService`

Datei: `src/.../Ai/VideoFullAnalysisService.cs`. Wird gewaehlt, wenn Sidecar nicht erreichbar/erzwungen aus. Kein YOLO/DINO/SAM; nur Qwen-VL ueber `EnhancedVisionAnalysisService.AnalyzeAsync(base64, ct)` pro Frame. Stellschrauben `FrameStepSeconds=3.0`, `DedupWindowFrames=3`, `MinSeverity=1`, `VisionFrameTimeout=120s`.

Ablauf je Frame: Frame extrahieren -> Qwen analysiert (Timeout/Fehler -> Frame skip, `deduplicator.AdvanceAll()`) -> Meter aus `analysis.Meter` oder `EstimateMeter(t, duration)` (eigene Heuristik mit ~0.1 m/s Default, monoton ueber `_lastKnownMeter`) -> Findings mit `Label` und `Severity >= MinSeverity` -> selber `TemporalFindingDeduplicator` (hier `NormalizeFallbackLabels=false`, `NormalizeOutputClock=true`). `meterSource` ist `"Analysis"` (echtes OSD) bzw. `"LinearEstimate"`.

### Plausibilitaet: `RuleBasedAiSuggestionPlausibilityService`

Datei: `src/.../Ai/RuleBasedAiSuggestionPlausibilityService.cs`, implementiert `IAiSuggestionPlausibilityService`; deterministisch/regelbasiert (kein LLM). `NoopAiSuggestionPlausibilityService` ist die No-Op-Variante. `ApplyChecks(AiSuggestionResult, ObservationContext)` validiert den Code gegen Katalog (optional injizierte `IReadOnlySet<string> allowedCodes`) und das VSA-Format (Regex `^[ABD][A-Z]{2,7}$`):
- PL01: nicht VSA-Format UND nicht im Katalog -> Confidence -0.5, Code verworfen (null). Nicht-Format aber im Katalog -> -0.15. 
- PL02: VSA-Format aber nicht im Katalog -> Confidence -0.4 (`UnknownCodePenalty`).
- PL04: BCD/BCE/BDC bereits bestaetigt (aus `context.AlreadyConfirmedCodes`) -> Confidence 0.0, Code verworfen (Grundgeruest ist einmalig).
- PL03 (nur Warnung): Beobachtungstext "Riss" aber Code nicht "BA..." bzw. "Verformung" aber nicht "BB...".

### Konfiguration: `PipelineConfig` + `AiSettingsFactory`

`PipelineConfig` (Application/Ai/PipelineConfig.cs): `MultiModelEnabled`, `SidecarUrl`, `SidecarToken?`, `Mode` (`PipelineMode { Auto, MultiModel, OllamaOnly }`), `YoloConfidence`, `YoloClassConfidence` (Dictionary Hauptcode->Schwelle), `DinoBoxThreshold`, `DinoTextThreshold`, `SidecarTimeoutSec`, `PipeDiameterMmOverride?`, `SamStabilityCheckEnabled=false`, `McDropoutEnabled=true`.

`AiSettingsFactory.Load` (`src/.../Ai/Configuration/AiSettingsFactory.cs`) liest Env-Vars (Praefix `SEWERSTUDIO_`, Alias `AUSWERTUNGPRO_`). Wichtige Defaults:
- `SidecarUrl` = `http://localhost:8100`; `OllamaBaseUri` = `http://localhost:11434`.
- `PipelineMode` Default `OllamaOnly` (Werte: `multimodel`/`multi`, `ollama`/`ollamaonly`, `auto`).
- `YoloConfidence` = 0.25; `DinoBoxThreshold` = 0.25; `DinoTextThreshold` = 0.20 (A/B 2026-06-10 auf 57er-clean).
- `SidecarTimeoutSec` = 300; `FfmpegPath` = `ffmpeg`.
- `YoloClassConfidence` (klassenspezifisch): BAB=0.15, BAA=0.20, BAC=0.25, BBA=0.20, BBB=0.25, BBC=0.25, BCA=0.30, BCC=0.30, BCD=0.30, BCE=0.30.

`YoloClassVsaMapper.ToVsaMainCode` (src/.../Ai/Pipeline/YoloClassVsaMapper.cs) mappt englische YOLO-Klassennamen auf VSA-Hauptcodes (crack->BAB, fracture->BAC, deformation->BAA, displacement->BAJ, intrusion->BAI, root/roots->BBA, deposit->BBC, infiltration->BBF, connection->BCA) und unterstuetzt Legacy "BAB_crack". `structural_other` bleibt ungemappt (Default-Schwelle).

### Nachbau-Fallstricke (must-not-break)

- Kein `HttpClient.BaseAddress` setzen; immer absolute URIs bauen (geteilter Client).
- `degraded=true` aus DINO/SAM darf nie als sauberer Negativbefund verbucht werden -> Review-Signal.
- COCO-Fallback (falsche YOLO-Gewichte) sichtbar warnen, nicht still durchlaufen lassen.
- OSD-Meter aus Qwen nur bei plausiblem Wert (0..500m) UND guter Bildqualitaet uebernehmen, sonst vergiftet er die fortlaufende Meter-Timeline.
- Im Klassifikator-Regime `ClockInKey=false` im Dedup, sonst splittet ein Ganzbild-Code ueber Masken-Uhrlagen.
- Genau ein Retry nur bei 503/Transportfehler; Abbruch des Aufrufers nie retryen.
- Qwen darf einen vom Temporal-Voting bestaetigten Klassifikator-Code nicht ueberschreiben (nur leere Hints fuellen).
- QualityGate Green/Yellow/Red speist sich aus `EvidenceVector` (nullable Signale: YoloConf, DinoConf, SamMaskStability, QwenVisionConf, ...); Dedup merget Signale per Max und summiert `FrameCount`.

## A5 · Python-FastAPI-Sidecar (`sidecar/`)

Der Sidecar ist ein eigenstaendiger Python-Prozess, der die GPU-Vision-Modelle (YOLO-Detektion, YOLO-Klassifikator, Grounding DINO, SAM 2.1) als lokaler HTTP-Dienst auf `127.0.0.1:8100` bereitstellt. Die C#-App (`VisionPipelineClient`) ruft ihn auf. Architekturprinzip: der Sidecar liefert **nur** rohe Modell-Ausgaben (Boxen, Masken, Klassen, Telemetrie); alle Geschaeftslogik (VSA-Mapping, Dedup, QualityGate) bleibt in C#.

### Verzeichnislayout

```
sidecar/
  pyproject.toml                # Projekt-Metadaten, optionale Extras, pytest-Marker
  requirements.txt              # Quell-Abhaengigkeiten (cu128-Nightly-Index)
  requirements-lock.txt         # eingefrorener Stand (uv pip freeze), Header-Caveats beachten
  setup.ps1                     # .venv anlegen + Lock installieren + GPU/Torch-Check
  start_sidecar.ps1             # Modelle praesenz-pruefen, Env setzen, uvicorn starten
  build_engine.ps1             # YOLO .pt -> ONNX -> TensorRT .engine + .names.json (hardware-gebunden)
  models/                       # Modellgewichte (nicht im Repo, .gitignore)
  sidecar/                      # Python-Paket
    main.py                     # FastAPI-App, lifespan, Middleware (Host+Token), Exception-Handler
    config.py                   # SidecarSettings (pydantic-settings, Env-Prefix SEWER_SIDECAR_)
    gpu_manager.py              # GpuModelManager-Singleton, ModelSlot-Enum, Locks, VRAM-Budget
    telemetry.py                # append-only JSONL-Telemetrie
    routes/                     # health, warmup, yolo, dino, sam, training
    models/                     # yolo_wrapper, dino_wrapper, sam_wrapper, bend_geometry, image_decode, box_utils, nocrop_compat
    schemas/                    # detection.py, segmentation.py (Pydantic-DTOs)
  tests/                        # pytest (Marker gpu/e2e/slow), default-Lauf ohne GPU
```

### App-Aufbau (`sidecar/sidecar/main.py`)

- FastAPI-App: `title="Sewer-Studio Vision Sidecar"`, `version="1.1.0"`, `description="Multi-Model Vision Pipeline (YOLO / Grounding DINO / SAM)"`.
- **`lifespan`** (asynccontextmanager): loggt Start (Host/Port/`models_dir` + effektive Devices), loest dann das Auth-Token auf und schaltet die Token-Pflicht scharf. Beim Shutdown ruft es `gpu_manager.unload_all()`.
- **Token-Aufloesung** `_resolve_or_create_token()` in Reihenfolge: 1) Env `SEWER_SIDECAR_AUTH_TOKEN`, 2) Token-Datei, 3) neu erzeugen (`secrets.token_urlsafe(32)`) und in die Datei schreiben. Token-Datei-Default (`_token_file_path()`): `%LOCALAPPDATA%/SewerStudio/.sidecar_token` — exakt der Pfad, den der C#-Client liest. Override via `SEWER_SIDECAR_AUTH_TOKEN_FILE`.
- **Sicherheits-Middleware** `enforce_loopback_security` (laeuft pro Request, vor den Routes):
  - **Host-Gate:** Host-Header wird normalisiert (`_normalize_host`, zieht Port/IPv6-Klammern ab) und gegen `trusted_hosts` geprueft (Default `127.0.0.1,localhost`). Nicht vertrauenswuerdig -> **403** `{"detail":"Untrusted host."}`. `*` in der Liste deaktiviert das Gate.
  - **Token-Gate:** Header **`X-Sidecar-Token`** muss per `hmac.compare_digest` (konstante Zeit) auf das Token passen. Fehlt/falsch -> **401** `{"detail":"Invalid or missing sidecar token."}`. **Wichtiger Fallstrick:** 401 heisst NICHT "Sidecar aus" — auch `/health` braucht den Token.
- **Zentraler Exception-Handler** `handle_unexpected` (nie roher 500-Stacktrace nach aussen):
  - CUDA-OOM (`_looks_like_oom`: Typname enthaelt "OutOfMemory" oder "out of memory" im Text) -> `gpu_manager.empty_cache()` + **503** `{"detail":"GPU out of memory"}`.
  - `FileNotFoundError` (Modell/Gewichte fehlen) -> **503** `{"detail":"model unavailable"}`.
  - sonst -> **500** `{"detail":"internal error"}` (voller Trace nur ins Log).
- Router-Reihenfolge: `health, yolo, dino, sam, training, warmup`.
- Logging: `level=INFO`, Format `"%(asctime)s [%(levelname)s] %(name)s: %(message)s"`.

### Konfiguration (`sidecar/sidecar/config.py`)

Klasse `SidecarSettings(BaseSettings)` mit `env_prefix="SEWER_SIDECAR_"` (also Feld `port` -> Env `SEWER_SIDECAR_PORT`). Vollstaendige Schluessel mit Defaults:

| Feld | Default | Bedeutung |
|---|---|---|
| `host` | `127.0.0.1` | Bind-Adresse |
| `port` | `8100` | HTTP-Port |
| `models_dir` | `./models` | Wurzel aller Gewichte (start_sidecar.ps1 setzt absoluten Pfad) |
| `gpu_device` | `cuda:0` | Default-Device fuer alle Modelle |
| `trusted_hosts` | `127.0.0.1,localhost` | Host-Gate-Whitelist (`*` = aus) |
| `auth_token` | `""` | Override-Token (sonst Datei/erzeugt) |
| `auth_token_file` | `""` | Override-Pfad der Token-Datei |
| `telemetry_enabled` | `True` | JSONL-Telemetrie an/aus |
| `telemetry_dir` | `""` | leer -> `%LOCALAPPDATA%/SewerStudio/Telemetry` |
| `training_export_root` | `./training_export` | Sandbox-Wurzel fuer `/training/export-yolo` |
| `training_max_image_bytes` | `25*1024*1024` | Limit pro Trainingsbild |
| `inference_max_image_bytes` | `25*1024*1024` | Limit pro Inferenzbild |
| `max_image_pixels` | `50_000_000` | Pixel-Obergrenze (Decompression-Schutz) |
| `yolo_device` / `dino_device` / `sam_device` | `""` | Per-Modell-Device-Override; leer -> `gpu_device` (Properties `effective_*_device`) |
| `yolo_confidence` | `0.25` | Default-Konfidenz Detektion |
| `yolo_imgsz` | `1280` | Inferenz-Aufloesung (1280 statt 640 fuer kleine Schaeden) |
| `yolo_model_name` | `yolo26m.pt` | Gewichtsdatei-Name (`.engine` = TensorRT) |
| `require_custom_yolo` | `False` | True -> kein COCO-Fallback, FileNotFoundError wenn Gewichte fehlen |
| `yolo_cls_model_path` | `""` | manueller Override fuer den VSA-Klassifikator |
| `yolo_cls_imgsz` | `1024` | cls-Aufloesung (v5_nocrop wurde mit 1024 trainiert) |
| `yolo_cls_preprocessing` | `letterbox` | `letterbox` (kein Crop) oder `default` |
| `yolo_cls_device` | `""` | leer -> `gpu_device` (Property `effective_cls_device`) |
| `frame_min_brightness` | `4.0` | Quality-Gate: dunkler -> `too_dark` |
| `frame_max_brightness` | `250.0` | heller -> `too_bright` |
| `frame_min_std` | `2.0` | weniger Std -> `too_uniform` |
| `frame_min_edge_var` | `1.0` | weniger Laplace-Varianz -> `too_blurry` |
| `dino_model_dir` | `auto` | `auto` bevorzugt Swin-B, sonst Swin-T OGC; sonst expliziter Ordner |
| `dino_box_threshold` | `0.25` | DINO Box-Schwelle |
| `dino_text_threshold` | `0.20` | DINO Text-Schwelle |
| `dino_labels` | langer Prompt | Default-Promptliste (` . `-getrennt, siehe unten) |
| `sam_backend` | `auto` | `auto`\|`sam2.1`\|`sam2`; SAM 1 ist entfernt |
| `sam2_weights_path` | `""` | expliziter Gewichtspfad |
| `sam2_model_cfg` | `auto` | `auto` leitet die `configs/sam2.1/...`-Yaml aus dem Dateinamen ab |
| `sam_min_score` | `0.5` | Masken mit Predictor-Score darunter werden verworfen (0.0 = Gate aus) |
| `bend_geometry_enabled` | `False` | geometrisches Bogen-Veto an/aus (Default aus = Verhalten der Sicherung 14.06.) |
| `sam3_enabled` | `False` | experimentell, AUS — siehe Hinweis |
| `sam3_weights_path` / `sam3_concept_labels` / `sam3_conf` (`0.25`) / `sam3_device` | div. | nur dormante Config-Schluessel |

`dino_labels` (Default-Prompt, ` . `-getrennt): `crack . fracture . break . deformation . corrosion . surface damage . erosion . root intrusion . roots . root mass . root ball . deposit . sediment . buildup . incrustation . scale . grease . obstacle . blockage . infiltration . water ingress . leak . displaced joint . open joint . offset joint . hole . collapse . missing wall . connection defect . pipe defect . intruding connection . protruding seal . lateral connection . junction . inlet . branch . side opening . pipe bend . bend`.

### GPU-Manager (`sidecar/sidecar/gpu_manager.py`)

- **Singleton** `gpu_manager = GpuModelManager()`.
- **Enum `ModelSlot(str, Enum)`:** `NONE="none"`, `YOLO="yolo"`, `DINO="dino"`, `SAM="sam"`. (Der YOLO-Klassifikator und SAM3 haengen NICHT an einem Slot.)
- **Locks:** je ein `threading.Lock` pro YOLO/DINO/SAM (Slot-Laden); ein `_global_lock` fuer dynamisches Lock-Anlegen.
- **`ensure_loaded(slot, device, loader)`:** Double-Check-Locking. Fast-Path wenn schon geladen (setzt `last_used = time.monotonic()`); sonst per-Slot-Lock, `loader()` aufrufen, `SlotState` (model, processor, device, load_time_sec, last_used) ablegen. Modelle bleiben **bewusst gleichzeitig resident** (Tempo) — KEINE automatische Eviction beim Slot-Wechsel.
- **VRAM-Budget:** `VRAM_BUDGET_GB` aus Env `SEWER_SIDECAR_VRAM_BUDGET_GB`, Default **`29`**. Nach jedem Laden `_warn_if_over_budget()` (loggt Warnung bei `torch.cuda.memory_allocated > Budget`, evictet aber nicht automatisch).
- **`evict_lru()`:** entlaedt den Slot mit kleinstem `last_used` (z.B. nach OOM aufrufbar).
- **`unload(slot)` / `unload_all()`:** `del model/processor`, `torch.cuda.empty_cache()`, `gc.collect()`.
- **`get_status()`** (fuer `/health`): liefert `current_model` (erstes geladenes oder `"none"`, Legacy), `vram_allocated_gb`, `vram_total_gb`, `vram_budget_gb`, `load_times_sec`, `loaded_models{slot: {device, load_time_sec}}`. Torch-Import ist defensiv (Sidecar laeuft auch ohne CUDA, dann 0.0).
- **Kein Monitor-Thread / kein periodisches Polling-Intervall in HEAD** — VRAM wird on-demand bei Laden/Status gemessen, nicht durch einen Hintergrund-Watcher.

### Routes

Alle Inferenz-Routes sind bewusst **`def` (sync), nicht `async`** — FastAPI fuehrt sync-Handler im Threadpool aus; als `async` wuerde die blockierende GPU-Inferenz den Event-Loop (und damit `/health`) blockieren. Inferenz wird zusaetzlich pro Modell mit einem `threading.Lock` serialisiert (Ultralytics/DINO/SAM predict sind nicht thread-sicher; parallele Threadpool-Requests sonst Race/OOM).

**`GET /health`** (`routes/health.py`, eigene `VERSION="1.2.0"`): liefert `status:"ok"`, `version`, `gpu` (= `gpu_manager.get_status()`), `yolo` (Runtime-Status), `classifier` (cls-Status), `models_present{dino,sam}` (leichter Glob-Praesenz-Check, kein Laden/Hash) und `device_config`. Zweck (Audit P2): degradierte Modelle sichtbar machen, statt "Full mode" zu behaupten.

**`POST|GET /warmup`** (`routes/warmup.py`): laedt YOLO, YOLO-cls, DINO, SAM vorab in den VRAM, damit die erste echte Analyse keinen Lade-Verzug hat. Idempotent, **best-effort** (ein scheiterndes Modell blockiert die anderen nicht, `_warm_one` faengt Exceptions und gibt `"fehler: <Typ>"`). Nutzt ein 64x64-Dummy-PNG. Antwort: `{warmup{...}, loaded[...], elapsed_sec, status}`. Wird vom C#-Start aufgerufen, sobald der Sidecar erreichbar ist.

**`POST /detect/yolo`** (`routes/yolo.py`) -> `YoloResponse`: ruft `yolo_wrapper.detect(image_base64, confidence_threshold)`, schreibt `yolo_detect`-Telemetrie.

**`POST /classify/yolo`** -> `YoloClassifyResponse`: Whole-Frame-VSA-Klassifikation (BCD/BCE/BCA/...) mit integriertem Quality-Gate; optional geometrisches Bogen-Veto (`bend_shift/is_bend/vanish_x/vanish_y`), nur wenn `bend_geometry_enabled` (sonst `0.0/False/0.5/0.5`). Antwort enthaelt Modell-Governance-Felder (`model_name/model_source/model_sha256/imgsz/preprocessing/device`).

**`POST /detect/dino`** (`routes/dino.py`) -> `DinoResponse`: `dino_wrapper.detect(...)`. `text_prompt=None` -> `dino_labels`.

**`POST /segment/sam`** (`routes/sam.py`) -> `SamResponse`: `sam_wrapper.segment(image_base64, bounding_boxes, pipe_diameter_mm)`.

**`POST /training/export-yolo`** (`routes/training.py`) -> `TrainingExportResponse`: schreibt Samples als YOLO-Datensatz (`images/{train,val}`, `labels/{train,val}`, `data.yaml`) in eine **Sandbox** unter `training_export_root` (`_resolve_output_dir` verhindert Pfad-Escapes -> 400). Vorab-Validierung (Groesse -> 413, ungueltiges base64 -> 400) **bevor** Ordner angelegt werden; Bilder werden lazy einzeln dekodiert (kein RAM-Spike). Shuffle + `train_split` (Default 0.8).

### Modell-Wrapper (`sidecar/sidecar/models/`)

**`yolo_wrapper.py` — Detektion:**
- Gewichts-Aufloesung `_resolve_yolo_model_path()`: 1) `models/yolo26m/<yolo_model_name>`, 2) flach `models/<yolo_model_name>`, 3) wenn nicht da und `require_custom_yolo=False` -> COCO-Fallback **`yolo11m.pt`** (Ultralytics laedt es selbst); wenn `require_custom_yolo=True` -> `FileNotFoundError`.
- Backend aus Suffix: `.engine`->tensorrt, `.pt/.pth`->pytorch, `.onnx`->onnx. Bei TensorRT wird die Klassen-Namensdatei `<engine>.names.json` (`{"names":{...}}`) geladen; fehlt sie -> Labels fallen auf `classN` zurueck (einmalige Warnung).
- Device: CPU-Pfad nutzt ein Modul-Singleton (`_cpu_model`), GPU-Pfad `gpu_manager.ensure_loaded(ModelSlot.YOLO,...)`. CUDA nicht verfuegbar -> Fallback `cpu` (ausser TensorRT).
- **Frame-Quality-Gate** `_is_frame_usable(img)`: Graustufen-Mittel/Std + Laplace-Kantenvarianz (scipy). Reihenfolge: `too_dark` < `frame_min_brightness`, `too_bright` > `frame_max_brightness`, `too_uniform` < `frame_min_std`, `too_blurry` < `frame_min_edge_var`. Unbrauchbar -> `is_relevant=False`, KEINE YOLO-Inferenz.
- `predict(source=np.array(img), conf, imgsz=yolo_imgsz, verbose=False)` unter `_yolo_predict_lock`. Mit Custom-Gewichten: `is_relevant = len(detections)>0`. Mit COCO-Fallback: `is_relevant=True` (Quality-Gate entscheidet, COCO-Boxen nur informativ).
- Telemetrie pro Response: `model_name/backend/device/queue_wait_ms/vram_*/gpu_utilization_percent` (Letztere via `nvidia-smi`-Aufruf mit 0.5s-Timeout).

**`yolo_wrapper.py` — VSA-Klassifikator (`classify_with_quality`, `classify`):**
- Modell-Aufloesung `_resolve_cls_model()` in Reihenfolge: 1) **`models/active.json`** (Eintrag `classifier`, einziger Schreiber: `model-promotion-warden`) mit **SHA-256-Verifikation** gegen die Gewichtsdatei (Mismatch = FEHLER, kein Laden), 2) `yolo_cls_model_path`-Override, 3) Legacy-Fallback (`yolo_cls_best.pt`/Grundgeruest-Laeufe, mit deutlicher Warnung). Liefert `{path, source, sha256, imgsz, preprocessing}` oder None.
- `active.json`-Beispiel (aktiver Stand): `vsa_cls_v5_nocrop`, `imgsz:1024`, `preprocessing:"letterbox"`, mit `sha256`.
- **Letterbox-Preprocessing** `_letterbox_rgb` (proportional skalieren + schwarz padden, kein Crop) — Pflicht fuer Paritaet zu `eval_cls.py`, sonst schneidet Ultralytics CenterCrop seitliche Rand-Schaeden (BAI/BAJ/BCA) ab. no-crop-Checkpoints picklen Transforms aus Modul `nocrop_patch` -> `_ensure_nocrop_module()` registriert `models/nocrop_compat.py` unter diesem Namen, damit `torch.load` ausserhalb des Trainingsordners gelingt.
- Top-K via `results[0].probs.data.topk(...)`, nur Klassen mit conf > 0.01.

**`dino_wrapper.py` — Grounding DINO:**
- Gewichts-/Config-Suche `_find_dino_files()`: bei `dino_model_dir=auto` Reihenfolge `grounding_dino_swinb` (Swin-B bevorzugt), dann `grounding_dino_1.5` (Swin-T OGC Fallback), `grounding_dino`, `groundingdino`. Braucht je eine `*config*.py`/`*cfg*.py` + `*.pth`/`*.pt`.
- Laedt via `groundingdino.util.inference.load_model`, Slot `ModelSlot.DINO`.
- Inferenz `predict(...)` unter `_dino_predict_lock`; Bild wird mit ImageNet-Normalisierung (`[0.485,0.456,0.406]/[0.229,0.224,0.225]`) transformiert. Box-Format `cx,cy,w,h` normiert -> absolute `x1,y1,x2,y2`.
- **Ehrlichkeit:** Inferenzfehler -> `degraded=True`, `error`, `error_code="dino_inference_failed"` (NICHT stilles "200 + leer"). Leer + `degraded=False` = echter Negativbefund.

**`sam_wrapper.py` — SAM 2.1 (prompt-basiert, Box->Maske):**
- Backend-Aufloesung `_resolve_sam_backend()`: `auto`/`sam2.1`/`sam2` -> sucht Gewichte in `models/sam2.1` dann `models/sam2` (oder `sam2_weights_path`). Keine Gewichte -> `FileNotFoundError`. **SAM 1 ist bewusst entfernt.**
- Lädt via `sam2.build_sam2` + `SAM2ImagePredictor`, Slot `ModelSlot.SAM`. Config-Yaml `_resolve_sam2_cfg` leitet aus dem Dateinamen ab (`large` -> `configs/sam2.1/sam2.1_hiera_l.yaml`, analog tiny/small/base_plus).
- `segment(...)` unter `_sam_predict_lock` (Predictor ist stateful: `set_image` + `predict` muessen pro Request atomar bleiben). Pro Box: `clamp_box` (Null-Flaeche/aus-Bild -> skip), `predict(box=..., multimask_output=False)`. **Score-Gate:** `score < sam_min_score` -> verworfen (`low_score_boxes++`). Maske wird **run-length-encodiert** (`_rle_encode`, Format `start_value,run1,run2,...`) inkl. `mask_area_pixels`, `centroid`, `bbox`.
- **Ehrlichkeit:** `requested_boxes`/`skipped_boxes`/`low_score_boxes`; `degraded = skipped_boxes>0`.

**`bend_geometry.py` — geometrisches Bogen-Signal (VSA-KEK BCC):** 1:1-Portierung des C#-`VanishingPointBendDetector`. Schwerpunkt der dunkelsten `DARKEST_FRACTION=0.15` Pixel = Tunnelende/Fluchtpunkt; seitlich verschoben -> Bogen. `BEND_SHIFT_THRESHOLD=0.12`. Liefert `BendResult(is_bend, shift∈[-0.5,0.5], vanish_x, vanish_y)`. Laeuft auf demselben `img_array` wie SAM (keine zweite Dekodierung). Per Default deaktiviert; C# erhaelt dann neutral `shift=0/is_bend=False/vanish=0.5`.

**`image_decode.py` — `decode_image_safe`:** zentrale, sichere base64-Dekodierung. Schutzkette: base64-Laenge -> 413, ungueltiges base64 -> 400, dekodierte Bytes > `max_bytes` -> 413, Pixelzahl > `max_pixels` -> 400, kein Bildformat -> 400. Gibt `Image.convert("RGB")`.

### Schemas (`sidecar/sidecar/schemas/`)

- **`YoloRequest`** `{image_base64, confidence_threshold=0.25 (0..1)}` -> **`YoloResponse`** `{is_relevant, detections[{x1,y1,x2,y2,class_name,confidence}], frame_class, inference_time_ms, model_name, model_backend, device, queue_wait_ms, vram_allocated_gb, vram_total_gb, gpu_utilization_percent}`.
- **`YoloClassifyRequest`** `{image_base64, top_k=5 (1..20)}` -> **`YoloClassifyResponse`** `{predictions[{class_name,confidence}], inference_time_ms, usable, quality_reason, model_name, model_source, model_sha256, imgsz, preprocessing, device, bend_shift, is_bend, vanish_x, vanish_y}`.
- **`DinoRequest`** `{image_base64, text_prompt?, box_threshold=0.25, text_threshold=0.20}` -> **`DinoResponse`** `{detections[{x1,y1,x2,y2,label,confidence,phrase}], inference_time_ms, degraded, error?, error_code?}`.
- **`BoundingBox`** `{x1,y1,x2,y2,label="",confidence=1.0}` (SAM-Input).
- **`SamRequest`** `{image_base64, bounding_boxes[BoundingBox] (max_length=256), pipe_diameter_mm?}` -> **`SamResponse`** `{masks[MaskResult], image_width, image_height, inference_time_ms, degraded, requested_boxes, skipped_boxes, low_score_boxes, error?, bend_shift, is_bend, vanish_x, vanish_y}`. `MaskResult` = `{label, confidence, bbox[4], mask_rle, mask_area_pixels, image_area_pixels, height_pixels, width_pixels, centroid_x, centroid_y}`.
- **`TrainingSample`** `{image_base64, labels[{class_name,x_center,y_center,width,height}]}`, **`TrainingExportRequest`** `{samples[], output_dir="./training_export", train_split=0.8 (0.1..1)}`.

### Telemetrie (`sidecar/sidecar/telemetry.py`)

Append-only **JSONL** unter `telemetry_dir` bzw. Default `%LOCALAPPDATA%/SewerStudio/Telemetry/sidecar.jsonl` (sonst `~/.sewerstudio/Telemetry/`). `write_event(type, fields)` schreibt `{timestamp_utc, event, ...}` thread-safe. Schreibfehler werden nur geloggt, nie zum Request-Fehler (Telemetrie darf Inferenz nie gefaehrden). Events: `yolo_detect`, `yolo_classify`, `dino_detect`, `sam_segment`.

### Start- und Build-Skripte

- **`start_sidecar.ps1`:** prueft `.venv`, aktiviert sie, GPU-Probe (`torch.cuda.is_available()`), Praesenz-Check der Modelle. Setzt Env-Defaults (`SEWER_SIDECAR_HOST=127.0.0.1`, `_PORT=8100`, `_MODELS_DIR=<abs>`), waehlt YOLO-Gewicht (`yolo26m.engine` wenn Engine vorhanden **und** CUDA da, sonst `yolo26m.pt`), setzt `_REQUIRE_CUSTOM_YOLO` (1 wenn Custom-Gewichte da). Startet zwingend mit dem **venv-Python**: `& .venv\Scripts\python.exe -m uvicorn sidecar.main:app --host ... --port ... --log-level info`. **Fallstrick (dokumentiert):** das mehrdeutige `python` fiel auf System-Python ohne torch/ultralytics zurueck -> Sidecar lief, lud aber nie Modelle.
- **`setup.ps1`:** legt `.venv` an (bevorzugt `uv`, sonst `python -m venv`), installiert `requirements-lock.txt` mit `--extra-index-url https://download.pytorch.org/whl/nightly/cu128`. Harte Abbruch-Checks: cu121-Lock auf einer RTX-50xx (sm_120) verboten; nach Install `torch.cuda.is_available()` muss True sein, sonst Fehler (falscher CUDA-Build).
- **`build_engine.ps1`** (hardware-gebunden, auf der Ziel-GPU laufen): exportiert `yolo26m.pt` -> ONNX (Ultralytics, `imgsz=640`, `opset=17`) -> TensorRT-Engine mit **fp16** (via `trtexec` falls vorhanden, sonst Python-TensorRT-API). Schreibt `yolo26m.names.json` (Klassennamen + SHA), `.build.json`-Metadaten und sichert die alte Engine nach `engine_backups/`.

### Abhaengigkeiten

- **`pyproject.toml`** (Mindestversionen): `fastapi>=0.110`, `uvicorn[standard]>=0.29`, `pydantic>=2.0`, `pydantic-settings>=2.0`, `torch>=2.1`, `torchvision>=0.16`, `ultralytics>=8.2`, `Pillow>=10.0`, `numpy>=1.24`. Extras: `dino`=`groundingdino-py>=0.4`, `sam`/`sam2`=`sam-2 @ git+https://github.com/facebookresearch/sam2.git`, `dev`=`pytest>=8.0,httpx>=0.27`. pytest-Marker: Default-Lauf `-m 'not gpu and not e2e'` (schnell, ohne GPU); `gpu`/`e2e`/`slow` laden echte Modelle.
- **`requirements.txt`** ergaenzt `tensorrt-cu12>=10.0`, `onnx>=1.16`, `scipy>=1.10`, `transformers>=4.38,<5` sowie `groundingdino-py>=0.4` und das SAM-2-Git-Paket. Index-Header: `--extra-index-url .../whl/nightly/cu128` und `https://pypi.nvidia.com`.
- **`requirements-lock.txt`** (eingefroren, exakte Pins, Auswahl): `torch==2.12.0.dev20260408+cu128`, `torchvision==0.27.0.dev20260407+cu128`, `ultralytics==8.4.56`, `fastapi==0.136.3`, `uvicorn==0.48.0`, `pydantic==2.13.4`, `pydantic-settings==2.14.1`, `numpy==2.4.4`, `scipy==1.17.1`, `onnx==1.21.0`, `tensorrt-cu12==10.16.1.11`, `groundingdino-py==0.4.0`, `transformers==4.57.6`, `SAM-2 @ git+...@2b90b9f5...`. **Header-Caveat:** cu128-Nightly ist Pflicht fuer RTX 5090 (sm_120); cu121 brickt die GPU-Pipeline. Nightly-Wheels koennen vom Index verschwinden -> bei Re-Sync auf aktuelles cu128 wechseln, aber **niemals auf cu121**. Backup des alten Locks: `requirements-lock.cu121-backup.txt`.

### Default-Modellgewichte & Verzeichnis `models/`

Layout (Gewichte nicht im Repo):
- `models/yolo26m/` — Detektion: `yolo26m.pt` (PyTorch), `yolo26m.engine` + `yolo26m.names.json` + `.onnx`/`.build.json` (TensorRT). COCO-Fallback `yolo11m.pt` (Auto-Download durch Ultralytics).
- `models/active.json` — VSA-Klassifikator-Pointer (aktiv `vsa_cls_v5_nocrop`, `weights_path` zeigt nach `C:\KI_BRAIN\...`, `imgsz:1024`, `preprocessing:letterbox`, mit SHA-256). Einziger Schreiber: `model-promotion-warden`.
- `models/grounding_dino_swinb/` — `GroundingDINO_SwinB_cfg.py` + `groundingdino_swinb_cogcoor.pth` (bevorzugt).
- `models/grounding_dino_1.5/` — `GroundingDINO_SwinT_OGC.cfg.py` + `groundingdino_swint_ogc.pth` (Fallback).
- `models/sam2.1/` — `sam2.1_hiera_large.pt` (produktiver Prompt-Segmenter).

**Wichtige Klarstellung zu SAM (HEAD weicht von einer aelteren Notiz ab):** Der produktive Segmenter ist **SAM 2.1 (`sam2.1_hiera_large.pt` unter `models/sam2.1/`)** ueber `SAM2ImagePredictor`, **nicht** SAM-1 `vit_h` und **nicht** SAM 3. SAM 3 existiert nur als **per Default deaktivierte** Experiment-Option ueber die Config-Schluessel `sam3_*` (v. a. `sam3_weights_path`) — ohne eigenen Wrapper oder Route im HEAD, von keinem Endpunkt geladen. Die fruehere Platzhalter-Ablage `models/sam3/` ist im HEAD **entfernt** (`.gitkeep` geloescht); nicht mehr als Verzeichnis annehmen.

Relevante Pfade: `c:\Sewer-Studio_KI_4.4\sidecar\sidecar\main.py`, `…\config.py`, `…\gpu_manager.py`, `…\telemetry.py`, `…\routes\{health,warmup,yolo,dino,sam,training}.py`, `…\models\{yolo_wrapper,dino_wrapper,sam_wrapper,bend_geometry,image_decode,box_utils,nocrop_compat}.py`, `…\schemas\{detection,segmentation}.py`, `c:\Sewer-Studio_KI_4.4\sidecar\{start_sidecar,setup,build_engine}.ps1`, `…\{pyproject.toml,requirements.txt,requirements-lock.txt}`, `…\models\active.json`.

## A6 · Ollama / Qwen-Integration (lokales VLM für Bild- und Code-Analyse)

Dieses Teilsystem stellt den C#-seitigen Zugang zum lokalen **Ollama**-Server bereit, über den die **Qwen3-VL**-Modelle (Vision/Text) und das Embedding-Modell **nomic-embed-text** laufen. Es liefert ausschließlich Text bzw. strukturiertes JSON; jegliche Geschäftslogik (VSA-Mapping, Dedup, QualityGate) bleibt in C# (Thin-AI-Prinzip). Code liegt unter `src/AuswertungPro.Next.Infrastructure/Ai/` und `src/AuswertungPro.Next.Infrastructure/Ai/Ollama/`, die Config-Records unter `src/AuswertungPro.Next.Application/Ai/`.

### Verwendete Modelle und Aufgaben-Trennung

| Rolle | Modell | Wo definiert / Default | Zweck |
|---|---|---|---|
| Vision (Primary, große GPU) | `qwen3-vl:8b-q8` | `GpuModelSelector.LargeModel` | Einzelframe-/Video-Bildanalyse, VSA-Code-Hint, Quantifizierung. Bewusst **8B-Q8**, nicht 32B, damit neben dem VLM der Sidecar-Stack (YOLO/DINO/SAM) ins 29-GB-VRAM-Budget passt (~11,7 GB für das VLM). |
| Vision (kleine GPU / Laptop) | `qwen3-vl:2b` | `GpuModelSelector.SmallModel`, auch `OllamaConfig.DefaultVisionModel` | Laptop-Mode, ~2 GB. |
| Text/Entscheider | `qwen3-vl:2b` | `OllamaConfig.DefaultTextModel` | VSA-Code-Vorschlag aus Findings + KB-Kontext (strukturiertes JSON). In der Praxis identisch zum Vision-Default; `TextModel` ist eigenständig konfigurierbar. |
| 32B-Referenz | `qwen3-vl:32b` | **nicht** Laufzeit-Default; nur RAM-Referenz/manuell | Wird im HEAD **nicht** automatisch zur Laufzeit geladen. |
| Embeddings | `nomic-embed-text` | `OllamaConfig.DefaultEmbedModel` | KnowledgeBase-Embeddings, `POST /api/embed`. |

**KRITISCH (Fallstrick, A/B Juni 2026):** Die Qwen**2.5**-VL-Familie lieferte 0 % (Parse-Fehler). Defaults und der Auto-Modus dürfen **nie still auf `qwen2.5*` zurückfallen** — nur `qwen3-vl` ist freigegeben. Diese Warnung steht als Kommentar in `OllamaConfig.cs` und `GpuModelSelector.cs` und ist beim Nachbau einzuhalten.

**Keine 8B→32B-Laufzeit-Eskalation:** Die Modellwahl ist **statisch** (einmalig beim Settings-Laden über VRAM-Schwellen). Es gibt keinen Mechanismus, der zur Laufzeit von 8B auf 32B hochschaltet. Nicht als implementiert annehmen.

### Konfiguration: `OllamaConfig` und `AiPlatformSettings`

`OllamaConfig` (`src/.../Infrastructure/Ai/Ollama/OllamaConfig.cs`) ist der schlanke Vertrag, den die Ollama-Clients konsumieren:

```csharp
public sealed record OllamaConfig(
    Uri BaseUri, string VisionModel, string TextModel, string EmbedModel,
    TimeSpan RequestTimeout,
    string KeepAlive = "24h",      // DefaultKeepAlive
    int NumCtx = 8192);            // DefaultNumCtx
```
Default-Konstanten: `DefaultVisionModel = "qwen3-vl:2b"`, `DefaultTextModel = "qwen3-vl:2b"`, `DefaultEmbedModel = "nomic-embed-text"`, `DefaultKeepAlive = "24h"`, `DefaultNumCtx = 8192`.

Das umfassende Settings-Record ist `AiPlatformSettings` (`src/.../Application/Ai/AiSettings.cs`). Es enthält die Ollama-Felder (`OllamaBaseUri`, `VisionModel`, `TextModel`, `EmbedModel`, `OllamaRequestTimeout`, `OllamaKeepAlive`, `OllamaNumCtx`) sowie Sidecar-/Pipeline-Felder. `AiSettingsOllamaExtensions.ToOllamaConfig(this AiPlatformSettings)` projiziert die Ollama-Teilmenge in ein `OllamaConfig`. Es existiert zusätzlich ein schlankes `AiRuntimeSettings` (über `ToRuntimeSettings()`) und ein `AiSettingsSource`-Record für die Eingangsquelle (AppSettings/Env).

**Default-/Override-Auflösung** (`AiSettingsFactory.Load`, `src/.../Infrastructure/Ai/Configuration/AiSettingsFactory.cs`) — Reihenfolge je Wert: explizit gesetzter Source-Wert → Environment-Variable → Fallback-Konstante. Env-Variablen tragen Präfix `SEWERSTUDIO_`, werden aber auch als `AUSWERTUNGPRO_*` akzeptiert (Aliasing in `Env()`):
- `SEWERSTUDIO_OLLAMA_URL` → sonst `http://localhost:11434`
- `SEWERSTUDIO_AI_VISION_MODEL` (leer oder `"auto"` ⇒ GPU-Auto-Select, siehe unten)
- `SEWERSTUDIO_AI_TEXT_MODEL` → sonst `DefaultTextModel`
- `SEWERSTUDIO_AI_EMBED_MODEL` → sonst `DefaultEmbedModel`
- `SEWERSTUDIO_AI_TIMEOUT_MIN` → sonst **5** Minuten (`OllamaRequestTimeout`)
- `SEWERSTUDIO_OLLAMA_KEEP_ALIVE` → sonst `"24h"`
- `SEWERSTUDIO_OLLAMA_NUM_CTX` → sonst der vom GPU-Profil gelieferte `numCtxDefault` bzw. `DefaultNumCtx`
- `SEWERSTUDIO_AI_ENABLED` (1/true)

### GPU-Modellwahl: `GpuModelSelector` (statisch, nach VRAM)

`GpuModelSelector` (`src/.../Infrastructure/Ai/Ollama/GpuModelSelector.cs`) ist eine **statische** Klasse, die per `nvidia-smi --query-gpu=memory.total,name --format=csv,noheader,nounits` (über `ExternalProcessRunner.RunAsync`, 5 s Timeout) den Gesamt-VRAM ermittelt und genau einmal das Modell wählt:

- VRAM ≥ `LargeModelThresholdMb = 24_000` → `LargeModel = "qwen3-vl:8b-q8"`, `LargeModelNumCtx = 12288`
- VRAM ≥ `SmallModelThresholdMb = 8_000` → `SmallModel = "qwen3-vl:2b"`, `SmallModelNumCtx = 4096`
- darunter → `SmallModel` mit `SmallModelNumCtx`, Hinweis "KI-Vision evtl. eingeschränkt"

`nvidia-smi` wird in dieser Reihenfolge gesucht: `System32\nvidia-smi.exe` → `ProgramFiles\NVIDIA Corporation\NVSMI\nvidia-smi.exe` → PATH. Fehlt es, wird das kleine Modell als Fallback gewählt. Ergebnis ist ein `GpuProfile(ResolvedModel, ResolvedNumCtx, VramTotalMb, GpuName, Reason)`. `IsAutoMode(modelName)` ⇒ `true` bei leer oder `"auto"`. Der Auto-Pfad wird in `AiSettingsFactory.Load` ausgelöst: ist der konfigurierte Vision-Wert Auto, wird `DetectAndSelect()` aufgerufen und sowohl `vision` als auch `numCtxDefault` daraus übernommen. Tests: `GpuModelSelectorProcessSafetyTests.cs`.

### HealthCheck und Modell-Auflösung

`OllamaHealthCheck` (`src/.../Ai/Ollama/OllamaHealthCheck.cs`): `CheckAsync` ruft `GET {BaseUri}/api/tags`, parst das `models[].name`-Array und liefert `HealthResult.Ok(models)` bzw. `HealthResult.Fail(error)`. `IsModelAvailableAsync(modelName)` prüft per Präfix-Match. Fehler/Unerreichbarkeit ⇒ `IsOnline = false` (kein Throw außer `OperationCanceledException`).

`OllamaModelResolver` (`src/.../Ai/Ollama/OllamaModelResolver.cs`, statisch):
- `ResolveBestInstalledModel(preferredModel, installedModels)`: exakte (case-insensitive) Übereinstimmung bevorzugt; sonst gleiche **Familie** (Teil vor `:`) mit kleinster `b`-Größe (Regex `:(?<size>\d+(?:\.\d+)?)b\b`); sonst `null`.
- `ClampNumCtxForVideoAnalysis(requested)`: deckelt auf **2048** (`safeNumCtx`); `≤0` ⇒ 2048; niedrigere Werte bleiben erhalten. Schützt die Video-Vollanalyse vor zu großem Kontext.

`OllamaClient.ListModelNamesAsync` liefert die installierten Modellnamen; die UI (`PlayerWindow.LiveDetection.cs`) nutzt das, um auf ein installiertes Modell zurückzufallen, wenn das konfigurierte fehlt. Tests: `OllamaModelResolverTests.cs`.

### HTTP-Client: `OllamaClient`

`OllamaClient` (`src/.../Infrastructure/Ai/OllamaClient.cs`) ist ein minimaler HTTP-Client. Konstruktor: `OllamaClient(Uri baseUri, HttpClient? http = null, TimeSpan? ownedTimeout = null, string keepAlive = "24h", int numCtx = 0)`. Bei selbst erzeugtem HttpClient gilt `ownedTimeout` (sonst 5 min Default). Jeder Request setzt `stream=false` und `keep_alive=_keepAlive`. `ApplyNumCtx` injiziert `options.num_ctx` nur wenn `_numCtx > 0` und noch nicht gesetzt.

Endpunkte/Methoden:
- `GenerateAsync(model, prompt, imagesBase64?, ct)` → `POST /api/generate`, liest `response`. Wird vom Legacy-Vision-Pfad genutzt.
- `ChatAsync(model, messages, ct)` → `POST /api/chat`, liest `message.content`. `ChatMessage(Role, Content, ImagesBase64?)`; Bilder werden als Base64-Array `images` pro Message angehängt.
- `ChatStructuredAsync<T>(model, messages, formatSchema, ct)` und `ChatStructuredWithOptionsAsync<T>(model, messages, formatSchema, options?, ct)` → `POST /api/chat` mit `format = <JSON-Schema>`. **Strukturierter Pfad ist der Standard für alle Qwen-Outputs.** Antwort wird über `JsonDefaults.CaseInsensitive` nach `T` deserialisiert; ist `message.content` leer/fehlerhaft ⇒ `InvalidOperationException` mit Roh-Inhalt (kein Freitext-Parsing als Fallback).

### JSON-Schema-Zwang (strict, kein Freitext)

Alle Qwen-Aufrufe nutzen Ollamas `format`-Feld mit einem strikten JSON-Schema (`additionalProperties: false`, `required`). Es gibt zwei Haupt-Schemas, beide als `static readonly JsonElement` im Code eingebettet:

1. **Bild-Analyse — `EnhancedVisionSchema`** in `EnhancedVisionAnalysisService.cs` (`src/.../Infrastructure/Ai/`). Top-Level: `meter`, `time_in_video`, `pipe_material` (enum: beton/steinzeug/pvc/pe/gfk/stahl/unbekannt), `pipe_diameter_mm`, `findings[]`, `image_quality` (enum: gut/mittel/schlecht), `is_empty_frame`. Jedes `findings`-Element: `label` (required), `vsa_code_hint`, `severity` 1–5 (required), `position_clock` (Uhrlage), `extent_percent`, `height_mm`, `width_mm`, `intrusion_percent`, `cross_section_reduction_percent`, `diameter_reduction_mm`, `bbox` ([x1,y1,x2,y2] normiert 0–1), `notes`. `required: [meter, findings, image_quality, is_empty_frame]`.

2. **Code-/Entscheider-Analyse — `ProtocolSuggestionSchema`** in `OllamaProtocolAiService.cs`. Felder: `suggestedCode` (string|null), `rationale` (string|null), `required: [suggestedCode]`. DTO `ProtocolSuggestionDto`.

Deterministik-Optionen (`OllamaDeterministicOptions.Create()`, `src/.../Ai/OllamaDeterministicOptions.cs`) werden beiden strukturierten Aufrufen mitgegeben: `temperature = 0`, `seed = 42`, `num_ctx = 12288`. Tests: `DeterministicOllamaRequestTests.cs`.

### Bild-Analyse-Service: `EnhancedVisionAnalysisService`

Konstruktor `(OllamaClient client, string model, ICodeCatalogProvider? codeCatalog = null)`. Aufrufer übergeben das **VisionModel**. Konstruiert in `VideoAnalysisPipelineService`, `VideoFullAnalysisService`, `PlayerWindow.Coding.cs`, `TrainingCenterViewModel`, sowie den Tools `EvalSetBenchmark`/`ClassifierPilot`/`SelfTrainingHarness`.

Kern: `AnalyzeAsync` / `AnalyzeWithObservationHintsAsync` / `AnalyzeWithContextAsync`. Alle bauen per `BuildPrompt(...)` einen deutschen Vision-Prompt und rufen `ChatStructuredWithOptionsAsync<EnhancedVisionDto>` mit dem Bild als einzelner `user`-Message (`ImagesBase64: [framePngBase64]`), `EnhancedVisionSchema` und den Deterministik-Optionen. Per-Frame-Timeout via verlinktem `CancellationTokenSource`: `FrameTimeout = 120 s` (innerer Cap gewinnt gegen äußere Pipeline-Timeouts). Timeout/Exception ⇒ leeres `EnhancedFrameAnalysis` mit `AnalysisOutcome.Timeout`/Fehler statt Crash.

Prompt-Bausteine (alle Deutsch, im Code):
- `BuildPrompt`: Aufgabenliste (Meterstand aus OSD lesen — Knotennummern ≥5-stellig ignorieren, Meter < 500; Material/Durchmesser; Schäden mit Severity 1–5; Uhrlage; Bildqualität; Schadensmaße; `vsa_code_hint`; bei Unsicherheit Hauptcode statt `???`; bbox). Severity-Skala = VSA-Zustandsklasse.
- `BuildDamageClassesPrompt(codeCatalog)`: VSA-KEK-Katalogauszug; Titel werden — wenn `ICodeCatalogProvider` vorhanden — aus dem aktiven Katalog nachgeschlagen (`LookupCatalogTitle`, exakt oder 3-stelliger Hauptcode), sonst Fallback-Titel.
- `BuildImportContextSection`: bekannte Protokoll-Befunde als Erwartungshorizont.
- `BuildObservationHintsSection`: unsichere Bild-Hinweise (z. B. YOLO-cls) — ausdrücklich **nicht** als VSA-Code zu übernehmen.
- `BuildContextPrompt`: DINO-Detections + SAM-quantifizierte Masken (Höhe/Breite/Ausdehnung/Querschnitt/Uhrlage über `MaskQuantificationService.QuantifyAll`) + optional vorheriger Befund (temporale Kohärenz).

Nachverarbeitung in C# (kein LLM-Vertrauen blind): `ValidateCodeHint` verwirft erfundene/unbekannte Codes gegen den Katalog; `NormalizeBbox` ordnet/clamped die Box auf [0,1] und verwirft degenerierte; `MeterPlausibility.Sanitize` deckelt Meter auf 0–500 m (fehlgelesene Knotennummern → null); Severity wird auf 1–5 geklemmt.

### Code-/Entscheider-Service: `OllamaProtocolAiService`

`OllamaProtocolAiService : IProtocolAiService` (`src/.../Infrastructure/Ai/`). Konstruktor erhält `OllamaConfig`, `ffmpegPath`, optional `IProtocolAiTrainingSampleProvider`, `IRetrievalService`, `IAiSuggestionPlausibilityService`. Intern: ein eigener `OllamaClient` (mit `config.KeepAlive`/`config.NumCtx`) plus ein `OllamaVisionFindingsService` für die Vision-Vorstufe.

Ablauf in `SuggestAsync(AiInput, ct)`:
1. Guard: deaktiviert ⇒ `null`; ohne `AllowedCodes` ⇒ `AiSuggestion` mit Flag `no_catalog`.
2. Bild beschaffen: Video→Frame via `VideoFrameExtractor.TryExtractFramePngAsync(ffmpeg, ...)` oder erstes Foto; Base64.
3. Vision-Vorstufe (`OllamaVisionFindingsService.AnalyzeAsync`, Legacy `/api/generate`, freies kleines JSON `{meter, findings[], severity}` mit Robust-Extraktion über `JsonObjectExtractor`).
4. KB-Retrieval (`TrySuggestFromKnowledgeBaseAsync`): Cosine-Retrieval über `IRetrievalService`, Meter-Gewichtung, nur erlaubte Codes, aggregierter Confidence-Score.
5. Entscheider-Prompt (`BuildPrompt`) mit Findings, Trainingsbeispielen, KB-Hinweisen, erlaubten Codes ⇒ `ChatStructuredWithOptionsAsync<ProtocolSuggestionDto>` mit System-Message *"Du bist ein Kanalinspektion-Experte nach VSA-Standard. Antworte ausschließlich im vorgegebenen JSON-Format."*, `ProtocolSuggestionSchema`, Deterministik-Optionen.
6. Verifikation: LLM-Code muss in `AllowedCodes` liegen, sonst Flag `llm_structured_failed`; Fallback auf KB-Code mit Flag `kb_fallback`; Abweichung ⇒ `kb_disagrees`. Confidence: LLM-Treffer ≥ 0,65, reiner KB-Fallback ≥ 0,55. Optional `IAiSuggestionPlausibilityService.ApplyChecks`. Bei strukturiertem JSON-Fehler **kein** Freitext-Parsing, sondern KB-Fallback.

### Embeddings: `EmbeddingService`

`EmbeddingService(HttpClient http, OllamaConfig config)` (`src/.../Infrastructure/Ai/KnowledgeBase/`). `EmbedAsync(text)` → `POST {BaseUri}/api/embed` mit `{ model = config.EmbedModel, input = text }`, liest `embeddings[0]` → `float[]`. Fehler/unerreichbar ⇒ `null` (best-effort, kein Throw außer Cancel). `ToBlob`/`FromBlob` serialisieren Vektoren für die SQLite-KB (Längenprüfung Vielfaches von `sizeof(float)`).

### Lifecycle / Startup (KeepAlive, Preload, Resident-Check)

`AiStartupService` (`src/.../UI/Services/AiStartupService.cs`) orchestriert den KI-Start:
- `ApplyRuntimeDefaults`: setzt `AiEnabled=true`, `PipelineMultiModelEnabled=true`, `PipelineMode="multimodel"`, Ollama-URL `http://localhost:11434`, Sidecar-URL `http://localhost:8100`, `AiOllamaKeepAlive="24h"`.
- Ollama-Erreichbarkeit: `GET /api/tags`; nicht erreichbar ⇒ `ollama serve` im Hintergrund starten, dann bis 40 s warten (Kaltstart inkl. GPU-Discovery).
- Preload: für Vision-, Text- und Embed-Modell (`BuildOllamaPreloadRequests`, dedupliziert). Generate-Modelle via `POST /api/generate` mit leerem Prompt + `keep_alive`; Embed-Modell via `POST /api/embed`. Danach **Resident-Verifikation** über `GET /api/ps` (`IsOllamaModelResidentAsync`, Präfix-/Familien-Match); ist das Modell nicht resident, einmaliges Nachladen.
- Sidecar (`/health`, X-Sidecar-Token), `/warmup` für YOLO/DINO/SAM/Classifier — gehört zum Vision-Sidecar, nicht zu Ollama.

`keep_alive` wird durchgängig aus der Config gesetzt (Default `"24h"`), damit Modelle im VRAM resident bleiben und die erste Analyse keinen Lade-Verzug hat. **Fallstrick VRAM-Budget:** Da Qwen-VL mit `keep_alive=24h` resident bleibt und der Sidecar (YOLO/DINO/SAM) ebenfalls Speicher belegt, ist die statische 8B-Wahl (nicht 32B) zwingend, um das 29-GB-Budget einzuhalten; niemals alle großen Modelle gleichzeitig.

### Relevante Dateipfade (Zusammenfassung)
- `src/AuswertungPro.Next.Infrastructure/Ai/Ollama/OllamaConfig.cs`, `GpuModelSelector.cs`, `OllamaHealthCheck.cs`, `OllamaModelResolver.cs`, `AiSettingsOllamaExtensions.cs`
- `src/AuswertungPro.Next.Infrastructure/Ai/OllamaClient.cs`, `OllamaDeterministicOptions.cs`, `OllamaVisionFindingsService.cs`, `OllamaProtocolAiService.cs`, `EnhancedVisionAnalysisService.cs`
- `src/AuswertungPro.Next.Infrastructure/Ai/Configuration/AiSettingsFactory.cs`
- `src/AuswertungPro.Next.Infrastructure/Ai/KnowledgeBase/EmbeddingService.cs`
- `src/AuswertungPro.Next.Application/Ai/AiSettings.cs` (`AiPlatformSettings`, `AiSettingsSource`, `AiRuntimeSettings`)
- `src/AuswertungPro.Next.UI/Services/AiStartupService.cs`
- Tests: `tests/AuswertungPro.Next.Pipeline.Tests/{OllamaClientTests,OllamaModelResolverTests,DeterministicOllamaRequestTests,EnhancedVisionAnalysisServiceTests,AiSettingsTests}.cs`, `tests/AuswertungPro.Next.Infrastructure.Tests/GpuModelSelectorProcessSafetyTests.cs`

## A7 · KnowledgeBase, Self-Training und Eval

Dieses Teilsystem implementiert das "Gehirn" der App: eine SQLite-Wissensdatenbank (KB) mit Vektor-Embeddings für Few-Shot-Retrieval, einen JSON-Trainingsspeicher, die Self-Training-Pipeline (Protokoll-Foto + blinde KI-Analyse → deterministischer Vergleich → Trainingssample), die Review-/Gold-Fund-Logik und ein eingefrorenes Eval-Set mit Hash-Freeze und clean/hidden-Split. Architekturleitlinie (Thin-AI): **C# trifft alle Entscheidungen** (Indexwürdigkeit, Auto-Accept, Eval-Schutz, Matching, Quality-Gating); das LLM/Ollama liefert ausschließlich Text (Embeddings via `nomic-embed-text`, Bildanalyse via Qwen-VL).

### Speicherorte und Pfad-Auflösung (`KnowledgeBasePaths`)

Alle KB-/Trainingsdaten liegen unter einer einzigen **Knowledge-Root**. Auflösungsreihenfolge in `ResolveRoot` (`src/AuswertungPro.Next.Infrastructure/Ai/KnowledgeBase/KnowledgeBasePaths.cs`):
1. expliziter `settingsOverride` (Argument), sonst
2. Umgebungsvariable **`SEWERSTUDIO_KNOWLEDGE_ROOT`** (getrimmt), sonst
3. `%LOCALAPPDATA%\SewerStudio\Knowledge` (bzw. `SEWERSTUDIO_APPDATA_DIR\Knowledge`, falls gesetzt).

Konkrete Pfade unter der Root:
- `KnowledgeBase.db` (`GetKnowledgeDbPath`)
- `training_samples.json` (`GetTrainingSamplesPath`)
- `training_settings.json`, `frames/` (Unterordner), `measures_learning.json`, `measures-model.zip`.

Fallstrick: Beim Start läuft (nur wenn weder Override noch Env-Var gesetzt sind, einmalig) eine **Legacy-Migration** `TryMigrateFromAppData`, die Alt-Daten aus `%APPDATA%\AuswertungPro\...` in die neue Root kopiert (DB inkl. `-wal`/`-shm`, JSONs, Frames). Wird `SEWERSTUDIO_KNOWLEDGE_ROOT` gesetzt, findet **keine** Migration statt. Die Root wird gecacht (`GetRoot`); `InvalidateCache()` nach Root-Wechsel aufrufen. Im Produktivbetrieb zeigt die Env-Var auf das echte Gehirn (z.B. `C:\KI_BRAIN`).

### SQLite-Schema (`KnowledgeBaseContext`)

Eine `SqliteConnection` (Microsoft.Data.Sqlite), `PRAGMA journal_mode=WAL` und `PRAGMA busy_timeout=3000` (konkurrierender Rebuild vs. Retrieval). `EnsureSchema()` legt idempotent an:
- **`Samples`**: `SampleId` (PK), `CaseId`, `VsaCode`, `Beschreibung`, `MeterStart`, `MeterEnd`, `IsStreck`, `FramePath`, `ExportedUtc`, `VersionId` und per `MigrateAddColumn` nachgerüstet `SourceType` und **`QualityGateLevel`** ("Green"/"Yellow"/"Red"). Index `idx_samples_code` auf `VsaCode`.
- **`Embeddings`**: `SampleId` (PK), `Model` (Embedding-Modellname), `Vector` (BLOB), `CreatedAt`. Index `idx_embeddings_model`.
- **`Versions`**: `VersionId` (PK), `CreatedAt`, `SampleCount`, `Notes` (Export-Snapshots).
- **`CategoryWeights`** und **`ValidationLog`**: für den selbstverbessernden QualityGate-Loop (per-Kategorie-Gewichte bzw. Validierungsprotokoll); im KB-/Self-Training-Pfad nur Schema-seitig relevant.

Migrationen sind additiv (`ALTER TABLE ... ADD COLUMN`, "duplicate column"-Exception wird geschluckt) — neue Spalten immer hinten anhängen, damit bestehende SELECT-Ordinale stabil bleiben.

### Embeddings (`EmbeddingService`)

`EmbedAsync(text, ct)` ruft Ollama `POST /api/embed` mit `{ model = config.EmbedModel, input = text }`. `config.EmbedModel` Default = **`nomic-embed-text`** (`OllamaConfig.DefaultEmbedModel`). Antwortformat `{ "embeddings": [[...]] }`, erster Vektor wird als `float[]` zurückgegeben. **Gibt bei Ollama-Fehler/leerem Text `null` zurück** (kein Throw außer `OperationCanceledException`) — Aufrufer behandeln `null` als "kein Embedding". `ModelName` = konfiguriertes Modell, wird beim Speichern in `Embeddings.Model` geschrieben.
Serialisierung: `ToBlob(float[])` → `byte[]` (`Buffer.BlockCopy`, little-endian), `FromBlob(byte[])` zurück; `FromBlob` wirft `ArgumentException`, wenn die Länge kein Vielfaches von `sizeof(float)` ist.

### Indexierung (`KnowledgeBaseManager`)

Konstruktor: `(KnowledgeBaseContext db, EmbeddingService embedder, IReadOnlySet<string>? evalImageHashes, IReadOnlySet<string>? evalHaltungKeys)`. Implementiert `ITrainingSampleIndexer`.

**Index-Würdigkeit** `IsIndexWorthy(sample)` (statisch, hartes Gate vor jedem Schreiben):
1. `Beschreibung` nicht leer und ≥ 10 Zeichen,
2. `Code` nicht leer und `VsaCodeResolver.LookupLabel(code)` ≠ null (Code muss im Katalog existieren),
3. `TrainingSampleEligibility.Evaluate(sample)` muss `IsEligible` sein (Datum/Herkunft),
4. `TrainingSamplePlausibility.IsFachlichPlausibel(sample, out reason)` (D7: kein fachlicher Müll).

**Eval-Kontaminationsschutz** `IsEvalContaminated(sample)`: true, wenn (a) der Frame inhaltsgleich zu einem Eval-Bild ist (Hash, über `evalImageHashes`) ODER (b) die `CaseId`/Haltung in `evalHaltungKeys` liegt. Aktiv nur, wenn der jeweilige Satz `Count > 0` ist; sonst inaktiv.

Schreibwege (alle prüfen ZUERST Eval-Kontamination, dann Indexwürdigkeit, dann Embedding):
- `IndexSampleAsync(sample)`: erzeugt Embedding; bei `null` → `false`. Sonst **atomar** `UpsertSample` + `UpsertEmbedding` in einer Transaction (kein Sample ohne Embedding).
- `IndexSamplesAsync(list)`: Phase 1 Embeddings sequenziell (Ollama single-request), Phase 2 eine Transaction für alle UPSERTs.
- `RebuildAsync(list, progress, ct, concurrency=1)`: Phase 1 Embeddings **parallel** (`Parallel.ForEachAsync`) VOR dem Löschen; **Sicherheitsabbruch** mit `InvalidOperationException`, wenn 0 oder < 50 % der Embeddings erzeugt wurden (Ollama-Ausfall darf bestehende KB nie löschen). Phase 2 löscht `Embeddings/Samples/Versions` und baut neu auf, finalisiert die aktuelle Version.
- `DeindexSample(id)`: löscht aus `Samples` und `Embeddings`.

`IsPermanentlySkipped(sample)` = `IsEvalContaminated || !IsIndexWorthy` — erlaubt Aufrufern, "bewusst übersprungen" (`KbIndexState.Skipped`) von echten Schreibfehlern (`KbIndexState.Error`) zu unterscheiden. UPSERT nutzt `INSERT OR REPLACE`. Versionen via `GetOrCreateCurrentVersionId`/`FinalizeCurrentVersion` (Guid, `SampleCount` nachgetragen).

### Indexer-Abstraktion (`IKnowledgeBaseIndexer` / `DelegatingKnowledgeBaseIndexer`)

`IKnowledgeBaseIndexer` (`Application/Ai/Training`) entkoppelt Review-Logik von der konkreten KB:
- `Task<KbIndexOutcome> IndexAsync(samples, ct)` mit `KbIndexOutcome(IndexedIds, SkippedIds)` — alles, was in keiner Liste steht, gilt als transienter Fehler.
- `void Deindex(sampleId)`.
`DelegatingKnowledgeBaseIndexer` ist ein reiner Adapter, der `IndexAsync`/`Deindex` an Func/Action aus dem ViewModel (`IncrementalKbUpdateAsync` / `TryDeindexSample`) durchreicht — keine eigene Logik.

### Retrieval (`RetrievalService`, `IRetrievalService`)

Aufgabe: Top-K ähnlichste KB-Samples für einen Query-Text (Few-Shot-Kontext für die Klassifikation und KB-Abgleich im Self-Training). `RetrieveAsync(queryText, topK=5, ct)`:
1. Query-Embedding via `EmbeddingService`; `null` → leere Liste.
2. Kandidaten aus **In-Memory-Cache** (`GetCandidatesCached`): lädt alle Embeddings+Sample-Felder in **einer JOIN-Query** (`LoadAllEmbeddingsWithSamples`, vermeidet N+1) und cacht sie. Cache-Invalidierung über eine billige Kennzahl `(COUNT(*), MAX(rowid))` der `Embeddings`-Tabelle — entkoppelt Retrieval-Latenz vom KB-Wachstum (~21.860 Embeddings nicht pro Query neu lesen).
3. `RankAndFilter(queryVec, candidates, topK, policy, out mismatchCount)` (rein, unit-testbar): Cosine-Similarity, Dimension-Mismatch wird gezählt und übersprungen.

**Qualitätsbewusstes Ranking** über `RetrievalQualityPolicy`:
- Default `GreenWeight=1.0`, `YellowWeight=1.0` (Green und Yellow GLEICH — innerhalb akzeptabler Qualität entscheidet Cosine), `RedFallbackWeight=0.15`, `AllowRedFallback=true`, `UnknownAs=Yellow`.
- Red wird ausgeschlossen und nur als **kontrollierter Fallback** (immer zuletzt, stark abgewertet) zugelassen, wenn sonst < topK Treffer. Begründung (an der echten KB gemessen): die QualityGate-Farbe misst Evidenzstärke, nicht Label-Korrektheit; Green-Bevorzugung senkte die Präzision@8.
- `ParseQuality` mappt "green"/"yellow"/"red" (case-insensitive), unbekannt → `UnknownAs`.

`CheckModelConsistency()` setzt `StoredEmbedModel` und `HasModelMismatch` (Vergleich `SELECT DISTINCT Model` gegen `embedder.ModelName`) — bei Mismatch ist ein KB-Rebuild nötig (Dimensionen passen sonst nicht).

### JSON-Trainingsspeicher (`TrainingSamplesStore`, `ITrainingSampleStore`)

Statischer Store auf `training_samples.json` (Pfad aus `KnowledgeBasePaths`). Ein `SemaphoreSlim(1,1)` serialisiert alle Load/Save (Race-Schutz). Operationen:
- `LoadAsync()` / `SaveAsync(list)`.
- `MergeAndSaveAsync(new)`: Dedup über **`Signature`** (kein Überschreiben, neue hinzufügen).
- `MergeOrUpdateAsync(samples)`: bei Signatur-Match In-place-Update der veränderlichen Felder (`TrainingSampleMerge.ApplyUpdatableFields` — Status, Notes, MatchLevel, KiCode, BBox/Maske), sonst Anhängen.

Robustheit (Daten-nie-verlieren): `SaveInternalAsync` schreibt in `.tmp`, **validiert** (gleiche Sample-Anzahl) und benennt atomar um; vorher rotierende Backups `.bak → .bak.2 → .bak.3`. `LoadInternalAsync` legt bei korruptem JSON ein `.bad_<timestamp>`-Backup an und lädt das jüngste lesbare Backup (`.bak` zuerst, dann `.bad_*`). `CleanupBadFiles` behält nur die letzten 3 `.bad_*`. Beim ersten Laden Migration alter 3-teiliger Signaturen → 4-teilig.

`ITrainingSampleStore` (Load/MergeOrUpdate/MergeAndSave) kapselt den statischen Store testbar; `TrainingSamplesStoreAdapter` ist die Produktiv-Implementierung.

### Trainingssample-Modell (`TrainingSample`)

Zentrale Datenklasse (`Application/Ai/Training/TrainingSampleModels.cs`). Wichtige Felder:
- Identität/Lokation: `SampleId`, `CaseId` (Haltung), `Code` (VSA), `Beschreibung`, `MeterStart`/`MeterEnd`, `IsStreckenschaden`, `TimeSeconds`, `FramePath`.
- Status/KB: `Status` (`TrainingSampleStatus { New, Approved, Rejected, Removed }`), `KbIndexState` (`{ None, Pending, Indexed, Error, Skipped }`), `MatchLevel`, `KiCode`, `KbCheck`, `SourceType`.
- Dedup: **`Signature`** = `BuildCanonicalSignature(caseId, code, meterCenter, meterEnd)` → `"{caseId}|{code}|{round1(meterCenter)}|{round1(meterEnd)}"` (CaseId verhindert Kollisionen gleicher Codes über Haltungen).
- Trainings-Eligibility: `InspectionDate`, `TrainingEligible`, `TrainingEligibilityReason`.
- Gold-Fund-Metadaten: `HumanConfirmed` (true=bestätigt, false=abgelehnt, null=nie beurteilt), `Corrected`, `ConfirmedByUser`, `ConfirmedAtUtc`, `QualityGateLevel`, `SnapshotError`.
- Box/Maske: `BboxXCenter/YCenter/Width/Height` (normiert 0–1, `HasBbox`), `SamMaskRle` + `SamMaskImageWidth/Height/AreaPixels/Confidence/Label` (`HasSamMask`).

Enums-Konstanten: `MatchLevelNames` (ExactMatch/PartialMatch/Mismatch/NoFindings/ReviewApproved/ReviewCorrected), `SourceTypeNames` (PdfPhoto/VideoTimestamp/VideoLinear/BatchImport/TeacherAnnotation).

**`TrainingSampleEligibility`**: `MinimumInspectionDate = 2022-01-01`. `Evaluate(date)` lehnt ab bei fehlendem Datum (`missing-inspection-date`) oder vor Cutoff (`legacy-before-2022`). `Evaluate(sample, ICodeCatalogProvider)` verlangt zusätzlich `IsSelectable && !IsObservedExtension` (`code-not-in-catalog`). `TryParseInspectionDate` parst viele Datumsformate inkl. eingebettetem `yyyyMMdd` aus Dateinamen.

### Self-Training-Orchestrator (`SelfTrainingOrchestrator`, `ISelfTrainingOrchestrator`)

Kernidee (PDF-Foto-basiert): Das Protokoll ist Ground-Truth; die KI analysiert das Foto **blind** (kennt das Protokoll nicht), dann deterministischer Vergleich. `RunAsync(TrainingCaseInput tc, IProgress<SelfTrainingStep> progress, ct)`. `TrainingCaseInput(CaseId, FolderPath, VideoPath, ProtocolPath, InspectionDate?)`. Ablauf:
1. **Eval-Early-Skip**: ist `tc.CaseId` eine reservierte Eval-Haltung (`EvalContaminationGuard.IsEvalHaltung`), wird die Haltung gar nicht erst gesammelt (kein Frame, kein Sample).
2. `PdfProtocolExtractor.ExtractAsync(tc.ProtocolPath, framesDir, ct)` → `IReadOnlyList<GroundTruthEntry>`. Bei 0 Einträgen sauberer Früh-Ausstieg.
3. Einträge mit eingebettetem Foto behalten; **Video-Fallback**, falls keine Fotos: `VideoProbeService` für Dauer, `ComputeMaxMeter` für lineare Meter→Zeit-Interpolation (`timeSec = 10 + (meterCenter/maxMeter)*(dauer-20)`, geclamped), Frame-Extraktion via `FrameStore.ExtractAndStoreAsync` (ffmpeg).
4. Pro Eintrag: Foto → `EnhancedVisionAnalysisService.AnalyzeAsync(base64, ct)` (blind), Fehler werden nach `…\SewerStudio\logs\selftraining_errors.log` geloggt.
5. **Deterministischer Vergleich** `ISelfTrainingComparisonService.Compare(entry, analysis)`.
6. Aufnahmetechnik: einmal pro Haltung via Qwen (`AssessFrameWithVisionAsync`), danach deterministisch (`AssessFrame`).
7. **Weg-1 KB-Abgleich** `EvaluateKbAgreementAsync`: read-only `IRetrievalService.RetrieveAsync(entry.Text, topK:5)`, dann `KbCodeAgreement.Classify(kiCode, kbCodes)`. Fehler/kein Retrieval → `KbNoSignal` (nie blockierend).
8. **Auto-Accept-Entscheidung** `SelfTrainingAutoAcceptPolicy.Decide(...)` → `TrainingSample` mit Status/KbIndexState. Samples werden via `TrainingSamplesStore.MergeAndSaveAsync` gemerged (NICHT direkt in die KB indexiert — das passiert erst nach menschlicher Bestätigung). Rückgabe: `SelfTrainingResult` mit Zählern (Exact/Partial/Mismatch/NoFindings, SamplesGenerated, Dauer).

`Pause()`/`Resume()` über `ManualResetEventSlim` (`_pauseGate`).

### Vergleichslogik (`SelfTrainingComparisonService`)

Rein deterministisch, kein LLM. `Compare(GroundTruthEntry, EnhancedFrameAnalysis)` → `ComparisonResult(Level, ConfidenceScore, …)`.
- Keine Findings: `NoFindings` nur wenn `IsTrainableNegative`, sonst `Mismatch`.
- Pro Finding gewichteter Score: Code 0.40, Meter 0.25, Severity 0.15, Uhrlage 0.20; bestes Finding gewinnt.
- **`CodesMatch`**: Punkt-Notation entfernt; exakt oder Protokoll-Code als Präfix der spezifischeren KI-Erkennung (max +2 Zeichen), nicht umgekehrt.
- **`MeterMatches`** (typabhängige Toleranz): Streckenschaden → Overlap-Prüfung mit `StreckenEdgeTolerance=0.50`; BCA*/BAH* (Anschluss) → `0.30`; sonst `0.50`.
- **`SeverityPlausible`**: Kategorie über 2. Buchstabe — `A` (baulich) ≥ 2, `B` (betrieblich) ≤ 4, `C` (Inventar) ≤ 2.
- **`EvaluateClock`**: nur ein positiv bestätigter Treffer (Protokoll hat + KI gleich, ±1h zirkulär) zählt; fehlende Protokoll-Uhrlage erzeugt KEINEN Volltreffer.
- `ExactMatch` verlangt **alle vier Achsen** sauber (Code, Meter, Severity, Uhrlage); nur `bestCodeMatch` → `PartialMatch`; sonst `Mismatch`. `ExactMatch` ist die Voraussetzung für Auto-Accept.

### Auto-Accept-Policy (`SelfTrainingAutoAcceptPolicy`) und KB-Abgleich (`KbCodeAgreement`)

`Decide(level, requireHumanReview, kbCheck, requireKbAgreement, confidenceScore, confidenceThreshold, framePositionReliable)` → `Decision(Status, KbIndexState, RouteToReview, Reason)`. Reihenfolge der Gates:
1. `KbCheckResult.KbDisagreement` → Review (`KbDisagreementReason`).
2. kein `ExactMatch` → Review.
3. `requireHumanReview` → Review (`HumanReviewRequiredReason`).
4. `confidenceScore < confidenceThreshold` → Review (`ConfidenceInsufficientReason`).
5. `requireKbAgreement && kbCheck != KbAgreement` → Review (`KbAgreementRequiredReason`).
6. `!framePositionReliable` → Review (`FramePositionUnverifiedReason`).
7. sonst → `Approved` + `KbIndexState.Pending`.

`KbCodeAgreement.Classify(kiCode, kbTopCodes)`: vergleicht den **3-stelligen Hauptcode** des KI-Codes gegen den Mehrheits-Hauptcode der KB-Treffer → `KbAgreement` / `KbDisagreement` / `KbNoSignal` (defensiv neutral bei Unklarheit).

`TrainingCenterSettings`-Defaults (sicher für unbeaufsichtigte Nachtläufe): `RequireHumanReview=true`, `RequireKbAgreementForAutoGold=true`, `AutoAcceptConfidenceThreshold=1.0`, `RequireReliableFramePositionForAutoGold=true`, `OsdMismatchThresholdMeters=20.0`. Mit den Defaults wird **nichts** automatisch Gold — alles geht in die Review-Queue.

### Review/Gold-Fund (`ReviewApprovalService`, `IReviewApprovalService`)

Wendet menschliche Entscheidungen auf Review-Samples an, Lookup per `SampleId`. Konstruktor `(ITrainingSampleStore store, IKnowledgeBaseIndexer indexer)`.
- **`ApproveSelfTrainingAsync(sampleId, box?, ct, confirmedByUser, mask?)`**: optionale Box/Maske VOR Statuswechsel anwenden; `Status=Approved`, `KbIndexState=Pending`, `MatchLevel=ReviewApproved`; Gold-Fund-Felder (`HumanConfirmed=true`, `Corrected=false`, `ConfirmedByUser`, `ConfirmedAtUtc`). `_indexer.IndexAsync` → KbIndexState = Indexed/Skipped/Error je nach Outcome; danach `MergeOrUpdateAsync`.
- **`RejectSelfTrainingAsync(sampleId, correctedCode?, ct, confirmedByUser, correctedDescription?)`**: `Status=Rejected`, `KbIndexState=None`, **`_indexer.Deindex`** (T3-Invariante: Ablehnen räumt den KB-Eintrag weg), `HumanConfirmed=false`. Bei `correctedCode` wird ein neues Sample `"{id}_corr"` (`MatchLevel=ReviewCorrected`, `HumanConfirmed=true`, `Corrected=true`, neue kanonische Signatur, Box/Maske übernommen) angelegt, gemergt und indexiert.

`ReviewApplyResult(Found, Indexed, Deindexed, CorrectedSampleId)`.

### Codiermodus-Feedback-Loop (`CodingSessionService`, `ICodingSessionService`)

Steuert einen Codier-Durchlauf von 0.00m bis Haltungsende (`StartSession`/Pause/Resume/Complete, `AddEvent`/`UpdateEvent`/`RemoveEvent`, `MoveNext/Previous/ToMeter`). Beim `CompleteSession`:
1. Protokoll aus `CodingEvent`s erzeugen, `ProtocolBoundaryService.EnsureBoundaries` (BCD@0, BCE@Ende).
2. **`PersistTrainingSamplesFromEvents`**: jedes Event → `CodingEventToSampleMapper.FromCodingEvent` → `TrainingSample`; **synchron** `TrainingSamplesStore.MergeAndSaveAsync` (Daten müssen vor Session-Ende auf Disk sein).
3. Fire-and-forget `IndexApprovedSamplesToKbAsync` (nur `Approved`).

`IndexConfirmedSampleAsync` indexiert ein einzelnes bestätigtes (`Approved`) Sample live. Beide laufen über `IndexAndPersistAsync`, das pro Sample einen eigenen `HttpClient` + `EmbeddingService` + `KnowledgeBaseContext` + `KnowledgeBaseManager` baut (mit Eval-Hashes/Haltungen aus den injizierten `Func<IReadOnlySet<string>>`-Providern), `IndexSampleAsync` ruft und das Ergebnis als `KbIndexState` zurück in `training_samples.json` schreibt (`MergeOrUpdateAsync`). **Fehlerklassifikation**: transiente Ollama-/Netzfehler (`HttpRequestException`, `TaskCanceledException`, `SocketException`, rekursiv) → `Pending` (Nachhol-Lauf); dauerhaft übersprungen (`IsPermanentlySkipped`) → `Skipped`; echter Schreibfehler → `Error`. Codieren darf nie an der KB scheitern (alles in try/catch, nur Debug-Log).

### Eval-Set: Kontaminationsschutz (`EvalContaminationGuard`)

Reine, seiteneffektarme Prüffunktionen (`Application/Ai/Training`). Zwei orthogonale Sperren:
- **Hash-Sperre** (pixelidentische Frames): `ComputeFileHash` = SHA-256-Hex (lowercase) des Datei-**Inhalts** (nicht des Namens). `LoadEvalImageHashes(evalSetRoot)` liest bevorzugt `_manifest.json` (`hashes["images/*"].sha256`), sonst direkt aus `images/`. `IsEvalContaminated(hashes, framePath)`.
- **Haltungs-Sperre** (gleiche reale Haltung, anderer Frame): `NormalizeHaltungKey(caseId)` extrahiert das kanonische Schacht-Paar (z.B. `"06.24379-06.24377" → "24379-24377"`, Bereichs-Präfix entfernt). `LoadEvalHaltungKeys(evalSetRoot)` liest bevorzugt `_candidates.json` (`haltung_key`), sonst aus Dateinamen-Präfix. `IsEvalHaltung(keys, caseId)`.

`ClassifyForExport(hashes, haltungKeys, framePath, caseId)` → `ExportContaminationResult { Clean, EvalImageHash, EvalHaltung }` (Reihenfolge Hash → Haltung). Leere Sätze ⇒ `Clean` (Schutz inaktiv statt Fehlalarm; degradiert sicher auf fremden Maschinen). Diese Guards werden überall vor KB-Indexierung und Trainings-/YOLO-Export verdrahtet; in der UI via `EvalContaminationGuard.LoadEval...(AppSettings.Load().EvalSetRoot)`.

### Eval-Set-Struktur, Freeze und clean/hidden-Split

Layout unter der Eval-Root (Default `C:\KI_BRAIN\eval_set`):
- `images/` und `labels/` (je 120 Frames/YOLO-Labels — das volle eingefrorene Set).
- `_manifest.json`: `total_candidates`, `approved`, `exported`, **`frozen:true`**, Warnung "DIESES EVAL-SET DARF NICHT VOM AUTO-TRAINING BERUEHRT WERDEN", `hash_algorithm:"sha256"`, `hashes_count`, und `hashes`: pro Datei `{ sha256, size_bytes }` (Schlüssel `images/<name>.png`, `_candidates.json` etc.). Das ist der **Hash-Freeze** zur Integritätsverifikation.
- `_candidates.json`: Kandidaten mit `haltung_key` und Erwartungscodes.
- `subsets/eval_visible_clean_eval_set/` (57 Frames, jeweils eigenes `images/`+`labels/`+Manifest) — der saubere Satz für **Modellvergleiche/Entscheidungen**.
- `subsets/eval_unclean_or_hidden_eval_set/` (63 Frames) — Kontrollblick (nicht "verbrennen"). Summe 57+63 = 120.

Dateinamenschema der Eval-Frames: `<haltung_key>_<zeit>s_<code>_t+0.png` (bzw. `..._kein_schaden.png` für Negative). Erwartungscode steckt im Namen und in `_candidates.json`.

### Benchmark-Tool (`tools/EvalSetBenchmark`)

Reines CLI (`net10.0`, nur Application + Infrastructure, keine WPF — entkoppelt vom UI-Target und von der laufenden App, die deren DLLs sperrt). `EvalSetBenchmarkDataset.Load(evalSetRoot)` lädt Cases (Bildpfad, erwarteter Code, Meter). Ablauf: pro Frame Qwen-Vision (`EnhancedVisionAnalysisService.AnalyzeAsync`), bestes Finding nach Severity → Vorhersage (leer → `"LEER"` bei `IsEmptyFrame`). Scoring über `EvalSetBenchmarkScorer.Evaluate/Summarize/SummarizeByExpectedCode/BuildConfusionMatrix`; schreibt CSV/JSON/by_code/confusion nach `docs/benchmarks`; in der Summary-JSON wird der Manifest-Freeze-Status mitgeschrieben (`TryReadManifestInfo`: `frozen`, `approved`, `hashes_count`, …). Wichtige Optionen: `--eval-set`, `--model`, `--max`, `--oracle-context` (gibt den erwarteten Code als Kontext — Testmodus), `--yolo-context`/`--yolo-presence-context` (Sidecar-YOLO-Kandidaten), `--yolo-detect-only`/`--yolo-detect-engine` (Sidecar-Health-Metrik mit Confidence-Sweep 0.25/0.5/0.7/0.85/0.9 — ausdrücklich **kein** Qualitätsbeweis), `--classifier-dataset`/`--coverage-only` (Abdeckungsanalyse), `--build-router-dataset`/`--dry-run`. Metriken: Exact/Main/Group-Accuracy, Negativ-Quote, Null-Antworten.

### Sauberer Trainingsdatensatz-Export (`StageAExporter`)

Baut aus `training_samples.json` einen Stage-A-YOLO-Datensatz (`StageAExportOptions(SourceSamplesPath, EvalSetRoot, OutputRoot, DryRun, ValidationRatio=0.2, DegreeOfParallelism=0, RequireBoundingBox=true)`). Pro Sample `AnalyzeSample` mit fester Ablehnungsreihenfolge → `StageASampleDecision`:
`NotApproved` (Status ≠ Approved) → **`EvalSet` via Haltung** (Vorrang vor Hash) → `TrainingIneligible` (Datum) → `InvalidCode` → `InvalidCatalogCode` → `WithoutBoundingBox` (wenn `RequireBoundingBox`) → `MissingOrCorrupt` (Datei fehlt/falsche Endung) → **`EvalSet` via Hash** (SHA-256) → `Accepted`. Danach Deduplizierung identischer Bilder per Hash, deterministischer **train/val-Split** (`ChooseSplit`: SHA-256 der SampleId, stabil), Klassenname = voller VSA-Code ohne Punkt-Suffix (`NormalizeClassName`). Schreibt `images/{train,val}`, `labels/{train,val}` (YOLO-Zeile `classId xc yc w h`, Box aus Sample oder Default 0.5/0.5/0.8/0.8), `data.yaml`, `clean_training_samples.json` und `stage_a_manifest.json` (inkl. aller Skip-Zähler, `eval_hashes_count`, `eval_hash_list_sha256`). Eval-Hashes werden — wie im Guard — bevorzugt aus `_manifest.json` gelesen. `StageAExportResult` liefert alle Zähler und Klassenstatistik. Dieser Datensatz ist die Eingabe für ein externes YOLO-Retraining (kein C#-Retrain-Orchestrator im HEAD).

### Befund-Matcher für ehrliche Evaluation (`BefundMatcher`)

`Application/Ai/Evaluation/BefundMatcher.cs`: gleicht KI-Befunde gegen Protokoll-Ground-Truth ab, ohne Frame-Vergleich. `Match(groundTruth, detections, BefundMatchOptions?)`:
- Gestufte Meter-Toleranz: grün ≤ `TolGruen=0.20`, gelb ≤ `TolGelb=0.50`; `Gap` = 0 bei Bereichs-Überlappung (Punkt vs. Strecke).
- Anker `BCD`/`BCE` (`ExcludedFamilies`) beidseitig herausgefiltert; KI-Detections **ohne** Code bleiben und zählen als Fehlalarm (Präzision wird nicht geschönt).
- **Eins-zu-eins-Zuordnung** über alle Paare via Min-Cost-Max-Cardinality-Matching (`MinCostMaxMatch`, SPFA/Successive-Shortest-Paths): Phase 1 gleiche Hauptcode-Familie + Meter ≤ gelb → Treffer; Phase 2 Reste mit Meter ≤ gelb aber anderer Familie → Falscher Code; Phase 3 Verpasst (FN) / Fehlalarm (FP).
- Vier Töpfe (`Treffer`/`FalscherCode`/`Verpasst`/`Fehlalarm`); `Precision = TP/(TP+FP+WC)`, `Recall = TP/(TP+FN+WC)`. `MainCode` = erste 3 Zeichen (Whitespace entfernt). `Add()` für gepoolte Summen über mehrere Haltungen.

### Trainings-Datenfluss (Ende-zu-Ende)

1. **Quelle** — Self-Training: PDF-Protokoll (`PdfProtocolExtractor.ExtractAsync` → `GroundTruthEntry[]`) + Foto/Video-Frame, blinde Qwen-Analyse, deterministischer Vergleich (`SelfTrainingComparisonService`) und KB-Abgleich (`KbCodeAgreement`). Oder — Codiermodus: bestätigte `CodingEvent`s.
2. **Entscheidung** (`SelfTrainingAutoAcceptPolicy`): mit Default-Settings landet alles als `New` in der Review-Queue; `ExactMatch` + alle Gates offen → `Approved/Pending`.
3. **Persistenz**: `TrainingSamplesStore.MergeAndSaveAsync` (Dedup über `Signature`) nach `training_samples.json`.
4. **Menschliche Bestätigung** (`ReviewApprovalService`): Approve → `Approved` + Gold-Fund-Felder + KB-Index; Reject → Deindex (+ optionales korrigiertes Sample).
5. **KB-Indexierung** (`KnowledgeBaseManager.IndexSampleAsync`): nur indexwürdige UND nicht eval-kontaminierte Samples; Embedding via `nomic-embed-text`; atomar Sample+Embedding in SQLite. `KbIndexState` zurück in die JSON.
6. **Retrieval** (`RetrievalService`): die KB liefert Few-Shot-Kontext für künftige Klassifikationen und den Weg-1-KB-Abgleich — der Loop schließt sich.
7. **Evaluation** (`EvalSetBenchmark` + `BefundMatcher`): gegen das eingefrorene, hash-verifizierte Eval-Set (clean für Entscheidungen, hidden als Kontrolle).
8. **Externes Modelltraining**: `StageAExporter` erzeugt den sauberen YOLO-Datensatz (Eval-Frames per Hash UND Haltung ausgeschlossen).

**Drei harte Invarianten, die nie verletzt werden dürfen:** (a) kein Eval-Frame (Hash oder Haltung) darf je in KB/Retrieval/Export gelangen; (b) ein Ollama-Ausfall darf eine bestehende KB nie löschen (50 %-Gate in `RebuildAsync`); (c) Trainingsdaten gehen nie verloren (atomare temp-Writes, rotierende Backups, Korruptions-Recovery in `TrainingSamplesStore`).

Relevante Pfade: `src/AuswertungPro.Next.Infrastructure/Ai/KnowledgeBase/{KnowledgeBaseManager,KnowledgeBaseContext,EmbeddingService,RetrievalService,KnowledgeBasePaths}.cs`; `src/AuswertungPro.Next.Infrastructure/Ai/Training/{SelfTrainingOrchestrator,SelfTrainingComparisonService,ReviewApprovalService,DelegatingKnowledgeBaseIndexer,TrainingSamplesStore}.cs`; `src/AuswertungPro.Next.Infrastructure/Ai/CodingSessionService.cs`; `src/AuswertungPro.Next.Infrastructure/Ai/Training/Services/PdfProtocolExtractor.cs`; `src/AuswertungPro.Next.Application/Ai/Training/{SelfTrainingAutoAcceptPolicy,KbCodeAgreement,EvalContaminationGuard,StageAExporter,TrainingSampleModels,TrainingCenterSettings,GroundTruthEntry,IKnowledgeBaseIndexer,IReviewApprovalService,ITrainingSampleStore}.cs`; `src/AuswertungPro.Next.Application/Ai/Evaluation/BefundMatcher.cs`; `src/AuswertungPro.Next.Application/Ai/KnowledgeBase/IRetrievalService.cs`; `tools/EvalSetBenchmark/Program.cs`; Eval-Set unter `C:\KI_BRAIN\eval_set` (`_manifest.json`, `_candidates.json`, `images/`, `labels/`, `subsets/`).

## A8 · WPF-UI-Architektur

Die UI ist das WPF-Frontend (`AuswertungPro.Next.UI`, .NET 10, MVVM mit CommunityToolkit.Mvvm). Sie haelt die gesamte Praesentation, oeffnet Fenster, rendert KI-Overlays und delegiert jede Geschaeftslogik an `Application`/`Infrastructure`. **Thin-AI gilt auch hier: die UI rechnet nichts fachlich aus, sie bindet Services und zeichnet.**

### Bootstrap und DI (`App`, `ServiceProvider`)

Datei: `src/AuswertungPro.Next.UI/App.xaml` + `App.xaml.cs`, `src/AuswertungPro.Next.UI/ServiceProvider.cs`.

- `App` ist die WPF-`Application`. `App.xaml` mergt zwei Theme-Dictionaries (`Theme/ThemeLight.xaml`, `Theme/Controls.xaml`) und definiert die **MVVM-DataTemplates** (ViewModel-Typ -> View): z. B. `vm:DataPageViewModel -> views:DataPage`, analog fuer `Project/Overview/Schaechte/Import/Export/MediaConflicts/Vsa/Diagnostics/Settings/Builder/SanierungsMatrix`. So bestimmt der `CurrentPage`-ViewModel-Typ im Shell automatisch die angezeigte Seite.
- `App.OnStartup` (Reihenfolge wichtig zum Nachbauen):
  1. `ShutdownMode = OnExplicitShutdown`, Typografie-Defaults (`Segoe UI Variable Display, Segoe UI, Aptos, Arial`, FontSize 14 via `OverrideMetadata`), Default-Fenster-Icon-Handler registrieren.
  2. `StartupSplashWindow` zeigen.
  3. `AppSettings.Load()`; optional KI-Runtime-Defaults; `ThemeManager.ApplyTheme(Resources, settings.UiTheme)`.
  4. Logging: `FileLoggerProvider` in `%AppData%/.../logs/app-yyyyMMdd.log` (Retention 60 Tage), `ILoggerFactory` bauen.
  5. **`_services = new ServiceProvider(settings, diagnostics, logger, loggerFactory)`** — danach erst `App.Services` verfuegbar.
  6. Globale Exception-Handler (`DispatcherUnhandledException`, `AppDomain.UnhandledException`, `TaskScheduler.UnobservedTaskException`) -> `HandleException` (Fehlercode via `ErrorCodeGenerator`, Dialog via `IDialogService`).
  7. `base.OnStartup(e)`, dann `MainWindow` (Opacity 0) zeigen, optional KI-Autostart, Splash ausblenden (max. 15 s), Fade-in 500 ms, `ShutdownMode = OnMainWindowClose`.
- `App.Services` ist der statische Zugriffspunkt (`public static IServiceProvider Services`). Fenster casten ihn typisch per `App.Services is ServiceProvider sp`.

`ServiceProvider` ist ein **handgeschriebener, minimaler DI-Container** (`IServiceProvider`, kein MS-Hosting-Paket; bewusste Entscheidung). Alle Services sind Konstruktor-initialisierte read-only Properties. Wichtigste Properties:

- Infrastruktur/Logging: `Settings` (`AppSettings`), `Diagnostics`, `Logger`, `LoggerFactory`, `ErrorCodes`, `Dialogs` (`new DialogService()`), `PlaywrightInstaller`.
- Import/Export/Projekt: `Projects` (`JsonProjectRepository`), `PdfImport`, `XtfImport`, `WinCanImport`, `IbakImport`, `KinsImport` (wraps WinCan+Ibak), `ExcelExport`, `Protocols`, `PhotoImport`, `ProtocolPdfExporter`.
- VSA/Katalog: `Vsa` (`VsaEvaluationService`, V2-Engine + Shadow-Mode per Settings-Flag), `CodeCatalog` (`CompositeCodeCatalogProvider` aus Manifest + XML-Katalogen), `CodeSelectionCatalog` (`VsaCodeTreeSelectionCatalog`), `VsaCatalogResolvedPath`. Katalogquelle wird mehrstufig aufgeloest (Settings -> Env-Vars `VSA_CATALOG_*` / `VSA_KEK_2020_CATALOG_MANIFEST` -> WinCan-Verzeichnis -> Defaults); `VsaCodeResolver.ConfigureCatalog(CodeCatalog)` statisch verdrahtet.
- KI: `ProtocolAi` (`OllamaProtocolAiService` wenn aktiviert, sonst `NoopProtocolAiService`), `Retrieval` (`RetrievalService` ueber `KnowledgeBaseContext`+`EmbeddingService`, fail-soft mit Modell-Mismatch-Warnung), `KnowledgeBaseDiagnostics`, `MeasureRecommendation`.
- **`PipelineConfig PipelineCfg`**: laedt bei jedem Zugriff `AiSettingsFactory.Load(...).ToPipelineConfig()` (1x laden, mehrfach projizieren). Quelle ist `AppSettingsAiSettingsProvider.ToSource(Settings)`.

**Standard-Pipeline-Erzeugungs-Pattern (zentral nachbauen):** Die UI baut die Videoanalyse-Pipeline ausschliesslich ueber die Factory-Methode auf `ServiceProvider`:

```csharp
public IVideoAnalysisPipelineService CreateVideoAnalysisPipeline(
    AiRuntimeSettings cfg, IAiSuggestionPlausibilityService plausibility, HttpClient http)
    => new VideoAnalysisPipelineService(cfg, PipelineCfg, plausibility, http, CodeCatalog, LoggerFactory);
```

Aufruf-Schema (siehe `DataPageViewModel.cs` ~745): `cfg` aus `AppSettingsAiSettingsProvider().Load().ToRuntimeSettings()`, `plausibility = new RuleBasedAiSuggestionPlausibilityService(HashSet aller erlaubten Codes aus CodeCatalog)`, eigener `HttpClient` mit Timeout -> `var pipeline = _sp.CreateVideoAnalysisPipeline(cfg, plausibility, http);`. Analog `CreateSanierungOptimization(cfg, http)`.

### Shell / MainWindow

Dateien: `MainWindow.xaml(.cs)`, `ViewModels/ShellViewModel.cs`.

- `MainWindow` setzt `DataContext = new ShellViewModel()`, trackt Fenstergeometrie (`WindowStateManager.Track`), behandelt Schliessen (Dirty-Abfrage via `DialogHost.Current.ConfirmCancel`, dann `Application.Current.Shutdown()`).
- Layout: `DockPanel` mit Menue (Top), Statuszeile (Bottom), zweispaltigem Grid: linke Sidebar (220px, im Fokusmodus 0) mit Logo, `NeuralSphereControl` (KI-Indikator), `SystemMonitorPanel` (VRAM/CPU) und Navigations-`ListBox` (`NavItems`/`SelectedNavItem`). Rechts `AnimatedContentControl Content="{Binding CurrentPage}"` in einer `Card`-Border; bei nicht verfuegbarem Projekt liegt ein Overlay darueber.
- Fokusmodus (F11) blendet Menue/Sidebar/Statuszeile per `DataTrigger` auf `IsFocusMode` aus.
- Menue oeffnet Fenster direkt im Code-Behind: `CodeCatalogEditorWindow`, `TrainingCenterWindow` (modeless `.Show()`), `KarteWindow`, KI-Start (`AiStartupService.StartAsync`).
- Alle Brushes per `DynamicResource` (`AccentBrush`, `TextBrush`, `BorderBrush`, `NavPanelBrush`, `CardBrush`, `HoverBrush`, `AccentSubtleBrush` …) — Theme-Wechsel zur Laufzeit funktioniert dadurch.

### MVVM-Aufbau / Verzeichnisstruktur

- `ViewModels/Pages/*` — eine ViewModel je Hauptseite (`DataPageViewModel`, `ProjectPageViewModel`, `SchaechtePageViewModel`, `KarteViewModel`, …). `ViewModels/Windows/*` und `ViewModels/Protocol/*` fuer Dialoge/Editoren. ViewModels erben von `ObservableObject`, Commands sind `RelayCommand`/`AsyncRelayCommand`.
- `Views/Pages/*` — Seiten-UserControls (DataTemplate-gebunden). `Views/Windows/*` — eigenstaendige Fenster. `Views/Controls/*` und `Controls/*` — wiederverwendbare Controls (`ClockPickerControl`, `ClockRangePickerControl`, `NeuralSphereControl`, `PipeGraphTimeline`, `AnimatedContentControl`, `UnfrozenDataGrid`, `SystemMonitorPanel`) und Wert-Converter (`PercentToColorConverter`, `IntToVisibilityConverter`, `BoolToDoubleConverter`, …).
- `Dialogs/*` und `Views/*Dialog.xaml` — modale Editoren (Code-Katalog, Preis-Katalog, Positionsvorlagen, Optionen). Zentraler `DialogHost.Current`/`IDialogService` (`Info/Warn/Error/ConfirmCancel/ConfirmWarn`) fuer alle MessageBox-artigen Interaktionen, damit ViewModels testbar bleiben.
- Hilfsschichten in `Player/*`, `DataPage/*`, `LiveControl/*`, `Ai/*`, `Mapping/*` kapseln View-nahe Logik (z. B. `DataPageVideoOverlayBuilder`, `PlayerTimelineLayoutCalculator`).

### PlayerWindow und Codiermodus

`PlayerWindow` ist als **partielle Klasse** ueber viele Dateien gesplittet: `PlayerWindow.xaml.cs` (Kern/Konstruktor), `.Playback.cs` (libVLC-Wiedergabe), `.LiveDetection.cs`, `.OverlayRendering.cs`, `.Coding.cs` (Codiermodus, ~4000 Zeilen), `.CodingSidePanelAccessors.cs` plus XAML (`PlayerWindow.xaml`, `PlayerWindow.Resources.xaml`, `PlayerCodingSidePanel.xaml`).

Konstruktor-Signatur (so wird es geoeffnet):
```csharp
public PlayerWindow(string videoPath, PlayerWindowOptions? options = null,
    string? initialOverlayText = null, PlayerDamageOverlayData? damageOverlay = null,
    ServiceProvider? serviceProvider = null, string? haltungId = null,
    Action<ProtocolEntry>? onEntryCreated = null, HaltungRecord? haltungRecord = null)
```
Geoeffnet wird es aus `DataPageViewModel` (~610): `PlayerWindowOptions` aus Video-Settings, `damageOverlay = DataPageVideoOverlayBuilder.Build(record)`, `serviceProvider: _sp`. Ohne `HaltungRecord` ist der Codiermodus gesperrt.

**Codiermodus-Lebenszyklus** (`PlayerWindow.Coding.cs`):
- `EnterCodingMode()`: Video pausieren, Live-Detection stoppen; Session-Services bauen: `_codingSessionService = CreateCodingSessionService()` (= `new CodingSessionService(...)` mit Ollama-Config-Loader und Eval-Kontaminations-Guards aus `EvalContaminationGuard`), `_codingOverlayService = new OverlayToolService()`, `_codingVm = new CodingSessionViewModel(sessionService, overlayService, new CodingFeedbackRecorder())`. DN aus `_haltungRecord.Fields["DN_mm"]` -> `SetCalibration(new PipeCalibration{NominalDiameterMm=dn})`. Session starten/pausieren, bestehende Protokoll-Eintraege in die **Import-Referenzliste** (`_codingImportEvents`) verschieben, KI-Befundliste startet leer. Standardwerkzeug = `OverlayToolType.Rectangle` (jede gezogene Box -> SAM-Segmentierung + Codefenster via `HandleMarkDrawingComplete`). UI einblenden: `CodingOverlayPopup`, `CodingSidePanel`, `CodingToolbar`, `PipeTimeline` (`PipeGraphTimeline` mit `MeterAccessor/CodeAccessor/ConfidenceAccessor`), OSD-Meter-Timer (`DispatcherTimer`, liest Meterstand), `InitCodingAi()` fire-and-forget.
- `InitCodingAi()`: `OllamaClient` + `LiveDetectionService` + `EnhancedVisionAnalysisService` (Qwen) + `QualityGateService` (immer Default-Gewichte, ADR-008: keine gelernten Gewichte laden); Multi-Model: `VisionPipelineClient(SidecarUrl, SidecarToken)` -> `SingleFrameMultiModelService(client, PipelineCfg)`, `MarkBoxSegmentationService(client.SegmentSamAsync)`; `PipelineHealthMonitor` pollt laufend Sidecar-Status (YOLO/DINO/SAM/Token) und schaltet Multi-Model vs. Qwen-only automatisch (`ApplyPipelineHealth` setzt Ampel: Full=gruen, Degraded=gelb, sonst grau).
- `ApplyCodingChanges()`: Coding-Events -> `ProtocolDocument` (Klon via `ProtocolRevisionCloner`), Update/Delete-Abgleich nach `EntryId`, Schutz vor versehentlichem Leeren (Rueckfrage), in primaere Schaeden uebertragen, `PersistCodingEventsAsTrainingSamples()`, sofort speichern.
- `ExitCodingMode()`: offene Streckenschaeden schliessen (`_streckenTracker` = `Application.Ai.StreckenschadenTracker`), ggf. BCE/BDC-Endcode setzen, alle Timer/Monitor stoppen, UI ausblenden, Event-Handler abmelden (Leak-Schutz).

**Andere wichtige Fenster:**
- `TrainingCenterWindow` (modeless, ueber Menue) — Review-Queue, Batch-Import+KB-Indizierung, KB-Pruefung, YOLO-Box-Labeling; rendert SAM-Masken via `SamMaskRenderer.RenderMasks(...)`.
- `LiveFrameWindow` — Einzelframe-Anzeige der Live-Pipeline mit **Uhrlage-Ring** (s. u.).
- `PhotoMeasurementWindow` — manuelle Messung auf Fotos; `BurnOverlayToPhoto()` rendert das `OverlayCanvas` via `RenderTargetBitmap`+`VisualBrush` in Originalaufloesung als `*_overlay.png` (Letterbox-Offset herausgerechnet).
- `VideoAnalysisPipelineWindow` — Batch-Videoanalyse-UI, oeffnet `LiveFrameWindow` fuer Live-Frames.

### Overlay-Rendering der KI-Befunde

Zwei getrennte Rendering-Pfade auf WPF-`Canvas`:

**A) Manuelle Mess-/Schema-Overlays** (`PlayerWindow.OverlayRendering.cs`, Ziel `CodingOverlayCanvas`): `RenderOverlayGeometry(OverlayGeometry, isPreview, labelAnchor)` zeichnet je `OverlayToolType` (`Line/Stretch/Rectangle/Point/Arc/PipeBend/LateralCircle/Ruler/Level/Ellipse/Freehand`). Normierte Punkte (0..1) werden ueber `CodingNormToPixel` (Letterbox-bewusst) in Canvas-Pixel umgesetzt. Vorschau = `Lime`/gestrichelt, finalisiert = Cyan (`#00E5FF`). Spezialwerkzeuge: Winkelmesser (`RenderPipeBendOverlay` mit Winkelbogen + Grad-Label), DN-Kreis (`RenderLateralCircleOverlay`, Magenta, DN-Label + % vom Haupt-DN), Lineal mit adaptiven Tick-Marks, Fuell-/Einragungs-Schema (`RenderLevelOverlay`/`RenderActiveCodingSchema`, Kreisprofil aus Kalibrierung). `RenderReferenceDn()` zeichnet den gestrichelten Referenz-DN-Kreis (`ReferenceDnGeometry.BuildCircleRect`). Elemente werden ueber `Tag` (`overlay_preview`/`overlay_manual`/`overlay_measure`/`ref_dn`) verwaltet/entfernt.

**B) SAM-Masken-Overlay** (`Ai/Pipeline/SamMaskRenderer.cs`, statisch, testbar):
- Dekodiert SAM-RLE (`DecodeRle`, Format `start_value,run1,run2,...`, C-order; defensiv gegen >50 Mio Pixel), extrahiert Kontur (`ExtractContourGeometry`, Scanline + Downsample auf `targetWidth=480`) und Fuellung (`ExtractFillGeometry`).
- **Render-Policy `DecideVisualMode(candidate, options)`** mit `MaskVisualMode { Hidden, OutlineOnly, SubtleFill }` und `RenderOptions.WinCanStyle` (Defaults: `LargeFindingOutlineAreaRatio=0.30`, `MinimumVisibleDetectionConfidence=0.25`, `MinimumVisibleSamConfidence=0.25`, `MinimumFillDetectionConfidence=0.60`, `FillAlpha=24`, `StrokeAlpha=230`, `HiddenLabelTokens` = `water wall/structure water wall/pipe wall/black border/osd`). Reihenfolge: (1) Hintergrund-Label -> `Hidden` (Grund `background_label`); (2) Confidence unter beiden Schwellen -> `Hidden` (`confidence_too_low`); (3) **sonst immer `SubtleFill`**. Achtung Fallstrick/Regression-Lehre: frueher strippte die Policy grosse Befunde auf `OutlineOnly`, was nur eine duenne Scanline-Kontur uebrig liess und "verzerrt" wirkte; das aktuelle, gewollte Backup-Verhalten ist **immer Fuellung + Kontur** fuer sichtbare Masken (Hidden bleibt fuer Hintergrund). Wirkt erst nach App-Neustart.
- `RenderCandidates(canvas, candidates, imageW, imageH, canvasW, canvasH, logger, options, offsetX, offsetY)` ruft pro Maske `DecideVisualMode`, ueberspringt `Hidden`, zeichnet `RenderSingleMask` (gruene Fuellung `argb(FillAlpha,0,255,0)` nur bei `SubtleFill`, gruene Kontur `StrokeThickness=2`, Label-Badge mit VSA-Klartext via `VsaCodeResolver.LookupLabel` + Messtext `H/W mm | Uhr | %`). Elemente per `MaskTag="sam_mask"`/`LabelTag="mm_label"`; `ClearMasks(canvas)` raeumt auf. Eine defekte Maske bricht den Lauf nicht ab (try/catch + Logger). Rueckgabe `RenderSummary` (Rendered/Hidden/OutlineOnly/SubtleFill + Hidden-Gruende). `offsetX/offsetY` tragen den Letterbox-Versatz (`GetCodingContentRect`).
- Verwendung im Codiermodus (`ShowMultiModelResults`): nur **codierbare, sichtbare** Masken werden gerendert. `BuildVisibleMaskFindings` filtert ueber dieselbe `DecideVisualMode`-Entscheidung (kein Befund aus Hintergrund); "Voraus"-Befunde (noch im DN-Kreis) werden nur intern gemerkt, nicht gezeichnet. Auch `TrainingCenterWindow` und der Mark-Box-Pfad (`ShowMarkSamMask`) nutzen denselben Renderer.

**Uhrlage-Ring** (`LiveFrameWindow.RenderOverlay`): zeichnet zwei gestrichelte Fuehrungs-Ellipsen (outer/inner) plus 12 Stunden-Ticks (`angleDeg = -90 + (hour%12)*30`, 12:00 oben). Pro Finding wird ein Ringsektor (`BuildRingSectorGeometry`, Sweep aus `ExtentPercent*3.6`, geklammert 14..160 Grad) an der geparsten Uhrlage (`ParseClockHour`) gezeichnet, Farbe aus `MapSeverityColor` (1..5: gruen -> rot), plus Marker-Punkt und Label-Badge (`Uhr / Extent% [H/Einr/QV] - VSA-Code`).

`AiOverlayConverter` liegt in **`Infrastructure/Ai/`** (nicht in der UI) und konvertiert Pipeline-Findings in UI-Overlay-Modelle; die UI konsumiert ihn nur.

### Theming / Brushes

Dateien: `Theme/ThemeLight.xaml` (hell, default), `Theme/Theme.xaml` (dunkel), `Theme/Controls.xaml` (Control-Styles), `Services/ThemeManager.cs`.

- `ThemeManager` kennt zwei Themes (`Light`/`Dark`), mappt auf `Theme/ThemeLight.xaml` bzw. `Theme/Theme.xaml`, und tauscht zur Laufzeit das Theme-Dictionary in `Application.Resources.MergedDictionaries` aus (`ApplyTheme`, `IsThemeDictionary` erkennt das alte). Aufgerufen beim Start aus `App.OnStartup` mit `settings.UiTheme`.
- Theme-Datei definiert `Color`-Schluessel (`ColorAccent=#2563EB`, `ColorBgLight`, `ColorCard`, `ColorBorder`, `ColorTextPrimary/Secondary/Muted`, semantisch `ColorSuccess/Danger/Warning/Info`) und davon abgeleitete `SolidColorBrush`-/`LinearGradientBrush`-Ressourcen (`AccentBrush`, `AccentSubtleBrush`, `TextBrush`, `TextSecondaryBrush`, `MutedBrush`, `BorderBrush`, `CardBrush`, `HoverBrush`, `BgBrush`, `NavPanelBrush`, `OverlayBrush`, …) sowie globale `Window`/`TextBlock`-Styles (Schriftfamilie, FontSize 14, ClearType). **Regel zum Nachbauen:** alle UI-Farben ueber `DynamicResource` referenzieren, damit Laufzeit-Theme-Wechsel greift; harte Overlay-/Schadensfarben (gruen fuer SAM, Cyan/Magenta/Gold fuer Werkzeuge, Severity-Palette) sind bewusst fest im Render-Code, nicht aus dem Theme.

Relevante Dateipfade (absolut):
- `c:\Sewer-Studio_KI_4.4\src\AuswertungPro.Next.UI\ServiceProvider.cs`
- `c:\Sewer-Studio_KI_4.4\src\AuswertungPro.Next.UI\App.xaml` / `App.xaml.cs`
- `c:\Sewer-Studio_KI_4.4\src\AuswertungPro.Next.UI\MainWindow.xaml` / `MainWindow.xaml.cs`
- `c:\Sewer-Studio_KI_4.4\src\AuswertungPro.Next.UI\Views\Windows\PlayerWindow.Coding.cs` / `PlayerWindow.OverlayRendering.cs` / `PlayerWindow.LiveDetection.cs` / `PlayerWindow.xaml.cs`
- `c:\Sewer-Studio_KI_4.4\src\AuswertungPro.Next.UI\Ai\Pipeline\SamMaskRenderer.cs`
- `c:\Sewer-Studio_KI_4.4\src\AuswertungPro.Next.UI\Views\Windows\LiveFrameWindow.xaml.cs` / `PhotoMeasurementWindow.xaml.cs` / `TrainingCenterWindow.xaml(.cs)`
- `c:\Sewer-Studio_KI_4.4\src\AuswertungPro.Next.UI\Theme\ThemeLight.xaml` / `Theme.xaml` / `Controls.xaml`
- `c:\Sewer-Studio_KI_4.4\src\AuswertungPro.Next.UI\Services\ThemeManager.cs`

## A9 · Import/Export und Datenformate

Dieses Teilsystem liest Fremd-Exporte verschiedener Kanal-TV-Software (WinCan, IBAK, KINS, INTERLIS/SIA405-XTF, PDF-Protokolle) ein und füllt damit das zentrale Datenmodell (`Project` mit `HaltungRecord`- und `SchachtRecord`-Listen). Es exportiert wieder nach Excel-Vorlage, CSV und PDF. Alle Geschäftslogik ist C#; LLMs sind hier nicht beteiligt (Thin-AI-Prinzip).

### Architektur-Überblick und einheitliche Import-Vertraege

Alle Import-Interfaces liegen in `src/AuswertungPro.Next.Application/Import/IImportServices.cs` und liefern einheitlich `Result<ImportStats>`:

```csharp
public sealed record ImportStats(int Found, int Created, int Updated, int Errors, int Uncertain, IReadOnlyList<string> Messages);

public interface IPdfImportService     { Result<ImportStats> ImportPdf(string pdfPath, Project project, string? pdfToTextPath, bool fillMissingOnly = false, ImportRunContext? ctx = null); }
public interface IXtfImportService      { Result<ImportStats> ImportXtfFiles(IEnumerable<string> xtfPaths, Project project, ImportRunContext? ctx = null); }
public interface IWinCanDbImportService { Result<ImportStats> ImportWinCanExport(string exportRoot, Project project, ImportRunContext? ctx = null); }
public interface IIbakImportService     { Result<ImportStats> ImportIbakExport(string exportRoot, Project project, ImportRunContext? ctx = null); }
public interface IKinsImportService     { Result<ImportStats> ImportKinsExport(string exportRoot, Project project, ImportRunContext? ctx = null); }
```

Wichtig: PDF und XTF weichen vom Schema `ImportXxx(exportRoot, project, ctx)` ab (PDF erwartet eine einzelne Datei plus optionalen `pdfToTextPath` und `fillMissingOnly`; XTF erwartet eine Liste von XTF-Pfaden). WinCan/IBAK/KINS folgen dem `exportRoot`-Schema.

`ImportRunContext` (`src/AuswertungPro.Next.Application/Import/ImportRunContext.cs`) bündelt die Cross-Cutting-Concerns eines Laufs: `CancellationToken`, `IProgress<ImportProgress>? Progress`, strukturiertes `ImportRunLog Log` (`AddEntry(source, step, ImportLogStatus, recordKey, sourceFile, detail)`), `bool DryRun` und ein optionales `object? CollectionLock`. `ctx` darf `null` sein; dann nutze `ImportRunContext.Default`. Jede Mutation an `project.Data` / `project.SchaechteData` muss über `ctx.WithCollectionLock(...)` laufen (Thread-Sicherheit bei UI-Aufrufen), bei `ctx == null` direkt.

Registriert und konstruiert werden die Services manuell im `ServiceProvider` (`src/AuswertungPro.Next.UI/ServiceProvider.cs`), kein DI-Container:

```csharp
PdfImport   = new PdfImportServiceAdapter();
XtfImport   = new XtfImportServiceAdapter();
WinCanImport= new WinCanDbImportService();
IbakImport  = new IbakExportImportService();
KinsImport  = new KinsImportService(WinCanImport, IbakImport);  // KINS delegiert an WinCan + IBAK
ExcelExport = new ExcelTemplateExportService();
```

`Result<ImportStats>.Fail("CODE", "Meldung")` bei harten Fehlern (Ordner fehlt, keine lesbare Quelle); ansonsten `Success(stats)`. Fehler einzelner Haltungen werden isoliert (try/catch pro Haltung, `errors++`, weiterlaufen) — eine kaputte Haltung darf den Lauf nicht abbrechen. `OperationCanceledException` wird immer durchgereicht.

### Wie Stammdaten und Protokoll ins HaltungRecord kommen

Das Zielmodell ist `HaltungRecord` (string-basierte Felder über `GetFieldValue(name)` / `SetFieldValue(name, value, FieldSource, userEdited)`). Wichtige logische Feldnamen, die alle Importer schreiben: `Haltungsname`, `Strasse`, `Rohrmaterial`, `DN_mm`, `Haltungslaenge_m`, `Nutzungsart`, `Eigentuemer`, `Bemerkungen`, `Datum_Jahr`, `Inspektionsrichtung`, `Link` (Video), `PDF_Path`, `PDF_All` (mehrere PDFs mit `;` getrennt), `Primaere_Schaeden`, `Anschluesse_verpressen`, `Schacht_oben`, `Schacht_unten`. Schächte landen in `SchachtRecord` mit Feldern wie `Schachtnummer`, `Funktion`, `Status`, `Zustandsklasse`.

`FieldSource` (`src/AuswertungPro.Next.Domain/Models/FieldSource.cs`) markiert die Datenherkunft pro Feld für Merge-/Konflikt-Logik: `Unknown=0, Legacy=1, Xtf=3, Xtf405=5, Ili=6, Pdf=7, Manual=10`. WinCan/IBAK/KINS schreiben mit `FieldSource.Legacy`, XTF mit `Xtf`/`Xtf405`, PDF mit `Pdf`. `userEdited: false` markiert importierte (nicht manuell editierte) Werte.

Das Protokoll (Befundliste) wird als `ProtocolDocument` mit `Original`/`Current`-Revision und `History` geführt. `ProtocolEntry` (`src/AuswertungPro.Next.Domain/Protocol/ProtocolModels.cs`): `Code`, `Beschreibung`, `MeterStart`, `MeterEnd`, `IsStreckenschaden`, `Mpeg`, `Zeit`, `FotoPaths`, `Source` (`ProtocolEntrySource { Imported, Manual, Ai }`). Beim Import gilt das Muster `ApplyProtocol`:
1. Kein Protokoll vorhanden oder leer → `protocolService.EnsureProtocol(haltungsname, entries, null)`.
2. Re-Import mit identischem Inhalt (`ProtocolContentFingerprint.HasSameContent`) → keine neue Revision (Audit I1).
3. Sonst: aktuelle Revision in `History` schieben, neue `Current`-Revision mit Import-`Comment` (z.B. `"Import (WinCan DB)"`, `"Import (IBAK Daten.txt)"`).

Parallel werden aus den Entries `VsaFindings` (Liste `VsaFinding` mit `KanalSchadencode`, `Raw`, `MeterStart/MeterEnd`, `MPEG`, `FotoPath`, `Quantifizierung1`) aktualisiert und ein menschenlesbarer `Primaere_Schaeden`-Text gebaut (Format pro Zeile `"0.00m CODE Beschreibung"`). Bei WinCan ist DB3 „Quelle der Wahrheit“: `record.VsaFindings = findings` (Ersetzen); bei IBAK wird gemerged.

Haltungs-Schlüssel werden überall normalisiert (`NormalizeHoldingKey`): Whitespace entfernen, `/`, En-/Em-Dash → `-`. IBAK strippt zusätzlich Video-Präfixe (`L__`, `L_`, `H__`) und Knoten-Präfixe (`07.1028055-10.1064892` → `1028055-1064892`). Matching gegen bestehende Records läuft exakt, dann über `HoldingKeyMatch.IsBoundaryPrefixMatch` (kein unscharfes `Contains`, damit `100-200` nicht `100-2000` trifft).

### WinCan-Import (DB3, mit Fallback-Kaskade)

`WinCanDbImportService` (`src/.../Import/WinCan/WinCanDbImportService.cs`). WinCan VX speichert in `.db3` (SQLite, via `Microsoft.Data.Sqlite`). Reihenfolge:
1. `FindDb3` sucht `*.db3` unterhalb eines `DB`-Ordners, nimmt die größte Datei.
2. Wenn nur `.sdf` (SQL Server Compact, **kein .NET-8-Treiber**): `TryImportViaXtfFallback` sucht `*.xtf` rekursiv und delegiert an `XtfImportServiceAdapter`; danach `LinkMediaFromFileIndex`. Sonst klare Benutzer-Meldung (im WinCan VX „Export → INTERLIS 2“ nötig).
3. Kein DB3/SDF → `ImportWithoutDb3` (MDB-Fallback via `M150MdbImportHelper.TryParseMdbFile`).

DB3-Schema (Tabellen/Spalten als Vertrag): `SECTION` (Haltungen, `OBJ_PK/OBJ_Key/OBJ_Street/OBJ_Material/OBJ_Size1/OBJ_Length/...`), `SECINSP` (Inspektionen, jüngste per Datum gewählt), `SECOBS` (Beobachtungen mit `OBS_OpCode`, `OBS_Distance`, `OBS_ContDefectLength`, `OBS_TimeCtr`, Quantifizierer `OBS_Q1..Q3_Value`, `OBS_U1..U3_Value`, `OBS_Char1/2`, `OBS_ClockPos1/2`, `OBS_SortOrder`, gefiltert `OBS_Deleted IS NULL`), `SECOBSMM` (Medien je Beobachtung), `NODE` (Schächte). Roh-OpCodes werden mit `VsaCodeValidator.TryNormalizeKnownCode` gegen den VSA-Katalog gesäubert (Parsing-Müll wird leer übernommen, nicht ins Training geschoben). `OBS_ContDefectLength > 0` → `IsStreckenschaden = true`, `MeterEnd = Distance + ContDefectLength`. Medien werden über einen `BuildFileIndex` (Dateiname → Pfade, nur eindeutige Treffer) in `Video`-/`Picture`-/`Report`-Unterordnern aufgelöst. Datumsparsing toleriert `Date(ms)`-JS-Format und europäische Formate (DD.MM.YYYY vor ISO, um DD/MM-Vertauschung zu vermeiden).

### IBAK-Import (Daten.txt + optional FDB)

`IbakExportImportService` (`src/.../Import/Ibak/IbakExportImportService.cs`). Sucht `Daten.txt` (bevorzugt im `Film`-Ordner), liest sie als **Windows-1252** (`CodePagesEncodingProvider` registrieren). Format pro Beobachtungszeile: `HH:MM:SS  <meter>m  <CODE>  <Beschreibung>` (Regex `ObservationRegex`); Header-Einträge ohne Zeitstempel (`AEC`, `AED`, `AEF`) über `HeaderLineRegex`. Eine Zeile ohne führenden Whitespace, die keine Beobachtung ist, startet eine neue Haltung.

Stammdaten aus Header-Codes (`ApplyHeaderFields`): `AEC` → DN (`Höhe=…mm`), `AED` → Material (`MapMaterial`: Polypropylen→PP, PVC, PE, Beton, Steinzeug, Guss, GFK), `AEF` → Baulänge nur als Fallback. Haltungslänge = max. `BCE`-Meterstand (Rohrende), sonst AEF-Fallback. Streckenschäden über Marker „Anfang/Beginn“ und „Ende“ mit Range-Index `(n)` zusammengeführt (`PendingRanges`). IBAK-Meta `@!$ibak$!` wird aus Beschreibungen gestrippt.

Fotos: zuerst Versuch über Firebird-FDB (`FirebirdSql.Data.FirebirdClient`, User/Passwort aus Env `IBAK_FDB_USER`/`IBAK_FDB_PASSWORD`, Default `SYSDBA`/`masterkey`, Charset WIN1252; Foto-Tabelle heuristisch über Spalten-Namens-Score erkannt, Identifier gequotet gegen SQL-Injection). Fallback: Dateinamen-Muster `(L__|L_|H__)<holding>_<index>.jpg`. Fotos werden Befunden zugeordnet, die im Text „foto“/„fotobeispiel“ enthalten.

### KINS-Import (Format-Erkennung + Delegation)

`KinsImportService` (`src/.../Import/Kins/KinsImportService.cs`) erkennt das tatsächliche Format mit **einem** rekursiven Scan (`DetectFormats` → `hasDb3/hasMdb/hasFdb/hasDatenTxt/hasKiDvDataTxt`) und delegiert:
- `kiDVDaten.txt` vorhanden → eigener KINS-TXT-Parser (`ImportKinsDvdText`).
- DB3, oder MDB ohne Daten.txt/FDB/kiDV → `IWinCanDbImportService`.
- `Daten.txt`/FDB → `IIbakImportService`.
- Uneindeutig → WinCan **und** IBAK als Fallback.

Der KINS-TXT-Parser liest Header-Zeilen mit `@Datei=<Video>` und Schacht-Paar via `<usage> <from> -> <to> [material] [DN]`; Holdingname = `from-to`. Beobachtungszeilen `(?<meter>\d+[.,]\d+)m <text> [@Pos=<pos>]` (Code bleibt leer, nur Beschreibung+Meter). Aufnahmedatum aus `kiDVinfo.txt` (Zeile „Aufnahmen … dd.MM.yy(yy)“). Ergebnisse werden per `MergeResult` summiert; KINS-TXT-Entries werden vor `ApplyProtocol` mit `CloneEntry` kopiert.

### PDF-Parser-Familie (mehrere getrennte Leser)

Der PDF-Import (`PdfImportServiceAdapter` → `LegacyPdfImportService`, `src/.../Import/Pdf/`) kombiniert mehrere unabhängige Leser, weil verschiedene Inspektionsfirmen (Fretz AG, KIT Bauinspekt, Abwasser Uri, IBAK-direkt) inkompatible Tabellenlayouts liefern:

1. **`PdfParser`** (`PdfParser.cs`) — regelbasierter Stammdaten-Feld-Extraktor. Regeln in `PdfFieldMapping.Rules` (Dictionary `Feldname → PdfFieldRule{ Regexes, Multiline, MaxLines }`) mit firmenspezifischen Regex-Varianten je Feld (z.B. `Haltungsname` matcht `Leitung …`, `Haltungsnahme`, `Haltung Nr.`). `PdfPostProcessors.Apply` normalisiert pro Feld (Kosten: CHF/`'` entfernen, Komma→Punkt; Material: erste Zeile, „Gereinigt“-Suffix strippen; Nutzungsart→Schmutz/Regen/Mischabwasser; Inspektionsrichtung→„In/Gegen Fliessrichtung“). `ExtractPrimaryDamages` parst Befundzeilen in zwei Formaten: Standard `<meter> <code> <desc>` und Fretz `[foto] [HH:MM:SS] <meter> <code.X.Y> <desc>`. `EnsureValidHaltungsname` rettet den Haltungsnamen aus Same-Line-, Zwei-Zeilen-Tabellen- und Schacht-Paar-Layouts („Oberer/Unterer Schacht“).
2. **`LegacyPdfImportService`** — Orchestrierung: Textextraktion (`PdfTextExtractor`, PDFPig; OCR-Fallback `PdfOcrExtractor` bei textlosen Scans), `PdfChunking.SplitIntoHaltungChunks` zerteilt das Dokument je Haltung, dann pro Chunk Felder parsen, Holding-Key auflösen (`TryResolveHoldingKey`: Feld → Chunk-DetectedId → Tabellenzeile → Dateiname/Pfad), Plausibilität via `HoldingIdPlausibility`. Merge in bestehende Records über `MergeEngine.MergeRecord` (mit `fillMissingOnly`). Schacht-Protokolle (`LooksLikeSchachtProtokoll` erkennt „Schachtprotokoll“) gehen einen eigenen Pfad: `ParseSchachtFields` + `ParseSchachtDamageEntries` über feste Bauteil-Reihenfolge (`Schachtdeckel, Deckelrahmen, Schachthals, Konus, Schachtrohr, Bankett, Durchlaufrinne, Anschluss, Leiter/Steigeisen, Tauchbogen`) und Checkbox-Glyph-Erkennung (`●/✓/☒/[x]` vor/nach dem Schadenswort). Robustheit: Placeholder-Records (`UNBEKANNT_…`, „Datum :“-Header) werden über Fingerprint (`Primaere_Schaeden|Inspektionsrichtung|Nutzungsart|DN|Länge|Material`) gegen echte IDs gemappt und bereinigt. PDF-Datei wird in `PDF_Path`/`PDF_All` verlinkt; `Anschluesse_verpressen` notfalls via `ConnectionCountEstimator` geschätzt. `PdfImportSafetyPolicy` begrenzt Dateigröße/Seitenzahl.
3. **`PdfProtocolTableParser`** (im MCP-Server, `tools/SewerStudioMcpServer/SewerStudioToolRegistry.cs`) — separater Tabellen-Leser, der pro `case_id` Protokolleinträge für die KI-Trainings-/Eval-Pipeline liefert (nicht der App-Importpfad). `PdfProjectMetadataParser` füllt Projekt-Metadaten/Projektname.

Ein **vierter** Pfad ist `HoldingFolderDistributor` / `HoldingFolderDistributor.PdfParsing.cs` (`ParsePdfPage` liefert Haltung+Datum), der einzelne PDFs Ordnern/Haltungen zuordnet und dabei optional den Kataster-Resolver nutzt.

### XTF / INTERLIS / SIA405

`XtfImportServiceAdapter` → `LegacyXtfImportService` (`src/.../Import/Xtf/`). Liest INTERLIS-2-XTF (LINQ-to-XML, namensraum-tolerant über `LocalName`). Zwei Datenebenen: Kanal-/Haltungs-Stammdaten (Elemente `*.Haltung`, `Bezeichnung`, `LaengeEffektiv`, `Lichte_Hoehe`, `Material`, Verknüpfung über `Kanal`/`Haltungspunkt`-Bezeichnungen) und Untersuchungs-/Befundebene (`Untersuchung` mit `vonPunktBezeichnung`/`bisPunktBezeichnung`, `Schadensbeobachtung` → Findings je Haltung). Schreibt `Haltungsname`, `Haltungslaenge_m`, `Rohrmaterial`, `DN_mm`, `Datum_Jahr`, `Strasse`, `Nutzungsart`, `Bemerkungen`, `Eigentuemer`, `Offen_abgeschlossen`, `Schacht_oben`/`Schacht_unten` mit `FieldSource.Xtf405` bzw. `Xtf`. `XtfPrimaryDamageFormatter.DeduplicateText` säubert den Schadenstext. `XtfHelper` und `M150MdbImportHelper` (MDB-Fallback für WinCan, M150-Schema) sind geteilte Hilfen.

### Kataster-Haltung-Resolver (Schacht-Paar → amtliche Haltung)

`HaltungCadastreExtractor` + `HaltungCadastreIndex` (`src/.../Map/`) lösen das Problem, dass Inspektions-/Dichtheitsprüf-Protokolle Schächte in beliebiger Reihenfolge nennen, der Abwasserkataster aber die kanonische Haltungsbezeichnung („865-864“ = von 865 nach 864) kennt.

- **Extraktion:** `HaltungCadastreExtractor.Extract(xtfPath)` streamt die ~600 MB große SIA405-XTF per `XmlReader` (DTD verboten, kein XmlResolver), liest aus `*.Haltung` die `Bezeichnung`, `LaengeEffektiv`, `Lichte_Hoehe`, `Material`; das Schacht-Paar wird aus `Bezeichnung` per `SplitShaftPair("A-B")` abgeleitet. `BuildTable` schreibt eine eigenständige **TSV** mit Metazeile (`# source=… bytes=… mtimeUtc=…`) + Header `Bezeichnung\tShaftA\tShaftB\tLaenge\tLichteHoehe\tMaterial`.
- **Feste Ablage:** `HaltungCadastreIndex.DefaultTablePath` = `%LOCALAPPDATA%\SewerStudio\map\abwasserkataster_haltungen.tsv`. `EnsureAndLoad(xtfPath, tablePath?)` baut die Tabelle nur neu, wenn sie fehlt oder via `IsTableFresh` (Bytes + mtime) als veraltet erkannt wird — die Riesen-XTF wird nicht bei jeder Verteilung neu geparst.
- **Resolver-Vertrag** (`IHaltungCadastreResolver`): Index ist `pairKey ("864|865") → Set kanonischer Bezeichnungen`; `PairKey` ist reihenfolge-unabhängig (lexikografisch sortiert). `TryResolvePair(a, b, out canonical)` liefert nur bei **genau einem** Treffer `true` (korrigiert vertauschte Schächte). `PairExists` ist ein Plausibilitäts-Gate (auch bei Mehrdeutigkeit true). `ResolveFromCandidates(zahlen)` testet alle Paare einer Kandidatenmenge gegen den Index. Der Resolver wird optional in `HoldingFolderDistributor` durchgereicht (`IHaltungCadastreResolver? cadastre = null`).

### Export

- **Excel:** `IExcelExportService` (`src/.../Application/Export/IExportServices.cs`) mit `ExportToTemplate` und `ExportSchaechteToTemplate(project, templatePath, outputPath, headerRow, startRow)`. Produktiv-Implementierung `ExcelTemplateExportService` (`src/.../Export/Excel/`, **ClosedXML**): öffnet eine vorhandene `.xlsx`-Vorlage, sucht das Worksheet „Haltungen“, liest die Header-Zeile (`headerRow`, Default 11), mappt Spalten-Überschriften über Aliasse (`"Haltungsnahme (ID)"→Haltungsname`, `"Fliessrichtung"→Inspektionsrichtung`) und `FieldCatalog.Definitions` (Label/Key, normalisiert) auf logische Feldnamen, leert ab `startRow` (Default 12) und schreibt die Records sortiert nach `NR` dann `Haltungsname`, Spaltenreihenfolge aus `FieldCatalog.ColumnOrder`. `CsvExcelExportService` ist die einfache CSV-Fallback-Implementierung desselben Interfaces (Semikolon-getrennt, alle `FieldCatalog.ColumnOrder`-Felder, UTF-8).
- **PDF:** Vertrag und Optionen liegen unter `src/.../Application/Reports/`; `ProtocolPdfExporter` und die übrigen QuestPDF-Renderer unter `src/.../Infrastructure/Reports/` (**QuestPDF** Community-Lizenz). `BuildPdf(projectTitle, ProtocolDocument doc, projectRootAbs, ProtocolPdfExportOptions)` → `byte[]`. Rendert A4-Protokoll mit Header (Projekt, `Haltung: doc.HaltungId`, Revision), optionaler KI-Zusammenfassung und einem Block je nicht-gelöschtem `ProtocolEntry` (Code, „Meter/Strecke“-Bereich), plus Haltungsgrafik (feste Pixelmaße `770×520`, Schachtknoten oben/unten).
- **Devis/Kostenvoranschlag:** Es gibt im aktuellen HEAD keinen dedizierten Devis-/Excel-Export als eigenen Service — Kostenvoranschlags-Export läuft über die Excel-Template-/CSV-Wege bzw. ist noch nicht als eigener Service implementiert (in Doku nur als Vorschlag, „CPM“ offen).

### Relevante Dateipfade

- Verträge: `src/AuswertungPro.Next.Application/Import/IImportServices.cs`, `.../Import/ImportRunContext.cs`, `src/AuswertungPro.Next.Application/Export/IExportServices.cs`
- Importer: `src/AuswertungPro.Next.Infrastructure/Import/{WinCan/WinCanDbImportService.cs, Ibak/IbakExportImportService.cs, Kins/KinsImportService.cs, Xtf/*, Pdf/*}`
- Kataster: `src/AuswertungPro.Next.Infrastructure/Map/{HaltungCadastreExtractor.cs, HaltungCadastreIndex.cs}`, Tabellen-Builder `tools/CadastreTableBuilder/Program.cs`
- Export: `src/AuswertungPro.Next.Infrastructure/Export/{Excel/ExcelTemplateExportService.cs, CsvExcelExportService.cs}`, `src/AuswertungPro.Next.Infrastructure/Reports/ProtocolPdfExporter.cs`
- Verdrahtung: `src/AuswertungPro.Next.UI/ServiceProvider.cs` (Konstruktion, ab Zeile 92)

### Fallstricke (für den Nachbau wichtig)

- IBAK-`Daten.txt` und KINS-`kiDVDaten.txt` sind **Windows-1252**, nicht UTF-8 — sonst Umlaut-Müll. `CodePagesEncodingProvider.Instance` registrieren.
- WinCan `.sdf` ist unter modernem .NET (Core/10) nicht lesbar → zwingend XTF-Fallback oder MDB; nie als Fehler abbrechen, sondern Benutzer zum INTERLIS-Export führen.
- Medien-/PDF-Auflösung nimmt nur **eindeutige** Dateinamen-Treffer (`ResolveFile` gibt bei >1 Kandidat `null` zurück), sonst falsche Zuordnung.
- Holding-Matching nie über `Contains` (verwechselt `100-200`/`100-2000`); immer Normalisierung + `IsBoundaryPrefixMatch`.
- Re-Import darf keine doppelten Protokoll-Revisionen erzeugen → `ProtocolContentFingerprint.HasSameContent` vor neuer Revision prüfen.
- Roh-Codes aus Fremdsystemen vor Übernahme gegen den VSA-Katalog normalisieren (`VsaCodeValidator.TryNormalizeKnownCode`), sonst gelangt Parsing-Müll ins Training.

---

# TEIL B · FACHDOMÄNE — VSA-REGELWERK (verbindlich)

> Dies ist die fachliche Wahrheit für alle Codier-, Quantifizierungs- und Zustandsbewertungs-Entscheidungen.
> Inhaltlich unverändert übernommen aus der bestehenden Referenz — mit **einer Korrektur in §14.3**
> (Modell-Versionen) gegen den realen Sidecar-Code: produktiver Segmenter ist **SAM 2.1**
> (`sam2.1_hiera_large.pt`, via `SAM2ImagePredictor`), nicht SAM-1 `vit_h`; Grounding DINO bevorzugt
> **Swin-B** (`grounding_dino_swinb`, Loader-Präferenz; Stresstest 2026-06-20 bestanden — 1000 Frames, 0 Timeouts, VRAM-Peak ≪ 29 GB), Fallback
> Swin-T OGC. SAM 3 existiert nur als deaktivierte Experiment-Option (`sam3_weights_path`, Default aus);
> die frühere Ablage `models/sam3` ist entfernt.


> Zweck: Eine vollständige, eigenständige Grundlage, um eine neue KI-Pipeline für die
> automatische Kanalinspektions-Codierung aufzubauen. Enthält den kompletten Code-Katalog,
> alle Codier-, Quantifizierungs- und Zustandsbewertungs-Regeln sowie die empirisch
> belegten Erkenntnisse, was technisch erkennbar ist und was nicht.

## 0. Quellen & Verbindlichkeit

| Bereich | Quelle |
|---|---|
| Schadencodierung | **VSA-Merkblatt Schadencodierung und Datentransfer 2019** (SN EN 13508-2) |
| Code-Katalog (Single Source of Truth) | **`vsa_kek_2020_catalog_manifest.json`** (ADR-006) — 680 Codes |
| Zustandsbewertung | **VSA-Richtlinie Zustandsbeurteilung 2023**, Anhang C (Kanäle) |
| Aufnahmetechnik | **Vorgaben für Kanalfernsehaufnahmen**, Abwasser Uri V1.1 |
| Datenformat | SN EN 13508, VSA-KEK / Interlis 2, WinCan |

**Wichtig:** Schadencodierung (2019) und Zustandsbewertung (2023) sind **zwei getrennte
Richtlinien**. Die Codierung sagt *was* zu sehen ist, die Zustandsbewertung *wie schlimm*.

---

## 1. Grundbegriffe

- **Haltung** = Kanalabschnitt zwischen zwei Schächten (typisch 30–80 m, max ~200 m).
- **Schacht** = Zugangs-/Knotenpunkt (Kontrollschacht KS, Schlammsammler SS …).
- **Haltungsnummer** = VonSchacht-NachSchacht (z. B. `506.02-34476`).
- **DN** = Nennweite in mm (DN150 = Hausanschluss, DN300 = Standard, DN600+ = Sammler).
- **Inspektionsrichtung** = meist in Fliessrichtung; Gegenbefahrung möglich (s. §10).
- **OSD** = On-Screen-Display im Video (Meterstand, Haltungsname, Datum).
- **Meterstand** = Kameraposition in der Haltung (0.00 m = Anfang).

---

## 2. Code-Struktur

```
B A B . B . A   = Riss, Untertyp "Riss", Lage "längs"
│ │ │   │   └─ Char2 (Lage/Richtung)
│ │ │   └───── Char1 (Untertyp)
│ │ └───────── 3. Zeichen Hauptcode
│ └─────────── 2. Zeichen (Gruppe: A=baulich, B=betrieblich, C=Bestand, D=sonstige)
└───────────── 1. Zeichen (B=Kanal, D=Schacht, A=Grundlagen-Änderung)
```

- **Hauptcode** = 3 Zeichen (z. B. `BAB`). **Char1** = Untertyp. **Char2** = Lage/Richtung.
- Ein vollständiger Code ist 3–5 Zeichen (z. B. `BABBA`, `BCAFA`, `BCCAY`).
- Im Manifest hat jeder Code: `code`, `title`, `isSelectable`, `group`, `parameters`.

### Parameter-Typen (Quantifizierung), die im Katalog vorkommen
| dataKey / Typ | Bedeutung |
|---|---|
| `Q1` (number) | Quantifizierung 1 (Hauptmaß, einheitenabhängig je Code) |
| `Q2` (number) | Quantifizierung 2 (z. B. Breite bei Anschluss BCA) |
| `SchadenlageAnfang/Ende` (clock) | Uhrlage Anfang/Ende (12 = Scheitel, 6 = Sohle) |
| `Verbindung` (string) | Verbindungs-Kennung (z. B. „A" — verknüpft Streckenschaden-Anfang/-Ende) |
| `*` (im Katalog) | Parameter ist **Pflicht** (`required: true`) |

---

## 3. Steuercodes / Grundgerüst (Pflicht & Struktur)

| Code | Bedeutung | Regel |
|---|---|---|
| **BCD** | Rohranfang | **Pflicht** bei 0.00 m (= Nullpunkt der Distanzmessung) |
| **BCE** | Rohrende | **Pflicht** am Haltungsende |
| **BCA** | Seitlicher Anschluss | wenn vorhanden (mit Char1/Char2) |
| **BCC** | Bogen / Richtungsänderung | wenn vorhanden |
| **BCB** | Reparatur sichtbar | wenn vorhanden |
| **BDB** | Anmerkung / Beginn TV-Untersuchung | optional / Steuercode |
| **BDC** | Abbruch der Inspektion | bei Abbruch |
| **BCDXP / BCEXP** | Distanzmessung Anfang/Ende (Pickelloch im Schacht) | optional |

---

## 4. Vollständiger Kanal-Schadenskatalog

> Direkt aus `vsa_kek_2020_catalog_manifest.json` (verbindlich). Format:
> **Hauptcode – Titel | Quant: Pflichtparameter (`*`) | Anzahl Codes**, darunter die wählbaren Subcodes.

### BA – Bauliche Schäden (Kanal)

**BAA - Verformung**  | Quant: (keine im Katalog; Q1 % laut Richtlinie) | 3 Codes
  - `BAAA` Rohr vertikal deformiert
  - `BAAB` Rohr horizontal deformiert

**BAB - Riss**  | Quant: Q1*, Uhrlage(A), Uhrlage(E), Verbindung | 16 Codes
  - `BABAA` Oberflächenriss (Haarriss) längs · `BABAB` …radial · `BABAC` …komplexe Rissbildung · `BABAD` …spiralförmig · `BABAE` …sternförmig
  - `BABBA` Riss längs · `BABBB` Riss radial · `BABBC` Riss komplex/Scherbenbildung · `BABBD` Riss spiralförmig · `BABBE` Riss sternförmig
  - `BABCA` Klaffender Riss längs · `BABCB` …radial · `BABCC` …komplex/Scherben · `BABCD` …spiralförmig · `BABCE` …sternförmig

**BAC - Leitungsbruch / Einsturz**  | Quant: Q1*, Uhrlage(A/E), Verbindung | 4 Codes
  - `BACA` In der Lage verschobene Scherbe · `BACB` Fehlende Scherbe/Wandungsteil (Loch) · `BACC` Leitungsbruch/Einsturz

**BAD - Defektes Mauerwerk**  | Quant: (keine) | 5 Codes
  - `BADA` Steine verschoben · `BADB` Steine fehlen · `BADC` Sohle abgesackt · `BADD` Einsturz

**BAE - Mörtel aus Mauerwerk fehlt**  | Quant: (keine; Q1 Tiefe mm laut Richtlinie) | 1 Code

**BAF - Oberflächenschaden**  | Quant: Uhrlage(A/E), Verbindung | 64 Codes
  Systematik Char1: A=raue Rohrwand, B=Abplatzung, C=Zuschlagstoffe sichtbar, D=Zuschlagstoffe einragend, E=Zuschlagstoffe fehlen, F=Bewehrung sichtbar, G=Bewehrung einragend, H=Bewehrung korrodiert, I=fehlende Rohrwandung, J=Rohrwand korrodiert, K=Beule, Z=andersartig.
  Char2: A=mechanisch, B=chemisch, C=chemisch oben, D=chemisch unten, E=Ursache unklar, Z=andere Ursache.
  (z. B. `BAFAA` raue Rohrwand mechanisch, `BAFJB` Rohrwand korrodiert chemisch, `BAFIB` fehlende Rohrwandung chemisch.)

**BAG - Anschluss einragend**  | Quant: Q1*, Uhrlage(A/E) | 2 Codes — `BAGA`

**BAH - Schadhafter Anschluss**  | Quant: Uhrlage(A/E) | 7 Codes
  - `BAHA` falsch eingeführt · `BAHB` zurückliegend · `BAHC` unvollständig/nicht eingebunden · `BAHD` beschädigt · `BAHE` verstopft · `BAHZ` andersartig

**BAI - Einragendes Dichtungsmaterial**  | Quant: Q1*, Uhrlage(A/E), Verbindung | 6 Codes
  - `BAIAA` Dichtring verschoben · `BAIAB` einragend, nicht gebrochen, oberhalb Rohrmitte · `BAIAC` …unterhalb · `BAIAD` einragend, gebrochen · `BAIZ` einragendes Dichtungsmaterial

**BAJ - Verschobene Rohrverbindung**  | Quant: Q1*, Uhrlage(A/E), Verbindung | 4 Codes
  - `BAJA` Breite Rohrverbindung (Q1 Abstand mm) · `BAJB` Versetzt (Q1 mm) · `BAJC` Knick (Q1 Winkel °)

**BAK - Feststellung der Innenauskleidung**  | Quant: (keine) | 19 Codes
  - `BAKA` abgelöst · `BAKB` verfärbt · `BAKC` Endstelle schadhaft · `BAKDA-DD` Faltenbildung (längs/radial/komplex/spiral) · `BAKE` Blasen/Beulen · `BAKF` Beule nach aussen · `BAKG` Ablösen Innenhaut · `BAKH` Ablösen Verbindungsnaht · `BAKI` Riss/Spalt · `BAKJ` Loch · `BAKK` Verbindung defekt · `BAKL` Werkstoff weich · `BAKM` Harz fehlt · `BAKN` Ende nicht abgedichtet · `BAKZ` andersartig

**BAL - Schadhafte Reparatur**  | Quant: (keine) | 12 Codes
  - `BALA` Wand fehlt teilweise · `BALB` Loch mangelhaft · `BALC` löst sich vom Altrohr · `BALD` fehlt an Kontaktfläche · `BALE` überschüssig (Hindernis) · `BALF` Loch · `BALGA-GD` Riss (längs/radial/komplex/spiral) · `BALZ` andersartig

**BAM - Schadhafte Schweissnaht**  | Quant: (keine) | 4 Codes — `BAMA` längs · `BAMB` radial · `BAMC` spiralförmig

**BAN - Leitung porös** | **BAO - anstehender Boden sichtbar** | **BAP - Hohlraum sichtbar** | je 1 Code, keine Quant.

### BB – Betriebliche Feststellungen (Kanal)

**BBA - Wurzeln**  | Quant: Q1* (% Querschnitt), Uhrlage(A/E), Verbindung | 4 Codes
  - `BBAA` Pfahlwurzel · `BBAB` einzelner feiner Einwuchs · `BBAC` komplexes Wurzelwerk

**BBB - Anhaftende Stoffe**  | Quant: Q1* (% Querschnitt), Uhrlage(A/E), Verbindung | 5 Codes
  - `BBBA` Inkrustation (verkalkt) · `BBBB` Fett · `BBBC` Fäulnis · `BBBZ` andersartig

**BBC - Ablagerung**  | Quant: Q1* (% Querschnitt), Uhrlage(A/E), Verbindung | 5 Codes
  - `BBCA` lose Sand · `BBCB` lose Kies · `BBCC` harte Ablagerungen · `BBCZ` andersartig

**BBD - Eindringendes Bodenmaterial**  | Quant: (keine; Q1 % laut Richtlinie) | 5 Codes
  - `BBDA` Sand · `BBDB` organisch · `BBDC` Feinmaterial · `BBDD` Grobmaterial · `BBDZ` Bodenmaterial

**BBE - Hindernis**  | Quant: (keine) | 9 Codes
  - `BBEA` Stein in Sohle · `BBEB` Leitungsstück · `BBEC` Gegenstand in Sohle · `BBED` ragt durch Wand · `BBEE` in Rohrverbindung eingeklemmt · `BBEF` aus Anschluss in Hauptleitung · `BBEG` fremde Werkleitungen/Kabel · `BBEH` in Rohrkörper eingebaut · `BBEZ` andersartig

**BBF - Infiltration**  | Quant: Uhrlage(A/E), Verbindung | 5 Codes
  - `BBFA` Schwitzen/Verkalkung · `BBFB` Wasser tropft · `BBFC` Wasser fliesst · `BBFD` Wasser spritzt

**BBG - Sichtbarer Wasseraustritt (Exfiltration)** | 1 Code, keine Quant.

**BBH - Ungeziefer**  | Quant: (keine; Q1 Anzahl laut Richtlinie) | 12 Codes
  - `BBHA*` Ratte · `BBHB*` Kakerlake · `BBHZ*` Tier (jeweils in Rohrleitung/Anschluss/offener Rohrverbindung)

### BC – Bestandsaufnahme / Grundgerüst (Kanal)

**BCA - Seitlicher Anschluss**  | Quant: Q1* (Höhe mm), Q2 (Breite mm), Uhrlage(A/E), Verbindung | 17 Codes
  Char1: A=Formstück, B=Sattel gebohrt, C=Sattel eingespitzt, D=gebohrt, E=eingespitzt, F=Spezial, G=unbekannt, Z=andersartig. Char2: A=offen, B=verschlossen.
  - `BCAAA/AB`, `BCABA/BB`, `BCACA/CB`, `BCADA/DB`, `BCAEA/EB`, `BCAFA/FB`, `BCAGA/GB`, `BCAZA/ZB`

**BCB - Punktuelle Reparatur (sichtbar)**  | Quant: Uhrlage(A/E), Verbindung | 9 Codes
  - `BCBA` Rohr ausgetauscht · `BCBB`/`BCBF` örtliche Innenauskleidung · `BCBC` Mörtelinjizierung · `BCBD` Injizierung · `BCBE` Loch repariert · `BCBG` Anschluss-Reparatur · `BCBZ` andersartig (grabenlos)

**BCC - Bogen**  | Quant: (keine; Q1 Winkel ° laut Richtlinie) | 10 Codes
  - `BCCAA` links oben · `BCCAB` links unten · `BCCAY` **nach links** · `BCCBA` rechts oben · `BCCBB` rechts unten · `BCCBY` **nach rechts** · `BCCYA` nach oben · `BCCYB` nach unten

**BCD - Rohranfang**  | Steuercode | 2 Codes — `BCD`, `BCDXP` (Distanzmessung Anfang)
**BCE - Rohrende**  | Steuercode | 2 Codes — `BCE`, `BCEXP` (Distanzmessung Ende)

### BD – Sonstige (Kanal)

**BDA - Allgemeinzustand / Fotobeispiel** | 1 Code, keine Quant.

**BDB - Allgemeine Anmerkung**  | 14 Codes (Steuercodes)
  - `BDBA` Beginn TV-Untersuchung · `BDBB` erst nach Reinigung möglich · `BDBC` später · `BDBF` von der Gegenseite · `BDBG-J` Kamera nicht einsetzbar · `BDBM` Gegenseite nicht möglich (einige ungültig → Ersatz BDC*)

**BDC - Abbruch der Inspektion**  | 28 Codes
  - Char1: A=Hindernis, B=hoher Wasserstand, C=Kamera defekt, Z=anderer Grund.
  - Char2: A=Ziel erreicht, B=Auftraggeber verzichtet, C=Gegenseite erreicht, D=Gegenseite nicht erreicht, E=unklar, Z=neutral. (z. B. `BDCAZ`, `BDCBZ`, `BDCZZ`.)

**BDD - Wasserspiegel**  | Quant: Q1* (% lichte Höhe), Verbindung | 6 Codes
  - `BDDA` klar · `BDDC` trüb · `BDDD` gefärbt · `BDDE` trüb und gefärbt (`BDDB` ungültig → BDDC)

**BDE - Zufluss / Fehlanschluss**  | 18 Codes
  - Char1: A=klar, B/C=trüb, D=gefärbt, E=trüb+gefärbt, Y=neutral. Char2: A=Schmutz→Regen, B=Regen→Schmutz, C=nur Zufluss.

**BDF - Gefährliche Atmosphäre**  | 4 Codes — `BDFA` Sauerstoffmangel · `BDFB` H₂S · `BDFC` Methan · `BDFZ` andersartig

**BDG - Keine Sicht**  | 3 Codes — `BDGA` Kamera unter Wasser · `BDGB` Verschlammung · `BDGC` Dampf

### AE – Änderungen Grundlageninformationen (Kanal)

**AEC - Rohrprofilwechsel** (8 Codes: Ei/Kreis/Maul/offen/Rechteck/Spezial/unbekannt)
**AED - Rohrmaterialwechsel** (24 Codes: Beton-Varianten, Steinzeug, Guss, PVC/PP/PE, Faserzement …)
**AEF - Neue Baulänge** (1 Code)

---

## 5. Schacht-Codes (D*) — Übersicht

> 337 Codes. Von der aktuellen KI-Pipeline **nicht** klassifiziert (nur Kanal/B*). Für
> Vollständigkeit hier die Hauptcodes; Systematik analog zu Kanal (DA=baulich, DB=betrieblich,
> DC=Bestand, DD=sonstige).

- **DA*** (baulich): DAA Verformung, DAB Riss, DAC verschobene Scherbe, DAD defektes Mauerwerk, DAE fehlender Mörtel, DAF Oberflächenschaden (63 Subcodes), DAG–DAP (Anschluss/Dichtring/Verbindung/Auskleidung/Reparatur/Schweissnaht/porös/Boden/Hohlraum), DAQ Steighilfe locker, DAR Deckel gebrochen.
- **DB*** (betrieblich): DBA Wurzeln, DBB Inkrustation, DBC Ablagerung, DBD Bodeneintrag, DBE Hindernis, DBF Infiltration, DBG Wasseraustritt, DBH Ungeziefer.
- **DC*** (Bestand): DCA Anschluss, DCB Reparatur, DCF Material, DCG Zulauf/Ablauf, DCH Bankett, DCI Durchlaufrinne, DCL Rohrdurchführung, DCM Schlammeimer.
- **DD*** (sonstige): DDA Foto, DDB Anmerkung, DDC Untersuch nicht möglich, DDD Wasserspiegel, DDE Fehlanschluss, DDF Gefährdung, DDG keine Sicht.

---

## 6. Quantifizierung — Einheiten pro Schadenstyp

| Code | Q1 (Einheit) | Q2 | Hinweis |
|---|---|---|---|
| BAA Verformung | % Deformation | – | materialabhängig (s. §11) |
| BAB Riss | Breite mm | – | A=Haarriss: **keine** Quant. |
| BAC Bruch | Länge mm | – | |
| BAE fehlender Mörtel | Tiefe mm | – | |
| BAF Oberflächenschaden | % Ausmaß | – | |
| BAG einragender Anschluss | % Querschnittsminderung | – | |
| BAI einragendes Dichtungsmaterial | % Querschnittsminderung | – | DN-relativ |
| BAJ verschobene Rohrverbindung | A/B: Abstand mm · C: Winkel ° | – | DN-relativ |
| BBA Wurzeln | % Querschnitt | – | |
| BBB anhaftende Stoffe | % Querschnitt | – | |
| BBC Ablagerung | % Querschnitt | – | |
| BBD eindringender Boden | % Querschnitt | – | |
| BBE Hindernis | % Querschnitt | – | |
| BBH Ungeziefer | Anzahl | – | |
| BCA Anschluss | Höhe mm | Breite mm | |
| BCC Bogen | Winkel ° | – | (geometrisch, s. §16) |
| BDD Wasserspiegel | % lichte Höhe | – | |
| BDE Zufluss | % Wasserspiegel Anschluss | – | |

**Keine Quantifizierung:** BAD, BAH, BAK, BAL, BAM, BAN, BAO, BAP, BBF, BBG, BCB, BCD, BCE, BDA, BDB, BDC, BDF, BDG, AE*.

---

## 7. Uhrlage (Clock Position)

Immer aus **Kamerasicht in Fahrtrichtung**:
```
        12:00 (Scheitel/oben)
   11    |    1
 10      |      2
  9 ─────●───── 3 (rechts)
  8      |      4
   7     |    5
        6:00 (Sohle/unten)
```
- 12 = Scheitel · 6 = Sohle · 3 = rechts · 9 = links.
- Bei Schächten: 12 Uhr = tiefste ausgehende Leitung.
- Streckenschäden haben **Uhrlage Anfang UND Ende** (z. B. Riss von 10 bis 2 Uhr).

---

## 8. Distanzmessung

```
VonPunkt (Schacht oben)            BisPunkt (Schacht unten)
   |                                       |
   BCDXP   BCD        ...Haltung...       BCE   BCEXP
  -0.50   0.00m                         45.30m
          ↑ Rohranfang = Nullpunkt       ↑ Rohrende
```
- **BCD = 0.00 m** = Nullpunkt. **BCE** = Haltungsende.
- Inspektion **muss vor** dem Rohranfang beginnen (Schacht sichtbar).
- **BCDXP/BCEXP** = Pickellochpunkte im Schacht (vor/nach dem Rohr).
- Kanallänge ≠ Rohrlänge (Pickelloch-Differenz).

---

## 9. Punktschaden vs. Streckenschaden

- **Punktschaden:** eine Stelle, ein Meterstand (z. B. Riss, Anschluss).
- **Streckenschaden:** über Länge (z. B. Korrosion 2.5 m – 8.0 m) → MeterStart **und** MeterEnd.
- Verknüpfung über das **Verbindung**-Feld (Kennung, z. B. „A"): markiert zusammengehörigen
  Anfang/Ende desselben Streckenschadens.

---

## 10. Gegenbefahrung

- Abbruch (**BDC**) codieren, dann:
  - **BDB** mit Anmerkung **F** = „Inspektion erfolgt von der Gegenseite", oder
  - **M** = „Gegenseite nicht möglich".
- Am Ende: angeben, ob Gegenseite erreicht (für automatische Zusammenführung).
- VonPunkt = immer Anfangsschacht der Inspektion. Bei Gegenbefahrung tiefer liegenden Schacht
  als VonPunkt **oder** Zusatzattribut Inspektionsrichtung mitliefern (sonst Rückweisung —
  Schadenpositionen wären falsch).

---

## 11. Zustandsbewertung (VSA-Richtlinie 2023) — vollständige Tabellen

### 11.1 Einzelzustand EZ (0–4, INVERTIERTE Skala!)

| EZ | Bedeutung | Handlungsbedarf |
|---|---|---|
| **4** | kein Mangel / Neuzustand | kein |
| 3 | leichter Mangel | Beobachtung |
| 2 | mittlerer Mangel | mittelfristig (8 J) |
| 1 | schwerer Mangel | kurzfristig (3 J) |
| **0** | Ausfall / Gefahr in Verzug | **Sofortmassnahme** |

→ **EZ_min = 0 = schlechtester** Zustand. EZ=4 = bester. ZN und DZ folgen derselben Logik (niedrig = schlecht).

### 11.2 Klassifizierungstabellen (Code + Messung → EZ)

**BAA — Verformung (Q1 = %) — materialabhängig**
| Material | EZ=4 | EZ=3 | EZ=2 | EZ=1 | EZ=0 |
|---|---|---|---|---|---|
| biegesteif (Beton/Steinzeug/Guss) | <1% | 1–3% | 3–4% | 4–7% | ≥7% |
| biegeweich (PVC/PE/PP) | <2% | 2–6% | 6–10% | 10–15% | ≥15% |
| Ei/Maul | <10% | 10–25% | 25–40% | 40–50% | ≥50% |

**BAB — Riss (Q1 = Rissbreite mm)**
| Ch1 | Ch2 | Regel |
|---|---|---|
| A (Haarriss) | alle | **immer EZ=3** (Q1 irrelevant) |
| B (Riss) | A,D (längs/spiral) | nach Q1: <1=EZ3, 1–3=EZ2, 3–5=EZ1, ≥5=EZ0 |
| B (Riss) | B (radial) | pauschal EZ=3 |
| B (Riss) | C,E (komplex/stern) | pauschal EZ=1 (Bruchgefahr) |
| C (Bruchansatz) | alle | pauschal EZ=2 |

**BAC — Bruch/Einsturz**
| Ch1 | EZ |
|---|---|
| A (partiell / verschobene Scherbe) | 1 |
| B (komplett / fehlende Scherbe) | 0 |
| C (sichtbare Hohlräume) | 0 |

**BAE — Fehlender Mörtel:** <10mm=EZ3 · 10–100mm=EZ2 · ≥100mm=EZ1
**BAF — Oberflächenschaden (Q1 = %):** S-Achse <10/10–30/30–50/≥50 → EZ 4/3/2/1
**BAI — Einragend (DN-relativ):** <DN/10=EZ3 · DN/10–DN/4=EZ2 · ≥DN/4=EZ1
**BAJ — Versatz (DN-relativ):** <DN/10=EZ3 · DN/10–DN/4=EZ2 · DN/4–DN/2=EZ1 · ≥DN/2=EZ0
**BBA — Wurzeln (Q1 = %):** <10=EZ3 · 10–30=EZ2 · ≥30=EZ1
**BBB/BBC/BBD/BBE — Querschnitt (Q1 = %):** <25=EZ3 · 25–50=EZ2 · 50–75=EZ1 · ≥75=EZ0
**BBF — Infiltration:** A Feuchtigkeit=EZ3 · B Rinnsal=EZ2 · C laufend=EZ1 · D sprudelnd=EZ0
**BDD — Wasserstand (Q1 = %):** <5=EZ3 · 5–15=EZ2 · ≥15=EZ1

### 11.3 Zustandsnote ZN & Dringlichkeitszahl DZ

```
ZN_X = EZ_min + 0.4 − A           (X ∈ {D=Dichtheit, S=Standsicherheit, B=Betrieb})
A    = 0.4 × Σ((4 − EZ_i) × LF_i) / ((4 − EZ_min) × LA)   mit A ≤ 0.8
Gesamt-ZN = min(ZN_D, ZN_S, ZN_B)   (schlechteste Achse zählt)
DZ   = ZN × 100 × B1 × B2 × B3 × B4   (niedrig = dringend)
```
**ZN-Schwellen:** ≥3.0 „i.O." · ≥1.5 „beobachten" · <1.5 „Sanierungsbedarf".

**Faktoren B1–B4 (je kleiner = dringlicher):**
- B1 Gewässerschutz: Zone S=0.90 · Au/Zu/Ao=0.95 · übrige=1.00
- B2 Nutzungsart: Industrie=0.90 · Schmutz=0.95 · Misch=1.00 · Regen=1.05 · Bach=1.10
- B3 Grundwasser: unterhalb=0.90 · oberhalb=1.10
- B4 Hierarchie: Hauptsammelkanal regional=0.90 · Hauptsammelkanal=0.95 · Liegenschaft=1.10

**Dringlichkeitsstufen (DZ ≤):** 50→Sofort · 150→Kurzfristig(3J) · 250→Mittelfristig(8J) · 350→Langfristig · >350→keine Maßnahme.

### 11.4 Mapping KI-Severity ↔ VSA-EZ (INVERTIERT!)

| KI-Severity (1–5, hoch=schlecht) | VSA-EZ | Bedeutung |
|---|---|---|
| 1 | 4 | optisch / kein Mangel |
| 2 | 3 | leicht |
| 3 | 2 | mittel |
| 4 | 1 | schwer |
| 5 | 0 | kritisch / Ausfall |

**Formel: `EZ = 5 − Severity`.**

### 11.5 Kritische Schwellen (Cheatsheet)
| Schadenstyp | EZ=0 | EZ=1 | EZ=2 |
|---|---|---|---|
| Verformung biegesteif | ≥7% | 4–7% | 3–4% |
| Verformung biegeweich | ≥15% | 10–15% | 6–10% |
| Riss längs (BABBA) | ≥5mm | 3–5mm | 1–3mm |
| Bruch komplett (BACB/C) | immer | – | – |
| Versatz | ≥DN/2 | DN/4–DN/2 | DN/10–DN/4 |
| Querschnitt (BB*) | ≥75% | 50–75% | 25–50% |
| Infiltration | sprudelnd | laufend | Rinnsal |

### 11.6 Vollständige VSA-RiLi-Klassifizierung (verbindlich, 142 Regeln)

> **Quelle:** `VSA_Rili_ Zustandsbeurteilung von Entwaesserungsanlagen.pdf` → maschinenlesbar in
> `vsa_zustandsklassifizierung_2023_channels.json` (Anhang C, Kanäle). Dies ist die **autoritative**
> Fassung — jede Regel mit Anforderungsachse (**D**=Dichtheit, **S**=Standsicherheit, **B**=Betrieb),
> Char1/Char2, Parameter+Einheit und EZ-Schwellen. 142 Regeln über 26 Schadenscodes
> (Achsen-Verteilung: D=49, S=43, B=50).

**Lesweise:** `Ch1=… Ch2=… | Achse=X | parameter einheit [Material] → EZ-Schwellen`.
`[rigid]`=biegesteif, `[flexible]`=biegeweich, `[any]`=materialunabhängig. „pauschal" = fester EZ ohne Messwert. Schwelle `EZ3:1-3` = EZ 3 bei Wert ≥1 und <3.

#### Codes OHNE Zustandsnote (Bestandsaufnahme / Steuercodes / Stammdaten)
Diese Codes erzeugen **keine** EZ/ZN — sie sind Beobachtung/Metadaten (fachliche Freigabe):
`BCA`, `BCB`, `BCC`, `BCD`, `BCE`, `BDA`, `BDB`, `BDC`, `BDG`, `AEC`, `AED`, `AEF`.

#### Achsen-Ausnahmen (Code wird für eine Achse NICHT klassifiziert)
- **BAG** nur Achse **B** (nicht S) — Tabelle 13.
- **BAI** nicht Achse **S** — Tabelle 15.
(Weitere in `nonAssessableRequirements`.)

#### Näherungs-EZ ohne Messwert (Fallback wenn Q1 fehlt)
| Code | EZ | Grund |
|---|---|---|
| BAA | 2 | Verformung ohne Messwert — mittlerer Näherungswert |
| BAB | 2 | Riss ohne Messwert |
| BAC | 1 | Bruch ohne Messwert — strukturell ernst, konservativ |
| BAF | 3 | Oberflächenschaden ohne Messwert — vorsichtig mild |
| BAI | 2 | einragendes Material ohne Messwert |
| BBC | 3 | Ablagerung ohne Messwert — betrieblich, mittelfristig |

#### Vollständiger Regelsatz (Kanal)

**BAA – Verformung**
- Ch1=A,B | **S** | Q1 % [biegesteif] → EZ4:<1 · EZ3:1–3 · EZ2:3–4 · EZ1:4–7 · EZ0:≥7
- Ch1=A,B | **S** | Q1 % [biegeweich] → EZ4:<2 · EZ3:2–6 · EZ2:6–10 · EZ1:10–15 · EZ0:≥15
- Ch1=A,B | **B** | Q1 % [any] → EZ4:<10 · EZ3:10–25 · EZ2:25–40 · EZ1:40–50 · EZ0:≥50

**BAB – Riss**
- Ch1=A (Haarriss) | **S** | → **EZ4 pauschal**
- Ch1=B,C Ch2=A,C,D,E | **S** | Q1 mm → EZ4:<1 · EZ3:1–3 · EZ2:3–5 · EZ1:5–8 · EZ0:≥8
- Ch1=B,C Ch2=B (radial) | **S** | → **EZ4 pauschal**
- Ch1=B | **D** | → **EZ2 pauschal** · Ch1=C | **D** | → **EZ1 pauschal**

**BAC – Bruch/Einsturz**
- Ch1=A (partiell) | D=EZ1 · S=EZ2 · B=EZ2
- Ch1=B (komplett) | D=EZ1 · S=EZ2 · B=… · Ch1=C (Hohlräume) | **D=EZ0 · S=EZ0 · B=EZ0**

**BAD – Defektes Mauerwerk**
- A (Steine verschoben) | D=EZ2 · S=EZ2 · B=EZ3 · B (Steine fehlen) | D=EZ2 · S=EZ2
- C (Sohle abgesackt) | **EZ0 (D/S/B)** · D (Einsturz) | **EZ0 (D/S/B)**

**BAE – Fehlender Mörtel**
- **D** | Q1 mm → EZ4:<100 · EZ2:≥100 · **S** | Q1 mm → EZ4:<10 · EZ3:10–100 · EZ2:≥100

**BAF – Oberflächenschaden** (pauschal je Char1 — Schweregrad steigt A→I)
- Char1 **S-Achse**: A=EZ4 · B=EZ3 · C=EZ3 · D=EZ2 · E=EZ1 · F=EZ3 · G=EZ2 · H=EZ1 · I=EZ1 · J=EZ4 · Z=EZ4
- Char1 **D-Achse**: I=EZ1 · Z=EZ4 (übrige nur S/B) · **B-Achse**: durchgehend EZ4, K=EZ3

**BAG – Anschluss einragend** (nur **B**, materialfrei, Q1 %)
- Variante 1: EZ4:<10 · EZ3:10–20 · EZ2:20–30 · EZ1:30–50 · EZ0:≥50
- Variante 2 (anderer Bereich): EZ4:<10 · EZ3:10–40 · EZ2:40–60 · EZ1:60–80 · EZ0:≥80

**BAH – Schadhafter Anschluss**
- Ch1=B,C,D | D=EZ2 · Ch1=A,E | B=EZ4 · Ch1=Z | D=EZ3 · S=EZ3

**BAI – Einragendes Dichtungsmaterial** (nicht S)
- Ch1=A Ch2=A | D=EZ2 · B=EZ4 · Ch1=A Ch2=B,C,D | D=EZ2 · B=EZ3
- Ch1=Z | **B** | Q1 % → EZ4:<5 · EZ3:5–20 · EZ2:20–35 · EZ1:35–50 · EZ0:≥50

**BAJ – Verschobene Rohrverbindung**
- Ch1=A (breit) | **D** | Q1 mm → EZ4:<20 · EZ3:20–30 · EZ2:30–50 · EZ1:50–70 · EZ0:≥70 (Var.2: …–80) · S=EZ4
- Ch1=B (versetzt) | **D** | Q1 mm → EZ4:<10 · EZ3:10–15 · EZ2:15–20 · EZ1:20–30 · EZ0:≥30 · B → EZ4:<10 · EZ3:≥10
- Ch1=C (Knick) | **D** | Q1 ° → EZ4:<5 · EZ3:5–7 · EZ2:7–9 · EZ1:9–12 · EZ0:≥12 (Var.2 enger: <2/2–3/3–4/4–6/≥6) · S=EZ4

**BAK – Innenauskleidung** (19 Untercodes, je Char1 D/S/B unterschiedlich — z. B. J=Loch D=EZ1, I=Riss D=EZ2, A=abgelöst B-Skala <5/5–20/20–35/35–50/≥50)

**BAL – Schadhafte Reparatur** (Char1 A–Z: D meist EZ1–2 pauschal; E=Loch B-Skala; G=Riss D=EZ3)

**BAM – Schadhafte Schweissnaht** | Ch1=A,C | D=EZ2 · S=EZ3 · Ch1=B | D=EZ2 · S=EZ4

**BAN – porös** | D=EZ2 · S=EZ2 · **BAO – Boden sichtbar** | D=EZ1 · S=EZ1 · **BAP – Hohlraum** | D=EZ1 · **S=EZ0**

**BBA – Wurzeln**
- D | Q1 % → **EZ2 pauschal** · **B** | Q1 % → EZ3:<10 · EZ2:10–20 · EZ1:20–30 · EZ0:≥30

**BBB – Anhaftende Stoffe**
- Ch1=A | D=EZ3 · alle | **B** | Q1 % → EZ4:<5 · EZ3:5–10 · EZ2:10–20 · EZ1:20–30 · EZ0:≥30

**BBC – Ablagerung**
- Ch1=A,B (lose) | B → **EZ4 pauschal** · Ch1=C,Z (hart) | **B** | Q1 % → EZ4:<10 · EZ3:10–25 · EZ2:25–40 · EZ1:40–50 · EZ0:≥50

**BBD – Eindringender Boden** | D=EZ1 · **S=EZ0** · B → EZ3:<10 · EZ2:10–20 · EZ1:20–30 · EZ0:≥30

**BBE – Hindernis**
- Ch1=D,G | D=EZ2 · alle | **B** | Q1 % → EZ4:<5 · EZ3:5–20 · EZ2:20–35 · EZ1:35–50 · EZ0:≥50

**BBF – Infiltration**
- Ch1=A,B (Schwitzen/Tropfen) | D=EZ2 · S=EZ3 · B=EZ4 · Ch1=C (fliesst) | S=EZ2 · D=EZ1 · Ch1=D (spritzt) | S=EZ1 · D=EZ1

**BBG – Exfiltration** | D=EZ1 · S=EZ3
**BBH – Ungeziefer** | B → **EZ4 pauschal** (Q1 Anzahl)

**BDD – Wasserspiegel** | **B** | Q1 % → EZ4:<10 · EZ3:10–50 · EZ2:≥50

**BDE – Fehlanschluss/Zufluss** | **B** | Ch2=A→EZ1 · Ch2=B→EZ2 · Ch2=C→(siehe PDF) · Y/Y→EZ4

> Hinweis: Doppelte Schwellensätze (z. B. BAG, BAJ-A, BAJ-C) entsprechen unterschiedlichen
> Bereichen/DN-Klassen in der Richtlinie. Bei der Implementierung beide Varianten je nach
> `scope.areas` anwenden. Vollständige Maschinen-Quelle: `vsa_zustandsklassifizierung_2023_channels.json`
> (`rules[]`), für Schächte analog `vsa_zustandsklassifizierung_2023_manholes.json`.

---

## 12. Aufnahmetechnik (Vorgaben Abwasser Uri V1.1)

- Aufnahmekatalog: **EN13508_VSA-2019**. Export: VSA-KEK nach Interlis 2.3 (.ili + .xtf).
- **Rohranfang + Rohrende MÜSSEN auf Video + Foto** sichtbar sein.
- Inspektion **vor** dem Rohranfang beginnen (Schacht sichtbar). Nach Rohrende Schacht mitaufnehmen.
- Während Fahrt: **entweder fahren ODER schwenken** — nie gleichzeitig. Vor Weiterfahrt Objektiv in Axialsicht zurückschwenken.
- Bei Anschluss: hineinzoomen wenn möglich. Beim Rohrende: 12–12 Uhr schwenken.
- Rohrmaterial korrekt angeben (PVC/PP/PE/Beton/Steinzeug…). Rohrlänge in Haltungsdaten.
- Schachtprotokoll: mind. 3 Fotos (Situation, Auslauf bei 12 Uhr, Schacht). Distanzmessung beginnt beim Deckel.
- Nummerierung: reine Schachtnummern (z. B. `506.02`, **nicht** `KS506.02`).

---

## 13. Datenformate

- **VSA-KEK Interlis 2** (.ili Struktur + .xtf Inhalt): Untersuchungen, Schadencodes, verknüpfte Dateien.
- **SIA 405 Abwasser** (Interlis 2): Werkinformationen (Material, DN, Profil, Rohrlängen). Alternative: Excel.
- **Dateinamen:** max 60 Zeichen, eindeutig über alle Aufträge/Jahrzehnte, **keine Umlaute/Sonderzeichen**; Dateiname in KEK muss mit physischem Dateinamen übereinstimmen.
- **WinCan VX:** Projektstandard VSA-2019 (nachträglich nicht änderbar), Medien Interlis-konform benennen.
- **WinCan DB3 (SQLite):** Tabellen `Observations` (Code, Distance, Time), `Sections` (Haltung, DN, Material, Länge), `Videos` (Pfade); Verknüpfung über `SectionId`.
- Untersuchungsgrund: Garantieabnahme · Neubauabnahme · Sanierungsabnahme · Zustandskontrolle · andere.

---

## 14. KI-Pipeline-Erkenntnisse (empirisch belegt — die Bauanleitung)

> Diese Erkenntnisse stammen aus systematischen A/B-Tests an echten Kanal-Frames. Sie
> verhindern teure Sackgassen beim Aufbau einer neuen Pipeline.

### 14.1 Was womit erkennbar ist — Befund-Natur entscheidet das Werkzeug
| Befund | Natur | Geeignetes Werkzeug | NICHT geeignet |
|---|---|---|---|
| **Bogen (BCC)** | **Geometrie** (Richtungsänderung, verschobener Fluchtpunkt) | Fluchtpunkt-Geometrie (dunkelster Bildbereich seitlich verschoben) | YOLO-cls, SAM, DINO, SAM 3.1 — alle scheitern (0/15 trotz 1139 Trainingsframes) |
| **Anschluss (BCA)** | **Textur** (runde Öffnung in Wand) | YOLO-Detection / Classifier (lernbar) | – |
| **Riss/Wurzel/Ablagerung** | Textur | YOLO-Detection + SAM (Maske) | – |
| **Rohranfang/-ende (BCD/BCE)** | Position + Bild | Meterstand-Logik + Classifier | – |

**Kernregel:** *Geometrische* Befunde (Bogen, Knick-Richtung) sind **nicht** klassifizier- oder
segmentierbar — nur geometrisch. *Textur*-Befunde sind klassifizier-/segmentierbar.

### 14.2 Modellwahl ist NICHT der Hebel (3× belegt)
- **Qwen-VL** (3-VL-8b vs 2.5-VL): beide treffen VSA-Codes ohne Training kaum.
- **SAM 3.1** vs SAM 1 (vit_h): SAM 3.1 **schlechter** auf Kanal-Bildern (4/6 Frames keine Maske).
- **YOLO-cls v11** (mit BCC/BCA + 1139 Bogen-Frames): Bögen 0/15, am Gate abgelehnt.
- **Der Hebel ist: Trainingsdaten + sauberes/vollständiges Eval-Set**, nicht die Architektur.

### 14.3 Aktuelle Modell-Versionen (Stand-Referenz)
| Komponente | Modell | Bemerkung |
|---|---|---|
| Detection-YOLO | **yolo26m** (TensorRT-Engine), 10 Schadensklassen | neueste YOLO-Generation, kein Upgrade nötig |
| Klassifikator-YOLO | **YOLOv8-nano** (`vsa_cls_v5_nocrop`, imgsz 1024, letterbox) | klein/älter — Spielraum, aber nur mit besseren Daten |
| Grounding DINO | **Swin-B geladen + vom Loader bevorzugt** (`grounding_dino_swinb`), Fallback Swin-T OGC (`grounding_dino_1.5`) | Stresstest 2026-06-20 bestanden: 1000 Frames, 0 Timeouts/Fehler, Forward ~107 ms median, VRAM-Peak ~21,3 GB ≪ 29 GB (s. `docs/benchmarks/2026-06-20-dino-swinb-stresstest.md`); Ordnername „1.5“ historisch irrefuehrend |
| SAM | **SAM 2.1** (`sam2.1_hiera_large.pt` unter `models/sam2.1`, via `SAM2ImagePredictor`) | produktiver Segmenter; SAM-1 `vit_h` entfernt; SAM 3 nur deaktivierte Option `sam3_weights_path`, alte `models/sam3`-Ablage entfernt |
| Qwen-VL | qwen3-vl:8b-q8 (Ollama) | Text/Bild, nur Hilfssignal |

### 14.4 Eval-Set-Falle (kritisch für Modell-Bewertung)
- Das 57er-clean-Eval enthält **keine BCC/BCA-Frames**. Folge: Modelle, die diese Klassen
  hinzufügen, werden **systematisch abgelehnt** — nur ihr Nachteil (mehr Klassen) ist messbar,
  nie ihr Gewinn. **Regel: Das Eval-Set muss alle Codes enthalten, die man messen/verbessern will.**
- LEER-Schwäche (37%) ist **teils ein Label-Artefakt** (Frames mit sichtbarer Naht/Überbelichtung/
  Tunnelblick als „kein_schaden" gelabelt). Mehr LEER-Training hilft nicht — erst Eval bereinigen.

### 14.5 Empfohlene Pipeline-Reihenfolge (Multi-Model) — Empfehlung, NICHT 1:1 = Ist-Code
```
1. Frame-Quality-Gate (schwarz/überbelichtet/strukturlos → verwerfen)
2. YOLO-cls (Grundgerüst BCD/BCE + Hauptcode-Vorschlag, schnell ~35ms)
3. Fluchtpunkt-Geometrie (Bogen-Veto) — im HEAD per Default DEAKTIVIERT (`bend_geometry_enabled=false`)
4. YOLO-Detection (Schadens-Boxen)
5. Grounding DINO (offene Vokabular-Detection; im Ist-Code läuft DINO nach JEDEM relevanten YOLO-Frame weiter, übersprungen nur bei YOLO `!IsRelevant` — NICHT „nur wenn YOLO unsicher")
6. SAM (Box → pixelgenaue Maske → Quantifizierung Höhe/Breite/Querschnitt/Uhrlage)
7. Qwen-VL (optional, Textbeschreibung — NUR Hilfssignal, nicht für Codes)
8. C#: VSA-Code-Mapping + Dedup + QualityGate (Green/Yellow/Red)
```

### 14.6 JSON-Schema-Empfehlung für KI-Output (strict)
```json
{
  "has_damage": true,
  "main_code": "BAB",          // gültiger VSA-Hauptcode aus dem Katalog
  "full_code": "BABBA",        // mit Char1/Char2 wenn bestimmbar
  "q1": 3.5,                   // Quantifizierung 1 (Einheit gemäß §6)
  "q2": null,                  // Quantifizierung 2 (nur BCA)
  "clock_from": 10, "clock_to": 2,  // Uhrlage (Streckenschaden)
  "severity": 4,              // 1-5 → EZ = 5 - severity
  "is_strecken": false,       // Punkt- vs Streckenschaden
  "meter": 12.30
}
```
**Regel:** Freitext-Codes verbieten. `main_code` muss gegen den Katalog (`isSelectable=true`)
validiert werden. Bei `has_damage=false` → kein Code (LEER).

---

## 15. Anti-Pattern (was NICHT zu tun ist)

- ❌ Bögen über Klassifikation/Segmentierung lösen wollen (Geometrie nutzen).
- ❌ Neueres/größeres Allzweck-Modell als Lösung erwarten (Daten sind der Hebel).
- ❌ LEER blind hochsampeln (verschlechtert; Eval-Labels prüfen).
- ❌ Modelle gegen ein Eval-Set messen, das die Zielcodes nicht enthält.
- ❌ Aufgewertete/halluzinierte Bilder (KI-Superresolution) als Befund/Training verwenden — forensisch unzulässig.
- ❌ Das eingefrorene Eval-Set eigenmächtig ändern (Versionssprung + Warden nötig).

---

*Quellen: VSA-KEK 2020 Katalog-Manifest (680 Codes), VSA-Merkblatt Schadencodierung 2019
(EN 13508-2), VSA-Richtlinie Zustandsbeurteilung 2023 (Anhang C), Vorgaben Kanalfernsehaufnahmen
Abwasser Uri V1.1. Pipeline-Erkenntnisse aus A/B-Tests SewerStudio Juni 2026.*


---

# TEIL C · NACHBAU-REIHENFOLGE, INVARIANTEN & ABNAHME

## Empfohlene Bau-Reihenfolge

1. **Fundament:** Solution mit 4 Schichten anlegen (`Domain`, `Application`, `Infrastructure`, `UI`),
   `Directory.Build.props` (Nullable, ImplicitUsings, LockFiles), `global.json` (.NET 10).
2. **Domain-Modell:** `HaltungRecord`/`SchachtRecord`/`Project`, `FieldCatalog` (34 Felder),
   `ProtocolDocument`/`ProtocolEntry`, VSA-Records, `Result<T>`/`ImportStats` (Teil A §2).
3. **VSA-Katalog:** `vsa_kek_2020_catalog_manifest.json` + `ICodeCatalogProvider` (Manifest read-only,
   Composite mit WinCan-Fallback). Ohne gültigen Katalog kein Code-Mapping (Teil A §2, Teil B §0/§2/§4).
4. **Fachlogik (testbar, ohne UI):** `VsaCodeResolver`, `CodingDedupPolicy`, `MetrierungProximityEvaluator`,
   `VsaEvaluationService` (EZ/ZN/DZ), `QualityGateService` (Teil A §3, Teil B §6/§11).
5. **Import/Export:** Import-Services (PDF/XTF/WinCan/IBAK/KINS) mit `Result<ImportStats>`,
   Excel/PDF-Export (Teil A §9).
6. **Sidecar:** FastAPI mit `/health`, `/warmup`, `/detect/yolo`, `/classify/yolo`, `/detect/dino`,
   `/segment/sam`, `/training/export-yolo`; `gpu_manager`, Token-/Host-Gate, cu128-Stack (Teil A §5).
7. **C#-Pipeline:** `VisionPipelineClient`, `MultiModelAnalysisService`, `SingleFrameMultiModelService`,
   `VideoFullAnalysisService` (Fallback), Dedup/Quantifizierung (Teil A §4).
8. **Ollama-Integration:** `OllamaClient` (striktes JSON-`format`), `EnhancedVisionAnalysisService`,
   `GpuModelSelector`, `AiStartupService`-Warmup (Teil A §6).
9. **KnowledgeBase & Self-Training:** SQLite-KB, Embeddings, Retrieval, `TrainingSamplesStore`,
   Review/Gold-Fund, Eval-Kontaminationsschutz, Eval-Set-Freeze (Teil A §7).
10. **UI:** `ServiceProvider`-Composition-Root, Shell/MainWindow, Seiten (MVVM), `PlayerWindow`-Codiermodus,
    SAM-Masken-Overlay, Theming (Teil A §8).

## Harte Invarianten (must-not-break)

Diese Regeln sind über alle Teilsysteme verstreut und dürfen NIE verletzt werden:

- **Thin-AI:** keine Fachlogik im Sidecar oder im LLM-Prompt; C# entscheidet.
- **Geteilter `HttpClient`:** nie `HttpClient.BaseAddress` setzen — immer absolute URIs bauen.
- **Ehrlichkeit:** `degraded=true` (DINO/SAM) und leere Detektion ≠ „sauberes Rohr", sondern Review-Signal.
- **COCO-Fallback** (falsche YOLO-Gewichte) sichtbar warnen, nie still durchlaufen.
- **OSD-Meter aus Qwen** nur bei plausiblem Wert (0–500 m) UND guter Bildqualität übernehmen.
- **VSA-Codes** immer gegen den Katalog validieren (`isSelectable && !isObservedExtension`); Freitext-Codes verboten.
- **EZ-Skala ist invertiert** (0 = schlecht, 4 = gut); Gesamt-ZN = `min(ZN_D, ZN_S, ZN_B)`; DZ niedrig = dringend.
- **Steuer-/Bestandscodes** (BCD/BCE/BCA/BCC/BDB/…) erzeugen KEINE Zustandsnote.
- **BCD nur am Rohranfang, BCE nur am Rohrende** (Negativ-Gate + Plausibilität gegen Fluchtpunkt/OSD-Fehler).
- **QualityGate-Green** verlangt mindestens 2 vorhandene Evidenzsignale (Ehrlichkeits-Deckel).
- **Eval-Set ist heilig:** kein Eval-Frame (per Hash ODER Haltung) darf je in KB/Retrieval/Training/Export gelangen.
- **KB-Rebuild:** ein Ollama-Ausfall darf eine bestehende KB nie löschen (50-%-Embedding-Gate).
- **Daten nie verlieren:** atomare temp-Writes + rotierende Backups + Korruptions-Recovery im `TrainingSamplesStore`.
- **Sidecar-Start** zwingend mit dem venv-Python (sonst läuft der Dienst ohne torch/ultralytics und lädt keine Modelle).
- **CUDA cu128** (RTX 50xx, `sm_120`) — niemals auf cu121 zurückfallen.

## Abnahme-Checkliste

- [ ] `dotnet build AuswertungPro.sln` grün (Windows, .NET 10 SDK).
- [ ] `dotnet test AuswertungPro.sln` grün (xUnit; Fokus: Recommendation-, QualityGate-, VSA-Logik).
- [ ] Sidecar `GET /health` mit Header `X-Sidecar-Token` liefert `status:"ok"` + geladene Modelle
      (401 heißt „Token fehlt", NICHT „Sidecar aus").
- [ ] App-Start fährt KI hoch: Ollama erreichbar + Modelle resident (`/api/ps`), Sidecar `/warmup` ok.
- [ ] Multi-Model-Pipeline läuft über ein Testvideo: Befunde mit VSA-Code, Meter, Uhrlage, QualityGate-Farbe.
- [ ] Eval-Set-Freeze verifiziert (`_manifest.json` `frozen:true`, Hashes stimmen); Benchmark läuft gegen 57er-clean.
- [ ] Import eines WinCan-DB3 + eines PDF-Protokolls füllt `HaltungRecord` + `ProtocolDocument`.
- [ ] Excel- und PDF-Export erzeugen gültige Dateien.

## Bekannte Doku-/Code-Diskrepanzen (beim Nachbau dem CODE folgen)

| Thema | Ältere Doku/Notiz sagt | Realer HEAD-Code sagt |
|---|---|---|
| Ziel-Framework | „.NET 8+" (CLAUDE.md) | **.NET 10** (`global.json`, alle `.csproj`) |
| SAM-Variante | SAM-1 `vit_h` unter `models/sam3` | **SAM 2.1** (`sam2.1_hiera_large.pt`, `models/sam2.1`, `SAM2ImagePredictor`); `vit_h` entfernt |
| Grounding DINO | Swin-T OGC („1.5") | **Swin-B vom Loader bevorzugt** (`grounding_dino_swinb`), Fallback Swin-T OGC — Stresstest 2026-06-20 bestanden (1000 Frames, 0 Timeouts) |
| `models/sam3` | „SAM 3" / produktiv | **Ablage entfernt** (`.gitkeep` geloescht); SAM 3 nur deaktivierte Option `sam3_weights_path`, kein Wrapper/Route |
| Bogen-Geometrie | aktiver Pipeline-Schritt | im Code per Default **deaktiviert** (`bend_geometry_enabled=false`) |
| 8B→32B-Eskalation | teils als Laufzeit-Feature beschrieben | existiert nicht — statische Wahl nach VRAM |

> Merksatz: **Bei Konflikt gewinnt der Code** (dieser Prompt wurde direkt daraus rekonstruiert),
> bei fachlichen Fragen das VSA-Regelwerk in Teil B.

---

# TEIL D · OFFENE PUNKTE & BACKLOG (Stand 2026-06-20)

> Quelle: zwei externe Architektur-Reviews (2026-06-20), hier als Backlog mit Priorität und
> Begründung festgehalten — inklusive der bewusst **nicht** übernommenen Vorschläge, damit sie
> nicht erneut aufkommen. Leitlinie: **messen statt umbauen, eval-getrieben, kein Enterprise-Overhead.**

## D1 · Jetzt — klein, hoher Wert

- ~~**DINO Swin-B Stresstest**~~ — **✓ ERLEDIGT 2026-06-20.** 1000 Frames gegen Swin-B: **0 Timeouts/Fehler**,
  Forward ~107 ms median, **VRAM-Peak ~21,3 GB ≪ 29-GB-Budget** (Δ während Inferenz +26 MiB). → **Swin-B
  behalten**, kein Fallback nötig. Detail: `docs/benchmarks/2026-06-20-dino-swinb-stresstest.md`.
  Nebenbefund (Backlog, nicht Swin-B): ~2 s Wall-Clock/Call durch Decode/Transform/Text-Encoding, backbone-unabhängig.
- **Pre-Load-VRAM-Budget-Check im `gpu_manager`** — `evict_lru()` existiert bereits, wird aber nur
  on-demand aufgerufen. Vor dem Laden eines **neuen** Slots Budget prüfen und ggf. den am längsten
  ungenutzten Slot präventiv entladen, statt auf den teuren 503-OOM-Fallback zu warten. Kleiner,
  lokaler Eingriff. Grenze: löst **keinen** Fremdprozess-OOM (z. B. wenn Ollama ein Extra-Modell lädt).

## D2 · Später — eval-gated (erst messen, dann bauen)

- **BoT-SORT `track_id` aus dem Sidecar** — leichtgewichtiges Tracking (Ultralytics `model.track`),
  das eine konsistente `track_id` an C# liefert; bricht Thin-AI **nicht** (track_id = Rohsignal, C#
  behält die Fachlogik). **Vorbedingung:** eine Messung muss belegen, dass ID-Switches
  (Kamera-Wackler/Stillstand) real Befunde verdoppeln. Solange die Kamera monoton vorwärtsfährt und
  `TemporalFindingDeduplicator` + `TemporalCodeVotingService` + Meter-Gap-Merge greifen, ist der
  Nutzen unbelegt. Bleibt „geplant", **nicht** Ist-Architektur.
- **Schlanker CI** — Windows-`dotnet build` + Unit-Tests bei Push (GitHub Actions). GPU-Sidecar-Tests
  bleiben lokal (`pytest -m gpu`); **kein** GPU-Runner.
- **Retry-Konstanten bündeln** — die wenigen Retry-Stellen (`VisionPipelineClient` 1×503/Transport,
  Ollama best-effort) an einer Stelle als Konstanten zusammenfassen. **Kein** CircuitBreaker/Policy-Service.

## D3 · Bewusst NICHT übernommen (mit Grund — bitte nicht erneut vorschlagen)

| Vorschlag | Warum nicht |
|---|---|
| MS `Extensions.DependencyInjection` statt DIY-Container | großes Composition-Root-Refactoring, null Nutzer-Nutzen; „kein grosses Refactoring ohne Diskussion". Per-Analyse-Objekte werden bereits ad hoc frisch gebaut. |
| Adaptives QualityGate (gelernte Gewichte laden) | **ADR-008**: bewusst zurückgestellt; nur mit Eval-Beweis, sonst blindes Tuning. `WeightLearningService` lernt bereits, das Anwenden ist absichtlich nicht verdrahtet. |
| `VsaCodeTree` durch Manifest ersetzen | bewusst getrennt: Manifest = Laufzeit-SSoT (tolerant), `VsaCodeTree` = strenger Trainingslabel-Eintrittsfilter. Zusammenlegen schwächt den Trainingsschutz. |
| Docker / Kubernetes | **WPF läuft nicht in Containern**; K8s für 1 Nutzer / 1 Maschine = reiner Overhead. |
| Feature-Flags mit `NotImplementedException` für nicht-existente Features | sinnlose Zeremonie; „geplant/nicht implementiert" steht bereits klar im Dokument. |
| async-Umstellung der Sidecar-Routen | kein Nebenläufigkeits-Gewinn (ein Analyse-Stream; Decode läuft schon außerhalb des `predict`-Locks); naiv async + blockierendes `torch` verschlechtert den Event-Loop. |
| Unified `PipelineResult<T>` | Refactor mit marginalem Nutzen; die drei Result-Typen bedienen verschiedene Call-Sites. |

## D4 · War in den Reviews als „Problem" gelistet, ist aber schon vorhanden

- **Sidecar-Status/Ampel:** `PipelineHealthMonitor` (grün/gelb/grau) + `CheckHealthDetailedAsync`
  (offline/401/ok); `MultiModel` wirft bei Ausfall, `Auto` fällt nur mit sichtbarer `fallbackReason` zurück.
- **Sample-Historie/Audit:** `TrainingSamplesStore` (atomare Writes + rotierende Backups + Recovery),
  KB-`Versions`-Tabelle, `ReviewApprovalService` (Approve/Reject/Corrected + Confirmed-Felder).
- **Weight-Learning:** `WeightLearningService` + `ValidationLogger` lernen alle 25 Entscheidungen
  (Anwenden bewusst offen, s. D3).
- **PlayerWindow-Split:** bereits Partial-Classes + `CodingSessionViewModel` + `SamMaskRenderer`-Service.
- **Sidecar-I/O:** Decode/Quality-Gate/Preprocessing laufen bereits **vor** dem `predict`-Lock.

---

*Erzeugt am 2026-06-20. **Teil A** aus dem realen HEAD-Code rekonstruiert (9 parallele Code-Lese-Agenten über alle Teilsysteme),
plus dem bestehenden VSA-Regelwerk. Quellen: VSA-KEK 2020 Katalog-Manifest (680 Codes), VSA-Merkblatt
Schadencodierung 2019 (EN 13508-2), VSA-Richtlinie Zustandsbeurteilung 2023, Vorgaben Kanalfernsehaufnahmen
Abwasser Uri V1.1.*
