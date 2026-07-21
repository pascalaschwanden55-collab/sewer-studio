using System.IO;
using System.Reflection;
using AuswertungPro.Next.Application.Common;
using AuswertungPro.Next.Domain.Models;
using AuswertungPro.Next.Infrastructure.Media;
using AuswertungPro.Next.UI;
using AuswertungPro.Next.UI.ViewModels;
using AuswertungPro.Next.UI.ViewModels.Pages;
using static AuswertungPro.Next.UI.Tests.TestRepoPaths;

namespace AuswertungPro.Next.UI.Tests;

public sealed class MediaConflictsPageViewModelDependencyTests
{
    [Fact]
    public void ViewModel_speichert_weder_Shell_noch_ServiceProvider_als_Feld()
    {
        var fields = typeof(MediaConflictsPageViewModel).GetFields(
            BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public);

        Assert.DoesNotContain(fields, field => field.FieldType == typeof(ShellViewModel));
        Assert.DoesNotContain(fields, field => field.FieldType == typeof(ServiceProvider));
        Assert.Contains(fields, field => field.FieldType == typeof(ISafeShellOpenService));
        Assert.Contains(fields, field => field.FieldType == typeof(IExplorerRevealService));
        Assert.Equal(
            typeof(MediaConflictCenterService),
            typeof(ServiceProvider).GetProperty(nameof(ServiceProvider.MediaConflictCenter))?.PropertyType);

        var source = File.ReadAllText(RepoFile(
            "src", "AuswertungPro.Next.UI", "ViewModels", "Pages", "MediaConflictsPageViewModel.cs"));
        Assert.DoesNotContain("Process.Start", source, StringComparison.Ordinal);
        Assert.DoesNotContain("ProcessStartInfo", source, StringComparison.Ordinal);
        Assert.DoesNotContain("SafeShellOpen.TryOpen", source, StringComparison.Ordinal);
    }

    [Fact]
    public void Ohne_Projektordner_bleibt_die_Seite_bedienbar_und_meldet_den_Grund()
    {
        var vm = CreateViewModel(new Project(), getProjectFolder: () => null, playVideo: _ => { });

        Assert.Empty(vm.Conflicts);
        Assert.Equal(0, vm.OpenConflictCount);
        Assert.Equal("Projektordner nicht verfuegbar. Bitte Projekt zuerst speichern.", vm.SummaryText);
    }

    [Fact]
    public void Videoaktion_nutzt_den_uebergebenen_Player_statt_selbst_ein_Fenster_zu_erzeugen()
    {
        var tempFile = Path.GetTempFileName();
        try
        {
            string? playedPath = null;
            var vm = CreateViewModel(new Project(), () => null, path => playedPath = path);
            var conflict = new MediaConflictCenterService.MediaConflictCase(
                InfoPath: "info.txt",
                HoldingFolder: "Haltungen/KS1",
                HoldingFolderName: "KS1",
                HoldingRaw: "KS1",
                SourcePdfPath: null,
                DateStamp: null,
                Date: null,
                ExpectedVideoName: null,
                Type: MediaConflictCenterService.ConflictType.Ambiguous,
                Candidates: new[] { tempFile },
                Fingerprint: "test");
            vm.SelectedConflict = new MediaConflictRowViewModel(conflict);

            vm.PlaySelectedCandidateCommand.Execute(null);

            Assert.Equal(tempFile, playedPath);
        }
        finally
        {
            File.Delete(tempFile);
        }
    }

    [Fact]
    public void Pdf_oeffnen_nutzt_den_eingespeisten_Shelldienst()
    {
        const string pdfPath = @"C:\Projekt\haltung.pdf";
        var shellOpen = new ShellOpenFake(result: true);
        var explorer = new ExplorerRevealFake(_ => true);
        var vm = CreateViewModel(
            new Project(),
            getProjectFolder: () => null,
            playVideo: _ => { },
            shellOpen,
            explorer);
        vm.SelectedConflict = CreateConflict(sourcePdfPath: pdfPath);

        vm.OpenPdfCommand.Execute(null);

        Assert.Equal(pdfPath, Assert.Single(shellOpen.Paths));
        Assert.Empty(explorer.Paths);
    }

    [Fact]
    public void Info_oeffnen_faellt_bei_Shellfehler_auf_den_Explorer_zurueck()
    {
        const string infoPath = @"C:\Projekt\info.txt";
        var shellOpen = new ShellOpenFake(result: false);
        var explorer = new ExplorerRevealFake(_ => true);
        var vm = CreateViewModel(
            new Project(),
            getProjectFolder: () => null,
            playVideo: _ => { },
            shellOpen,
            explorer);
        vm.SelectedConflict = CreateConflict(infoPath: infoPath);

        vm.OpenInfoCommand.Execute(null);

        Assert.Equal(infoPath, Assert.Single(shellOpen.Paths));
        Assert.Equal(infoPath, Assert.Single(explorer.Paths));
    }

