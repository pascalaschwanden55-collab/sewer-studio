using AuswertungPro.Next.Application.Common;
using AuswertungPro.Next.Application.Export;
using AuswertungPro.Next.Domain.Models;
using AuswertungPro.Next.UI.DataPage;
using System.IO;

namespace AuswertungPro.Next.UI.Tests;

public sealed class DataPageHoldingRenameControllerTests
{
    [Fact]
    public void GleicherName_IstOhneNebenwirkungErfolgreich()
    {
        var record = Record("A-1");
        var project = ProjectWith(record);
        var rename = new RecordingHoldingRenameService();
        var rewriter = new RecordingPdfTextLayerRewriter();

        var success = DataPageHoldingRenameController.Apply(
            rename,
            rewriter,
            record,
            "A-1",
            "a-1",
            projectPath: null,
            project,
            (_, _) => throw new InvalidOperationException("Keine Warnung erwartet."),
            (_, _) => throw new InvalidOperationException("Kein Fehler erwartet."));

        Assert.True(success);
        Assert.Equal(0, rename.Calls);
        Assert.Equal(0, rewriter.BatchCalls);
        Assert.Equal("A-1", record.GetFieldValue(FieldKeys.HoldingName));
        Assert.Empty(PdfCorrectionMetadata.LoadHoldingRenames(project));
    }

    [Fact]
    public void DoppelterName_WarntUndStartetKeineDienste()
    {
        var record = Record("A-1");
        var existing = Record("B-2");
        var project = ProjectWith(record, existing);
        var rename = new RecordingHoldingRenameService();
        var rewriter = new RecordingPdfTextLayerRewriter();
        var warnings = new List<(string Message, string Title)>();

        var success = DataPageHoldingRenameController.Apply(
            rename,
            rewriter,
            record,
            "A-1",
            " b-2 ",
            projectPath: null,
            project,
            (message, title) => warnings.Add((message, title)),
            (_, _) => throw new InvalidOperationException("Kein Fehler erwartet."));

        Assert.False(success);
        Assert.Equal(0, rename.Calls);
        Assert.Equal(0, rewriter.BatchCalls);
        Assert.Equal("A-1", record.GetFieldValue(FieldKeys.HoldingName));
        Assert.Equal(
            ("Die Haltungsnummer 'b-2' ist bereits vorhanden.", "Doppelte Haltungsnummer"),
            Assert.Single(warnings));
    }

    [Fact]
    public void RenameFehler_LaesstNameMetadatenUndPdfDienstUnveraendert()
    {
        var record = Record("A-1");
        record.SetFieldValue(FieldKeys.PdfPath, "a.pdf", FieldSource.Manual, userEdited: false);
        var project = ProjectWith(record);
        var rename = new RecordingHoldingRenameService
        {
            Result = HoldingRenameService.HoldingRenameResult.Fail("Testfehler")
        };
        var rewriter = new RecordingPdfTextLayerRewriter();
        var errors = new List<(string Message, string Title)>();

        var success = DataPageHoldingRenameController.Apply(
            rename,
            rewriter,
            record,
            "A-1",
            "B-2",
            projectPath: null,
            project,
            (_, _) => throw new InvalidOperationException("Keine Warnung erwartet."),
            (message, title) => errors.Add((message, title)));

        Assert.False(success);
        Assert.Equal(1, rename.Calls);
        Assert.Equal(0, rewriter.BatchCalls);
        Assert.Equal("A-1", record.GetFieldValue(FieldKeys.HoldingName));
        Assert.Empty(PdfCorrectionMetadata.LoadHoldingRenames(project));
        Assert.Equal(("Umbenennen fehlgeschlagen:\nTestfehler", "Umbenennen"), Assert.Single(errors));
    }

    [Fact]
    public void ErfolgOhnePdf_AktualisiertNameUndProjektmetadaten()
    {
        var record = Record("A-1");
        var project = ProjectWith(record);
        var rename = new RecordingHoldingRenameService();
        var rewriter = new RecordingPdfTextLayerRewriter();

        var success = DataPageHoldingRenameController.Apply(
            rename,
            rewriter,
            record,
            "A-1",
            "B-2",
            projectPath: null,
            project,
            (_, _) => throw new InvalidOperationException("Keine Warnung erwartet."),
            (_, _) => throw new InvalidOperationException("Kein Fehler erwartet."));

        Assert.True(success);
        Assert.Equal("B-2", record.GetFieldValue(FieldKeys.HoldingName));
        Assert.Equal(FieldSource.Manual, record.FieldMeta[FieldKeys.HoldingName].Source);
        Assert.True(record.FieldMeta[FieldKeys.HoldingName].UserEdited);
        Assert.Equal("B-2", PdfCorrectionMetadata.LoadHoldingRenames(project)["A-1"]);
        Assert.Equal(0, rewriter.BatchCalls);
    }

