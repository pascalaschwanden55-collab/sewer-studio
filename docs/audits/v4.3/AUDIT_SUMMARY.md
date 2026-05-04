# Audit-Konsens v4.3 — SewerStudio KI

**Datum:** 2026-05-04 (initial) · **Update:** 2026-05-04 nach Phase 0
**Quellen:** 3 KI-Audits (Gemini, Claude, Codex) + 1 konsolidierender Audit (`audit_konsolidiert_2026-05-04.md`, deutsche Codex-Re-Formulierung — **kein 4. Stimmgeber**)
**Branch:** feature/pdf-import-beobachtungen
**Status:** Sammlung & Auswertung · Phase 0 erledigt

> **Lesehilfe:** Konsens = mind. 2 von 3 Agenten haben das gleiche Problem genannt. Konsens-Punkte sind die wichtigsten — sie haben das höchste Gewicht für v4.3.

> **Phase-0-Erkenntnis 2026-05-04:** Der von Claude+Codex gemeldete "Build kaputt: 140 XAML-Fehler" war eine **Fehldiagnose**. Es gab null XAML-Fehler. Wurzel war ein File-Lock auf `*.g.cs` durch ein `AdditionalProperties`-Override in `tests/AuswertungPro.Next.Pipeline.Tests/Pipeline.Tests.csproj`, das MSBuild zwang, das UI-Projekt zweimal parallel zu bauen. Fix war 1 Zeile — siehe B1.

---

## Vergleichstabelle: Wer hat was gefunden?

| Befund | Gemini | Claude | Codex | Konsens |
|---|:---:|:---:|:---:|:---:|
| **KI lebt im UI-Projekt (statt Infrastructure)** | ✅ | ✅ | ✅ | **3/3** |
| **God-Classes: PlayerWindow / TrainingCenterVM zu gross** | ✅ | ✅ | ✅ | **3/3** |
| **App.Services-Locator statt DI** | ✅ | ✅ | (impliz.) | **2-3/3** |
| **Build kaputt: 140 XAML/Code-Behind-Fehler** ✅ erledigt 2026-05-04 | – | ✅ | ✅ | **2/3** (Fehldiagnose — Wurzel war File-Lock, siehe B1) |
| **Empty catch { } ohne Logging** | ✅ | ✅ | ✅ | **3/3** |
| **Blocking-Waits in async Code (Dispatcher.Invoke, .Result)** | ✅ | ✅ | ✅ | **3/3** |
| **Command Injection / unsichere Process.Start** | ✅ | (gefixt) | ✅ | **2/3** (teilweise gefixt) |
| **YOLO-Export Speicher-Bombe (Base64 in RAM)** | ✅ | – | – | 1/3 |
| **HttpClient-Lifecycle / kbHttp-Leak** | – | ✅ | ✅ | **2/3** |
| **SQLite-Schreib-Lock bremst Pipeline (Channel<T> nötig)** | ✅ | – | ✅ | **2/3** |
| **Embedding-Versionierung fehlt** | – | ✅ | (impliz.) | 1-2/3 |
| **FK-Constraints Embeddings → Samples fehlen** | – | ✅ | ✅ | **2/3** |
| **QualityGate liegt im UI statt Application** | – | ✅ | ✅ | **2/3** |
| **Trainingsdaten: 70% rot / Qualität vor Quantität** | ✅ | – | ✅ (RunId/Provenance) | **2/3** |
| **Sanierungs-Hardcoding statt YAML-Driven** | ✅ | ✅ | ✅ | **3/3** |
| **Hydraulik / Eigendevis raus / Slim-Down** | ✅ | ✅ | ✅ | **3/3** |
| **Repo-Ballast: Sidecar / Modelle / Caches** | – | ✅ (7 GB) | ✅ (22.9 GB) | **2/3** |
| **Redundante Services (3× FullScan, 3× FrameExtractor, 5× PDF-CLI)** | – | ✅ | ✅ | **2/3** |
| **UI: kein zentrales Design-System / Hex-Hardcodes** | ✅ | ✅ | ✅ | **3/3** |
| **UI: Anleitungstexte in Settings** | ✅ | – | – | 1/3 |
| **UI: zu dichtes Layout / "Cockpit-Optik"** | – | ✅ | ✅ | **2/3** |
| **UI: kein Produktlogo / App-Icon / kein PerMonitor-DPI** | – | ✅ | – | 1/3 |
| **Pipeline-Tests nicht von GPU/Langzeit getrennt** | – | – | ✅ | 1/3 |
| **Zwei SQLite-Pakete parallel** | – | – | ✅ | 1/3 |
| **Sprache D/E gemischt (Glossar fehlt)** | – | – | ✅ | 1/3 |
| **DamageClassesPromptFull deaktiviert** | – | ✅ | – | 1/3 |
| **51× new XxxWindow() in ViewModels (kein DialogService)** | – | ✅ | – | 1/3 |
| **15× ffmpeg ProcessStartInfo statt zentralem Runner** | – | ✅ | (impliz.) | 1-2/3 |

