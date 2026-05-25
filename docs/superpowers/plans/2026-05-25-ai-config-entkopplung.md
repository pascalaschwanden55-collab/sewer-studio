# AI Config Entkopplung Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** `AiRuntimeConfig` und `AiPlatformConfig` aus dem UI loesen, damit KI-Services keine UI-Settings mehr mitschleppen.

**Architecture:** `AppSettings` bleibt im UI. Application bekommt neutrale Settings-Records und ein Provider-Interface. Infrastructure bekommt eine Factory, die Defaults, Environment-Variablen und GPU-Autoauswahl zusammenfuehrt.

**Tech Stack:** C#/.NET 10, xUnit, WPF UI, Ollama, bestehende Application/Infrastructure/UI-Schichten.

---

## Ergebnisbild

Nach dieser Migration gilt:

- `AppSettings` bleibt in `src/AuswertungPro.Next.UI/AppSettings.cs`.
- Application definiert nur neutrale Daten:
  - `AiRuntimeSettings`
  - `AiPlatformSettings`
  - `AiSettingsSource`
  - `IAiSettingsProvider`
- Infrastructure baut daraus echte Settings:
  - Defaults
  - Environment-Variablen
  - GPU-Autoauswahl
- UI liest `AppSettings` und liefert sie als neutrale Quelle an Infrastructure.
- `AiRuntimeConfig.cs` und `AiPlatformConfig.cs` verschwinden aus `src/AuswertungPro.Next.UI/Ai`.

## Wichtige Dateien

- Create: `src/AuswertungPro.Next.Application/Ai/AiSettings.cs`
- Create: `src/AuswertungPro.Next.Infrastructure/Ai/Configuration/AiSettingsFactory.cs`
- Create: `src/AuswertungPro.Next.UI/Services/AppSettingsAiSettingsProvider.cs`
- Modify: `src/AuswertungPro.Next.UI/ServiceProvider.cs`
- Modify: `src/AuswertungPro.Next.UI/Views/Windows/PlayerWindow.xaml.cs`
- Modify: `src/AuswertungPro.Next.UI/Views/Windows/CodingModeWindow.xaml.cs`
- Modify: `src/AuswertungPro.Next.UI/ViewModels/Windows/TrainingCenterViewModel.cs`
- Modify: `src/AuswertungPro.Next.UI/ViewModels/Pages/DataPageViewModel.cs`
- Modify: `src/AuswertungPro.Next.UI/Ai/VideoAnalysisPipelineService.cs`
- Modify: `src/AuswertungPro.Next.UI/Ai/FullProtocolGenerationService.cs`
- Modify: `src/AuswertungPro.Next.UI/Ai/Training/TrainingSampleGenerator.cs`
- Modify: `src/AuswertungPro.Next.UI/Ai/Training/MeterTimelineService.cs`
- Modify: `src/AuswertungPro.Next.UI/Ai/Training/SelfTrainingOrchestrator.cs`
- Modify: `src/AuswertungPro.Next.UI/Ai/Sanierung/AiSanierungOptimizationService.cs`
- Delete: `src/AuswertungPro.Next.UI/Ai/AiRuntimeConfig.cs`
- Delete: `src/AuswertungPro.Next.UI/Ai/AiPlatformConfig.cs`
- Test: `tests/AuswertungPro.Next.Pipeline.Tests/AiSettingsTests.cs`
- Test: `tests/AuswertungPro.Next.Pipeline.Tests/AiSuggestionContractTests.cs`

---

### Task 1: Application Settings-Vertrag einfuehren

**Files:**
- Create: `src/AuswertungPro.Next.Application/Ai/AiSettings.cs`
- Test: `tests/AuswertungPro.Next.Pipeline.Tests/AiSettingsTests.cs`

- [ ] **Step 1: Failing Test schreiben**

Create `tests/AuswertungPro.Next.Pipeline.Tests/AiSettingsTests.cs`:

