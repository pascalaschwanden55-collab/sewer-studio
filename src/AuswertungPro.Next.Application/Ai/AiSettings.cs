using System;
using System.Collections.Generic;

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
    string? SidecarToken = null,
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
    string? SidecarToken,
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
        SidecarToken,
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
