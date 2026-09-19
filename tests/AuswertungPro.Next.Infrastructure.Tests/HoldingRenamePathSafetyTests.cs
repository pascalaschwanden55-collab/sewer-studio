using AuswertungPro.Next.Application.Common;
using AuswertungPro.Next.Domain.Models;
using AuswertungPro.Next.Infrastructure.Tests.Backup;

namespace AuswertungPro.Next.Infrastructure.Tests;

public sealed class HoldingRenamePathSafetyTests : IDisposable
{
    private readonly string _root = Path.Combine(Path.GetTempPath(), "holding-rename-safety", Guid.NewGuid().ToString("N"));

    [JunctionFact]
    public void Rename_VerknuepfterHaltungsordner_BewahrtFremdeOriginale()
        => CheckLinkedPath("Haltungen_Verteilt/H-1");

    [JunctionFact]
    public void Rename_VerknuepfteVerteilwurzel_BewahrtFremdeOriginale()
        => CheckLinkedPath("Haltungen_Verteilt");

    [JunctionFact]
    public void Rename_VerknuepfteProjektwurzel_BewahrtFremdeOriginale()
        => CheckLinkedPath("");

    [JunctionFact]
    public void Rename_VerknuepfterProjektElternordner_BewahrtFremdeOriginale()
        => CheckLinkedPath("", linkedParent: true);

    [JunctionFact]
    public void Rename_VerknuepfterFotoordner_BewahrtAuchDenHaltungsordner()
        => CheckLinkedPath("Fotos/Haltungen/H-1");

    [JunctionFact]
    public void Rename_VerknuepfteFotowurzel_BewahrtAuchDenHaltungsordner()
        => CheckLinkedPath("Fotos/Haltungen");

    [JunctionFact]
    public void Rename_VerknuepfterUnterordner_BrichtVorJederAenderungAb()
    {
        var projectRoot = Path.Combine(_root, "Projekt");
        var projectFile = Write(Path.Combine(projectRoot, "Projektdateien", "projekt.json"), "{}");
        var original = Write(Path.Combine(projectRoot, "Haltungen_Verteilt", "H-1", "H-1.pdf"));
        var external = Write(Path.Combine(_root, "Kundenquelle", "H-1.pdf"));
        var link = Path.Combine(Path.GetDirectoryName(original)!, "Details");
        JunctionTestSupport.CreateDirectoryLink(link, Path.GetDirectoryName(external)!);
        var record = Record("Haltungen_Verteilt/H-1/H-1.pdf");

        var result = HoldingRenameService.Rename(record, "H-1", "H-2", projectFile);

        Assert.False(result.Success);
        Assert.Contains("Verkn", result.ErrorMessage, StringComparison.OrdinalIgnoreCase);
        Assert.Equal("Testoriginal", File.ReadAllText(original));
        Assert.Equal("Testoriginal", File.ReadAllText(external));
        Assert.Equal("Haltungen_Verteilt/H-1/H-1.pdf", record.GetFieldValue(FieldKeys.PdfPath));
    }

    [Fact]
    public void Rename_NormaleArbeitskopien_BenennenDateienUndVerweiseWeiterhinUm()
    {
        var projectRoot = Path.Combine(_root, "Projekt");
        var projectFile = Write(Path.Combine(projectRoot, "Projektdateien", "projekt.json"), "{}");
        Write(Path.Combine(projectRoot, "Haltungen_Verteilt", "H-1", "H-1.pdf"));
        Write(Path.Combine(projectRoot, "Fotos", "Haltungen", "H-1", "H-1.jpg"));
        var record = Record("Haltungen_Verteilt/H-1/H-1.pdf");

        var result = HoldingRenameService.Rename(record, "H-1", "H-2", projectFile);

        Assert.True(result.Success, result.ErrorMessage);
        Assert.Equal("Testoriginal", File.ReadAllText(Path.Combine(projectRoot, "Haltungen_Verteilt", "H-2", "H-2.pdf")));
        Assert.Equal("Testoriginal", File.ReadAllText(Path.Combine(projectRoot, "Fotos", "Haltungen", "H-2", "H-2.jpg")));
        Assert.Equal("Haltungen_Verteilt/H-2/H-2.pdf", record.GetFieldValue(FieldKeys.PdfPath));
    }

