using AuswertungPro.Next.Application.Reports;
using AuswertungPro.Next.Domain.Models;
using AuswertungPro.Next.Domain.Protocol;
using AuswertungPro.Next.UI;
using AuswertungPro.Next.UI.DataPage;

namespace AuswertungPro.Next.UI.Tests;

public sealed class DataPagePrintControllerTests
{
    [Fact]
    public void PrintAwuHaltungsprotokollPdf_zeigt_hinweis_wenn_keine_haltung_ausgewaehlt_ist()
    {
        var dialogs = new CapturingDialogService();
        var controller = CreateController(dialogs);

        controller.PrintAwuHaltungsprotokollPdf(
            new Project(),
            record: null,
            ensureProtocolDocument: _ => throw new InvalidOperationException("document should not be requested"));

        Assert.Equal(("Bitte zuerst eine Haltung auswaehlen.", "Haltungsprotokoll AWU"), dialogs.LastInfo);
        Assert.Empty(dialogs.SaveFileCalls);
    }

    [Fact]
    public void PrintAwuHaltungsprotokollPdf_cancel_speichern_ohne_pdf_erzeugung()
    {
        var dialogs = new CapturingDialogService { SaveFileResult = "" };
        var controller = CreateController(
            dialogs,
            buildAwuPdf: (_, _, _, _, _) => throw new InvalidOperationException("pdf should not be built"));

        controller.PrintAwuHaltungsprotokollPdf(
            new Project(),
            Record("12/34"),
            ensureProtocolDocument: _ => throw new InvalidOperationException("document should not be requested"));

        Assert.Single(dialogs.SaveFileCalls);
        Assert.Null(dialogs.LastInfo);
        Assert.Null(dialogs.LastError);
    }

    [Fact]
    public void PrintAwuHaltungsprotokollPdf_erzeugt_pdf_und_meldet_erfolg()
    {
        var record = Record("12/34");
        var project = new Project { Name = "P" };
        var doc = new ProtocolDocument { HaltungId = "12/34" };
        var dialogs = new CapturingDialogService { SaveFileResult = "C:\\out\\awu.pdf" };
        var written = new List<(string Path, byte[] Bytes)>();
        var buildCalls = new List<(Project Project, HaltungRecord Record, ProtocolDocument Doc, string Root, HaltungsprotokollPdfOptions Options)>();

        var controller = CreateController(
            dialogs,
            projectFolder: "C:\\projekt",
            baseDirectory: "C:\\app",
            fileExists: path => path == "C:\\app\\Assets\\Brand\\abwasser-uri-logo.png",
            writeAllBytes: (path, bytes) => written.Add((path, bytes)),
            now: () => new DateTime(2026, 1, 2),
            buildAwuPdf: (p, r, d, root, options) =>
            {
                buildCalls.Add((p, r, d, root, options));
                return new byte[] { 1, 2, 3 };
            });

        controller.PrintAwuHaltungsprotokollPdf(
            project,
            record,
            ensureProtocolDocument: r =>
            {
                Assert.Same(record, r);
                return doc;
            });

        var saveCall = Assert.Single(dialogs.SaveFileCalls);
        Assert.Equal("Haltungsprotokoll AWU als PDF speichern", saveCall.Title);
        Assert.Equal("PDF (*.pdf)|*.pdf", saveCall.Filter);
        Assert.Equal("pdf", saveCall.DefaultExt);
        Assert.Equal("Haltungsprotokoll_AWU_12_34_20260102.pdf", saveCall.DefaultFileName);

        var build = Assert.Single(buildCalls);
        Assert.Same(project, build.Project);
        Assert.Same(record, build.Record);
        Assert.Same(doc, build.Doc);
        Assert.Equal("C:\\projekt", build.Root);
        Assert.Equal("C:\\app\\Assets\\Brand\\abwasser-uri-logo.png", build.Options.LogoPathAbs);

        var output = Assert.Single(written);
        Assert.Equal("C:\\out\\awu.pdf", output.Path);
        Assert.Equal(new byte[] { 1, 2, 3 }, output.Bytes);
        Assert.Equal(("AWU-Haltungsprotokoll wurde erstellt:\nC:\\out\\awu.pdf", "Haltungsprotokoll AWU"), dialogs.LastInfo);
        Assert.Null(dialogs.LastError);
    }

    [Fact]
    public void PrintAwuHaltungsprotokollPdf_meldet_fehler_ohne_exception()
    {
        var dialogs = new CapturingDialogService { SaveFileResult = "C:\\out\\awu.pdf" };
        var controller = CreateController(
            dialogs,
            buildAwuPdf: (_, _, _, _, _) => throw new InvalidOperationException("kaputt"));

        controller.PrintAwuHaltungsprotokollPdf(
            new Project(),
            Record("12/34"),
            ensureProtocolDocument: _ => new ProtocolDocument());

        Assert.Equal(("AWU-Haltungsprotokoll konnte nicht erstellt werden:\nkaputt", "Haltungsprotokoll AWU"), dialogs.LastError);
    }

