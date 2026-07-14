using AuswertungPro.Next.Application.Common;
using AuswertungPro.Next.Domain.Models;
using AuswertungPro.Next.UI.DataPage;
using AuswertungPro.Next.UI.Views.Pages.Schachtansicht;
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
}
