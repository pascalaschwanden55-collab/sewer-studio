using AuswertungPro.Next.Application.Diagnostics;
using AuswertungPro.Next.Application.Import;
using AuswertungPro.Next.Domain.Models;
using AuswertungPro.Next.UI;
using AuswertungPro.Next.UI.Services;
using AuswertungPro.Next.UI.ViewModels;
using AuswertungPro.Next.UI.ViewModels.Pages;
using Microsoft.Extensions.Logging;

namespace AuswertungPro.Next.UI.Tests;

public sealed class ImportPageViewModelProjectToolsTests : IDisposable
{
    private readonly ILoggerFactory _loggerFactory = LoggerFactory.Create(_ => { });

    [Fact]
    public async Task Projekt_portabel_ohne_gespeichertes_Projekt_zeigt_Hinweis()
    {
        var dialogs = new DialogFake();
        var services = new ServiceProvider(
            new AppSettings { EnableRestorePoints = false },
            new DiagnosticsOptions(),
            _loggerFactory.CreateLogger("test"),
            _loggerFactory)
        {
            Dialogs = dialogs
        };
        using var shell = new ShellViewModel(
            services,
            new SystemMonitorService(enableHardwareSensorInit: false));
        var viewModel = new ImportPageViewModel(shell, services);

        await viewModel.MakeProjectPortableCommand.ExecuteAsync(null);

        Assert.Contains("zuerst speichern", dialogs.LastInfoMessage, StringComparison.OrdinalIgnoreCase);
        Assert.Equal("Projekt portabel machen", dialogs.LastInfoTitle);
    }

    [Fact]
    public async Task Portabilitaets_Controller_uebernimmt_Ergebnis_und_speichert_Projekt()
    {
        var dialogs = new DialogFake();
        var service = new PortabilityFake();
        var controller = new ImportProjectPortabilityController(dialogs, service);
        var project = new Project();
        project.Data.Add(new HaltungRecord());
        var progress = string.Empty;
        var summary = string.Empty;
        var details = string.Empty;
        var saveCalls = 0;

        await controller.ExecuteAsync(new ImportProjectPortabilityActions(
            GetProjectFolder: () => @"C:\Projekt",
            GetProject: () => project,
            SaveProject: () =>
            {
                saveCalls++;
                return true;
            },
            SetProgress: value => progress = value,
            AppendSummary: value => summary += value,
            AppendDetails: value => details += value));

        Assert.Equal(1, service.Calls);
        Assert.Equal(1, saveCalls);
        Assert.Equal(string.Empty, progress);
        Assert.Contains("3 Pfade relativ", summary);
        Assert.Contains("Foto A", details);
        Assert.Contains("1:1", dialogs.LastInfoMessage);
    }

    public void Dispose() => _loggerFactory.Dispose();

    private sealed class PortabilityFake : IProjectPortabilityService
    {
        public int Calls { get; private set; }

        public ProjectPortabilityResult MakePortable(string projectFolder, Project project, bool dryRun = false)
        {
            Calls++;
            return new ProjectPortabilityResult(
                RelinkedPaths: 3,
                FotosCopied: 2,
                Unresolved: 1,
                Messages: new[] { "Foto A" });
        }
    }

    private sealed class DialogFake : IDialogService
    {
        public string LastInfoMessage { get; private set; } = string.Empty;
        public string LastInfoTitle { get; private set; } = string.Empty;

        public string? OpenFile(string title, string filter, string? initialDirectory = null) => null;
        public string? SaveFile(string title, string filter, string? defaultExt = null, string? defaultFileName = null) => null;
        public string[] OpenFiles(string title, string filter) => Array.Empty<string>();
        public string? SelectFolder(string title, string? initialPath = null) => null;
        public void Info(string message, string title = "Hinweis")
        {
            LastInfoMessage = message;
            LastInfoTitle = title;
        }
        public void Warn(string message, string title = "Warnung") { }
        public void Error(string message, string title = "Fehler") { }
        public bool Confirm(string message, string title = "Bestaetigung") => false;
        public bool ConfirmWarn(string message, string title = "Bestaetigung", bool defaultNo = true) => false;
        public DialogConfirm ConfirmCancel(string message, string title = "Bestaetigung") => DialogConfirm.Cancel;
    }
}
