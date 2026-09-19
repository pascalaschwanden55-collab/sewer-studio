using System.Text.RegularExpressions;
using AuswertungPro.Next.Infrastructure.Import.Pdf;

namespace AuswertungPro.Next.Infrastructure.Tests;

/// <summary>
/// Der SchachtPro-Export (GKS Cahenzli, Goeschenen 2026) traegt eine eigene Anschlusstabelle
/// mit Kennung, Uhrzeit, Tiefe, Durchmesser, Typ, Medium, Material und Zustand. pdftotext
/// schiebt die Zellen «150 mm Auslauf» um eine Zeile nach unten (Schacht 8705: die erste
/// Durchmesserzelle steht in der Zeile von E1). Vorlage: echter Text von Seite 1, Name des
/// Ausfuehrenden anonymisiert.
/// </summary>
public sealed class SchachtProtocolZusatzParserSchachtProTests
{
    private static string Vorlage8705()
        => File.ReadAllText(Path.Combine(
            TestRepoPaths.RepoRoot(), "tests", "Fixtures", "Schachtprotokolle", "8705_schachtpro_seite1_layout.txt"));

    private static string Vorlage80409()
        => File.ReadAllText(Path.Combine(
            TestRepoPaths.RepoRoot(), "tests", "Fixtures", "Schachtprotokolle", "80409_seite1_layout.txt"));

    [Fact]
    public void Der_SchachtPro_Export_wird_erkannt_das_Uri_Formular_nicht()
    {
        Assert.True(SchachtProtocolZusatzParser.IstSchachtPro(SchachtProtocolParser.NormalizePdfText(Vorlage8705())));
        Assert.False(SchachtProtocolZusatzParser.IstSchachtPro(SchachtProtocolParser.NormalizePdfText(Vorlage80409())));
        Assert.True(SchachtProtocolZusatzParser.IstSchachtPro("Ansc... Uhrzeit  Tiefe  Durchmesser Typ\nA1 12 1.28"));
    }

    [Fact]
    public void Liest_Kennung_Uhrzeit_Tiefe_Durchmesser_und_Material_je_Anschluss()
    {
        var zusatz = SchachtProtocolZusatzParser.Parse(Vorlage8705());

        Assert.Equal(4, zusatz.Anschluesse.Count);
        Assert.Equal(new[] { 1, 2, 3, 4 }, zusatz.Anschluesse.Select(a => a.Nr).ToArray());
        Assert.Equal(new[] { "Auslauf", "Einlauf", "Einlauf", "Einlauf" }, zusatz.Anschluesse.Select(a => a.Art).ToArray());
        Assert.Equal(new[] { "12", "4", "6", "7" }, zusatz.Anschluesse.Select(a => a.Uhr).ToArray());
        Assert.Equal(new decimal[] { 1.28m, 0.67m, 1.20m, 1.22m }, zusatz.Anschluesse.Select(a => a.TiefeM!.Value).ToArray());
        // Durchmesser aus der Skizzenlegende «A1 DN150 … E1 DN100 … E2 DN120 … E3 DN150»,
        // nicht aus der verschobenen Spalte, in der «150 mm Auslauf» in der Zeile von E1 steht.
        Assert.Equal(new int[] { 150, 100, 120, 150 }, zusatz.Anschluesse.Select(a => a.DnMm!.Value).ToArray());
        Assert.All(zusatz.Anschluesse, a => Assert.Equal("Normalbeton (NB)", a.Material));
        Assert.All(zusatz.Anschluesse, a => Assert.Equal("SchachtPro", a.Quelle));
    }

    [Fact]
    public void Ohne_Skizzenlegende_kommen_die_Durchmesser_aus_der_verschobenen_Spalte()
    {
        var ohneLegende = Regex.Replace(Vorlage8705(), @"\b[AE]\d\s+DN\d{2,4}\b", "");
        Assert.DoesNotContain("DN150", ohneLegende, StringComparison.Ordinal);

        var zusatz = SchachtProtocolZusatzParser.Parse(ohneLegende);

        // Vier Zeilen, vier Zellen «… mm Auslauf/Einlauf» in Tabellenreihenfolge.
        Assert.Equal(new int[] { 150, 100, 120, 150 }, zusatz.Anschluesse.Select(a => a.DnMm!.Value).ToArray());
    }

    [Fact]
    public void Liest_Medium_Materialien_Deckel_und_Tauchbogen_des_SchachtPro_Exports()
    {
        var zusatz = SchachtProtocolZusatzParser.Parse(Vorlage8705());

        Assert.Equal("Schmutzabwasser", zusatz.Medium);
        Assert.Equal("Fertigbetonelement", zusatz.MaterialSchacht);
        Assert.Equal("Beton", zusatz.MaterialDeckel);
        Assert.Equal("500", zusatz.DeckelDurchmesserMm);
        Assert.Equal("nicht notwendig", zusatz.Tauchbogen);
        Assert.Null(zusatz.Steighilfe);
    }

    [Fact]
    public void Das_Uri_Formular_bleibt_beim_bisherigen_Weg()
    {
        var zusatz = SchachtProtocolZusatzParser.Parse(Vorlage80409());

        Assert.Equal(4, zusatz.Anschluesse.Count);
        Assert.All(zusatz.Anschluesse, a => Assert.Null(a.Uhr));
        Assert.All(zusatz.Anschluesse, a => Assert.Equal("PDF", a.Quelle));
        Assert.Equal("660", zusatz.DeckelDurchmesserMm);
    }
}
