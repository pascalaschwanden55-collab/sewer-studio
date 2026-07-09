using System.IO;
using System.Text;
using AuswertungPro.Next.Application.Diagnostics;
using AuswertungPro.Next.Domain.Models;
using AuswertungPro.Next.UI;
using AuswertungPro.Next.UI.Services;
using AuswertungPro.Next.UI.ViewModels;
using AuswertungPro.Next.UI.ViewModels.Pages;
using Microsoft.Extensions.Logging;

namespace AuswertungPro.Next.UI.Tests;

public sealed class OverviewPreviewPdfCommandTests
{
    [Fact]
    public async Task PrintPreviewPdfCommand_speichert_pdf_fuer_aktives_projekt()
    {
        using var temp = new TempDir();
        var output = Path.Combine(temp.Path, "preview.pdf");
        using var loggerFactory = LoggerFactory.Create(_ => { });
        var services = new ServiceProvider(
            new AppSettings { EnableRestorePoints = false },
            new DiagnosticsOptions(),
            loggerFactory.CreateLogger("test"),
            loggerFactory);
        var dialogs = new DialogFake(output);
        services.Dialogs = dialogs;
        using var shell = new ShellViewModel(services, new SystemMonitorService(enableHardwareSensorInit: false));

        shell.Project.Name = "Projekt A";
        shell.Project.Data.Add(Holding("H1"));
        shell.MarkProjectReady();
        shell.EnterWorkspaceOn("Uebersicht");
        var vm = Assert.IsType<OverviewPageViewModel>(shell.CurrentPage);

        Assert.True(vm.PrintPreviewPdfCommand.CanExecute(null));
        await vm.PrintPreviewPdfCommand.ExecuteAsync(null);

        Assert.True(File.Exists(output));
        var bytes = File.ReadAllBytes(output);
        Assert.Equal("%PDF", Encoding.ASCII.GetString(bytes, 0, 4));
        Assert.Contains(dialogs.SaveFileCalls, call =>
            call.Title == "Projektvorschau PDF speichern"
            && call.DefaultExt == "pdf"
            && call.DefaultFileName is not null
            && call.DefaultFileName.StartsWith("Projektvorschau_Projekt A_", StringComparison.Ordinal));
        Assert.NotNull(dialogs.LastInfo);
        Assert.Equal("Projektvorschau", dialogs.LastInfo.Value.Title);
    }

    [Fact]
    public void BuildPreviewPdfFileName_ersetzt_ungueltige_dateizeichen()
    {
        var name = OverviewPageViewModel.BuildPreviewPdfFileName("A:B/C");

        Assert.StartsWith("Projektvorschau_A_B_C_", name, StringComparison.Ordinal);
        Assert.EndsWith(".pdf", name, StringComparison.Ordinal);
    }

    private static HaltungRecord Holding(string name)
    {
        var record = new HaltungRecord();
        record.SetFieldValue("Haltungsname", name, FieldSource.Manual, false);
        record.SetFieldValue("Zustandsklasse", "2", FieldSource.Manual, false);
        record.SetFieldValue("Haltungslaenge_m", "12.5", FieldSource.Manual, false);
        return record;
    }

    private sealed class DialogFake(string saveFileResult) : IDialogService
    {
        public List<(string Title, string Filter, string? DefaultExt, string? DefaultFileName)> SaveFileCalls { get; } = new();
        public (string Message, string Title)? LastInfo { get; private set; }

        public string? OpenFile(string title, string filter, string? initialDirectory = null) => null;

        public string? SaveFile(string title, string filter, string? defaultExt = null, string? defaultFileName = null)
        {
            SaveFileCalls.Add((title, filter, defaultExt, defaultFileName));
            return saveFileResult;
        }

        public string[] OpenFiles(string title, string filter) => [];
        public string? SelectFolder(string title, string? initialPath = null) => null;
        public void Info(string message, string title = "Hinweis") => LastInfo = (message, title);
        public void Warn(string message, string title = "Warnung") { }
        public void Error(string message, string title = "Fehler") => throw new InvalidOperationException(message);
        public bool Confirm(string message, string title = "Bestaetigung") => false;
        public bool ConfirmWarn(string message, string title = "Bestaetigung", bool defaultNo = true) => false;
        public DialogConfirm ConfirmCancel(string message, string title = "Bestaetigung") => DialogConfirm.No;
    }

    private sealed class TempDir : IDisposable
    {
        public string Path { get; } = System.IO.Path.Combine(
            System.IO.Path.GetTempPath(),
            "OverviewPreviewPdfCommandTests_" + Guid.NewGuid().ToString("N"));

        public TempDir()
            => Directory.CreateDirectory(Path);

        public void Dispose()
        {
            try
            {
                if (Directory.Exists(Path))
                    Directory.Delete(Path, recursive: true);
            }
            catch
            {
                // Best-effort cleanup.
            }
        }
    }
}
