using AuswertungPro.Next.Domain.Models;
using AuswertungPro.Next.UI;
using AuswertungPro.Next.UI.DataPage;

namespace AuswertungPro.Next.UI.Tests;

public sealed class DataPageOriginalPdfControllerTests
{
    [Fact]
    public void Open_ignoriert_null_record()
    {
        var dialogs = new CapturingDialogService();
        var opened = new List<string?>();
        var controller = CreateController(
            dialogs,
            ensureProtocolPath: _ => throw new InvalidOperationException("protocol path should not be requested"),
            tryOpen: path =>
            {
                opened.Add(path);
                return (true, null);
            });

        controller.Open(null);

        Assert.Empty(opened);
        Assert.Null(dialogs.LastInfo);
        Assert.Null(dialogs.LastWarn);
    }

    [Fact]
    public void Open_bevorzugt_haltungsspezifischen_protokollpfad()
    {
        var dialogs = new CapturingDialogService();
        var record = Record("12/34");
        var opened = new List<string?>();
        var controller = CreateController(
            dialogs,
            ensureProtocolPath: r =>
            {
                Assert.Same(record, r);
                return "C:\\verteilt\\12-34.pdf";
            },
            resolveOriginalPdfPaths: (_, _) => throw new InvalidOperationException("fallback should not run"),
            tryOpen: path =>
            {
                opened.Add(path);
                return (true, null);
            });

        controller.Open(record);

        Assert.Equal(new[] { "C:\\verteilt\\12-34.pdf" }, opened);
        Assert.Null(dialogs.LastInfo);
        Assert.Null(dialogs.LastWarn);
    }

    [Fact]
    public void Open_nutzt_original_pdf_fallback_wenn_verteiltes_pdf_fehlt()
    {
        var dialogs = new CapturingDialogService();
        var record = Record("12/34");
        var opened = new List<string?>();
        var controller = CreateController(
            dialogs,
            projectFolder: "C:\\projekt",
            ensureProtocolPath: _ => null,
            resolveOriginalPdfPaths: (r, folder) =>
            {
                Assert.Same(record, r);
                Assert.Equal("C:\\projekt", folder);
                return new List<string> { "C:\\original\\alle.pdf" };
            },
            tryOpen: path =>
            {
                opened.Add(path);
                return (true, null);
            });

        controller.Open(record);

        Assert.Equal(new[] { "C:\\original\\alle.pdf" }, opened);
    }

    [Fact]
    public void Open_meldet_fehlendes_pdf_mit_haltungsname()
    {
        var dialogs = new CapturingDialogService();
        var controller = CreateController(
            dialogs,
            ensureProtocolPath: _ => "",
            resolveOriginalPdfPaths: (_, _) => new List<string>(),
            tryOpen: _ => throw new InvalidOperationException("shell open should not run"));

        controller.Open(Record("12/34"));

        Assert.Equal(
            ("Kein PDF gefunden fuer Haltung '12/34'.\n\nPruefen Sie, ob das Protokoll-PDF in der Verteilung liegt.", "Haltungsprotokoll (PDF)"),
            dialogs.LastInfo);
    }

    [Fact]
    public void Open_meldet_shell_open_fehler()
    {
        var dialogs = new CapturingDialogService();
        var controller = CreateController(
            dialogs,
            ensureProtocolPath: _ => "C:\\verteilt\\12-34.pdf",
            tryOpen: _ => (false, "Datei nicht gefunden."));

        controller.Open(Record("12/34"));

        Assert.Equal(("PDF konnte nicht geoeffnet werden:\nDatei nicht gefunden.", "Fehler"), dialogs.LastWarn);
    }

    private static DataPageOriginalPdfController CreateController(
        CapturingDialogService dialogs,
        string projectFolder = "",
        Func<HaltungRecord, string?>? ensureProtocolPath = null,
        Func<HaltungRecord, string, List<string>>? resolveOriginalPdfPaths = null,
        Func<string?, (bool Success, string? Error)>? tryOpen = null)
        => new(
            dialogs,
            ensureProtocolPath ?? (_ => null),
            getProjectFolder: () => projectFolder,
            resolveOriginalPdfPaths ?? ((_, _) => new List<string>()),
            tryOpen ?? (_ => (true, null)));

    private static HaltungRecord Record(string holding)
    {
        var record = new HaltungRecord();
        record.SetFieldValue("Haltungsname", holding, FieldSource.Manual, userEdited: false);
        return record;
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
