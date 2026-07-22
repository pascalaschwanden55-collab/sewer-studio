# BCA-Feincode Etappe 2 — Prüfplatz-Knopf „Bauart bestimmen"

> **Für agentische Umsetzer:** ERFORDERLICHER SUB-SKILL: `superpowers:executing-plans`
> (oder `subagent-driven-development`). Schritte nutzen Checkbox-Syntax (`- [ ]`).

**Goal:** Am Prüfplatz (TrainingStudio) gibt es einen Knopf „Bauart bestimmen". Ist ein Anschluss
im Bild, ruft er den `BcaFineCodeClassifier` und fügt die feinen Bauart-Codes (Quelle „bca") zur
vorhandenen Vorschlagsliste hinzu; der Nutzer wählt und bestätigt sie wie jeden anderen Vorschlag.

**Architecture:** Der Prüfplatz-Service (`AnnotationWorkbenchService`) bekommt eine neue Methode
`SuggestBcaBauartAsync` und einen optionalen `IBcaFineCodeClassifier?`. Die Fabrik
(`TrainingStudioWindowDependencyFactory`) erzeugt einen eigenen Qwen-`OllamaClient` und den
Classifier. Das ViewModel bekommt einen `RelayCommand`, der die neue Methode ruft und die
Kandidaten in `Suggestion` ergänzt. Der Knopf sitzt in `TrainingStudioWindow.xaml`.

**Tech Stack:** C# / .NET 10, WPF, CommunityToolkit.Mvvm, xUnit.

## Global Constraints

- Kein Eingriff in die automatische Analyse (nur Prüfplatz). Feincode ist Zusatz, nie Ersatz.
- Der Classifier ist **optional**: Ohne ihn (Fabrik-Rückfall) liefert `SuggestBcaBauartAsync` eine
  leere/unbrauchbare Suggestion und der Knopf bleibt wirkungslos, ohne Absturz.
- Qwen-Client-Lebenszyklus: Der `OllamaClient` gehört dem Classifier (owns) und wird über die
  vorhandene `IDisposable`-Kette des Prüfplatzes freigegeben (Fenster schliessen).
- Logik im Service, nicht im ViewModel; ViewModel bleibt dünn. Kommentare deutsch. Kein NuGet.
- Referenz-Muster: Qwen-Client-Aufbau wie `QuickScanSession`/`CreateQuickScanSession`;
  Dispose-Kette wie `AnnotationWorkbenchService.Dispose` (`(_x as IDisposable)?.Dispose()`).
- Nach jedem Task: betroffene Tests grün, Commit. Volle Solution-Build/UI.Tests nur, wenn die
  laufende App geschlossen ist (sonst DLL-Lock — dann Pipeline/Infrastructure/gefilterte UI-Tests).

---

### Task 1: BcaFineCodeClassifier entsorgbar machen (Lebenszyklus)

**Files:**
- Modify: `src/AuswertungPro.Next.Infrastructure/Ai/BcaFineCodeClassifier.cs`
- Test: `tests/AuswertungPro.Next.Pipeline.Tests/BcaFineCodeClassifierTests.cs` (ergänzen)

**Interfaces:**
- Produces: `BcaFineCodeClassifier` erhält Konstruktor-Überladung
  `BcaFineCodeClassifier(OllamaClient client, string model, bool ownsClient)` und implementiert
  `IDisposable` (gibt den Client nur bei `ownsClient` frei). Bestehender 2-Parameter-Ctor bleibt
  (delegiert mit `ownsClient: false`).

- [ ] **Step 1: Failing test schreiben** — ans Ende von `BcaFineCodeClassifierTests` einfügen:

```csharp
    [Fact]
    public void Dispose_gibt_eigenen_Client_frei_aber_nicht_den_fremden()
    {
        // Fremder Client (ownsClient=false, Default): bleibt nutzbar.
        var (sharedClient, sharedHttp) = FakeQwen("""
            { "code": "unsicher", "confidence": 0.0, "is_uncertain": true }
            """);
        using var _ = sharedHttp;
        var shared = new BcaFineCodeClassifier(sharedClient, "qwen-test");
        shared.Dispose();   // darf sharedClient NICHT schliessen
        // Kein Wurf beim Weiterverwenden des geteilten Clients:
        _ = sharedClient;   // Referenz bleibt gueltig

        // Eigener Client (ownsClient=true): wird beim Dispose geschlossen.
        var ownedHttp = new HttpClient(new StaticHandler("""
            { "code": "unsicher", "confidence": 0.0, "is_uncertain": true }
            """)) { BaseAddress = new Uri("http://localhost:11434") };
        var ownedClient = new OllamaClient(new Uri("http://localhost:11434"), ownedHttp);
        var owned = new BcaFineCodeClassifier(ownedClient, "qwen-test", ownsClient: true);
        owned.Dispose();   // schliesst ownedClient -> ownedHttp
        Assert.Throws<ObjectDisposedException>(() => ownedHttp.CancelPendingRequests());
    }
```

- [ ] **Step 2: Test fehlschlagen** — `dotnet test ...Pipeline.Tests... --filter "FullyQualifiedName~BcaFineCodeClassifierTests"` → FAIL (Ctor mit 3 Params / Dispose fehlt).

- [ ] **Step 3: Klasse anpassen** — in `BcaFineCodeClassifier.cs`:
  - Klassendeklaration: `public sealed class BcaFineCodeClassifier : IBcaFineCodeClassifier, IDisposable`.
  - Feld `private readonly bool _ownsClient;`.
  - Bestehenden Ctor umbauen zu `public BcaFineCodeClassifier(OllamaClient client, string model) : this(client, model, ownsClient: false) { }` und neuen Ctor:

```csharp
    public BcaFineCodeClassifier(OllamaClient client, string model, bool ownsClient)
    {
        _client = client ?? throw new ArgumentNullException(nameof(client));
        _model = model ?? throw new ArgumentNullException(nameof(model));
        _ownsClient = ownsClient;
    }

    public void Dispose()
    {
        if (_ownsClient)
            _client.Dispose();
    }
```

- [ ] **Step 4: Tests grün** — `dotnet test ...Pipeline.Tests... --filter "FullyQualifiedName~BcaFineCodeClassifierTests"` → PASS (6 Tests).

- [ ] **Step 5: Commit**

```bash
git add src/AuswertungPro.Next.Infrastructure/Ai/BcaFineCodeClassifier.cs tests/AuswertungPro.Next.Pipeline.Tests/BcaFineCodeClassifierTests.cs
git commit -m "feat(ai): BcaFineCodeClassifier entsorgbar (ownsClient) fuer Pruefplatz-Verdrahtung"
```

---

### Task 2: SuggestBcaBauartAsync im Prüfplatz-Service

**Files:**
- Modify: `src/AuswertungPro.Next.Application/Ai/Workbench/IAnnotationWorkbenchService.cs`
- Modify: `src/AuswertungPro.Next.UI/Services/AnnotationWorkbenchService.cs`
- Test: `tests/AuswertungPro.Next.UI.Tests/Ai/Workbench/AnnotationWorkbenchBcaBauartTests.cs` (neu)

**Interfaces:**
- Consumes: `IBcaFineCodeClassifier`, `BcaFineCodeSuggestion` (Etappe 1).
- Produces: `IAnnotationWorkbenchService.SuggestBcaBauartAsync(WorkbenchItem item, CancellationToken ct = default) -> Task<WorkbenchSuggestion>`.
  Kandidaten tragen `Quelle = "bca"`. Ohne Classifier oder bei `IsUncertain` → leere Kandidaten
  mit `FrameUsable: true` (kein Fehlerzustand), `QualityReason: ""`, `IsBend: false`.

- [ ] **Step 1: Failing test schreiben** — `tests/AuswertungPro.Next.UI.Tests/Ai/Workbench/AnnotationWorkbenchBcaBauartTests.cs`:

```csharp
using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using AuswertungPro.Next.Application.Ai;
using AuswertungPro.Next.Application.Ai.Workbench;
using AuswertungPro.Next.UI.Services;
using Xunit;

namespace AuswertungPro.Next.UI.Tests.Ai.Workbench;

public sealed class AnnotationWorkbenchBcaBauartTests
{
    private static WorkbenchItem Item() =>
        new("frame.png", "case1", 0, 0, null, null, null);

    [Fact]
    public async Task Ohne_Classifier_liefert_leere_Bauart_Kandidaten()
    {
        var sut = WorkbenchFactoryForTests.Create(bcaClassifier: null, frameBytes: [1, 2, 3]);

        var result = await sut.SuggestBcaBauartAsync(Item());

        Assert.Empty(result.Candidates);
        Assert.True(result.FrameUsable);
    }

    [Fact]
    public async Task Mit_Classifier_liefert_Bauart_Kandidaten_mit_Quelle_bca()
    {
        var classifier = new FakeClassifier(new BcaFineCodeSuggestion(
            new[] { new BcaFineCodeCandidate("BCAAA", 0.8) }, IsUncertain: false));
        var sut = WorkbenchFactoryForTests.Create(classifier, frameBytes: [1, 2, 3]);

        var result = await sut.SuggestBcaBauartAsync(Item());

        Assert.Single(result.Candidates);
        Assert.Equal("BCAAA", result.Candidates[0].VsaCode);
        Assert.Equal("bca", result.Candidates[0].Quelle);
    }

    private sealed class FakeClassifier(BcaFineCodeSuggestion answer) : IBcaFineCodeClassifier
    {
        public Task<BcaFineCodeSuggestion> SuggestAsync(string anschlussBildBase64, CancellationToken ct = default)
            => Task.FromResult(answer);
    }
}
```

  Ausserdem einen kleinen Test-Aufbauhelfer `tests/AuswertungPro.Next.UI.Tests/Ai/Workbench/WorkbenchFactoryForTests.cs`
  anlegen, der einen `AnnotationWorkbenchService` mit Fake-Abhängigkeiten baut (SAM/Pipeline/Store
  als minimale Fakes, `frameBytes`-Delegate, optionaler Classifier). **Referenz vor dem Schreiben:**
  bestehende Fakes/Muster in `tests/AuswertungPro.Next.UI.Tests/Ai/Workbench/TrainingStudioViewModelTests.cs`
  wiederverwenden; nur die für `SuggestBcaBauartAsync` nötigen Abhängigkeiten müssen echt sein
  (`readFileBytes`, `bcaClassifier`), der Rest darf werfen/leer sein, da diese Methode sie nicht nutzt.

- [ ] **Step 2: Test fehlschlagen** — `dotnet test ...UI.Tests... --filter "FullyQualifiedName~AnnotationWorkbenchBcaBauartTests"` → FAIL (Methode/Parameter fehlen).

- [ ] **Step 3: Interface + Service erweitern**

  In `IAnnotationWorkbenchService.cs` nach `SuggestAsync` einfügen:

```csharp
    /// <summary>
    /// Fragt Qwen nach der feinen Anschluss-Bauart (nur wenn ein Anschluss im Bild ist).
    /// Kandidaten mit Quelle "bca"; ohne Classifier oder bei Unsicherheit leer.
    /// </summary>
    Task<WorkbenchSuggestion> SuggestBcaBauartAsync(WorkbenchItem item, CancellationToken ct = default);
```

  In `AnnotationWorkbenchService.cs`: neues Feld `private readonly IBcaFineCodeClassifier? _bcaClassifier;`,
  Konstruktor-Parameter `IBcaFineCodeClassifier? bcaClassifier = null` (ans Ende, mit Zuweisung),
  im `Dispose()` ergänzen: `(_bcaClassifier as IDisposable)?.Dispose();`. Neue Methode:

```csharp
    public async Task<WorkbenchSuggestion> SuggestBcaBauartAsync(WorkbenchItem item, CancellationToken ct = default)
    {
        if (_bcaClassifier is null)
            return new WorkbenchSuggestion(Array.Empty<WorkbenchCodeCandidate>(), true, string.Empty, false);

        var b64 = Convert.ToBase64String(_readFileBytes(item.FramePath));
        var suggestion = await _bcaClassifier.SuggestAsync(b64, ct).ConfigureAwait(false);

        var candidates = suggestion.Candidates
            .Select(c => new WorkbenchCodeCandidate(c.VsaCode, c.Confidence, "bca"))
            .ToList();
        return new WorkbenchSuggestion(candidates, true, string.Empty, false);
    }
```

  `using AuswertungPro.Next.Application.Ai;` ist bereits vorhanden.

- [ ] **Step 4: Tests grün** — `dotnet test ...UI.Tests... --filter "FullyQualifiedName~AnnotationWorkbenchBcaBauartTests"` → PASS (2 Tests). Falls die App läuft, ggf. vorher schliessen (UI.Tests-Build braucht das UI-bin).

- [ ] **Step 5: Commit**

```bash
git add src/AuswertungPro.Next.Application/Ai/Workbench/IAnnotationWorkbenchService.cs src/AuswertungPro.Next.UI/Services/AnnotationWorkbenchService.cs tests/AuswertungPro.Next.UI.Tests/Ai/Workbench/
git commit -m "feat(ai): SuggestBcaBauartAsync am Pruefplatz-Service (Bauart-Kandidaten Quelle bca)"
```

---

### Task 3: Verdrahtung — Qwen-Client + Classifier in der Fabrik

**Files:**
- Modify: `src/AuswertungPro.Next.UI/Services/TrainingStudioWindowDependencyFactory.cs`

**Interfaces:**
- Consumes: `BcaFineCodeClassifier` (owns-Ctor, Task 1), `AnnotationWorkbenchService` (Task 2),
  `OllamaClient`, `AiRuntimeSettings` (Bestand: `services?.AiSettings.Load()` liefert Ollama-URI + VisionModel).

- [ ] **Step 1: Classifier in `Create(...)` bauen und injizieren** — in der privaten
  `Create(ServiceProvider?, IVisionPipelineClient)`-Methode vor dem `return new AnnotationWorkbenchService(...)`:

```csharp
        // 7) Feiner Anschluss-Code (BCA-Bauart) via eigenem Qwen-Client. Nur wenn KI-Settings da sind;
        //    sonst null -> Knopf bleibt wirkungslos. Der Client gehoert dem Classifier (owns) und
        //    wird ueber die Dispose-Kette des Workbench-Service freigegeben.
        IBcaFineCodeClassifier? bcaClassifier = null;
        var aiCfg = services?.AiSettings.Load();
        if (aiCfg is { Enabled: true } && !string.IsNullOrWhiteSpace(aiCfg.OllamaBaseUri) && !string.IsNullOrWhiteSpace(aiCfg.VisionModel))
        {
            var ollama = new OllamaClient(
                new Uri(aiCfg.OllamaBaseUri),
                httpClient: null,
                ownedTimeout: TimeSpan.FromSeconds(90),
                keepAlive: aiCfg.OllamaKeepAlive,
                numCtx: aiCfg.OllamaNumCtx);
            bcaClassifier = new BcaFineCodeClassifier(ollama, aiCfg.VisionModel, ownsClient: true);
        }
```

  und den Konstruktoraufruf um `bcaClassifier` als letztes Argument ergänzen. Nötige `using`:
  `AuswertungPro.Next.Infrastructure.Ai;` (OllamaClient, BcaFineCodeClassifier) am Kopf ergänzen.

  **Vor dem Umsetzen prüfen:** die exakte `OllamaClient`-Ctor-Signatur
  (`OllamaClient(Uri baseUri, HttpClient? http = null, TimeSpan? ownedTimeout = null, string keepAlive = "24h", int numCtx = 0)`)
  und die Feldnamen von `AiRuntimeSettings` (`OllamaBaseUri`, `VisionModel`, `OllamaKeepAlive`,
  `OllamaNumCtx`, `Enabled`) — bei Abweichung anpassen.

- [ ] **Step 2: Build prüfen** — App schliessen, dann `dotnet build src/AuswertungPro.Next.UI/AuswertungPro.Next.UI.csproj` → 0 Fehler/0 Warnungen. (Reiner Verdrahtungscode; getestet indirekt über Task 4.)

- [ ] **Step 3: Commit**

```bash
git add src/AuswertungPro.Next.UI/Services/TrainingStudioWindowDependencyFactory.cs
git commit -m "feat(ai): Pruefplatz-Fabrik verdrahtet Qwen-BcaFineCodeClassifier (owns, dispose-sicher)"
```

---

### Task 4: ViewModel-Command + Knopf

**Files:**
- Modify: `src/AuswertungPro.Next.UI/ViewModels/TrainingStudioViewModel.cs`
- Modify: `src/AuswertungPro.Next.UI/Views/Windows/TrainingStudioWindow.xaml`
- Test: `tests/AuswertungPro.Next.UI.Tests/Ai/Workbench/TrainingStudioViewModelTests.cs` (ergänzen + Fake anpassen)

**Interfaces:**
- Consumes: `IAnnotationWorkbenchService.SuggestBcaBauartAsync` (Task 2).
- Produces: `TrainingStudioViewModel.BestimmeBauartCommand` (`IAsyncRelayCommand`), das die
  Bauart-Kandidaten in `Suggestion` einmischt (bestehende `SuggestionCandidates` zeigt sie an).

- [ ] **Step 1: Fake im bestehenden Test anpassen** — der Fake `IAnnotationWorkbenchService` in
  `TrainingStudioViewModelTests.cs` muss die neue Methode implementieren. Minimal:

```csharp
    public Task<WorkbenchSuggestion> SuggestBcaBauartAsync(WorkbenchItem item, CancellationToken ct = default)
        => Task.FromResult(new WorkbenchSuggestion(
            new[] { new WorkbenchCodeCandidate("BCAAA", 0.8, "bca") }, true, string.Empty, false));
```

- [ ] **Step 2: Failing test schreiben** — neuer Test in `TrainingStudioViewModelTests.cs`:

```csharp
    [Fact]
    public async Task BestimmeBauart_fuegt_Bauart_Kandidaten_zur_Vorschlagsliste()
    {
        var vm = CreateViewModelWithCurrentBox();   // vorhandener/analoger Aufbauhelfer mit CurrentItem+CurrentBox

        await vm.BestimmeBauartCommand.ExecuteAsync(null);

        Assert.Contains(vm.SuggestionCandidates, c => c.VsaCode == "BCAAA" && c.Quelle == "bca");
    }
```

  (Wenn kein solcher Helfer existiert: minimal `vm.Items`/`vm.CurrentIndex`/`vm.CurrentBox` im Test
  setzen, sodass `CurrentItem` != null ist. Muster aus den vorhandenen Tests derselben Datei.)

- [ ] **Step 3: Test fehlschlagen** — `dotnet test ...UI.Tests... --filter "FullyQualifiedName~TrainingStudioViewModelTests"` → FAIL (Command fehlt).

- [ ] **Step 4: Command im ViewModel** — in `TrainingStudioViewModel.cs`:

```csharp
    [RelayCommand]
    private async Task BestimmeBauartAsync()
    {
        var item = CurrentItem;
        if (item is null) return;

        using var cts = new CancellationTokenSource();
        var bauart = await _workbench.SuggestBcaBauartAsync(item, cts.Token).ConfigureAwait(true);
        if (bauart.Candidates.Count == 0)
        {
            StatusText = "Keine sichere Anschluss-Bauart erkannt.";
            return;
        }

        // Bauart-Kandidaten zur bestehenden Liste hinzufuegen (Quelle "bca"), Duplikate vermeiden.
        var vorhanden = Suggestion?.Candidates ?? (IReadOnlyList<WorkbenchCodeCandidate>)Array.Empty<WorkbenchCodeCandidate>();
        var merged = vorhanden
            .Concat(bauart.Candidates)
            .GroupBy(c => c.VsaCode, StringComparer.OrdinalIgnoreCase)
            .Select(g => g.OrderByDescending(c => c.Confidence).First())
            .OrderByDescending(c => c.Confidence)
            .ToList();
        Suggestion = new WorkbenchSuggestion(
            merged,
            Suggestion?.FrameUsable ?? true,
            Suggestion?.QualityReason ?? string.Empty,
            Suggestion?.IsBend ?? false);
        StatusText = "Anschluss-Bauart vorgeschlagen.";
    }
```

- [ ] **Step 5: Test grün** — `dotnet test ...UI.Tests... --filter "FullyQualifiedName~TrainingStudioViewModelTests"` → PASS.

- [ ] **Step 6: Knopf in `TrainingStudioWindow.xaml`** — im Codier-Panel (bei den bestehenden
  Aktionen/Vorschlägen) einen Knopf einfügen. Style/Brushes wie im Fenster üblich (`sewer-wpf-ui`):

```xml
<Button Content="Bauart bestimmen"
        Command="{Binding BestimmeBauartCommand}"
        ToolTip="Fragt die KI nach der Anschluss-Bauart (nur bei Anschlüssen sinnvoll)."
        Margin="0,6,0,0"/>
```

  Nach dem Einfügen `xaml-binding-checker` gedanklich anwenden: `BestimmeBauartCommand` existiert
  als generiertes Command (aus `[RelayCommand] BestimmeBauartAsync`). Passt.

- [ ] **Step 7: Build + Tests** — App schliessen, `dotnet build src/AuswertungPro.Next.UI/...` (0/0),
  `dotnet test ...UI.Tests... --filter "FullyQualifiedName~TrainingStudioViewModelTests"` grün.

- [ ] **Step 8: Commit**

```bash
git add src/AuswertungPro.Next.UI/ViewModels/TrainingStudioViewModel.cs src/AuswertungPro.Next.UI/Views/Windows/TrainingStudioWindow.xaml tests/AuswertungPro.Next.UI.Tests/Ai/Workbench/TrainingStudioViewModelTests.cs
git commit -m "feat(ui): Pruefplatz-Knopf 'Bauart bestimmen' fuegt BCA-Feincodes zur Vorschlagsliste"
```

---

## Selbst-Review (gegen Design §4)

1. **Abdeckung:** Knopf am Prüfplatz (Task 4) → `SuggestBcaBauartAsync` (Task 2) → Classifier
   (Task 1 Lebenszyklus, Task 3 Verdrahtung). Bestätigung/Speichern nutzt den bestehenden Save-Weg
   (SelectedCode → SaveAsync) — kein neuer Code nötig. Auto-Analyse unberührt (Constraint).
2. **Placeholder:** Kein „TBD"; Code je Step vorhanden. Zwei „vor dem Umsetzen prüfen"-Hinweise
   (OllamaClient-Signatur, Test-Aufbauhelfer) sind bewusste Verifikationspunkte, keine Lücken.
3. **Typ-Konsistenz:** `SuggestBcaBauartAsync(WorkbenchItem, CancellationToken)` identisch in
   Interface, Impl, Fakes, ViewModel-Aufruf. `WorkbenchCodeCandidate(VsaCode, Confidence, Quelle)`
   mit Quelle „bca" durchgehend.
4. **Risiko-Hinweis:** UI.Tests/Build brauchen die geschlossene App (DLL-Lock) — in den Steps notiert.
