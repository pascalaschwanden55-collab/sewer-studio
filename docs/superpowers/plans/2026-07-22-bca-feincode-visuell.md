# BCA-Feincode (visuell) — Implementierungsplan

> **Für agentische Umsetzer:** ERFORDERLICHER SUB-SKILL: `superpowers:subagent-driven-development`
> (empfohlen) oder `superpowers:executing-plans`, um diesen Plan Task für Task umzusetzen.
> Schritte nutzen Checkbox-Syntax (`- [ ]`) zur Nachverfolgung.

**Goal:** Ein neuer Dienst schlägt zu einem erkannten Anschluss (grobe Familie BCA) den feinen
Anschluss-Code (Bauart + offen/verschlossen) vor — visuell über einen fokussierten Qwen-Aufruf
mit strengem JSON-Schema; bei Unsicherheit oder Fehler bleibt es beim groben Code.

**Architecture:** Vertrag in Application (`IBcaFineCodeClassifier` + DTOs), Umsetzung in
Infrastructure (`BcaFineCodeClassifier`), die den vorhandenen `OllamaClient` mit einem festen
16-Codes-Enum-Schema aufruft (Muster: `EnhancedVisionAnalysisService`). Der Dienst ist isoliert
testbar über einen Fake-`HttpMessageHandler` (Muster: `StaticOllamaHandler`). Kein UI-, kein
Pipeline-Bezug in diesem Plan.

**Tech Stack:** C# / .NET 10, System.Text.Json, xUnit. Kein neues NuGet.

## Global Constraints

- Vertrag/DTOs in `AuswertungPro.Next.Application.Ai`, Umsetzung in
  `AuswertungPro.Next.Infrastructure.Ai`. Application enthält keine Transporttypen
  (kein `System.Net.Http`, keine `HttpClient`/`HttpRequestException`).
- Qwen-Ausgabe strikt über JSON-Schema (Enum), **kein Freitext**.
- Der feine Code ist immer ein Zusatz, nie ein Ersatz: Bei Fehler/Unsicherheit liefert der Dienst
  eine leere Kandidatenliste, wirft nicht.
- Kommentare auf Deutsch. Kein neues NuGet ohne Rückfrage.
- Die 16 gültigen BCA-Feincodes (exakt, aus `vsa_kek_2020_catalog_manifest.json` verifiziert):
  `BCAAA BCAAB BCABA BCABB BCACA BCACB BCADA BCADB BCAEA BCAEB BCAFA BCAFB BCAGA BCAGB BCAZA BCAZB`.
- Deterministische Ollama-Optionen wie im Bestand: `OllamaDeterministicOptions.Create()`.
- Nach jedem Task: `dotnet build AuswertungPro.sln` + betroffene Tests grün, dann Commit.

---

### Task 1: Vertrag + DTOs (Application/Ai)

**Files:**
- Create: `src/AuswertungPro.Next.Application/Ai/BcaFineCodeModels.cs`
- Test: `tests/AuswertungPro.Next.Pipeline.Tests/BcaFineCodeContractTests.cs`

**Interfaces:**
- Produces:
  - `interface IBcaFineCodeClassifier { Task<BcaFineCodeSuggestion> SuggestAsync(string anschlussBildBase64, CancellationToken ct = default); }`
  - `sealed record BcaFineCodeCandidate(string VsaCode, double Confidence);`
  - `sealed record BcaFineCodeSuggestion(IReadOnlyList<BcaFineCodeCandidate> Candidates, bool IsUncertain)` mit
    `public static BcaFineCodeSuggestion Uncertain { get; } = new(Array.Empty<BcaFineCodeCandidate>(), true);`

- [ ] **Step 1: Failing test schreiben**

`tests/AuswertungPro.Next.Pipeline.Tests/BcaFineCodeContractTests.cs`:

```csharp
using System;
using AuswertungPro.Next.Application.Ai;
using Xunit;

namespace AuswertungPro.Next.Pipeline.Tests;

public sealed class BcaFineCodeContractTests
{
    [Fact]
    public void Uncertain_ist_leer_und_markiert_unsicher()
    {
        var s = BcaFineCodeSuggestion.Uncertain;

        Assert.True(s.IsUncertain);
        Assert.Empty(s.Candidates);
    }

    [Fact]
    public void Suggestion_haelt_kandidaten_in_uebergebener_reihenfolge()
    {
        var s = new BcaFineCodeSuggestion(
            new[] { new BcaFineCodeCandidate("BCAAA", 0.7), new BcaFineCodeCandidate("BCAEA", 0.2) },
            IsUncertain: false);

        Assert.False(s.IsUncertain);
        Assert.Equal("BCAAA", s.Candidates[0].VsaCode);
        Assert.Equal(0.2, s.Candidates[1].Confidence);
    }
}
```

