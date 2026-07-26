using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using AuswertungPro.Next.Application.Ai;
using AuswertungPro.Next.Application.Ai.Startup;
using AuswertungPro.Next.Infrastructure.Ai.Configuration;
using AuswertungPro.Next.Infrastructure.Ai.KnowledgeBase;
using AuswertungPro.Next.Infrastructure.Ai.Pipeline;
using AuswertungPro.Next.Infrastructure.Ai.Startup;

// Typaliase: Die Kontrakt-Typen wanderten nach Application.Ai.Startup.
// Damit bestehender Aufrufer-Code in der UI unveraendert bleibt, werden
// sie hier per global using re-exportiert. Kein eigener Namespace-Wechsel
// noetig – alle Typen sind durch die using-Direktiven sichtbar.

namespace AuswertungPro.Next.UI.Services;

// -----------------------------------------------------------------------
// Thin-Delegate: Alle oeffentlichen Signaturen bleiben 1:1 erhalten;
// die Implementierung delegiert an die Application/Infrastructure-Klassen.
//
// Bewegte Logik:
//   ApplyRuntimeDefaults     -> Application.Ai.Startup.AiStartupPlanBuilder
//   BuildOllamaPreloadRequests -> Application.Ai.Startup.AiStartupPlanBuilder
//   BuildModelLabel          -> Application.Ai.Startup.AiStartupPlanBuilder
//   BuildRuntimeStatusText   -> Application.Ai.Startup.AiStartupResultSummarizer
//   StartAsync-Sequenz       -> Application.Ai.Startup.AiStartupOrchestrator
//   FindDefaultSidecarScript -> Infrastructure.Ai.Startup.SidecarScriptLocator
//   ResolvePowerShellExe     -> Infrastructure.Ai.Startup.SidecarScriptLocator
//   DefaultAiStartupLauncher -> Infrastructure.Ai.Startup.DefaultAiStartupLauncher
// -----------------------------------------------------------------------

public static class AiStartupService
{
    // ------------------------------------------------------------------ Oeffentliche API (unveraendert)

    /// <summary>
    /// Setzt fehlende Pflichteinstellungen auf sinnvolle Standardwerte.
    /// Gibt true zurueck, wenn mindestens eine Einstellung veraendert wurde.
    /// </summary>
    public static bool ApplyRuntimeDefaults(AppSettings settings)
        => AiStartupPlanBuilder.ApplyRuntimeDefaults(settings);

    public static Task<AiStartupResult> StartAsync(
        AppSettings settings,
        CancellationToken ct = default)
        => StartCoreAsync(
            settings,
            new DefaultAiStartupLauncher(),
            AiSettingsFactory.Current,
            SidecarScriptLocator.Current,
            SidecarTokenResolver.Current,
            sidecarScriptPath: null,
            progress: null,
            ct);

    public static Task<AiStartupResult> StartAsync(
        AppSettings settings,
        IProgress<string> progress,
        CancellationToken ct = default)
        => StartCoreAsync(
            settings,
            new DefaultAiStartupLauncher(),
            AiSettingsFactory.Current,
            SidecarScriptLocator.Current,
            SidecarTokenResolver.Current,
            sidecarScriptPath: null,
            progress,
            ct);

    public static Task<AiStartupResult> StartAsync(
        AppSettings settings,
        IAiStartedProcessLifetime startedProcesses,
        IAiPlatformSettingsResolver aiSettings,
        ISidecarScriptLocator sidecarScripts,
        ISidecarTokenResolver sidecarTokens,
        IProgress<string>? progress = null,
        CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(startedProcesses);
        return StartCoreAsync(
            settings,
            new DefaultAiStartupLauncher(startedProcesses),
            aiSettings,
            sidecarScripts,
            sidecarTokens,
            sidecarScriptPath: null,
            progress,
            ct);
    }

    public static Task<AiStartupResult> StartAsync(
        AppSettings settings,
        IAiStartupLauncher launcher,
        string? sidecarScriptPath = null,
        CancellationToken ct = default)
        => StartAsync(settings, launcher, sidecarScriptPath, progress: null, ct);

    public static async Task<AiStartupResult> StartAsync(
        AppSettings settings,
        IAiStartupLauncher launcher,
        string? sidecarScriptPath,
        IProgress<string>? progress,
        CancellationToken ct = default)
        => await StartCoreAsync(
            settings,
            launcher,
            AiSettingsFactory.Current,
            SidecarScriptLocator.Current,
            SidecarTokenResolver.Current,
            sidecarScriptPath,
            progress,
            ct).ConfigureAwait(false);

