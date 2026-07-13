using AuswertungPro.Next.Application.Common;
using AuswertungPro.Next.Application.Import;
using AuswertungPro.Next.Domain.Models;
using AuswertungPro.Next.Infrastructure.Import;
using AuswertungPro.Next.Infrastructure.Import.Protocols;

namespace AuswertungPro.Next.Infrastructure.Tests;

public sealed class SchachtProtocolImportServiceTests
{
    [Fact]
    public void ParseFromText_MitSchachtprotokoll_LiefertFelder()
    {
        var text = string.Join("\n", new[]
        {
            "Schachtprotokoll   Nr. 74467",
            "Schachttyp Kontrollschacht",
            "Schachtform Oval",
            "Dimension 1000 x 800 mm",
            "Schachttiefe 2,35 m",
            "Datum 02/10/2025"
        });

        var result = SchachtProtocolImportService.ParseFromText(text);

        Assert.True(result.IstSchachtprotokoll);
        Assert.Equal("74467", result.Schachtnummer);
        Assert.Equal("Kontrollschacht", result.Funktion);
        Assert.Equal("Oval", result.Schachtform);
        Assert.Equal("1000 x 800 mm", result.Dimension);
        Assert.Equal("2.35", result.Schachttiefe);
    }

    [Fact]
    public void ParseFromText_OhneSchachtprotokoll_IstFalse()
    {
        var result = SchachtProtocolImportService.ParseFromText("Irgendein Haltungsprotokoll Text");

        Assert.False(result.IstSchachtprotokoll);
        Assert.Null(result.Schachtnummer);
        Assert.Empty(result.Schaeden);
    }

    [Fact]
    public void FindSchacht_FindetPerSchachtnummer()
    {
        var project = new Project();
        var schacht = new SchachtRecord();
        schacht.SetFieldValue("Schachtnummer", "74467");
        project.SchaechteData.Add(schacht);
        var svc = new SchachtProtocolImportService();

        var found = svc.FindSchacht(project, "74467");

        Assert.Same(schacht, found);
    }

    [Fact]
    public void FindSchacht_NullWennNichtVorhanden()
    {
        var svc = new SchachtProtocolImportService();

        Assert.Null(svc.FindSchacht(new Project(), "99999"));
    }

    [Fact]
    public void Apply_BautRecordNeuAuf()
    {
        var ergebnis = new SchachtProtocolParseResult(
            true, "74467", "02.10.2025", "Kontrollschacht",
            "Rund", "1000 mm", "2.35", null, null, "offen", null,
            new[] { ("Schachtdeckel", "gerissen") });
        var schacht = new SchachtRecord();
        var svc = new SchachtProtocolImportService();

        svc.Apply(schacht, ergebnis, "Schaechte_Verteilt/74467/quelle.pdf");

        Assert.Equal("74467", schacht.GetFieldValue("Schachtnummer"));
        Assert.Equal("Kontrollschacht", schacht.GetFieldValue("Funktion"));
        Assert.Equal("Rund", schacht.GetFieldValue("Schachtform"));
        Assert.Equal("1000 mm", schacht.GetFieldValue("Dimension"));
        Assert.Equal("2.35", schacht.GetFieldValue("Schachttiefe"));
        Assert.Equal("Schaechte_Verteilt/74467/quelle.pdf", schacht.GetFieldValue("PDF_Path"));
        Assert.NotNull(schacht.Protocol);
        Assert.Single(schacht.Protocol!.Original.Entries);
        Assert.Equal("Schachtdeckel", schacht.Protocol!.Original.Entries[0].Code);
    }

    [Fact]
    public void DistributePdf_KopiertUndGibtRelativenPfad()
    {
        var root = Path.Combine(Path.GetTempPath(), "sst_" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        try
        {
            var src = Path.Combine(root, "quelle.pdf");
            File.WriteAllText(src, "%PDF-1.4 dummy");
            var svc = new SchachtProtocolImportService();

            var rel = svc.DistributePdf(root, "74467", src);

            var expected = Path.Combine(ProjectStructure.SchachtVerteiltDir(root, "74467"), "quelle.pdf");
            Assert.True(File.Exists(expected));
            Assert.Equal(ProjectPathResolver.MakeRelative(expected, root), rel);
            Assert.Contains("74467", rel);
            Assert.Contains("quelle.pdf", rel);
        }
        finally
        {
            try { Directory.Delete(root, recursive: true); } catch { /* Best effort. */ }
        }
    }
}
