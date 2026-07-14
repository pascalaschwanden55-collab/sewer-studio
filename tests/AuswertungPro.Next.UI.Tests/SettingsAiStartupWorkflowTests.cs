using AuswertungPro.Next.Application.Ai.Startup;
using AuswertungPro.Next.UI;
using AuswertungPro.Next.UI.Settings;

namespace AuswertungPro.Next.UI.Tests;

public sealed class SettingsAiStartupWorkflowTests
{
    [Fact]
    public async Task RunAsync_success_starts_ai_saves_settings_and_updates_status()
    {
        var settings = new AppSettings();
        var dialogs = new DialogFake();
        var state = new UiState();
        var calls = new List<string>();

        await SettingsAiStartupWorkflow.RunAsync(
            Request(
                settings,
                dialogs,
                state,
                calls,
                start: (currentSettings, progress, _) =>
                {
                    Assert.Same(settings, currentSettings);
                    progress.Report("Lade Modelle...");
                    calls.Add("start");
                    return Task.FromResult(Result(messages: ["Bereit"]));
                }),
            CancellationToken.None);

        Assert.Equal(["start", "save"], calls);
        Assert.Equal("KI gestartet.", state.Status);
        Assert.False(state.IsStarting);
        Assert.Empty(dialogs.Infos);
        Assert.Empty(dialogs.Errors);
    }

    [Fact]
    public async Task RunAsync_warning_updates_status_and_shows_summary()
    {
        var dialogs = new DialogFake();
        var state = new UiState();
        var calls = new List<string>();

        await SettingsAiStartupWorkflow.RunAsync(
            Request(
                new AppSettings(),
                dialogs,
                state,
                calls,
                start: (_, _, _) => Task.FromResult(Result(
                    messages: ["Bereit"],
                    warnings: ["Modell konnte nicht vorgeladen werden"]))),
            CancellationToken.None);

        Assert.Equal(["save"], calls);
        Assert.Equal("KI-Start mit Warnung.", state.Status);
        Assert.False(state.IsStarting);
        Assert.Single(dialogs.Infos);
        Assert.Contains("Warnung: Modell konnte nicht vorgeladen werden", dialogs.Infos[0]);
        Assert.Equal("KI starten", dialogs.InfoTitles[0]);
    }

    [Fact]
    public async Task RunAsync_failure_reports_error_and_does_not_save()
    {
        var dialogs = new DialogFake();
        var state = new UiState();
        var calls = new List<string>();

        await SettingsAiStartupWorkflow.RunAsync(
            Request(
                new AppSettings(),
                dialogs,
                state,
                calls,
                start: (_, _, _) =>
                {
                    calls.Add("start");
                    throw new InvalidOperationException("kaputt");
                }),
            CancellationToken.None);

        Assert.Equal(["start"], calls);
        Assert.StartsWith("KI-Start fehlgeschlagen:", state.Status, StringComparison.Ordinal);
        Assert.Contains("Programmlog", state.Status, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("kaputt", state.Status, StringComparison.Ordinal);
        Assert.False(state.IsStarting);
        Assert.Single(dialogs.Errors);
        Assert.Equal(state.Status, dialogs.Errors[0]);
        Assert.Equal("KI starten", dialogs.ErrorTitles[0]);
    }

    [Fact]
    public async Task RunAsync_while_already_starting_does_not_start_again()
    {
        var state = new UiState { IsStarting = true };
        var calls = new List<string>();

        await SettingsAiStartupWorkflow.RunAsync(
            Request(
                new AppSettings(),
                new DialogFake(),
                state,
                calls,
                start: (_, _, _) =>
                {
                    calls.Add("start");
                    return Task.FromResult(Result());
                }),
            CancellationToken.None);

        Assert.True(state.IsStarting);
        Assert.Equal("", state.Status);
        Assert.Empty(calls);
    }

    private static SettingsAiStartupWorkflowRequest Request(
        AppSettings settings,
        IDialogService dialogs,
        UiState state,
        List<string> calls,
        Func<AppSettings, IProgress<string>, CancellationToken, Task<AiStartupResult>>? start = null)
        => new(
            Settings: settings,
            Dialogs: dialogs,
            Ui: new SettingsAiStartupWorkflowUi(
                GetIsStarting: () => state.IsStarting,
                SetIsStarting: value => state.IsStarting = value,
                SetStatusText: value => state.Status = value),
            StartAsync: start ?? ((_, _, _) =>
            {
                calls.Add("start");
                return Task.FromResult(Result());
            }),
            SaveSettingsImmediate: () => calls.Add("save"));

    private static AiStartupResult Result(
        IReadOnlyList<string>? messages = null,
        IReadOnlyList<string>? warnings = null)
        => new(
            SettingsChanged: false,
            OllamaReachable: true,
            OllamaStartAttempted: false,
            OllamaStartSucceeded: false,
            SidecarReachable: true,
            SidecarStartAttempted: false,
            SidecarStartSucceeded: false,
            PreloadedModels: [],
            Messages: messages ?? [],
            Warnings: warnings ?? []);

    private sealed class UiState
    {
        public bool IsStarting { get; set; }
        public string Status { get; set; } = "";
    }

    private sealed class DialogFake : IDialogService
    {
        public List<string> Infos { get; } = new();
        public List<string> InfoTitles { get; } = new();
        public List<string> Errors { get; } = new();
        public List<string> ErrorTitles { get; } = new();

        public string? OpenFile(string title, string filter, string? initialDirectory = null) => null;
        public string? SaveFile(string title, string filter, string? defaultExt = null, string? defaultFileName = null) => null;
        public string[] OpenFiles(string title, string filter) => [];
        public string? SelectFolder(string title, string? initialPath = null) => null;

        public void Info(string message, string title = "Hinweis")
        {
            Infos.Add(message);
            InfoTitles.Add(title);
        }

        public void Warn(string message, string title = "Warnung") { }

        public void Error(string message, string title = "Fehler")
        {
            Errors.Add(message);
            ErrorTitles.Add(title);
        }

        public bool Confirm(string message, string title = "Bestaetigung") => false;
        public bool ConfirmWarn(string message, string title = "Bestaetigung", bool defaultNo = true) => false;
        public DialogConfirm ConfirmCancel(string message, string title = "Bestaetigung") => DialogConfirm.Cancel;
    }
}
