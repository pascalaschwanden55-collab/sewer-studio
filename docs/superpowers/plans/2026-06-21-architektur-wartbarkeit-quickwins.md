# Architektur-Wartbarkeit Quick-Wins Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Schliessen der wartbarkeitsnahen Null-/Niedrigrisiko-Punkte aus der Architektur-Verifikation: Sidecar-Token-Falle, ENV-Doku, C#↔Sidecar-Contract-Drift und veraltete CLAUDE.md-Aussagen.

**Architecture:** Keine Grossrefaktorierung und keine UI-God-Class-Zerlegung in diesem Durchlauf. Token-Aufloesung wird als kleine zentrale Helper-Klasse in Infrastructure umgesetzt und von C#-Client sowie Startup genutzt. Contract-Drift wird mit einem statischen Test abgesichert, der Pydantic-Feldnamen aus den Sidecar-Schemas gegen C#-DTO-JSON-Namen vergleicht.

**Tech Stack:** .NET 10, xUnit, C# Reflection, Python/Pydantic-Schema-Dateien als Textquelle, Markdown-Doku.

---

### Task 1: Sidecar-Token-Aufloesung zentralisieren und Alias-Bug schliessen

**Files:**
- Create: `src/AuswertungPro.Next.Infrastructure/Ai/Pipeline/SidecarTokenResolver.cs`
- Modify: `src/AuswertungPro.Next.Infrastructure/Ai/Pipeline/VisionPipelineClient.cs`
- Modify: `src/AuswertungPro.Next.UI/Services/AiStartupService.cs`
- Test: `tests/AuswertungPro.Next.Pipeline.Tests/VisionPipelineClientTests.cs`

- [x] **Step 1: Failing test fuer `SEWERSTUDIO_SIDECAR_TOKEN` schreiben**

Add to `tests/AuswertungPro.Next.Pipeline.Tests/VisionPipelineClientTests.cs`:

```csharp
[Fact]
public async Task ClassifyYoloAsync_UsesSewerStudioSidecarTokenEnvironmentAlias()
{
    var previousCanonical = Environment.GetEnvironmentVariable("SEWERSTUDIO_SIDECAR_TOKEN");
    var previousCompat = Environment.GetEnvironmentVariable("AUSWERTUNGPRO_SIDECAR_TOKEN");
    var previousAuth = Environment.GetEnvironmentVariable("SEWER_SIDECAR_AUTH_TOKEN");
    var previousLegacy = Environment.GetEnvironmentVariable("SEWER_SIDECAR_TOKEN");
    Environment.SetEnvironmentVariable("SEWERSTUDIO_SIDECAR_TOKEN", "canonical-token");
    Environment.SetEnvironmentVariable("AUSWERTUNGPRO_SIDECAR_TOKEN", null);
    Environment.SetEnvironmentVariable("SEWER_SIDECAR_AUTH_TOKEN", null);
    Environment.SetEnvironmentVariable("SEWER_SIDECAR_TOKEN", null);

    try
    {
        var handler = new CaptureHandler("""{"predictions":[],"inference_time_ms":1}""");
        var client = new VisionPipelineClient(
            new Uri("http://127.0.0.1:8100"),
            new HttpClient(handler));

        await client.ClassifyYoloAsync(new YoloClassifyRequest("abc", 1));

        Assert.Equal("canonical-token", handler.LastSidecarToken);
    }
    finally
    {
        Environment.SetEnvironmentVariable("SEWERSTUDIO_SIDECAR_TOKEN", previousCanonical);
        Environment.SetEnvironmentVariable("AUSWERTUNGPRO_SIDECAR_TOKEN", previousCompat);
        Environment.SetEnvironmentVariable("SEWER_SIDECAR_AUTH_TOKEN", previousAuth);
        Environment.SetEnvironmentVariable("SEWER_SIDECAR_TOKEN", previousLegacy);
    }
}
```

- [x] **Step 2: Test rot laufen lassen**

Run:

```bash
dotnet test tests/AuswertungPro.Next.Pipeline.Tests/AuswertungPro.Next.Pipeline.Tests.csproj --filter "FullyQualifiedName~ClassifyYoloAsync_UsesSewerStudioSidecarTokenEnvironmentAlias" -v minimal --no-restore
```

