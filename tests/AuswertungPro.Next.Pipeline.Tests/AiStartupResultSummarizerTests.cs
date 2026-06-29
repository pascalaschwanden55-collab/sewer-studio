using System.Collections.Generic;
using AuswertungPro.Next.Application.Ai.Startup;

namespace AuswertungPro.Next.Pipeline.Tests;

// -----------------------------------------------------------------------
// Charakterisierungstests fuer AiStartupResultSummarizer.
// Prueft reine Logik ohne Seiteneffekte.
// -----------------------------------------------------------------------

public sealed class AiStartupResultSummarizerTests
{
    [Fact]
    public void BuildRuntimeStatusText_gibt_warnung_wenn_warnungen_vorhanden()
    {
        var result = MakeResult(warnings: ["etwas stimmt nicht"]);
        var text = AiStartupResultSummarizer.BuildRuntimeStatusText(result);
        Assert.Equal("KI gestartet mit Warnung", text);
    }

    [Fact]
    public void BuildRuntimeStatusText_gibt_modelle_geladen_wenn_preloaded_modelle_und_keine_warnung()
    {
        var result = MakeResult(preloadedModels: ["qwen3-vl:8b"]);
        var text = AiStartupResultSummarizer.BuildRuntimeStatusText(result);
        Assert.Equal("Modelle geladen", text);
    }

    [Fact]
    public void BuildRuntimeStatusText_gibt_KI_gestartet_wenn_prozesse_gestartet_wurden()
    {
        var result = MakeResult(ollamaAttempted: true);
        var text = AiStartupResultSummarizer.BuildRuntimeStatusText(result);
        Assert.Equal("KI gestartet", text);
    }

    [Fact]
    public void BuildRuntimeStatusText_gibt_KI_bereit_wenn_nichts_gestartet_und_keine_modelle()
    {
        var result = MakeResult();
        var text = AiStartupResultSummarizer.BuildRuntimeStatusText(result);
        Assert.Equal("KI bereit", text);
    }

    [Fact]
    public void BuildRuntimeStatusText_warnung_hat_hoehere_prioritaet_als_modelle_geladen()
    {
        var result = MakeResult(
            preloadedModels: ["qwen3-vl:8b"],
            warnings: ["Sidecar nicht erreichbar"]);
        var text = AiStartupResultSummarizer.BuildRuntimeStatusText(result);
        Assert.Equal("KI gestartet mit Warnung", text);
    }

    [Fact]
    public void BuildRuntimeStatusText_sidecar_start_gilt_als_prozess_gestartet()
    {
        var result = MakeResult(sidecarAttempted: true);
        var text = AiStartupResultSummarizer.BuildRuntimeStatusText(result);
        Assert.Equal("KI gestartet", text);
    }

    // -------------------------------------------------- Hilfsmethoden

    private static AiStartupResult MakeResult(
        IReadOnlyList<string>? preloadedModels = null,
        IReadOnlyList<string>? warnings = null,
        bool ollamaAttempted = false,
        bool sidecarAttempted = false)
        => new AiStartupResult(
            SettingsChanged: false,
            OllamaReachable: true,
            OllamaStartAttempted: ollamaAttempted,
            OllamaStartSucceeded: false,
            SidecarReachable: true,
            SidecarStartAttempted: sidecarAttempted,
            SidecarStartSucceeded: false,
            PreloadedModels: preloadedModels ?? [],
            Messages: [],
            Warnings: warnings ?? []);
}
