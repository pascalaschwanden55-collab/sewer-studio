using AuswertungPro.Next.Application.Xtf;
using AuswertungPro.Next.Domain.Models;
using AuswertungPro.Next.Infrastructure.Import.Xtf;

namespace AuswertungPro.Next.Infrastructure.Tests;

public sealed class XtfRevisionExportServiceSourceSelectionTests
{
    [Fact]
    public void Explizite_Quelle_wird_ohne_Projektkopie_lesend_und_nur_einmal_verwendet()
    {
        using var temp = new TempDirectory();
        var quelle = temp.CreateFile("Extern/quelle.xtf", "<?xml version=\"1.0\"?><TRANSFER />");
        var vorher = File.ReadAllBytes(quelle);
        var doppelterPfad = Path.Combine(Path.GetDirectoryName(quelle)!, ".", Path.GetFileName(quelle));

        var result = new XtfRevisionExportService().Erzeuge(
            new XtfRevisionExportRequest(
                new Project(),
                Path.Combine(temp.Path, "Projekt", "Projektdateien", "projekt.json"),
                Path.Combine(temp.Path, "Ausgabe"),
                NurPruefen: true,
                Quelldateien: [quelle, doppelterPfad]));

        Assert.True(result.Ok, result.Fehler);
        Assert.False(result.QuelleFehlt);
        Assert.Equal(1, Zaehle(result.Bericht, "quelle.xtf:"));
        Assert.Equal(vorher, File.ReadAllBytes(quelle));
        Assert.False(Directory.Exists(Path.Combine(temp.Path, "Ausgabe")));
    }

    [Theory]
    [InlineData("Extern/fehlt.xtf", false, "nicht gefunden")]
    [InlineData("Extern/quelle.txt", true, ".xtf")]
    public void Ungueltige_explizite_Quelle_stoppt_vor_dem_Export(
        string relativePath,
        bool createFile,
        string expectedMessage)
    {
        using var temp = new TempDirectory();
        var quelle = Path.Combine(temp.Path, relativePath.Replace('/', Path.DirectorySeparatorChar));
        if (createFile)
            temp.CreateFile(relativePath, "<?xml version=\"1.0\"?><TRANSFER />");

        var result = new XtfRevisionExportService().Erzeuge(
            new XtfRevisionExportRequest(
                new Project(),
                Path.Combine(temp.Path, "Projekt", "Projektdateien", "projekt.json"),
                Path.Combine(temp.Path, "Ausgabe"),
                NurPruefen: true,
                Quelldateien: [quelle]));

        Assert.False(result.Ok);
        Assert.False(result.QuelleFehlt);
        Assert.Contains(expectedMessage, result.Fehler, StringComparison.OrdinalIgnoreCase);
        Assert.False(Directory.Exists(Path.Combine(temp.Path, "Ausgabe")));
    }

    [Fact]
    public void Leere_explizite_Liste_faellt_auf_die_Projektkopie_zurueck()
    {
        using var temp = new TempDirectory();
        var projektPfad = Path.Combine(temp.Path, "Projekt", "Projektdateien", "projekt.json");
        temp.CreateFile("Projekt/Imports/XTF/projektquelle.xtf", "<?xml version=\"1.0\"?><TRANSFER />");

        var result = new XtfRevisionExportService().Erzeuge(
            new XtfRevisionExportRequest(
                new Project(),
                projektPfad,
                Path.Combine(temp.Path, "Ausgabe"),
                NurPruefen: true,
                Quelldateien: []));

        Assert.True(result.Ok, result.Fehler);
        Assert.Contains("projektquelle.xtf:", result.Bericht, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Bytegleiche_Projektquellen_mit_gleichem_Namen_werden_nur_einmal_gelesen()
    {
        using var temp = new TempDirectory();
        const string xml = "<?xml version=\"1.0\"?><TRANSFER />";
        var projektPfad = Path.Combine(temp.Path, "Projekt", "Projektdateien", "projekt.json");
        temp.CreateFile("Projekt/Imports/XTF/kataster.xtf", xml);
        temp.CreateFile("Projekt/Importdateien/XTF/kataster.xtf", xml);

        var result = new XtfRevisionExportService().Erzeuge(
            new XtfRevisionExportRequest(
                new Project(),
                projektPfad,
                Path.Combine(temp.Path, "Ausgabe"),
                NurPruefen: true));

        Assert.True(result.Ok, result.Fehler);
        Assert.Equal(1, Zaehle(result.Bericht, "kataster.xtf:"));
    }

    [Fact]
    public void Unterschiedliche_Projektquellen_mit_gleichem_Namen_stoppen_fail_closed()
    {
        using var temp = new TempDirectory();
        var projektPfad = Path.Combine(temp.Path, "Projekt", "Projektdateien", "projekt.json");
        var modern = temp.CreateFile(
            "Projekt/Imports/XTF/kataster.xtf",
            "<?xml version=\"1.0\"?><TRANSFER />");
        var legacy = temp.CreateFile(
            "Projekt/Importdateien/XTF/kataster.xtf",
            "<?xml version=\"1.0\"?><ANDERE />");

        var result = new XtfRevisionExportService().Erzeuge(
            new XtfRevisionExportRequest(
                new Project(),
                projektPfad,
                Path.Combine(temp.Path, "Ausgabe"),
                NurPruefen: true));

        Assert.False(result.Ok);
        Assert.False(result.QuelleFehlt);
        Assert.Contains("gleichen Namen", result.Fehler, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("unterschiedlichen Inhalt", result.Fehler, StringComparison.OrdinalIgnoreCase);
        Assert.Contains(modern, result.Fehler, StringComparison.OrdinalIgnoreCase);
        Assert.Contains(legacy, result.Fehler, StringComparison.OrdinalIgnoreCase);
        Assert.False(Directory.Exists(Path.Combine(temp.Path, "Ausgabe")));
    }

    private static int Zaehle(string text, string gesucht)
    {
        var count = 0;
        var position = 0;
        while ((position = text.IndexOf(gesucht, position, StringComparison.OrdinalIgnoreCase)) >= 0)
        {
            count++;
            position += gesucht.Length;
        }

        return count;
    }

    private sealed class TempDirectory : IDisposable
    {
        public string Path { get; } = System.IO.Path.Combine(
            System.IO.Path.GetTempPath(),
            "XtfRevisionSources_" + Guid.NewGuid().ToString("N"));

        public TempDirectory() => Directory.CreateDirectory(Path);

        public string CreateFile(string relativePath, string content)
        {
            var path = System.IO.Path.Combine(
                Path,
                relativePath.Replace('/', System.IO.Path.DirectorySeparatorChar));
            Directory.CreateDirectory(System.IO.Path.GetDirectoryName(path)!);
            File.WriteAllText(path, content);
            return path;
        }

        public void Dispose()
        {
            try
            {
                Directory.Delete(Path, recursive: true);
            }
            catch
            {
                // Test-Aufraeumen darf das Ergebnis nicht verdecken.
            }
        }
    }
}