Expected: FAIL, weil `VisionPipelineClient.TryLoadSidecarToken()` den kanonischen Namen noch nicht kennt.

- [x] **Step 3: Zentrale Helper-Klasse implementieren**

Create `src/AuswertungPro.Next.Infrastructure/Ai/Pipeline/SidecarTokenResolver.cs`:

```csharp
using System;
using System.IO;

namespace AuswertungPro.Next.Infrastructure.Ai.Pipeline;

/// <summary>
/// Zentrale Aufloesung des Sidecar-Auth-Tokens fuer C#-Client und Startup.
/// Kanonisch ist SEWERSTUDIO_SIDECAR_TOKEN; die alten SEWER_SIDECAR_*-Namen
/// bleiben als Kompatibilitaet erhalten.
/// </summary>
public static class SidecarTokenResolver
{
    public const string HeaderName = "X-Sidecar-Token";
    private const string ProductFolderName = "SewerStudio";
    private const string TokenFileName = ".sidecar_token";

    private static readonly string[] EnvironmentNames =
    [
        "SEWERSTUDIO_SIDECAR_TOKEN",
        "AUSWERTUNGPRO_SIDECAR_TOKEN",
        "SEWER_SIDECAR_AUTH_TOKEN",
        "SEWER_SIDECAR_TOKEN"
    ];

    public static string? Resolve(string? configuredToken = null)
        => Resolve(configuredToken, Environment.GetEnvironmentVariable, ReadTokenFile);

    internal static string? Resolve(
        string? configuredToken,
        Func<string, string?> readEnvironment,
        Func<string?> readTokenFile)
    {
        var configured = Normalize(configuredToken);
        if (configured is not null)
            return configured;

        foreach (var name in EnvironmentNames)
        {
            var value = Normalize(readEnvironment(name));
            if (value is not null)
                return value;
        }

        return Normalize(readTokenFile());
    }

    public static string? Normalize(string? token)
        => string.IsNullOrWhiteSpace(token) ? null : token.Trim();

    private static string? ReadTokenFile()
    {
        try
        {
            var localAppData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
            if (string.IsNullOrWhiteSpace(localAppData))
                return null;

            var path = Path.Combine(localAppData, ProductFolderName, TokenFileName);
            return File.Exists(path) ? File.ReadAllText(path) : null;
        }
        catch
        {
            return null;
        }
    }
}
```

- [x] **Step 4: Client und Startup auf Helper umstellen**

In `VisionPipelineClient.cs`:

```csharp
_sidecarToken = _sendSidecarToken
    ? SidecarTokenResolver.Resolve(sidecarToken)
    : null;
```

Use `SidecarTokenResolver.HeaderName` in `AddSidecarTokenHeader`. Remove the private `TryLoadSidecarToken()` and `NormalizeToken()` helpers.

In `AiStartupService.cs`, add:

```csharp
using AuswertungPro.Next.Infrastructure.Ai.Pipeline;
```

Replace `BuildSidecarHeaders` with:

```csharp
private static IReadOnlyDictionary<string, string>? BuildSidecarHeaders(string? configuredToken)
{
    var token = SidecarTokenResolver.Resolve(configuredToken);
    return token is null
        ? null
        : new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            [SidecarTokenResolver.HeaderName] = token
        };
}
```

Remove the private `TryLoadSidecarToken()` and `NormalizeToken()` helpers from `AiStartupService`.

- [x] **Step 5: Token-Tests gruen**

Run:

```bash
dotnet test tests/AuswertungPro.Next.Pipeline.Tests/AuswertungPro.Next.Pipeline.Tests.csproj --filter "FullyQualifiedName~VisionPipelineClientTests" -v minimal --no-restore
```

Expected: PASS.

---

### Task 2: ENV-Dokumentation als aktuelle Quelle anlegen

**Files:**
- Create: `docs/ENV.md`

- [x] **Step 1: ENV-Doku erstellen**

Create `docs/ENV.md` with:

```markdown
# SewerStudio ENV-Konfiguration

Stand: 2026-06-21

Diese Datei ist die kurze aktuelle Quelle fuer relevante Umgebungsvariablen. Historische Audit-Dokumente koennen alte Namen enthalten.

## Sidecar-Auth

Kanonisch:

- `SEWERSTUDIO_SIDECAR_TOKEN`

Kompatibilitaet, weiterhin akzeptiert:

- `AUSWERTUNGPRO_SIDECAR_TOKEN`
- `SEWER_SIDECAR_AUTH_TOKEN`
- `SEWER_SIDECAR_TOKEN`

Aufloesungsreihenfolge:

1. explizit konfigurierter Token aus Settings/API
2. `SEWERSTUDIO_SIDECAR_TOKEN`
3. `AUSWERTUNGPRO_SIDECAR_TOKEN`
4. `SEWER_SIDECAR_AUTH_TOKEN`
5. `SEWER_SIDECAR_TOKEN`
6. `%LOCALAPPDATA%\SewerStudio\.sidecar_token`

Der Header heisst immer `X-Sidecar-Token`.

## KI/Ollama

- `SEWERSTUDIO_AI_VISION_MODEL`
- `SEWERSTUDIO_AI_TEXT_MODEL`
- `SEWERSTUDIO_AI_EMBED_MODEL`
- `SEWERSTUDIO_OLLAMA_URL`
- `SEWERSTUDIO_OLLAMA_KEEP_ALIVE`
- `SEWERSTUDIO_OLLAMA_NUM_CTX`

Default-Modelle kommen aus `OllamaConfig` und `GpuModelSelector`; keine Dokumentation darf qwen2.5 als Default nennen.

## Pipeline/Sidecar

- `SEWERSTUDIO_SIDECAR_URL`
- `SEWERSTUDIO_MULTIMODEL_ENABLED`
- `SEWERSTUDIO_PIPELINE_MODE`
- `SEWERSTUDIO_YOLO_CONFIDENCE`
- `SEWERSTUDIO_DINO_BOX_THRESHOLD`
- `SEWERSTUDIO_DINO_TEXT_THRESHOLD`
- `SEWERSTUDIO_CLASSIFIER_DECISION`
- `SEWERSTUDIO_CLASSIFIER_ONLY_STRUCTURAL_OFF`
- `SEWERSTUDIO_EXPECTED_YOLO_MODEL`
- `SEWERSTUDIO_TELEMETRY_DIR`

## Speicherorte

- `SEWERSTUDIO_APPDATA_DIR`
- `SEWERSTUDIO_KNOWLEDGE_ROOT`
- `SEWER_VIDEO_LABEL_TOOL_DIR`

## Kataloge

- `VSA_KEK_2020_CATALOG_MANIFEST`
- `VSA_CATALOG_SEC_XML`
- `VSA_CATALOG_SEC_ROOT`
- `VSA_CATALOG_NOD_XML`
- `VSA_CATALOG_NOD_ROOT`
```

- [x] **Step 2: Doku-Syntax kurz pruefen**

Run:

```bash
Get-Content docs/ENV.md
```

Expected: Datei lesbar, keine Platzhalter.

---

### Task 3: C#↔Sidecar-DTO-Contract-Test einfuehren

**Files:**
- Create: `tests/AuswertungPro.Next.Pipeline.Tests/SidecarContractTests.cs`

- [x] **Step 1: Contract-Test schreiben**

Create `tests/AuswertungPro.Next.Pipeline.Tests/SidecarContractTests.cs`:

