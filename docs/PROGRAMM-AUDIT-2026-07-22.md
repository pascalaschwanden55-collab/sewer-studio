# Programm-Audit SewerStudio — 21./22.07.2026

**Geprüfter Stand:** Branch `feature/gis-karte`, HEAD `7e2aac6dd` (inkl. der am 21.07.
abends gelandeten Deepscan-Abarbeitung, gepusht).
**Methode:** Build + volle Testsuite frisch ausgeführt; Deepscan-Bericht, KI-Diagnose
und Sitzungs-Memory vom 21.07. als Ausgangslage; **~20 Befund-Stellen von Hand am Code
nachgeprüft** (jede Statusangabe unten mit Datei:Zeile belegt). Drei zusätzliche
Tiefen-Scan-Agenten (Architektur, Performance, Optik) sind am Anthropic-Sitzungslimit
abgebrochen — ihre Kernpunkte wurden danach gezielt von Hand geprüft.
**Nicht geprüft:** echtes Laufzeitverhalten unter GPU-Last (Soak, 3000-Videos-Last)
und Optik per Auge (Sichtprüfung durch Pascal steht weiterhin aus).

**Vorab — Begriffsklärung „Fable":** Die Audit-Vorlage war für F#-Webprojekte gedacht
(„Fable" = F#-zu-JavaScript-Compiler, Elmish, Feliz, CSS). Im gesamten Projektordner
und in `.gemini` gibt es keinerlei F#-Code (Glob-geprüft). „Fable" ist hier nur der
Name des Claude-Modells. Das Audit wurde deshalb sinngemäss übersetzt:
JS-Interop → C#↔Sidecar/Ollama-HTTP, MVU → MVVM, virtuelles DOM → WPF-Rendering,
CSS → XAML/Theme.

---

## Abschnitt 1 — Fehleranalyse & Robustheit

### 1.1 Compiler & Tests (frisch gemessen)

| Prüfung | Ergebnis |
|---|---|
| `dotnet build AuswertungPro.sln` | **0 Fehler, 0 Warnungen** (4.9 s) |
| `dotnet test` (4 Testprojekte) | **10'253 Tests, 0 Fehler**, 3 bewusst übersprungen (Echt-Video, VSA-Archiv, Fenster-Smoke), ~1¼ min |

### 1.2 Ist-Stand der Deepscan-Befunde vom 21.07. (von Hand am Code verifiziert)

Wichtig: Der Deepscan-Bericht (`docs/DEEPSCAN-VERBESSERUNGSPLAN-2026-07-21.md`)
beschreibt den Morgen-Stand. In den Abendsitzungen wurde der Plan **praktisch komplett
abgearbeitet**. Dieses Audit hat die Fixes unabhängig am Code bestätigt:

| Befund | Status auf HEAD | Beleg |
|---|---|---|
| befund-1 (P1) OllamaClient `BaseAddress` killt Batch | **behoben** | `OllamaClient.cs:30-43` — keine BaseAddress mehr, absolute `Endpoint()`-URIs |
| befund-2 (P1) Sidecar-Ausfall = stiller Teilerfolg | **behoben** (`edf78556e`) | `MultiModelAnalysisService.cs:184,438-450,922` — Folgefehler-Zähler (Abbruch ab 8), `Degraded`-Kennzeichen, UI-Warnung |
| befund-3 (P2) Zwei Store-Instanzen auf `training_samples.json` | **behoben** (anders als geplant) | `TrainingSampleFileStore.cs:20-23,49` — EIN Lock je Zieldatei über alle Instanzen (`ConcurrentDictionary<string,SemaphoreSlim>`); Lost-Update-Mechanik weg. Zwei Instanzen existieren weiter — nur noch Kosmetik. |
| befund-4 (P2) KINS wischt Felder leer | **behoben** | `KinsImportService.cs:165-180` — MergeEngine mit Leer-Schutz; Haltungsname nur bei Neuanlage |
| befund-5 (P2) Backup rotiert fehlende Quelle still weg | **behoben** | `DirectoryMirror.cs:86-96` — Warnung + `PreserveExistingMirror` |
| befund-6 (P2) Test-Race Umgebungsvariablen | **behoben** | `AiSettingsTests.cs:12` — `[Collection("EnvironmentVars")]` |
| U1 Einzelframe ignoriert Modellausfall | **behoben** (`2237c3e57`) | `SingleFrameMultiModelService.cs:160-216` — `Degraded` + Grund |
| U2 ffmpeg ohne Timeout im Fallback | **behoben** (AP-1) | `VideoFullAnalysisService.cs:37-73` — gemeinsamer `VideoProbeService` (Timeout + Kill-Baum) |
| U4 Live-Edits verloren beim Projekt-Swap | **dauerhaft gelöst** | Import-Transaktion: Content-Signatur (Edits gewinnen) + Absturz-Journal `.import-transaction.json` + Recovery beim Laden (8 Commits, Spec unter `docs/superpowers/specs/`) |
| U6/U16 Legacy-Import überschreibt still | **behoben** | WinCan/IBAK/KINS mergen jetzt über `LegacyStammdatenMerger` → MergeEngine (Leer-/UserEdited-Schutz, Priorität, Konfliktprotokoll); WinCan-Matching vereinheitlicht |
| U7 YoloConf binär 1.0 | **behoben** (`fb2833649`) | `MultiModelAnalysisService.cs:721-728` |
| U8 Fehlgelesener OSD-Meter vergiftet Timeline | **Gates da, Rest bewusst zurückgestellt** | 0–500-m-Gate + Bildqualitäts-Gate (`MeterPlausibility.cs`, `MultiModelAnalysisService.cs:776-790`). Offen bleibt der Abgleich mit der echten Haltungslänge (`EstimateMeter` hält per `Math.Max` jeden akzeptierten Ausreisser bis zur nächsten guten OSD-Lesung, Z. 962) — zurückgestellt, weil die echte Länge am Aufrufort erst verfügbar gemacht werden muss. |
| U9 toter QualityGate-Cluster | teils Fehlbefund, Kern **behoben** | `McDropoutService` gelöscht; Rest des gemeldeten „Clusters" ist produktiv (Self-Improving) bzw. bewusste Design-Frage (TemperatureScaler) |
| U10 / U11 (Mapping-Doppel, Uhrlage-Parser) | **geprüft: kein Bug** | U10 Fehlbefund; U11 = Konsolidierungsidee, keine Fehlersemantik |
| U12 / U13 Test-Isolation | **behoben** (`507ba57dc`) | Sidecar-Test lädt keine Gewichte mehr aus dem Netz; AppData-Isolationsnetz in Infrastructure.Tests |
| U14 `VideoFullAnalysisService` ohne Test | **behoben** (`df84c7321`) | 4 Charakterisierungstests |
| U15 Fenster hält ServiceProvider-Feld | bestätigt, klein | `TrainingStudioWindow.xaml.cs:29` — bewusster, kommentierter Durchgriff; VM wird bei `Closed` entsorgt |
| Quick-Wins 6–10 | **alle behoben** | `_lastFinding`-Reset (Z. 179), `BestEffort`-Warnung, Karten-Timeout 10 s (`OnlineXyzTileSource.cs:53`), `classifier_loaded` fail-closed (`VisionPipelineDtos.cs:106-109`), Null-Gate → ehrlich Rot |
| Sidecar-Härtung (AP-6) | **behoben** (`5b13cf3cf` u. a.) | `main.py:82-87` (`run_in_threadpool`), `sam_wrapper.py:198-202` (OOM-Re-Raise via `cuda_errors`), `gpu_manager.py:96-190` (`_global_lock` + Kopien) |
| AP-9 HttpClient-Lecks | **alle 3 kartierten Lecks geschlossen** | `VisionPipelineClient` IDisposable/_ownsHttp; Codiermodus-Runtime disposed; TrainingStudio/-Center-Ketten disposed |

**Wirklich noch unverifiziert** (vor einem Fix kurz nachlesen): nur noch
**U3** (`VideoFrameStream`-Stillende bei ffmpeg-Hänger — ContinueWith wurde bereits
gehärtet, der Stillende-Pfad selbst ist ungeprüft) und **U5** (`_E.pdf`-Neuerzeugung
nicht-atomar).

### 1.3 Absturzquellen & Fehlerbehandlung (Stichproben frisch)

- `async void`: nur **14 Stellen in 8 Dateien**, alle in Fenster-/Event-Handlern (dort gehört es hin).
- Leere `catch {}`: nur in Shutdown-/Aufräumpfaden (Listener-Stop, Fenster-Close); produktive Pfade nutzen `BestEffort.ReportWarning` bzw. `Result<T>`.
- WPF-Crashmuster VisualTree/Hit-Test: flächendeckend über `VisualTreeSafe` abgefangen.
- JSON-Verträge: C#↔Sidecar mit Versions-Gate 1.2.0 und fail-closed-Defaults; Ollama-Antworten defensiv geparst (`TryGetProperty`); Qwen nur mit striktem JSON-Schema.
- Netz-/Leerdaten: Batch und Einzelframe kennzeichnen Modellausfälle als `Degraded` statt „grünes Rohr"; fehlendes QualityGate ⇒ Rot; Online-Karte fällt nach 10 s sauber auf leer.

**Urteil Abschnitt 1: sehr robust.** Die beiden P1 vom Vortag sind erledigt; übrig
sind zwei kleine Verifikationen (U3, U5) und ein bewusst zurückgestelltes Meter-Gate.

---

## Abschnitt 2 — Architektur & Struktur

### 2.1 Muster & Konsequenz

- **MVVM + Composition Root** konsequent: Domain ← Application ← Infrastructure ← UI. Die Projekt-Referenzen bestätigen die Richtung exakt; keine Rückwärts-Referenz (csproj-geprüft).
- „Injizierbar"-Konvention (Vertrag in Application, Impl in Infrastructure, Registrierung im ServiceProvider) wird von Guard-Tests erzwungen: Boundary-Ratchet, gepinnter Registrierungs-Zähler, `*DependencyTests`, 1000-Zeilen-Ratchet, Lifecycle-Tests.
- Kennzahlen: **~232'000 Zeilen produktiver C#-Code**, ~1'950 Testdateien (~219'000 Zeilen), 73 XAML-Ansichten, 58 Python-Dateien im Sidecar.

### 2.2 Grösste Dateien (alle unter dem 1000-Zeilen-Ratchet)

`TrainingCenterViewModel` 997 · `WinCanDbImportService` 972 · `LegacyXtfImportService` 970
· `PdfProtocolExtractor` 969 · `MultiModelAnalysisService` 966 · `OverlayToolService` 954
· `ShellViewModel` 936 · `HoldingFolderDistributor` 928 · `SystemMonitorService` 921 (UI-Schicht!)

### 2.3 Architektur-Nachscan: abgeschlossen — Ergebnis gut

Der beim Deepscan zweimal gescheiterte Architektur-Scan wurde am 21.07. abends per
Workflow doch noch vollständig gefahren (19 Rohbefunde → **8 bestätigt: 2 P2, 6 P3,
0 P1, keine Bugs**; >50 % der Meldungen wurden bei der Gegenprüfung herabgestuft).
Stand heute:

- Beide P2 **gefixt**: PDF-Export hinter `IOfferPdfExportService` (`8a432e962`), KI-Schnellscan hinter `IQuickScanSession`-Fabrik (`996edb652`).
- Von den 6 P3 sind **3 bereits erledigt** (Registrierungs-Guard echte Invariante `ede0ec801`, ProtocolService-Injektion `7e2aac6dd`, Plausibilitätsdienst-Injektion `78128df1c`).
- **Offen bleiben 3 P3 (Kosmetik/Wartbarkeit):** ServiceProvider-Konstruktor mischt Verdrahtung und Laufzeit-I/O (~380 Z.), Eval-Schutz wird doppelt konfiguriert, `SettingsPageViewModel`-Convenience-`new`.
- Dazu die bekannten Struktur-Wünsche: `SystemMonitorService` (921 Z.) aus der UI-Schicht nach Infrastructure (beim nächsten Anfassen), `TrainingCenterViewModel` vor Training-Studio-Etappe 2 entlasten, Übergangs-Fassaden weiter nur schrumpfen lassen.

**Urteil Abschnitt 2:** Für ein Solo-Projekt aussergewöhnlich diszipliniert; die
Schichtenregeln werden von Tests erzwungen, und der unabhängige Nachscan fand **keinen
einzigen P1**. Note **B+ bis A-** bestätigt.

---

## Abschnitt 3 — Performance & Speicher

### 3.1 Geprüft und in Ordnung

- **Alle grossen Tabellen virtualisieren** mit Recycling + Pixel-Scroll: DataPage, BuilderPage, SchaechtePage, MediaSearch, Haltungs-/Schachtansicht (XAML-geprüft).
- Abschluss-Anzeige der Pipeline deckelt bewusst auf **250 sichtbare Befunde** (`PipelineResultPresenter`).
- Kein blockierendes `.Result`/`.Wait()` auf dem UI-Thread gefunden: Treffer waren entweder nach `RanToCompletion`-Prüfung (sicher, `TrainingStudioViewModel.cs:294-300`), mit 1-s-Deckel im Dispose oder auf Hintergrund-Threads.
- Hintergrundarbeit gedeckelt (`BoundedBackgroundTaskRunner`); HttpClient-Lecks im Codiermodus/Training seit AP-9 geschlossen; `NeuralSphere`- und `SchaechtePage`-Timer-Lecks bereits gefixt.

### 3.2 Kleine Befunde

| # | Stelle | Bewertung |
|---|---|---|
| P3 | `PlayerCodingSidePanel.xaml:127` u. `:371` — zwei Codier-Listen mit `IsVirtualizing="False"` | Bewusst (MaxHeight 400, weiches Scrollen, Drag&Drop). Erst bei sehr langen Codierlisten spürbar. Bei Bedarf: Virtualisierung an + `CanContentScroll="True"` testen. |
| P3 | `SystemMonitorService.cs:564,613,759,838` — 4× sync-über-async | Läuft in `Task.Run`-Hintergrund-Polls mit 5-s-Timeout, friert die UI nicht ein. Beim Umzug nach Infrastructure auf echtes `await` umstellen. |
| offen | OverviewPage-Vorschaubilder (alter Cockpit-Befund „synchrones Preview-Laden") | Nicht nachgeprüft — beim nächsten Optik-Durchgang verifizieren (Stichwort `DecodePixelWidth` + `Freeze`). |

### 3.3 Nicht gemessen

Echte Laufzeitprofile (GPU-Batch über Stunden, UI-Flüssigkeit bei 3000 Videos) waren
nicht Teil dieses Audits. Wenn dich etwas konkret ruckelt: die Stelle nennen, dann
wird gezielt gemessen statt geraten.

---

## Abschnitt 4 — Visuelle Effekte & Optik

### 4.1 Ist-Stand (viel ist schon da)

Das Neural-Elegance-Paket (16.07., 8 Pakete) liefert bereits: globaler **Ruhe-Schalter**
(ReduceMotion), **Toasts mit Lebenslinie**, TextBox-**Fokus-Glow**, Einblende-/
Hover-Effekte als angehängte Eigenschaften (`HoverFx.Lift`, `WindowFx.Entrance`,
`EntranceFx.Stagger`), KI-Puls, NeuralSphere, reparierte Wartebalken (9 Stellen),
Fluent-Icons statt Emoji. Guard-Tests sichern Icon-Regeln, Schlüssel-Eindeutigkeit und
„Animationen laufen nicht ewig". Pressed-Feedback ist in beiden Themes vorhanden
(10 `IsPressed`-Trigger, Stichprobe). Leerzustände („Noch keine …"): in mindestens
7 Ansichten belegt (Overview, DataPage, Schaechte, Karte, MediaConflicts, MediaSearch,
Pipeline-Fenster).

### 4.2 Offene Punkte

1. **H7-Farbbefund — wartet auf DEINE Entscheidung.** WPF liest `#AARRGGBB`, CSS-Denkweise `#RRGGBBAA`: einige achtstellige Abzeichen-Farben sind dadurch vermutlich seit jeher eine andere Farbe als gemeint (`docs/DESIGN-NEURAL-ELEGANCE-PLAN-2026-07-16.md`, H7). Kleiner Fix, aber Farbwahl = Geschmacksfrage.
2. **Sichtprüfung** aller Design-Pakete durch dich steht weiter aus (App starten, durchklicken).
3. **179 sechsstellige Hex-Farben in 20 View-Dateien** ausserhalb der Themes. Ein grosser Teil ist bewusst (Einbrenn-Overlays der Foto-Messung, Karten-Pins — die MÜSSEN feste Farben haben). Rest: beim Anfassen der jeweiligen Datei auf Theme-Brushes umstellen (so steht es schon im Design-Plan).
4. Leerzustand-Abdeckung für VsaPage / ImportPage / Export-Seite prüfen (im Suchmuster nicht gefunden — kann auch nur anders formuliert sein).

### 4.3 Konkrete Mini-Vorschläge (auf dem vorhandenen Baukasten)

Sanfter Druck-Effekt für einen Knopf, der noch keinen hat:

```xml
<Trigger Property="IsPressed" Value="True">
    <Setter Property="RenderTransform">
        <Setter.Value><ScaleTransform ScaleX="0.97" ScaleY="0.97"/></Setter.Value>
    </Setter>
    <Setter Property="RenderTransformOrigin" Value="0.5,0.5"/>
</Trigger>
```

Mittelfristige Idee: ein kleines wiederverwendbares `StatusHost`-Control mit vier
Zuständen (Lädt / Leer / Fehler / Inhalt), gefüttert vom ViewModel — dann sieht jede
neue Seite automatisch richtig aus. Das ist das WPF-Gegenstück zum `RemoteData`-Muster
aus der F#-Welt; die Bausteine (Wartebalken, Toasts, Leertexte) existieren schon.

---

## Abschnitt 5 — Weitere Verbesserungen

- **Grösster Hebel überhaupt (KI-Diagnose 21.07.):** die Taxonomie-Entscheidung Weg A (grob, 11 Klassen, Migration freigeben) vs. Weg B (fein, echtes Datenprojekt). Produktentscheidung von dir — kein Label-Marathon. Ohne sie wirkt die Erkennung ~2× schlechter als sie ist (48 % vs. 22 % Top-1).
- **Repo-Hygiene:** `docs/KI-ERKENNUNG-DIAGNOSE-2026-07-21.md` committen; `Amtsblatt-Monitor/state.json` + `tmp/` in `.gitignore` aufnehmen oder committen.
- **Offene Design-Fragen** (bewusst nicht still entschieden): `TemperatureScaler`/`CalibrationMetrics` getestet-aber-unverdrahtet (anschliessen oder löschen?), `SchachtSelectionChanged`-Kanal ohne KartePage-Verdrahtung (anschliessen oder verwerfen?).
- **Typsicherheit/Idiomatik:** stark (Records, `FieldKeys`, `Result<T>`, strikte JSON-Schemata). Kein Handlungsbedarf.
- **Internationalisierung:** bewusst Deutsch-only — für ein Solo-Werkzeug richtig.
- **Tooling:** Build 4.9 s, Suite ~1¼ min — schnell; kein Umbau nötig.
- **Sitzungs-Ökonomie (Lehre aus diesem Audit):** Drei parallel gestartete Tiefen-Scan-Agenten haben das Sitzungslimit gerissen. Grosse Analysen künftig: erst Berichte/Memory nutzen, dann gezielte Einzel-Lesezugriffe; Agenten einzeln und eng begrenzt (der erfolgreiche Architektur-Nachscan lief als bewusst orchestrierter Workflow mit kleinen Einzelaufträgen).

---

## Abschnitt 6 — Zusammenfassung & priorisierte To-do-Liste

**Gesamtbild:** Der Code ist in bemerkenswert gutem Zustand. Build sauber, 10'253
Tests grün, beide P1 vom Vortag behoben, der Deepscan-Plan ist praktisch komplett
abgearbeitet und gepusht, der unabhängige Architektur-Nachscan fand keinen P1.
Was bleibt, sind Kleinigkeiten, zwei Verifikationen — und vor allem **Entscheidungen,
die nur du treffen kannst**.

### Sofort (je < 1 h)
1. U3 + U5 kurz am Code verifizieren (die letzten zwei ungeprüften Deepscan-Meldungen); bei Bestätigung je ein kleiner Fix + Test.
2. Repo-Hygiene: KI-Diagnose-Doc committen; Amtsblatt-Artefakte in `.gitignore`.

### Deine Entscheidungen (blockieren jeweils den nächsten Schritt)
3. **Taxonomie Weg A oder B** — grösster Hebel für die Erkennungsqualität; danach ggf. `detect_class_migration_v2` fachlich freigeben.
4. **H7-Farben** + Sichtprüfung der Design-Pakete (App durchklicken).
5. Design-Fragen: TemperatureScaler (anschliessen/löschen), SchachtSelectionChanged (verdrahten/entfernen).

### Mittelfristig (kleine Pakete, je mit Test)
6. U8-Rest: echte Haltungslänge an die Analyse geben und akzeptierte OSD-Meter dagegen prüfen (z. B. ≤ 1.5 × Länge).
7. Die 3 verbliebenen Architektur-P3 (ServiceProvider-Ctor entzerren, Eval-Schutz-Doppelkonfig, SettingsPageVM-new).
8. `SystemMonitorService` nach Infrastructure (beim nächsten Anfassen); `TrainingCenterViewModel` vor Training-Studio-Etappe 2 entlasten.
9. AP-8-Rest: Converter-Duplikate (nur mit laufender UI-Testrunde, XAML-Risiko).

### Langfristig
10. Ein-Knopf-Import und additives XTF-Rohdatenarchiv in die neue Import-Transaktionssitzung integrieren (laut CLAUDE.md noch ausserhalb).
11. `StatusHost`-Vierergespann für einheitliches Seiten-Feedback.
12. Echte Laufzeit-/Soak-Messung der Pipeline (GPU-Nachtlauf mit Telemetrie-Auswertung).