    [Fact]
    public void Rename_VerknuepfteDatei_BewahrtOriginalUndVerweise()
    {
        var projectRoot = Path.Combine(_root, "Projekt");
        var projectFile = Write(Path.Combine(projectRoot, "Projektdateien", "projekt.json"), "{}");
        var original = Write(Path.Combine(_root, "Kundenquelle", "H-1.pdf"));
        var link = Path.Combine(projectRoot, "Haltungen_Verteilt", "H-1", "H-1.pdf");
        Directory.CreateDirectory(Path.GetDirectoryName(link)!);
        File.CreateSymbolicLink(link, original);
        var record = Record("Haltungen_Verteilt/H-1/H-1.pdf");

        var result = HoldingRenameService.Rename(record, "H-1", "H-2", projectFile);

        Assert.False(result.Success);
        Assert.Contains("Verkn", result.ErrorMessage, StringComparison.OrdinalIgnoreCase);
        Assert.Equal("Testoriginal", File.ReadAllText(original));
        Assert.True(File.Exists(link));
        Assert.Equal("Haltungen_Verteilt/H-1/H-1.pdf", record.GetFieldValue(FieldKeys.PdfPath));
    }

    [Theory]
    [InlineData("Haltungen_Verteilt")]
    [InlineData("Fotos/Haltungen")]
    public void Rename_DefekteZielverknuepfung_BrichtVorJederAenderungAb(string targetParent)
    {
        var projectRoot = Path.Combine(_root, "Projekt");
        var projectFile = Write(Path.Combine(projectRoot, "Projektdateien", "projekt.json"), "{}");
        var original = Write(Path.Combine(projectRoot, "Haltungen_Verteilt", "H-1", "H-1.pdf"));
        var photo = Write(Path.Combine(projectRoot, "Fotos", "Haltungen", "H-1", "H-1.jpg"));
        var link = Path.Combine(projectRoot, targetParent.Replace('/', Path.DirectorySeparatorChar), "H-2");
        Directory.CreateSymbolicLink(link, Path.Combine(_root, "NichtVorhandeneQuelle"));
        var record = Record("Haltungen_Verteilt/H-1/H-1.pdf");

        var result = HoldingRenameService.Rename(record, "H-1", "H-2", projectFile);

        Assert.False(result.Success);
        Assert.Contains("Verkn", result.ErrorMessage, StringComparison.OrdinalIgnoreCase);
        Assert.Equal("Testoriginal", File.ReadAllText(original));
        Assert.Equal("Testoriginal", File.ReadAllText(photo));
        Assert.Equal("Haltungen_Verteilt/H-1/H-1.pdf", record.GetFieldValue(FieldKeys.PdfPath));
    }

    [Fact]
    public void Rename_GesperrtesFoto_NimmtHaltungsumbenennungSicherZurueck()
    {
        var projectRoot = Path.Combine(_root, "Projekt");
        var projectFile = Write(Path.Combine(projectRoot, "Projektdateien", "projekt.json"), "{}");
        var original = Write(Path.Combine(projectRoot, "Haltungen_Verteilt", "H-1", "H-1.pdf"));
        var photo = Write(Path.Combine(projectRoot, "Fotos", "Haltungen", "H-1", "H-1.jpg"));
        var record = Record("Haltungen_Verteilt/H-1/H-1.pdf");

        HoldingRenameService.HoldingRenameResult result;
        using (File.Open(photo, FileMode.Open, FileAccess.Read, FileShare.None))
            result = HoldingRenameService.Rename(record, "H-1", "H-2", projectFile);

        Assert.False(result.Success);
        Assert.DoesNotContain("Rollback", result.ErrorMessage, StringComparison.OrdinalIgnoreCase);
        Assert.Equal("Testoriginal", File.ReadAllText(original));
        Assert.Equal("Testoriginal", File.ReadAllText(photo));
        Assert.Equal("Haltungen_Verteilt/H-1/H-1.pdf", record.GetFieldValue(FieldKeys.PdfPath));
        Assert.False(Directory.Exists(Path.Combine(projectRoot, "Haltungen_Verteilt", "H-2")));
    }