    [Fact]
    public void Kandidat_oeffnen_versucht_bei_fehlendem_Ziel_den_Elternordner()
    {
        const string candidatePath = @"C:\Projekt\Videos\fehlend.mp4";
        var explorer = new ExplorerRevealFake(_ => false);
        var vm = CreateViewModel(
            new Project(),
            getProjectFolder: () => null,
            playVideo: _ => { },
            new ShellOpenFake(result: true),
            explorer);
        vm.SelectedConflict = CreateConflict(candidates: [candidatePath]);

        vm.OpenSelectedCandidateCommand.Execute(null);

        Assert.Equal([candidatePath, @"C:\Projekt\Videos"], explorer.Paths);
    }

    [Fact]
    public void Haltungsordner_oeffnen_nutzt_den_eingespeisten_Shelldienst()
    {
        const string holdingFolder = @"C:\Projekt\Haltungen\KS1";
        var shellOpen = new ShellOpenFake(result: true);
        var vm = CreateViewModel(
            new Project(),
            getProjectFolder: () => null,
            playVideo: _ => { },
            shellOpen,
            new ExplorerRevealFake(_ => true));
        vm.SelectedConflict = CreateConflict(holdingFolder: holdingFolder);

        vm.OpenHoldingFolderCommand.Execute(null);

        Assert.Equal(holdingFolder, Assert.Single(shellOpen.Paths));
    }

    private static MediaConflictsPageViewModel CreateViewModel(
        Project project,
        Func<string?> getProjectFolder,
        Action<string> playVideo,
        ISafeShellOpenService? shellOpen = null,
        IExplorerRevealService? explorerReveal = null)
        => new(
            getProject: () => project,
            getProjectFolder: getProjectFolder,
            getLastVideoSourceFolder: () => null,
            saveVideoSourceFolder: _ => { },
            dialogs: new DialogFake(),
            service: new MediaConflictCenterService(),
            setStatus: _ => { },
            playVideo: playVideo,
            shellOpen: shellOpen ?? new ShellOpenFake(result: true),
            explorerReveal: explorerReveal ?? new ExplorerRevealFake(_ => true));

    private static MediaConflictRowViewModel CreateConflict(
        string infoPath = "info.txt",
        string holdingFolder = "Haltungen/KS1",
        string? sourcePdfPath = null,
        IReadOnlyList<string>? candidates = null)
        => new(new MediaConflictCenterService.MediaConflictCase(
            InfoPath: infoPath,
            HoldingFolder: holdingFolder,
            HoldingFolderName: "KS1",
            HoldingRaw: "KS1",
            SourcePdfPath: sourcePdfPath,
            DateStamp: null,
            Date: null,
            ExpectedVideoName: null,
            Type: MediaConflictCenterService.ConflictType.Ambiguous,
            Candidates: candidates ?? [],
            Fingerprint: "test"));

    private sealed class ShellOpenFake(bool result) : ISafeShellOpenService
    {
        public List<string?> Paths { get; } = [];

        public bool TryOpen(string? path, out string? error)
        {
            Paths.Add(path);
            error = result ? null : "Start blockiert";
            return result;
        }
    }

    private sealed class ExplorerRevealFake(Func<string?, bool> result) : IExplorerRevealService
    {
        public List<string?> Paths { get; } = [];

        public bool TryReveal(string? targetPath, out string? error)
        {
            Paths.Add(targetPath);
            var success = result(targetPath);
            error = success ? null : "Ziel fehlt";
            return success;
        }
    }

    private sealed class DialogFake : IDialogService
    {
        public string? OpenFile(string title, string filter, string? initialDirectory = null) => null;
        public string? SaveFile(string title, string filter, string? defaultExt = null, string? defaultFileName = null) => null;
        public string[] OpenFiles(string title, string filter) => Array.Empty<string>();
        public string? SelectFolder(string title, string? initialPath = null) => null;
        public void Info(string message, string title = "Hinweis") { }
        public void Warn(string message, string title = "Warnung") { }
        public void Error(string message, string title = "Fehler") { }
        public bool Confirm(string message, string title = "Bestaetigung") => false;
        public bool ConfirmWarn(string message, string title = "Bestaetigung", bool defaultNo = true) => false;
        public DialogConfirm ConfirmCancel(string message, string title = "Bestaetigung") => DialogConfirm.Cancel;
    }
}