```csharp
using AuswertungPro.Next.Application.Ai;

namespace AuswertungPro.Next.Pipeline.Tests;

public sealed class AiSettingsTests
{
    [Fact]
    public void AiSettingsModels_LiveInApplicationLayer()
    {
        Assert.StartsWith("AuswertungPro.Next.Application", typeof(AiRuntimeSettings).Namespace);
        Assert.StartsWith("AuswertungPro.Next.Application", typeof(AiPlatformSettings).Namespace);
        Assert.StartsWith("AuswertungPro.Next.Application", typeof(AiSettingsSource).Namespace);
        Assert.StartsWith("AuswertungPro.Next.Application", typeof(IAiSettingsProvider).Namespace);
    }

    [Fact]
    public void AiPlatformSettings_ProjectsRuntimeAndPipelineSettings()
    {
        var platform = new AiPlatformSettings(
            Enabled: true,
            OllamaBaseUri: new Uri("http://localhost:11434"),
            VisionModel: "qwen2.5vl:7b",
            TextModel: "qwen2.5:14b",
            EmbedModel: "nomic-embed-text",
            OllamaRequestTimeout: TimeSpan.FromMinutes(5),
            OllamaKeepAlive: "24h",
            OllamaNumCtx: 8192,
            MultiModelEnabled: true,
            SidecarUrl: new Uri("http://localhost:8100"),
            PipelineMode: PipelineMode.Auto,
            YoloConfidence: 0.25,
            YoloClassConfidence: new Dictionary<string, double>(StringComparer.OrdinalIgnoreCase)
            {
                ["BAB"] = 0.15
            },
            DinoBoxThreshold: 0.30,
            DinoTextThreshold: 0.25,
            SidecarTimeoutSec: 300,
            PipeDiameterMmOverride: 300,
            FfmpegPath: "ffmpeg");

        var runtime = platform.ToRuntimeSettings();
        var pipeline = platform.ToPipelineConfig();

        Assert.True(runtime.Enabled);
        Assert.Equal("qwen2.5vl:7b", runtime.VisionModel);
        Assert.Equal("qwen2.5:14b", runtime.TextModel);
        Assert.Equal("ffmpeg", runtime.FfmpegPath);
        Assert.True(pipeline.MultiModelEnabled);
        Assert.Equal(PipelineMode.Auto, pipeline.Mode);
        Assert.Equal(0.25, pipeline.YoloConfidence);
    }
}
```

- [ ] **Step 2: Test rot laufen lassen**

Run:

```powershell
dotnet test tests/AuswertungPro.Next.Pipeline.Tests/AuswertungPro.Next.Pipeline.Tests.csproj --filter "FullyQualifiedName~AiSettingsTests" -v minimal --no-restore
```

Expected:

```text
CS0246: Der Typ- oder Namespacename "AiRuntimeSettings" wurde nicht gefunden
```

- [ ] **Step 3: Application Settings implementieren**

Create `src/AuswertungPro.Next.Application/Ai/AiSettings.cs`:

```csharp
namespace AuswertungPro.Next.Application.Ai;

public sealed record AiRuntimeSettings(
    bool Enabled,
    Uri OllamaBaseUri,
    string VisionModel,
    string TextModel,
    string? EmbedModel,
    string? FfmpegPath,
    TimeSpan OllamaRequestTimeout,
    string OllamaKeepAlive,
    int OllamaNumCtx);

public sealed record AiSettingsSource(
    bool? Enabled = null,
    string? OllamaUrl = null,
    string? VisionModel = null,
    string? TextModel = null,
    string? EmbedModel = null,
    int? OllamaTimeoutMin = null,
    string? OllamaKeepAlive = null,
    int? OllamaNumCtx = null,
    bool? MultiModelEnabled = null,
    string? SidecarUrl = null,
    string? PipelineMode = null,
    double? YoloConfidence = null,
    double? DinoBoxThreshold = null,
    double? DinoTextThreshold = null,
    int? PipeDiameterMm = null,
    string? FfmpegPath = null);

public sealed record AiPlatformSettings(
    bool Enabled,
    Uri OllamaBaseUri,
    string VisionModel,
    string TextModel,
    string EmbedModel,
    TimeSpan OllamaRequestTimeout,
    string OllamaKeepAlive,
    int OllamaNumCtx,
    bool MultiModelEnabled,
    Uri SidecarUrl,
    PipelineMode PipelineMode,
    double YoloConfidence,
    Dictionary<string, double> YoloClassConfidence,
    double DinoBoxThreshold,
    double DinoTextThreshold,
    int SidecarTimeoutSec,
    int? PipeDiameterMmOverride,
    string FfmpegPath)
{
    public AiRuntimeSettings ToRuntimeSettings() => new(
        Enabled,
        OllamaBaseUri,
        VisionModel,
        TextModel,
        EmbedModel,
        FfmpegPath,
        OllamaRequestTimeout,
        OllamaKeepAlive,
        OllamaNumCtx);

    public PipelineConfig ToPipelineConfig() => new(
        MultiModelEnabled,
        SidecarUrl,
        PipelineMode,
        YoloConfidence,
        YoloClassConfidence,
        DinoBoxThreshold,
        DinoTextThreshold,
        SidecarTimeoutSec,
        PipeDiameterMmOverride);
}

public interface IAiSettingsProvider
{
    AiPlatformSettings Load();
}
```