```csharp
using System.Reflection;
using System.Text.Json.Serialization;
using System.Text.RegularExpressions;
using AuswertungPro.Next.Infrastructure.Ai.Pipeline;
using Xunit;

namespace AuswertungPro.Next.Pipeline.Tests;

public sealed class SidecarContractTests
{
    private static readonly string RepoRoot = FindRepoRoot();

    public static TheoryData<Type, string, string> ContractModels => new()
    {
        { typeof(YoloRequest), "sidecar/sidecar/schemas/detection.py", "YoloRequest" },
        { typeof(YoloDetectionDto), "sidecar/sidecar/schemas/detection.py", "YoloDetection" },
        { typeof(YoloResponse), "sidecar/sidecar/schemas/detection.py", "YoloResponse" },
        { typeof(DinoRequest), "sidecar/sidecar/schemas/detection.py", "DinoRequest" },
        { typeof(DinoDetectionDto), "sidecar/sidecar/schemas/detection.py", "DinoDetection" },
        { typeof(DinoResponse), "sidecar/sidecar/schemas/detection.py", "DinoResponse" },
        { typeof(YoloClassifyRequest), "sidecar/sidecar/schemas/detection.py", "YoloClassifyRequest" },
        { typeof(YoloClassifyPrediction), "sidecar/sidecar/schemas/detection.py", "YoloClassifyPrediction" },
        { typeof(YoloClassifyResponse), "sidecar/sidecar/schemas/detection.py", "YoloClassifyResponse" },
        { typeof(SamBoundingBox), "sidecar/sidecar/schemas/detection.py", "BoundingBox" },
        { typeof(SamRequest), "sidecar/sidecar/schemas/segmentation.py", "SamRequest" },
        { typeof(SamMaskResult), "sidecar/sidecar/schemas/segmentation.py", "MaskResult" },
        { typeof(SamResponse), "sidecar/sidecar/schemas/segmentation.py", "SamResponse" },
        { typeof(TrainingExportSample), "sidecar/sidecar/schemas/segmentation.py", "TrainingSample" },
        { typeof(TrainingExportRequestDto), "sidecar/sidecar/schemas/segmentation.py", "TrainingExportRequest" },
        { typeof(TrainingExportResponseDto), "sidecar/sidecar/schemas/segmentation.py", "TrainingExportResponse" },
    };

    [Theory]
    [MemberData(nameof(ContractModels))]
    public void CSharpDtoJsonNames_MatchSidecarPydanticFields(Type csharpType, string schemaRelativePath, string pythonClassName)
    {
        var csharpFields = JsonNames(csharpType);
        var sidecarFields = PydanticFields(Path.Combine(RepoRoot, schemaRelativePath), pythonClassName);

        Assert.Equal(sidecarFields.OrderBy(x => x), csharpFields.OrderBy(x => x));
    }

    private static IReadOnlyList<string> JsonNames(Type type)
        => type.GetProperties(BindingFlags.Public | BindingFlags.Instance)
            .Where(p => p.GetMethod is not null && p.GetMethod.GetParameters().Length == 0)
            .Select(p => p.GetCustomAttribute<JsonPropertyNameAttribute>()?.Name)
            .Where(name => !string.IsNullOrWhiteSpace(name))
            .Select(name => name!)
            .ToList();

    private static IReadOnlyList<string> PydanticFields(string schemaPath, string className)
    {
        var lines = File.ReadAllLines(schemaPath);
        var classStart = Array.FindIndex(lines, line => Regex.IsMatch(line, $@"^class\s+{Regex.Escape(className)}\s*\("));
        Assert.True(classStart >= 0, $"Sidecar class not found: {className} in {schemaPath}");

        var fields = new List<string>();
        for (var i = classStart + 1; i < lines.Length; i++)
        {
            var line = lines[i];
            if (line.StartsWith("class ", StringComparison.Ordinal))
                break;

            var match = Regex.Match(line, @"^\s{4}([A-Za-z_][A-Za-z0-9_]*)\s*:");
            if (match.Success)
                fields.Add(match.Groups[1].Value);
        }

        Assert.NotEmpty(fields);
        return fields;
    }

    private static string FindRepoRoot()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null)
        {
            if (File.Exists(Path.Combine(dir.FullName, "AuswertungPro.sln")))
                return dir.FullName;
            dir = dir.Parent;
        }

        throw new InvalidOperationException("Repo root not found.");
    }
}
```

- [x] **Step 2: Contract-Test laufen lassen**

Run:

```bash
dotnet test tests/AuswertungPro.Next.Pipeline.Tests/AuswertungPro.Next.Pipeline.Tests.csproj --filter "FullyQualifiedName~SidecarContractTests" -v minimal --no-restore
```