    private static async Task<AiStartupResult> StartCoreAsync(
        AppSettings settings,
        IAiStartupLauncher launcher,
        IAiPlatformSettingsResolver aiSettings,
        ISidecarScriptLocator sidecarScripts,
        ISidecarTokenResolver sidecarTokens,
        string? sidecarScriptPath,
        IProgress<string>? progress,
        CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(settings);
        ArgumentNullException.ThrowIfNull(launcher);
        ArgumentNullException.ThrowIfNull(aiSettings);
        ArgumentNullException.ThrowIfNull(sidecarScripts);
        ArgumentNullException.ThrowIfNull(sidecarTokens);

        var settingsChanged = ApplyRuntimeDefaults(settings);
        var platform = aiSettings.Load(AppSettingsAiSettingsProvider.ToSource(settings));
        var preloadRequests = AiStartupPlanBuilder.BuildOllamaPreloadRequests(
            platform.VisionModel, platform.TextModel, platform.EmbedModel, platform.OllamaKeepAlive);
        var modelLabel = AiStartupPlanBuilder.BuildModelLabel(
            GetModelNames(preloadRequests), platform.MultiModelEnabled);

        AiRuntimeStatusTracker.MarkStarting(modelLabel);

        var sidecarHeaders = BuildSidecarHeaders(platform.SidecarToken, sidecarTokens);
        var script = sidecarScriptPath ?? sidecarScripts.FindDefaultSidecarScript();
        var psExe = sidecarScripts.ResolvePowerShellExe();

        var input = new AiStartupOrchestratorInput(
            OllamaBaseUri: platform.OllamaBaseUri,
            SidecarUrl: platform.SidecarUrl,
            SidecarHeaders: sidecarHeaders,
            PreloadRequests: preloadRequests,
            SidecarScriptPath: script,
            PowerShellExe: psExe,
            SettingsChanged: settingsChanged,
            SidecarEnvironmentVariables: new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            {
                ["SEWER_SIDECAR_TRAINING_EXPORT_ROOT"] = System.IO.Path.Combine(
                    KnowledgeBasePaths.GetRoot(),
                    "training",
                    "datasets"),
                ["SEWER_SIDECAR_TRAINING_MODEL_CANDIDATES_ROOT"] = System.IO.Path.Combine(
                    KnowledgeBasePaths.GetRoot(),
                    "training",
                    "models",
                    "candidates")
            });

        var result = await AiStartupOrchestrator.StartAsync(input, launcher, progress, ct)
            .ConfigureAwait(false);

        progress?.Report(result.HasWarnings ? "KI gestartet (mit Warnung)" : "KI bereit");
        AiRuntimeStatusTracker.MarkReady(
            modelLabel,
            result.HasWarnings,
            AiStartupResultSummarizer.BuildRuntimeStatusText(result));

        return result;
    }

    /// <summary>
    /// Sucht das Sidecar-Startskript aufwaerts durch die Ordnerhierarchie.
    /// Oeffentlich, damit Tests und andere Dienste den Pfad pruefen koennen.
    /// </summary>
    public static string? FindDefaultSidecarScript()
        => SidecarScriptLocator.FindDefaultSidecarScript();

    // ------------------------------------------------------------------ Hilfsmethoden

    private static IReadOnlyDictionary<string, string>? BuildSidecarHeaders(
        string? configuredToken,
        ISidecarTokenResolver sidecarTokens)
    {
        var token = sidecarTokens.Resolve(configuredToken);
        return token is null
            ? null
            : new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            {
                [SidecarTokenResolver.HeaderName] = token
            };
    }

    private static IEnumerable<string> GetModelNames(IReadOnlyList<AiStartupModelPreloadRequest> requests)
    {
        foreach (var r in requests)
            yield return r.ModelName;
    }
}

// -----------------------------------------------------------------------
// Typ-Re-Exporte: Alle Typen, die in UI.Services.AiStartupService urspruenglich
// definiert waren, werden hier als Aliase sichtbar gemacht, damit bestehende
// Aufrufer keine using-Direktiven aendern muessen.
// -----------------------------------------------------------------------
// Die Typen sind jetzt in Application.Ai.Startup definiert; der Compiler
// findet sie ueber die using-Direktive am Dateianfang automatisch.
// Explizite typedef/alias-Weiterleitung ist in C# nicht noetig –
// die Typen sind durch die using-Direktiven im ganzen Namespace sichtbar.