- [ ] **Step 4: Test gruen laufen lassen**

Run:

```powershell
dotnet test tests/AuswertungPro.Next.Pipeline.Tests/AuswertungPro.Next.Pipeline.Tests.csproj --filter "FullyQualifiedName~AiSettingsTests" -v minimal --no-restore
```

Expected:

```text
Bestanden!
```

---

### Task 2: Infrastructure Factory fuer Settings bauen

**Files:**
- Create: `src/AuswertungPro.Next.Infrastructure/Ai/Configuration/AiSettingsFactory.cs`
- Test: `tests/AuswertungPro.Next.Pipeline.Tests/AiSettingsTests.cs`

- [ ] **Step 1: Failing Tests ergaenzen**

Append to `AiSettingsTests`:

```csharp
using AuswertungPro.Next.Infrastructure.Ai.Configuration;

[Fact]
public void AiSettingsFactory_UsesSourceOverEnvironmentAndDefaults()
{
    var source = new AiSettingsSource(
        Enabled: true,
        OllamaUrl: "http://127.0.0.1:11434",
        VisionModel: "custom-vision",
        TextModel: "custom-text",
        EmbedModel: "custom-embed",
        OllamaTimeoutMin: 9,
        OllamaKeepAlive: "12h",
        OllamaNumCtx: 4096,
        MultiModelEnabled: true,
        SidecarUrl: "http://127.0.0.1:8100",
        PipelineMode: "multi",
        YoloConfidence: 0.42,
        DinoBoxThreshold: 0.50,
        DinoTextThreshold: 0.60,
        PipeDiameterMm: 250,
        FfmpegPath: "C:\\tools\\ffmpeg.exe");

    var settings = AiSettingsFactory.Load(source);

    Assert.True(settings.Enabled);
    Assert.Equal(new Uri("http://127.0.0.1:11434"), settings.OllamaBaseUri);
    Assert.Equal("custom-vision", settings.VisionModel);
    Assert.Equal("custom-text", settings.TextModel);
    Assert.Equal("custom-embed", settings.EmbedModel);
    Assert.Equal(TimeSpan.FromMinutes(9), settings.OllamaRequestTimeout);
    Assert.Equal("12h", settings.OllamaKeepAlive);
    Assert.Equal(4096, settings.OllamaNumCtx);
    Assert.True(settings.MultiModelEnabled);
    Assert.Equal(new Uri("http://127.0.0.1:8100"), settings.SidecarUrl);
    Assert.Equal(PipelineMode.MultiModel, settings.PipelineMode);
    Assert.Equal(0.42, settings.YoloConfidence);
    Assert.Equal(0.50, settings.DinoBoxThreshold);
    Assert.Equal(0.60, settings.DinoTextThreshold);
    Assert.Equal(250, settings.PipeDiameterMmOverride);
    Assert.Equal("C:\\tools\\ffmpeg.exe", settings.FfmpegPath);
}

[Theory]
[InlineData("multimodel", PipelineMode.MultiModel)]
[InlineData("multi", PipelineMode.MultiModel)]
[InlineData("ollama", PipelineMode.OllamaOnly)]
[InlineData("ollamaonly", PipelineMode.OllamaOnly)]
[InlineData("auto", PipelineMode.Auto)]
[InlineData("unknown", PipelineMode.Auto)]
public void AiSettingsFactory_ParsesPipelineMode(string value, PipelineMode expected)
{
    var settings = AiSettingsFactory.Load(new AiSettingsSource(PipelineMode: value));

    Assert.Equal(expected, settings.PipelineMode);
}
```

- [ ] **Step 2: Test rot laufen lassen**

Run:

```powershell
dotnet test tests/AuswertungPro.Next.Pipeline.Tests/AuswertungPro.Next.Pipeline.Tests.csproj --filter "FullyQualifiedName~AiSettingsTests" -v minimal --no-restore
```

Expected:

```text
CS0234: Der Typ- oder Namespacename "Configuration" ist im Namespace "AuswertungPro.Next.Infrastructure.Ai" nicht vorhanden
```

- [ ] **Step 3: Factory implementieren**

Create `src/AuswertungPro.Next.Infrastructure/Ai/Configuration/AiSettingsFactory.cs`:

```csharp
using System.Globalization;
using AuswertungPro.Next.Application.Ai;
using AuswertungPro.Next.Infrastructure.Ai.Ollama;

namespace AuswertungPro.Next.Infrastructure.Ai.Configuration;

public static class AiSettingsFactory
{
    public static AiPlatformSettings Load(AiSettingsSource? source = null)
    {
        source ??= new AiSettingsSource();

        var configuredVision = FirstNonEmpty(
            source.VisionModel,
            Env("SEWERSTUDIO_AI_VISION_MODEL"));

        string vision;
        var numCtxDefault = OllamaConfig.DefaultNumCtx;

        if (GpuModelSelector.IsAutoMode(configuredVision))
        {
            var gpuProfile = GpuModelSelector.DetectAndSelect();
            if (gpuProfile is not null)
            {
                vision = gpuProfile.ResolvedModel;
                numCtxDefault = gpuProfile.ResolvedNumCtx;
            }
            else
            {
                vision = OllamaConfig.DefaultVisionModel;
            }
        }
        else
        {
            vision = configuredVision!;
        }

        var yoloClassConf = new Dictionary<string, double>(StringComparer.OrdinalIgnoreCase)
        {
            ["BAB"] = 0.15,
            ["BAA"] = 0.20,
            ["BAC"] = 0.25,
            ["BBA"] = 0.20,
            ["BBB"] = 0.25,
            ["BBC"] = 0.25,
            ["BCA"] = 0.35,
            ["BCC"] = 0.30,
            ["BCD"] = 0.30,
            ["BCE"] = 0.30
        };

        return new AiPlatformSettings(
            Enabled: source.Enabled ?? ParseBool(Env("SEWERSTUDIO_AI_ENABLED")),
            OllamaBaseUri: new Uri(FirstNonEmpty(source.OllamaUrl, Env("SEWERSTUDIO_OLLAMA_URL")) ?? "http://localhost:11434"),
            VisionModel: vision,
            TextModel: FirstNonEmpty(source.TextModel, Env("SEWERSTUDIO_AI_TEXT_MODEL")) ?? OllamaConfig.DefaultTextModel,
            EmbedModel: FirstNonEmpty(source.EmbedModel, Env("SEWERSTUDIO_AI_EMBED_MODEL")) ?? OllamaConfig.DefaultEmbedModel,
            OllamaRequestTimeout: TimeSpan.FromMinutes(source.OllamaTimeoutMin ?? ParseInt(Env("SEWERSTUDIO_AI_TIMEOUT_MIN")) ?? 5),
            OllamaKeepAlive: FirstNonEmpty(source.OllamaKeepAlive, Env("SEWERSTUDIO_OLLAMA_KEEP_ALIVE")) ?? OllamaConfig.DefaultKeepAlive,
            OllamaNumCtx: source.OllamaNumCtx ?? ParseInt(Env("SEWERSTUDIO_OLLAMA_NUM_CTX")) ?? numCtxDefault,
            MultiModelEnabled: source.MultiModelEnabled ?? ParseBool(Env("SEWERSTUDIO_MULTIMODEL_ENABLED")),
            SidecarUrl: new Uri(FirstNonEmpty(source.SidecarUrl, Env("SEWERSTUDIO_SIDECAR_URL")) ?? "http://localhost:8100"),
            PipelineMode: ParsePipelineMode(FirstNonEmpty(source.PipelineMode, Env("SEWERSTUDIO_PIPELINE_MODE"))),
            YoloConfidence: source.YoloConfidence ?? ParseDouble(Env("SEWERSTUDIO_YOLO_CONFIDENCE")) ?? 0.25,
            YoloClassConfidence: yoloClassConf,
            DinoBoxThreshold: source.DinoBoxThreshold ?? ParseDouble(Env("SEWERSTUDIO_DINO_BOX_THRESHOLD")) ?? 0.30,
            DinoTextThreshold: source.DinoTextThreshold ?? ParseDouble(Env("SEWERSTUDIO_DINO_TEXT_THRESHOLD")) ?? 0.25,
            SidecarTimeoutSec: ParseInt(Env("SEWERSTUDIO_SIDECAR_TIMEOUT_SEC")) ?? 300,
            PipeDiameterMmOverride: source.PipeDiameterMm ?? ParseInt(Env("SEWERSTUDIO_PIPE_DIAMETER_MM")),
            FfmpegPath: FirstNonEmpty(source.FfmpegPath, Env("SEWERSTUDIO_FFMPEG")) ?? "ffmpeg");
    }

    public static PipelineMode ParsePipelineMode(string? value)
    {
        return (value ?? "auto").Trim().ToLowerInvariant() switch
        {
            "multimodel" or "multi" => PipelineMode.MultiModel,
            "ollama" or "ollamaonly" => PipelineMode.OllamaOnly,
            _ => PipelineMode.Auto
        };
    }

    public static bool ParseBool(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
            return false;

        var trimmed = value.Trim();
        return trimmed == "1" || (bool.TryParse(trimmed, out var parsed) && parsed);
    }

    public static double? ParseDouble(string? value)
        => double.TryParse(value?.Trim(), NumberStyles.Float, CultureInfo.InvariantCulture, out var d) ? d : null;

    public static int? ParseInt(string? value)
        => int.TryParse(value?.Trim(), out var i) ? i : null;

    private static string? Env(string name)
    {
        var val = Environment.GetEnvironmentVariable(name)?.Trim();
        if (!string.IsNullOrEmpty(val))
            return val;

        if (name.StartsWith("SEWERSTUDIO_", StringComparison.Ordinal))
            return Environment.GetEnvironmentVariable("AUSWERTUNGPRO_" + name["SEWERSTUDIO_".Length..])?.Trim();

        return null;
    }

    private static string? FirstNonEmpty(params string?[] values)
    {
        foreach (var value in values)
        {
            if (!string.IsNullOrWhiteSpace(value))
                return value.Trim();
        }

        return null;
    }
}
```

