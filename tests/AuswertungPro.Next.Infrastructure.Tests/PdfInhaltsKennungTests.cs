using System.Text;
using UglyToad.PdfPig;
using UglyToad.PdfPig.Content;
using UglyToad.PdfPig.Core;
using UglyToad.PdfPig.Fonts.Standard14Fonts;
using UglyToad.PdfPig.Writer;

namespace AuswertungPro.Next.Infrastructure.Tests;

public sealed class PdfInhaltsKennungTests : IDisposable
{
    private readonly string _dir = Directory.CreateTempSubdirectory("pdfkennung_").FullName;

    public void Dispose() => Directory.Delete(_dir, recursive: true);

    [Fact]
    public void Gleiche_Seiten_ergeben_dieselbe_Datei_andere_Seiten_eine_andere()
    {
        var quelle = Quelle();

        var a = Auszug(quelle, 2, "a.pdf");
        var b = Auszug(quelle, 2, "b.pdf");
        var c = Auszug(quelle, 1, "c.pdf");

        Assert.Equal(File.ReadAllBytes(a), File.ReadAllBytes(b));
        Assert.NotEqual(File.ReadAllBytes(a), File.ReadAllBytes(c));
        using var gelesen = PdfDocument.Open(a);
        Assert.Equal(1, gelesen.NumberOfPages);
        Assert.Contains("Seite 2", gelesen.GetPage(1).Text);
    }

    [Fact]
    public void Verschiedene_Trailerkennungen_werden_ohne_Wartezeit_vereinheitlicht()
    {
        // Die Kennungen unterscheiden sich garantiert, unabhaengig von Uhr und PDF-Baukasten.
        var a = Encoding.ASCII.GetBytes("%PDF-1.4 Inhalt /ID [<11111111111111111111111111111111><22222222222222222222222222222222>]");
        var b = Encoding.ASCII.GetBytes("%PDF-1.4 Inhalt /ID [<AAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAA><BBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBB>]");
        var originalA = a.ToArray();
        var originalB = b.ToArray();

        var normalisiertA = AuswertungPro.Next.Infrastructure.HoldingDistribution.PdfInhaltsKennung.Festlegen(a);
        var normalisiertB = AuswertungPro.Next.Infrastructure.HoldingDistribution.PdfInhaltsKennung.Festlegen(b);

        Assert.NotEqual(a, b);
        Assert.Equal(normalisiertA, normalisiertB);
        Assert.NotEqual(originalA, normalisiertA);
        Assert.Equal(a.Length, normalisiertA.Length);
        Assert.Equal(originalA, a);
        Assert.Equal(originalB, b);
    }

    [Fact]
    public void Ohne_Kennung_bleibt_die_Datei_unveraendert()
    {
        var bytes = "%PDF-1.4 kein Trailer"u8.ToArray();
        Assert.Same(bytes, AuswertungPro.Next.Infrastructure.HoldingDistribution.PdfInhaltsKennung.Festlegen(bytes));
    }

    private string Auszug(string quelle, int seite, string name)
    {
        var ziel = Path.Combine(_dir, name);
        HoldingFolderDistributor.WritePdfPages(quelle, [seite], ziel);
        return ziel;
    }

    private string Quelle()
    {
        var pfad = Path.Combine(_dir, "quelle.pdf");
        using var builder = new PdfDocumentBuilder();
        var font = builder.AddStandard14Font(Standard14Font.Helvetica);
        builder.AddPage(PageSize.A4).AddText("Seite 1", 12, new PdfPoint(40, 700), font);
        builder.AddPage(PageSize.A4).AddText("Seite 2", 12, new PdfPoint(40, 700), font);
        File.WriteAllBytes(pfad, builder.Build());
        return pfad;
    }
}