    private void CheckLinkedPath(string relativeLink, bool linkedParent = false)
    {
        var physicalRoot = Path.Combine(_root, "Kundenquelle");
        var link = Path.Combine(_root, "Projekt", relativeLink.Replace('/', Path.DirectorySeparatorChar));
        var physicalProject = physicalRoot;
        var projectRoot = Path.Combine(_root, "Projekt");
        string physicalPdf;
        string physicalPhoto;
        if (linkedParent)
        {
            link = Path.Combine(_root, "VerknuepfterElternordner");
            projectRoot = Path.Combine(link, "Projekt");
            physicalProject = Path.Combine(physicalRoot, "Projekt");
        }

        if (relativeLink.Length == 0)
        {
            physicalPdf = Write(Path.Combine(physicalProject, "Haltungen_Verteilt", "H-1", "H-1.pdf"));
            physicalPhoto = Write(Path.Combine(physicalProject, "Fotos", "Haltungen", "H-1", "H-1.jpg"));
            Write(Path.Combine(physicalProject, "Projektdateien", "projekt.json"), "{}");
        }
        else
        {
            var isPhoto = relativeLink.StartsWith("Fotos/", StringComparison.Ordinal);
            var linkedFile = Write(Path.Combine(physicalRoot, relativeLink.EndsWith("H-1", StringComparison.Ordinal) ? "" : "H-1", isPhoto ? "H-1.jpg" : "H-1.pdf"));
            physicalPdf = isPhoto ? Write(Path.Combine(projectRoot, "Haltungen_Verteilt", "H-1", "H-1.pdf")) : linkedFile;
            physicalPhoto = isPhoto ? linkedFile : Write(Path.Combine(projectRoot, "Fotos", "Haltungen", "H-1", "H-1.jpg"));
            Write(Path.Combine(projectRoot, "Projektdateien", "projekt.json"), "{}");
        }

        Directory.CreateDirectory(Path.GetDirectoryName(link)!);
        JunctionTestSupport.CreateDirectoryLink(link, physicalRoot);
        var record = Record("Haltungen_Verteilt/H-1/H-1.pdf");

        var result = HoldingRenameService.Rename(record, "H-1", "H-2", Path.Combine(projectRoot, "Projektdateien", "projekt.json"));

        Assert.False(result.Success);
        Assert.Contains("Verkn", result.ErrorMessage, StringComparison.OrdinalIgnoreCase);
        Assert.Equal("Testoriginal", File.ReadAllText(physicalPdf));
        Assert.Equal("Testoriginal", File.ReadAllText(physicalPhoto));
        Assert.Equal("Haltungen_Verteilt/H-1/H-1.pdf", record.GetFieldValue(FieldKeys.PdfPath));
        Assert.False(Directory.Exists(Path.Combine(projectRoot, "Haltungen_Verteilt", "H-2")));
        Assert.False(Directory.Exists(Path.Combine(projectRoot, "Fotos", "Haltungen", "H-2")));
    }

    private static HaltungRecord Record(string pdf)
    {
        var record = new HaltungRecord();
        record.SetFieldValue(FieldKeys.PdfPath, pdf, FieldSource.Manual, userEdited: false);
        return record;
    }

    private static string Write(string path, string text = "Testoriginal")
    {
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        File.WriteAllText(path, text);
        return path;
    }

    public void Dispose()
    {
        if (Directory.Exists(_root))
            Directory.Delete(_root, recursive: true);
    }
}