- [ ] **Step 4: Test gruen laufen lassen**

Run:

```powershell
dotnet test tests/AuswertungPro.Next.Pipeline.Tests/AuswertungPro.Next.Pipeline.Tests.csproj --filter "FullyQualifiedName~AiSettingsTests" -v minimal --no-restore
```

Expected:

```text
Bestanden!
```

---

### Task 3: UI Provider fuer AppSettings bauen

**Files:**
- Create: `src/AuswertungPro.Next.UI/Services/AppSettingsAiSettingsProvider.cs`
- Modify: `src/AuswertungPro.Next.UI/ServiceProvider.cs`
- Test: `tests/AuswertungPro.Next.Pipeline.Tests/AiSettingsTests.cs`

- [ ] **Step 1: Provider-Test schreiben**

Append to `AiSettingsTests`:

```csharp
[Fact]
public void AiSettingsProvider_Interface_CanLoadPlatformSettings()
{
    IAiSettingsProvider provider = new StaticAiSettingsProvider(new AiPlatformSettings(
        Enabled: true,
        OllamaBaseUri: new Uri("http://localhost:11434"),
        VisionModel: "vision",
        TextModel: "text",
        EmbedModel: "embed",
        OllamaRequestTimeout: TimeSpan.FromMinutes(1),
        OllamaKeepAlive: "1h",
        OllamaNumCtx: 2048,
        MultiModelEnabled: false,
        SidecarUrl: new Uri("http://localhost:8100"),
        PipelineMode: PipelineMode.OllamaOnly,
        YoloConfidence: 0.25,
        YoloClassConfidence: new Dictionary<string, double>(),
        DinoBoxThreshold: 0.3,
        DinoTextThreshold: 0.25,
        SidecarTimeoutSec: 300,
        PipeDiameterMmOverride: null,
        FfmpegPath: "ffmpeg"));

    var loaded = provider.Load();

    Assert.True(loaded.Enabled);
    Assert.Equal("vision", loaded.VisionModel);
}

private sealed class StaticAiSettingsProvider(AiPlatformSettings settings) : IAiSettingsProvider
{
    public AiPlatformSettings Load() => settings;
}
```

- [ ] **Step 2: Test laufen lassen**

Run:

```powershell
dotnet test tests/AuswertungPro.Next.Pipeline.Tests/AuswertungPro.Next.Pipeline.Tests.csproj --filter "FullyQualifiedName~AiSettingsTests" -v minimal --no-restore
```