---

## A. Konsens-Findings (3/3 — höchste Priorität)

Diese 5 Punkte haben **alle drei** Audits unabhängig genannt — sie sind unbestreitbar die wichtigsten Baustellen für v4.3.

### A1. KI lebt im UI-Projekt
- **Claude:** 144 KI-Dateien in UI vs. 1 in Infrastructure
- **Codex:** 89.000 C# Zeilen UI vs. 1.700 Domain
- **Gemini:** "AuswertungPro.Next.UI.Ai.* in eigenes Projekt"

### A2. God-Classes (PlayerWindow / TrainingCenterVM)
- **Claude:** PlayerWindow.xaml.cs = 9856 Zeilen, gewachsen seit 23.04.
- **Codex:** PlayerWindow = 8581, TrainingCenterVM = 2628, CodingMode = 3459
- **Gemini:** "TrainingCenterViewModel.cs (fast 3000 Zeilen)"

### A3. Empty catch { } ohne Logging
- Alle drei: Fehler werden geschluckt, Debugging unmöglich

### A4. Blocking-Waits in async Code
- Alle drei: Dispatcher.Invoke, .Result, .GetAwaiter().GetResult(), Thread.Sleep

### A5. Slim-Down nötig (Hydraulik/Eigendevis/Feature-Creep)
- **Gemini:** "Hydraulik DWA-A 110 + Eigendevis raus"
- **Claude:** "7 GB + 25 Dateien sofort löschbar"
- **Codex:** "Produktmodus vs. Expertenmodus"

### A6. UI: kein zentrales Design-System
- Alle drei: Hex-Hardcodes überall, Inline-Styles, lokale Farben in jedem Window

### A7. Sanierungs-Wissen verteilt / Hardcode statt YAML
- Alle drei: RehabilitationRulesEngine.Procedures hartcoded, Knowledge-YAMLs nicht alleinige Quelle

---

## B. Starke Konsens-Findings (2/3)

### B1. Build war kaputt (File-Lock, KEINE XAML-Fehler) — ✅ erledigt 2026-05-04
**Claude + Codex** meldeten ~140 Fehler in DataPage.xaml.cs, ClockPickerControl.xaml.cs, SchaechtePage.xaml.cs.
**Tatsaechliche Wurzel:** `AdditionalProperties="BaseOutputPath=...bin\ProjectRefs\..."` in `tests/AuswertungPro.Next.Pipeline.Tests/Pipeline.Tests.csproj` zwang MSBuild, das UI-Projekt **zweimal parallel** zu bauen (einmal direkt, einmal als Dependency). Beide WPF-Markup-Compiler-Targets schrieben gleichzeitig in dasselbe `obj/`-Verzeichnis → `error MC1000` (File-Lock auf `*.g.cs`). Die 140 `CS2001`-Fehler waren reine Folgeschaeden, weil der Markup-Compiler abbrach, **bevor** er die `.g.cs`-Dateien generieren konnte.
**Fix:** 1 Zeile — `AdditionalProperties` aus dem `<ProjectReference>` entfernt. MSBuild dedupliziert jetzt zu einem einzigen UI-Build. `dotnet build AuswertungPro.sln` parallel: 0 Fehler, 2 Warnings, ~8 s.
**Lehre:** Beide Audits sahen den Symptom-Stack (CS2001), nicht die `error MC1000`-Wurzel. Vor Audit-Annahmen immer die **erste** Fehlerzeile pruefen, nicht die haeufigste.

