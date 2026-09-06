using AuswertungPro.Next.Application.Common;
using AuswertungPro.Next.Domain.Models;
using AuswertungPro.Next.Domain.Protocol;
using AuswertungPro.Next.Infrastructure.Tests.Backup;

namespace AuswertungPro.Next.Infrastructure.Tests;

public sealed class ShaftRenameSafetyTests : IDisposable
{
    private readonly string _root = Path.Combine(Path.GetTempPath(), "shaft-rename-safety", Guid.NewGuid().ToString("N"));
    private string ProjectRoot => Path.Combine(_root, "Projekt");
    private string ProjectFile => Path.Combine(ProjectRoot, "Projektdateien", "projekt.json");

    public ShaftRenameSafetyTests() => Write(ProjectFile, "{}");

    [Theory]
    [InlineData("PDF_Path")]
    [InlineData("PDF_All")]
    [InlineData("PDF_Eigen")]
    [InlineData("Link")]
    public void Rename_FremderPfad_WeistAbUndBewahrtOriginalUndVerweise(string field)
    {
        var original = Write(Path.Combine(_root, "Kundenquelle", "123", "123.pdf"));
        var ownPdf = Write(Path.Combine(ProjectRoot, "Schächte_Verteilt", "123", "123.pdf"));
        var record = Record(ownPdf);
        record.SetFieldValueTechnical(field, original);

        var result = ShaftRenameService.Rename(record, "123", "456", ProjectFile);

        Assert.False(result.Success);
        Assert.Contains("Projekt", result.ErrorMessage, StringComparison.OrdinalIgnoreCase);
        Assert.Equal("Testoriginal", File.ReadAllText(original));
        Assert.Equal(original, record.GetFieldValue(field));
        Assert.True(File.Exists(ownPdf));
        Assert.False(Directory.Exists(Path.Combine(ProjectRoot, "Schächte_Verteilt", "456")));
    }