Expected:

```text
Bestanden!
```

- [ ] **Step 3: UI Provider implementieren**

Create `src/AuswertungPro.Next.UI/Services/AppSettingsAiSettingsProvider.cs`:

```csharp
using AuswertungPro.Next.Application.Ai;
using AuswertungPro.Next.Infrastructure.Ai.Configuration;

namespace AuswertungPro.Next.UI.Services;

public sealed class AppSettingsAiSettingsProvider : IAiSettingsProvider
{
    public AiPlatformSettings Load()
    {
        AppSettings? settings = null;
        try
        {
            settings = AppSettings.Load();
        }
        catch
        {
            settings = null;
        }

        return AiSettingsFactory.Load(ToSource(settings));
    }

    public static AiSettingsSource ToSource(AppSettings? settings)
    {
        if (settings is null)
            return new AiSettingsSource();

        return new AiSettingsSource(
            Enabled: settings.AiEnabled,
            OllamaUrl: settings.AiOllamaUrl,
            VisionModel: settings.AiVisionModel,
            TextModel: settings.AiTextModel,
            EmbedModel: settings.AiEmbedModel,
            OllamaTimeoutMin: settings.AiOllamaTimeoutMin,
            OllamaKeepAlive: settings.AiOllamaKeepAlive,
            OllamaNumCtx: settings.AiOllamaNumCtx,
            MultiModelEnabled: settings.PipelineMultiModelEnabled,
            SidecarUrl: settings.PipelineSidecarUrl,
            PipelineMode: settings.PipelineMode,
            YoloConfidence: settings.PipelineYoloConfidence,
            DinoBoxThreshold: settings.PipelineDinoBoxThreshold,
            DinoTextThreshold: settings.PipelineDinoTextThreshold,
            PipeDiameterMm: settings.PipelinePipeDiameterMm,
            FfmpegPath: settings.AiFfmpegPath);
    }
}
```

- [ ] **Step 4: ServiceProvider auf Provider umstellen**

In `src/AuswertungPro.Next.UI/ServiceProvider.cs`, ersetze direkte Config-Ladung:

```csharp
var aiPlatform = AiPlatformConfig.Load(settings);
var cfg = aiPlatform.ToRuntimeConfig();
```

durch:

```csharp
var aiSettingsProvider = new AppSettingsAiSettingsProvider();
var aiPlatform = aiSettingsProvider.Load();
var cfg = aiPlatform.ToRuntimeSettings();
```

- [ ] **Step 5: Build laufen lassen**

Run:

```powershell
dotnet build src/AuswertungPro.Next.UI/AuswertungPro.Next.UI.csproj -v minimal -p:UseAppHost=false
```

Expected:

```text
0 Fehler
```

---

### Task 4: Services von AiRuntimeConfig auf AiRuntimeSettings umstellen

**Files:**
- Modify: `src/AuswertungPro.Next.UI/Ai/Training/MeterTimelineService.cs`
- Modify: `src/AuswertungPro.Next.UI/Ai/Training/TrainingSampleGenerator.cs`
- Modify: `src/AuswertungPro.Next.UI/Ai/VideoAnalysisPipelineService.cs`
- Modify: `src/AuswertungPro.Next.UI/Ai/FullProtocolGenerationService.cs`
- Modify: `src/AuswertungPro.Next.UI/Ai/Sanierung/AiSanierungOptimizationService.cs`
- Modify: `src/AuswertungPro.Next.UI/Ai/Training/SelfTrainingOrchestrator.cs`
- Modify: affected tests

- [ ] **Step 1: Failing Architekturtest schreiben**

Append to `tests/AuswertungPro.Next.Pipeline.Tests/AiSuggestionContractTests.cs`:

```csharp
[Fact]
public void RuntimeSettings_LiveOutsideUiLayer()
{
    Assert.StartsWith("AuswertungPro.Next.Application", typeof(AiRuntimeSettings).Namespace);
    AssertNoUiType("AiRuntimeConfig");
}
```

- [ ] **Step 2: Test rot laufen lassen**

Run:

```powershell
dotnet test tests/AuswertungPro.Next.Pipeline.Tests/AuswertungPro.Next.Pipeline.Tests.csproj --filter "FullyQualifiedName~AiSuggestionContractTests.RuntimeSettings_LiveOutsideUiLayer" -v minimal --no-restore
```

Expected:

```text
Assert.DoesNotContain() Failure
```

