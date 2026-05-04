# Phase 5.1 — DI-Container (Inventar + Empfehlung)

**Datum:** 2026-05-04
**Auftrag:** "Microsoft.Extensions.DependencyInjection einfuehren" — Audit B2 (Konsens 2/3 Gemini+Claude, ~2 Tage).
**Resultat:** Inventar + API-Empfehlung. KEIN Code-Eingriff. KEIN NuGet-Add.

---

## A. Bestand

**Aktueller manueller DI-Container:**
- `src/AuswertungPro.Next.UI/ServiceProvider.cs` — **678 Zeilen** Konstruktor-Code, der alle Services direkt erzeugt (PdfImport, XtfImport, WinCanImport, Ollama, EmbeddingService, RetrievalService, KnowledgeBaseManager, MeasureRecommendation, DevisGenerator, ...)
- `App.Services` als statische Property — `static IServiceProvider Services => _services`
- `_services = new ServiceProvider(settings, diagnostics, logger, loggerFactory)` einmal in `App.OnStartup`

**Aufrufer-Stellen (`App.Services`):**
- 22 Files
- Top: DataPage.xaml.cs (10), TrainingCenterWindow.xaml.cs (6), SettingsPage.xaml.cs (6), SchaechtePage.xaml.cs (4)
- Cast-Pattern ueberall: `var sp = (ServiceProvider)App.Services; sp.Dialogs.OpenFile(...)`

**NuGet-Stand:**
- `Microsoft.Extensions.Logging` 10.0.2 vorhanden
- `Microsoft.Extensions.DependencyInjection` **fehlt** — muss als neues NuGet ergaenzt werden

---

## B. Warum Phase 5.1 nicht in einer Session

1. **NuGet-Genehmigung:** CLAUDE.md sagt "keine NuGet-Pakete ohne Rueckfrage". Vor Phase 5.1 muss der User `Microsoft.Extensions.DependencyInjection` (Version passend zu Logging 10.0.2) explizit freigeben.

2. **API-Bruch-Risiko:** ServiceProvider hat 678 Zeilen mit gewachsenen Konstruktor-Abhaengigkeiten (Sidecar, KbHttp, Retrieval, BrainMirror, MeasureRecommendation, DevisGenerator). Migration auf `IServiceCollection.AddSingleton<>` braucht Sortierung der Abhaengigkeitsgraphen — ein Fehler in der Order = Runtime-Crash.

3. **Aufrufer-Migration:** 22 Files referenzieren `App.Services` direkt mit explizitem Cast. Saubere DI haette Constructor-Injection in jedem Page-VM und jedem Window-CodeBehind. Das aendert ~22 Dateien plus alle Konstruktoren.

4. **Test-Coverage:** Aktuell **keine Tests** auf ServiceProvider-Initialisierung oder Service-Verkabelung. DI-Migration ohne Tests ist Blindflug.

---

## C. Empfohlener gestaffelter Pfad

### Etappe 1: Vorbereitung (~1 h, NuGet-Genehmigung noetig)

1. NuGet `Microsoft.Extensions.DependencyInjection` zum UI-Projekt ergaenzen.
2. **Parallel** zu `ServiceProvider` einen neuen `ConfigureServices`-Helper anlegen, der `IServiceCollection`-Registrierungen baut.
3. `App.OnStartup` baut beide Container — `ServiceProvider` bleibt aktiv, neue DI ist ungenutzt verfuegbar fuer schrittweise Migration.

### Etappe 2: Service-Migration (~1 Tag)

1. Services nach Schichten gruppieren:
   - Stateless Services (Logger, ProcessRunner, FfmpegLocator, FileLoggerProvider) → AddSingleton
   - Konfigurations-Wrapper (AppSettings, DiagnosticsOptions, OllamaConfig) → AddSingleton
   - Lifetime-relevante Services (KnowledgeBaseContext, KnowledgeBaseWriter, OllamaClient, Sidecar) → AddSingleton mit Disposable
   - Per-Session/Scope (RetrievalService, EmbeddingService) → AddScoped (wenn benoetigt) oder AddSingleton
2. Pro Service-Gruppe Tests schreiben (z.B. "ServiceProvider liefert korrekt verdrahteten KnowledgeBaseManager").

### Etappe 3: Aufrufer-Migration (~1 Tag)

1. Page-VMs auf Constructor-Injection umstellen: `public DataPageViewModel(IDialogService dialogs, IPdfImportService pdfImport, ...)`.
2. ShellViewModel.NavItems-Factory uebergibt nicht mehr `_sp` sondern `IServiceProvider` und resolvet pro Klick.
3. CodeBehind-Stellen (Window.xaml.cs) kommen schrittweise dran.

### Etappe 4: ServiceProvider-Cleanup (~2-4 h)

1. Sobald alle Aufrufer ueber DI gehen, kann `ServiceProvider` als reiner Compatibility-Shim bleiben oder geloescht werden.
2. `App.Services` wird zu `IServiceProvider` aus DI-Container.

---

## D. Risiken

| Risiko | Wirkung | Gegenmittel |
|---|---|---|
| Fehlerhafte Service-Lifecycle (Dispose) | Memory-Leaks oder Doppel-Dispose-Crash | AddSingleton + IDisposable testen |
| Zirkulaere Abhaengigkeiten | DI-Resolve-Crash zur Laufzeit | Pre-build-Validierung mit Microsoft.Extensions.DependencyInjection.Validation |
| Konstruktor-Injection brichtt PageVM-Konstruktoren | Kompile-Fehler in Views/DataTemplates | Schritt-fuer-Schritt mit Tests |
| KnowledgeBaseContext nicht thread-safe bei Singleton | Race-Condition in Tests | KnowledgeBaseWriter (Phase 2.2) deckt Schreib-Path ab; Lese-Path braucht weitere Pruefung |

---

## E. Empfehlung

**Phase 5.1 ist eine eigene Session, mit folgenden Voraussetzungen:**

1. **Explizite Freigabe** fuer NuGet `Microsoft.Extensions.DependencyInjection` (passend zu vorhandenem `Logging` 10.0.2 → Version 10.0.x)
2. **Branch oder eigener PR** — nicht in `feature/pdf-import-beobachtungen` mischen
3. **Etappen-Commits** statt Big-Bang
4. **Tests pro Etappe** (siehe C)
5. **App-Live-Test** zwischen jeder Etappe

Geschaetzter Aufwand: **2 Tage** (wie im Audit-Plan), realistisch eher **3-4 Tage** wenn Tests sauber gemacht werden.

In dieser Iteration: **dokumentierter Stand**, kein Code-Eingriff.

---

## F. Akzeptanz-Kriterium

Audit B2 (audit_claude.md / audit_gemini.md): *"App.Services-Locator statt DI"*.

- ServiceProvider als manueller DI-Container verifiziert, 678 Zeilen.
- 22 Files mit `App.Services`-Direktzugriff inventarisiert.
- Migrationspfad in 4 Etappen dokumentiert.
- Audit-Punkt **dokumentiert**, ⏸️ Migration in eigener Session mit NuGet-Freigabe.
