using System.Collections.Generic;
using AuswertungPro.Next.Application.Ai.Startup;

namespace AuswertungPro.Next.Pipeline.Tests;

// -----------------------------------------------------------------------
// Charakterisierungstests fuer AiStartupPlanBuilder.
// Prueft reine Logik ohne Netzwerkzugriffe.
// -----------------------------------------------------------------------

public sealed class AiStartupPlanBuilderTests
{
    // -------------------------------------------------- ApplyRuntimeDefaults

    [Fact]
    public void ApplyRuntimeDefaults_aktiviert_KI_wenn_deaktiviert()
    {
        var settings = new FakeAiStartupSettings { AiEnabled = false };
        var changed = AiStartupPlanBuilder.ApplyRuntimeDefaults(settings);
        Assert.True(changed);
        Assert.True(settings.AiEnabled);
    }

    [Fact]
    public void ApplyRuntimeDefaults_aktiviert_MultiModel_wenn_deaktiviert()
    {
        var settings = new FakeAiStartupSettings { PipelineMultiModelEnabled = false };
        var changed = AiStartupPlanBuilder.ApplyRuntimeDefaults(settings);
        Assert.True(changed);
        Assert.True(settings.PipelineMultiModelEnabled);
    }

    [Fact]
    public void ApplyRuntimeDefaults_setzt_PipelineMode_auf_multimodel()
    {
        var settings = new FakeAiStartupSettings { PipelineMode = "ollamaonly" };
        AiStartupPlanBuilder.ApplyRuntimeDefaults(settings);
        Assert.Equal("multimodel", settings.PipelineMode);
    }

    [Fact]
    public void ApplyRuntimeDefaults_behaelt_bestehenden_OllamaUrl()
    {
        var settings = new FakeAiStartupSettings { AiOllamaUrl = "http://custom:9999" };
        AiStartupPlanBuilder.ApplyRuntimeDefaults(settings);
        Assert.Equal("http://custom:9999", settings.AiOllamaUrl);
    }

    [Fact]
    public void ApplyRuntimeDefaults_setzt_StandardUrl_wenn_leer()
    {
        var settings = new FakeAiStartupSettings { AiOllamaUrl = null };
        AiStartupPlanBuilder.ApplyRuntimeDefaults(settings);
        Assert.Equal(AiStartupPlanBuilder.DefaultOllamaUrl, settings.AiOllamaUrl);
    }

    [Fact]
    public void ApplyRuntimeDefaults_setzt_SidecarUrl_wenn_leer()
    {
        var settings = new FakeAiStartupSettings { PipelineSidecarUrl = null };
        AiStartupPlanBuilder.ApplyRuntimeDefaults(settings);
        Assert.Equal(AiStartupPlanBuilder.DefaultSidecarUrl, settings.PipelineSidecarUrl);
    }

    [Fact]
    public void ApplyRuntimeDefaults_setzt_KeepAlive_wenn_leer()
    {
        var settings = new FakeAiStartupSettings { AiOllamaKeepAlive = null };
        AiStartupPlanBuilder.ApplyRuntimeDefaults(settings);
        Assert.Equal("24h", settings.AiOllamaKeepAlive);
    }

    [Fact]
    public void ApplyRuntimeDefaults_gibt_false_wenn_alles_schon_gesetzt()
    {
        var settings = new FakeAiStartupSettings
        {
            AiEnabled = true,
            PipelineMultiModelEnabled = true,
            PipelineMode = "multimodel",
            AiOllamaUrl = AiStartupPlanBuilder.DefaultOllamaUrl,
            PipelineSidecarUrl = AiStartupPlanBuilder.DefaultSidecarUrl,
            AiOllamaKeepAlive = "24h"
        };
        var changed = AiStartupPlanBuilder.ApplyRuntimeDefaults(settings);
        Assert.False(changed);
    }

    [Fact]
    public void ApplyRuntimeDefaults_case_insensitive_fuer_multimodel()
    {
        var settings = new FakeAiStartupSettings { PipelineMode = "MULTIMODEL" };
        AiStartupPlanBuilder.ApplyRuntimeDefaults(settings);
        // Kein Aendern wenn schon gleich (case-insensitive)
        Assert.Equal("MULTIMODEL", settings.PipelineMode);
    }

    // -------------------------------------------------- BuildOllamaPreloadRequests

