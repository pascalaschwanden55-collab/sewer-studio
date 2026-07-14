using System.IO;
using AuswertungPro.Next.Application.Maintenance;
using AuswertungPro.Next.UI.Services;
using AuswertungPro.Next.UI.Settings;

namespace AuswertungPro.Next.UI.Tests;

public sealed class SettingsCodexArtifactCleanupWorkflowTests
{
    [Fact]
    public async Task RunAsync_requires_separate_confirmation_before_deleting()
    {
        var service = ServiceWithOneItem();
        var dialogs = new DialogFake { ConfirmWarnResult = false };
        var states = new List<bool>();
        var statuses = new List<string>();

        await SettingsCodexArtifactCleanupWorkflow.RunAsync(Request(
            service,
            dialogs,
            new ToastFake(),
            states,
            statuses));

        Assert.Equal(0, service.CleanCalls);
        Assert.Equal([true, false], states);
        Assert.Equal("Bereinigung nicht gestartet.", statuses[^1]);
        Assert.Contains("mindestens 24 Stunden", dialogs.ConfirmWarnMessage);
        Assert.Contains("Projektdateien", dialogs.ConfirmWarnMessage, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task RunAsync_cleans_confirmed_agent_copies_and_reports_result()
    {
        var service = ServiceWithOneItem();
        service.Result = new CodexArtifactCleanupResult(4_096, 2, 1, []);
        var dialogs = new DialogFake { ConfirmWarnResult = true };
        var toasts = new ToastFake();
        var states = new List<bool>();
        var statuses = new List<string>();

        await SettingsCodexArtifactCleanupWorkflow.RunAsync(Request(
            service,
            dialogs,
            toasts,
            states,
            statuses));

        Assert.Equal(1, service.CleanCalls);
        Assert.Equal([true, false], states);
        Assert.StartsWith("Agenten-Daten bereinigt:", statuses[^1], StringComparison.Ordinal);
        Assert.Contains(toasts.Messages, message => message.StartsWith("success:", StringComparison.Ordinal));
        Assert.Empty(dialogs.Errors);
    }

    [Fact]
    public void RequestFactory_uses_program_root_and_protects_last_24_hours()
    {
        var root = NewTestRoot();
        var appBase = Path.Combine(root, "src", "AuswertungPro.Next.UI", "bin", "Debug", "net10.0");
        var now = new DateTime(2026, 7, 14, 12, 0, 0, DateTimeKind.Utc);

        try
        {
            Directory.CreateDirectory(appBase);
            File.WriteAllText(Path.Combine(root, "AuswertungPro.sln"), "test");

            var request = SettingsCodexArtifactCleanupRequestFactory.Create(appBase, root, now);

            Assert.Equal(Path.GetFullPath(root), Path.GetFullPath(request.ProgramRoot));
            Assert.Equal(now.AddDays(-1), request.ActivityCutoffUtc);
        }
        finally
        {
            DeleteTestRoot(root);
        }
    }

    private static CleanupServiceFake ServiceWithOneItem()
    {
        var root = Path.GetTempPath();
        return new CleanupServiceFake
        {
            Report = new CodexArtifactCleanupReport(
                Path.Combine(root, ".codex-artifacts"),
                DateTime.UtcNow.AddDays(-1),
                [new CodexArtifactCleanupItem(Path.Combine(root, ".codex-artifacts", "old-build"), 4_096, 2, DateTime.UtcNow.AddDays(-2))],
                [])
        };
    }

    private static SettingsCodexArtifactCleanupWorkflowRequest Request(
        CleanupServiceFake service,
        DialogFake dialogs,
        ToastFake toasts,
        ICollection<bool> states,
        ICollection<string> statuses)
        => new(
            new CodexArtifactCleanupRequest(Path.GetTempPath(), DateTime.UtcNow.AddDays(-1)),
            service,
            dialogs,
            toasts,
            new SettingsProgramCleanupWorkflowUi(states.Add, statuses.Add));

    private static string NewTestRoot()
    {
        var root = Path.Combine(Path.GetTempPath(), "settings-codex-cleanup-tests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        return root;
    }

    private static void DeleteTestRoot(string root)
    {
        try
        {
            if (Directory.Exists(root))
                Directory.Delete(root, recursive: true);
        }
        catch
        {
            // Test-Cleanup ist best effort.
        }
    }

    private sealed class CleanupServiceFake : ICodexArtifactCleanupService
    {
        public CodexArtifactCleanupReport Report { get; set; } = null!;
        public CodexArtifactCleanupResult Result { get; set; } = new(0, 0, 0, []);
        public int CleanCalls { get; private set; }

        public CodexArtifactCleanupReport Analyze(CodexArtifactCleanupRequest request) => Report;

        public CodexArtifactCleanupResult Clean(
            CodexArtifactCleanupRequest request,
            IReadOnlyCollection<string> approvedPaths)
        {
            CleanCalls++;
            return Result;
        }
    }

    private sealed class DialogFake : IDialogService
    {
        public bool ConfirmWarnResult { get; set; }
        public string ConfirmWarnMessage { get; private set; } = string.Empty;
        public List<string> Errors { get; } = new();

        public string? OpenFile(string title, string filter, string? initialDirectory = null) => null;
        public string? SaveFile(string title, string filter, string? defaultExt = null, string? defaultFileName = null) => null;
        public string[] OpenFiles(string title, string filter) => [];
        public string? SelectFolder(string title, string? initialPath = null) => null;
        public void Info(string message, string title = "Hinweis") { }
        public void Warn(string message, string title = "Warnung") { }
        public void Error(string message, string title = "Fehler") => Errors.Add(message);
        public bool Confirm(string message, string title = "Bestaetigung") => false;
        public bool ConfirmWarn(string message, string title = "Bestaetigung", bool defaultNo = true)
        {
            ConfirmWarnMessage = message;
            return ConfirmWarnResult;
        }
        public DialogConfirm ConfirmCancel(string message, string title = "Bestaetigung") => DialogConfirm.Cancel;
    }

    private sealed class ToastFake : IToastService
    {
        public List<string> Messages { get; } = new();
        public void Success(string message) => Messages.Add("success:" + message);
        public void Info(string message) => Messages.Add("info:" + message);
        public void Warning(string message) => Messages.Add("warning:" + message);
        public void Error(string message) => Messages.Add("error:" + message);
    }
}
