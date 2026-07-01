using AuswertungPro.Next.Domain.Models;
using AuswertungPro.Next.Infrastructure.Media;
using AuswertungPro.Next.UI;
using AuswertungPro.Next.UI.DataPage;

namespace AuswertungPro.Next.UI.Tests;

public sealed class DataPageVideoRelinkControllerTests
{
    [Fact]
    public void Relink_ignoriert_null_record()
    {
        var dialogs = new CapturingDialogService();
        var persisted = new List<string>();
        var saved = new List<(HaltungRecord Record, string Path, bool UserEdited)>();
        var controller = CreateController(
            dialogs,
            persistSelectedFolder: persisted.Add,
            saveVideoLink: (record, path, userEdited) => saved.Add((record, path, userEdited)));

        controller.Relink(null);

        Assert.Null(dialogs.LastOpenFile);
        Assert.Empty(persisted);
        Assert.Empty(saved);
    }

    [Fact]
    public void Relink_nutzt_quellordner_vor_legacy_und_projektordner()
    {
        var dialogs = new CapturingDialogService { OpenFileResult = null };
        var controller = CreateController(
            dialogs,
            lastVideoSourceFolder: "C:\\Quelle",
            lastVideoFolder: "C:\\Legacy",
            lastProjectPath: "C:\\Projekt\\projekt.json");

        controller.Relink(new HaltungRecord());

        Assert.Equal(("Video auswaehlen", MediaFileTypes.VideoDialogFilter, "C:\\Quelle"), dialogs.LastOpenFile);
    }

    [Fact]
    public void Relink_nutzt_projektordner_wenn_keine_video_ordner_gespeichert_sind()
    {
        var dialogs = new CapturingDialogService { OpenFileResult = null };
        var controller = CreateController(
            dialogs,
            lastVideoSourceFolder: "",
            lastVideoFolder: "",
            lastProjectPath: "C:\\Projekt\\projekt.json");

        controller.Relink(new HaltungRecord());

        Assert.Equal(("Video auswaehlen", MediaFileTypes.VideoDialogFilter, "C:\\Projekt"), dialogs.LastOpenFile);
    }

    [Fact]
    public void Relink_bricht_bei_leerer_auswahl_ohne_persist_und_save_ab()
    {
        var dialogs = new CapturingDialogService { OpenFileResult = "  " };
        var persisted = new List<string>();
        var saved = new List<(HaltungRecord Record, string Path, bool UserEdited)>();
        var controller = CreateController(
            dialogs,
            persistSelectedFolder: persisted.Add,
            saveVideoLink: (record, path, userEdited) => saved.Add((record, path, userEdited)));

        controller.Relink(new HaltungRecord());

        Assert.Empty(persisted);
        Assert.Empty(saved);
    }

    [Fact]
    public void Relink_persistiert_auswahlordner_und_speichert_video_link_als_user_edit()
    {
        var record = new HaltungRecord();
        var dialogs = new CapturingDialogService { OpenFileResult = "C:\\Videos\\haltung.mp4" };
        var persisted = new List<string>();
        var saved = new List<(HaltungRecord Record, string Path, bool UserEdited)>();
        var controller = CreateController(
            dialogs,
            persistSelectedFolder: persisted.Add,
            saveVideoLink: (r, path, userEdited) => saved.Add((r, path, userEdited)));

        controller.Relink(record);

        Assert.Equal(new[] { "C:\\Videos" }, persisted);
        var savedItem = Assert.Single(saved);
        Assert.Same(record, savedItem.Record);
        Assert.Equal("C:\\Videos\\haltung.mp4", savedItem.Path);
        Assert.True(savedItem.UserEdited);
    }

    private static DataPageVideoRelinkController CreateController(
        CapturingDialogService dialogs,
        string? lastVideoSourceFolder = null,
        string? lastVideoFolder = null,
        string? lastProjectPath = null,
        Action<string>? persistSelectedFolder = null,
        Action<HaltungRecord, string, bool>? saveVideoLink = null)
        => new(
            dialogs,
            getLastVideoSourceFolder: () => lastVideoSourceFolder,
            getLastVideoFolder: () => lastVideoFolder,
            getLastProjectPath: () => lastProjectPath,
            persistSelectedFolder ?? (_ => { }),
            saveVideoLink ?? ((_, _, _) => { }));

    private sealed class CapturingDialogService : IDialogService
    {
        public (string Title, string Filter, string? InitialDirectory)? LastOpenFile { get; private set; }
        public string? OpenFileResult { get; set; }

        public string? OpenFile(string title, string filter, string? initialDirectory = null)
        {
            LastOpenFile = (title, filter, initialDirectory);
            return OpenFileResult;
        }

        public string? SaveFile(string title, string filter, string? defaultExt = null, string? defaultFileName = null)
            => throw new NotSupportedException();

        public string[] OpenFiles(string title, string filter)
            => throw new NotSupportedException();

        public string? SelectFolder(string title, string? initialPath = null)
            => throw new NotSupportedException();

        public void Info(string message, string title = "Hinweis")
            => throw new NotSupportedException();

        public void Warn(string message, string title = "Warnung")
            => throw new NotSupportedException();

        public void Error(string message, string title = "Fehler")
            => throw new NotSupportedException();

        public bool Confirm(string message, string title = "Bestaetigung")
            => throw new NotSupportedException();

        public bool ConfirmWarn(string message, string title = "Bestaetigung", bool defaultNo = true)
            => throw new NotSupportedException();

        public DialogConfirm ConfirmCancel(string message, string title = "Bestaetigung")
            => throw new NotSupportedException();
    }
}