    [Fact]
    public void BuildOllamaPreloadRequests_erstellt_generate_request_fuer_vision()
    {
        var reqs = AiStartupPlanBuilder.BuildOllamaPreloadRequests("qwen3-vl:8b", null, null, "24h");
        Assert.Single(reqs);
        Assert.Equal("qwen3-vl:8b", reqs[0].ModelName);
        Assert.Equal(AiStartupModelKind.Generate, reqs[0].Kind);
        Assert.Equal("24h", reqs[0].KeepAlive);
    }

    [Fact]
    public void BuildOllamaPreloadRequests_erstellt_embed_request_fuer_embed_modell()
    {
        var reqs = AiStartupPlanBuilder.BuildOllamaPreloadRequests(null, null, "nomic-embed", "12h");
        Assert.Single(reqs);
        Assert.Equal("nomic-embed", reqs[0].ModelName);
        Assert.Equal(AiStartupModelKind.Embed, reqs[0].Kind);
    }

    [Fact]
    public void BuildOllamaPreloadRequests_entfernt_duplikate_case_insensitive()
    {
        // Vision und Text sind dasselbe Modell (anderer Case) -> nur 1 Request
        var reqs = AiStartupPlanBuilder.BuildOllamaPreloadRequests("Qwen3-VL:8b", "qwen3-vl:8b", null, "24h");
        Assert.Single(reqs);
    }

    [Fact]
    public void BuildOllamaPreloadRequests_leere_modelle_werden_uebersprungen()
    {
        var reqs = AiStartupPlanBuilder.BuildOllamaPreloadRequests(null, "  ", "", "24h");
        Assert.Empty(reqs);
    }

    [Fact]
    public void BuildOllamaPreloadRequests_drei_verschiedene_modelle()
    {
        var reqs = AiStartupPlanBuilder.BuildOllamaPreloadRequests(
            "qwen3-vl:8b", "qwen3:8b", "nomic-embed", "24h");
        Assert.Equal(3, reqs.Count);
        Assert.Equal("qwen3-vl:8b", reqs[0].ModelName);
        Assert.Equal("qwen3:8b", reqs[1].ModelName);
        Assert.Equal("nomic-embed", reqs[2].ModelName);
        Assert.Equal(AiStartupModelKind.Generate, reqs[0].Kind);
        Assert.Equal(AiStartupModelKind.Generate, reqs[1].Kind);
        Assert.Equal(AiStartupModelKind.Embed, reqs[2].Kind);
    }

    // -------------------------------------------------- BuildModelLabel

    [Fact]
    public void BuildModelLabel_mit_single_model_und_multimodel_fuegt_sidecar_hinzu()
    {
        var label = AiStartupPlanBuilder.BuildModelLabel(["qwen3-vl:8b"], multiModelEnabled: true);
        Assert.Equal("qwen3-vl:8b + Sidecar", label);
    }

    [Fact]
    public void BuildModelLabel_ohne_multimodel_zeigt_nur_modellnamen()
    {
        var label = AiStartupPlanBuilder.BuildModelLabel(["qwen3-vl:8b"], multiModelEnabled: false);
        Assert.Equal("qwen3-vl:8b", label);
    }

    [Fact]
    public void BuildModelLabel_leer_verwendet_fallback_Qwen_VL()
    {
        var label = AiStartupPlanBuilder.BuildModelLabel([], multiModelEnabled: false);
        Assert.Equal("Qwen-VL", label);
    }

    [Fact]
    public void BuildModelLabel_entfernt_duplikate_und_verbindet_mit_plus()
    {
        var label = AiStartupPlanBuilder.BuildModelLabel(
            ["qwen3-vl:8b", "QWEN3-VL:8b", "qwen3:8b"], multiModelEnabled: false);
        // Duplikat entfernt -> "qwen3-vl:8b + qwen3:8b"
        Assert.Equal("qwen3-vl:8b + qwen3:8b", label);
    }

    [Fact]
    public void BuildModelLabel_leer_mit_multimodel_fuegt_sidecar_nach_fallback()
    {
        var label = AiStartupPlanBuilder.BuildModelLabel([], multiModelEnabled: true);
        Assert.Equal("Qwen-VL + Sidecar", label);
    }

    // -------------------------------------------------- Hilfsmethode: Fake-Implementierung

    private sealed class FakeAiStartupSettings : IAiStartupSettings
    {
        public bool? AiEnabled { get; set; }
        public bool? PipelineMultiModelEnabled { get; set; }
        public string? PipelineMode { get; set; }
        public string? AiOllamaUrl { get; set; }
        public string? PipelineSidecarUrl { get; set; }
        public string? AiOllamaKeepAlive { get; set; }
    }
}