- [ ] **Step 2: Test fehlschlagen lassen**

Run: `dotnet test tests/AuswertungPro.Next.Pipeline.Tests/AuswertungPro.Next.Pipeline.Tests.csproj --filter "FullyQualifiedName~BcaFineCodeContractTests"`
Expected: FAIL (Kompilierfehler — `BcaFineCodeSuggestion` existiert nicht).

- [ ] **Step 3: Vertrag + DTOs schreiben**

`src/AuswertungPro.Next.Application/Ai/BcaFineCodeModels.cs`:

```csharp
using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace AuswertungPro.Next.Application.Ai;

/// <summary>Ein feiner Anschluss-Code-Vorschlag (Bauart) mit Konfidenz.</summary>
public sealed record BcaFineCodeCandidate(string VsaCode, double Confidence);

/// <summary>
/// Ergebnis der feinen Anschluss-Codierung: absteigend sortierte Kandidaten, oder
/// <see cref="IsUncertain"/> = true, wenn keine sichere Bauart bestimmbar war.
/// </summary>
public sealed record BcaFineCodeSuggestion(
    IReadOnlyList<BcaFineCodeCandidate> Candidates,
    bool IsUncertain)
{
    /// <summary>Kein sicherer Feincode — der grobe Code BCA bleibt bestehen.</summary>
    public static BcaFineCodeSuggestion Uncertain { get; } =
        new(Array.Empty<BcaFineCodeCandidate>(), true);
}

/// <summary>
/// Bestimmt zu einem erkannten Anschluss den feinen VSA-Code (Bauart + offen/verschlossen)
/// aus dem Bild. Reiner Zusatz: bei Fehler/Unsicherheit werden leere Kandidaten geliefert.
/// </summary>
public interface IBcaFineCodeClassifier
{
    Task<BcaFineCodeSuggestion> SuggestAsync(string anschlussBildBase64, CancellationToken ct = default);
}
```

- [ ] **Step 4: Test bestehen lassen**

Run: `dotnet test tests/AuswertungPro.Next.Pipeline.Tests/AuswertungPro.Next.Pipeline.Tests.csproj --filter "FullyQualifiedName~BcaFineCodeContractTests"`
Expected: PASS (2 Tests).

- [ ] **Step 5: Commit**

```bash
git add src/AuswertungPro.Next.Application/Ai/BcaFineCodeModels.cs tests/AuswertungPro.Next.Pipeline.Tests/BcaFineCodeContractTests.cs
git commit -m "feat(ai): Vertrag + DTOs fuer feinen Anschluss-Code (BCA)"
```

---

### Task 2: BcaFineCodeClassifier (Infrastructure) mit Qwen-Enum-Schema

**Files:**
- Create: `src/AuswertungPro.Next.Infrastructure/Ai/BcaFineCodeClassifier.cs`
- Test: `tests/AuswertungPro.Next.Pipeline.Tests/BcaFineCodeClassifierTests.cs`

**Interfaces:**
- Consumes: `IBcaFineCodeClassifier`, `BcaFineCodeSuggestion`, `BcaFineCodeCandidate` (Task 1);
  `OllamaClient`, `OllamaClient.ChatMessage`, `OllamaClient.ChatStructuredWithOptionsAsync<T>`,
  `OllamaDeterministicOptions.Create()` (Bestand).
- Produces: `sealed class BcaFineCodeClassifier(OllamaClient client, string model) : IBcaFineCodeClassifier`.

**Referenz vor dem Start lesen:** `src/AuswertungPro.Next.Infrastructure/Ai/EnhancedVisionAnalysisService.cs`
(Schema als `JsonElement`, `ChatStructuredWithOptionsAsync`, DTO-Mapping, try/catch mit
`OperationCanceledException`) und den Fake `StaticOllamaHandler` in
`tests/AuswertungPro.Next.Pipeline.Tests/EnhancedVisionAnalysisServiceTests.cs`.

- [ ] **Step 1: Failing test schreiben — Qwen liefert eine Bauart**