    [Fact]
    public void Rename_DateipfadOhneProjekt_WeistAb()
    {
        var original = Write(Path.Combine(_root, "123", "123.pdf"));
        var record = Record(original);

        var result = ShaftRenameService.Rename(record, "123", "456", null);

        Assert.False(result.Success);
        Assert.Equal(original, record.GetFieldValue(FieldKeys.PdfPath));
        Assert.Equal("Testoriginal", File.ReadAllText(original));
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void Rename_Unterordner_DateiUndVerweisFolgenDemselbenPlan(bool relative)
    {
        var oldPdf = Write(Path.Combine(ProjectRoot, "Schächte_Verteilt", "123", "Sanierung", "123", "123.pdf"));
        var record = Record(relative ? Path.GetRelativePath(ProjectRoot, oldPdf) : oldPdf);
        record.SetFieldValueTechnical(FieldKeys.PdfAll, record.GetFieldValue(FieldKeys.PdfPath));

        var result = ShaftRenameService.Rename(record, "123", "456", ProjectFile);

        Assert.True(result.Success, result.ErrorMessage);
        var expected = Path.Combine(ProjectRoot, "Schächte_Verteilt", "456", "Sanierung", "456", "456.pdf");
        Assert.Equal("Testoriginal", File.ReadAllText(expected));
        foreach (var field in new[] { FieldKeys.PdfPath, FieldKeys.PdfAll })
        {
            var stored = record.GetFieldValue(field);
            Assert.Equal(!relative, Path.IsPathRooted(stored));
            Assert.Equal(expected, Path.GetFullPath(ProjectPathResolver.ResolveFilePath(stored, ProjectFile)!));
        }
        Assert.False(File.Exists(oldPdf));
    }

    [Fact]
    public void Rename_FotoZielkonflikt_AendertAuchDenHauptordnerNicht()
    {
        var original = Write(Path.Combine(ProjectRoot, "Schächte_Verteilt", "123", "123.pdf"));
        var photo = Write(Path.Combine(ProjectRoot, "Fotos", "Schächte", "123", "123.jpg"));
        var other = Write(Path.Combine(ProjectRoot, "Fotos", "Schächte", "456", "fremd.jpg"), "Fremdes Foto");
        var record = Record(original);

        var result = ShaftRenameService.Rename(record, "123", "456", ProjectFile);

        Assert.False(result.Success);
        Assert.Equal("Testoriginal", File.ReadAllText(original));
        Assert.Equal("Testoriginal", File.ReadAllText(photo));
        Assert.Equal("Fremdes Foto", File.ReadAllText(other));
        Assert.Equal(original, record.GetFieldValue(FieldKeys.PdfPath));
    }

    [Fact]
    public void Rename_DateikonfliktImUnterordner_AendertNichts()
    {
        var original = Write(Path.Combine(ProjectRoot, "Schächte_Verteilt", "123", "Sanierung", "123.pdf"));
        var other = Write(Path.Combine(Path.GetDirectoryName(original)!, "456.pdf"), "Anderes PDF");
        var record = Record(original);

        var result = ShaftRenameService.Rename(record, "123", "456", ProjectFile);

        Assert.False(result.Success);
        Assert.Equal("Testoriginal", File.ReadAllText(original));
        Assert.Equal("Anderes PDF", File.ReadAllText(other));
        Assert.Equal(original, record.GetFieldValue(FieldKeys.PdfPath));
    }

    [JunctionFact]
    public void Rename_VerknuepfterUnterordner_WeistDenGanzenLaufAb()
    {
        var original = Write(Path.Combine(ProjectRoot, "Schächte_Verteilt", "123", "123.pdf"));
        var external = Write(Path.Combine(_root, "Kundenquelle", "123.pdf"));
        var link = Path.Combine(Path.GetDirectoryName(original)!, "Sanierung");
        JunctionTestSupport.CreateDirectoryLink(link, Path.GetDirectoryName(external)!);
        try
        {
            var record = Record(original);
            var result = ShaftRenameService.Rename(record, "123", "456", ProjectFile);
            Assert.False(result.Success);
            Assert.Equal("Testoriginal", File.ReadAllText(original));
            Assert.Equal("Testoriginal", File.ReadAllText(external));
            Assert.Equal(original, record.GetFieldValue(FieldKeys.PdfPath));
        }
        finally { Directory.Delete(link); }
    }

    [Theory]
    [InlineData("Imports")]
    [InlineData("Importdateien")]
    [InlineData("__RESTORE_POINTS")]
    [InlineData("Projektdateien")]
    public void Rename_Projektarchiv_BleibtUnveraendert(string archiveFolder)
    {
        var original = Write(Path.Combine(ProjectRoot, archiveFolder, "123", "123.pdf"));
        var record = Record(original);
        var result = ShaftRenameService.Rename(record, "123", "456", ProjectFile);
        Assert.False(result.Success);
        Assert.Equal("Testoriginal", File.ReadAllText(original));
        Assert.Equal(original, record.GetFieldValue(FieldKeys.PdfPath));
    }

    [Fact]
    public void Rename_GesperrtesFoto_NimmtHauptordnerUndVerweiseZurueck()
    {
        var original = Write(Path.Combine(ProjectRoot, "Schächte_Verteilt", "123", "123.pdf"));
        var photo = Write(Path.Combine(ProjectRoot, "Fotos", "Schächte", "123", "123.jpg"));
        var record = Record(original);
        ShaftRenameService.ShaftRenameResult result;
        using (File.Open(photo, FileMode.Open, FileAccess.Read, FileShare.None))
            result = ShaftRenameService.Rename(record, "123", "456", ProjectFile);

        Assert.False(result.Success);
        Assert.DoesNotContain("Rücknahme fehlgeschlagen", result.ErrorMessage);
        Assert.Equal("Testoriginal", File.ReadAllText(original));
        Assert.Equal("Testoriginal", File.ReadAllText(photo));
        Assert.Equal(original, record.GetFieldValue(FieldKeys.PdfPath));
        Assert.False(Directory.Exists(Path.Combine(ProjectRoot, "Schächte_Verteilt", "456")));
    }

    [Fact]
    public void Rename_FotosInAllenProtokollstaenden_BleibenErreichbar()
    {
        var original = Write(Path.Combine(ProjectRoot, "Schächte_Verteilt", "123", "123.pdf"));
        var photo = Write(Path.Combine(ProjectRoot, "Fotos", "Schächte", "123", "Details", "123.jpg"));
        var archive = Write(Path.Combine(ProjectRoot, "Importdateien", "unveraendertes-foto.jpg"));
        var record = Record(original);
        record.Protocol = new ProtocolDocument { History = [new ProtocolRevision()] };
        var revisions = new[] { record.Protocol.Original, record.Protocol.Current, record.Protocol.History[0] };
        foreach (var revision in revisions)
            revision.Entries.Add(new ProtocolEntry
            {
                FotoPaths = [Path.GetRelativePath(ProjectRoot, photo)],
                OriginalFotoPaths = [photo, archive]
            });

        var result = ShaftRenameService.Rename(record, "123", "456", ProjectFile);

        Assert.True(result.Success, result.ErrorMessage);
        var expected = Path.Combine(ProjectRoot, "Fotos", "Schächte", "456", "Details", "456.jpg");
        Assert.Equal("Testoriginal", File.ReadAllText(expected));
        foreach (var revision in revisions)
        {
            var entry = Assert.Single(revision.Entries);
            Assert.Equal(expected, ProjectPathResolver.ResolveFilePath(Assert.Single(entry.FotoPaths), ProjectFile));
            Assert.Equal(expected, Path.GetFullPath(ProjectPathResolver.ResolveFilePath(entry.OriginalFotoPaths[0], ProjectFile)!));
            Assert.Equal(archive, entry.OriginalFotoPaths[1]);
        }
        Assert.Equal("Testoriginal", File.ReadAllText(archive));
    }

    private static SchachtRecord Record(string pdf)
    {
        var record = new SchachtRecord();
        record.SetFieldValueTechnical(FieldKeys.PdfPath, pdf);
        return record;
    }

    private static string Write(string path, string text = "Testoriginal")
    {
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        File.WriteAllText(path, text);
        return path;
    }

    public void Dispose() { if (Directory.Exists(_root)) Directory.Delete(_root, true); }
}
