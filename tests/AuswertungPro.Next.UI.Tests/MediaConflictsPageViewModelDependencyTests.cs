using System.IO;
using System.Reflection;
using AuswertungPro.Next.Domain.Models;
using AuswertungPro.Next.Infrastructure.Media;
using AuswertungPro.Next.UI;
using AuswertungPro.Next.UI.ViewModels;
using AuswertungPro.Next.UI.ViewModels.Pages;

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
        Assert.Equal(
            typeof(MediaConflictCenterService),
            typeof(ServiceProvider).GetProperty(nameof(ServiceProvider.MediaConflictCenter))?.PropertyType);
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

    private static MediaConflictsPageViewModel CreateViewModel(
        Project project,
        Func<string?> getProjectFolder,
        Action<string> playVideo)
        => new(
            getProject: () => project,
            getProjectFolder: getProjectFolder,
            getLastVideoSourceFolder: () => null,
            saveVideoSourceFolder: _ => { },
            dialogs: new DialogFake(),
            service: new MediaConflictCenterService(),
            setStatus: _ => { },
            playVideo: playVideo);

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