    [Fact]
    public async Task PrintHydraulikPdfAsync_zeigt_hinweis_wenn_keine_haltung_ausgewaehlt_ist()
    {
        var dialogs = new CapturingDialogService();
        var controller = CreateController(
            dialogs,
            buildHydraulikCalculation: _ => throw new InvalidOperationException("calculation should not run"));

        await controller.PrintHydraulikPdfAsync(record: null);

        Assert.Equal(("Bitte zuerst eine Haltung auswaehlen.", "Hydraulik PDF"), dialogs.LastInfo);
        Assert.Empty(dialogs.SaveFileCalls);
    }

    [Fact]
    public async Task PrintHydraulikPdfAsync_warnt_wenn_berechnung_fehlt()
    {
        var dialogs = new CapturingDialogService();
        var controller = CreateController(
            dialogs,
            buildHydraulikCalculation: _ => null,
            selectHydraulikPrintOptions: () => throw new InvalidOperationException("options should not be requested"));

        await controller.PrintHydraulikPdfAsync(Record("12/34"));

        Assert.Equal(("Hydraulik-Berechnung konnte nicht durchgefuehrt werden.\nBitte DN und Gefaelle pruefen.", "Hydraulik PDF"), dialogs.LastWarn);
        Assert.Empty(dialogs.SaveFileCalls);
    }

    [Fact]
    public async Task PrintHydraulikPdfAsync_bricht_ab_wenn_optionen_abgebrochen_werden()
    {
        var dialogs = new CapturingDialogService();
        var controller = CreateController(
            dialogs,
            buildHydraulikCalculation: _ => HydraulikCalc(),
            selectHydraulikPrintOptions: () => null,
            buildHydraulikPdfAsync: (_, _, _) => throw new InvalidOperationException("pdf should not be built"));

        await controller.PrintHydraulikPdfAsync(Record("12/34"));

        Assert.Empty(dialogs.SaveFileCalls);
        Assert.Null(dialogs.LastInfo);
        Assert.Null(dialogs.LastError);
    }

    [Fact]
    public async Task PrintHydraulikPdfAsync_cancel_speichern_ohne_pdf_erzeugung()
    {
        var dialogs = new CapturingDialogService { SaveFileResult = "" };
        var controller = CreateController(
            dialogs,
            buildHydraulikCalculation: _ => HydraulikCalc(),
            selectHydraulikPrintOptions: () => new HydraulikPrintOptions(),
            buildHydraulikPdfAsync: (_, _, _) => throw new InvalidOperationException("pdf should not be built"));

        await controller.PrintHydraulikPdfAsync(Record("12/34"));

        Assert.Single(dialogs.SaveFileCalls);
        Assert.Null(dialogs.LastInfo);
        Assert.Null(dialogs.LastError);
    }

    [Fact]
    public async Task PrintHydraulikPdfAsync_erzeugt_pdf_und_meldet_erfolg()
    {
        var record = Record("12/34");
        var calc = HydraulikCalc();
        var dialogs = new CapturingDialogService { SaveFileResult = "C:\\out\\hydraulik.pdf" };
        var written = new List<(string Path, byte[] Bytes)>();
        var buildCalls = new List<(HaltungRecord Record, HydraulikCalcResult Calc, HydraulikPrintOptions Options)>();

        var controller = CreateController(
            dialogs,
            baseDirectory: "C:\\app",
            fileExists: path => path == "C:\\app\\Assets\\Brand\\abwasser-uri-logo.png",
            writeAllBytesAsync: (path, bytes) =>
            {
                written.Add((path, bytes));
                return Task.CompletedTask;
            },
            now: () => new DateTime(2026, 1, 2),
            buildHydraulikCalculation: r =>
            {
                Assert.Same(record, r);
                return calc;
            },
            selectHydraulikPrintOptions: () => new HydraulikPrintOptions
            {
                IncludeAblagerung = false,
                FooterLine = "Footer"
            },
            buildHydraulikPdfAsync: (r, c, options) =>
            {
                buildCalls.Add((r, c, options));
                return Task.FromResult(new byte[] { 4, 5, 6 });
            });

        await controller.PrintHydraulikPdfAsync(record);

        var saveCall = Assert.Single(dialogs.SaveFileCalls);
        Assert.Equal("Hydraulik-Bericht als PDF speichern", saveCall.Title);
        Assert.Equal("PDF (*.pdf)|*.pdf", saveCall.Filter);
        Assert.Equal("pdf", saveCall.DefaultExt);
        Assert.Equal("Hydraulik_12_34_20260102.pdf", saveCall.DefaultFileName);

        var build = Assert.Single(buildCalls);
        Assert.Same(record, build.Record);
        Assert.Same(calc, build.Calc);
        Assert.False(build.Options.IncludeAblagerung);
        Assert.Equal("Footer", build.Options.FooterLine);
        Assert.Equal("C:\\app\\Assets\\Brand\\abwasser-uri-logo.png", build.Options.LogoPathAbs);

        var output = Assert.Single(written);
        Assert.Equal("C:\\out\\hydraulik.pdf", output.Path);
        Assert.Equal(new byte[] { 4, 5, 6 }, output.Bytes);
        Assert.Equal(("PDF wurde erstellt:\nC:\\out\\hydraulik.pdf", "Hydraulik PDF"), dialogs.LastInfo);
        Assert.Null(dialogs.LastError);
    }

