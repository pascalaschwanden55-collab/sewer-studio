using AuswertungPro.Next.Infrastructure.Import.Pdf;

namespace AuswertungPro.Next.Infrastructure.Tests;

/// <summary>
/// Die Schachtfunktion wird aus einem Tabellen-PDF gelesen. Der Wert steht in
/// derselben Textzeile wie die naechsten Tabellenspalten — eine Regex bis zum
/// Zeilenende nimmt sie mit.
///
/// Die Beispiele sind echte Werte aus den Projekten Jagdmatt_2026,
/// Feldliweg_6460_Altdorf und Fuerlauwi (Stand 2026-08-30). Dort steht heute
/// in 58 Schaechten die halbe Tabellenzeile im Feld "Funktion".
/// </summary>
public sealed class SchachtFunktionSpaltenTests
{
    [Theory]
    // Grosser Spaltenabstand
    [InlineData(
        "Schachttyp Dachwasserschacht          Deckeltyp                       -                         12",
        "Dachwasserschacht")]
    [InlineData(
        "Schachttyp Kontrollschacht              Deckeltyp               Pickelloch                              12",
        "Kontrollschacht")]
    // Mehrwortige Funktion — hier trennt nur EIN Leerzeichen vor "Deckeltyp"
    [InlineData(
        "Schachttyp Einlaufschacht mit Schlammsammler Deckeltyp               Einlaufroste                   12",
        "Einlaufschacht mit Schlammsammler")]
    [InlineData(
        "Schachttyp Schlammsammler             Deckeltyp                Einlaufroste                        12",
        "Schlammsammler")]
    // Ohne Folgespalte: der Wert bleibt vollstaendig
    [InlineData("Schachttyp Oelabscheider", "Oelabscheider")]
    [InlineData("Schachtfunktion Sickerschacht", "Sickerschacht")]
    public void Die_Funktion_endet_vor_der_naechsten_Tabellenspalte(string zeile, string erwartet)
    {
        var felder = SchachtProtocolParser.ParseSchachtFields(zeile);

        Assert.Equal(erwartet, felder.Funktion);
    }

    [Fact]
    public void Ein_normaler_Wert_bleibt_unveraendert()
    {
        var felder = SchachtProtocolParser.ParseSchachtFields("Schachttyp Kontrollschacht");

        Assert.Equal("Kontrollschacht", felder.Funktion);
    }
}
