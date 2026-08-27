using AuswertungPro.Next.Application.Dossiers.Preview;

namespace AuswertungPro.Next.Infrastructure.Tests.Dossiers;

/// <summary>
/// Die Vorschau erkennt ein Feld heute an seinem TEXT. Tragen mehrere Felder
/// denselben Text, bleibt die Stelle bewusst ohne Treffer - sonst wuerde geraten.
/// Seit fehlende Angaben als „unbekannt" erscheinen, betrifft das viele Zellen.
///
/// Deshalb bekommt jede fuellbare Stelle eine unsichtbare Word-Textmarke, die als
/// benanntes Ziel in der PDF landet. Gemessen: LibreOffice schreibt sie mit
/// Seitennummer und exakter Position - auch fuer eine voellig leere Zelle.
///
/// Der Name muss deshalb zwei Bedingungen erfuellen:
/// nur Buchstaben und Ziffern (aus „SSFELD_Beilagen" machte LibreOffice
/// „SSFELD5FBeilagen"), und aus derselben Adresse immer derselbe Name.
/// </summary>
public sealed class DossierPdfFieldMarkerTests
{
    [Fact]
    public void Ein_Name_enthaelt_nur_Buchstaben_und_Ziffern()
    {
        var name = DossierPdfFieldMarker.Name(
            DossierPreviewTarget.RowCell("Eigentuemer", 0, "Eigentuemer_Zelle"));

        Assert.Matches("^[A-Za-z0-9]+$", name);
    }

    [Fact]
    public void Dieselbe_Adresse_ergibt_immer_denselben_stabilen_Namen()
    {
        var a = DossierPdfFieldMarker.Name(DossierPreviewTarget.RowCell("Themen", 8, "Text"));
        var b = DossierPdfFieldMarker.Name(DossierPreviewTarget.RowCell("Themen", 8, "Text"));

        Assert.Equal(a, b);
        Assert.Equal("SSFELD825D6F305C1D27E3948F1BC3DD098F4351", a);
    }

    [Theory]
    [InlineData("Themen", 0, "Text", "Themen", 1, "Text")]
    [InlineData("Themen", 0, "Text", "Themen", 0, "Thema")]
    [InlineData("Themen", 0, "Text", "Eigentuemer", 0, "Text")]
    public void Verschiedene_Adressen_ergeben_verschiedene_Namen(
        string keyA, int rowA, string cellA,
        string keyB, int rowB, string cellB)
    {
        var a = DossierPdfFieldMarker.Name(DossierPreviewTarget.RowCell(keyA, rowA, cellA));
        var b = DossierPdfFieldMarker.Name(DossierPreviewTarget.RowCell(keyB, rowB, cellB));

        Assert.NotEqual(a, b);
    }

    [Fact]
    public void Ein_freies_Feld_und_eine_Tabellenzelle_kollidieren_nicht()
    {
        var feld = DossierPdfFieldMarker.Name(DossierPreviewTarget.Field("Text"));
        var zelle = DossierPdfFieldMarker.Name(DossierPreviewTarget.RowCell("Themen", 0, "Text"));

        Assert.NotEqual(feld, zelle);
    }

    [Fact]
    public void Der_Name_bleibt_innerhalb_der_Word_Grenze_von_40_Zeichen()
    {
        // Word verwirft oder kuerzt laengere Textmarkennamen. Dann findet die
        // Vorschau den aus der Feldadresse neu gebauten Namen nicht mehr wieder.
        var name = DossierPdfFieldMarker.Name(
            DossierPreviewTarget.RowCell("Eigentuemer", 99, "Eigentuemer_Zelle"));

        Assert.Equal(40, name.Length);
    }

    [Fact]
    public void Ein_fremder_Name_wird_nicht_als_eigene_Marke_ausgegeben()
    {
        // Word legt eigene Marken an (_Toc..., _GoBack, OLE_LINK1). Die duerfen
        // niemals als Feldziel gedeutet werden.
        Assert.False(DossierPdfFieldMarker.IsMarker("5FToc5177405"));
        Assert.False(DossierPdfFieldMarker.IsMarker("5FGoBack"));
        Assert.False(DossierPdfFieldMarker.IsMarker("OLE5FLINK1"));
    }

    [Fact]
    public void Eine_eigene_Marke_wird_erkannt()
    {
        var name = DossierPdfFieldMarker.Name(DossierPreviewTarget.RowCell("Themen", 3, "Text"));

        Assert.True(DossierPdfFieldMarker.IsMarker(name));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void Ein_leerer_Name_ist_keine_Marke(string? name)
    {
        Assert.False(DossierPdfFieldMarker.IsMarker(name));
    }
}