    [Fact]
    public async Task PrintHydraulikPdfAsync_meldet_fehler_ohne_exception()
    {
        var dialogs = new CapturingDialogService { SaveFileResult = "C:\\out\\hydraulik.pdf" };
        var controller = CreateController(
            dialogs,
            buildHydraulikCalculation: _ => HydraulikCalc(),
            selectHydraulikPrintOptions: () => new HydraulikPrintOptions(),
            buildHydraulikPdfAsync: (_, _, _) => throw new InvalidOperationException("kaputt"));

        await controller.PrintHydraulikPdfAsync(Record("12/34"));

        Assert.Equal(("PDF konnte nicht erstellt werden:\nkaputt", "Hydraulik PDF"), dialogs.LastError);
    }

    private static DataPagePrintController CreateController(
        CapturingDialogService dialogs,
        string projectFolder = "",
        string baseDirectory = "C:\\app",
        Func<string, bool>? fileExists = null,
        Action<string, byte[]>? writeAllBytes = null,
        Func<string, byte[], Task>? writeAllBytesAsync = null,
        Func<DateTime>? now = null,
        Func<Project, HaltungRecord, ProtocolDocument, string, HaltungsprotokollPdfOptions, byte[]>? buildAwuPdf = null,
        Func<HaltungRecord, HydraulikCalcResult?>? buildHydraulikCalculation = null,
        Func<HydraulikPrintOptions?>? selectHydraulikPrintOptions = null,
        Func<HaltungRecord, HydraulikCalcResult, HydraulikPrintOptions, Task<byte[]>>? buildHydraulikPdfAsync = null)
        => new(
            dialogs,
            getProjectFolder: () => projectFolder,
            buildAwuPdf: buildAwuPdf ?? ((_, _, _, _, _) => Array.Empty<byte>()),
            baseDirectory,
            fileExists: fileExists ?? (_ => false),
            writeAllBytes: writeAllBytes ?? ((_, _) => { }),
            writeAllBytesAsync: writeAllBytesAsync ?? ((_, _) => Task.CompletedTask),
            now: now ?? (() => new DateTime(2026, 1, 2)),
            buildHydraulikCalculation: buildHydraulikCalculation,
            selectHydraulikPrintOptions: selectHydraulikPrintOptions,
            buildHydraulikPdfAsync: buildHydraulikPdfAsync);

    private static HaltungRecord Record(string holding)
    {
        var record = new HaltungRecord();
        record.SetFieldValue("Haltungsname", holding, FieldSource.Manual, userEdited: false);
        return record;
    }

    private static HydraulikCalcResult HydraulikCalc()
        => new()
        {
            DN_mm = 300,
            Wasserstand_mm = 150,
            Gefaelle_Promille = 10,
            Kb = 0.0015,
            Temperatur_C = 10,
            Material = "Beton"
        };

    private sealed class CapturingDialogService : IDialogService
    {
        public string? SaveFileResult { get; set; } = "C:\\out\\awu.pdf";
        public List<(string Title, string Filter, string? DefaultExt, string? DefaultFileName)> SaveFileCalls { get; } = new();
        public (string Message, string Title)? LastInfo { get; private set; }
        public (string Message, string Title)? LastWarn { get; private set; }
        public (string Message, string Title)? LastError { get; private set; }

        public string? OpenFile(string title, string filter, string? initialDirectory = null)
            => throw new NotSupportedException();

        public string? SaveFile(string title, string filter, string? defaultExt = null, string? defaultFileName = null)
        {
            SaveFileCalls.Add((title, filter, defaultExt, defaultFileName));
            return SaveFileResult;
        }

        public string[] OpenFiles(string title, string filter)
            => throw new NotSupportedException();

        public string? SelectFolder(string title, string? initialPath = null)
            => throw new NotSupportedException();

        public void Info(string message, string title = "Hinweis")
            => LastInfo = (message, title);

        public void Warn(string message, string title = "Warnung")
            => LastWarn = (message, title);

        public void Error(string message, string title = "Fehler")
            => LastError = (message, title);

        public bool Confirm(string message, string title = "Bestaetigung")
            => throw new NotSupportedException();

        public bool ConfirmWarn(string message, string title = "Bestaetigung", bool defaultNo = true)
            => throw new NotSupportedException();

        public DialogConfirm ConfirmCancel(string message, string title = "Bestaetigung")
            => throw new NotSupportedException();
    }
}