`tests/AuswertungPro.Next.Pipeline.Tests/BcaFineCodeClassifierTests.cs`:

```csharp
using System;
using System.Net;
using System.Net.Http;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using AuswertungPro.Next.Infrastructure.Ai;
using Xunit;

namespace AuswertungPro.Next.Pipeline.Tests;

public sealed class BcaFineCodeClassifierTests
{
    private static (OllamaClient client, HttpClient http) FakeQwen(string structuredContent)
    {
        var http = new HttpClient(new StaticHandler(structuredContent))
        {
            BaseAddress = new Uri("http://localhost:11434")
        };
        return (new OllamaClient(new Uri("http://localhost:11434"), http), http);
    }

    [Fact]
    public async Task Liefert_Bauart_Kandidat_aus_Qwen_Antwort()
    {
        var (client, http) = FakeQwen("""
            { "code": "BCAAA", "confidence": 0.8, "is_uncertain": false }
            """);
        using var _ = http;
        var sut = new BcaFineCodeClassifier(client, "qwen-test");

        var result = await sut.SuggestAsync(Convert.ToBase64String([1, 2, 3]));

        Assert.False(result.IsUncertain);
        Assert.Single(result.Candidates);
        Assert.Equal("BCAAA", result.Candidates[0].VsaCode);
        Assert.Equal(0.8, result.Candidates[0].Confidence);
    }

    [Fact]
    public async Task Unsicheres_Qwen_Ergebnis_liefert_leere_Kandidaten()
    {
        var (client, http) = FakeQwen("""
            { "code": "unsicher", "confidence": 0.0, "is_uncertain": true }
            """);
        using var _ = http;
        var sut = new BcaFineCodeClassifier(client, "qwen-test");

        var result = await sut.SuggestAsync(Convert.ToBase64String([1, 2, 3]));

        Assert.True(result.IsUncertain);
        Assert.Empty(result.Candidates);
    }

    [Fact]
    public async Task Unbekannter_Code_wird_als_unsicher_behandelt()
    {
        // Qwen liefert einen Code ausserhalb der 16 gueltigen BCA-Feincodes.
        var (client, http) = FakeQwen("""
            { "code": "BABBA", "confidence": 0.9, "is_uncertain": false }
            """);
        using var _ = http;
        var sut = new BcaFineCodeClassifier(client, "qwen-test");

        var result = await sut.SuggestAsync(Convert.ToBase64String([1, 2, 3]));

        Assert.True(result.IsUncertain);
        Assert.Empty(result.Candidates);
    }

    [Fact]
    public async Task Transportfehler_liefert_leere_Kandidaten_ohne_Wurf()
    {
        var http = new HttpClient(new ThrowingHandler())
        {
            BaseAddress = new Uri("http://localhost:11434")
        };
        using var _ = http;
        var client = new OllamaClient(new Uri("http://localhost:11434"), http);
        var sut = new BcaFineCodeClassifier(client, "qwen-test");

        var result = await sut.SuggestAsync(Convert.ToBase64String([1, 2, 3]));

        Assert.True(result.IsUncertain);
        Assert.Empty(result.Candidates);
    }

    [Fact]
    public async Task Prompt_enthaelt_die_gueltigen_Bauart_Codes()
    {
        var (client, http) = FakeQwen("""
            { "code": "unsicher", "confidence": 0.0, "is_uncertain": true }
            """);
        using var _ = http;
        var sut = new BcaFineCodeClassifier(client, "qwen-test");

        await sut.SuggestAsync(Convert.ToBase64String([1, 2, 3]));

        Assert.Contains("BCAAA", StaticHandler.LastRequestJson);
        Assert.Contains("BCAEA", StaticHandler.LastRequestJson);
    }

    private sealed class StaticHandler(string structuredContent) : HttpMessageHandler
    {
        public static string LastRequestJson { get; private set; } = "";

        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request, CancellationToken cancellationToken)
        {
            LastRequestJson = request.Content?.ReadAsStringAsync(cancellationToken).GetAwaiter().GetResult() ?? "";
            var responseJson = $$"""
                { "message": { "role": "assistant",
                  "content": {{System.Text.Json.JsonSerializer.Serialize(structuredContent)}} } }
                """;
            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(responseJson, Encoding.UTF8, "application/json")
            });
        }
    }

    private sealed class ThrowingHandler : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request, CancellationToken cancellationToken)
            => throw new HttpRequestException("connection refused");
    }
}
```

