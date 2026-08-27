using System.Text;
using AuswertungPro.Next.Application.Dossiers.Preview;
using AuswertungPro.Next.Infrastructure.Dossiers;

namespace AuswertungPro.Next.Infrastructure.Tests.Dossiers;

/// <summary>
/// Liest die benannten Ziele aus der erzeugten PDF. Sie stammen aus den
/// Word-Textmarken und sagen exakt, welches Feld an welcher Stelle steht.
///
/// Die PDF kommt immer aus dem eigenen Wandler, ist also kein fremdes Format.
/// Trotzdem gilt fail-closed: Was nicht sicher gelesen werden kann, wird
/// weggelassen. Fehlen alle Ziele, greift der bisherige Weg ueber den Text.
/// </summary>
public sealed class DossierPdfFieldAnchorReaderTests
{
    private static readonly string Marke =
        DossierPdfFieldMarker.Name(DossierPreviewTarget.RowCell("Themen", 0, "Text"));

    [Fact]
    public void Ein_Ziel_wird_mit_Seite_und_Position_gelesen()
    {
        var pdf = PdfMit($"<</{Marke}[15 0 R/XYZ 226.9 402.139 0]>>", seitenObjekte: [15]);

        var anker = Assert.Single(DossierPdfFieldAnchorReader.Read(pdf));

        Assert.Equal(Marke, anker.MarkerName);
        Assert.Equal(1, anker.PageNumber);
        Assert.Equal(226.9, anker.X, 3);
        Assert.Equal(402.139, anker.Y, 3);
    }

    [Fact]
    public void Mehrere_Ziele_auf_mehreren_Seiten_werden_richtig_zugeordnet()
    {
        var zweite = DossierPdfFieldMarker.Name(DossierPreviewTarget.RowCell("Themen", 1, "Text"));
        var pdf = PdfMit(
            $"<</{Marke}[15 0 R/XYZ 100 700 0]\n/{zweite}[21 0 R/XYZ 110 690 0]>>",
            seitenObjekte: [15, 21]);

        var anker = DossierPdfFieldAnchorReader.Read(pdf);

        Assert.Equal(2, anker.Count);
        Assert.Equal(1, anker.Single(a => a.MarkerName == Marke).PageNumber);
        Assert.Equal(2, anker.Single(a => a.MarkerName == zweite).PageNumber);
    }

    [Fact]
    public void Fremde_Ziele_der_Vorlage_werden_uebergangen()
    {
        // Word legt eigene Marken an. Sie duerfen nie als Feldziel gelten.
        var pdf = PdfMit(
            $"<</5FToc5177405[15 0 R/XYZ 99 806 0]\n/OLE5FLINK1[15 0 R/XYZ 70 813 0]\n/{Marke}[15 0 R/XYZ 226 402 0]>>",
            seitenObjekte: [15]);

        var anker = Assert.Single(DossierPdfFieldAnchorReader.Read(pdf));

        Assert.Equal(Marke, anker.MarkerName);
    }

    [Fact]
    public void Ein_Ziel_auf_einer_unbekannten_Seite_wird_verworfen()
    {
        // Lieber kein Ziel als eines auf der falschen Seite.
        var pdf = PdfMit($"<</{Marke}[99 0 R/XYZ 100 700 0]>>", seitenObjekte: [15]);

        Assert.Empty(DossierPdfFieldAnchorReader.Read(pdf));
    }

    [Fact]
    public void Eine_PDF_ohne_Ziele_liefert_nichts_und_wirft_nicht()
    {
        var pdf = Encoding.Latin1.GetBytes("%PDF-1.6\n1 0 obj\n<</Type/Page>>\nendobj\n%%EOF");

        Assert.Empty(DossierPdfFieldAnchorReader.Read(pdf));
    }

    [Theory]
    [InlineData("")]
    [InlineData("nicht wirklich eine PDF")]
    public void Unlesbare_Eingaben_liefern_nichts(string inhalt)
        => Assert.Empty(DossierPdfFieldAnchorReader.Read(Encoding.Latin1.GetBytes(inhalt)));

    [Fact]
    public void Null_liefert_nichts()
        => Assert.Empty(DossierPdfFieldAnchorReader.Read(null));

    [Fact]
    public void Ein_Ziel_ohne_Koordinaten_wird_verworfen()
    {
        // /Fit hat keine Position - daraus laesst sich keine Zelle bestimmen.
        var pdf = PdfMit($"<</{Marke}[15 0 R/Fit]>>", seitenObjekte: [15]);

        Assert.Empty(DossierPdfFieldAnchorReader.Read(pdf));
    }

    /// <summary>
    /// Baut eine kleine, aber echte PDF-Struktur: Seitenobjekte in der
    /// Reihenfolge des Seitenbaums und ein /Dests-Objekt im Katalog.
    /// </summary>
    private static byte[] PdfMit(string destsInhalt, int[] seitenObjekte)
    {
        var builder = new StringBuilder();
        builder.Append("%PDF-1.6\n");

        foreach (var nummer in seitenObjekte)
            builder.Append($"{nummer} 0 obj\n<</Type/Page/MediaBox[0 0 612 792]>>\nendobj\n");

        var kinder = string.Join(" ", seitenObjekte.Select(nummer => $"{nummer} 0 R"));
        builder.Append($"7 0 obj\n<</Type/Pages/Kids[{kinder}]/Count {seitenObjekte.Length}>>\nendobj\n");
        builder.Append($"5 0 obj\n{destsInhalt}\nendobj\n");
        builder.Append("6 0 obj\n<</Type/Catalog/Pages 7 0 R\n/Dests 5 0 R>>\nendobj\n");
        builder.Append("trailer\n<</Size 8/Root 6 0 R>>\n%%EOF");

        return Encoding.Latin1.GetBytes(builder.ToString());
    }
}