Expected: PASS. Falls FAIL, dann zeigt der Test echten DTO-Drift, der sofort an C#-DTO oder Sidecar-Schema angeglichen werden muss.

---

### Task 4: CLAUDE.md als aktuelle Arbeitsanweisung korrigieren

**Files:**
- Modify: `CLAUDE.md`

- [x] **Step 1: Veraltete Aussagen korrigieren**

Change these statements in `CLAUDE.md`:

```markdown
- Dedup/Merge: C#-framebasiert in `MultiModelAnalysisService.UpdateActive` und `VideoFullAnalysisService.UpdateActive` ueber `DedupWindowFrames`.
```

to:

```markdown
- Dedup/Merge: C#-framebasiert ueber `TemporalFindingDeduplicator` und `TemporalCodeVotingService`; keine Annahme zu alten `UpdateActive`-Duplikaten treffen.
```

Change:

```markdown
- `DetectionAggregator` / meterbasierter Merge-Radius / Temporal Voting: nicht im aktuellen HEAD.
- `KbDeduplicationService` / Cosine-Dedup beim Schreiben: nicht implementiert; Cosine wird fuer Retrieval genutzt.
```

to:

```markdown
- `DetectionAggregator` / echtes Multi-Object-Tracking: nicht im aktuellen HEAD. Temporal Voting existiert als `TemporalCodeVotingService`, kein separater Aggregator.
- `KbDeduplicationService`: existiert fuer Similarity-Checks im Trainings-/Review-Kontext; nicht mit dem Retrieval-Ranking verwechseln.
```

Change:

```markdown
- Tests NUR fuer Recommendation- und QualityGate-Logik
```

to:

```markdown
- Tests breit einsetzen: Parser, Import, Pipeline, KnowledgeBase, UI-ViewModels und QualityGate. Keine riskanten Logik-Aenderungen ohne fokussierten Test.
```

- [x] **Step 2: CLAUDE.md auf alte falsche Marker pruefen**

Run:

```bash
rg -n "Tests NUR|UpdateActive|Temporal Voting: nicht|KbDeduplicationService.*nicht implementiert" CLAUDE.md
```

Expected: keine Treffer.

---

### Task 5: Abschlussverifikation

**Files:** Keine weiteren Aenderungen.

- [x] **Step 1: Gezielte Tests**

Run:

```bash
dotnet test tests/AuswertungPro.Next.Pipeline.Tests/AuswertungPro.Next.Pipeline.Tests.csproj --filter "FullyQualifiedName~VisionPipelineClientTests|FullyQualifiedName~SidecarContractTests" -v minimal --no-restore
```

Expected: PASS.

- [x] **Step 2: Voller Build**

Run:

```bash
dotnet build AuswertungPro.sln -v minimal --no-restore
```

Expected: 0 Fehler, 0 Warnungen.

- [x] **Step 3: Arbeitsbaum pruefen**

Run:

```bash
git status --short
```

Expected: nur die in diesem Plan geaenderten Dateien plus bereits vorhandene untracked lokale Doku/Modelldateien.

---

## Self-Review

**Spec-Abdeckung:**
- Token-Bug: Task 1.
- ENV-Doku: Task 2.
- C#↔Sidecar-Contract-Test: Task 3.
- CLAUDE.md-Korrektur: Task 4.
- Verifikation: Task 5.

**Bewusst nicht in diesem Quick-Win-Plan:**
- PlayerWindow-Zerlegung, Service-Locator-Entkopplung, ProtocolPdfExporter-/Import-Modularisierung. Das sind groessere Refactors und brauchen eigene Plaene.
- Debug.WriteLine-Logging-Migration komplett. Das ist breit gestreut und braucht Priorisierung pro Modul.
- XAML-Binding-Checker, async-void und sync-over-async. Diese werden als Folgeplan behandelt, damit dieser Bugfix-Durchlauf klein und sicher bleibt.

**Type-Konsistenz:**
- Token-Helper liegt in Infrastructure und ist von UI referenzierbar.
- Contract-Test nutzt echte DTO-Typen aus `VisionPipelineDtos.cs` und echte Sidecar-Schema-Dateien.
