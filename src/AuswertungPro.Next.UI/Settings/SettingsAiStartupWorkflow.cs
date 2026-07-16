using System;
using System.Threading;
using System.Threading.Tasks;
using AuswertungPro.Next.Application.Ai;
using AuswertungPro.Next.Application.Common;
using AuswertungPro.Next.Application.Ai.Startup;
using AuswertungPro.Next.UI.Services;

namespace AuswertungPro.Next.UI.Settings;

public sealed record SettingsAiStartupWorkflowUi(
    Func<bool> GetIsStarting,
    Action<bool> SetIsStarting,
    Action<string> SetStatusText);

public sealed record SettingsAiStartupWorkflowRequest(
    AppSettings Settings,
    IDialogService Dialogs,
    SettingsAiStartupWorkflowUi Ui,
    Func<AppSettings, IProgress<string>, CancellationToken, Task<AiStartupResult>> StartAsync,
    Action SaveSettingsImmediate);

public static class SettingsAiStartupWorkflow
{
    public static Task RunAsync(
        AppSettings settings,
        IDialogService dialogs,
        SettingsAiStartupWorkflowUi ui,
        Action saveSettingsImmediate,
        CancellationToken ct = default)
        => RunAsync(
            new SettingsAiStartupWorkflowRequest(
                settings,
                dialogs,
                ui,
                StartWithDefaultsAsync,
                saveSettingsImmediate),
            ct);

    public static Task RunAsync(
        AppSettings settings,
        IDialogService dialogs,
        SettingsAiStartupWorkflowUi ui,
        Action saveSettingsImmediate,
        IAiStartedProcessLifetime startedProcesses,
        IAiPlatformSettingsResolver aiSettings,
        ISidecarScriptLocator sidecarScripts,
        ISidecarTokenResolver sidecarTokens,
        CancellationToken ct = default)
        => RunAsync(
            new SettingsAiStartupWorkflowRequest(
                settings,
                dialogs,
                ui,
                (currentSettings, progress, token) => AiStartupService.StartAsync(
                    currentSettings,
                    startedProcesses,
                    aiSettings,
                    sidecarScripts,
                    sidecarTokens,
                    progress,
                    token),
                saveSettingsImmediate),
            ct);

    public static async Task RunAsync(
        SettingsAiStartupWorkflowRequest request,
        CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(request);

        if (request.Ui.GetIsStarting())
            return;

        request.Ui.SetIsStarting(true);
        request.Ui.SetStatusText("Starte KI...");

        try
        {
            var progress = new InlineProgress<string>(request.Ui.SetStatusText);
            var result = await request.StartAsync(request.Settings, progress, ct).ConfigureAwait(true);
            request.SaveSettingsImmediate();

            request.Ui.SetStatusText(result.HasWarnings
                ? "KI-Start mit Warnung."
                : "KI gestartet.");

            if (result.HasWarnings)
                request.Dialogs.Info(result.Summary, "KI starten");
        }
        catch (Exception ex)
        {
            var userMessage = UserError.DescribeAndReport(ex, "KI aus Einstellungen starten");
            request.Ui.SetStatusText($"KI-Start fehlgeschlagen: {userMessage}");
            request.Dialogs.Error($"KI-Start fehlgeschlagen: {userMessage}", "KI starten");
        }
        finally
        {
            request.Ui.SetIsStarting(false);
        }
    }

    private static Task<AiStartupResult> StartWithDefaultsAsync(
        AppSettings settings,
        IProgress<string> progress,
        CancellationToken ct)
        => AiStartupService.StartAsync(settings, progress, ct);

    private sealed class InlineProgress<T> : IProgress<T>
    {
        private readonly Action<T> _handler;

        public InlineProgress(Action<T> handler)
            => _handler = handler ?? throw new ArgumentNullException(nameof(handler));

        public void Report(T value) => _handler(value);
    }
}