- [ ] **Step 3: Constructor-Typen ersetzen**

Beispiel fuer `MeterTimelineService`:

Before:

```csharp
private readonly AiRuntimeConfig _cfg;

public MeterTimelineService(AiRuntimeConfig cfg, OsdMeterDetectionService? osd = null, int concurrency = 1)
{
    _cfg = cfg;
    _osd = osd;
    _concurrency = Math.Max(1, concurrency);
}
```

After:

```csharp
private readonly AiRuntimeSettings _cfg;

public MeterTimelineService(AiRuntimeSettings cfg, OsdMeterDetectionService? osd = null, int concurrency = 1)
{
    _cfg = cfg;
    _osd = osd;
    _concurrency = Math.Max(1, concurrency);
}
```

Apply the same replacement in:

```text
VideoAnalysisPipelineService
FullProtocolGenerationService
TrainingSampleGenerator
SelfTrainingOrchestrator
AiSanierungOptimizationService
```

- [ ] **Step 4: OllamaClient-Erzeugung ersetzen**

Where code currently uses:

```csharp
_client = cfg.CreateOllamaClient(http);
```

replace with:

```csharp
_client = new OllamaClient(
    cfg.OllamaBaseUri,
    http,
    cfg.OllamaRequestTimeout,
    keepAlive: cfg.OllamaKeepAlive,
    numCtx: cfg.OllamaNumCtx);
```

- [ ] **Step 5: Call-sites ersetzen**

Where code currently uses:

```csharp
var cfg = AiRuntimeConfig.Load();
```

replace with:

```csharp
var cfg = new AppSettingsAiSettingsProvider()
    .Load()
    .ToRuntimeSettings();
```

For pipeline config:

```csharp
var pipelineCfg = AiPlatformConfig.Load().ToPipelineConfig();
```

replace with:

```csharp
var pipelineCfg = new AppSettingsAiSettingsProvider()
    .Load()
    .ToPipelineConfig();
```

- [ ] **Step 6: Tests anpassen**

Replace test setup:

```csharp
var cfg = new AiRuntimeConfig(
    Enabled: false,
    OllamaBaseUri: new Uri("http://localhost:11434"),
    VisionModel: "",
    TextModel: "",
    EmbedModel: null,
    FfmpegPath: null);
```

with:

```csharp
var cfg = new AiRuntimeSettings(
    Enabled: false,
    OllamaBaseUri: new Uri("http://localhost:11434"),
    VisionModel: "",
    TextModel: "",
    EmbedModel: null,
    FfmpegPath: null,
    OllamaRequestTimeout: TimeSpan.FromMinutes(5),
    OllamaKeepAlive: "24h",
    OllamaNumCtx: 8192);
```

- [ ] **Step 7: Build laufen lassen**

Run:

```powershell
dotnet build src/AuswertungPro.Next.UI/AuswertungPro.Next.UI.csproj -v minimal -p:UseAppHost=false
```

Expected:

```text
0 Fehler
```

- [ ] **Step 8: Architekturtest gruen laufen lassen**

Run:

```powershell
dotnet test tests/AuswertungPro.Next.Pipeline.Tests/AuswertungPro.Next.Pipeline.Tests.csproj --filter "FullyQualifiedName~AiSuggestionContractTests.RuntimeSettings_LiveOutsideUiLayer" -v minimal --no-restore
```

Expected:

```text
Bestanden!
```

---

### Task 5: AiPlatformConfig entfernen

**Files:**
- Delete: `src/AuswertungPro.Next.UI/Ai/AiPlatformConfig.cs`
- Modify: `tests/AuswertungPro.Next.Pipeline.Tests/AiPlatformConfigTests.cs`
- Modify: `tests/AuswertungPro.Next.Pipeline.Tests/PipelineConfigTests.cs`

- [ ] **Step 1: Architekturtest fuer alten Typ ergaenzen**

Append to `AiSuggestionContractTests`:

```csharp
[Fact]
public void PlatformSettings_LiveOutsideUiLayer()
{
    Assert.StartsWith("AuswertungPro.Next.Application", typeof(AiPlatformSettings).Namespace);
    AssertNoUiType("AiPlatformConfig");
}
```

- [ ] **Step 2: Alte Tests umbenennen**

Rename class in `tests/AuswertungPro.Next.Pipeline.Tests/AiPlatformConfigTests.cs`:

```csharp
public sealed class AiPlatformConfigTests
```

to:

```csharp
public sealed class AiSettingsFactoryTests
```

- [ ] **Step 3: Alte Config-Aufrufe ersetzen**

Replace:

```csharp
var config = AiPlatformConfig.Load(settings: null);
```

with:

```csharp
var config = AiSettingsFactory.Load();
```

Replace:

```csharp
var config = AiPlatformConfig.Load(settings);
```

with:

```csharp
var config = AiSettingsFactory.Load(AppSettingsAiSettingsProvider.ToSource(settings));
```

Replace parse helper calls:

```csharp
AiPlatformConfig.ParseBool(value)
AiPlatformConfig.ParseDouble(value)
AiPlatformConfig.ParseInt(value)
```

with:

```csharp
AiSettingsFactory.ParseBool(value)
AiSettingsFactory.ParseDouble(value)
AiSettingsFactory.ParseInt(value)
```

- [ ] **Step 4: Datei loeschen**

Delete:

```text
src/AuswertungPro.Next.UI/Ai/AiPlatformConfig.cs
```

- [ ] **Step 5: Tests laufen lassen**

Run:

```powershell
dotnet test tests/AuswertungPro.Next.Pipeline.Tests/AuswertungPro.Next.Pipeline.Tests.csproj --filter "FullyQualifiedName~AiSettingsFactoryTests|FullyQualifiedName~AiSuggestionContractTests.PlatformSettings_LiveOutsideUiLayer" -v minimal --no-restore
```

Expected:

```text
Bestanden!
```

---

### Task 6: Abschlusspruefung

**Files:**
- No production code unless failures are found.

- [ ] **Step 1: UI/Ai Count pruefen**

Run:

```powershell
(rg --files 'src/AuswertungPro.Next.UI/Ai' -g '*.cs' | Measure-Object).Count
```

Expected:

```text
19
```

The count may differ by 1 if another branch changed nearby files. Important is that `AiRuntimeConfig.cs` and `AiPlatformConfig.cs` are gone.

- [ ] **Step 2: Suche nach alten Typen**

Run:

```powershell
rg "AiRuntimeConfig|AiPlatformConfig" src tests -g "*.cs"
```

Expected:

```text
No matches
```

- [ ] **Step 3: Build**

Run:

```powershell
dotnet build src/AuswertungPro.Next.UI/AuswertungPro.Next.UI.csproj -v minimal -p:UseAppHost=false
```

Expected:

```text
0 Fehler
```

- [ ] **Step 4: Tests**

Run:

```powershell
dotnet test tests/AuswertungPro.Next.Infrastructure.Tests/AuswertungPro.Next.Infrastructure.Tests.csproj -v minimal --no-restore
```

Expected:

```text
Fehler: 0
```

Run:

```powershell
dotnet test tests/AuswertungPro.Next.Pipeline.Tests/AuswertungPro.Next.Pipeline.Tests.csproj -v minimal --no-restore
```

Expected:

```text
Fehler: 0
```

- [ ] **Step 5: Paket-Sicherheitscheck**

Run:

```powershell
dotnet list AuswertungPro.sln package --vulnerable --include-transitive
```

Expected:

```text
keine anfaelligen Pakete
```

- [ ] **Step 6: Commit**

Run:

```powershell
git add src/AuswertungPro.Next.Application/Ai src/AuswertungPro.Next.Infrastructure/Ai src/AuswertungPro.Next.UI tests/AuswertungPro.Next.Pipeline.Tests
git commit -m "refactor: decouple ai runtime settings"
```

Expected:

```text
[branch commit] refactor: decouple ai runtime settings
```

---

## Nicht in diesem Plan

- Kein `IImageSource`.
- Kein CLI-Benchmark.
- Kein Verschieben der grossen Ablaufsteuerungen.
- Kein Umbau der WPF-Overlay-Renderer.
- Kein VSA-Codebaum-Umbau.

Diese Punkte kommen danach in eigenen kleinen Plaenen.

## Self-Review

- Spec coverage: Config wird aus UI geloest, AppSettings bleibt UI, Application bekommt Vertrag, Infrastructure bekommt Factory.
- Placeholder scan: Keine offenen Platzhalter im Plan.
- Type consistency: `AiRuntimeSettings`, `AiPlatformSettings`, `AiSettingsSource`, `IAiSettingsProvider` werden in Task 1 definiert und danach verwendet.
