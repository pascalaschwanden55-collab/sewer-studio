using AuswertungPro.Next.Application.Import;
using AuswertungPro.Next.Infrastructure.Import.Protocols;

namespace AuswertungPro.Next.Infrastructure.Tests;

public sealed class SchachtProtocolFileLocatorTests
{
    [Fact]
    public void Locate_Nimmt_Den_Gespeicherten_Relativen_Pfad()
    {
        using var temp = new TempShaftProject();
        var pdf = temp.CreatePdf("Schächte_Verteilt", "80638", "20250924_80638.pdf");

        var treffer = new SchachtProtocolFileLocator().Locate(
            temp.Root,
            "Schächte_Verteilt/80638/20250924_80638.pdf",
            null,
            "80638");

        Assert.NotNull(treffer);
        Assert.Equal(Path.GetFullPath(pdf), treffer!.PdfPfad);
        Assert.Equal(SchachtProtocolFileOrigin.Verknuepfung, treffer.Herkunft);
    }

    [Fact]
    public void Locate_Nimmt_Auch_Einen_Absoluten_Gespeicherten_Pfad()
    {
        using var temp = new TempShaftProject();
        var pdf = temp.CreatePdf("Schächte_Verteilt", "80638", "20250924_80638.pdf");

        var treffer = new SchachtProtocolFileLocator().Locate(temp.Root, pdf, null, "80638");

        Assert.NotNull(treffer);
        Assert.Equal(Path.GetFullPath(pdf), treffer!.PdfPfad);
        Assert.Equal(SchachtProtocolFileOrigin.Verknuepfung, treffer.Herkunft);
    }

    [Fact]
    public void Locate_Findet_Umbenannte_Datei_Im_Ordner_Dieses_Schachts()
    {
        using var temp = new TempShaftProject();
        var pdf = temp.CreatePdf("Schächte_Verteilt", "80638", "20250924_80638.pdf");
        var veralteterPfad = Path.Combine(temp.Root, "Schächte_Verteilt", "80638", "20250924_80638_01.pdf");

        var treffer = new SchachtProtocolFileLocator().Locate(temp.Root, veralteterPfad, null, "80638");

        Assert.NotNull(treffer);
        Assert.Equal(Path.GetFullPath(pdf), treffer!.PdfPfad);
        Assert.Equal(SchachtProtocolFileOrigin.Schachtordner, treffer.Herkunft);
    }

    [Fact]
    public void Locate_Findet_Pdf_Im_Unterordner_Des_Schachtordners()
    {
        using var temp = new TempShaftProject();
        var pdf = temp.CreatePdf(Path.Combine("Schächte_Verteilt", "80409", "PDF"), "20250924_80409.pdf");

        var treffer = new SchachtProtocolFileLocator().Locate(temp.Root, "fehlt/80409.pdf", null, "80409");

        Assert.NotNull(treffer);
        Assert.Equal(Path.GetFullPath(pdf), treffer!.PdfPfad);
        Assert.Equal(SchachtProtocolFileOrigin.Schachtordner, treffer.Herkunft);
    }

    [Fact]
    public void Locate_Bevorzugt_Die_Datei_Mit_Der_Schachtnummer_Im_Namen()
    {
        using var temp = new TempShaftProject();
        temp.CreatePdf("Schächte_Verteilt", "80551", "beilage.pdf");
        var richtig = temp.CreatePdf("Schächte_Verteilt", "80551", "20250924_80551.pdf");

        var treffer = new SchachtProtocolFileLocator().Locate(temp.Root, "fehlt.pdf", null, "80551");

        Assert.Equal(Path.GetFullPath(richtig), treffer!.PdfPfad);
    }

    [Fact]
    public void Locate_Sucht_Niemals_Im_Ordner_Eines_Fremden_Schachts()
    {
        using var temp = new TempShaftProject();
        temp.CreatePdf("Schächte_Verteilt", "80631", "20250924_80631.pdf");

        var treffer = new SchachtProtocolFileLocator().Locate(temp.Root, "fehlt/80638.pdf", null, "80638");

        Assert.Null(treffer);
    }

    [Fact]
    public void Locate_Ohne_Schachtnummer_Keine_Ordnersuche()
    {
        using var temp = new TempShaftProject();
        temp.CreatePdf("Schächte_Verteilt", "80638", "20250924_80638.pdf");

        var treffer = new SchachtProtocolFileLocator().Locate(temp.Root, "fehlt.pdf", null, "  ");

        Assert.Null(treffer);
    }

    [Fact]
    public void Locate_Verwendet_Das_Link_Feld_Nur_Fuer_Pdf_Dateien()
    {
        using var temp = new TempShaftProject();
        var pdf = temp.CreatePdf("Schächte_Verteilt", "74466", "74466.pdf");
        var video = temp.CreateFile("Videos", "74466.mp4");

        Assert.Null(new SchachtProtocolFileLocator().Locate(temp.Root, null, video, null));

        var treffer = new SchachtProtocolFileLocator().Locate(temp.Root, null, pdf, null);
        Assert.Equal(Path.GetFullPath(pdf), treffer!.PdfPfad);
        Assert.Equal(SchachtProtocolFileOrigin.Verknuepfung, treffer.Herkunft);
    }

    [Fact]
    public void Locate_Akzeptiert_Eine_Vorhandene_Quelle_Ausserhalb_Des_Projekts()
    {
        using var temp = new TempShaftProject();
        using var extern1 = new TempShaftProject();
        var pdf = extern1.CreatePdf("Kundenordner", "80638", "80638.pdf");

        var treffer = new SchachtProtocolFileLocator().Locate(temp.Root, pdf, null, "80638");

        Assert.Equal(Path.GetFullPath(pdf), treffer!.PdfPfad);
        Assert.Equal(SchachtProtocolFileOrigin.Verknuepfung, treffer.Herkunft);
    }

    [Fact]
    public void Locate_Findet_Nichts_Wenn_Weder_Verknuepfung_Noch_Ordner_Existieren()
    {
        using var temp = new TempShaftProject();

        Assert.Null(new SchachtProtocolFileLocator().Locate(temp.Root, "fehlt.pdf", "auch-weg.pdf", "80638"));
    }

    private sealed class TempShaftProject : IDisposable
    {
        public TempShaftProject()
        {
            Root = Path.Combine(
                Path.GetTempPath(),
                "SchachtProtocolFileLocatorTests",
                Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(Root);
        }

        public string Root { get; }

        public string CreatePdf(string baseFolder, string number, string fileName)
            => CreatePdf(Path.Combine(baseFolder, number), fileName);

        public string CreatePdf(string relativeFolder, string fileName)
            => CreateFile(relativeFolder, fileName);

        public string CreateFile(string relativeFolder, string fileName)
        {
            var directory = Path.Combine(Root, relativeFolder);
            Directory.CreateDirectory(directory);
            var path = Path.Combine(directory, fileName);
            File.WriteAllBytes(path, "%PDF-test"u8.ToArray());
            return path;
        }

        public void Dispose()
        {
            try
            {
                Directory.Delete(Root, recursive: true);
            }
            catch
            {
                // Testaufraeumen darf das Testergebnis nicht verdecken.
            }
        }
    }
}