### B2. App.Services-Locator statt DI
**Gemini + Claude:** 47 Treffer in 21 Dateien, 0× MS.Extensions.DependencyInjection

### B3. SQLite-Bottleneck (Channel<T> als Lösung)
**Gemini + Codex:** Globaler Schreib-Lock bremst KI-Threads aus

### B4. FK-Constraints + Embedding-Versionierung
**Claude + Codex:** Embeddings ohne FK auf Samples, kein Modell-Tag → Migration zerlegt KB

### B5. QualityGate liegt im UI statt Application/KI-Core
**Claude + Codex:** Tests müssen UI-Projekt referenzieren

### B6. Repo-Ballast (Sidecar / Modelle / Caches)
**Claude (7 GB) + Codex (22.9 GB):** sidecar/florence2_shadow_log, .pt-Roh-Weights, .venvs, .dotnet-Caches

### B7. Redundante Services / Konsolidierung möglich
**Claude + Codex:** 3× FullScan-Wrapper, 3× FrameExtractor, 5× PDF-CLI, 3 Self-Training-Orchestratoren

### B8. UI zu technisch / "Cockpit-Optik"
**Claude + Codex:** Hardware-Status in Hauptscreen, viele Panels gleichzeitig, dichte Layouts

### B9. HttpClient-Lifecycle (kbHttp-Leak)
**Claude + Codex:** Socket-Leak, Hot-Path "using var http = new HttpClient"

### B10. Trainingsdaten-Provenance
**Gemini (Quality-Gate 70% rot) + Codex (RunId, Modellversion, Prompt-Version)**

### B11. Command Injection (teilweise schon gefixt)
**Gemini + Codex:** Claude bestätigt SEC-H1..H5 sind gefixt, aber neue Stellen (Explorer, PowerShell, nvidia-smi, Sidecar) bleiben uneinheitlich

---

## C. Wertvolle Einzelmeinungen (1/3)

### Nur Gemini
- **YOLO-Export Speicher-Bombe** (Base64 → OOM) — sehr konkret, prüfenswert
- **UI-Anleitungstexte in Settings** ("Programm-Anleitung — 20 Sektionen" raus)

