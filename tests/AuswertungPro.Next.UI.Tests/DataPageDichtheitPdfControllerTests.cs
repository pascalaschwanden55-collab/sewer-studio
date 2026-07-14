using AuswertungPro.Next.Application.DataPage;
using AuswertungPro.Next.Domain.Models;
using AuswertungPro.Next.UI.DataPage;

namespace AuswertungPro.Next.UI.Tests;

public sealed class DataPageDichtheitPdfControllerTests
{
    [Fact]
    public void Open_oeffnet_den_ersten_gefundenen_DP_Pfad()
    {
        var record = Haltung("12-34");
        var locator = new RecordingLocator(["C:\\DP\\neu.pdf", "C:\\DP\\alt.pdf"]);
        var dialogs = new CapturingDialogService();
        string? opened = null;
        var controller = CreateController(
            dialogs,
            locator,
            tryOpen: path =>
            {
                opened = path;
                return (true, null);
            });

        controller.Open(record);

        Assert.Equal("C:\\DP\\neu.pdf", opened);
        Assert.Same(record, locator.Record);
        Assert.Equal("C:\\Projekt", locator.ProjectFolder);
        Assert.Equal("D:\\Extern", locator.ConfiguredRoot);
        Assert.Null(dialogs.LastInfo);
    }

    [Fact]
    public void Open_meldet_wenn_keine_DP_Datei_gefunden_wurde()
    {
        var dialogs = new CapturingDialogService();
        var controller = CreateController(dialogs, new RecordingLocator([]));

        controller.Open(Haltung("12-34"));

        Assert.Equal(
            ("Kein Dichtheitspruefungsprotokoll fuer Haltung '12-34' gefunden.\n" +
             "Dichtheitsprotokolle werden beim Kanalfernseh-Import automatisch verteilt (…_DP.pdf).",
             "Dichtheitspruefung"),
            dialogs.LastInfo);
    }

    [Fact]
    public void Open_meldet_fehler_des_Dateioeffnens()
    {
        var dialogs = new CapturingDialogService();
        var controller = CreateController(
            dialogs,
            new RecordingLocator(["C:\\DP\\neu.pdf"]),
            tryOpen: _ => (false, "Datei gesperrt."));

        controller.Open(Haltung("12-34"));

        Assert.Equal(
            ("Dichtheitspruefung konnte nicht geoeffnet werden:\nDatei gesperrt.", "Dichtheitspruefung"),
            dialogs.LastWarn);
    }

    private static DataPageDichtheitPdfController CreateController(
        CapturingDialogService dialogs,
        IDichtheitProtocolFileLocator locator,
        Func<string?, (bool Success, string? Error)>? tryOpen = null)
        => new(
            dialogs,
            locator,
            getProjectFolder: () => "C:\\Projekt",
            getConfiguredRoot: () => "D:\\Extern",
            tryOpen ?? (_ => (true, null)));

    private static HaltungRecord Haltung(string name)
    {
        var record = new HaltungRecord();
        record.SetFieldValue("Haltungsname", name, FieldSource.Xtf, userEdited: false);
        return record;
    }

    private sealed class RecordingLocator(IReadOnlyList<string> result) : IDichtheitProtocolFileLocator
    {
        public HaltungRecord? Record { get; private set; }
        public string? ProjectFolder { get; private set; }
        public string? ConfiguredRoot { get; private set; }

        public IReadOnlyList<string> FindPdfPaths(
            HaltungRecord? record,
            string? projectFolder,
            string? configuredRoot)
        {
            Record = record;
            ProjectFolder = projectFolder;
            ConfiguredRoot = configuredRoot;
            return result;
        }
    }

    private sealed class CapturingDialogService : IDialogService
    {
        public (string Message, string Title)? LastInfo { get; private set; }
        public (string Message, string Title)? LastWarn { get; private set; }

        public string? OpenFile(string title, string filter, string? initialDirectory = null)
            => throw new NotSupportedException();

        public string? SaveFile(string title, string filter, string? defaultExt = null, string? defaultFileName = null)
            => throw new NotSupportedException();

        public string[] OpenFiles(string title, string filter)
            => throw new NotSupportedException();

        public string? SelectFolder(string title, string? initialPath = null)
            => throw new NotSupportedException();

        public void Info(string message, string title = "Hinweis")
            => LastInfo = (message, title);

        public void Warn(string message, string title = "Warnung")
            => LastWarn = (message, title);

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
