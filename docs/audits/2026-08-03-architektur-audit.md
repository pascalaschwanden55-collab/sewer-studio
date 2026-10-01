# Architektur-Audit SewerStudio — 2026-08-03

Vollständiges Architektur-Audit gegen den **Arbeitsbaum** (nicht nur HEAD).
Methodik: drei unabhängige Teilanalysen (.NET-Schichten/Guards, Sidecar/QGIS/Tools,
Doku-Drift/Tests), jede Kennzahl zusätzlich stichprobenartig direkt verifiziert,
plus kompletter Build- und Testlauf.

## 1. Kurzurteil

**Architektur: GESUND — keine harten Schichtverletzungen, Sicherungsnetz wirkt messbar.**

Die verbindlichen Regeln aus CLAUDE.md/AGENTS.md (Schichtrichtung, UI/Ai-Freeze,
UseCases-Regel, Boundary-Ratchet, Größen-Ratchet) werden aktuell **alle eingehalten**
und sind durch ein dichtes Netz aus Architektur-Tests abgesichert. Build und alle
Teststrecken sind grün.

Die wesentlichen Risiken sind keine Regelbrüche, sondern:

1. **Betrieblich offen:** Der Produktiv-Detektor (yolo26m) ist seit 2026-07-25 per
   `model_qualification.json` gesperrt (`qualified=false`, BBox-Kollaps) — by design
   fail-closed, aber die KI-Pipeline läuft seither ohne YOLO-Gate im Degraded-Modus.
2. **Zwei Alt-Funde aus dem Audit 2026-07-25 sind weiterhin unverändert offen**
   (F6 Klassifikator-Health, F10 Wissen-Import ohne Grenzen).
3. **Strukturelle Dauerschuld:** PlayerWindow (3.939 Zeilen), HoldingFolderDistributor
   (2.980 Zeilen), 178 Property-Composition-Root, 50+ eingefrorene
   Kompatibilitätsfassaden — allesamt geduldet und rückläufig, aber ohne Abbaupfad.
4. **Doku-Drift:** Der Skill `sewer-architektur` (Stand 2026-07-25) hinkt dem Code
   hinterher; CLAUDE.md enthält duplizierte Absätze; zwei Zahlen sind veraltet.

## 2. Ist-Schnappschuss

