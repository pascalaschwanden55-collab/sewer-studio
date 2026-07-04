using System;
using AuswertungPro.Next.Application.Diagnostics;
using AuswertungPro.Next.UI.Services;

namespace AuswertungPro.Next.UI.Settings;

public sealed record SettingsSaveValues(
    bool EnableDiagnostics,
    string? PdfToTextPath,
    string? ProjectPath,
    string? ProjectsRootDirectory,
    string? VideoFolder,
    string? KantonUriXtfDirectory,
    AutoSaveMode DataAutoSaveMode,
    bool EnableRestorePoints,
    bool VideoHwDecoding,
    bool VideoDropLateFrames,
    bool VideoSkipFrames,
    int VideoFileCachingMs,
    int VideoNetworkCachingMs,
    int VideoCodecThreads,
    string? VideoOutput,
    string? UiTheme,
    bool StartAiOnProgramStart,
    double PipelineYoloConfidence,
    double PipelineDinoBoxThreshold,
    double PipelineDinoTextThreshold);

public sealed record SettingsSaveWorkflowRequest(
    AppSettings Settings,
    DiagnosticsOptions Diagnostics,
    SettingsSaveValues Values,
    Action SaveSettings);

public static class SettingsSaveWorkflow
{
    public static void Save(SettingsSaveWorkflowRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);

        var settings = request.Settings;
        var values = request.Values;

        settings.EnableDiagnostics = values.EnableDiagnostics;
        settings.PdfToTextPath = values.PdfToTextPath;
        settings.LastProjectPath = NormalizeProjectPath(values.ProjectPath);
        settings.ProjectsRootDirectory = NormalizeOptionalPath(values.ProjectsRootDirectory);
        settings.LastVideoSourceFolder = values.VideoFolder;
        settings.LastVideoFolder = values.VideoFolder;
        settings.KantonUriXtfDirectory = NormalizeRequiredPath(values.KantonUriXtfDirectory);
        settings.DataAutoSaveMode = values.DataAutoSaveMode.Normalize();
        settings.EnableRestorePoints = values.EnableRestorePoints;
        settings.VideoHwDecoding = values.VideoHwDecoding;
        settings.VideoDropLateFrames = values.VideoDropLateFrames;
        settings.VideoSkipFrames = values.VideoSkipFrames;
        settings.VideoFileCachingMs = ClampCaching(values.VideoFileCachingMs);
        settings.VideoNetworkCachingMs = ClampCaching(values.VideoNetworkCachingMs);
        settings.VideoCodecThreads = ClampCodecThreads(values.VideoCodecThreads);
        settings.VideoOutput = NormalizeVideoOutput(values.VideoOutput);
        settings.UiTheme = ThemeManager.NormalizeTheme(values.UiTheme);
        settings.AiStartOnProgramStart = values.StartAiOnProgramStart;
        settings.PipelineYoloConfidence = ClampThreshold(values.PipelineYoloConfidence);
        settings.PipelineDinoBoxThreshold = ClampThreshold(values.PipelineDinoBoxThreshold);
        settings.PipelineDinoTextThreshold = ClampThreshold(values.PipelineDinoTextThreshold);
        request.SaveSettings();

        request.Diagnostics.EnableDiagnostics = values.EnableDiagnostics;
        request.Diagnostics.ExplicitPdfToTextPath = values.PdfToTextPath;
    }

    public static int ClampCaching(int value)
        => Math.Clamp(value, 100, 10000);

    public static int ClampCodecThreads(int value)
        => Math.Clamp(value, 1, 16);

    public static string NormalizeVideoOutput(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
            return "direct3d11";

        var normalized = value.Trim().ToLowerInvariant();
        return normalized is "direct3d11" or "direct3d9" or "any"
            ? normalized
            : "direct3d11";
    }

    public static double ClampThreshold(double value)
        => Math.Clamp(value, 0d, 1d);

    private static string? NormalizeProjectPath(string? value)
    {
        var trimmed = NormalizeOptionalPath(value);
        if (trimmed is null)
            return null;

        if (!trimmed.EndsWith(".json", StringComparison.OrdinalIgnoreCase))
            trimmed += ".json";

        return trimmed;
    }

    private static string? NormalizeOptionalPath(string? value)
        => string.IsNullOrWhiteSpace(value) ? null : value.Trim();

    private static string NormalizeRequiredPath(string? value)
        => string.IsNullOrWhiteSpace(value) ? string.Empty : value.Trim();
}
