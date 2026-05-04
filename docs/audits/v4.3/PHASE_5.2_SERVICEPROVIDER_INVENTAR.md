# Phase 5.2 — ServiceProvider zerlegen (Inventar + Empfehlung)

**Datum:** 2026-05-04
**Auftrag:** "ServiceProvider.cs (657 Zeilen) zerlegen" — Audit B2 (Konsens 2/3, ~1 Woche).
**Resultat:** Inventar + Module-Vorschlag. KEIN Code-Eingriff.

---

## A. Bestand

`src/AuswertungPro.Next.UI/ServiceProvider.cs` aktuell **678 Zeilen** mit:
- 24 oeffentliche Service-Properties
- 36 `new XxxService(...)`-Aufrufe im Konstruktor
- Sidecar-Pfad-Resolution
- Brain-Mirror-Initialisierung
- Warmup-Tasks fuer Vision/Embed/Reference-Modell
- VRAM-Verifikation
- Knowledge-Base-Retrieval-Setup

**Service-Kategorien:**

### A1. Domain-IO-Services (Import/Export)
- `Projects`, `PdfImport`, `XtfImport`, `WinCanImport`, `IbakImport`, `KinsImport`, `ExcelExport`, `Protocols`, `PhotoImport`

### A2. Reports/Auswertung
- `ProtocolPdfExporter`, `Vsa`, `MeasureRecommendation`, `DevisGenerator`, `DevisExcelExporter`

### A3. AI/KI-Pipeline
- `ProtocolAi`, `CodeCatalog`, `PipelineCfg`, `Sidecar`, `Retrieval` (private), `_kbHttp` (private)

### A4. UI-Services (cross-cutting)
- `Dialogs`, `PlaywrightInstaller`, `Logger`, `LoggerFactory`, `ErrorCodes`

### A5. Konfiguration
- `Settings`, `Diagnostics`

### A6. Konstruktor-Aktionen (Setup)
- Sidecar-Pfad-Suche (`AppContext.BaseDirectory`/sidecar/...)
- ThemeManager (Settings.UiTheme)
- Logger-File-Pfad
- Warmup-Tasks (Task.Run mit Sidecar-Wait + 3 Modell-Vorlade-Schritte + VRAM-Verifikation)
- Brain-Mirror-Service-Initialisierung
- Decision-Log-Setup (Phase 5.5)
- Disk-Space-Pruefung
- KbHttp-Initialisierung mit IDisposable-Cleanup
- ModelConsistency-Pruefung

---

## B. Probleme im aktuellen Stand

1. **Konstruktor zu lang (~600 Zeilen Body).** Schwer testbar, schwer zu reviewen.
2. **Implizite Abhaengigkeitsreihenfolge** zwischen Modellen (z.B. SidecarConfig → Sidecar → Warmup → Retrieval).
3. **Lebenszyklus-Mischung:** Singleton-Services (Logger, Sidecar) und langlebige Hintergrund-Tasks (Warmup) im selben Konstruktor.
4. **Disposable-Lifecycle:** Phase 0.2 hat `_kbHttp.Dispose()` in `App.OnExit` ergaenzt, aber Sidecar/PlaywrightInstaller/RetrievalService haben eigene Lifecycle-Patterns.

---

## C. Empfohlene Modularisierung

### Modul-Vorschlag (jeweils in eigene Datei)

| Modul | Inhalt | Zielgroesse |
|---|---|---:|
| `ServiceProvider.cs` (Shell) | Properties, Konstruktor-Aufruf der Module | 100-150 Zeilen |
| `Modules/ImportExportModule.cs` | Domain-IO-Services A1 | 50-80 Zeilen |
| `Modules/ReportsModule.cs` | A2 + ProtocolPdfExporter | 30-50 Zeilen |
| `Modules/AiPipelineModule.cs` | A3 + Warmup-Setup | 200-250 Zeilen |
| `Modules/UiServicesModule.cs` | A4 | 30-50 Zeilen |
| `Modules/KnowledgeBaseModule.cs` | KB-Context, Writer, Retrieval, EmbeddingService, BrainMirror | 100-150 Zeilen |
| `Modules/SidecarSetupHelper.cs` | Sidecar-Pfad-Suche + StartAsync-Wrapper | 50-80 Zeilen |

Jedes Modul ist statisch oder eine kleine Klasse mit Methode `Configure(ServiceProvider sp)`.

### Naechster Schritt nach Modularisierung
- Phase 5.1 (DI-Container) wird trivial — jedes Modul registriert seine Services bei `IServiceCollection`.

---

## D. Risiken

| Risiko | Wirkung | Gegenmittel |
|---|---|---|
| Reihenfolge-Bug zwischen Modulen | Service ist null bei Konstruktor-Aufruf | Build-Reihenfolge im Shell-Konstruktor explizit dokumentieren |
| Tests muessen alle Module einzeln testen | Mehr Tests | Stichproben — End-to-End-Test bleibt Smoke-Lauf |
| Brain-Mirror und Warmup haben Background-Tasks | Race-Conditions | Background-Tasks in eigenes Modul, klar dokumentiert |
| ServiceProvider-Field-Pattern bleibt | Halbe Migration | Sub-Phase 5.2 ist genau dieser Schritt — DI-Container kommt in 5.1 nach 5.2 |

---

## E. Empfohlener gestaffelter Pfad

### Sub-Phase 5.2.A: Module-Skelette (~2-3 h)
- Module-Verzeichnis anlegen, leere Module-Klassen.
- ServiceProvider ruft Modul-Configure-Methoden auf — Code wird verschoben aber nicht geaendert.

### Sub-Phase 5.2.B: Module-Migration einzeln (~je 1-2 h)
- ImportExport (einfach) → erste Migration
- Reports (klein) → zweite
- UiServices (Singleton mit IDisposable) → dritte
- KnowledgeBase (mittel-komplex) → vierte
- AiPipeline + Sidecar (komplex, Background-Tasks) → letzte

### Sub-Phase 5.2.C: Tests + Smoke-Lauf (~2 h)
- Unit-Test pro Modul (Service-Verkabelung).
- App-Smoke-Lauf: alle Module in Reihenfolge.

**Total: ~10-15 h, statt 1 Woche geplant.**

---

## F. Reihenfolge mit Phase 5.1

**Empfohlene Reihenfolge:**
1. Phase 5.2 zuerst (ServiceProvider zerlegen) — keine NuGet noetig
2. Phase 5.1 danach (DI-Container) — auf modularisierten Service-Bloecken bauen

**Alternative:** beide in einer eigenen 2-Wochen-Session parallel, weil sie inhaltlich zusammenhaengen.

In dieser Iteration: **dokumentierter Stand**, kein Code-Eingriff.

---

## G. Akzeptanz

- ServiceProvider mit 678 Zeilen + 24 Properties + 36 Service-Erzeugungen verifiziert.
- 6 Module-Vorschlaege mit Zielgroessen.
- Reihenfolge-Empfehlung: 5.2 vor 5.1.
- ⏸️ Migration in eigener Session.
- KEIN Code-Eingriff in dieser Iteration.