### Nur Claude
- **DamageClassesPromptFull deaktiviert** (#pragma warning disable CS0414) — Quick-Win, ~30 Min
- **51× new XxxWindow() in ViewModels** statt IDialogService
- **15× ffmpeg-ProcessStartInfo** statt zentraler ProcessRunner
- **Kein App-Icon / kein PerMonitorV2-DPI / keine Icon-Library**
- **PlayerWindow.xaml definiert lokale PlayerCard/PlayerLabelText** statt Theme-Erweiterung
- **Status-Tracking aus April-Audit** (welche STAB-/SEC-Findings sind gefixt)

### Nur Codex
- **Pipeline-Tests nicht von GPU-Tests getrennt** (QwenModelComparison = 100 Min!)
- **Zwei SQLite-Pakete** (System.Data.SQLite + Microsoft.Data.Sqlite)
- **Sprach-Glossar fehlt** (D/E gemischt: Haltung vs. WeakSpot)
- **Knowledge/sanierung Mehrfach-Quellen** (yaml + json + measures_learning)
- **TrainingRuns-Tabelle** mit RunId/Modellversion/Prompt-Version

---

## D. Empfohlener Fahrplan v4.3 (Konsens-basiert)

### 🚨 Phase 0 — heute (Hard-Blocker)
| # | Aufgabe | Konsens | Aufwand | Status |
|---|---|---|---|---|
| 0.1 | ~~Build reparieren (140 XAML-Fehler in DataPage / ClockPicker / Schaechte)~~ | B1 (2/3) | 1 Zeile (statt 1 Tag) | ✅ 2026-05-04 |
| 0.1b | ~~**Pipeline-Tests trennen** (Default-Filter)~~ — `.runsettings` mit `Category!=GpuEval&LongRunning` als Default. 452 Tests, 14 s. Manueller GPU-Lauf via `.runsettings.gpu`. Trait-Sweep fuer untraitierte Tests bleibt offen. | C-Codex + B2.2-konsolidiert (2/3) | 1 h (statt 4) | ✅ 2026-05-04 |
| 0.2 | ~~kbHttp-Leak fixen (ServiceProvider.cs:230)~~ — Field statt local var, ServiceProvider : IDisposable, catch-Block disposed explizit. Caller-Side Dispose() ist optional (App.OnExit-Hook offen). | B9 (2/3) | 1 h | ✅ 2026-05-04 |
| 0.3 | ~~SafeFireAndForget überall einsetzen (Helper existiert)~~ — 6 Stellen in PlayerWindow.xaml.cs migriert (4 Audit-genannt + 2 dasselbe Pattern). 1 Stelle bewusst nicht migriert (eigenes try/catch). | A4 (3/3) | 1 h | ✅ 2026-05-04 |
| 0.4 | ~~DamageClassesPromptFull aktivieren~~ — Konstruktor-Parameter `useFullDamagePrompt`, 3 Batch/Video-Aufrufer umgestellt, Pragma weg. Codier-Modus unveraendert. | C-Claude | 30 min | ✅ 2026-05-04 |
| 0.5 | ~~Caller-Side `ServiceProvider.Dispose()` in `App.OnExit`~~ — Hook ergaenzt (Folge von 0.2). | (Folge 0.2) | 5 min | ✅ 2026-05-04 |
| 0.6 | ~~SafeFireAndForget-Sweep ausserhalb PlayerWindow~~ — 9 Stellen migriert (OverviewPage 8x, KnowledgeMirror 1x). UI-Klick-Handler bewusst nicht migriert (kein Stabilitaetsrisiko). | (Folge A4) | 30 min | ✅ 2026-05-04 |

### Phase 1 — diese Woche (Quick-Wins)
| # | Aufgabe | Konsens | Aufwand |
|---|---|---|---|
| 1.1 | Slim-Down: 7 GB Sidecar-Shadow-Log + .pt-Roots + 5 Window-Leichen | A5 (3/3) | 1 Tag |
| 1.2 | ~~Empty-catch-Audit: 6+ Stellen mit mind. Debug.WriteLine~~ — 5 echt stille Service-Catches migriert (KnowledgeMirror, FewShotBuilder, SystemMonitor, TrainingCenterImport, PdfProtocolExtractor). UI-Cleanup-Catches und dokumentierte Best-Effort-Catches bewusst gelassen. ✅ 2026-05-04 | A3 (3/3) | 30 min |
| 1.3 | .gitignore schärfen: sidecar/.venv, Frames, Logs, Caches | B6 (2/3) | 30 min |
| 1.4 | ~~Hydraulik (DWA-A 110) + Eigendevis aus Hauptcode entfernen oder ausgliedern~~ — Sanfte Entkopplung: AppSettings.ShowExpertenmodusFeatures (default true), NavItem filtert Eigendevis, Hydraulik-Toolbar-Buttons mit Visibility-Binding, Settings-Page-Checkbox. Kein Default-Verhaltens-Wechsel, keine Loeschung. ✅ 2026-05-04 | A5 (3/3) | 1 Tag (geplant) / ~1 h (effektiv) |

### Phase 2 — nächste Woche (KB & KI-Qualität)
| # | Aufgabe | Konsens | Aufwand |
|---|---|---|---|
| 2.1 | ~~KnowledgeBase: FK Embeddings→Samples + Embedding.ModelVersion~~ — Schema, Runtime-PRAGMA, defensive 4-Schritt-Migration mit Orphan-Archivierung. 8 neue Tests. ✅ 2026-05-04 | B4 (2/3) | 4 h (geplant) / ~1 h (effektiv) |
| 2.2 | ~~KnowledgeBaseWriter (zentral, busy_timeout, foreign_keys, WAL)~~ — Writer-Klasse mit SemaphoreSlim, ExecuteInTransaction-Helper. busy_timeout=5000 + synchronous=NORMAL ergaenzt. KnowledgeBaseManager 3 Hot-Paths umgestellt. 10 neue Tests (PRAGMAs, Lock-Serialisierung, Stress, FK). ✅ 2026-05-04 | B3 (2/3) | 1 Tag (geplant) / ~1.5 h (effektiv) |
| 2.3 | Channel<T>-Pipeline für Training (Pre→KI→Gate→KB-Writer→UI) | B3 (2/3) | 2 Tage |
| 2.4 | Pipeline-Tests von GPU/Langzeit trennen (Trait Categories) | C-Codex | 4 h |
| 2.5 | ~~RehabilitationRulesEngine: Hardcode raus, nur YAML~~ — JSON-Schema erweitert (damage_groups_by_vsa_code + damage_matrix), Engine liest primaer JSON-Spiegel, Hardcode wird Fallback. YAML bleibt menschliche Pflege-Quelle, JSON ist maschinenlesbare Laufzeitquelle (kein direktes YAML-Parsing — wuerde NuGet erfordern). 11 neue Tests. ✅ 2026-05-04 | A7 (3/3) | 1 Tag (geplant) / ~1 h (effektiv) |

### Phase 3 — 2. Woche (UI Quick-Wins)
| # | Aufgabe | Konsens | Aufwand |
|---|---|---|---|
| 3.1 | app.manifest + PerMonitorV2-DPI + ApplicationIcon | C-Claude | 1 h |
| 3.2 | ~~Spacing/Typography-Tokens (Sp.S=4, M=8, L=16) als StaticResources~~ — Sp{XS,S,M,L,XL,XXL} (Double + Thickness) und Type{Caption,Small,Body,BodyLarge,H4,H3,H2,H1} in ThemeLight.xaml. 2 Beispielmigrationen in SettingsPage. ✅ 2026-05-04 | A6 (3/3) | 4 h (geplant) / ~30 min (effektiv, ohne Massenmigration) |
| 3.3 | ~~Zentraler DataGrid-Style + PageHeader UserControl~~ — DataGrid-Default-Style war bereits zentral (impliziter Style fuer alle 29 DataGrids). Neu: benannte Varianten DataGridStandard + DataGridCompact (BasedOn Default). PageHeader UserControl bleibt offen fuer spaeter. ✅ teilweise 2026-05-04 | A6 (3/3) | 6 h (geplant) / ~15 min (Token-Variante allein) |
| 3.4 | Hex-Hardcodes → DynamicResource (162 + 22 Stellen) | A6 (3/3) | 6 h |
| 3.5 | Anleitungstexte aus Settings in eigene Hilfe-Seite | C-Gemini | 2 h |

### Phase 4 — Monat 1 (Konsistenz)
| # | Aufgabe | Konsens | Aufwand |
|---|---|---|---|
| 4.1 | IDialogService erzwingen (51 ViewModel-Verstöße) | C-Claude | 10 h |
| 4.2 | ~~ffmpeg-Konsolidierung (15 Stellen → ProcessRunner)~~ — Inventar + Empfehlung dokumentiert. STAB-H1/SEC-H1..H3 sind aus April-Audit bereits erledigt. Konsistenz-Sweep ist gestaffelt: C1 (5 triviale Files) → C2 (5 mittlere) → C3 (5 komplexe Streaming). Pro Stufe Live-Test gegen Video-Material noetig. Bericht: `PHASE_4.2_FFMPEG_INVENTAR.md`. ⏸️ teilweise (April-Sicherheits-Fixes erledigt, Konsistenz-Sweep offen) | C-Claude | 1 Tag (geplant) / Inventar+Bericht (effektiv) |
| 4.3 | ~~Empty-catch-Sweep komplett (Logging-Pflicht)~~ — Inventar verifiziert: keine weiteren echt-stillen Catches. Verbleibende 98 Stellen sind alle by design (Cleanup-Pattern ~50, UI-Logging-Wrapper ~34, Typed-Exception-Filter ~10, Lifecycle-Race-Filter ~3). Phase 1.2 hat alle echt-stillen erfasst. Bericht: `PHASE_4.3_EMPTY_CATCH_INVENTAR.md`. ✅ 2026-05-04 | A3 (3/3) | 4 h (geplant) / Inventar+Bericht (effektiv) |
| 4.4 | ~~Trainings-Provenance: TrainingRuns-Tabelle + RunId~~ — Schema, Migration, BeginRun/EndRun/GetActiveRunId-API. UpsertSample schreibt RunId mit. 12 neue Tests. ✅ 2026-05-04 | B10 (2/3) | 1 Tag (geplant) / ~1 h (effektiv, ohne Aufrufer-Migration) |

### Phase 5 — Monat 2 (Architektur-Sanierung)
| # | Aufgabe | Konsens | Aufwand |
|---|---|---|---|
| 5.1 | DI-Container: Microsoft.Extensions.DependencyInjection | B2 (2/3) | 2 Tage |
| 5.2 | ServiceProvider.cs (657 Zeilen) zerlegen | B2 (2/3) | 1 Woche |
| 5.3 | KI-Schicht migrieren: QualityGate + Aggregator + Resolver → Application/KI | A1 (3/3) | 1 Woche |
| 5.4 | Produktmodus / Expertenmodus einführen | A5 (3/3) | 3 Tage |
| 5.5 | Sanierungs-Decision-Log (warum welche Massnahme?) | A7 (3/3) | 1 Tag |

### Phase 6 — Monat 3+ (Langfrist)
| # | Aufgabe | Konsens | Aufwand |
|---|---|---|---|
| 6.1 | PlayerWindow.xaml.cs in Partials zerlegen (9856 Zeilen) | A2 (3/3) | 1 Woche |
| 6.2 | TrainingCenterViewModel zerlegen → BatchOrchestrator + Repo + Writer | A2 (3/3) | 1 Woche |
| 6.3 | KI-Schicht komplett aus UI ausziehen (144 Dateien) | A1 (3/3) | langfristig |
| 6.4 | UI-Redesign: Fluent-WPF / WPF-UI 3 als Design-Basis | A6 (3/3) | 2 Wochen |

---

## D2. Zusatz-Befunde aus dem konsolidierten Audit (2026-05-04)

Diese drei Punkte tauchen im konsolidierten Audit (`audit_konsolidiert_2026-05-04.md`) erstmals explizit auf — wertvoll, aber kein Konsens-Status.

### D2.1. WindowStateManager.Save() bleibt synchron im Closing-Hook
- **Befund 2.5 (konsolidiert):** try/catch ist im Closing-Hook ergaenzt (gut), aber `settings.Save()` laeuft synchron. Bei Antivirus-Lock oder langsamer Platte kann Fensterschliessen kurz blockieren. `GetSettings()` schluckt Fehler ohne Log.
- **Empfehlung:** Save entkoppeln (Debouncer) oder mind. Logging in `GetSettings()` ergaenzen.
- **Aufwand:** ~30 min

### D2.2. RotateGenCts() validiert frueheren April-Fix
- **Befund 2.6 (konsolidiert):** Das fruehere CTS-Race-Pattern `Cancel(); Dispose(); new()` in `TrainingCenterViewModel` wurde durch `RotateGenCts()` ersetzt. **Audit-Bestaetigung, dass der Fix haelt.**
- **Empfehlung:** Pattern beibehalten. Restliche `CancellationTokenSource`-Stellen im Repo nach demselben Muster pruefen.
- **Aufwand:** Sweep ~2 h

### D2.3. ProcessRunner ist verbessert, aber Stil noch uneinheitlich
- **Befund 2.3 (konsolidiert):** `ProcessRunner.cs` (Application/Common) nutzt jetzt `ArgumentList`, async stdout/stderr-Drain, Timeout. `PdfProtocolExtractor.cs` nutzt `ArgumentList` fuer pdftotext + Python. **Aber:** Direkte `ProcessStartInfo.Arguments = ...` existieren weiterhin fuer Explorer, PowerShell, nvidia-smi, Playwright und Sidecar. Manche kontrolliert, aber Stil uneinheitlich.
- **Empfehlung:** Alle neuen Prozessstarts ueber `ProcessRunner`. Bestehende direkte Stellen pruefen — wenn User-/Dateipfade beteiligt sind, auf `ArgumentList` umstellen.
- **Aufwand:** ~1 Tag (Sweep + Migration)

---

## E. Wo widersprechen sich die Audits?

**Wenig direkter Widerspruch.** Sie ergänzen sich:
- **Gemini** bleibt allgemein und konzeptionell (gut für Stakeholder-Kommunikation)
- **Claude** ist sehr metrisch und differenziert nach STAB/SEC/ARCH (gut für Tracking)
- **Codex** ist build-/test-fokussiert und repo-praktisch (gut für Operations)

**Einziger leichter Widerspruch:**
- **Gemini:** "QualityGate ist zu streng (70% rot)" → eher lockern
- **Claude:** "QualityGate-Schwellen 0.75/0.45 OK" → eher stabil halten
- **Lösung:** Erst Provenance + Validierung verbessern, dann Schwellen evidenzbasiert anpassen.

---

## F. Top-10 für v4.3 (Empfehlung)

Wenn nur 10 Dinge gemacht werden, dann diese:

1. ~~**Build reparieren** (Phase 0.1)~~ — ✅ erledigt 2026-05-04 (1 Zeile, siehe B1).
   ~~**Neu auf #1:** Pipeline-Tests von GPU/Langzeit trennen~~ — ✅ erledigt 2026-05-04 (`.runsettings`-Default, 452 Tests in 14 s).
2. **7-22 GB Repo-Ballast löschen** (Phase 1.1) — Codex/konsolidiert: 22.9 GB sidecar, Claude: 7 GB sidecar/florence2_shadow_log + .pt-Roots
3. **Empty-catch-Sweep** (Phase 1.2 + 4.3)
4. **Hydraulik/Eigendevis raus oder optional** (Phase 1.4)
5. **KnowledgeBase: FK + ModelVersion + zentraler Writer** (Phase 2.1+2.2)
6. **RehabilitationRulesEngine nur aus YAML** (Phase 2.5)
7. **UI: Spacing/Typography-Tokens + zentraler DataGrid-Style** (Phase 3.2+3.3)
8. **DI-Container einführen** (Phase 5.1)
9. **PlayerWindow.xaml.cs in Partials zerlegen** (Phase 6.1)
10. **Produktmodus / Expertenmodus** (Phase 5.4)

---

## G. Was fehlt noch?

Bevor v4.3 final geplant wird, könnten zusätzliche Audits sinnvoll sein für:
- **Performance / Profiling** (kein Audit hat echte Laufzeit-Messungen)
- **Threading / Race Conditions** (alle drei nennen Verdachtsfälle, keiner reproduziert)
- **Echte UX-Tests** mit Anwender (nicht nur Code-Optik)
- **Datenbank-Migration-Pfad** (wie kommen bestehende KB-Daten in v4.3?)

Sag Bescheid, wenn weitere Audits dazukommen — ich aktualisiere dann diese Übersicht.
