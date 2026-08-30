using AuswertungPro.Next.Application.Common;
using AuswertungPro.Next.Application.Export;
using AuswertungPro.Next.Domain.Models;
using AuswertungPro.Next.Infrastructure.HoldingDistribution;
using AuswertungPro.Next.UI.DataPage;
using AuswertungPro.Next.UI.Views.Pages.Schachtansicht;
using AuswertungPro.Next.UI.Views.Windows;
using System.IO;

namespace AuswertungPro.Next.UI.Tests;

public sealed class SchaechteRecordDetailsBuilderTests
{
    [Fact]
    public void Build_GruppiertFelderUndSchaltetSanierungsdetailsLive()
    {
        var record = new SchachtRecord();
        record.SetFieldValue("Schachtnummer", "S-1");
        record.SetFieldValue("Sanieren", "Nein");
        record.SetFieldValue("Kosten", "1200");
        record.SetFieldValue("Zustandsklasse", "3");
        record.SetFieldValue("PDF", "bericht.pdf");
        var commits = new List<(KonsolidiertesSchachtFeld Field, string? Value)>();
        var builder = new SchaechteRecordDetailsBuilder(
            _ => ["Nein", "Ja"],
            _ => null,
            (_, field, value) => commits.Add((field, value)));

        var groups = builder.Build(
            ["Schachtnummer", "Sanieren", "Kosten", "Zustandsklasse", "PDF"],
            record);

        Assert.Equal(
            ["Stammdaten", "Zustand und Inspektion", "Sanierung und Kosten", "Dokumente und Medien"],
            groups.Select(group => group.Title));
        Assert.Equal(
            [RecordDetailGroupKind.MasterData, RecordDetailGroupKind.Condition,
                RecordDetailGroupKind.RenovationCosts, RecordDetailGroupKind.Documents],
            groups.Select(group => group.Kind));
        var renovation = groups.Single(group => group.Title == "Sanierung und Kosten");
        var switchItem = renovation.Items.Single(item => item.Label == "Sanieren Ja/Nein");
        var costItem = renovation.Items.Single(item => item.Label == "Kosten");
        Assert.False(costItem.IsVisible);

        switchItem.Value = "Ja";

        Assert.True(costItem.IsVisible);
        Assert.Contains(commits, commit => commit.Field.AnzeigeName == "Sanieren" && commit.Value == "Ja");
        var state = groups.Single(group => group.Title == "Zustand und Inspektion")
            .Items.Single(item => item.Label == "Zustandsklasse");
        Assert.True(state.IsCombo);
        Assert.Equal(["0", "1", "2", "3", "4"], state.Options);
    }

    [Fact]
    public void Build_KonsolidiertEncodingVariantenUndUebergibtAlleSchluesselBeimCommit()
    {
        var record = new SchachtRecord();
        record.SetFieldValue("Ausfuehrung", "Beton");
        record.SetFieldValue("Ausführung", "");
        KonsolidiertesSchachtFeld? committedField = null;
        var builder = new SchaechteRecordDetailsBuilder(
            _ => [],
            _ => null,
            (_, field, _) => committedField = field);

        var groups = builder.Build(["Ausführung"], record);
        var item = Assert.Single(Assert.Single(groups).Items);

        item.Value = "Kunststoff";

        Assert.NotNull(committedField);
        Assert.Equal(2, committedField.AlleKeys.Count);
        Assert.Contains("Ausfuehrung", committedField.AlleKeys);
        Assert.Contains("Ausführung", committedField.AlleKeys);
    }

    [Fact]
    public void Build_OhneViewModel_laesstDropdownFeldWieBisherAlsTextfeld()
    {
        var record = new SchachtRecord();
        record.SetFieldValue("Sanieren", "Nein");
        var builder = new SchaechteRecordDetailsBuilder(
            _ => ["Nein", "Ja"],
            _ => null,
            (_, _, _) => { },
            canResolveDropdowns: () => false);

        var groups = builder.Build(["Sanieren"], record);
        var item = Assert.Single(Assert.Single(groups).Items);

        Assert.False(item.IsCombo);
    }

    [Fact]
    public void ShaftRename_AktualisiertRecordUndProjektmetadatenOhneDateipfade()
    {
        var record = new SchachtRecord();
        record.SetFieldValue("Schachtnummer", "A-1");
        var project = new Project();
        var errors = new List<string>();

        var success = SchaechteShaftRenameController.Apply(
            new ShaftRenameFileService(),
            new PdfTextLayerRewriteService(),
            record,
            "A-1",
            "B-2",
            projectPath: null,
            project,
            (message, _) => errors.Add(message));

        Assert.True(success);
        Assert.Empty(errors);
        Assert.Equal("B-2", record.GetFieldValue("Schachtnummer"));
        Assert.Equal("B-2", PdfCorrectionMetadata.LoadShaftRenames(project)["A-1"]);
    }