- [ ] **Step 2: Test fehlschlagen lassen**

Run: `dotnet test tests/AuswertungPro.Next.Pipeline.Tests/AuswertungPro.Next.Pipeline.Tests.csproj --filter "FullyQualifiedName~BcaFineCodeClassifierTests"`
Expected: FAIL (Kompilierfehler — `BcaFineCodeClassifier` existiert nicht).

- [ ] **Step 3: Implementierung schreiben**

`src/AuswertungPro.Next.Infrastructure/Ai/BcaFineCodeClassifier.cs`:

```csharp
using System;
using System.Collections.Generic;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading;
using System.Threading.Tasks;
using AuswertungPro.Next.Application.Ai;

namespace AuswertungPro.Next.Infrastructure.Ai;

/// <summary>
/// Bestimmt den feinen Anschluss-Code (Bauart + offen/verschlossen) aus dem Bild ueber einen
/// fokussierten Qwen-Aufruf mit striktem 16-Codes-Enum-Schema. Reiner Zusatz: bei Fehler,
/// Zeitueberschreitung, "unsicher" oder unbekanntem Code werden leere Kandidaten geliefert.
/// Muster wie <see cref="EnhancedVisionAnalysisService"/>.
/// </summary>
public sealed class BcaFineCodeClassifier : IBcaFineCodeClassifier
{
    // Die 16 gueltigen BCA-Feincodes (Bauart A-G,Z je offen/verschlossen) plus "unsicher".
    private static readonly IReadOnlySet<string> ValidCodes = new HashSet<string>(StringComparer.Ordinal)
    {
        "BCAAA","BCAAB","BCABA","BCABB","BCACA","BCACB","BCADA","BCADB",
        "BCAEA","BCAEB","BCAFA","BCAFB","BCAGA","BCAGB","BCAZA","BCAZB",
    };

    private static readonly JsonElement Schema = JsonDocument.Parse("""
    {
      "type": "object",
      "additionalProperties": false,
      "properties": {
        "code": { "type": "string",
          "enum": ["BCAAA","BCAAB","BCABA","BCABB","BCACA","BCACB","BCADA","BCADB",
                   "BCAEA","BCAEB","BCAFA","BCAFB","BCAGA","BCAGB","BCAZA","BCAZB","unsicher"] },
        "confidence": { "type": "number", "minimum": 0, "maximum": 1 },
        "is_uncertain": { "type": "boolean" }
      },
      "required": ["code", "is_uncertain"]
    }
    """).RootElement.Clone();

    private const string Prompt =
        "Du siehst das Bild eines seitlichen Rohranschlusses (VSA-Familie BCA). Bestimme die " +
        "Bauart und ob der Anschluss offen oder verschlossen ist. Antworte NUR mit einem der " +
        "Codes:\n" +
        "BCAAA Anschluss mit Formstueck; BCAAB dito verschlossen;\n" +
        "BCABA Sattelanschluss gebohrt; BCABB dito verschlossen;\n" +
        "BCACA Sattelanschluss eingespitzt; BCACB dito verschlossen;\n" +
        "BCADA Anschluss gebohrt; BCADB dito verschlossen;\n" +
        "BCAEA Anschluss eingespitzt; BCAEB dito verschlossen;\n" +
        "BCAFA Spezialanschluss; BCAFB dito verschlossen;\n" +
        "BCAGA Anschluss unbekannter Bauart; BCAGB dito verschlossen;\n" +
        "BCAZA andersartiger Anschluss; BCAZB dito verschlossen.\n" +
        "Wenn die Bauart nicht sicher erkennbar ist, setze code='unsicher' und is_uncertain=true.";

    private static readonly TimeSpan Timeout = TimeSpan.FromSeconds(60);

    private readonly OllamaClient _client;
    private readonly string _model;

    public BcaFineCodeClassifier(OllamaClient client, string model)
    {
        _client = client ?? throw new ArgumentNullException(nameof(client));
        _model = model ?? throw new ArgumentNullException(nameof(model));
    }

    public async Task<BcaFineCodeSuggestion> SuggestAsync(
        string anschlussBildBase64, CancellationToken ct = default)
    {
        BcaFineCodeDto dto;
        try
        {
            using var cts = CancellationTokenSource.CreateLinkedTokenSource(ct);
            cts.CancelAfter(Timeout);
            dto = await _client.ChatStructuredWithOptionsAsync<BcaFineCodeDto>(
                model: _model,
                messages: [new OllamaClient.ChatMessage("user", Prompt, [anschlussBildBase64])],
                formatSchema: Schema,
                options: OllamaDeterministicOptions.Create(),
                ct: cts.Token).ConfigureAwait(false);
        }
        catch (OperationCanceledException) when (!ct.IsCancellationRequested)
        {
            return BcaFineCodeSuggestion.Uncertain;   // Zeitueberschreitung -> grober Code bleibt
        }
        catch (OperationCanceledException) { throw; }
        catch (Exception)
        {
            return BcaFineCodeSuggestion.Uncertain;   // Transport-/Modellfehler -> grober Code bleibt
        }

        var code = dto.Code?.Trim().ToUpperInvariant();
        if (dto.IsUncertain || code is null || !ValidCodes.Contains(code))
            return BcaFineCodeSuggestion.Uncertain;

        var confidence = Math.Clamp(dto.Confidence ?? 0.0, 0.0, 1.0);
        return new BcaFineCodeSuggestion(
            new[] { new BcaFineCodeCandidate(code, confidence) },
            IsUncertain: false);
    }

    private sealed record BcaFineCodeDto(
        [property: JsonPropertyName("code")] string? Code,
        [property: JsonPropertyName("confidence")] double? Confidence,
        [property: JsonPropertyName("is_uncertain")] bool IsUncertain);
}
```