    [Fact]
    public void PdfPfade_WerdenNachRenameGelesenDedupliziertUndAufZweiFelderBegrenzt()
    {
        var root = Directory.CreateTempSubdirectory("holding-rename-");
        var projectPath = Path.Combine(root.FullName, "projekt.json");
        var includedPdf = Path.Combine(root.FullName, "included.pdf");
        var ignoredPdf = Path.Combine(root.FullName, "ignored.pdf");
        File.WriteAllText(includedPdf, "Test");
        File.WriteAllText(ignoredPdf, "Test");
        var record = Record("A-1");
        record.SetFieldValue(FieldKeys.PdfPath, "vorher.pdf", FieldSource.Manual, userEdited: false);
        var project = ProjectWith(record);
        var rename = new RecordingHoldingRenameService
        {
            OnRename = current =>
            {
                current.SetFieldValue(FieldKeys.PdfPath, "included.pdf;included.pdf", FieldSource.Manual, userEdited: false);
                current.SetFieldValue(FieldKeys.PdfAll, "included.pdf", FieldSource.Manual, userEdited: false);
                current.SetFieldValue(FieldKeys.PdfEigen, "ignored.pdf", FieldSource.Manual, userEdited: false);
                current.SetFieldValue(FieldKeys.Link, "ignored.pdf", FieldSource.Manual, userEdited: false);
            }
        };
        var rewriter = new RecordingPdfTextLayerRewriter
        {
            BatchResult = new PdfTextLayerBatchRewriteResult(0, 0, 1)
        };
        var errors = new List<(string Message, string Title)>();

        try
        {
            var success = DataPageHoldingRenameController.Apply(
                rename,
                rewriter,
                record,
                "A-1",
                "B-2",
                projectPath,
                project,
                (_, _) => throw new InvalidOperationException("Keine Warnung erwartet."),
                (message, title) => errors.Add((message, title)));

            Assert.True(success);
            Assert.Equal(1, rewriter.BatchCalls);
            Assert.Equal(includedPdf, Assert.Single(rewriter.PdfPaths!), ignoreCase: true);
            Assert.Equal("A-1", rewriter.OldValue);
            Assert.Equal("B-2", rewriter.NewValue);
            Assert.Equal(
                ("1 Protokoll-PDF(s) konnten nicht aktualisiert werden.\n" +
                 "Die bisherigen PDF-Dateien wurden nicht ueberschrieben.", "PDF nicht aktualisiert"),
                Assert.Single(errors));
        }
        finally
        {
            root.Delete(recursive: true);
        }
    }

    private static HaltungRecord Record(string name)
    {
        var record = new HaltungRecord();
        record.SetFieldValue(FieldKeys.HoldingName, name, FieldSource.Manual, userEdited: false);
        return record;
    }

    private static Project ProjectWith(params HaltungRecord[] records)
    {
        var project = new Project();
        foreach (var record in records)
            project.Data.Add(record);
        return project;
    }

    private sealed class RecordingHoldingRenameService : IHoldingRenameService
    {
        public HoldingRenameService.HoldingRenameResult Result { get; init; }
            = HoldingRenameService.HoldingRenameResult.Ok(false, 0);
        public Action<HaltungRecord>? OnRename { get; init; }
        public int Calls { get; private set; }

        public HoldingRenameService.HoldingRenameResult Rename(
            HaltungRecord record,
            string oldHolding,
            string newHolding,
            string? projectFilePath)
        {
            Calls++;
            OnRename?.Invoke(record);
            return Result;
        }
    }

    private sealed class RecordingPdfTextLayerRewriter : IPdfTextLayerRewriter
    {
        public PdfTextLayerBatchRewriteResult BatchResult { get; init; } = new(0, 0, 0);
        public int BatchCalls { get; private set; }
        public IReadOnlyList<string>? PdfPaths { get; private set; }
        public string? OldValue { get; private set; }
        public string? NewValue { get; private set; }

        public bool CanRewrite(string? oldValue, string? newValue) => true;

        public PdfTextLayerRewriteResult TryRewriteHoldingNumber(
            string sourcePdfPath,
            string? oldValue,
            string? newValue)
            => throw new NotSupportedException();

        public PdfTextLayerBatchRewriteResult RewriteIdentifierInPlace(
            IReadOnlyList<string> pdfPaths,
            string? oldValue,
            string? newValue)
        {
            BatchCalls++;
            PdfPaths = pdfPaths.ToArray();
            OldValue = oldValue;
            NewValue = newValue;
            return BatchResult;
        }
    }
}
