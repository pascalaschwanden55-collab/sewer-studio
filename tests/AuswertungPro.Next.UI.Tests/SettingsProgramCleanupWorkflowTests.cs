using System.IO;
using AuswertungPro.Next.Application.Maintenance;
using AuswertungPro.Next.Infrastructure.Maintenance;
using AuswertungPro.Next.UI.Services;
using AuswertungPro.Next.UI.Settings;

namespace AuswertungPro.Next.UI.Tests;

public sealed class SettingsProgramCleanupWorkflowTests
{
    [Fact]
    public async Task RunAsync_requires_confirmation_before_deleting()
    {
        var root = NewTestRoot();
        var programRoot = Path.Combine(root, "program");
        var tempRoot = Path.Combine(root, "windows-temp");
        var cacheFile = Path.Combine(programRoot, ".tmp", "cache.bin");
        WriteFile(cacheFile, 2_048);
        Directory.CreateDirectory(tempRoot);

        try
        {
            var dialogs = new DialogFake { ConfirmWarnResult = false };
            var toasts = new ToastFake();
            var states = new List<bool>();
            var statuses = new List<string>();
            var request = Request(
                programRoot,
                tempRoot,
                dialogs,
                toasts,
                states,
                statuses);

            await SettingsProgramCleanupWorkflow.RunAsync(request);

            Assert.True(File.Exists(cacheFile));
            Assert.Equal([true, false], states);
            Assert.Equal("Bereinigung nicht gestartet.", statuses[^1]);
            Assert.Contains("Geschuetzt bleiben Projektdateien", dialogs.ConfirmWarnMessage);
            Assert.Empty(toasts.Messages);
        }
        finally
        {
            DeleteTestRoot(root);
        }
    }

    [Fact]
    public async Task RunAsync_deletes_confirmed_cache_and_reports_freed_space()
    {
        var root = NewTestRoot();
        var programRoot = Path.Combine(root, "program");
        var tempRoot = Path.Combine(root, "windows-temp");
        var cacheDirectory = Path.Combine(programRoot, ".tmp");
        WriteFile(Path.Combine(cacheDirectory, "cache.bin"), 4_096);
        Directory.CreateDirectory(tempRoot);

        try
        {
            var dialogs = new DialogFake { ConfirmWarnResult = true };
            var toasts = new ToastFake();
            var states = new List<bool>();
            var statuses = new List<string>();

            await SettingsProgramCleanupWorkflow.RunAsync(Request(
                programRoot,
                tempRoot,
                dialogs,
                toasts,
                states,
                statuses));

            Assert.False(Directory.Exists(cacheDirectory));
            Assert.Equal([true, false], states);
            Assert.StartsWith("Bereinigt:", statuses[^1], StringComparison.Ordinal);
            Assert.Contains(toasts.Messages, message => message.StartsWith("success:", StringComparison.Ordinal));
            Assert.Empty(dialogs.Errors);
        }
        finally
        {
            DeleteTestRoot(root);
        }
    }

    [Fact]
    public void RequestFactory_finds_program_root_and_protects_known_projects()
    {
        var root = NewTestRoot();
        var appBase = Path.Combine(root, "src", "AuswertungPro.Next.UI", "bin", "Debug", "net10.0");
        var projectRoot = Path.Combine(root, "customer-project");
        var projectFile = Path.Combine(projectRoot, "Projektdateien", "projekt.json");
        var projectsRoot = Path.Combine(root, ".tmp-projects");
        var systemTemp = Path.Combine(root, "windows-temp");

        try
        {
            Directory.CreateDirectory(appBase);
            Directory.CreateDirectory(systemTemp);
            File.WriteAllText(Path.Combine(root, "AuswertungPro.sln"), "test");
            var settings = new AppSettings
            {
                LastProjectPath = projectFile,
                RecentProjectPaths = [projectFile],
                ProjectsRootDirectory = projectsRoot
            };
            var now = new DateTime(2026, 7, 11, 10, 0, 0, DateTimeKind.Utc);

            var request = SettingsProgramCleanupRequestFactory.Create(
                settings,
                appBase,
                root,
                systemTemp,
                now);

            Assert.Equal(Path.GetFullPath(root), Path.GetFullPath(request.ProgramRoot));
            Assert.Contains(
                request.ProtectedProjectRoots!,
                path => string.Equals(
                    Path.GetFullPath(path),
                    Path.GetFullPath(projectRoot),
                    StringComparison.OrdinalIgnoreCase));
            Assert.Contains(
                request.ProtectedProjectRoots!,
                path => string.Equals(
                    Path.GetFullPath(path),
                    Path.GetFullPath(projectsRoot),
                    StringComparison.OrdinalIgnoreCase));
            Assert.Equal(now.AddDays(-1), request.TemporaryFileCutoffUtc);
        }
        finally
        {
            DeleteTestRoot(root);
        }
    }

    [Fact]
    public void RequestFactory_verwendet_die_injizierte_Programmordner_Suche()
    {
        var locator = new ProgramRootLocatorFake("C:\\SewerStudio-Test");
        var settings = new AppSettings();

        var request = SettingsProgramCleanupRequestFactory.Create(
            settings,
            "C:\\App",
            "C:\\Arbeitsordner",
            "C:\\Temp",
            new DateTime(2026, 7, 15, 10, 0, 0, DateTimeKind.Utc),
            locator);

        Assert.Equal("C:\\SewerStudio-Test", request.ProgramRoot);
        Assert.Equal("C:\\App", locator.AppBaseDirectory);
        Assert.Equal("C:\\Arbeitsordner", locator.CurrentDirectory);
    }

    private static SettingsProgramCleanupWorkflowRequest Request(
        string programRoot,
        string tempRoot,
        DialogFake dialogs,
        ToastFake toasts,
        ICollection<bool> states,
        ICollection<string> statuses)
        => new(
            new ProgramCleanupRequest(
                programRoot,
                tempRoot,
                Path.Combine(programRoot, "runtime"),
                TemporaryFileCutoffUtc: DateTime.UtcNow.AddDays(-1)),
            new ProgramCleanupService(),
            dialogs,
            toasts,
            new SettingsProgramCleanupWorkflowUi(states.Add, statuses.Add));

    private static string NewTestRoot()
    {
        var root = Path.Combine(Path.GetTempPath(), "settings-cleanup-tests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        return root;
    }

    private static void WriteFile(string path, int bytes)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        File.WriteAllBytes(path, new byte[bytes]);
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

    private sealed class ProgramRootLocatorFake(string result) : IProgramRootLocator
    {
        public string? AppBaseDirectory { get; private set; }
        public string? CurrentDirectory { get; private set; }

        public string FindProgramRoot(string appBaseDirectory, string currentDirectory)
        {
            AppBaseDirectory = appBaseDirectory;
            CurrentDirectory = currentDirectory;
            return result;
        }
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