- [ ] **Step 4: Tests bestehen lassen**

Run: `dotnet test tests/AuswertungPro.Next.Pipeline.Tests/AuswertungPro.Next.Pipeline.Tests.csproj --filter "FullyQualifiedName~BcaFineCodeClassifierTests"`
Expected: PASS (5 Tests).

- [ ] **Step 5: Volle Solution grün prüfen**

Run: `dotnet build AuswertungPro.sln` (0 Fehler, 0 Warnungen) und
`dotnet test AuswertungPro.sln --no-build` (alles grün).

- [ ] **Step 6: Commit**

```bash
git add src/AuswertungPro.Next.Infrastructure/Ai/BcaFineCodeClassifier.cs tests/AuswertungPro.Next.Pipeline.Tests/BcaFineCodeClassifierTests.cs
git commit -m "feat(ai): BcaFineCodeClassifier — feiner Anschluss-Code aus Qwen-Enum-Schema"
```

---

## Folgepläne (nicht Teil dieses Plans — je eigener Plan)

Dieser Plan liefert den isoliert testbaren Dienst. Zwei Etappen folgen als eigene Pläne, sobald
dieser Dienst steht:

- **Etappe 2 — Prüfplatz-Andockung:** Erkennt der Prüfplatz (TrainingStudio) einen Anschluss,
  ruft er `IBcaFineCodeClassifier` und zeigt den Vorschlag als zusätzlichen
  `WorkbenchCodeCandidate`; Bestätigung fliesst in die bestehende Speicherlogik (Trainingsmaterial).
  Erfordert vorher: Erkundung, wie der Prüfplatz seinen Vorschlag erzeugt und mit welchem
  `OllamaClient` der Dienst dort verdrahtet wird (Fabrik im ServiceProvider, Muster
  `CreateQuickScanSession`). Die Auto-Analyse bleibt bewusst unberührt.
- **Etappe 3 — Messung:** BCA-Feincode-Trefferquote am eingefrorenen 120er-Eval vorher/nachher.

---

## Selbst-Review (gegen die Spec)

1. **Spec-Abdeckung:** Dienst + Enum-Schema + Fallback (Spec §3, §5) → Task 1+2. Messung (§6) und
   Prüfplatz (§4) sind bewusst Folgepläne (isolierte Teilsysteme, Skill-konform).
2. **Placeholder-Scan:** Kein „TBD"/„TODO"; jeder Code-Step enthält vollständigen Code.
3. **Typ-Konsistenz:** `BcaFineCodeSuggestion`/`BcaFineCodeCandidate`/`IBcaFineCodeClassifier`
   identisch in Task 1 (Definition), Task 2 (Nutzung) und den Tests. Methode durchgehend
   `SuggestAsync(string, CancellationToken)`.
4. **Ambiguität:** „unsicher", unbekannter Code, Timeout und Transportfehler führen alle zu
   `BcaFineCodeSuggestion.Uncertain` — je ein eigener Test.
