using AuswertungPro.Next.Application.Ai;
using AuswertungPro.Next.UI.Ai;
using AuswertungPro.Next.UI.Ai.Live;

namespace AuswertungPro.Next.UI.Tests;

public sealed class LiveDetectionStartupWorkflowTests
{
    [Fact]
    public async Task StartAsync_unchecks_toggle_and_warns_when_settings_cannot_load()
    {
        var calls = new List<string>();

        var result = await LiveDetectionStartupWorkflow.StartAsync(
            loadSettings: () => throw new InvalidOperationException("settings"),
            createRuntimeAsync: _ => throw new InvalidOperationException("should not start"),
            dialogs: Dialogs(calls),
            actions: Actions(calls));

        Assert.False(result);
        Assert.Equal(["warn:KI-Konfiguration konnte nicht geladen werden.", "uncheck"], calls);
    }

    [Fact]
    public async Task StartAsync_unchecks_toggle_and_informs_when_ai_is_disabled()
    {
        var calls = new List<string>();

        var result = await LiveDetectionStartupWorkflow.StartAsync(
            loadSettings: () => Settings(enabled: false),
            createRuntimeAsync: _ => throw new InvalidOperationException("should not start"),
            dialogs: Dialogs(calls),
            actions: Actions(calls));

        Assert.False(result);
        Assert.Equal(["info:KI ist deaktiviert. Bitte in den Einstellungen aktivieren.", "uncheck"], calls);
    }

    [Fact]
    public async Task StartAsync_creates_and_starts_runtime_when_enabled()
    {
        var calls = new List<string>();

        var result = await LiveDetectionStartupWorkflow.StartAsync(
            loadSettings: () => Settings(enabled: true),
            createRuntimeAsync: settings =>
            {
                calls.Add($"create:{settings.VisionModel}");
                return Task.FromResult(new LiveDetectionRuntime(null!, null!, "selected-vision"));
            },
            dialogs: Dialogs(calls),
            actions: Actions(calls));

        Assert.True(result);
        Assert.Equal(["create:configured-vision", "start:selected-vision"], calls);
    }

    [Fact]
    public async Task StartAsync_unchecks_toggle_and_warns_when_runtime_start_fails()
    {
        var calls = new List<string>();

        var result = await LiveDetectionStartupWorkflow.StartAsync(
            loadSettings: () => Settings(enabled: true),
            createRuntimeAsync: _ => throw new InvalidOperationException("Port belegt"),
            dialogs: Dialogs(calls),
            actions: Actions(calls));

        Assert.False(result);
        Assert.Equal(["uncheck", "warn:Live-KI konnte nicht gestartet werden: Port belegt"], calls);
    }

    private static LiveDetectionStartupActions Actions(List<string> calls)
        => new(
            UncheckToggle: () => calls.Add("uncheck"),
            StartRuntime: runtime => calls.Add($"start:{runtime.VisionModel}"));

    private static LiveDetectionDialogService Dialogs(List<string> calls)
        => new(
            (message, _) => calls.Add($"warn:{message}"),
            (message, _) => calls.Add($"info:{message}"));

    private static AiRuntimeSettings Settings(bool enabled)
        => new(
            Enabled: enabled,
            OllamaBaseUri: new Uri("http://localhost:11434"),
            VisionModel: "configured-vision",
            TextModel: "text",
            EmbedModel: null,
            FfmpegPath: null,
            OllamaRequestTimeout: TimeSpan.FromSeconds(30),
            OllamaKeepAlive: "5m",
            OllamaNumCtx: 4096);
}
