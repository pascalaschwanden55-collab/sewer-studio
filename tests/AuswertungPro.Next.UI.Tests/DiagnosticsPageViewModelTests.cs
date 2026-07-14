using AuswertungPro.Next.Application.Common;
using AuswertungPro.Next.Application.Diagnostics;
using AuswertungPro.Next.UI.ViewModels.Pages;
using System.IO;

namespace AuswertungPro.Next.UI.Tests;

public sealed class DiagnosticsPageViewModelTests
{
    [Fact]
    public void Konstruktor_zeigt_die_gelesenen_logzeilen()
    {
        var viewModel = new DiagnosticsPageViewModel(
            new FakeLogTailReader(new LogTailReadResult(
                FileExists: true,
                Lines: ["Warnung 1", "Fehler 2"],
                UserMessage: null)));

        Assert.Equal("Warnung 1" + Environment.NewLine + "Fehler 2", viewModel.LogTail);
    }

    [Fact]
    public void Konstruktor_zeigt_verstaendlichen_hinweis_wenn_datei_fehlt()
    {
        var viewModel = new DiagnosticsPageViewModel(
            new FakeLogTailReader(new LogTailReadResult(
                FileExists: false,
                Lines: [],
                UserMessage: null)));

        Assert.Equal("Noch keine Log-Datei vorhanden.", viewModel.LogTail);
    }

    [Fact]
    public void Konstruktor_zeigt_sichere_fachmeldung_statt_roher_fehlerdetails()
    {
        var viewModel = new DiagnosticsPageViewModel(
            new FakeLogTailReader(new LogTailReadResult(
                FileExists: true,
                Lines: [],
                UserMessage: "Tageslog konnte nicht gelesen werden. Details stehen im Programmlog.")));

        Assert.Equal(
            "Tageslog konnte nicht gelesen werden. Details stehen im Programmlog.",
            viewModel.LogTail);
    }

    [Fact]
    public async Task Diagnosepaket_nutzt_gewaehlten_Zielpfad_und_meldet_Erfolg()
    {
        var destination = Path.Combine(Path.GetTempPath(), "SewerStudio-Diagnose-Test.zip");
        var dialogs = new FakeDialogs { SavePath = destination };
        var packages = new FakeDiagnosticsPackageService(
            new DiagnosticsPackageResult(
                true,
                destination,
                2,
                "Diagnosepaket erstellt (2 Logdateien)."));
        var viewModel = new DiagnosticsPageViewModel(
            new FakeLogTailReader(new LogTailReadResult(false, [], null)),
            packages,
            dialogs);

        await viewModel.CreatePackageCommand.ExecuteAsync(null);

        Assert.Equal(destination, packages.RequestedPath);
        Assert.Contains(destination, viewModel.PackageStatus, StringComparison.Ordinal);
        Assert.Contains("Diagnosepaket erstellt", dialogs.InfoMessage, StringComparison.Ordinal);
        Assert.Null(dialogs.WarningMessage);
    }

    [Fact]
    public async Task Diagnosepaket_zeigt_sichere_Warnung_bei_Fehlresultat()
    {
        var dialogs = new FakeDialogs { SavePath = "C:\\Temp\\SewerStudio-Diagnose.zip" };
        var packages = new FakeDiagnosticsPackageService(
            new DiagnosticsPackageResult(
                false,
                null,
                0,
                "Diagnosepaket konnte nicht erstellt werden. Details stehen im Programmlog."));
        var viewModel = new DiagnosticsPageViewModel(
            new FakeLogTailReader(new LogTailReadResult(false, [], null)),
            packages,
            dialogs);

        await viewModel.CreatePackageCommand.ExecuteAsync(null);

        Assert.Equal(packages.Result.UserMessage, viewModel.PackageStatus);
        Assert.Equal(packages.Result.UserMessage, dialogs.WarningMessage);
        Assert.Null(dialogs.InfoMessage);
    }

    [Fact]
    public void Logordner_verwendet_den_injizierten_Oeffnungsdienst()
    {
        var dialogs = new FakeDialogs();
        var packages = new FakeDiagnosticsPackageService(
            new DiagnosticsPackageResult(false, null, 0, "nicht verwendet"));
        var folderOpen = new FolderOpenFake();
        var viewModel = new DiagnosticsPageViewModel(
            new FakeLogTailReader(new LogTailReadResult(false, [], null)),
            packages,
            dialogs,
            folderOpen);

        viewModel.OpenLogFolderCommand.Execute(null);

        Assert.Equal(packages.LogDirectory, folderOpen.OpenedPath);
    }

    private sealed class FakeLogTailReader(LogTailReadResult result) : ILogTailReader
    {
        public LogTailReadResult ReadToday(int maximumLines = 200) => result;
    }

    private sealed class FakeDiagnosticsPackageService(DiagnosticsPackageResult result)
        : IDiagnosticsPackageService
    {
        public DiagnosticsPackageResult Result { get; } = result;
        public string LogDirectory => Path.GetTempPath();
        public string? RequestedPath { get; private set; }

        public Task<DiagnosticsPackageResult> CreateAsync(
            string destinationZipPath,
            CancellationToken cancellationToken = default)
        {
            RequestedPath = destinationZipPath;
            return Task.FromResult(Result);
        }
    }

    private sealed class FakeDialogs : IDialogService
    {
        public string? SavePath { get; init; }
        public string? InfoMessage { get; private set; }
        public string? WarningMessage { get; private set; }

        public string? SaveFile(string title, string filter, string? defaultExt = null, string? defaultFileName = null)
            => SavePath;

        public void Info(string message, string title = "Hinweis") => InfoMessage = message;
        public void Warn(string message, string title = "Warnung") => WarningMessage = message;

        public string? OpenFile(string title, string filter, string? initialDirectory = null) => null;
        public string[] OpenFiles(string title, string filter) => [];
        public string? SelectFolder(string title, string? initialPath = null) => null;
        public void Error(string message, string title = "Fehler") { }
        public bool Confirm(string message, string title = "Bestaetigung") => false;
        public bool ConfirmWarn(string message, string title = "Bestaetigung", bool defaultNo = true) => false;
        public DialogConfirm ConfirmCancel(string message, string title = "Bestaetigung") => DialogConfirm.Cancel;
    }

    private sealed class FolderOpenFake : IFolderOpenService
    {
        public string? OpenedPath { get; private set; }

        public FolderOpenResult EnsureAndOpen(string? path)
        {
            OpenedPath = path;
            return new FolderOpenResult(true, null);
        }
    }
}