    [Fact]
    public void CollectPdfPaths_NimmtPdfFelderAufUndDedupliziert()
    {
        var root = Path.Combine(Path.GetTempPath(), "schacht-detail-paths", Guid.NewGuid().ToString("N"));
        var projectPath = Path.Combine(root, "projekt.json");
        Directory.CreateDirectory(root);
        File.WriteAllText(Path.Combine(root, "a.pdf"), "pdf");
        File.WriteAllText(Path.Combine(root, "c.PDF"), "pdf");
        var record = new SchachtRecord();
        record.SetFieldValue(FieldKeys.PdfPath, "a.pdf;b.txt");
        record.SetFieldValue(FieldKeys.PdfAll, "a.pdf;c.PDF");

        try
        {
            var paths = SchaechteShaftRenameController.CollectPdfPaths(record, projectPath);

            Assert.Equal(2, paths.Count);
            Assert.Contains(paths, path => path.EndsWith("a.pdf", StringComparison.OrdinalIgnoreCase));
            Assert.Contains(paths, path => path.EndsWith("c.PDF", StringComparison.OrdinalIgnoreCase));
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }

    [Fact]
    public void ShaftRename_RuftBatchdienstMitDedupliziertenPdfPfadenAuf()
    {
        var root = Path.Combine(Path.GetTempPath(), "schacht-rename-pdfs", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        var projectPath = Path.Combine(root, "projekt.json");
        var pdfPath = Path.Combine(root, "a.pdf");
        File.WriteAllText(pdfPath, "Testdatei fuer Pfadauflösung");
        var record = new SchachtRecord();
        record.SetFieldValue("Schachtnummer", "A-1");
        record.SetFieldValue(FieldKeys.PdfPath, "a.pdf;a.pdf");
        record.SetFieldValue(FieldKeys.PdfAll, "a.pdf");
        var rewriter = new RecordingPdfTextLayerRewriter
        {
            BatchResult = new PdfTextLayerBatchRewriteResult(0, 0, 1)
        };
        var errors = new List<string>();

        try
        {
            var success = SchaechteShaftRenameController.Apply(
                new RecordingShaftRenameService(ShaftRenameService.ShaftRenameResult.Ok(false, 0)),
                rewriter,
                record,
                "A-1",
                "B-2",
                projectPath,
                new Project(),
                (message, _) => errors.Add(message));

            Assert.True(success);
            Assert.Equal(1, rewriter.BatchCalls);
            Assert.Equal("A-1", rewriter.OldValue);
            Assert.Equal("B-2", rewriter.NewValue);
            Assert.Equal(pdfPath, Assert.Single(rewriter.PdfPaths!), ignoreCase: true);
            Assert.Single(errors);
            Assert.Contains("1 Protokoll-PDF(s)", errors[0], StringComparison.Ordinal);
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }

    [Fact]
    public void ShaftRename_RenameFehlerStartetKeinePdfKorrektur()
    {
        var record = new SchachtRecord();
        record.SetFieldValue("Schachtnummer", "A-1");
        record.SetFieldValue(FieldKeys.PdfPath, "a.pdf");
        var rewriter = new RecordingPdfTextLayerRewriter();
        var errors = new List<string>();

        var success = SchaechteShaftRenameController.Apply(
            new RecordingShaftRenameService(ShaftRenameService.ShaftRenameResult.Fail("Testfehler")),
            rewriter,
            record,
            "A-1",
            "B-2",
            projectPath: null,
            new Project(),
            (message, _) => errors.Add(message));

        Assert.False(success);
        Assert.Equal(0, rewriter.BatchCalls);
        Assert.Equal("A-1", record.GetFieldValue("Schachtnummer"));
        Assert.Single(errors);
        Assert.Contains("Testfehler", errors[0], StringComparison.Ordinal);
    }

    private sealed class RecordingShaftRenameService(
        ShaftRenameService.ShaftRenameResult result) : IShaftRenameService
    {
        public ShaftRenameService.ShaftRenameResult Rename(
            SchachtRecord record,
            string oldShaftNumber,
            string newShaftNumber,
            string? projectFilePath)
            => result;
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