| Merkmal | Stand |
|---|---|
| Auditzeitpunkt | 2026-08-03 |
| Branch | `feature/eval-pruefsatz-review` |
| HEAD | `41f94ebd9` (2026-07-27, „feat(eval): menschliche schadensreview auswerten") |
| Arbeitsbaum | **241 uncommittete Einträge** — Audit gilt für den Dirty-Tree, nicht für HEAD |
| Build `AuswertungPro.sln -c Release` | **0 Fehler, 0 Warnungen** |
| .NET-Tests gesamt | **11'279 bestanden, 0 Fehler, 15 übersprungen** |
| Sidecar-Tests (pytest, nicht-gpu) | **222 bestanden, 2 übersprungen, 2 deselektiert** |
| QGIS-Tests (unittest) | **5 bestanden** |

Testdetails: Infrastructure 3'479 (13 Skip), Pipeline 2'157 (1 Skip), UI 5'581
(1 Skip), ProjectModernizer 62 (0 Skip). Der erste QGIS-Lauf scheiterte an einem
kaputten `python` auf PATH (fremdes Hermes-venv, Permission denied) — mit dem
Sidecar-venv laufen die 5 Tests sauber; Umgebungsproblem, kein Codeproblem.
Nebenbefund: `sidecar/.pytest_cache` ist für pytest nicht beschreibbar
(Permission denied, PytestCacheWarning) — Rechteproblem im Ordner, Testlauf selbst grün.

## 3. Schichtarchitektur (.NET) — verifiziert

```
Domain (38 Dateien / 2'920 Z.)     ← null ProjectReference, null NuGets
Application (453 / 37'534)         ← nur → Domain
Infrastructure (533 / 96'878)      ← → Domain + Application (9 NuGets)
UI (1.434 / 128'301)               ← → alle drei (12 NuGets)
```

- **Schichtrichtung exakt wie dokumentiert** (csproj-Verweise direkt gelesen).
- **Domain ist sauber:** Grep nach `System.IO`, `HttpClient`, `Microsoft.Data.Sqlite`,
  `File.`, `Directory.` sowie usings auf höhere Schichten: **0 Treffer**.
- Größenverhältnis UI : Infrastructure : Application : Domain ≈ 44 : 33 : 13 : 1
  (Codezeilen) — die UI trägt mit Abstand die meiste Masse, trotz dünner
  ViewModels-Regel. Das ist der strukturelle Hauptdruckpunkt des Systems.

### Composition Root (ServiceProvider)

- **5 Partial-Dateien**, zusammen **178 public Properties** (165 Auto-Properties
  `ServiceProvider.cs` + `FullBackup`, 13 expression-bodied in
  `ServiceProvider.TrainingYoloExport.cs`/`ServiceProvider.cs`). Eigene Zählung,
  korrigiert die Skill-Angabe „~145".
- `ServiceProviderRegistrationMap`: **132 Vertragstypen** ohne Doppelkeys
  (Doku sagt 130 → veraltet). → **~46 Properties sind nicht per `GetService`
  auflösbar** (u. a. `Settings`, `Dialogs`, `PhotoImport`, `KnowledgeBackup`).
- **Wichtige Einordnung:** `GetService(Type)` wird im Produktivcode **nirgends**
  aufgerufen (Grep: nur die Definition selbst). Die Map ist damit keine
  Laufzeit-Auflösung, sondern ein **Konsistenz-Register für die 90 DependencyTests**.
  Die Lücke ist dadurch kein Laufzeitrisiko, aber eine undokumentierte
  Willkür-Grenze: neue Dienste landen automatisch in der Map, ältere nicht.
- Der Konstruktor (`ServiceProvider.cs:323-753`) verdrahtet ~150 Dienste sequenziell,
  inkl. statischer Altfassaden (`Use*`/`Configure*`) und zweier direkt erzeugter
  `HttpClient`-Instanzen — die einzige nennenswerte Infrastructure-Erzeugung in der UI.

### UI/Ai-Freeze und UseCases

- `UI/Ai`: **599 Dateien ↔ 599 Allowlist-Einträge** (4 Kommentarzeilen abgezogen,
  keine Duplikate, kein Diff) — Freeze exakt synchron, Testlauf bestätigt grün.
- `Application/UseCases/`: 9 Dateien, Request/Result-Muster eingehalten, neueste
  Datei 2026-08-02 (`TrainingStudioBoxAnalysisUseCase`) — die Regel wird gelebt.
  Kleinigkeit: `CodingModeBackgroundServicesWorkflow.cs` liegt ohne
  Feature-Unterordner direkt in `UseCases/`.

## 4. Architektur-Sicherungsnetz — Bestandsaufnahme

| Guard | Regel | Ist-Stand |
|---|---|---|
| `UiAiFreezeArchitectureTests` | Allowlist-Freeze `UI/Ai` | 599 ↔ 599 ✓ |
| `ViewModelInfrastructureBoundaryTests` | kein Store-`new` in ViewModels/Views/DataPage | **0** direkte Instanziierungen; 13 Kompat-Fassaden eingefroren ✓ |
| `MaintainabilityFitnessTests` | ≤ 1.000 Z./Datei, ≤ 2.000 Z./Partial-Typ | max. Einzeldatei 997 Z. ✓; Baselines unterschritten ✓ |
| 90 `*DependencyTests` (nur UI.Tests) | Felder tragen Application-Verträge, ServiceProvider liefert dieselbe Instanz | grün ✓ |
| ~120 `*ArchitectureTests`/`*GuardTests` (UI.Tests) | div. Grenzen (PlayerWindow-Slices, QgisBridge, Backups …) | grün ✓ |
| `PipelineTestProjectArchitectureTests` u. a. | Pipeline-Tests ohne WPF-Referenz u. v. m. | grün ✓ |

**Nachweis, dass die Ratchets wirken:** Die beiden God-Class-Baselines werden nicht
nur eingehalten, sondern unterschritten — `PlayerWindow` 3'939 Zeilen (Baseline
4'263), `HoldingFolderDistributor` 2'980 (Baseline 3'064). Die Schuld schrumpft
tatsächlich. Static-DI-Bypass exakt auf 2 Marker eingefroren (`DialogHost.Current`,
`VsaCodeResolver.CurrentCatalog`).

## 5. Größenmetriken und Hotspots

Top-Einzeldateien (Limit 1.000 Zeilen, Puffer < 1 %):

| Zeilen | Datei |
|---:|---|
| 997 | `UI/ViewModels/Windows/TrainingCenterViewModel.cs` |
| 994 | `UI/ViewModels/TrainingStudioViewModel.cs` |
| 993 | `UI/Views/Windows/VsaCodeExplorerWindow.xaml.cs` |
| 982 | `Infrastructure/Ai/Pipeline/MultiModelAnalysisService.cs` |
| 976 | `UI/ViewModels/Pages/SanierungsMatrixPageViewModel.cs` |
| 972 | `Infrastructure/Import/WinCan/WinCanDbImportService.cs` |
| 922 | `UI/ServiceProvider.cs` (wächst strukturell mit jedem neuen Dienst) |

- **Drei Dateien haben < 1 % Puffer zum Limit** — jede kleine Erweiterung dort
  macht den Fitness-Test rot. Das erzwingt die gewollte Disziplin, ist aber als
  Dauerzustand unbequem: frühzeitig Slices abspalten statt am Limit balancieren.
- Partial-Typen: `PlayerWindow` 3'939 Z. in 72 Dateien, `HoldingFolderDistributor`
  2'980 Z. in 6 Dateien — beide geduldet, rückläufig.
- Statische Dauerschichten: `VsaCodeResolver` (komplette static class),
  50 als unveränderlich eingefrorene Compatibility-Fassaden, 13 Ratchet-Einträge.
  Alles per Test gedeckelt — aber es gibt **keinen dokumentierten Abbaupfad**.

## 6. Sidecar-Architektur (Python/FastAPI)

Paket `sidecar/sidecar/`: 27 Dateien / 5'052 Zeilen; 36 Testdateien, 222 Tests
(grün, Marker trennen gpu/e2e sauber, Default-Lauf hardwarefrei).

- **GPU-Verwaltung vollständig und wie dokumentiert:** Busy-Leases mit
  Besitzer-UUIDs, atomare LRU-Eviction unter `_global_lock`, In-flight-VRAM-
  Reservierungen, `InferenceWatchdog` mit hartem `os._exit(42)` (180 s),
  `MODEL_VRAM_ESTIMATE_GB` (YOLO 3.0/DINO 4.0/SAM 6.0), Reserve 12 GB, Budget 29 GB.
- **Fail-closed-Qualifikation:** `model_qualification.json` bindet pt/engine/onnx
  getrennt per SHA-256; aktuell `qualified=false` → `/health` meldet `degraded`
  mit maschinenlesbarem `status_detail`, `/detect/yolo` führt das Modell nicht aus
  und antwortet strukturiert. DINO/SAM laufen weiter. Korrekt gehärtet.
- **Sicherheit:** verpflichtendes Token (`hmac.compare_digest`), DNS-Rebinding-
  Schutz, Loopback-only. Kein Stacktrace nach außen.
- **Modellstand passt zur Doku:** SAM 2.1 (`models/sam2.1`, SAM 2 hart abgelehnt,
  kein SAM-1-Rest), `_pil_rgb_to_ultralytics_bgr` an beiden Inferenzpfaden,
  `bend_geometry_enabled=false` (leichtes `bend_veto_enabled=true` im cls-Pfad).
- **Auffälligkeiten:**
  - `yolo_wrapper.py` (815 Z.) trägt drei Verantwortlichkeiten (Detektor,
    Klassifikator, Quality-Gate) — God-Class-Ansatzpunkt, ohne Pendant zum
    1.000-Zeilen-Ratchet der C#-Seite. Python-Seite hat keinen Fitness-Guard.
  - Bewusste Doppelpflege: `bend_geometry.py` ↔ `VanishingPointBendDetector.cs`
    (1:1-Portierung, Docstring verweist korrekt).
  - Altlasten: lose `yolo11m.engine` im sidecar-Root (Code-Fallback heißt
    `yolo11m.pt`), 4 requirements-Stände, `spike_sam3_concept.py` im Paket-Root,
    5 alte `.engine`-Backups (~300 MB).
  - TODO/FIXME im Sidecar: **0 Treffer** — sauber.

## 7. QGIS, Tools, Inselsysteme

- **QGIS-Bridge** (`integrations/qgis/`): sauberes, read-only Loopback-Plugin
  (nur PyQGIS + Stdlib), 5 vertragsbasierte Tests laufen ohne QGIS — grün.
- **Tools:** **41 csproj, alle 41 in `AuswertungPro.sln` eingetragen** (Doku stimmt).
  Dadurch bricht jeder Strukturbruch im normalen Release-Build — gute Absicherung.
  - `tools/` belegt **8,8 GB auf Disk** (self-contained bin/-Outputs à ~520 MB).
    `.gitignore` deckt `bin/`/`obj/` ab, **0 Dateien davon sind getrackt** — kein
    Repo-Problem, aber ein Platten-/Aufräum-Thema.
  - Toter Verweis: `tools/SewerStudioMcpServer/SewerStudioToolRegistry.cs:29`
    beschreibt ein Tool mit der entfernten Klasse `PdfProtocolTableParser`.
- **Inselsysteme:** `Amtsblatt-Monitor/` (93 KB, Python-Scanner, null Referenzen)
  und `_legacy/` (714 KB, historische PS-Vorgänger) sind unreferenzierte Archive
  im Repo-Root. Kein Build-Risiko; dokumentarisch einzuordnen oder auszulagern.

## 8. Doku-Drift

| Behauptung | Fundstelle | Ist-Zustand |
|---|---|---|
| „~145 public Properties" | Skill `sewer-architektur` | **178** (eigene Zählung) |
| „130 Vertragstypen" | CLAUDE.md:311, CODEBASE-KARTE.md | **132** `[typeof(...)]`-Einträge |
| „~10'450 Tests, 4 skip" | Skill | **11'279 .NET-Tests, 15 Skips** (heute gelaufen) |
| `PdfProtocolTableParser` „0 Treffer" | CODEBASE-KARTE.md §14 | Klasse entfernt ✓, Name lebt im MCP-Tool-String weiter |
| `PhotoMeasurementGeometryService` = „UI-Helfer" | CODEBASE-KARTE.md §12 | liegt in `Application/Ai/` |
| Skill-Stand 2026-07-25 (Commit `2e92a23bb`, Branch gis-karte) | SKILL.md:18 | Branch/HEAD weiter; `docs/CODEBASE-KARTE.md` (2026-08-02) ist die frischere Quelle |
| CLAUDE.md-Absätze einmalig | — | **mehrere wörtlich duplizierte Absätze** (u. a. `TrainingPdfProtocolFindingParser`, `PhotoAnnotationBatchSaveUseCase` je 2×, weitere 2 duplizierte Langzeilen per Skript gefunden) |
| ADR-Nummerierung | `docs/ADR-007-*` | **ADR-007 doppelt vergeben**; ADR-001…005 fehlen |
| F6/F10 „nicht behoben" | Audit 2026-07-25 | **weiterhin offen** (s. §9) |

Korrektur früherer Doku-Fehler bestätigt: ByteTrack/OC-SORT, `DetectionAggregator`,
`InferenceOrchestratorService`, `KbDeduplicationService`, `FewShotExampleStore`
existieren nach wie vor **nicht** im Code (Negativliste stimmt; FewShot-Fehlen ist
per Guard-Test abgesichert).

## 9. Offene Alt-Funde (Stichprobe, aktueller Code)

Aus `docs/audits/2026-07-25-kimi-ergebnis-audit.md`:

- **F6 Klassifikator-Health — offen, inzwischen dokumentierte Designentscheidung:**
  `sidecar/sidecar/models/yolo_wrapper.py:745-765` prüft im Lazy-Zustand bewusst
  kein Gewicht/SHA (Docstring: „kein SHA-Hashing pro Health-Poll"); C#-Warnung nur
  bei `loaded=false` (`VisionPipelineDtos.cs:41-46`). Operativ blind bei physisch
  fehlendem Gewicht.
- **F10 Wissen-Import — offen, alle drei Teilbefunde bestätigt:**
  Manifest ohne Dateihashes (`KnowledgeBackupService.cs:97-104`), Katalog mit
  Teacher-/training-/Legacy-Bäumen (`KnowledgeBackupFileCatalog.cs:60-119`),
  Import ohne Größen-/Anzahl-/Verhältnisgrenzen (`KnowledgeBackupService.cs:333-349`)
  — ZIP-Bomb-/Integritätsrisiko besteht zwei Integritätsaudits später weiter.

## 10. Risiken und Empfehlungen (priorisiert)

| # | Risiko | Schwere | Empfehlung |
|---|---|---|---|
| R1 | Produktiv-Detektor gesperrt (`qualified=false`), Pipeline läuft dauerhaft degraded | **hoch (betrieblich)** | Release-Weg für Nachfolgemodell zu Ende führen (Holdout/Negativsammlung laut CLAUDE.md) oder bewussten Dauerzustand dokumentieren |
| R2 | F10: Wissen-Import ohne Größen-/Integritätsgrenzen, Manifest ohne Hashes | **hoch (Sicherheit/Daten)** | Dateihashes ins Manifest, harte Caps (Anzahl/Größe/Verhältnis) in `CollectImportFiles`, Fokustests |
| R3 | F6: fehlendes Klassifikatorgewicht bleibt unsichtbar | mittel | Gewichts-Existenz (nicht SHA) beim ersten nicht-lazy Health-Poll oder bei Warmup prüfen |
| R4 | Drei Einzeldateien < 1 % unter dem 1.000-Zeilen-Limit; ServiceProvider.cs wächst monoton | mittel | TrainingCenterViewModel/TrainingStudioViewModel/VsaCodeExplorerWindow slicen; ServiceProvider-Partials nach Feature splitten |
| R5 | Kein dokumentierter Abbaupfad für 50+ Kompat-Fassaden, statische `VsaCodeResolver`, 2 God-Classes | mittel | Abbaupfad in `docs/WARTBARKEITS-SCHULDEN.md` festhalten (Existenz dort prüfen/ergänzen) |
| R6 | Python-Seite ohne Größen-/Fitness-Guard (`yolo_wrapper.py` 815 Z., 3 Zuständigkeiten) | mittel | Klassifikator/Quality-Gate in eigene Module ziehen; optional einfachen Zeilen-Ratchet in pytest |
| R7 | GetService-Map lückenhaft (46/178) und nur Test-Register; Grenze undokumentiert | niedrig | Entweder Regel dokumentieren („jeder neue Dienst in die Map") oder Map als reinen Test-Spiegel kennzeichnen |
| R8 | Skill `sewer-architektur` veraltet (Properties, Testzahlen, Branch) | niedrig | Skill gegen `docs/CODEBASE-KARTE.md` (2026-08-02) aktualisieren |
| R9 | 8,8 GB Build-Outputs unter `tools/`; ~300 MB engine_backups; Altlasten im sidecar-Root | niedrig | Aufräumskript/Rotation; `spike_sam3_concept.py` und lose `yolo11m.engine` einsortieren |
| R10 | CLAUDE.md duplizierte Absätze; ADR-007 doppelt; toter MCP-Tool-String | niedrig | Redaktioneller Fix; MCP-Beschreibung auf echten Parserweg korrigieren |
| R11 | Umgebung: `sidecar/.pytest_cache` nicht beschreibbar; `python` auf PATH kaputt | niedrig | Ordnerrechte reparieren; QGIS-Testanleitung auf konkreten Interpreter verweisen lassen |

## 11. Positiv bestätigt

- Schichtrichtung und Domain-Reinheit: exakt wie dokumentiert, null Verletzungen.
- UI/Ai-Freeze exakt synchron (599 ↔ 599); UseCases-Regel wird für neuen Code gelebt.
- Alle Ratchets wirken nachweislich: God-Class-Baselines unterschritten, null
  direkte Store-Instanziierungen in geschützten UI-Bereichen.
- 90 DependencyTests + ~120 Architektur-/Guard-Tests + Boundary-/Fitness-/Freeze-
  Tests bilden ein dichtes, aktuell vollständig grünes Sicherungsnetz.
- Sidecar: GPU-Lease/Eviction/Watchdog/VRAM-Zulassung vollständig implementiert,
  Qualifikation fail-closed mit SHA-Bindung pro Backend, Auth gehärtet.
- Alle 41 Tool-Projekte in der Solution — Strukturbrüche brechen den Release-Build.
- Build 0 Warnungen; 11'279 .NET-Tests + 222 Sidecar- + 5 QGIS-Tests grün.
- Negativliste der Doku (keine Tracking-/Aggregator-/FewShot-Klassen) stimmt;
  Entfernungen sind per Guard-Test gegen Wiederkehr gesichert.

---

*Erstellt durch dreifache Teilanalyse mit direkter Gegenprobe aller Kennzahlen sowie
vollständigem Build- und Testlauf (Befehle gemäß AGENTS.md). Keine Dateiänderungen
außer diesem Bericht.*
