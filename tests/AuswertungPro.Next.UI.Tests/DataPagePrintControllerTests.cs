using System.IO;
using AuswertungPro.Next.Application.Costs;
using AuswertungPro.Next.Application.DataPage;
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
    public void PrintAwuHaltungsprotokollPdf_hinweis_wenn_projekt_nicht_gespeichert()
    {
        var dialogs = new CapturingDialogService();
        var regenCalled = false;
        var controller = CreateController(
            dialogs,
            projectFolder: "",
            regenerateOne: (_, _, _, _) => { regenCalled = true; return "x"; });

        controller.PrintAwuHaltungsprotokollPdf(
            new Project(),
            Record("12/34"),
            ensureProtocolDocument: _ => new ProtocolDocument());

        Assert.False(regenCalled);
        Assert.Equal(
            ("Projekt bitte zuerst speichern — dann wird das Protokoll direkt in den Haltungsordner erzeugt.", "Haltungsprotokoll AWU"),
            dialogs.LastInfo);
        Assert.Empty(dialogs.SaveFileCalls);
    }

    [Fact]
    public void PrintAwuHaltungsprotokollPdf_erzeugt_direkt_in_ordner_und_oeffnet()
    {
        var record = Record("12/34");
        var project = new Project { Name = "P" };
        var doc = new ProtocolDocument { HaltungId = "12/34" };
        var dialogs = new CapturingDialogService();
        var regenCalls = new List<(Project P, string Folder, HaltungRecord R, ProtocolDocument D)>();
        string? opened = null;

        var controller = CreateController(
            dialogs,
            projectFolder: "C:\\projekt",
            regenerateOne: (p, folder, r, d) =>
            {
                regenCalls.Add((p, folder, r, d));
                return "C:\\projekt\\Haltungen_Verteilt\\12_34\\20260102_12_34_E.pdf";
            },
            openPdf: path => { opened = path; return true; });

        controller.PrintAwuHaltungsprotokollPdf(
            project,
            record,
            ensureProtocolDocument: r =>
            {
                Assert.Same(record, r);
                return doc;
            });

        var call = Assert.Single(regenCalls);
        Assert.Same(project, call.P);
        Assert.Equal("C:\\projekt", call.Folder);
        Assert.Same(record, call.R);
        Assert.Same(doc, call.D);
        Assert.Equal("C:\\projekt\\Haltungen_Verteilt\\12_34\\20260102_12_34_E.pdf", opened);
        Assert.True(project.Dirty);
        Assert.Empty(dialogs.SaveFileCalls); // kein Speichern-Dialog mehr
        Assert.Null(dialogs.LastInfo);        // bei erfolgreichem Oeffnen keine zusaetzliche Meldung
        Assert.Null(dialogs.LastError);
    }

    [Fact]
    public void PrintAwuHaltungsprotokollPdf_meldet_pfad_wenn_oeffnen_fehlschlaegt()
    {
        var dialogs = new CapturingDialogService();
        var controller = CreateController(
            dialogs,
            projectFolder: "C:\\projekt",
            regenerateOne: (_, _, _, _) => "C:\\projekt\\Haltungen_Verteilt\\12_34\\x_E.pdf",
            openPdf: _ => false);

        controller.PrintAwuHaltungsprotokollPdf(
            new Project(),
            Record("12/34"),
            ensureProtocolDocument: _ => new ProtocolDocument());

        Assert.Equal(
            ("AWU-Haltungsprotokoll wurde erstellt:\nC:\\projekt\\Haltungen_Verteilt\\12_34\\x_E.pdf", "Haltungsprotokoll AWU"),
            dialogs.LastInfo);
    }

    [Fact]
    public void PrintAwuHaltungsprotokollPdf_info_wenn_kein_zielpfad()
    {
        var dialogs = new CapturingDialogService();
        var controller = CreateController(
            dialogs,
            projectFolder: "C:\\projekt",
            regenerateOne: (_, _, _, _) => null);

        controller.PrintAwuHaltungsprotokollPdf(
            new Project(),
            Record("12/34"),
            ensureProtocolDocument: _ => new ProtocolDocument());

        Assert.Equal(
            ("Fuer diese Haltung liegt kein Haltungsname vor — der Zielordner kann nicht bestimmt werden.", "Haltungsprotokoll AWU"),
            dialogs.LastInfo);
    }

    [Fact]
    public void PrintAwuHaltungsprotokollPdf_meldet_fehler_ohne_exception()
    {
        var dialogs = new CapturingDialogService();
        var controller = CreateController(
            dialogs,
            projectFolder: "C:\\projekt",
            regenerateOne: (_, _, _, _) => throw new InvalidOperationException("kaputt"));

        controller.PrintAwuHaltungsprotokollPdf(
            new Project(),
            Record("12/34"),
            ensureProtocolDocument: _ => new ProtocolDocument());

        Assert.NotNull(dialogs.LastError);
        Assert.Equal("Haltungsprotokoll AWU", dialogs.LastError.Value.Title);
        Assert.Contains("Programmlog", dialogs.LastError.Value.Message, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("kaputt", dialogs.LastError.Value.Message, StringComparison.Ordinal);
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

        Assert.NotNull(dialogs.LastError);
        Assert.Equal("Hydraulik PDF", dialogs.LastError.Value.Title);
        Assert.Contains("Programmlog", dialogs.LastError.Value.Message, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("kaputt", dialogs.LastError.Value.Message, StringComparison.Ordinal);
    }

    [Fact]
    public async Task PrintDossierPdfAsync_zeigt_hinweis_wenn_keine_haltung_ausgewaehlt_ist()
    {
        var dialogs = new CapturingDialogService();
        var controller = CreateController(
            dialogs,
            selectDossierPrintOptions: _ => throw new InvalidOperationException("options should not be requested"));

        await controller.PrintDossierPdfAsync(new Project(), record: null);

        Assert.Equal(("Bitte zuerst eine Haltung auswaehlen.", "Dossier"), dialogs.LastInfo);
        Assert.Empty(dialogs.SaveFileCalls);
    }

    [Fact]
    public async Task PrintDossierPdfAsync_gibt_verfuegbarkeit_an_dialog_und_bricht_bei_cancel_ab()
    {
        var dialogs = new CapturingDialogService();
        var schachtVon = Schacht("A");
        var schachtBis = Schacht("B");
        DataPageDossierPrintAvailability? captured = null;
        var controller = CreateController(
            dialogs,
            splitHoldingNodes: _ => ("A", "B"),
            findSchachtByNummer: nr => nr == "A" ? schachtVon : nr == "B" ? schachtBis : null,
            readDossierHydraulikAvailability: _ => new DataPageHydraulikAvailability(300, 10),
            findHoldingCost: _ => HoldingCostWithMeasure("12/34"),
            resolveDossierOriginalPdfPaths: (_, _, _, _) => new List<string> { "C:\\orig1.pdf", "C:\\orig2.pdf" },
            selectDossierPrintOptions: availability =>
            {
                captured = availability;
                return null;
            },
            buildDossierPdfAsync: (_, _, _, _, _, _, _) => throw new InvalidOperationException("pdf should not be built"));

        await controller.PrintDossierPdfAsync(new Project(), Record("12/34"));

        Assert.NotNull(captured);
        Assert.True(captured!.HasSchachtVon);
        Assert.Equal("A", captured.SchachtVonNr);
        Assert.True(captured.HasSchachtBis);
        Assert.Equal("B", captured.SchachtBisNr);
        Assert.True(captured.HydraulikAvailable);
        Assert.True(captured.KostenAvailable);
        Assert.Equal(2, captured.OriginalPdfCount);
        Assert.Empty(dialogs.SaveFileCalls);
    }

    [Fact]
    public async Task PrintDossierPdfAsync_laesst_Projektkosten_ueber_den_injizierten_Vertrag_laden()
    {
        var dialogs = new CapturingDialogService();
        var costs = new RecordingProjectCostStoreRepository();
        costs.Store.ByHolding["12/34"] = HoldingCostWithMeasure("12/34");
        DataPageDossierPrintAvailability? captured = null;
        var controller = CreateController(
            dialogs,
            getLastProjectPath: () => "C:\\projekt\\Projektdateien\\projekt.json",
            projectCosts: costs,
            selectDossierPrintOptions: availability =>
            {
                captured = availability;
                return null;
            });

        await controller.PrintDossierPdfAsync(new Project(), Record("12/34"));

        Assert.Equal("C:\\projekt\\Projektdateien\\projekt.json", costs.LoadedProjectPath);
        Assert.NotNull(captured);
        Assert.True(captured!.KostenAvailable);
    }

    [Fact]
    public async Task PrintDossierPdfAsync_warnt_bei_dirty_project_und_bricht_bei_nein_ab()
    {
        var dialogs = new CapturingDialogService { ConfirmWarnResult = false };
        var buildCalled = false;
        var controller = CreateController(
            dialogs,
            selectDossierPrintOptions: _ => EmptyDossierOptions() with { IncludeDeckblatt = true },
            buildDossierPdfAsync: (_, _, _, _, _, _, _) =>
            {
                buildCalled = true;
                return Task.FromResult(Array.Empty<byte>());
            });

        await controller.PrintDossierPdfAsync(new Project { Dirty = true }, Record("12/34"));

        Assert.False(buildCalled);
        Assert.Empty(dialogs.SaveFileCalls);
        var call = Assert.Single(dialogs.ConfirmWarnCalls);
        Assert.Equal("Dossier", call.Title);
        Assert.Contains("ungespeicherte Aenderungen", call.Message);
        Assert.True(call.DefaultNo);
    }

    [Fact]
    public async Task PrintDossierPdfAsync_meldet_nicht_druckbare_auswahl_ohne_pdf_build()
    {
        var dialogs = new CapturingDialogService { SaveFileResult = "C:\\out\\dossier.pdf" };
        var controller = CreateController(
            dialogs,
            selectDossierPrintOptions: _ => EmptyDossierOptions(),
            buildDossierPdfAsync: (_, _, _, _, _, _, _) => throw new InvalidOperationException("pdf should not be built"));

        await controller.PrintDossierPdfAsync(new Project(), Record("12/34"));

        Assert.Single(dialogs.SaveFileCalls);
        Assert.Equal(("Die ausgewaehlte Kombination enthaelt keine druckbaren Inhalte.", "Dossier"), dialogs.LastInfo);
        Assert.Null(dialogs.LastError);
    }

    [Fact]
    public async Task PrintDossierPdfAsync_verwendet_injizierte_fotoverfuegbarkeit()
    {
        var dialogs = new CapturingDialogService { SaveFileResult = "C:\\out\\dossier.pdf" };
        var photoAvailability = new RecordingDossierPhotoAvailability(result: true);
        var buildCalled = false;
        var controller = CreateController(
            dialogs,
            dossierPhotoAvailability: photoAvailability,
            selectDossierPrintOptions: _ => EmptyDossierOptions() with { IncludeFotos = true },
            buildDossierPdfAsync: (_, _, _, _, _, _, _) =>
            {
                buildCalled = true;
                return Task.FromResult(new byte[] { 1 });
            });

        await controller.PrintDossierPdfAsync(new Project(), Record("12/34"));

        Assert.Equal(1, photoAvailability.Calls);
        Assert.True(buildCalled);
        Assert.Equal(("Dossier wurde erstellt:\nC:\\out\\dossier.pdf", "Dossier"), dialogs.LastInfo);
    }

    [Fact]
    public async Task PrintDossierPdfAsync_erzeugt_basis_dossier_und_haengt_originale_an()
    {
        var project = new Project { Name = "P" };
        var record = Record("12/34");
        var schachtVon = Schacht("12");
        var schachtBis = Schacht("34");
        var calc = HydraulikCalc();
        var cost = HoldingCostWithMeasure("12/34");
        var dialogs = new CapturingDialogService { SaveFileResult = "C:\\out\\dossier.pdf" };
        var written = new List<(string Path, byte[] Bytes)>();
        var buildCalls = new List<(Project Project, HaltungRecord Record, SchachtRecord? Von, SchachtRecord? Bis, HydraulikCalcResult? Calc, string Root, DossierPrintOptions Options)>();
        var merged = new List<(byte[] Generated, IReadOnlyList<string> Originals)>();

        var controller = CreateController(
            dialogs,
            projectFolder: "C:\\projekt",
            baseDirectory: "C:\\app",
            fileExists: path => path == "C:\\app\\Assets\\Brand\\abwasser-uri-logo.png",
            writeAllBytesAsync: (path, bytes) =>
            {
                written.Add((path, bytes));
                return Task.CompletedTask;
            },
            now: () => new DateTime(2026, 1, 2),
            splitHoldingNodes: _ => ("12", "34"),
            findSchachtByNummer: nr => nr == "12" ? schachtVon : nr == "34" ? schachtBis : null,
            readDossierHydraulikAvailability: _ => new DataPageHydraulikAvailability(300, 10),
            buildDossierHydraulikCalculation: (_, dn) =>
            {
                Assert.Equal(300, dn);
                return calc;
            },
            findHoldingCost: holding => holding == "12/34" ? cost : null,
            resolveDossierOriginalPdfPaths: (_, _, _, _) => new List<string> { "C:\\orig.pdf" },
            selectDossierPrintOptions: _ => EmptyDossierOptions() with
            {
                IncludeDeckblatt = true,
                IncludeHydraulik = true,
                IncludeKostenschaetzung = true,
                IncludeOriginalProtokolle = true,
                FooterLine = "Footer"
            },
            buildDossierPdfAsync: (p, r, von, bis, hydraulik, root, options) =>
            {
                buildCalls.Add((p, r, von, bis, hydraulik, root, options));
                return Task.FromResult(new byte[] { 1, 2, 3 });
            },
            mergeWithOriginals: (generated, originals) =>
            {
                merged.Add((generated, originals));
                return new byte[] { 9, 9 };
            });

        await controller.PrintDossierPdfAsync(project, record);

        var saveCall = Assert.Single(dialogs.SaveFileCalls);
        Assert.Equal("Haltungsdossier als PDF speichern", saveCall.Title);
        Assert.Equal("PDF (*.pdf)|*.pdf", saveCall.Filter);
        Assert.Equal("pdf", saveCall.DefaultExt);
        Assert.Equal("Dossier_12_34_20260102.pdf", saveCall.DefaultFileName);

        var build = Assert.Single(buildCalls);
        Assert.Same(project, build.Project);
        Assert.Same(record, build.Record);
        Assert.Same(schachtVon, build.Von);
        Assert.Same(schachtBis, build.Bis);
        Assert.Same(calc, build.Calc);
        Assert.Equal("C:\\projekt", build.Root);
        Assert.Equal("Footer", build.Options.FooterLine);
        Assert.Equal("C:\\app\\Assets\\Brand\\abwasser-uri-logo.png", build.Options.LogoPathAbs);
        Assert.Same(cost, build.Options.HoldingCost);
        Assert.Equal(new[] { "C:\\orig.pdf" }, build.Options.OriginalPdfPaths);

        var merge = Assert.Single(merged);
        Assert.Equal(new byte[] { 1, 2, 3 }, merge.Generated);
        Assert.Equal(new[] { "C:\\orig.pdf" }, merge.Originals);

        var output = Assert.Single(written);
        Assert.Equal("C:\\out\\dossier.pdf", output.Path);
        Assert.Equal(new byte[] { 9, 9 }, output.Bytes);
        Assert.Equal(("Dossier wurde erstellt:\nC:\\out\\dossier.pdf", "Dossier"), dialogs.LastInfo);
        Assert.Null(dialogs.LastError);
    }

    [Fact]
    public async Task PrintDossierPdfAsync_originale_allein_werden_ohne_basis_dossier_gemerged()
    {
        var dialogs = new CapturingDialogService { SaveFileResult = "C:\\out\\dossier.pdf" };
        var written = new List<(string Path, byte[] Bytes)>();
        var controller = CreateController(
            dialogs,
            resolveDossierOriginalPdfPaths: (_, _, _, _) => new List<string> { "C:\\orig.pdf" },
            selectDossierPrintOptions: _ => EmptyDossierOptions() with { IncludeOriginalProtokolle = true },
            buildDossierPdfAsync: (_, _, _, _, _, _, _) => throw new InvalidOperationException("base dossier should not be built"),
            mergeOriginals: originals =>
            {
                Assert.Equal(new[] { "C:\\orig.pdf" }, originals);
                return new byte[] { 7, 7 };
            },
            mergeWithOriginals: (_, _) => throw new InvalidOperationException("originals are already merged"),
            writeAllBytesAsync: (path, bytes) =>
            {
                written.Add((path, bytes));
                return Task.CompletedTask;
            });

        await controller.PrintDossierPdfAsync(new Project(), Record("12/34"));

        var output = Assert.Single(written);
        Assert.Equal("C:\\out\\dossier.pdf", output.Path);
        Assert.Equal(new byte[] { 7, 7 }, output.Bytes);
        Assert.Equal(("Dossier wurde erstellt:\nC:\\out\\dossier.pdf", "Dossier"), dialogs.LastInfo);
    }

    [Fact]
    public async Task PrintDossierPdfAsync_verwendet_injizierte_Protokollsuche()
    {
        var dialogs = new CapturingDialogService { SaveFileResult = "C:\\out\\dossier.pdf" };
        var locator = new RecordingInspectionProtocolFileLocator("C:\\projekt\\original.pdf");
        var controller = CreateController(
            dialogs,
            inspectionProtocolFiles: locator,
            selectDossierPrintOptions: _ => EmptyDossierOptions() with { IncludeOriginalProtokolle = true },
            mergeOriginals: paths =>
            {
                Assert.Equal(new[] { "C:\\projekt\\original.pdf" }, paths);
                return new byte[] { 7 };
            });

        await controller.PrintDossierPdfAsync(new Project(), Record("12/34"));

        Assert.Equal(1, locator.ResolveOriginalCalls);
        Assert.Equal(("Dossier wurde erstellt:\nC:\\out\\dossier.pdf", "Dossier"), dialogs.LastInfo);
    }

    [Fact]
    public async Task PrintDossierPdfAsync_meldet_fehler_wenn_original_merge_leer_ist()
    {
        var dialogs = new CapturingDialogService { SaveFileResult = "C:\\out\\dossier.pdf" };
        var controller = CreateController(
            dialogs,
            resolveDossierOriginalPdfPaths: (_, _, _, _) => new List<string> { "C:\\orig.pdf" },
            selectDossierPrintOptions: _ => EmptyDossierOptions() with { IncludeOriginalProtokolle = true },
            mergeOriginals: _ => Array.Empty<byte>(),
            writeAllBytesAsync: (_, _) => throw new InvalidOperationException("file should not be written"));

        await controller.PrintDossierPdfAsync(new Project(), Record("12/34"));

        Assert.Equal(("Dossier konnte nicht erstellt werden:\nDie Original-Protokolle konnten nicht zusammengefuehrt werden.", "Dossier"), dialogs.LastError);
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
        Func<HaltungRecord, HydraulikCalcResult, HydraulikPrintOptions, Task<byte[]>>? buildHydraulikPdfAsync = null,
        Func<string?>? getLastProjectPath = null,
        Func<string, (string? VonNr, string? BisNr)>? splitHoldingNodes = null,
        Func<string?, SchachtRecord?>? findSchachtByNummer = null,
        Func<HaltungRecord, DataPageHydraulikAvailability>? readDossierHydraulikAvailability = null,
        Func<HaltungRecord, double?, HydraulikCalcResult?>? buildDossierHydraulikCalculation = null,
        Func<string, HoldingCost?>? findHoldingCost = null,
        Func<HaltungRecord, string, SchachtRecord?, SchachtRecord?, List<string>>? resolveDossierOriginalPdfPaths = null,
        Func<DataPageDossierPrintAvailability, DossierPrintOptions?>? selectDossierPrintOptions = null,
        Func<Project, HaltungRecord, SchachtRecord?, SchachtRecord?, HydraulikCalcResult?, string, DossierPrintOptions, Task<byte[]>>? buildDossierPdfAsync = null,
        Func<IReadOnlyList<string>, byte[]>? mergeOriginals = null,
        Func<byte[], IReadOnlyList<string>, byte[]>? mergeWithOriginals = null,
        Func<Project, string, HaltungRecord, ProtocolDocument, string?>? regenerateOne = null,
        Func<string, bool>? openPdf = null,
        IDossierPhotoAvailabilityService? dossierPhotoAvailability = null,
        IInspectionProtocolFileLocator? inspectionProtocolFiles = null,
        IProjectCostStoreRepository? projectCosts = null)
        => new(
            dialogs,
            getProjectFolder: () => projectFolder,
            buildAwuPdf: buildAwuPdf ?? ((_, _, _, _, _) => Array.Empty<byte>()),
            baseDirectory,
            projectCosts: projectCosts ?? new RecordingProjectCostStoreRepository(),
            fileExists: fileExists ?? (_ => false),
            writeAllBytes: writeAllBytes ?? ((_, _) => { }),
            writeAllBytesAsync: writeAllBytesAsync ?? ((_, _) => Task.CompletedTask),
            now: now ?? (() => new DateTime(2026, 1, 2)),
            buildHydraulikCalculation: buildHydraulikCalculation,
            selectHydraulikPrintOptions: selectHydraulikPrintOptions,
            buildHydraulikPdfAsync: buildHydraulikPdfAsync,
            getLastProjectPath: getLastProjectPath,
            splitHoldingNodes: splitHoldingNodes,
            findSchachtByNummer: findSchachtByNummer,
            readDossierHydraulikAvailability: readDossierHydraulikAvailability,
            buildDossierHydraulikCalculation: buildDossierHydraulikCalculation,
            findHoldingCost: findHoldingCost,
            resolveDossierOriginalPdfPaths: resolveDossierOriginalPdfPaths,
            selectDossierPrintOptions: selectDossierPrintOptions,
            buildDossierPdfAsync: buildDossierPdfAsync,
            mergeOriginals: mergeOriginals,
            mergeWithOriginals: mergeWithOriginals,
            regenerateOne: regenerateOne,
            openPdf: openPdf ?? (_ => true),
            dossierPhotoAvailability: dossierPhotoAvailability,
            inspectionProtocolFiles: inspectionProtocolFiles);

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

    private static SchachtRecord Schacht(string nr)
    {
        var record = new SchachtRecord();
        record.SetFieldValue("Schachtnummer", nr);
        return record;
    }

    private static HoldingCost HoldingCostWithMeasure(string holding)
        => new()
        {
            Holding = holding,
            Measures =
            {
                new MeasureCost
                {
                    MeasureId = "m",
                    MeasureName = "Massnahme",
                    Total = 1
                }
            }
        };

    private static DossierPrintOptions EmptyDossierOptions()
        => new()
        {
            IncludeDeckblatt = false,
            IncludeHaltungsprotokoll = false,
            IncludeFotos = false,
            IncludeSchachtVon = false,
            IncludeSchachtBis = false,
            IncludeHydraulik = false,
            IncludeKostenschaetzung = false,
            IncludeOriginalProtokolle = false
        };

    private sealed class RecordingDossierPhotoAvailability(bool result) : IDossierPhotoAvailabilityService
    {
        public int Calls { get; private set; }

        public bool HasPrintablePhotos(HaltungRecord record, string projectFolder)
        {
            Calls++;
            return result;
        }
    }

    private sealed class RecordingInspectionProtocolFileLocator(string originalPath)
        : IInspectionProtocolFileLocator
    {
        public int ResolveOriginalCalls { get; private set; }

        public string? ResolveExistingPath(string? raw, string? projectPath) => null;

        public string? FindProtocolPath(
            HaltungRecord record,
            string? resolvedLink,
            string? initialFolder,
            string? projectPath,
            string? storedFilesRaw)
            => null;

        public List<string> ResolveOriginalPdfPaths(HaltungRecord record, string projectFolder)
        {
            ResolveOriginalCalls++;
            return [originalPath];
        }

        public void AddResolvedPdf(List<string> paths, string? raw, string projectFolder)
        {
        }

        public void ResolveSchachtPdfPaths(
            SchachtRecord schacht,
            string projectFolder,
            List<string> paths)
        {
        }
    }

    private sealed class RecordingProjectCostStoreRepository : IProjectCostStoreRepository
    {
        public ProjectCostStore Store { get; } = new();
        public string? LoadedProjectPath { get; private set; }

        public ProjectCostStore Load(string? projectPath)
        {
            LoadedProjectPath = projectPath;
            return Store;
        }

        public ProjectCostStore Load(string? projectPath, out string? loadError)
        {
            loadError = null;
            return Load(projectPath);
        }

        public bool Save(string? projectPath, ProjectCostStore store, out string? error)
        {
            error = null;
            return true;
        }

        public string GetStorePath(string projectDirectory)
            => Path.Combine(projectDirectory, "costs.json");
    }

    private sealed class CapturingDialogService : IDialogService
    {
        public string? SaveFileResult { get; set; } = "C:\\out\\awu.pdf";
        public List<(string Title, string Filter, string? DefaultExt, string? DefaultFileName)> SaveFileCalls { get; } = new();
        public bool ConfirmWarnResult { get; set; } = true;
        public List<(string Message, string Title, bool DefaultNo)> ConfirmWarnCalls { get; } = new();
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
        {
            ConfirmWarnCalls.Add((message, title, defaultNo));
            return ConfirmWarnResult;
        }

        public DialogConfirm ConfirmCancel(string message, string title = "Bestaetigung")
            => throw new NotSupportedException();
    }
}
